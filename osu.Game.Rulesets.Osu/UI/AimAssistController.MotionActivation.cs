// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class AimAssistController
    {
        private void updateMovementHistory(Vector2 rawDelta, double elapsed)
        {
            double realElapsed = toRealTimeWindow(elapsed);
            Vector2 sampleVelocity = realElapsed > 0 ? rawDelta * (1000f / (float)realElapsed) : Vector2.Zero;

            // Непрерывные фильтры не зависят от количества кадров в окне истории.
            fastRawVelocity = interpolate(fastRawVelocity, sampleVelocity, (float)(1 - Math.Exp(-realElapsed / 7)));
            slowRawVelocity = interpolate(slowRawVelocity, sampleVelocity, (float)(1 - Math.Exp(-realElapsed / 24)));

            // Торможение и разворот важнее старой средней скорости.
            bool braking = fastRawVelocity.LengthSquared < slowRawVelocity.LengthSquared
                           || Vector2.Dot(fastRawVelocity, slowRawVelocity) < 0;
            averagedVelocity = braking ? fastRawVelocity : (fastRawVelocity + slowRawVelocity) * 0.5f;
        }

        private void updateAntiJitterState(double elapsed)
        {
            bool primaryPressed = inputManager!.PressedActions.Contains(OsuAction.LeftButton) || inputManager.PressedActions.Contains(OsuAction.RightButton);
            double antiJitterDuration = getConfiguredAntiJitterMs();

            if (primaryPressed && !lastPrimaryPressed && antiJitterDuration > 0)
            {
                antiJitterRemaining = scaleRealTimeWindow(antiJitterDuration);
            }

            lastPrimaryPressed = primaryPressed;

            if (antiJitterRemaining > 0)
                antiJitterRemaining = Math.Max(0, antiJitterRemaining - elapsed);
        }

        private ActivationState evaluateActivation(AimAssistContext context, Vector2 rawPosition, float strengthFactor)
        {
            bool pointMode = context.ModeName == @"point";
            bool streamMode = context.ModeName == @"stream";
            if (motionTargetExited && (pointMode || streamMode))
                return default;

            Vector2 centre = getDisplayCentre(context.Drawable);
            Vector2 output = rawPosition + assistOffset;
            float radius = getDrawableTargetRadius(context.Drawable, context.Drawable.HitObject);
            float fieldRadius = getTargetGravityRadius(context);
            float rawDistance = pointMode ? GetPredictedPointMissDistance(rawPosition, centre, averagedVelocity, getPointAcquisitionPredictionHorizon(context))
                : streamMode ? getStreamCorridorDistance(rawPosition, context) : context.DistanceToDesired;
            float outputDistance = pointMode ? (centre - output).Length
                : streamMode ? getStreamCorridorDistance(output, context) : (context.DesiredPoint - output).Length;

            Vector2 movement = fastRawVelocity;
            bool moving = movement.Length > stationary_hold_speed_threshold;
            Vector2 targetDirection = pointMode ? normaliseOrZero(centre - rawPosition) : context.IntentDirection;
            float intent = moving ? Vector2.Dot(normaliseOrZero(movement), targetDirection) : -1;
            if (!pointMode && moving && context.Tangent.LengthSquared > 0)
                intent = Math.Max(intent, Vector2.Dot(normaliseOrZero(movement), normaliseOrZero(context.Tangent)));

            bool approaching = moving && intent >= Math.Max(0, getConfiguredIntentThreshold());
            bool trackingHold = !pointMode && !moving && outputDistance <= radius;
            bool capturedHold = pointMode && motionTargetCaptured && !moving && outputDistance <= radius;
            double untilHit = toRealTimeWindow(context.FocusTime - Time.Current);
            float timing = GetApproachTimingWeight(untilHit, getGreatWindow(context.Drawable), context.PreemptTime);
            float pointConfidence = 0;

            if (pointMode && moving)
            {
                float competitorEvidence = 0;

                if (context.PreviewPoint.HasValue)
                {
                    float previewRadius = context.PreviewRadius > 0 ? context.PreviewRadius : radius;
                    float previewMiss = GetPredictedPointMissDistance(rawPosition, context.PreviewPoint.Value, averagedVelocity, getPointAcquisitionPredictionHorizon(context));
                    float previewDirection = Vector2.Dot(normaliseOrZero(movement), normaliseOrZero(context.PreviewPoint.Value - rawPosition));
                    competitorEvidence = GetPointIntentEvidence(previewMiss, previewRadius, fieldRadius, previewDirection, timing) * 0.72f;
                }

                pointConfidence = GetPointIntentConfidence(rawDistance, radius, fieldRadius, intent, timing, competitorEvidence);
            }

            bool withinField = pointMode
                ? ShouldActivatePointTrajectory(rawDistance, fieldRadius, intent, (float)getConfiguredIntentThreshold(), pointConfidence)
                : rawDistance <= fieldRadius;
            if (withinField && approaching)
                lastIntentPassTime = Time.Current;

            bool settling = !moving && context.AllowPassiveAssist
                            && Time.Current - lastIntentPassTime <= scaleRealTimeWindow(intent_grace_window);
            bool passed = (withinField || capturedHold || trackingHold)
                          && (approaching || settling || capturedHold || trackingHold);
            if (!passed)
                return new ActivationState(false, intent, 0);

            // Сила почти постоянна внутри поля; плавно затухает лишь у его внешней границы.
            // Ошибка доведения измеряется до настоящего круга, не до смещённой точки предпросмотра.
            float proximity = capturedHold || trackingHold || pointMode ? 1 : getGravityFieldFalloff(rawDistance, fieldRadius);
            float intentWeight = approaching ? 0.7f + 0.3f * Math.Clamp(intent, 0, 1) : 1;
            float engage = GetPointCorrectionWeight(strengthFactor, proximity, timing, intentWeight);
            return new ActivationState(engage > 0, intent, Math.Clamp(engage, 0, 1));
        }

        internal static bool ShouldActivatePointTrajectory(float predictedMissDistance, float assistanceRadius, float directionAlignment,
                                                           float configuredIntentThreshold, float confidence)
        {
            if (predictedMissDistance > Math.Max(0, assistanceRadius))
                return false;

            if (confidence >= 0.12f)
                return true;

            // Negative intent thresholds in stronger profiles deliberately allow recovery of a
            // plausible off-axis miss. Requiring positive confidence here made those profiles
            // behave almost identically to low strength because confidence already embeds direction.
            return configuredIntentThreshold < 0 && directionAlignment >= configuredIntentThreshold;
        }

        internal static float GetApproachTimingWeight(double timeUntilHit, double greatWindow, double preemptTime = 0)
        {
            double fullAuthorityLead = Math.Max(100, greatWindow * 3);
            double hitWindowAcquisitionLead = Math.Max(fullAuthorityLead + 1, greatWindow * 6);
            double movementAcquisitionLead = preemptTime > 0
                ? Math.Max(fullAuthorityLead + 1, preemptTime * 0.72)
                : 0;
            double acquisitionLead = Math.Max(hitWindowAcquisitionLead, movementAcquisitionLead);

            if (timeUntilHit <= fullAuthorityLead)
                return 1;

            return (float)Math.Clamp((acquisitionLead - timeUntilHit) / (acquisitionLead - fullAuthorityLead), 0, 1);
        }

        internal static float GetPointCorrectionWeight(float strengthFactor, float proximity, float timing, float intentWeight)
            => Math.Clamp(strengthFactor, 0, 1)
               * Math.Clamp(proximity, 0, 1)
               * Math.Clamp(timing, 0, 1)
               * Math.Clamp(intentWeight, 0, 1);

        internal static float GetPointIntentConfidence(float predictedMissDistance, float hitboxRadius, float assistanceRadius,
                                                       float directionAlignment, float timingWeight, float competitorConfidence)
        {
            float evidence = GetPointIntentEvidence(predictedMissDistance, hitboxRadius, assistanceRadius, directionAlignment, timingWeight);

            // Confidence is a margin over the best competing target, not merely closeness to this target.
            // A clear winner keeps its evidence; near-ties collapse quickly instead of causing target ping-pong.
            float separation = Math.Clamp((evidence - Math.Clamp(competitorConfidence, 0, 1)) / 0.35f, 0, 1);
            return evidence * separation;
        }

        internal static float GetPointIntentEvidence(float predictedMissDistance, float hitboxRadius, float assistanceRadius,
                                                     float directionAlignment, float timingWeight)
        {
            float correctionNeed = Math.Max(0, predictedMissDistance - Math.Max(0, hitboxRadius));
            float correctionReach = Math.Max(1, assistanceRadius - Math.Max(0, hitboxRadius));
            float trajectory = 1 - Math.Clamp(correctionNeed / correctionReach, 0, 1);
            float direction = Math.Clamp((directionAlignment + 0.15f) / 1.15f, 0, 1);
            return trajectory * direction * Math.Clamp(timingWeight, 0, 1);
        }

        private void updateAssistState(AimAssistContext context, Vector2 rawPosition)
        {
            if (context.ModeName is @"slider" or @"slider-repeat")
            {
                hasInsideFreedomLatch = false;
                CurrentAssistStateName = @"slider-track";
                return;
            }

            Vector2 output = rawPosition + assistOffset;

            if (hasInsideFreedomLatch)
            {
                if ((output - insideFreedomCentre).LengthSquared <= insideFreedomRadius * insideFreedomRadius)
                {
                    CurrentAssistStateName = @"inside";
                    return;
                }

                hasInsideFreedomLatch = false;
            }

            if (context.ModeName != @"point" && context.ModeName != @"stream")
            {
                CurrentAssistStateName = @"handoff";
                return;
            }

            Vector2 centre = getDisplayCentre(context.Drawable);
            float radius = getDrawableTargetRadius(context.Drawable, context.Drawable.HitObject);
            bool outputInside = (output - centre).LengthSquared <= radius * radius;

            if (outputInside && context.Drawable is DrawableHitCircle)
            {
                hasInsideFreedomLatch = true;
                insideFreedomCentre = centre;
                insideFreedomRadius = radius;
            }

            CurrentAssistStateName = outputInside
                ? @"inside"
                : motionTargetExited ? @"exit" : @"acquire";
        }

        private float getTargetGravityRadius(AimAssistContext context)
        {
            float radius = getDrawableTargetRadius(context.Drawable, context.Drawable.HitObject);
            float configured = getConfiguredFovRadius();
            float baseRadius = Math.Max(radius, configured > 0 ? configured : radius + getConfiguredOvershootAllowance());
            float timeUntilHit = (float)toRealTimeWindow(context.FocusTime - Time.Current);
            return GetMotionAwareAssistRadius(baseRadius, fastRawVelocity.Length, timeUntilHit, getStrengthFactor(context));
        }

        private float getPointAcquisitionPredictionHorizon(AimAssistContext context)
        {
            float timeUntilHit = Math.Max(0, (float)toRealTimeWindow(context.FocusTime - Time.Current) / 1000f);
            return Math.Min(0.18f, timeUntilHit);
        }

        internal static float GetPredictedPointMissDistance(Vector2 position, Vector2 target, Vector2 velocity, float predictionHorizon)
        {
            Vector2 toTarget = target - position;
            float velocitySquared = velocity.LengthSquared;
            Vector2 closestPosition = position;

            if (velocitySquared > 1)
            {
                float secondsToClosest = Math.Clamp(Vector2.Dot(toTarget, velocity) / velocitySquared, 0, Math.Max(0, predictionHorizon));
                closestPosition += velocity * secondsToClosest;
            }

            return (target - closestPosition).Length;
        }

        internal static float GetMotionAwareAssistRadius(float baseRadius, float speed, float timeUntilHit, float strength)
        {
            if (baseRadius <= 0 || speed < 700 || timeUntilHit < 24 || timeUntilHit > 210)
                return Math.Max(0, baseRadius);

            float speedWeight = Math.Clamp((speed - 700) / 1500, 0, 1);
            float timingWeight = Math.Clamp(1 - Math.Abs(timeUntilHit - 95) / 115, 0, 1);
            float expansion = Math.Min(160, speed * 0.08f) * speedWeight * timingWeight * Math.Clamp(strength, 0, 1);
            return baseRadius + expansion;
        }

        private static float getGravityFieldFalloff(float distance, float fieldRadius)
        {
            float weight = Math.Clamp((fieldRadius - distance) / Math.Max(1, fieldRadius * 0.25f), 0, 1);
            return weight * weight * (3 - 2 * weight);
        }
    }
}
