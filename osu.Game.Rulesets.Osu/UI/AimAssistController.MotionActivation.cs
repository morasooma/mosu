// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class AimAssistController
    {
        private void updateMovementHistory(Vector2 rawDelta, double elapsed)
        {
            double sampleTime = Time.Current;
            double historyWindow = scaleRealTimeWindow(movement_history_window);
            Vector2 sampleVelocity = elapsed > 0
                ? rawDelta * (1000f / (float)elapsed)
                : Vector2.Zero;

            recentMovement.Enqueue(new MovementSample(sampleTime, sampleVelocity));

            while (recentMovement.Count > 0 && sampleTime - recentMovement.Peek().Time > historyWindow)
                recentMovement.Dequeue();

            if (recentMovement.Count == 0)
            {
                averagedVelocity = sampleVelocity;
                return;
            }

            Vector2 weightedVelocity = Vector2.Zero;
            float totalWeight = 0;

            foreach (MovementSample sample in recentMovement)
            {
                float weight = (float)Math.Clamp(1 - (sampleTime - sample.Time) / historyWindow, 0.1, 1);
                weightedVelocity += sample.Velocity * weight;
                totalWeight += weight;
            }

            averagedVelocity = totalWeight > 0 ? weightedVelocity / totalWeight : sampleVelocity;
        }

        private void updateAntiJitterState(double elapsed)
        {
            bool primaryPressed = inputManager!.PressedActions.Contains(OsuAction.LeftButton) || inputManager.PressedActions.Contains(OsuAction.RightButton);
            double antiJitterDuration = getConfiguredAntiJitterMs();

            if (primaryPressed && !lastPrimaryPressed && antiJitterDuration > 0)
            {
                antiJitterRemaining = scaleRealTimeWindow(antiJitterDuration);
                antiJitterOutputAnchor = CurrentOutputPosition;
            }

            lastPrimaryPressed = primaryPressed;

            if (antiJitterRemaining > 0)
                antiJitterRemaining = Math.Max(0, antiJitterRemaining - elapsed);
        }

        private ActivationState evaluateActivation(AimAssistContext context, Vector2 rawPosition, float strengthFactor)
        {
            bool streamMode = context.ModeName == @"stream";
            float gentleFlowWeight = streamMode && Time.Current >= context.FocusTime
                ? getGentleFlowAssistWeight(context.PatternInfo)
                : 0;
            float rawDistance = streamMode
                ? getStreamCorridorDistance(rawPosition, context)
                : context.DistanceToDesired;
            Vector2 outputPosition = rawPosition + assistOffset;
            Vector2 outputToDesired = context.DesiredPoint - outputPosition;
            ProjectionResult outputProjection = streamMode
                ? getStreamCorridorProjection(outputPosition, context)
                : default;
            float outputDistance = streamMode
                ? (outputProjection.Point - outputPosition).Length
                : outputToDesired.Length;
            float overshootAllowance = getConfiguredOvershootAllowance();
            float fovRadius = getConfiguredFovRadius();
            double intentThreshold = getConfiguredIntentThreshold();
            float holdRadius = context.Radius + overshootAllowance + stationary_hold_radius_padding;

            if (streamMode)
            {
                float streamRadiusScale = context.PatternInfo.StreamSpacing switch
                {
                    OsuStreamSpacingKind.Spaced => 1.34f,
                    OsuStreamSpacingKind.Variable => 1.18f,
                    _ => 0.92f
                };

                if (context.PatternInfo.StreamShape == OsuStreamShapeKind.ZigZag)
                    streamRadiusScale = Math.Max(streamRadiusScale, 1.08f);

                holdRadius = Math.Max(holdRadius, context.Radius * streamRadiusScale + 16f);

            }

            bool withinFov = fovRadius <= 0
                ? rawDistance <= holdRadius
                : rawDistance <= fovRadius;
            bool withinRetainRange = withinFov || outputDistance <= holdRadius;
            Vector2 outputCorrection = streamMode
                ? outputProjection.Point - outputPosition
                : outputToDesired;
            Vector2 outputDirection = outputDistance > 0.001f ? outputCorrection / outputDistance : Vector2.Zero;
            float outwardSpeed = outputDirection.LengthSquared > 0 ? Vector2.Dot(averagedVelocity, -outputDirection) : 0;

            float jumpAssistWeight = getPointJumpAssistWeight(context);

            if (context.ModeName == @"point" && jumpAssistWeight > 0)
            {
                float jumpHoldRadius = context.Radius * (float)Interpolation.Lerp(1.18f, 1.52f, jumpAssistWeight)
                                       + overshootAllowance
                                       + 12f;
                holdRadius = Math.Max(holdRadius, jumpHoldRadius);
            }

            float intentScore = computeIntentScore(context, rawPosition);
            float pointEarlySettleWeight = getPointEarlySettleWeight(context, rawDistance, strengthFactor);
            pointEarlySettleWeight = Math.Max(pointEarlySettleWeight, getIsolatedPointFastSettleWeight(context, rawDistance, intentScore, strengthFactor));
            float pointCaptureReleaseWeight = getPointCaptureReleaseWeight(context, rawPosition);
            float pointCapturePenalty = (float)Interpolation.Lerp(0.98f, 0.4f, jumpAssistWeight);
            pointEarlySettleWeight *= 1 - pointCaptureReleaseWeight * pointCapturePenalty;

            if (intentScore < -0.2f && averagedVelocity.Length > stationary_hold_speed_threshold * 1.1f)
                pointEarlySettleWeight = 0;

            bool pointEarlySettleHold = false;
            bool continuousTrackingMode = context.ModeName is @"slider" or @"slider-repeat" or @"stream";
            bool continuousTrackingHold = continuousTrackingMode
                                          && withinRetainRange
                                          && outputDistance <= holdRadius * (streamMode && context.PatternInfo.StreamShape == OsuStreamShapeKind.ZigZag ? 1.38f : 1.25f)
                                          && outwardSpeed <= stationary_hold_speed_threshold * 1.8f;
            bool stationaryHold = false;

            if (intentScore > -0.95f)
                lastPositiveIntentScore = intentScore;

            float pointSoftThreshold = (float)Interpolation.Lerp(-0.25f, -0.92f, strengthFactor);

            if (jumpAssistWeight > 0)
                pointSoftThreshold = (float)Interpolation.Lerp(pointSoftThreshold, -0.985f, jumpAssistWeight * 0.82f);

            bool pointSoftPass = context.ModeName == @"point"
                                 && withinRetainRange
                                 && intentThreshold <= 0
                                 && intentScore >= pointSoftThreshold;
            bool thresholdPassed = pointSoftPass || continuousTrackingHold || intentScore >= intentThreshold;

            if (withinFov && thresholdPassed)
                lastIntentPassTime = Time.Current;

            double passiveAssistGraceWindow = scaleRealTimeWindow(continuousTrackingMode
                ? intent_grace_window * (streamMode && context.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Spaced ? 3.8 : 3)
                : intent_grace_window);

            bool passiveAssist = context.AllowPassiveAssist
                                 && Time.Current - lastIntentPassTime <= passiveAssistGraceWindow
                                 && withinRetainRange
                                 && outwardSpeed <= (continuousTrackingMode ? stationary_hold_speed_threshold * 1.8f : stationary_hold_speed_threshold);
            bool passed = withinRetainRange && (thresholdPassed || passiveAssist);
            float engageAmount = 0;

            if (passed)
            {
                float fovWeight = fovRadius <= 0
                    ? 1
                    : Math.Clamp(1 - rawDistance / Math.Max(1, fovRadius), 0, 1);
                float proximityWeight = (float)Math.Pow(fovWeight, context.ModeName == @"point"
                    ? Interpolation.Lerp(0.08f, 0.015f, strengthFactor)
                    : 0.35f);

                float timingWeight = getTimingWeight(context, strengthFactor);
                float timingAuthority = context.ModeName == @"point"
                    ? (float)Math.Pow(timingWeight, Interpolation.Lerp(1.85f, 1.25f, strengthFactor))
                    : timingWeight;
                timingAuthority = Math.Max(timingAuthority, pointEarlySettleWeight);
                float intentWeight = thresholdPassed
                    ? Math.Clamp((intentScore + 1) / 2, context.ModeName == @"point"
                        ? (float)Interpolation.Lerp(0.45f, 0.92f, strengthFactor)
                        : 0.15f, 1)
                    : context.ModeName == @"point"
                        ? (float)Interpolation.Lerp(0.5f, 0.85f, strengthFactor)
                        : 0.45f;

                float strengthWeight = strengthFactor * (float)Interpolation.Lerp(
                    context.ModeName == @"point" ? 0.6f : 0.35f,
                    context.ModeName == @"point" ? 1.85f : 1f,
                    strengthFactor) * (context.ModeName == @"point" ? 1.9f : 1.35f);
                engageAmount = Math.Clamp(strengthWeight * proximityWeight * timingAuthority * intentWeight, 0, 1);

                if (context.ModeName == @"point")
                {
                    float pointCaptureEngagePenalty = context.PreviewPoint.HasValue || context.PointFlowBias > 0 ? 0.96f : 0.86f;
                    pointCaptureEngagePenalty *= (float)Interpolation.Lerp(1f, 0.55f, strengthFactor);
                    pointCaptureEngagePenalty = (float)Interpolation.Lerp(pointCaptureEngagePenalty, 0.28f, jumpAssistWeight);
                    engageAmount *= 1 - pointCaptureReleaseWeight * pointCaptureEngagePenalty;

                    float authorityFloor = (float)Interpolation.Lerp(0.48f, 0.96f, strengthFactor)
                                           * timingAuthority
                                           * Math.Max(fovWeight, 0.86f);

                    if (pointEarlySettleWeight > 0)
                        authorityFloor = Math.Max(authorityFloor, pointEarlySettleWeight * (float)Interpolation.Lerp(0.82f, 1.12f, strengthFactor));

                    authorityFloor *= 1 - pointCaptureReleaseWeight * pointCapturePenalty;

                    if (jumpAssistWeight > 0)
                        authorityFloor = Math.Min(1, authorityFloor * (float)Interpolation.Lerp(1f, 1.18f, jumpAssistWeight));

                    engageAmount = Math.Max(engageAmount, Math.Min(1, authorityFloor));
                }

                // Global minimum engage for high-strength jump/flow configurations only
                if (strengthFactor > 0.5f && context.ModeName == @"point" && (jumpAssistWeight > 0.1f || context.PointFlowBias > 0.2f || context.PatternInfo.ContinuityWeight > 0.5f))
                {
                    float difficultyWeight = Math.Max(jumpAssistWeight, Math.Max(context.PointFlowBias, context.PatternInfo.ContinuityWeight * 0.8f));
                    float globalMinEngage = (float)Interpolation.Lerp(0f, 0.18f, Math.Clamp((strengthFactor - 0.5f) * 2f, 0, 1)) * difficultyWeight;
                    engageAmount = Math.Max(engageAmount, globalMinEngage * fovWeight);
                }
            }

            return new ActivationState(passed, intentScore, engageAmount, pointEarlySettleWeight, pointCaptureReleaseWeight);
        }

        private float getPointEarlySettleWeight(AimAssistContext context, float rawDistance, float strengthFactor)
        {
            if (context.ModeName != @"point")
                return 0;

            double delta = context.FocusTime - Time.Current;

            if (delta <= 0)
                return 0;

            double greatWindow = Math.Max(18, context.Drawable.HitObject.HitWindows?.WindowFor(HitResult.Great) ?? 50);
            double settleLead = scaleRealTimeWindow(Math.Clamp(Interpolation.Lerp(greatWindow * 9.6, greatWindow * 6.9, strengthFactor), 210, 460));

            if (delta > settleLead)
                return 0;

            float settleRadius = Math.Max(
                context.Radius * (float)Interpolation.Lerp(2.7f, 4.1f, strengthFactor),
                context.Radius + getConfiguredOvershootAllowance() + 24f);
            float distanceWeight = Math.Clamp(1 - rawDistance / settleRadius, 0, 1);

            if (distanceWeight <= 0)
                return 0;

            float timeProgress = 1 - (float)Math.Clamp(delta / settleLead, 0, 1);
            float timeWeight = (float)Math.Pow(timeProgress, Interpolation.Lerp(1.3f, 0.9f, strengthFactor));
            float authorityCap = (float)Interpolation.Lerp(0.24f, 0.58f, strengthFactor);

            return distanceWeight * timeWeight * authorityCap;
        }

        private float getIsolatedPointFastSettleWeight(AimAssistContext context, float rawDistance, float intentScore, float strengthFactor)
        {
            if (context.ModeName != @"point" || context.Drawable is not DrawableHitCircle || context.PreviewPoint != null)
                return 0;

            double delta = context.FocusTime - Time.Current;

            if (delta <= 0)
                return 0;

            double greatWindow = Math.Max(18, context.Drawable.HitObject.HitWindows?.WindowFor(HitResult.Great) ?? 50);
            double settleLead = scaleRealTimeWindow(Math.Clamp(Interpolation.Lerp(greatWindow * 8.2, greatWindow * 6.1, strengthFactor), 180, 360));

            if (delta > settleLead)
                return 0;

            float settleRadius = Math.Max(
                context.Radius * (float)Interpolation.Lerp(3.8f, 5.9f, strengthFactor),
                context.Radius + getConfiguredOvershootAllowance() + 38f);
            float distanceWeight = Math.Clamp(1 - rawDistance / settleRadius, 0, 1);

            if (distanceWeight <= 0)
                return 0;

            float intentWeight = Math.Clamp((intentScore + 0.35f) / 1.35f, 0, 1);
            float speedWeight = Math.Clamp((averagedVelocity.Length - stationary_hold_speed_threshold * 0.75f) / 360f, 0, 1);
            float activationWeight = Math.Max(intentWeight, speedWeight * 0.85f);

            if (activationWeight <= 0)
                return 0;

            float timeProgress = 1 - (float)Math.Clamp(delta / settleLead, 0, 1);
            float timeWeight = (float)Math.Pow(timeProgress, Interpolation.Lerp(1.05f, 0.65f, strengthFactor));
            float authorityCap = (float)Interpolation.Lerp(0.32f, 0.68f, strengthFactor);

            return distanceWeight * activationWeight * timeWeight * authorityCap;
        }

        private float getPointCaptureReleaseWeight(AimAssistContext context, Vector2 rawPosition)
        {
            if (context.ModeName != @"point")
                return 0;

            Vector2 targetCentre = getDisplayCentre(context.Drawable);
            Vector2 outputPosition = rawPosition + assistOffset;
            float baseRadius = getDrawableTargetRadius(context.Drawable, context.Drawable.HitObject);

            if (baseRadius <= 0.01f)
                return 0;

            float rawDistanceToTarget = (rawPosition - targetCentre).Length;
            float outputDistanceToTarget = (outputPosition - targetCentre).Length;
            float captureRadius = baseRadius + Math.Min(getConfiguredOvershootAllowance() * 0.22f, baseRadius * 0.18f);
            float outputCaptureRadius = captureRadius + Math.Max(4f, baseRadius * 0.12f);
            float rawInsideWeight = Math.Clamp(1 - rawDistanceToTarget / Math.Max(1f, captureRadius), 0, 1);
            float outputInsideWeight = Math.Clamp(1 - outputDistanceToTarget / Math.Max(1f, outputCaptureRadius), 0, 1);

            if (rawInsideWeight <= 0 || outputInsideWeight <= 0)
                return 0;

            float movementWeight = Math.Clamp(1 - averagedVelocity.Length / (stationary_hold_speed_threshold * 3.2f), 0.18f, 1);
            float captureWeight = rawInsideWeight * outputInsideWeight * movementWeight;

            if (context.PreviewPoint.HasValue || context.PointFlowBias > 0)
            {
                float flowCaptureCap = context.PatternInfo.Kind switch
                {
                    OsuPatternKind.Burst => 0.46f,
                    OsuPatternKind.Stack => 0.32f,
                    _ => 0.68f
                };
                captureWeight = Math.Min(Math.Max(captureWeight, rawInsideWeight * 0.72f), flowCaptureCap);
            }

            return Math.Clamp(captureWeight, 0, 1);
        }

        private float computeIntentScore(AimAssistContext context, Vector2 rawPosition)
        {
            Vector2 movement = averagedVelocity;

            if (movement.LengthSquared <= 0.0001f)
                return -1;

            Vector2 movementDirection = normaliseOrZero(movement);
            Vector2 targetDirection = context.IntentDirection;
            float directIntent = targetDirection.LengthSquared > 0 ? Vector2.Dot(movementDirection, targetDirection) : -1;
            float tangentIntent = context.Tangent.LengthSquared > 0 ? Vector2.Dot(movementDirection, context.Tangent) : -1;

            if (context.ModeName != @"point")
                tangentIntent = Math.Max(tangentIntent, directIntent * 0.9f);

            return Math.Max(directIntent, tangentIntent);
        }

        private float getTimingWeight(AimAssistContext context, float strengthFactor)
        {
            double delta = context.FocusTime - Time.Current;

            if (context.ModeName == @"stream")
            {
                float continuityWeight = Math.Clamp((context.PatternInfo.ContinuityWeight - 0.32f) / 0.68f, 0.55f, 1f);
                float densityWeight = context.PatternInfo.StreamSpacing switch
                {
                    OsuStreamSpacingKind.Spaced => 0.94f,
                    OsuStreamSpacingKind.Variable => 0.92f,
                    _ => 0.98f
                };
                float shapeWeight = context.PatternInfo.StreamShape == OsuStreamShapeKind.ZigZag ? 0.94f : 1f;

                return Math.Clamp(continuityWeight * densityWeight * shapeWeight, 0.58f, 1f);
            }

            if (context.ModeName == @"point")
            {
                double greatWindow = Math.Max(18, context.Drawable.HitObject.HitWindows?.WindowFor(HitResult.Great) ?? 50);
                double engageLead = scaleRealTimeWindow(Math.Clamp(Interpolation.Lerp(greatWindow * 4.2, greatWindow * 2.4, strengthFactor), 70, 180));
                double postHitRetention = scaleRealTimeWindow(Math.Clamp(greatWindow * 1.35, 24, 90));

                if (delta >= 0)
                {
                    if (delta >= engageLead)
                        return (float)Interpolation.Lerp(0f, 0.03f, strengthFactor);

                    float progress = 1 - (float)Math.Clamp(delta / engageLead, 0, 1);
                    float ramp = (float)Math.Pow(progress, Interpolation.Lerp(2.2f, 1.45f, strengthFactor));
                    float minimumWeight = (float)Interpolation.Lerp(0.02f, 0.06f, strengthFactor);
                    return (float)Interpolation.Lerp(minimumWeight, 1f, ramp);
                }

                return (float)Interpolation.Lerp(1f, 0.62f, Math.Clamp(-delta / postHitRetention, 0.0, 1.0));
            }

            if (context.AllowPassiveAssist && delta <= 0)
                return 1;

            if (delta >= 0)
            {
                double preempt = Math.Max(120, context.PreemptTime);
                float minimumWeight = 0.35f;
                return (float)Interpolation.Lerp(minimumWeight, 1f, 1 - Math.Clamp(delta / preempt, 0, 1));
            }

            return (float)Interpolation.Lerp(1f, 0.45f, Math.Clamp(-delta / scaleRealTimeWindow(140), 0, 1));
        }
    }
}
