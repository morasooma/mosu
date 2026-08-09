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
        private Vector2 computeEngagedOffset(AimAssistContext context, ActivationState activation, Vector2 rawPosition, float strengthFactor)
        {
            Vector2 outputPosition = rawPosition + assistOffset;
            Vector2 toDesired = context.DesiredPoint - outputPosition;
            float distanceToDesired = toDesired.Length;
            float jumpAssistWeight = getPointJumpAssistWeight(context);
            float overshootAllowance = getConfiguredOvershootAllowance();
            float fovRadius = getConfiguredFovRadius();

            if (context.ModeName == @"stream")
                return computeStreamEngagedOffset(context, activation, rawPosition, outputPosition, strengthFactor);

            if (distanceToDesired <= 0.001f)
                return assistOffset;

            Vector2 radialDirection = toDesired / distanceToDesired;
            float targetRadius = (context.Radius + overshootAllowance) * (1 - strengthFactor * (context.ModeName == @"point"
                ? (0.85f + 0.15f * activation.EngageAmount)
                : (0.35f + 0.65f * activation.EngageAmount)));
            float pointFlowContinuationWeight = getPointFlowContinuationWeight(context, rawPosition);
            float targetSwitchContinuationWeight = getRecentTargetSwitchCarryWeight(context);
            float pointCaptureReleaseWeight = activation.PointCaptureReleaseWeight;
            float effectivePointCaptureReleaseWeight = pointCaptureReleaseWeight * (float)Interpolation.Lerp(1f, 0.26f, jumpAssistWeight);
            float pointRecenteringBias = getPointRecenteringBias(context, pointFlowContinuationWeight, strengthFactor);

            if (context.ModeName == @"point")
            {
                float loosePointRadius = context.Radius + overshootAllowance;
                targetRadius = (float)Interpolation.Lerp(loosePointRadius, targetRadius, pointRecenteringBias);

                if (context.PointFlowBias > 0)
                    targetRadius = (float)Interpolation.Lerp(loosePointRadius, targetRadius, 1 - context.PointFlowBias * 0.72f);

                if (jumpAssistWeight > 0)
                {
                    float jumpTightenWeight = jumpAssistWeight * (float)Interpolation.Lerp(0.18f, 0.34f, activation.EngageAmount);
                    targetRadius = (float)Interpolation.Lerp(loosePointRadius, targetRadius, jumpTightenWeight);
                }

                if (effectivePointCaptureReleaseWeight > 0)
                    targetRadius = (float)Interpolation.Lerp(targetRadius, loosePointRadius, effectivePointCaptureReleaseWeight * 0.96f);

                if (targetSwitchContinuationWeight > 0)
                    targetRadius = (float)Interpolation.Lerp(targetRadius, loosePointRadius, targetSwitchContinuationWeight * 0.72f);
            }

            if (context.ModeName == @"slider-repeat")
            {
                float repeatDeadzone = context.Radius * (float)Interpolation.Lerp(0.18f, 0.32f, 1 - activation.EngageAmount);
                targetRadius = Math.Min(targetRadius, repeatDeadzone + overshootAllowance);
            }

            if (context.ModeName == @"slider")
            {
                float trackingDeadzone = Math.Max(6f, context.Radius * 0.24f);
                targetRadius = Math.Min(targetRadius, trackingDeadzone + overshootAllowance);
            }

            targetRadius = Math.Max(0, targetRadius);
            float effectiveDistance = Math.Max(0, distanceToDesired - targetRadius);
            Vector2 steeringPoint = outputPosition + radialDirection * effectiveDistance;
            Vector2 desiredOffset = steeringPoint - rawPosition;

            if (context.ModeName == @"point")
            {
                float loosePointRadius = context.Radius + overshootAllowance;
                float exitIntentWeight = getPointExitIntentWeight(context, rawPosition);
                float pointFlowRelaxation = Math.Max(pointFlowContinuationWeight, context.PointFlowBias * 0.74f);
                pointFlowRelaxation = Math.Max(pointFlowRelaxation, effectivePointCaptureReleaseWeight);
                float userFreedom = 1 - pointRecenteringBias;
                float insideNoteWeight = userFreedom <= 0 || loosePointRadius <= 0
                    ? 0
                    : Math.Clamp(1 - distanceToDesired / loosePointRadius, 0, 1);

                if (insideNoteWeight > 0)
                {
                    float releaseWeight = insideNoteWeight * userFreedom * (1 - exitIntentWeight * 0.94f) * (1 - pointFlowRelaxation * 0.9f);

                    if (jumpAssistWeight > 0)
                        releaseWeight *= 1 - jumpAssistWeight * 0.78f;

                    // Reduce "hold inside note" pull when user is actively moving the mouse — prevents feeling of cursor being slowed/stuck
                    float movementReleaseBoost = Math.Clamp(averagedVelocity.Length / (stationary_hold_speed_threshold * 3f), 0f, 1f);
                    releaseWeight *= (1f - movementReleaseBoost * 0.7f);

                    if (context.PointFlowBias > 0 || context.PatternInfo.Kind is OsuPatternKind.Burst or OsuPatternKind.Stack)
                    {
                        float continuityHold = Math.Max(context.PointFlowBias, context.PatternInfo.ContinuityWeight);
                        Vector2 heldOffset = interpolate(CurrentOutputPosition - rawPosition, assistOffset, 0.34f);
                        float holdWeight = releaseWeight * (float)Interpolation.Lerp(0.48f, 0.86f, continuityHold);
                        desiredOffset = interpolate(desiredOffset, heldOffset, holdWeight);
                    }
                    else
                        desiredOffset = interpolate(desiredOffset, Vector2.Zero, releaseWeight);
                }

                Vector2 centredOffset = context.DesiredPoint - rawPosition;
                float centreBias = Math.Clamp(
                    (float)Interpolation.Lerp(0.3f, 0.7f, strengthFactor) * (0.5f + 0.5f * activation.EngageAmount),
                    0,
                    0.85f);
                centreBias *= pointRecenteringBias;
                centreBias *= 1 - exitIntentWeight * 0.82f;
                centreBias *= 1 - pointFlowRelaxation * 0.94f;

                if (jumpAssistWeight > 0)
                    centreBias = Math.Min(0.995f, centreBias + jumpAssistWeight * (float)Interpolation.Lerp(0.04f, 0.14f, strengthFactor));

                if (context.PatternInfo.Kind == OsuPatternKind.Stack)
                {
                    centreBias *= 0.05f;
                }

                desiredOffset = interpolate(desiredOffset, centredOffset, centreBias);

                if (effectivePointCaptureReleaseWeight > 0)
                {
                    if (context.PointFlowBias > 0 || context.PatternInfo.Kind is OsuPatternKind.Burst or OsuPatternKind.Stack)
                        desiredOffset = interpolate(desiredOffset, assistOffset, effectivePointCaptureReleaseWeight * 0.72f);
                    else
                        desiredOffset = interpolate(desiredOffset, Vector2.Zero, effectivePointCaptureReleaseWeight * 0.98f);
                }

                if (pointFlowContinuationWeight > 0)
                {
                    Vector2 previewDirection = getPointPreviewDirection(context);

                    if (previewDirection.LengthSquared > 0.0001f && context.PreviewPoint.HasValue)
                    {
                        float previewLeadDistance = Math.Min(context.Radius * 0.95f, (context.PreviewPoint.Value - context.DesiredPoint).Length * 0.24f);
                        Vector2 continuousPoint = context.DesiredPoint + previewDirection * previewLeadDistance;
                        Vector2 continuousOffset = continuousPoint - rawPosition;
                        desiredOffset = interpolate(desiredOffset, continuousOffset, pointFlowContinuationWeight * 0.68f);
                    }

                    desiredOffset = interpolate(desiredOffset, assistOffset, pointFlowContinuationWeight * 0.3f);
                }

                if (targetSwitchContinuationWeight > 0)
                {
                    float macroCarryWeight = (float)Interpolation.Lerp(0.42f, 0.74f, Math.Max(context.PointFlowBias, targetSwitchContinuationWeight));
                    desiredOffset = interpolate(desiredOffset, targetSwitchAnchorOffset, targetSwitchContinuationWeight * macroCarryWeight);
                }

                float stabilityRadius = Math.Max(context.Radius * 0.26f, 5f);
                
                if (context.PatternInfo.Kind == OsuPatternKind.Stack)
                {
                    stabilityRadius = context.Radius * 1.5f;
                }

                float stabilityWeight = Math.Clamp(1 - distanceToDesired / stabilityRadius, 0, 1)
                                        * Math.Clamp(1 - averagedVelocity.Length / (stationary_hold_speed_threshold * 2.4f), 0, 1);
                stabilityWeight *= 1 - exitIntentWeight * 0.94f;
                stabilityWeight *= 1 - pointFlowRelaxation * 0.95f;

                if (stabilityWeight > 0)
                {
                    Vector2 settledOffset = CurrentOutputPosition - rawPosition;
                    desiredOffset = interpolate(desiredOffset, settledOffset, stabilityWeight * 0.74f);
                }

                // Coherence dampening: prevent conflicting correction systems from creating reversal loops
                {
                    Vector2 currentDir = normaliseOrZero(assistOffset);
                    Vector2 desiredDir = normaliseOrZero(desiredOffset);

                    if (currentDir.LengthSquared > 0.0001f && desiredDir.LengthSquared > 0.0001f && assistOffset.LengthSquared > 4f)
                    {
                        float dirAlign = Vector2.Dot(currentDir, desiredDir);

                        if (dirAlign < -0.25f)
                        {
                            float conflictSeverity = Math.Clamp((-0.25f - dirAlign) / 0.95f, 0, 1);
                            desiredOffset = interpolate(desiredOffset, assistOffset, conflictSeverity * 0.32f);
                        }
                    }
                }
            }

            if (context.Drawable is DrawableSlider slider && isSliderHeadHit(slider))
            {
                double handoffWindow = getSliderHeadHandoffWindow(slider);
                double handoffProgress = Math.Clamp((Time.Current - slider.HitObject.StartTime) / handoffWindow, 0, 1);
                float handoffWeight = (float)(1 - handoffProgress);

                if (handoffWeight > 0)
                {
                    Vector2 handoffOffset = CurrentOutputPosition - rawPosition;
                    desiredOffset = interpolate(desiredOffset, handoffOffset, handoffWeight * 0.82f);
                }
            }

            float steeringBlend = Math.Clamp(activation.EngageAmount * (float)Interpolation.Lerp(
                context.ModeName == @"point" ? 2.05f : 1.1f,
                context.ModeName == @"point" ? 3.8f : 1.55f,
                strengthFactor), 0, 1);
            Vector2 blendedOffset = interpolate(assistOffset, desiredOffset, steeringBlend);



            if (context.ModeName == @"point" && activation.PointEarlySettleWeight > 0)
            {
                float candidateDistance = (context.DesiredPoint - (rawPosition + blendedOffset)).Length;

                if (candidateDistance > distanceToDesired + 0.25f)
                    blendedOffset = assistOffset;
            }

            if (context.ModeName == @"point")
            {
                Vector2 currentToDesired = context.DesiredPoint - outputPosition;
                Vector2 candidateToDesired = context.DesiredPoint - (rawPosition + blendedOffset);
                float candidateDistance = candidateToDesired.Length;
                float overshootBrakeRadius = Math.Max(8f, context.Radius * 0.42f + overshootAllowance * 0.25f);

                if (currentToDesired.LengthSquared > 0.001f
                    && candidateToDesired.LengthSquared > 0.001f
                    && distanceToDesired <= context.Radius + overshootAllowance + 20f)
                {
                    float directionalFlip = Vector2.Dot(currentToDesired / distanceToDesired, candidateToDesired / candidateDistance);

                    if (directionalFlip < 0)
                    {
                        float closenessWeight = Math.Clamp(1 - candidateDistance / overshootBrakeRadius, 0, 1);
                        float overshootBrake = Math.Clamp(-directionalFlip, 0, 1) * closenessWeight;

                        if (overshootBrake > 0)
                            blendedOffset = interpolate(blendedOffset, assistOffset, overshootBrake * 0.72f);
                    }
                }
            }

            float maxOffset = Math.Max(
                context.Radius + overshootAllowance + (float)Interpolation.Lerp(
                    context.ModeName == @"point" ? 150f : 28f,
                    context.ModeName == @"point" ? 420f : 96f,
                    strengthFactor),
                fovRadius * (float)Interpolation.Lerp(context.ModeName == @"point" ? 1.05f : 0.35f, context.ModeName == @"point" ? 1.55f : 1.15f, strengthFactor));

            // Further reduce allowed pull distance on very small notes (strengthFactor is already scaled)
            float smallNoteScale = Math.Clamp(context.Radius / 30f, 0.30f, 1f);
            maxOffset *= Math.Clamp(smallNoteScale * 1.1f, 0.5f, 1f);

            return clampLength(blendedOffset, maxOffset);
        }

        private Vector2 computeStreamEngagedOffset(AimAssistContext context, ActivationState activation, Vector2 rawPosition, Vector2 outputPosition, float strengthFactor)
        {
            float overshootAllowance = getConfiguredOvershootAllowance();
            float fovRadius = getConfiguredFovRadius();
            ProjectionResult corridorProjection = getStreamCorridorProjection(outputPosition, context);
            Vector2 tangentDirection = normaliseOrZero(corridorProjection.Tangent);
            bool spacedStream = context.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Spaced;
            bool variableStream = context.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Variable;
            bool zigZagStream = context.PatternInfo.StreamShape == OsuStreamShapeKind.ZigZag;
            float naturalCenteringBias = getNaturalCenteringBias(context.PatternInfo);
            float curveFreedom = 1 - naturalCenteringBias;
            float gentleFlowWeight = Time.Current >= context.FocusTime
                ? getGentleFlowAssistWeight(context.PatternInfo)
                : 0;

            if (tangentDirection.LengthSquared <= 0.0001f)
                tangentDirection = normaliseOrZero(context.DesiredPoint - outputPosition);

            if (tangentDirection.LengthSquared <= 0.0001f)
                return assistOffset;

            Vector2 corridorPoint = corridorProjection.Point;
            Vector2 correctionVector = getTangentLineCorrectionVector(outputPosition, corridorPoint, tangentDirection);
            float orthogonalDistance = correctionVector.Length;
            float corridorScale = spacedStream
                ? 0.74f
                : variableStream
                    ? 0.68f
                    : zigZagStream
                        ? 0.72f
                        : 0.6f;
            corridorScale = (float)Interpolation.Lerp(corridorScale, corridorScale + 0.18f, curveFreedom);

            if (gentleFlowWeight > 0)
                corridorScale = (float)Interpolation.Lerp(corridorScale, corridorScale + 0.16f, gentleFlowWeight);

            float corridorRadius = Math.Max(context.Radius * corridorScale, 8f);
            float recaptureScale = spacedStream
                ? 1.84f
                : variableStream
                    ? 1.68f
                    : zigZagStream
                        ? 1.72f
                        : 1.48f;
            recaptureScale = (float)Interpolation.Lerp(recaptureScale, recaptureScale + 0.14f, curveFreedom);

            if (gentleFlowWeight > 0)
                recaptureScale = (float)Interpolation.Lerp(recaptureScale, recaptureScale + 0.18f, gentleFlowWeight);

            float recaptureRadius = Math.Max(context.Radius * recaptureScale, corridorRadius + 12f);
            float recaptureWeight = orthogonalDistance <= corridorRadius
                ? 0
                : Math.Clamp((orthogonalDistance - corridorRadius) / Math.Max(1f, recaptureRadius - corridorRadius), 0, 1);
            float insideCorridorFreedom = orthogonalDistance <= corridorRadius
                ? Math.Clamp(1 - orthogonalDistance / Math.Max(1f, corridorRadius), 0, 1)
                : 0;
            float insideCorrectionWeight = spacedStream
                ? 0.06f
                : variableStream
                    ? 0.08f
                    : zigZagStream
                        ? 0.07f
                        : 0.09f;
            insideCorrectionWeight = (float)Interpolation.Lerp(insideCorrectionWeight, insideCorrectionWeight * 0.78f, curveFreedom);
            insideCorrectionWeight *= (float)Interpolation.Lerp(0.7f, 1.4f, strengthFactor);
            float outsideCorrectionWeight = spacedStream
                ? 0.44f
                : variableStream
                    ? 0.48f
                    : zigZagStream
                        ? 0.40f
                        : 0.52f;
            outsideCorrectionWeight = (float)Interpolation.Lerp(outsideCorrectionWeight, outsideCorrectionWeight * 0.78f, curveFreedom);
            outsideCorrectionWeight *= (float)Interpolation.Lerp(0.85f, 1.25f, strengthFactor);
            float orthogonalCorrectionWeight = orthogonalDistance <= corridorRadius
                ? insideCorrectionWeight * (0.2f + 0.8f * (1 - insideCorridorFreedom))
                : (float)Interpolation.Lerp(outsideCorrectionWeight * 0.22f, outsideCorrectionWeight, recaptureWeight);

            if (gentleFlowWeight > 0)
                orthogonalCorrectionWeight = (float)Interpolation.Lerp(orthogonalCorrectionWeight, orthogonalCorrectionWeight * 0.78f, gentleFlowWeight);

            Vector2 tangentialOffset = tangentDirection * Vector2.Dot(assistOffset, tangentDirection);
            Vector2 orthogonalOffset = assistOffset - tangentialOffset;
            Vector2 desiredOrthogonalOffset = orthogonalOffset + correctionVector * orthogonalCorrectionWeight * activation.EngageAmount;

            if (orthogonalDistance <= corridorRadius)
            {
                float insideHoldWeight = spacedStream
                    ? 0.92f
                    : zigZagStream
                        ? 0.90f
                        : 0.87f;
                insideHoldWeight = (float)Interpolation.Lerp(insideHoldWeight, insideHoldWeight + 0.02f, curveFreedom);
                insideHoldWeight *= (float)Interpolation.Lerp(1f, 0.92f, strengthFactor);
                desiredOrthogonalOffset = interpolate(desiredOrthogonalOffset, orthogonalOffset, insideHoldWeight * insideCorridorFreedom);
            }
            else
            {
                float recaptureBlend = (float)Interpolation.Lerp(0.24f, spacedStream ? 0.68f : 0.62f, recaptureWeight);
                desiredOrthogonalOffset = interpolate(orthogonalOffset, desiredOrthogonalOffset, recaptureBlend);
            }

            if (gentleFlowWeight > 0 && orthogonalDistance <= corridorRadius)
                desiredOrthogonalOffset = interpolate(desiredOrthogonalOffset, orthogonalOffset, gentleFlowWeight * 0.24f);

            if (orthogonalDistance > corridorRadius)
            {
                float curveEntry = Math.Clamp(orthogonalDistance / (corridorRadius * 1.5f), 0, 1);
                tangentialOffset += tangentDirection * (corridorRadius * curveEntry * 0.8f);
            }

            Vector2 desiredOffset = tangentialOffset + desiredOrthogonalOffset;

            float maxOffset = Math.Max(
                context.Radius + overshootAllowance + (float)Interpolation.Lerp(42f, 128f, strengthFactor),
                fovRadius * (float)Interpolation.Lerp(0.45f, 1.05f, strengthFactor));

            // Further reduce allowed pull distance on very small notes (strengthFactor is already scaled)
            float smallNoteScale = Math.Clamp(context.Radius / 30f, 0.30f, 1f);
            maxOffset *= Math.Clamp(smallNoteScale * 1.1f, 0.5f, 1f);

            return clampLength(desiredOffset, maxOffset);
        }

        private float getPointExitIntentWeight(AimAssistContext context, Vector2 rawPosition)
        {
            if (context.ModeName != @"point")
                return 0;

            Vector2 rawFromDesired = rawPosition - context.DesiredPoint;
            float rawDistance = rawFromDesired.Length;

            if (rawDistance <= 0.001f)
                return 0;

            Vector2 moveDirection = normaliseOrZero(averagedVelocity);
            Vector2 rawOutwardDirection = rawFromDesired / rawDistance;
            float outwardIntent = moveDirection.LengthSquared > 0
                ? Vector2.Dot(moveDirection, rawOutwardDirection)
                : 0;

            if (outwardIntent <= 0.08f)
                return 0;

            float exitRadius = Math.Max(context.Radius + getConfiguredOvershootAllowance() + 10f, 1f);
            float distanceWeight = Math.Clamp((rawDistance + assistOffset.Length * 0.16f) / exitRadius, 0, 1);
            float exitWeight = Math.Clamp((outwardIntent - 0.08f) / 0.72f, 0, 1) * distanceWeight;

            if (context.PreviewPoint.HasValue)
            {
                Vector2 nextDirection = normaliseOrZero(context.PreviewPoint.Value - context.DesiredPoint);
                float previewAlignment = nextDirection.LengthSquared > 0 && moveDirection.LengthSquared > 0
                    ? Vector2.Dot(moveDirection, nextDirection)
                    : 0;
                float previewWeight = (float)Interpolation.Lerp(0.82f, 1.18f, Math.Clamp((previewAlignment + 1) * 0.5f, 0, 1));
                exitWeight *= previewWeight;
            }

            return Math.Clamp(exitWeight, 0, 1);
        }

        private float getPointFlowContinuationWeight(AimAssistContext context, Vector2 rawPosition)
        {
            if (context.ModeName != @"point" || context.PointFlowBias <= 0 || !context.PreviewPoint.HasValue)
                return 0;

            Vector2 previewDirection = getPointPreviewDirection(context);
            Vector2 movementDirection = normaliseOrZero(averagedVelocity);
            Vector2 outputPosition = rawPosition + assistOffset;
            Vector2 outputFromDesired = outputPosition - context.DesiredPoint;
            Vector2 rawFromDesired = rawPosition - context.DesiredPoint;

            if (previewDirection.LengthSquared <= 0.0001f)
                return 0;

            float previewAlignment = movementDirection.LengthSquared > 0.0001f
                ? Vector2.Dot(movementDirection, previewDirection)
                : 0;
            float outputAlignment = outputFromDesired.LengthSquared > 0.0001f
                ? Vector2.Dot(normaliseOrZero(outputFromDesired), previewDirection)
                : 0;
            float rawAlignment = rawFromDesired.LengthSquared > 0.0001f
                ? Vector2.Dot(normaliseOrZero(rawFromDesired), previewDirection)
                : 0;
            float motionAlignmentWeight = Math.Clamp((previewAlignment - 0.12f) / 0.76f, 0, 1);
            float positionalAlignmentWeight = Math.Clamp((Math.Max(outputAlignment, rawAlignment) + 0.05f) / 0.95f, 0, 1);
            float alignmentWeight = Math.Max(motionAlignmentWeight, positionalAlignmentWeight * 0.92f);

            if (alignmentWeight <= 0)
                return 0;

            float loosePointRadius = context.Radius + getConfiguredOvershootAllowance() + 18f;
            float proximityWeight = Math.Clamp(1 - (context.DesiredPoint - outputPosition).Length / Math.Max(1f, loosePointRadius), 0, 1);
            float forwardProgress = Math.Clamp(Vector2.Dot(outputPosition - context.DesiredPoint, previewDirection) / Math.Max(1f, context.Radius * 0.82f), 0, 1);
            float speedWeight = Math.Clamp((averagedVelocity.Length - stationary_hold_speed_threshold * 0.35f) / 280f, 0, 1);
            float continuationBase = Math.Max(Math.Max(proximityWeight, forwardProgress), positionalAlignmentWeight * 0.78f);

            return Math.Clamp(continuationBase * alignmentWeight * Math.Max(0.52f, speedWeight) * context.PointFlowBias, 0, 1);
        }

        private float getRecentTargetSwitchCarryWeight(AimAssistContext context)
        {
            if (context.ModeName != @"point" || lastTargetSwitchCarryWeight <= 0 || !ReferenceEquals(lastContextDrawable, context.Drawable))
                return 0;

            double elapsedSinceSwitch = Time.Current - lastTargetSwitchTime;

            if (elapsedSinceSwitch < 0)
                return 0;

            double carryWindow = scaleRealTimeWindow(Interpolation.Lerp(80, 180, lastTargetSwitchCarryWeight));
            float timeWeight = 1 - (float)Math.Clamp(elapsedSinceSwitch / carryWindow, 0, 1);
            return Math.Clamp(timeWeight * lastTargetSwitchCarryWeight, 0, 1);
        }

        private float getPointRecenteringBias(AimAssistContext context, float pointFlowContinuationWeight, float strengthFactor)
        {
            float configuredBias = context.PointFlowBias > 0 || context.PatternInfo.IsContinuousFlow
                ? getNaturalCenteringBias(context.PatternInfo)
                : Math.Clamp((float)getConfiguredCenterBias(), 0, 1);
            float recenteringBias = Math.Min(configuredBias, (float)Interpolation.Lerp(0.9f, 0.96f, strengthFactor));

            if (context.ModeName != @"point")
                return recenteringBias;

            if (!context.PreviewPoint.HasValue && context.PointFlowBias <= 0)
                return recenteringBias;

            float flowWeight = Math.Max(pointFlowContinuationWeight, context.PointFlowBias);
            float flowCap = (float)Interpolation.Lerp(0.68f, 0.2f, flowWeight);

            if (context.PointFlowBias > 0)
                flowCap = Math.Min(flowCap, (float)Interpolation.Lerp(0.56f, 0.18f, context.PointFlowBias));

            if (context.PatternInfo.Kind == OsuPatternKind.Burst)
            {
                float burstWeight = Math.Max(context.PointFlowBias, context.PatternInfo.ContinuityWeight);
                float burstCap = (float)Interpolation.Lerp(0.34f, 0.12f, burstWeight);
                flowCap = Math.Min(flowCap, burstCap);
            }

            recenteringBias = Math.Min(recenteringBias, flowCap);
            return Math.Clamp(recenteringBias, 0, 1);
        }

        private static Vector2 getPointPreviewDirection(AimAssistContext context)
            => context.PreviewPoint.HasValue
                ? normaliseOrZero(context.PreviewPoint.Value - context.DesiredPoint)
                : Vector2.Zero;

        private double getSliderHeadHandoffWindow(DrawableSlider slider)
        {
            double baseWindow = scaleRealTimeWindow(Math.Clamp(getGreatWindow(slider) * 1.1, 36, 95));
            return Math.Min(baseWindow, Math.Max(scaleRealTimeWindow(10), slider.HitObject.SpanDuration * 0.22));
        }

        private float getDynamicFrictionAmount(AimAssistContext context, Vector2 rawPosition)
        {
            double dynamicFriction = getConfiguredDynamicFriction();

            if (dynamicFriction <= 0)
                return 0;

            if (context.ModeName == @"stream")
                return 0;

            if (context.ModeName == @"point")
            {
                double delta = context.FocusTime - Time.Current;
                double greatWindow = Math.Max(18, context.Drawable.HitObject.HitWindows?.WindowFor(HitResult.Great) ?? 50);
                double frictionLead = scaleRealTimeWindow(Math.Clamp(greatWindow * 1.25, 26, 90));

                if (delta > frictionLead)
                    return 0;
            }

            Vector2 outputPosition = rawPosition + assistOffset;
            Vector2 toDesired = context.DesiredPoint - outputPosition;
            float distanceToDesired = toDesired.Length;

            if (distanceToDesired <= 0.001f)
                return 0;

            float brakingRadius = context.Radius + getConfiguredOvershootAllowance() + 24f;

            if (distanceToDesired > brakingRadius)
                return 0;

            Vector2 radialDirection = toDesired / distanceToDesired;
            float outwardSpeed = Vector2.Dot(averagedVelocity, -radialDirection);

            if (outwardSpeed <= 0)
                return 0;

            float distanceFactor = 1 - distanceToDesired / brakingRadius;
            float speedFactor = Math.Clamp(outwardSpeed / 600f, 0, 1);
            float result = (float)dynamicFriction * distanceFactor * speedFactor;

            if (context.ModeName == @"point")
            {
                double delta = context.FocusTime - Time.Current;
                double greatWindow = Math.Max(18, context.Drawable.HitObject.HitWindows?.WindowFor(HitResult.Great) ?? 50);
                double frictionLead = scaleRealTimeWindow(Math.Clamp(greatWindow * 1.25, 26, 90));
                float timingFactor = delta <= 0
                    ? 1
                    : 1 - (float)Math.Clamp(delta / frictionLead, 0, 1);
                result *= timingFactor;
            }

            return result;
        }

        private static Vector2 smoothDamp(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Math.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

            Vector2 change = current - target;
            float maxChange = maxSpeed * smoothTime;
            if (change.LengthSquared > maxChange * maxChange)
            {
                change = normaliseOrZero(change) * maxChange;
            }

            target = current - change;

            Vector2 temp = (currentVelocity + change * omega) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            Vector2 output = target + (change + temp) * exp;

            if (Vector2.Dot(target - current, output - target) > 0)
            {
                output = target;
                currentVelocity = (output - target) / deltaTime;
            }

            return output;
        }

        private Vector2 updateAssistOffset(Vector2 targetOffset, bool engaged, Vector2 rawPosition, Vector2 rawDelta, double elapsed, float engageAmount, float radialFrictionAmount,
                                           bool pointMode, bool shortRepeatSliderMode, bool streamMode, float strengthFactor)
        {
            float dt = (float)elapsed / 1000f;
            if (dt <= 0) return assistOffset;

            if (!engaged)
                targetOffset = getReleaseTargetOffset(rawDelta, elapsed, rawDelta.Length / dt, strengthFactor);

            if (!engaged && assistOffset.Length <= 0.1f && targetOffset.Length <= 0.1f)
            {
                clearStreamOutputMotionState();
                return Vector2.Zero;
            }

            clearStreamOutputMotionState();

            float smoothTime;
            float maxSpeed = 10000f;

            if (engaged) {
                // Responsiveness is handled by faster smoothTime and high maxSpeed below.
                // Previously adding rawDelta here caused crooked/overshooting behavior.

                smoothTime = (float)Interpolation.Lerp(
                    pointMode ? 0.065f : shortRepeatSliderMode ? 0.085f : 0.095f,
                    pointMode ? 0.030f : shortRepeatSliderMode ? 0.040f : 0.050f,
                    engageAmount);
                
                smoothTime *= (float)Interpolation.Lerp(1.15f, 0.72f, strengthFactor);
                maxSpeed = averagedVelocity.Length * (pointMode ? 2.5f : 1.8f) + (float)Interpolation.Lerp(1200f, 4000f, strengthFactor);
            } else {
                smoothTime = 0.16f; 
                maxSpeed = averagedVelocity.Length * 0.8f + 800f;
            }

            if (IsAntiJitterActive) {
                smoothTime *= 2.0f;
                maxSpeed *= 0.55f;
            }

            if (engaged && radialFrictionAmount > 0) {
                smoothTime *= 1f + radialFrictionAmount * 1.8f;
            }

            if (!hasPreviousOffsetVelocity) {
                previousOffsetVelocity = Vector2.Zero;
                hasPreviousOffsetVelocity = true;
            }

            Vector2 nextOffset = smoothDamp(assistOffset, targetOffset, ref previousOffsetVelocity, smoothTime, maxSpeed, dt);
            
            float safeMaxOffset = Math.Max(getConfiguredFovRadius() * 1.3f, targetOffset.Length + 15f);
            nextOffset = clampLength(nextOffset, safeMaxOffset);

            if (!engaged && nextOffset.Length <= 0.1f) return Vector2.Zero;
            
            return nextOffset;
        }

        private Vector2 getReleaseTargetOffset(Vector2 rawDelta, double elapsed, float rawSpeed, float strengthFactor)
        {
            if (assistOffset.LengthSquared <= 0.01f)
                return Vector2.Zero;

            Vector2 offsetDirection = normaliseOrZero(assistOffset);
            Vector2 rawDirection = normaliseOrZero(rawDelta);
            float alignment = rawDirection.LengthSquared > 0
                ? Vector2.Dot(rawDirection, offsetDirection)
                : 0;

            float alignmentWeight = Math.Clamp((alignment + 1) * 0.5f, 0, 1);
            float outputFollowFraction = (float)Interpolation.Lerp(0.28f, 0.62f, alignmentWeight);
            outputFollowFraction = (float)Interpolation.Lerp(outputFollowFraction, outputFollowFraction + 0.12f, Math.Clamp(rawSpeed / 900f, 0, 1));
            outputFollowFraction = Math.Clamp(outputFollowFraction, 0.24f, 0.76f);

            Vector2 offsetAfterInput = assistOffset - rawDelta * (1 - outputFollowFraction);

            float recenterSpeed = (float)Interpolation.Lerp(55f, 165f, Math.Clamp(rawSpeed / 900f, 0, 1));
            recenterSpeed *= (float)Interpolation.Lerp(0.9f, 1.15f, strengthFactor);
            recenterSpeed *= (float)Interpolation.Lerp(0.8f, 1.35f, alignmentWeight);

            return moveTowards(offsetAfterInput, Vector2.Zero, recenterSpeed * (float)elapsed / 1000f);
        }

        private void releaseVirtualCursor(bool restoreOriginalPosition = true)
        {
            if (!drivingCursor)
                return;

            drivingCursor = false;
            inputManager!.ResetVirtualCursor(restoreOriginalPosition);
        }

        private void applyOutput(Vector2 rawPosition)
        {
            bool shouldDrive = assistOffset.LengthSquared > 0.01f || PassedActivationFilters || IsAntiJitterActive;

            if (mosuAntiAimAssistMod == null && IsAimAssistEnabled && assistOffset.LengthSquared > 0.01f)
            {
                AdjustedFrameCount++;
                MaxAdjustmentMagnitude = Math.Max(MaxAdjustmentMagnitude, assistOffset.Length);
            }

            if (shouldDrive)
            {
                Vector2 outputOffset = assistOffset;

                inputManager!.MoveVirtualCursorTo(rawPosition + outputOffset);
                drivingCursor = true;
            }
            else if (drivingCursor)
                releaseVirtualCursor();

            CurrentOutputPosition = inputManager!.CurrentState.Mouse.Position;
        }
    }
}
