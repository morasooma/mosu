// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Utils;
using osuTK;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class AimAssistController
    {
        private float getAdaptiveTargetRadius(TargetDescriptor current, TargetDescriptor? next, double focusTime)
        {
            float baseRadius = current.Radius;

            if (next?.Drawable == null || current.Drawable is DrawableSpinner || next.Value.Drawable is DrawableSpinner)
                return baseRadius;

            double spacing = (next.Value.BaseLocalPosition - current.BaseLocalPosition).Length;
            double referenceRadius = Math.Max(1, Math.Min(current.HitObject.Radius, next.Value.HitObject.Radius));
            double spacingRatio = spacing / (referenceRadius * 2);
            double realGap = toRealTimeWindow(Math.Max(0, next.Value.StartTime - focusTime));
            float pressure = (float)Math.Clamp(1 - realGap / 240.0, 0, 1);
            float closeCompression = (float)Math.Clamp((1.4 - spacingRatio) / 0.7, 0, 1);
            float wideExpansion = (float)Math.Clamp((spacingRatio - 1.95) / 1.95, 0, 1);
            float scale = 1;

            scale -= (float)Interpolation.Lerp(0.12f, 0.28f, pressure) * closeCompression;
            scale += (float)Interpolation.Lerp(0.1f, 0.34f, pressure) * wideExpansion;

            if (current.Drawable is DrawableSlider)
                scale = Math.Min(scale, 1.22f);

            return baseRadius * Math.Clamp(scale, 0.76f, 1.34f);
        }

        private AimAssistContext createContext(TargetDescriptor current, TargetDescriptor? previous, TargetDescriptor? next, IReadOnlyList<TargetDescriptor> allTargets,
                                               IReadOnlyList<OsuPatternState> patternStates, int currentTargetIndex, Vector2 rawPosition)
        {
            OsuPatternState patternState = currentTargetIndex >= 0 && currentTargetIndex < patternStates.Count
                ? patternStates[currentTargetIndex]
                : default;
            OsuPatternInfo patternInfo = patternState.PatternInfo;

            bool committedFlowPending = patternState.IsCommittedStream
                                        && previous?.Drawable != null
                                        && previous.Value.Drawable.IsHit
                                        && isRailSegment(previous.Value, current);

            if (isRelaxAwaitingPress(current) && !committedFlowPending)
                return createRelaxPendingContext(current, previous, next, rawPosition, patternState);

            if (current.Drawable is DrawableSlider slider && Time.Current <= current.EndTime + scaleRealTimeWindow(80))
                return createSliderContext(current, previous, slider, next, rawPosition);

            List<TargetDescriptor> railTargets = buildRailTargets(current, next, allTargets, currentTargetIndex, patternState);

            if (railTargets.Count >= 2)
            {
                fillRailProjectionPointBuffer(previous, railTargets);

                if (relaxController?.IsEnabled == true && Time.Current < current.StartTime)
                    return createPointContext(current, previous, next, rawPosition, impactStrength: 0, patternState: patternState);

                if (shouldUseRailContext(current, previous, next, railTargets, patternState))
                    return createRailEntryContext(current, previous, railTargets, next, rawPosition, patternState, currentTargetIndex);

                return createBurstPointContext(current, previous, railTargets, next, rawPosition, patternState: patternState);
            }

            return createPointContext(current, previous, next, rawPosition, patternState: patternState);
        }

        private bool isRelaxAwaitingPress(TargetDescriptor target)
            => relaxController?.IsTargetAwaitingPress(target.HitObject) == true;

        private AimAssistContext createRelaxPendingContext(TargetDescriptor current, TargetDescriptor? previous, TargetDescriptor? next, Vector2 rawPosition, OsuPatternState patternState)
        {
            double focusTime = getRelaxPendingFocusTime(current);

            if (current.Drawable is DrawableSlider pendingSlider)
                return createSliderHeadContext(current, previous, pendingSlider, next, rawPosition, focusTime, false);

            return createPointContext(current, previous, next, rawPosition, focusTime, false, 0, patternState);
        }

        private double getRelaxPendingFocusTime(TargetDescriptor current)
        {
            if (relaxController?.TryGetTargetLinkedPressTime(current.HitObject, out double pressTime) == true)
                return Math.Max(current.StartTime, pressTime);

            if (relaxController?.TryGetTargetScheduledPressTime(current.HitObject, out pressTime) == true)
                return Math.Max(current.StartTime, pressTime);

            return current.StartTime;
        }

        private AimAssistContext createRailEntryContext(TargetDescriptor current, TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> railTargets, TargetDescriptor? next,
                                                        Vector2 rawPosition, OsuPatternState patternState, int currentIndex)
        {
            if (current.Drawable is not DrawableHitCircle hitCircle)
                return createRailContext(current, previous, railTargets, next, rawPosition, patternState);

            bool midChainFlow = patternState.SupportsFlowPath
                                && (currentIndex > patternState.SegmentStartIndex
                                    || previous?.Drawable != null && previous.Value.Drawable.IsHit && isRailSegment(previous.Value, current));

            if (midChainFlow)
                return createRailContext(current, previous, railTargets, next, rawPosition, patternState);

            bool streamStarted = previous?.Drawable != null
                                 && previous.Value.Drawable.IsHit
                                 && isRailSegment(previous.Value, current);

            if (!streamStarted)
                return createPointContext(current, previous, next, rawPosition, impactStrength: 0.42f, patternState: patternState);

            double handoffWindow = Math.Clamp(getGreatWindow(hitCircle) * 1.1, 32, 85);

            if (Time.Current <= current.StartTime + handoffWindow
                && !shouldSkipRailHandoff(current, previous, next, railTargets, rawPosition, handoffWindow))
                return createRailStartHandoffContext(current, previous, railTargets, next, rawPosition, handoffWindow, patternState);

            return createRailContext(current, previous, railTargets, next, rawPosition, patternState);
        }

        private bool shouldSkipRailHandoff(TargetDescriptor current, TargetDescriptor? previous, TargetDescriptor? next, IReadOnlyList<TargetDescriptor> railTargets,
                                           Vector2 rawPosition, double handoffWindow)
        {
            if (previous?.Drawable == null)
                return false;

            fillRailProjectionPointBuffer(previous, railTargets);

            if (projectionPointBuffer.Count < 2)
                return true;

            float motionScale = getRailMotionScale(previous, current, next, railTargets);
            Vector2 outputPosition = rawPosition + assistOffset;
            ProjectionResult projection = projectOntoPolyline(projectionPointBuffer, getRailProjectionQuery(rawPosition, motionScale));
            float railDistance = (projection.Point - outputPosition).Length;
            float onRailThreshold = Math.Max(current.Radius * 0.95f, 12f);
            bool alreadyOnRail = railDistance <= onRailThreshold;
            Vector2 movementDirection = normaliseOrZero(averagedVelocity);
            Vector2 tangentDirection = normaliseOrZero(projection.Tangent);
            float tangentAlignment = movementDirection.LengthSquared > 0.0001f && tangentDirection.LengthSquared > 0.0001f
                ? Vector2.Dot(movementDirection, tangentDirection)
                : 0;
            bool followingRail = tangentAlignment > 0.42f && railDistance <= Math.Max(current.Radius * 1.4f, 20f);
            bool handoffMostlyElapsed = Time.Current >= current.StartTime + handoffWindow * 0.18;

            return alreadyOnRail || followingRail || handoffMostlyElapsed;
        }

        private bool shouldUseRailContext(TargetDescriptor current, TargetDescriptor? previous, TargetDescriptor? next, IReadOnlyList<TargetDescriptor> railTargets,
                                          OsuPatternState patternState)
        {
            if (railTargets.Count < 2)
                return false;

            if (patternState.SupportsFlowPath)
                return true;

            OsuPatternInfo patternInfo = patternState.PatternInfo;

            if (patternInfo.Kind != OsuPatternKind.Burst || patternInfo.ChainLength < 3)
                return false;

            if (previous?.Drawable == null || next?.Drawable == null)
                return patternInfo.ContinuityWeight >= 0.56f;

            if (patternInfo.StreamShape == OsuStreamShapeKind.ZigZag)
                return patternInfo.ContinuityWeight >= 0.62f;

            return getRailCornerAlignment(previous, current, next) >= 0.12f;
        }

        private AimAssistContext createPointContext(TargetDescriptor current, TargetDescriptor? previous, TargetDescriptor? next, Vector2 rawPosition, double? focusTime = null,
                                                    bool allowPassiveAssist = true, float impactStrength = 1f, OsuPatternState patternState = default)
        {
            OsuPatternInfo patternInfo = patternState.PatternInfo;
            double resolvedFocusTime = focusTime ?? current.StartTime;
            Vector2 desiredPoint = impactStrength <= 0
                ? getDisplayCentre(current.Drawable)
                : getImpactPoint(current, previous, next, rawPosition, impactStrength);

            if (impactStrength > 0 && patternState.SupportsFlowPath && next?.Drawable != null)
                desiredPoint = ensureForwardFlowLead(current, next.Value, desiredPoint);

            Vector2 toDesired = desiredPoint - rawPosition;
            float adaptiveRadius = getAdaptiveTargetRadius(current, next, resolvedFocusTime);

            return new AimAssistContext(
                @"point",
                current.Drawable,
                desiredPoint,
                normaliseOrZero(toDesired),
                Vector2.Zero,
                desiredPoint,
                adaptiveRadius,
                toDesired.Length,
                resolvedFocusTime,
                current.HitObject.TimePreempt,
                allowPassiveAssist,
                next?.Drawable != null ? next.Value.ScreenSpacePosition : null,
                next?.Drawable != null ? next.Value.Radius : 0,
                0,
                patternInfo,
                patternState);
        }

        private Vector2 ensureForwardFlowLead(TargetDescriptor current, TargetDescriptor next, Vector2 desiredPoint)
        {
            Vector2 flowDirection = normaliseOrZero(next.ScreenSpacePosition - current.ScreenSpacePosition);

            if (flowDirection.LengthSquared <= 0.0001f)
                return desiredPoint;

            float neighbourDistance = (next.ScreenSpacePosition - current.ScreenSpacePosition).Length;
            float minimumForwardLead = Math.Min(
                Math.Max(current.Radius * 0.24f, 4f),
                Math.Max(6f, neighbourDistance * 0.1f));
            float forwardDistance = Vector2.Dot(desiredPoint - current.ScreenSpacePosition, flowDirection);

            if (forwardDistance >= minimumForwardLead)
                return desiredPoint;

            return desiredPoint + flowDirection * (minimumForwardLead - forwardDistance);
        }

        private AimAssistContext createBurstPointContext(TargetDescriptor current, TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> railTargets, TargetDescriptor? next, Vector2 rawPosition,
                                                         double? focusTime = null, bool allowPassiveAssist = true, OsuPatternState patternState = default)
        {
            OsuPatternInfo patternInfo = patternState.PatternInfo;
            double resolvedFocusTime = focusTime ?? current.StartTime;
            Vector2 desiredPoint = getBurstSteeringPoint(current, previous, railTargets, next, rawPosition);
            Vector2 toDesired = desiredPoint - rawPosition;
            float adaptiveRadius = getAdaptiveTargetRadius(current, next, resolvedFocusTime);
            float pointFlowBias = getBurstPointFlowBias(previous, current, next, railTargets);

            if (patternInfo.Kind == OsuPatternKind.Burst && patternInfo.ContinuityWeight > 0)
                pointFlowBias = Math.Max(pointFlowBias, (float)Interpolation.Lerp(0.62f, 0.9f, patternInfo.ContinuityWeight));

            return new AimAssistContext(
                @"point",
                current.Drawable,
                desiredPoint,
                normaliseOrZero(toDesired),
                Vector2.Zero,
                desiredPoint,
                adaptiveRadius,
                toDesired.Length,
                resolvedFocusTime,
                current.HitObject.TimePreempt,
                allowPassiveAssist,
                next?.Drawable != null ? next.Value.ScreenSpacePosition : null,
                next?.Drawable != null ? next.Value.Radius : 0,
                pointFlowBias,
                patternInfo,
                patternState);
        }

        private Vector2 getImpactPoint(TargetDescriptor current, TargetDescriptor? previous, TargetDescriptor? next, Vector2 rawPosition, float strengthMultiplier)
        {
            Vector2 targetPosition = current.ScreenSpacePosition;
            Vector2? previousPosition = previous?.Drawable != null ? previous.Value.ScreenSpacePosition : null;
            Vector2? nextPosition = next?.Drawable != null ? next.Value.ScreenSpacePosition : null;
            return ImpactPointHelper.GetImpactPoint(targetPosition, current.Radius, rawPosition, previousPosition, nextPosition, getConfiguredCenterBias(), strengthMultiplier);
        }

        private Vector2 getBurstSteeringPoint(TargetDescriptor current, TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> railTargets, TargetDescriptor? next, Vector2 rawPosition)
        {
            Vector2 impactPoint = getImpactPoint(current, previous, next, rawPosition, 0.34f);
            Vector2 currentCentre = current.ScreenSpacePosition;
            Vector2 flowDirection = getBurstFlowDirection(previous, current, next);
            float neighbourDistance = next?.Drawable != null
                ? (next.Value.ScreenSpacePosition - currentCentre).Length
                : previous?.Drawable != null
                    ? (currentCentre - previous.Value.ScreenSpacePosition).Length
                    : 0;
            float steeringDistance = Math.Min(Math.Max(current.Radius * 0.42f, neighbourDistance * 0.26f), Math.Max(current.Radius * 0.9f, 10f));
            Vector2 directionalPoint = currentCentre + flowDirection * steeringDistance;

            fillRailProjectionPointBuffer(previous, railTargets);

            if (projectionPointBuffer.Count < 2)
                return interpolate(impactPoint, directionalPoint, 0.84f);

            float projectionLead = Math.Min(Math.Max(current.Radius * 0.34f, 8f), Math.Max(14f, neighbourDistance * 0.12f));
            ProjectionResult projection = projectOntoPolyline(projectionPointBuffer, rawPosition + assistOffset + flowDirection * projectionLead);
            Vector2 flowVector = projection.Point - currentCentre;

            if (flowVector.LengthSquared > 0.0001f && flowDirection.LengthSquared > 0.0001f)
            {
                Vector2 projectedDirection = normaliseOrZero(flowVector);

                if (Vector2.Dot(projectedDirection, flowDirection) < 0)
                    flowVector = Vector2.Zero;
            }

            float flowClampRadius = Math.Max(current.Radius * 0.9f, 10f);
            Vector2 projectedFlowPoint = flowVector.LengthSquared > 0.0001f
                ? currentCentre + clampLength(flowVector, flowClampRadius)
                : directionalPoint;
            float motionScale = getRailMotionScale(previous, current, next, railTargets);
            Vector2 flowPoint = interpolate(directionalPoint, projectedFlowPoint, (float)Interpolation.Lerp(0.36f, 0.78f, motionScale));
            float flowWeight = (float)Interpolation.Lerp(0.76f, 0.96f, motionScale);

            if (railTargets.Count == 2)
                flowWeight = Math.Max(flowWeight, 0.84f);

            if (railTargets.Count >= 3)
                flowWeight = Math.Max(flowWeight, 0.8f);

            Vector2 desiredPoint = interpolate(impactPoint, flowPoint, flowWeight);

            if (flowDirection.LengthSquared > 0.0001f)
            {
                float forwardDistance = Vector2.Dot(desiredPoint - currentCentre, flowDirection);
                float minimumForwardLead = Math.Min(
                    Math.Max(current.Radius * 0.26f, 4f),
                    Math.Max(6f, neighbourDistance * 0.1f));

                if (forwardDistance < minimumForwardLead)
                    desiredPoint += flowDirection * (minimumForwardLead - forwardDistance);
            }

            return desiredPoint;
        }

        private Vector2 getBurstFlowDirection(TargetDescriptor? previous, TargetDescriptor current, TargetDescriptor? next)
        {
            Vector2 outgoing = next?.Drawable != null
                ? normaliseOrZero(next.Value.ScreenSpacePosition - current.ScreenSpacePosition)
                : Vector2.Zero;
            Vector2 incoming = previous?.Drawable != null
                ? normaliseOrZero(current.ScreenSpacePosition - previous.Value.ScreenSpacePosition)
                : Vector2.Zero;

            if (incoming.LengthSquared > 0.0001f && outgoing.LengthSquared > 0.0001f)
            {
                float alignment = Vector2.Dot(incoming, outgoing);

                if (alignment <= 0.18f)
                {
                    Vector2 movementDirection = normaliseOrZero(averagedVelocity);

                    if (movementDirection.LengthSquared > 0.0001f)
                    {
                        float outgoingAlignment = Vector2.Dot(movementDirection, outgoing);
                        float incomingAlignment = Vector2.Dot(movementDirection, incoming);

                        if (outgoingAlignment >= incomingAlignment - 0.06f)
                            return outgoing;

                        if (incomingAlignment > 0.34f)
                            return incoming;
                    }

                    return outgoing;
                }

                float outgoingWeight = (float)Interpolation.Lerp(0.5f, 0.82f, Math.Clamp((0.52f - alignment) / 0.52f, 0, 1));
                Vector2 directedBlend = normaliseOrZero(incoming * (1 - outgoingWeight) + outgoing * outgoingWeight);

                if (directedBlend.LengthSquared > 0.0001f)
                    return directedBlend;

                Vector2 blended = normaliseOrZero(incoming + outgoing);

                if (blended.LengthSquared > 0.0001f)
                    return blended;
            }

            if (outgoing.LengthSquared > 0.0001f)
                return outgoing;

            if (incoming.LengthSquared > 0.0001f)
                return incoming;

            return Vector2.UnitX;
        }

        private float getBurstPointFlowBias(TargetDescriptor? previous, TargetDescriptor current, TargetDescriptor? next, IReadOnlyList<TargetDescriptor> railTargets)
        {
            float motionScale = getRailMotionScale(previous, current, next, railTargets);
            float flowBias = (float)Interpolation.Lerp(0.58f, 0.88f, motionScale);

            if (railTargets.Count == 2)
                flowBias = Math.Max(flowBias, 0.62f);
            else if (railTargets.Count >= 3)
                flowBias = Math.Max(flowBias, 0.74f);

            if (previous?.Drawable != null && next?.Drawable != null)
            {
                float cornerAlignment = getRailCornerAlignment(previous, current, next);
                float cornerSharpness = Math.Clamp((0.35f - cornerAlignment) / 0.95f, 0, 1);
                flowBias = (float)Interpolation.Lerp(flowBias, 0.92f, cornerSharpness);
            }

            return Math.Clamp(flowBias, 0, 1);
        }

        private AimAssistContext createRailStartHandoffContext(TargetDescriptor current, TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> railTargets, TargetDescriptor? next,
                                                               Vector2 rawPosition, double handoffWindow, OsuPatternState patternState = default)
        {
            OsuPatternInfo patternInfo = patternState.PatternInfo;
            float motionScale = getRailMotionScale(previous, current, next, railTargets);
            ProjectionResult projection = getContinuousRailProjection(current, previous, railTargets, next, rawPosition, motionScale, patternInfo);
            Vector2 currentCentre = current.ScreenSpacePosition;
            float handoffProgress = (float)Math.Clamp((Time.Current - current.StartTime) / handoffWindow, 0, 1);
            handoffProgress = (float)Math.Pow(handoffProgress, 1.45f);
            float adaptiveRadius = getAdaptiveTargetRadius(current, next, current.StartTime);

            Vector2 desiredPoint = interpolate(currentCentre, projection.Point, handoffProgress);
            Vector2 toDesired = desiredPoint - rawPosition;

            return new AimAssistContext(
                @"rail",
                current.Drawable,
                desiredPoint,
                normaliseOrZero(toDesired),
                normaliseOrZero(projection.Tangent),
                projection.Point,
                adaptiveRadius,
                toDesired.Length,
                current.StartTime,
                current.HitObject.TimePreempt,
                true,
                next?.Drawable != null ? next.Value.ScreenSpacePosition : null,
                next?.Drawable != null ? next.Value.Radius : 0,
                0,
                patternInfo,
                patternState);
        }

        private AimAssistContext createRailContext(TargetDescriptor current, TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> railTargets, TargetDescriptor? next,
                                                   Vector2 rawPosition, OsuPatternState patternState = default)
        {
            OsuPatternInfo patternInfo = patternState.PatternInfo;

            if (shouldUseContinuousStreamContext(current, previous, railTargets, patternState))
                return createStreamContext(current, previous, railTargets, next, rawPosition, patternState);

            float motionScale = getRailMotionScale(previous, current, next, railTargets);
            ProjectionResult projection = getContinuousRailProjection(current, previous, railTargets, next, rawPosition, motionScale, patternInfo);
            Vector2 desiredPoint = applyRailLead(projection.Point, projection.Tangent, motionScale);
            Vector2 toDesired = desiredPoint - rawPosition;
            float adaptiveRadius = getAdaptiveTargetRadius(current, next, current.StartTime);

            return new AimAssistContext(
                @"rail",
                current.Drawable,
                desiredPoint,
                normaliseOrZero(toDesired),
                normaliseOrZero(projection.Tangent),
                projection.Point,
                adaptiveRadius,
                toDesired.Length,
                current.StartTime,
                current.HitObject.TimePreempt,
                true,
                next?.Drawable != null ? next.Value.ScreenSpacePosition : null,
                next?.Drawable != null ? next.Value.Radius : 0,
                0,
                patternInfo,
                patternState);
        }

        private AimAssistContext createStreamContext(TargetDescriptor current, TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> railTargets, TargetDescriptor? next,
                                                     Vector2 rawPosition, OsuPatternState patternState = default)
        {
            OsuPatternInfo patternInfo = patternState.PatternInfo;
            float motionScale = getRailMotionScale(previous, current, next, railTargets);
            ProjectionResult projection = getContinuousRailProjection(current, previous, railTargets, next, rawPosition, motionScale, patternInfo);
            Vector2 tangent = normaliseOrZero(projection.Tangent);
            Vector2 intentDirection = tangent.LengthSquared > 0.0001f
                ? tangent
                : normaliseOrZero(projection.Point - rawPosition);
            float adaptiveRadius = getAdaptiveTargetRadius(current, next, current.StartTime);

            if (patternInfo.IsSpacedStream)
                adaptiveRadius *= 1.18f;
            else if (patternInfo.IsVariableStream)
                adaptiveRadius *= 1.1f;

            if (patternInfo.StreamShape == OsuStreamShapeKind.ZigZag)
                adaptiveRadius *= 1.08f;

            ProjectionResult corridorProjection = projectionPointBuffer.Count >= 2
                ? projectOntoPolyline(projectionPointBuffer, rawPosition)
                : projection;
            float corridorDistance = (corridorProjection.Point - rawPosition).Length;

            return new AimAssistContext(
                @"stream",
                current.Drawable,
                projection.Point,
                intentDirection,
                tangent,
                projection.Point,
                adaptiveRadius,
                corridorDistance,
                current.StartTime,
                current.HitObject.TimePreempt,
                true,
                next?.Drawable != null ? next.Value.ScreenSpacePosition : null,
                next?.Drawable != null ? next.Value.Radius : 0,
                0,
                patternInfo,
                patternState);
        }

        private AimAssistContext createSliderContext(TargetDescriptor current, TargetDescriptor? previous, DrawableSlider slider, TargetDescriptor? next, Vector2 rawPosition)
        {
            if (!isSliderBodyAssistActive(current, slider, Time.Current))
                return createSliderHeadContext(current, previous, slider, next, rawPosition);

            bool shortRepeatSlider = isShortRepeatSlider(slider, current.Radius);
            double tailHoldLead = getSliderTailHoldLead(slider, shortRepeatSlider);

            if (Time.Current >= current.EndTime - tailHoldLead)
                return createSliderTailContext(current, slider, next, rawPosition, shortRepeatSlider);

            return createSliderTrackingContext(current, previous, slider, next, rawPosition);
        }

        private double getSliderTailHoldLead(DrawableSlider slider, bool shortRepeatSlider)
        {
            double baseLead = scaleRealTimeWindow(Math.Clamp(getGreatWindow(slider) * 1.35, 45, 100));

            if (!shortRepeatSlider)
                return baseLead;

            return Math.Min(baseLead, Math.Max(scaleRealTimeWindow(12), slider.HitObject.SpanDuration * 0.12));
        }

        private AimAssistContext createSliderHeadContext(TargetDescriptor current, TargetDescriptor? previous, DrawableSlider slider, TargetDescriptor? next, Vector2 rawPosition,
                                                         double? focusTime = null, bool allowPassiveAssist = true)
        {
            double resolvedFocusTime = focusTime ?? current.StartTime;
            
            // Steer the impact point slightly into the slider's path, rather than towards the next independent hit object.
            double initialProgress = Math.Min(1.0, 20.0 / Math.Max(1.0, slider.HitObject.Duration));
            Vector2 pathLeadPosition = toScreenSpace(slider.HitObject.StackedPositionAt(initialProgress));
            Vector2 targetPosition = current.ScreenSpacePosition;
            Vector2? previousPosition = previous?.Drawable != null ? previous.Value.ScreenSpacePosition : null;
            
            Vector2 desiredPoint = ImpactPointHelper.GetImpactPoint(targetPosition, current.Radius, rawPosition, previousPosition, pathLeadPosition, getConfiguredCenterBias(), 0.72f);
            Vector2 toDesired = desiredPoint - rawPosition;
            
            // Don't arbitrarily clamp the radius to the physical visual circle, allow adaptive scaling
            float adaptiveRadius = getAdaptiveTargetRadius(current, next, resolvedFocusTime);

            return new AimAssistContext(
                @"point",
                current.Drawable,
                desiredPoint,
                normaliseOrZero(toDesired),
                Vector2.Zero,
                desiredPoint,
                adaptiveRadius,
                toDesired.Length,
                resolvedFocusTime,
                current.HitObject.TimePreempt,
                allowPassiveAssist,
                next?.Drawable != null ? next.Value.ScreenSpacePosition : null,
                next?.Drawable != null ? next.Value.Radius : 0,
                0);
        }

        private AimAssistContext createSliderTrackingContext(TargetDescriptor current, TargetDescriptor? previous, DrawableSlider slider, TargetDescriptor? next, Vector2 rawPosition)
        {
            double duration = Math.Max(1, slider.HitObject.Duration);
            double currentProgress = Math.Clamp((Time.Current - slider.HitObject.StartTime) / duration, 0, 1);
            double lookBehindProgress = Math.Clamp(getSliderTrackingWindow(slider, 40) / duration, 0.02, 0.12);
            double lookAheadProgress = Math.Clamp(getSliderTrackingWindow(slider, 70) / duration, 0.03, 0.16);
            double startProgress = Math.Max(0, currentProgress - lookBehindProgress);
            double endProgress = Math.Min(1, currentProgress + lookAheadProgress);
            float followRadius = getSliderFollowRadius(current.Radius);
            bool shortRepeatSlider = isShortRepeatSlider(slider, current.Radius);
            Vector2 ballPosition = getSliderBallScreenPosition(slider, currentProgress);
            sampleSliderRail(projectionPointBuffer, slider, startProgress, endProgress);
            Vector2 projectionQuery = getSliderTrackingProjectionQuery(rawPosition, ballPosition, followRadius);
            ProjectionResult projection = projectOntoPolyline(projectionPointBuffer, projectionQuery);
            Vector2 desiredPoint = getSliderTrackingDesiredPoint(projection.Point, projection.Tangent, ballPosition, followRadius);
            double headHandoffWindow = getSliderHeadHandoffWindow(slider) * 1.15;
            float headHandoffWeight = (float)Math.Clamp(1 - (Time.Current - slider.HitObject.StartTime) / Math.Max(1, headHandoffWindow), 0, 1);

            if (headHandoffWeight > 0)
            {
                Vector2 headPoint = getImpactPoint(current, previous, next, rawPosition, 0.72f);
                float blend = 1 - (float)Math.Pow(headHandoffWeight, 1.55f);
                desiredPoint = interpolate(headPoint, desiredPoint, blend);
            }

            if (shortRepeatSlider)
            {
                Vector2 spanMidPoint = toScreenSpace(slider.HitObject.StackedPositionAt(getSpanMidProgress(slider, currentProgress)));
                desiredPoint = ballPosition + clampLength(spanMidPoint - ballPosition, followRadius * 0.74f);
            }

            Vector2 toDesired = desiredPoint - rawPosition;
            double focusTime = Math.Clamp(Time.Current, current.StartTime, current.EndTime);

            return new AimAssistContext(
                shortRepeatSlider ? @"slider-repeat" : @"slider",
                current.Drawable,
                desiredPoint,
                normaliseOrZero(toDesired),
                normaliseOrZero(projection.Tangent),
                ballPosition,
                followRadius,
                toDesired.Length,
                focusTime,
                Math.Max(120, current.EndTime - current.StartTime),
                true,
                next?.Drawable != null ? next.Value.ScreenSpacePosition : null,
                next?.Drawable != null ? next.Value.Radius : 0,
                0);
        }

        private double getSliderTrackingWindow(DrawableSlider slider, double milliseconds)
            => Math.Clamp(scaleRealTimeWindow(milliseconds), scaleRealTimeWindow(20), Math.Max(scaleRealTimeWindow(20), slider.HitObject.Duration * 0.24));

        private Vector2 getSliderBallScreenPosition(DrawableSlider slider, double progress)
        {
            if (slider.Ball.IsLoaded)
                return slider.Ball.ScreenSpaceDrawQuad.Centre;

            return toScreenSpace(slider.HitObject.StackedPositionAt(progress));
        }

        private Vector2 getSliderTrackingProjectionQuery(Vector2 rawPosition, Vector2 ballPosition, float followRadius)
        {
            Vector2 outputPosition = rawPosition + assistOffset;
            Vector2 movementDirection = normaliseOrZero(averagedVelocity);
            float forwardLookahead = Math.Min(followRadius * 0.6f, averagedVelocity.Length * 0.035f);
            
            // Allow the query to freely explore the path ahead to permit natural corner-cutting,
            // instead of strictly clamping to the ball's absolute position.
            return outputPosition + movementDirection * forwardLookahead;
        }

        private Vector2 getSliderTrackingDesiredPoint(Vector2 projectedPoint, Vector2 tangent, Vector2 ballPosition, float followRadius)
        {
            Vector2 tangentDirection = normaliseOrZero(tangent);
            float forwardIntent = Math.Max(0, Vector2.Dot(normaliseOrZero(averagedVelocity), tangentDirection));
            
            // Allow leading the slider ball fluidly 
            float leadDistance = Math.Min(followRadius * 0.65f, averagedVelocity.Length * 0.025f) * forwardIntent;
            Vector2 anticipatedPathPoint = projectedPoint + tangentDirection * leadDistance;

            // Blend between the ball center and the path ahead.
            // This prevents rigid "lock-on" to the ball and gives the player the freedom to draw their own path within the follow circle.
            Vector2 desiredPoint = interpolate(ballPosition, anticipatedPathPoint, 0.6f);
            Vector2 relativeToBall = desiredPoint - ballPosition;

            // Allow the soft-target to exist out to 1.1x the follow radius, so edge-tracking doesn't cause rubber-bands.
            return ballPosition + clampLength(relativeToBall, followRadius * 1.1f);
        }

        private AimAssistContext createSliderTailContext(TargetDescriptor current, DrawableSlider slider, TargetDescriptor? next, Vector2 rawPosition, bool shortRepeatSlider)
        {
            Vector2 desiredPoint;
            Vector2 tangent;

            if (shortRepeatSlider)
            {
                double midpointProgress = getFinalSpanMidProgress(slider);
                desiredPoint = toScreenSpace(slider.HitObject.StackedPositionAt(midpointProgress));
                tangent = getSliderProgressTangent(slider, midpointProgress, 0.18 / slider.HitObject.SpanCount());
            }
            else
            {
                desiredPoint = toScreenSpace(slider.HitObject.StackedEndPosition);
                tangent = normaliseOrZero(
                    toScreenSpace(slider.HitObject.StackedEndPosition) - toScreenSpace(slider.HitObject.StackedPositionAt(0.92)));
            }

            Vector2 toDesired = desiredPoint - rawPosition;

            return new AimAssistContext(
                shortRepeatSlider ? @"slider-repeat" : @"slider",
                current.Drawable,
                desiredPoint,
                normaliseOrZero(toDesired),
                tangent,
                desiredPoint,
                getSliderFollowRadius(current.Radius),
                toDesired.Length,
                current.EndTime,
                Math.Max(120, current.EndTime - current.StartTime),
                true,
                next?.Drawable != null ? next.Value.ScreenSpacePosition : null,
                next?.Drawable != null ? next.Value.Radius : 0,
                0);
        }

        private bool isShortRepeatSlider(DrawableSlider slider, float headRadius)
        {
            if (slider.HitObject.RepeatCount <= 0)
                return false;

            float followRadius = getSliderFollowRadius(headRadius);
            double firstSpanEndProgress = 1d / slider.HitObject.SpanCount();
            Vector2 spanStart = toScreenSpace(slider.HitObject.StackedPositionAt(0));
            Vector2 spanEnd = toScreenSpace(slider.HitObject.StackedPositionAt(firstSpanEndProgress));

            return (spanEnd - spanStart).Length <= followRadius * 1.9f;
        }

        private double getFinalSpanMidProgress(DrawableSlider slider)
            => ((slider.HitObject.SpanCount() - 1) + 0.5) / slider.HitObject.SpanCount();

        private double getSpanMidProgress(DrawableSlider slider, double overallProgress)
        {
            int spanIndex = Math.Clamp(slider.HitObject.SpanAt(overallProgress), 0, slider.HitObject.SpanCount() - 1);
            return (spanIndex + 0.5) / slider.HitObject.SpanCount();
        }

        private Vector2 getSliderProgressTangent(DrawableSlider slider, double progress, double progressDelta)
        {
            double startProgress = Math.Max(0, progress - progressDelta);
            double endProgress = Math.Min(1, progress + progressDelta);

            if (endProgress - startProgress <= 0.0001)
                return Vector2.Zero;

            return normaliseOrZero(
                toScreenSpace(slider.HitObject.StackedPositionAt(endProgress)) - toScreenSpace(slider.HitObject.StackedPositionAt(startProgress)));
        }
    }
}
