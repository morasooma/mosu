// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Transforms;
using osu.Framework.Utils;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class AimAssistController
    {
        private OsuPatternState[] analyzePatternStates(IReadOnlyList<TargetDescriptor> allTargets, int currentIndex, TargetDescriptor? previous)
        {
            if (allTargets.Count == 0 || currentIndex < 0 || currentIndex >= allTargets.Count)
                return Array.Empty<OsuPatternState>();

            patternNodeBuffer.Clear();
            IReadOnlyList<TargetDescriptor> flowHistory = getFlowHistoryTargets(allTargets[currentIndex], previous);
            int visibleStartIndex = currentIndex;

            for (int i = 0; i < flowHistory.Count; i++)
            {
                TargetDescriptor historyTarget = flowHistory[i];
                patternNodeBuffer.Add(new OsuPatternNode(
                    historyTarget.ScreenSpacePosition,
                    historyTarget.StartTime,
                    historyTarget.Radius,
                    historyTarget.Drawable is DrawableHitCircle));
            }

            for (int i = visibleStartIndex; i < allTargets.Count; i++)
            {
                TargetDescriptor target = allTargets[i];
                patternNodeBuffer.Add(new OsuPatternNode(
                    target.ScreenSpacePosition,
                    target.StartTime,
                    target.Radius,
                    target.Drawable is DrawableHitCircle));
            }

            OsuPatternState[] analysedStates = OsuPatternSegmentAnalyzer.Analyze(patternNodeBuffer, getBeatLengthAtIndex);
            OsuPatternState[] translatedStates = new OsuPatternState[allTargets.Count];

            for (int i = visibleStartIndex; i < allTargets.Count; i++)
            {
                int localIndex = flowHistory.Count + i - visibleStartIndex;
                translatedStates[i] = translatePatternState(analysedStates[localIndex], flowHistory.Count, visibleStartIndex, allTargets.Count - 1);
            }

            return translatedStates;

            double getBeatLengthAtIndex(int index)
            {
                if (index < flowHistory.Count)
                    return getBeatLengthAt(flowHistory[index].StartTime);

                int targetIndex = visibleStartIndex + index - flowHistory.Count;
                return getBeatLengthAt(allTargets[targetIndex].StartTime);
            }
        }

        private static OsuPatternState translatePatternState(OsuPatternState state, int historyCount, int visibleStartIndex, int maxIndex)
        {
            OsuPatternInfo patternInfo = state.PatternInfo;
            int translatedChainStart = Math.Clamp(visibleStartIndex + patternInfo.ChainStartIndex - historyCount, 0, maxIndex);
            int translatedChainEnd = Math.Clamp(visibleStartIndex + patternInfo.ChainEndIndex - historyCount, 0, maxIndex);
            int translatedSegmentStart = Math.Clamp(visibleStartIndex + state.SegmentStartIndex - historyCount, 0, maxIndex);
            int translatedSegmentEnd = Math.Clamp(visibleStartIndex + state.SegmentEndIndex - historyCount, 0, maxIndex);

            OsuPatternInfo translatedInfo = new OsuPatternInfo(
                patternInfo.Kind,
                patternInfo.StreamShape,
                patternInfo.StreamSpacing,
                translatedChainStart,
                translatedChainEnd,
                patternInfo.DensityWeight,
                patternInfo.ContinuityWeight,
                patternInfo.AverageSpacingRatio,
                patternInfo.SpacingVariance);

            return new OsuPatternState(translatedInfo, state.Candidate, translatedSegmentStart, translatedSegmentEnd, state.JumpSeverity);
        }

        private List<TargetDescriptor> buildRailTargets(TargetDescriptor current, TargetDescriptor? next, IReadOnlyList<TargetDescriptor> allTargets, int currentIndex, OsuPatternState patternState)
        {
            railTargetBuffer.Clear();

            if (currentIndex < 0)
                return railTargetBuffer;

            if (patternState.SupportsFlowPath && patternState.SegmentEndIndex > currentIndex)
            {
                int startIndex = Math.Clamp(patternState.SegmentStartIndex, 0, currentIndex);
                int endIndex = Math.Min(patternState.SegmentEndIndex, allTargets.Count - 1);
                int maxFlowTargets = patternState.IsCommittedStream ? 24 : 12;

                for (int i = startIndex; i <= endIndex && railTargetBuffer.Count < maxFlowTargets; i++)
                    railTargetBuffer.Add(allTargets[i]);

                return railTargetBuffer;
            }

            if (next == null)
                return railTargetBuffer;

            if (!isRailSegment(current, next.Value))
                return railTargetBuffer;

            railTargetBuffer.Add(current);
            TargetDescriptor previous = current;

            for (int i = currentIndex + 1; i < allTargets.Count && railTargetBuffer.Count < 8; i++)
            {
                TargetDescriptor candidate = allTargets[i];

                if (!isRailSegment(previous, candidate))
                    break;

                if (!continuesRailShape(railTargetBuffer, candidate))
                    break;

                railTargetBuffer.Add(candidate);
                previous = candidate;
            }

            return railTargetBuffer;
        }

        private bool isRailSegment(TargetDescriptor start, TargetDescriptor end)
        {
            return OsuPatternClassifier.IsFlowConnection(
                new OsuPatternNode(start.ScreenSpacePosition, start.StartTime, start.Radius, start.Drawable is DrawableHitCircle),
                new OsuPatternNode(end.ScreenSpacePosition, end.StartTime, end.Radius, end.Drawable is DrawableHitCircle),
                getBeatLengthAt(start.StartTime));
        }

        private int getRailBurstLength(TargetDescriptor current, IReadOnlyList<TargetDescriptor> allTargets, int currentIndex)
        {
            if (current.Drawable is not DrawableHitCircle)
                return 0;

            if (currentIndex < 0)
                return 0;

            int length = 1;
            TargetDescriptor anchor = current;

            for (int i = currentIndex - 1; i >= 0 && length < 6; i--)
            {
                TargetDescriptor candidate = allTargets[i];

                if (!isRailSegment(candidate, anchor))
                    break;

                length++;
                anchor = candidate;
            }

            anchor = current;

            for (int i = currentIndex + 1; i < allTargets.Count && length < 6; i++)
            {
                TargetDescriptor candidate = allTargets[i];

                if (!isRailSegment(anchor, candidate))
                    break;

                length++;
                anchor = candidate;
            }

            return length;
        }

        private bool continuesRailShape(IReadOnlyList<TargetDescriptor> chain, TargetDescriptor candidate)
        {
            if (chain.Count < 2)
                return true;

            Vector2 previousStart = chain[^2].ScreenSpacePosition;
            Vector2 previousEnd = chain[^1].ScreenSpacePosition;
            Vector2 candidatePoint = candidate.ScreenSpacePosition;
            Vector2 previousDirection = normaliseOrZero(previousEnd - previousStart);
            Vector2 nextDirection = normaliseOrZero(candidatePoint - previousEnd);

            if (previousDirection.LengthSquared <= 0.0001f || nextDirection.LengthSquared <= 0.0001f)
                return true;

            return Vector2.Dot(previousDirection, nextDirection) >= -0.45f;
        }

        private float getRailCornerAlignment(TargetDescriptor? previous, TargetDescriptor current, TargetDescriptor? next)
        {
            if (previous?.Drawable == null || next?.Drawable == null)
                return 1;

            Vector2 incoming = normaliseOrZero(current.ScreenSpacePosition - previous.Value.ScreenSpacePosition);
            Vector2 outgoing = normaliseOrZero(next.Value.ScreenSpacePosition - current.ScreenSpacePosition);

            if (incoming.LengthSquared <= 0.0001f || outgoing.LengthSquared <= 0.0001f)
                return 1;

            return Vector2.Dot(incoming, outgoing);
        }

        private float getRailChainAlignment(IReadOnlyList<TargetDescriptor> railTargets)
        {
            if (railTargets.Count < 3)
                return 1;

            float worstAlignment = 1;

            for (int i = 0; i < railTargets.Count - 2; i++)
            {
                Vector2 first = normaliseOrZero(railTargets[i + 1].ScreenSpacePosition - railTargets[i].ScreenSpacePosition);
                Vector2 second = normaliseOrZero(railTargets[i + 2].ScreenSpacePosition - railTargets[i + 1].ScreenSpacePosition);

                if (first.LengthSquared <= 0.0001f || second.LengthSquared <= 0.0001f)
                    continue;

                worstAlignment = Math.Min(worstAlignment, Vector2.Dot(first, second));
            }

            return worstAlignment;
        }

        private float getRailMotionScale(TargetDescriptor? previous, TargetDescriptor current, TargetDescriptor? next, IReadOnlyList<TargetDescriptor> railTargets)
        {
            float cornerAlignment = getRailCornerAlignment(previous, current, next);
            float chainAlignment = getRailChainAlignment(railTargets);
            float cornerWeight = Math.Clamp((cornerAlignment + 0.1f) / 0.9f, 0, 1);
            float chainWeight = Math.Clamp((chainAlignment + 0.2f) / 1.0f, 0.2f, 1);
            return Math.Min(cornerWeight, chainWeight);
        }

        private float getSliderFollowRadius(float headRadius) => headRadius * DrawableSliderBall.FOLLOW_AREA;

        private void sampleSliderRail(List<Vector2> result, DrawableSlider slider, double startProgress = 0, double endProgress = 1)
        {
            result.Clear();
            startProgress = Math.Clamp(startProgress, 0, 1);
            endProgress = Math.Clamp(endProgress, startProgress + 0.001, 1);
            double remainingProgress = Math.Max(0.05, endProgress - startProgress);
            int sampleCount = Math.Clamp((int)Math.Ceiling(remainingProgress * 64), 24, 72);

            if (result.Capacity < sampleCount + 1)
                result.Capacity = sampleCount + 1;

            for (int i = 0; i <= sampleCount; i++)
            {
                double t = Interpolation.ValueAt(i, startProgress, endProgress, 0, sampleCount, Easing.None);
                result.Add(toScreenSpace(slider.HitObject.StackedPositionAt(t)));
            }
        }

        private void fillRailProjectionPointBuffer(TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> targets, OsuPatternInfo patternInfo = default)
        {
            railSourcePointBuffer.Clear();
            railSourceTimeBuffer.Clear();
            railDescriptorBuffer.Clear();
            IReadOnlyList<TargetDescriptor> flowHistory = targets.Count > 0
                ? getFlowHistoryTargets(targets[0], previous)
                : Array.Empty<TargetDescriptor>();

            for (int i = 0; i < flowHistory.Count; i++)
                railDescriptorBuffer.Add(flowHistory[i]);

            if (previous?.Drawable != null
                && targets.Count > 0
                && isRailSegment(previous.Value, targets[0])
                && (flowHistory.Count == 0 || !ReferenceEquals(flowHistory[^1].HitObject, previous.Value.HitObject)))
                railDescriptorBuffer.Add(previous.Value);

            for (int i = 0; i < targets.Count; i++)
                railDescriptorBuffer.Add(targets[i]);

            for (int i = 0; i < railDescriptorBuffer.Count; i++)
            {
                TargetDescriptor descriptor = railDescriptorBuffer[i];
                TargetDescriptor? sourcePrevious = i > 0 ? railDescriptorBuffer[i - 1] : null;
                TargetDescriptor? sourceNext = i + 1 < railDescriptorBuffer.Count ? railDescriptorBuffer[i + 1] : null;
                Vector2 point = getRailGuidePoint(sourcePrevious, descriptor, sourceNext, patternInfo);
                addSourcePoint(point, descriptor.StartTime);
            }

            fillProjectionPointBuffer(railSourcePointBuffer);

            void addSourcePoint(Vector2 point, double time)
            {
                if (railSourcePointBuffer.Count > 0 && (railSourcePointBuffer[^1] - point).LengthSquared <= 0.04f)
                {
                    railSourceTimeBuffer[^1] = time;
                    return;
                }

                railSourcePointBuffer.Add(point);
                railSourceTimeBuffer.Add(time);
            }
        }

        private Vector2 getRailGuidePoint(TargetDescriptor? previous, TargetDescriptor current, TargetDescriptor? next, OsuPatternInfo patternInfo)
        {
            if (!patternInfo.IsContinuousFlow || current.Drawable is not DrawableHitCircle)
                return current.ScreenSpacePosition;

            Vector2 currentPosition = current.ScreenSpacePosition;
            Vector2 guideDirection = getBurstFlowDirection(previous, current, next);
            float naturalCenteringBias = getNaturalCenteringBias(patternInfo);
            float curveFreedom = 1 - naturalCenteringBias;

            if (guideDirection.LengthSquared <= 0.0001f)
                return currentPosition;

            float neighbourDistance = next?.Drawable != null
                ? (next.Value.ScreenSpacePosition - currentPosition).Length
                : previous?.Drawable != null
                    ? (currentPosition - previous.Value.ScreenSpacePosition).Length
                    : 0;
            float spacingLeadRatio = patternInfo.StreamSpacing switch
            {
                OsuStreamSpacingKind.Spaced => 0.26f,
                OsuStreamSpacingKind.Variable => 0.22f,
                OsuStreamSpacingKind.Tight => 0.14f,
                _ => 0.18f
            };
            float shapeLeadMultiplier = patternInfo.StreamShape switch
            {
                OsuStreamShapeKind.ZigZag => 0.82f,
                OsuStreamShapeKind.Arc => 1.04f,
                _ => 1f
            };
            float guideDistance = Math.Min(
                Math.Max(current.Radius * 0.42f, 7f),
                Math.Max(current.Radius * 0.16f, neighbourDistance * spacingLeadRatio)) * shapeLeadMultiplier;

            Vector2 forwardGuidePoint = currentPosition + guideDirection * guideDistance;
            float cornerCutWeight = getRailCornerCutWeight(previous, current, next, patternInfo);

            if (cornerCutWeight <= 0)
                return clampRailGuidePoint(currentPosition, forwardGuidePoint, current.Radius, patternInfo);

            Vector2 cornerCutPoint = getRailCornerCutPoint(previous, current, next);
            float cornerCutBlend = Math.Clamp(cornerCutWeight * (0.72f + 0.4f * curveFreedom), 0, 0.96f);
            Vector2 guidePoint = interpolate(forwardGuidePoint, cornerCutPoint, cornerCutBlend);
            float minimumForwardLead = Math.Min(
                Math.Max(current.Radius * (float)Interpolation.Lerp(0.05f, 0.16f, cornerCutBlend), 2f),
                Math.Max(4f, neighbourDistance * 0.08f));
            float forwardDistance = Vector2.Dot(guidePoint - currentPosition, guideDirection);

            if (forwardDistance < minimumForwardLead)
                guidePoint += guideDirection * (minimumForwardLead - forwardDistance);

            return clampRailGuidePoint(currentPosition, guidePoint, current.Radius, patternInfo);
        }

        private Vector2 getRailCornerCutPoint(TargetDescriptor? previous, TargetDescriptor current, TargetDescriptor? next)
        {
            if (previous?.Drawable == null || next?.Drawable == null)
                return current.ScreenSpacePosition;

            Vector2 chord = next.Value.ScreenSpacePosition - previous.Value.ScreenSpacePosition;
            float chordLengthSquared = chord.LengthSquared;

            if (chordLengthSquared <= 0.0001f)
                return current.ScreenSpacePosition;

            Vector2 currentPosition = current.ScreenSpacePosition;
            float t = Math.Clamp(Vector2.Dot(currentPosition - previous.Value.ScreenSpacePosition, chord) / chordLengthSquared, 0.18f, 0.82f);
            Vector2 chordPoint = previous.Value.ScreenSpacePosition + chord * t;
            float maximumCornerCut = Math.Min(
                Math.Max(current.Radius * 0.62f, 6f),
                Math.Max(
                    current.Radius * 0.18f,
                    Math.Min(
                        (currentPosition - previous.Value.ScreenSpacePosition).Length,
                        (next.Value.ScreenSpacePosition - currentPosition).Length) * 0.22f));

            return currentPosition + clampLength(chordPoint - currentPosition, maximumCornerCut);
        }

        private float getRailCornerCutWeight(TargetDescriptor? previous, TargetDescriptor current, TargetDescriptor? next, OsuPatternInfo patternInfo)
        {
            if (previous?.Drawable == null || next?.Drawable == null)
                return 0;

            float naturalCenteringBias = getNaturalCenteringBias(patternInfo);
            float cornerAlignment = getRailCornerAlignment(previous, current, next);
            float cornerSharpness = Math.Clamp((0.94f - cornerAlignment) / 1.66f, 0, 1);

            if (cornerSharpness <= 0)
                return 0;

            float shapeWeight = patternInfo.StreamShape switch
            {
                OsuStreamShapeKind.Arc => 1f,
                OsuStreamShapeKind.ZigZag => 0.42f,
                _ => 0.72f
            };
            float spacingWeight = patternInfo.StreamSpacing switch
            {
                OsuStreamSpacingKind.Spaced => 0.86f,
                OsuStreamSpacingKind.Variable => 0.78f,
                OsuStreamSpacingKind.Tight => 0.54f,
                _ => 0.68f
            };
            float densityWeight = Math.Clamp((0.76f - patternInfo.DensityWeight) / 0.76f, 0.14f, 1f);
            float continuityWeight = Math.Clamp((patternInfo.ContinuityWeight - 0.18f) / 0.82f, 0.18f, 1f);
            float curveFreedom = 1 - naturalCenteringBias;
            float naturalFloor = patternInfo.StreamShape switch
            {
                OsuStreamShapeKind.Arc => 0.22f,
                OsuStreamShapeKind.ZigZag => 0.1f,
                _ => 0.04f
            };

            return Math.Clamp(
                Math.Max(
                    naturalFloor * continuityWeight,
                    cornerSharpness
                    * shapeWeight
                    * spacingWeight
                    * (0.28f + 0.72f * continuityWeight)
                    * (0.44f + 0.56f * densityWeight))
                * (0.72f + 0.4f * curveFreedom),
                0,
                0.94f);
        }

        private float getNaturalCenteringBias(OsuPatternInfo patternInfo)
        {
            float configuredBias = Math.Clamp((float)getConfiguredCenterBias(), 0, 1);

            if (!patternInfo.IsContinuousFlow)
                return configuredBias;

            float curveCap = patternInfo.StreamShape switch
            {
                OsuStreamShapeKind.Arc => 0.34f,
                OsuStreamShapeKind.ZigZag => 0.26f,
                _ => 0.5f
            };

            curveCap += patternInfo.StreamSpacing switch
            {
                OsuStreamSpacingKind.Spaced => 0.1f,
                OsuStreamSpacingKind.Variable => 0.06f,
                _ => 0f
            };

            if (patternInfo.IsLowDensityFlow)
                curveCap += 0.06f;

            return Math.Min(configuredBias, Math.Clamp(curveCap, 0.18f, 0.62f));
        }

        private Vector2 clampRailGuidePoint(Vector2 currentPosition, Vector2 guidePoint, float radius, OsuPatternInfo patternInfo)
        {
            if (radius <= 0.01f)
                return guidePoint;

            float curveFreedom = 1 - getNaturalCenteringBias(patternInfo);
            float radiusLimitRatio = patternInfo.StreamShape switch
            {
                OsuStreamShapeKind.Arc => (float)Interpolation.Lerp(0.58f, 0.84f, curveFreedom),
                OsuStreamShapeKind.ZigZag => (float)Interpolation.Lerp(0.42f, 0.66f, curveFreedom),
                _ => (float)Interpolation.Lerp(0.44f, 0.58f, curveFreedom * 0.6f)
            };

            radiusLimitRatio += patternInfo.StreamSpacing switch
            {
                OsuStreamSpacingKind.Spaced => 0.08f,
                OsuStreamSpacingKind.Variable => 0.05f,
                _ => 0f
            };

            return currentPosition + clampLength(guidePoint - currentPosition, Math.Max(5f, radius * radiusLimitRatio));
        }

        private ProjectionResult getContinuousRailProjection(TargetDescriptor current, TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> railTargets, TargetDescriptor? next,
                                                             Vector2 rawPosition, float motionScale, OsuPatternInfo patternInfo = default)
        {
            prepareRailProjectionPath(current, previous, railTargets, patternInfo);

            Vector2 projectionQuery = getRailProjectionQuery(rawPosition, motionScale);
            return projectOntoPolyline(projectionPointBuffer, projectionQuery);
        }

        private void prepareRailProjectionPath(TargetDescriptor current, TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> railTargets, OsuPatternInfo patternInfo)
        {
            if (patternInfo.IsContinuousFlow && tryApplyCommittedStreamProjectionPath(current, railTargets, patternInfo))
                return;

            fillRailProjectionPointBuffer(previous, railTargets, patternInfo);

            if (patternInfo.IsContinuousFlow)
                cacheCommittedStreamProjectionPath(patternInfo);
        }

        private bool tryApplyCommittedStreamProjectionPath(TargetDescriptor current, IReadOnlyList<TargetDescriptor> railTargets, OsuPatternInfo patternInfo)
        {
            if (!hasCommittedStreamProjectionState)
                return false;

            if (committedStreamPathBuffer.Count < 2
                || committedStreamSourceBuffer.Count < 2
                || committedStreamSourceBuffer.Count != committedStreamSourceTimeBuffer.Count)
            {
                clearCommittedStreamProjectionState();
                return false;
            }

            if (patternInfo.StreamShape != committedStreamShape || patternInfo.StreamSpacing != committedStreamSpacing)
                return false;

            double startTolerance = scaleRealTimeWindow(140);
            double endTolerance = scaleRealTimeWindow(180);
            double visibleEndTime = railTargets.Count > 0 ? railTargets[^1].StartTime : current.StartTime;

            if (current.StartTime < committedStreamStartTime - startTolerance
                || current.StartTime > committedStreamEndTime + endTolerance
                || visibleEndTime > committedStreamEndTime + endTolerance)
                return false;

            railSourcePointBuffer.Clear();
            railSourceTimeBuffer.Clear();
            projectionPointBuffer.Clear();

            railSourcePointBuffer.AddRange(committedStreamSourceBuffer);
            railSourceTimeBuffer.AddRange(committedStreamSourceTimeBuffer);
            projectionPointBuffer.AddRange(committedStreamPathBuffer);
            rebuildProjectionArcLengths();
            return true;
        }

        private void cacheCommittedStreamProjectionPath(OsuPatternInfo patternInfo)
        {
            if (projectionPointBuffer.Count < 2
                || railSourcePointBuffer.Count < 2
                || railSourcePointBuffer.Count != railSourceTimeBuffer.Count)
                return;

            committedStreamPathBuffer.Clear();
            committedStreamSourceBuffer.Clear();
            committedStreamSourceTimeBuffer.Clear();

            committedStreamPathBuffer.AddRange(projectionPointBuffer);
            committedStreamSourceBuffer.AddRange(railSourcePointBuffer);
            committedStreamSourceTimeBuffer.AddRange(railSourceTimeBuffer);
            committedStreamStartTime = railSourceTimeBuffer[0];
            committedStreamEndTime = railSourceTimeBuffer[^1];
            committedStreamShape = patternInfo.StreamShape;
            committedStreamSpacing = patternInfo.StreamSpacing;
            hasCommittedStreamProjectionState = true;
        }

        private bool tryGetTimedRailFollowerProjection(OsuPatternInfo patternInfo, double currentStartTime, out ProjectionResult projection)
        {
            projection = default;
            clearStreamPathTimingState();
            return false;
        }

        private float getTimedRailFollowerWeight(TargetDescriptor current, TargetDescriptor? next, Vector2 rawPosition, ProjectionResult spatialProjection, ProjectionResult timedProjection,
                                                 float motionScale, OsuPatternInfo patternInfo)
        {
            float flowWeight = getTimedRailFlowWeight(patternInfo, current.StartTime);
            bool lowDensityFlow = patternInfo.IsLowDensityFlow;
            float gentleFlowWeight = Time.Current >= current.StartTime
                ? getGentleFlowAssistWeight(patternInfo)
                : 0;

            if (flowWeight <= 0)
                return 0;

            Vector2 outputPosition = rawPosition + assistOffset;
            float proximityRadius = Math.Max(current.Radius * 1.45f, 18f);
            float pathDistance = (outputPosition - spatialProjection.Point).Length;
            float proximityWeight = Math.Clamp(1 - pathDistance / proximityRadius, 0, 1);
            Vector2 movementDirection = normaliseOrZero(averagedVelocity);
            Vector2 timedDirection = normaliseOrZero(timedProjection.Tangent);
            float tangentAlignment = movementDirection.LengthSquared > 0.0001f && timedDirection.LengthSquared > 0.0001f
                ? Vector2.Dot(movementDirection, timedDirection)
                : 0;
            float alignmentWeight = Math.Clamp((tangentAlignment + 0.18f) / 1.18f, 0, 1);
            float speedWeight = Math.Clamp((averagedVelocity.Length - stationary_hold_speed_threshold * 0.2f) / 220f, 0, 1);
            float continuityWeight = Math.Max(proximityWeight, lowDensityFlow ? 0.56f : 0.42f);
            continuityWeight *= Math.Max(lowDensityFlow ? 0.64f : 0.5f, alignmentWeight * (lowDensityFlow ? 0.82f : 0.74f) + speedWeight * (lowDensityFlow ? 0.18f : 0.26f));
            continuityWeight *= lowDensityFlow
                ? 0.6f + 0.4f * Math.Max(motionScale, patternInfo.ContinuityWeight)
                : 0.48f + 0.52f * motionScale;

            if (next?.Drawable != null)
            {
                double upcomingGap = toRealTimeWindow(Math.Max(0, next.Value.StartTime - Time.Current));
                float pressure = (float)Math.Clamp(1 - upcomingGap / 150.0, 0, 1);
                continuityWeight *= (float)Interpolation.Lerp(lowDensityFlow ? 0.82f : 0.7f, 1.0f, pressure);
            }

            float followerWeight = Math.Clamp(flowWeight * continuityWeight, 0, lowDensityFlow ? 0.72f : 0.68f);

            if (gentleFlowWeight > 0)
                followerWeight *= (float)Interpolation.Lerp(1f, 0.82f, gentleFlowWeight);

            if (patternInfo.IsContinuousFlow)
            {
                float followerFloor = patternInfo.StreamSpacing switch
                {
                    OsuStreamSpacingKind.Spaced => 0.34f,
                    OsuStreamSpacingKind.Variable => 0.42f,
                    _ => 0.48f
                };

                if (patternInfo.StreamShape == OsuStreamShapeKind.ZigZag)
                    followerFloor = Math.Min(followerFloor, 0.38f);

                if (patternInfo.IsLowDensityFlow)
                    followerFloor *= 0.82f;

                followerWeight = Math.Max(followerWeight, followerFloor * (0.45f + 0.55f * continuityWeight));
            }

            return followerWeight;
        }

        private float getTimedRailFlowWeight(OsuPatternInfo patternInfo, double currentStartTime = double.NegativeInfinity)
        {
            float densityWeight = getTimedRailDensityWeight();

            if (!patternInfo.SupportsFlowAim)
                return densityWeight;

            float continuityWeight = Math.Clamp((patternInfo.ContinuityWeight - 0.44f) / 0.44f, 0, 1);
            float chainWeight = Math.Clamp((patternInfo.ChainLength - 3) / 2f, 0, 1);
            float looseFlowWeight = Math.Clamp((0.62f - patternInfo.DensityWeight) / 0.62f, 0, 1);
            float flowWeight = continuityWeight * (0.24f + 0.48f * chainWeight) * (0.42f + 0.58f * looseFlowWeight);

            if (patternInfo.StreamSpacing is OsuStreamSpacingKind.Spaced or OsuStreamSpacingKind.Variable)
                flowWeight = Math.Max(flowWeight, continuityWeight * 0.36f);

            float combinedWeight = Math.Max(densityWeight, Math.Clamp(flowWeight, 0, 0.74f));
            float gentleFlowWeight = Time.Current >= currentStartTime
                ? getGentleFlowAssistWeight(patternInfo)
                : 0;

            if (gentleFlowWeight > 0)
                combinedWeight = (float)Interpolation.Lerp(combinedWeight, Math.Min(combinedWeight, 0.68f), gentleFlowWeight * 0.4f);

            return combinedWeight;
        }

        private float getTimedRailDensityWeight()
        {
            if (railSourceTimeBuffer.Count < 3)
                return 0;

            double totalGap = 0;
            double largestGap = 0;
            int gapCount = 0;

            for (int i = 0; i < railSourceTimeBuffer.Count - 1; i++)
            {
                double gap = toRealTimeWindow(Math.Max(0, railSourceTimeBuffer[i + 1] - railSourceTimeBuffer[i]));

                if (gap <= 0.01)
                    continue;

                totalGap += gap;
                largestGap = Math.Max(largestGap, gap);
                gapCount++;
            }

            if (gapCount < 2)
                return 0;

            double averageGap = totalGap / gapCount;
            float averageWeight = (float)Math.Clamp((150 - averageGap) / 90.0, 0, 1);
            float largestWeight = (float)Math.Clamp((165 - largestGap) / 95.0, 0, 1);
            return Math.Clamp(averageWeight * 0.66f + largestWeight * 0.34f, 0, 1);
        }

        private bool shouldUseContinuousStreamContext(TargetDescriptor current, TargetDescriptor? previous, IReadOnlyList<TargetDescriptor> railTargets, OsuPatternState patternState)
        {
            if (current.Drawable is not DrawableHitCircle || railTargets.Count < 2)
                return false;

            return patternState.IsCommittedStream;
        }

        private bool tryGetTimedRailPathProgress(double currentTime, OsuPatternInfo patternInfo, out float pathProgress)
        {
            pathProgress = 0;

            if (railSourceTimeBuffer.Count < 2)
            {
                clearStreamPathTimingState();
                return false;
            }

            double localStartTime = railSourceTimeBuffer[0];
            double localEndTime = railSourceTimeBuffer[^1];

            if (localEndTime <= localStartTime + 0.001)
            {
                clearStreamPathTimingState();
                return false;
            }

            double resetGap = scaleRealTimeWindow(220);
            double rewindTolerance = scaleRealTimeWindow(320);
            bool resetState = !hasStreamPathTimingState
                              || currentTime < streamPathLastSampleTime - 1
                              || currentTime - streamPathLastSampleTime > resetGap
                              || localStartTime > streamPathEndTime + resetGap
                              || localEndTime < streamPathEndTime - rewindTolerance;

            if (resetState)
            {
                hasStreamPathTimingState = true;
                streamPathStartTime = localStartTime;
                streamPathEndTime = localEndTime;
                streamPathProgress = 0;
            }
            else
            {
                streamPathStartTime = Math.Min(streamPathStartTime, localStartTime);
                streamPathEndTime = Math.Max(streamPathEndTime, localEndTime);
            }

            if (streamPathEndTime <= streamPathStartTime + 0.001)
                return false;

            float idealProgress = (float)Math.Clamp((currentTime - streamPathStartTime) / (streamPathEndTime - streamPathStartTime), 0, 1);
            float naturalLag = patternInfo.StreamSpacing switch
            {
                OsuStreamSpacingKind.Spaced => 0.08f,
                OsuStreamSpacingKind.Variable => 0.065f,
                _ => 0.045f
            };

            if (patternInfo.StreamShape == OsuStreamShapeKind.Arc)
                naturalLag += 0.02f;
            else if (patternInfo.StreamShape == OsuStreamShapeKind.ZigZag)
                naturalLag *= 0.7f;

            if (patternInfo.IsLowDensityFlow)
                naturalLag += 0.025f;

            idealProgress = Math.Max(0, idealProgress - naturalLag);

            if (resetState)
                streamPathProgress = Math.Max(0, idealProgress * 0.78f);

            double sampleElapsed = double.IsNegativeInfinity(streamPathLastSampleTime)
                ? 0
                : Math.Max(0, currentTime - streamPathLastSampleTime);
            float catchupBlend = sampleElapsed <= 0
                ? 0.16f
                : (float)Math.Clamp(sampleElapsed / scaleRealTimeWindow(48), 0.08f, 0.26f);

            if (patternInfo.StreamSpacing == OsuStreamSpacingKind.Spaced)
                catchupBlend = Math.Min(catchupBlend, 0.18f);
            else if (patternInfo.StreamShape == OsuStreamShapeKind.Arc)
                catchupBlend = Math.Min(catchupBlend, 0.2f);

            pathProgress = Math.Max(streamPathProgress, (float)Interpolation.Lerp(streamPathProgress, idealProgress, catchupBlend));
            streamPathProgress = pathProgress;
            streamPathLastSampleTime = currentTime;
            return true;
        }

        private Vector2 sampleRailPathAtProgress(float progress)
        {
            if (projectionPointBuffer.Count == 0)
                return Vector2.Zero;

            if (projectionPointBuffer.Count == 1 || projectionArcLengthBuffer.Count != projectionPointBuffer.Count)
                return projectionPointBuffer[0];

            float totalLength = projectionArcLengthBuffer[^1];

            if (totalLength <= 0.001f)
                return projectionPointBuffer[^1];

            float targetLength = totalLength * Math.Clamp(progress, 0, 1);

            for (int i = 1; i < projectionArcLengthBuffer.Count; i++)
            {
                float startLength = projectionArcLengthBuffer[i - 1];
                float endLength = projectionArcLengthBuffer[i];

                if (targetLength > endLength && i < projectionArcLengthBuffer.Count - 1)
                    continue;

                float segmentLength = Math.Max(0.001f, endLength - startLength);
                float segmentProgress = Math.Clamp((targetLength - startLength) / segmentLength, 0, 1);
                return interpolate(projectionPointBuffer[i - 1], projectionPointBuffer[i], segmentProgress);
            }

            return projectionPointBuffer[^1];
        }

        private Vector2 getRailPathTangentAtProgress(float progress)
        {
            const float sample_offset = 0.018f;
            float previousSample = Math.Max(0, progress - sample_offset);
            float nextSample = Math.Min(1, progress + sample_offset);
            Vector2 tangent = sampleRailPathAtProgress(nextSample) - sampleRailPathAtProgress(previousSample);

            if (tangent.LengthSquared > 0.0001f)
                return tangent;

            return projectionPointBuffer.Count >= 2
                ? projectionPointBuffer[^1] - projectionPointBuffer[^2]
                : Vector2.Zero;
        }

        private void fillProjectionPointBuffer(IReadOnlyList<Vector2> points)
        {
            projectionPointBuffer.Clear();
            projectionArcLengthBuffer.Clear();

            if (points.Count == 0)
                return;

            if (points.Count < 3)
            {
                if (projectionPointBuffer.Capacity < points.Count)
                    projectionPointBuffer.Capacity = points.Count;

                for (int i = 0; i < points.Count; i++)
                    projectionPointBuffer.Add(points[i]);

                rebuildProjectionArcLengths();

                return;
            }

            float pathSmoothing = getRailPathSmoothing(points);

            if (pathSmoothing <= 0.04f)
            {
                if (projectionPointBuffer.Capacity < points.Count)
                    projectionPointBuffer.Capacity = points.Count;

                for (int i = 0; i < points.Count; i++)
                    projectionPointBuffer.Add(points[i]);

                rebuildProjectionArcLengths();

                return;
            }

            projectionPointBuffer.Add(points[0]);

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[i + 1];
                Vector2 startTangent = getRailSplineTangent(points, i, pathSmoothing);
                Vector2 endTangent = getRailSplineTangent(points, i + 1, pathSmoothing);
                float segmentSmoothing = getRailSegmentSmoothing(points, i, pathSmoothing);
                int sampleCount = getRailSplineSampleCount(start, end, segmentSmoothing);

                for (int sample = 1; sample <= sampleCount; sample++)
                {
                    float t = sample / (float)sampleCount;
                    Vector2 linearPoint = interpolate(start, end, t);
                    Vector2 curvedPoint = sampleHermite(start, end, startTangent, endTangent, t);
                    Vector2 point = interpolate(linearPoint, curvedPoint, segmentSmoothing);

                    if ((point - projectionPointBuffer[^1]).LengthSquared <= 0.04f)
                        continue;

                    projectionPointBuffer.Add(point);
                }
            }

            rebuildProjectionArcLengths();
        }

        private void rebuildProjectionArcLengths()
        {
            projectionArcLengthBuffer.Clear();

            if (projectionPointBuffer.Count == 0)
                return;

            projectionArcLengthBuffer.Add(0);

            for (int i = 1; i < projectionPointBuffer.Count; i++)
                projectionArcLengthBuffer.Add(projectionArcLengthBuffer[^1] + (projectionPointBuffer[i] - projectionPointBuffer[i - 1]).Length);
        }

        private float getRailPathSmoothing(IReadOnlyList<Vector2> points)
        {
            if (points.Count < 3)
                return 0;

            float worstAlignment = 1;
            float totalAlignment = 0;
            int alignmentCount = 0;

            for (int i = 0; i < points.Count - 2; i++)
            {
                Vector2 first = normaliseOrZero(points[i + 1] - points[i]);
                Vector2 second = normaliseOrZero(points[i + 2] - points[i + 1]);

                if (first.LengthSquared <= 0.0001f || second.LengthSquared <= 0.0001f)
                    continue;

                float alignment = Vector2.Dot(first, second);
                worstAlignment = Math.Min(worstAlignment, alignment);
                totalAlignment += alignment;
                alignmentCount++;
            }

            if (alignmentCount == 0)
                return 0;

            float averageAlignment = totalAlignment / alignmentCount;
            float worstWeight = Math.Clamp((worstAlignment + 0.18f) / 1.0f, 0, 1);
            float averageWeight = Math.Clamp((averageAlignment + 0.05f) / 1.05f, 0, 1);

            return Math.Clamp(worstWeight * 0.58f + averageWeight * 0.42f, 0, 1);
        }

        private float getRailSegmentSmoothing(IReadOnlyList<Vector2> points, int segmentIndex, float pathSmoothing)
        {
            float localAlignment = 1;

            if (segmentIndex > 0)
                localAlignment = Math.Min(localAlignment, getRailPointAlignment(points[segmentIndex - 1], points[segmentIndex], points[segmentIndex + 1]));

            if (segmentIndex + 2 < points.Count)
                localAlignment = Math.Min(localAlignment, getRailPointAlignment(points[segmentIndex], points[segmentIndex + 1], points[segmentIndex + 2]));

            float localWeight = Math.Clamp((localAlignment + 0.22f) / 1.02f, 0, 1);
            return Math.Clamp(pathSmoothing * localWeight, 0, 1);
        }

        private static float getRailPointAlignment(Vector2 first, Vector2 second, Vector2 third)
        {
            Vector2 incoming = normaliseOrZero(second - first);
            Vector2 outgoing = normaliseOrZero(third - second);

            if (incoming.LengthSquared <= 0.0001f || outgoing.LengthSquared <= 0.0001f)
                return 1;

            return Vector2.Dot(incoming, outgoing);
        }

        private Vector2 getRailSplineTangent(IReadOnlyList<Vector2> points, int index, float pathSmoothing)
        {
            Vector2 current = points[index];
            Vector2 incoming = index > 0 ? current - points[index - 1] : Vector2.Zero;
            Vector2 outgoing = index + 1 < points.Count ? points[index + 1] - current : Vector2.Zero;

            if (incoming.LengthSquared > 0.0001f && outgoing.LengthSquared > 0.0001f)
            {
                Vector2 incomingDirection = normaliseOrZero(incoming);
                Vector2 outgoingDirection = normaliseOrZero(outgoing);
                float alignment = Vector2.Dot(incomingDirection, outgoingDirection);
                float tangentWeight = pathSmoothing * Math.Clamp((alignment + 0.2f) / 1.2f, 0, 1);
                Vector2 blendedDirection = normaliseOrZero(incomingDirection + outgoingDirection);

                if (blendedDirection.LengthSquared <= 0.0001f)
                    blendedDirection = outgoingDirection;

                float baseMagnitude = Math.Min(incoming.Length, outgoing.Length);
                float magnitude = baseMagnitude * (float)Interpolation.Lerp(0.08f, 0.34f, tangentWeight);
                return blendedDirection * magnitude;
            }

            Vector2 segment = outgoing.LengthSquared > 0.0001f ? outgoing : incoming;

            if (segment.LengthSquared <= 0.0001f)
                return Vector2.Zero;

            float edgeMagnitude = segment.Length * (float)Interpolation.Lerp(0.06f, 0.18f, pathSmoothing);
            return normaliseOrZero(segment) * edgeMagnitude;
        }

        private int getRailSplineSampleCount(Vector2 start, Vector2 end, float segmentSmoothing)
        {
            float distance = (end - start).Length;
            int denseSamples = Math.Clamp((int)Math.Ceiling(distance / 14f), 4, 12);
            return Math.Clamp((int)Math.Round(Interpolation.Lerp(3, denseSamples, segmentSmoothing)), 3, 12);
        }

        private static Vector2 sampleHermite(Vector2 start, Vector2 end, Vector2 startTangent, Vector2 endTangent, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float h00 = 2 * t3 - 3 * t2 + 1;
            float h10 = t3 - 2 * t2 + t;
            float h01 = -2 * t3 + 3 * t2;
            float h11 = t3 - t2;

            return start * h00 + startTangent * h10 + end * h01 + endTangent * h11;
        }

        private double getGreatWindow(DrawableOsuHitObject drawable)
            => Math.Max(18, drawable.HitObject.HitWindows?.WindowFor(HitResult.Great) ?? 50);

        private ProjectionResult projectOntoPolyline(IReadOnlyList<Vector2> points, Vector2 query)
        {
            if (points.Count == 0)
                return new ProjectionResult(query, Vector2.Zero);

            if (points.Count == 1)
                return new ProjectionResult(points[0], Vector2.Zero);

            Vector2 bestPoint = points[0];
            Vector2 bestTangent = points[1] - points[0];
            float bestDistance = float.MaxValue;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 segmentStart = points[i];
                Vector2 segmentEnd = points[i + 1];
                Vector2 segment = segmentEnd - segmentStart;
                float segmentLengthSquared = segment.LengthSquared;

                if (segmentLengthSquared <= 0.0001f)
                    continue;

                float t = Math.Clamp(Vector2.Dot(query - segmentStart, segment) / segmentLengthSquared, 0, 1);
                Vector2 projectedPoint = segmentStart + segment * t;
                float distance = (query - projectedPoint).LengthSquared;

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestPoint = projectedPoint;
                bestTangent = segment;
            }

            return new ProjectionResult(bestPoint, bestTangent);
        }

        private Vector2 applyRailLead(Vector2 projectedPoint, Vector2 tangent, float motionScale)
        {
            if (tangent.LengthSquared <= 0.0001f)
                return projectedPoint;

            Vector2 tangentDirection = normaliseOrZero(tangent);
            float forwardIntent = Math.Max(0, Vector2.Dot(normaliseOrZero(averagedVelocity), tangentDirection));
            float leadDistance = Math.Min(18f, averagedVelocity.Length * 0.018f) * forwardIntent * motionScale;

            return projectedPoint + tangentDirection * leadDistance;
        }

        private Vector2 getRailProjectionQuery(Vector2 rawPosition, float motionScale)
        {
            Vector2 outputPosition = rawPosition + assistOffset;
            Vector2 movementDirection = normaliseOrZero(averagedVelocity);

            if (movementDirection.LengthSquared <= 0.0001f)
                return outputPosition;

            float forwardLookahead = Math.Min(22f, averagedVelocity.Length * 0.015f) * motionScale;
            return outputPosition + movementDirection * forwardLookahead;
        }

        private double getBeatLengthAt(double time)
        {
            double beatLength = beatmap?.ControlPointInfo.TimingPointAt(time).BeatLength ?? 500;
            return beatLength > 0 ? beatLength : 500;
        }

        private float getScreenSpaceRadius(Vector2 localPosition, float localRadius)
        {
            Vector2 centre = toScreenSpace(localPosition);
            Vector2 edge = toScreenSpace(localPosition + new Vector2(localRadius, 0));
            return (edge - centre).Length;
        }

        private Vector2 toScreenSpace(Vector2 localPosition) => playfield!.GamefieldToScreenSpace(localPosition);
    }
}
