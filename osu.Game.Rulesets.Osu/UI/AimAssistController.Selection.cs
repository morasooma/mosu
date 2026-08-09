// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class AimAssistController
    {
        private List<TargetDescriptor> getTargets()
        {
            targetBuffer.Clear();
            hasActiveSpinnerThisFrame = false;

            TargetDescriptor previous = default;
            bool hasPrevious = false;
            bool requiresSort = false;
            double currentTime = Time.Current;

            foreach (Drawable drawable in playfield!.HitObjectContainer.Objects)
            {
                if (drawable is not DrawableOsuHitObject osuDrawable)
                    continue;

                if (osuDrawable is DrawableSpinner spinner)
                {
                    if (!spinner.AllJudged && currentTime >= spinner.HitObject.StartTime)
                        hasActiveSpinnerThisFrame = true;

                    continue;
                }

                if (osuDrawable is DrawableSliderHead || osuDrawable is DrawableSliderTail || osuDrawable is DrawableSliderTick || osuDrawable is DrawableSliderRepeat)
                    continue;

                if (osuDrawable.AllJudged)
                    continue;

                TargetDescriptor target = createTargetDescriptor(osuDrawable);

                if (hasPrevious && compareTargets(previous, target) > 0)
                    requiresSort = true;

                targetBuffer.Add(target);
                previous = target;
                hasPrevious = true;
            }

            if (requiresSort && targetBuffer.Count > 1)
                targetBuffer.Sort(compareTargets);

            if (targetBuffer.Count > 0)
                clearResolvedFlowHistoryForFutureTargets(targetBuffer[0]);

            return targetBuffer;
        }

        private void clearResolvedFlowHistoryForFutureTargets(TargetDescriptor firstTarget)
        {
            if (Time.Current >= firstTarget.StartTime - scaleRealTimeWindow(44))
                return;

            resolvedFlowHistory.Clear();
            flowHistoryBuffer.Clear();
            hasLastResolvedTarget = false;
            lastResolvedTarget = default;
            clearCommittedStreamProjectionState();
            clearStreamPathTimingState();
        }

        private static int compareTargets(TargetDescriptor first, TargetDescriptor second)
        {
            int result = first.StartTime.CompareTo(second.StartTime);

            if (result != 0)
                return result;

            result = first.EndTime.CompareTo(second.EndTime);

            if (result != 0)
                return result;

            result = first.BaseLocalPosition.X.CompareTo(second.BaseLocalPosition.X);

            if (result != 0)
                return result;

            return first.BaseLocalPosition.Y.CompareTo(second.BaseLocalPosition.Y);
        }

        private TargetDescriptor createTargetDescriptor(DrawableOsuHitObject drawable)
        {
            OsuHitObject hitObject = drawable.HitObject;
            double endTime = hitObject is IHasDuration hasDuration ? hasDuration.EndTime : hitObject.StartTime;

            return new TargetDescriptor(
                drawable,
                hitObject,
                hitObject.StartTime,
                endTime,
                hitObject.StackedPosition,
                getDisplayCentre(drawable),
                getDrawableTargetRadius(drawable, hitObject));
        }

        private float getDrawableTargetRadius(DrawableOsuHitObject drawable, OsuHitObject hitObject)
        {
            switch (drawable)
            {
                case DrawableSlider slider when slider.IsLoaded:
                    return getHitCircleScreenRadius(slider.HeadCircle);

                case DrawableHitCircle hitCircle when hitCircle.IsLoaded:
                    return getHitCircleScreenRadius(hitCircle);

                default:
                    return getScreenSpaceRadius(hitObject.StackedPosition, (float)hitObject.Radius);
            }
        }

        private static float getHitCircleScreenRadius(DrawableHitCircle hitCircle)
            => getDrawableScreenRadius(hitCircle.HitArea);

        private static float getDrawableScreenRadius(Drawable drawable)
        {
            float width = (drawable.ScreenSpaceDrawQuad.TopRight - drawable.ScreenSpaceDrawQuad.TopLeft).Length;
            float height = (drawable.ScreenSpaceDrawQuad.BottomLeft - drawable.ScreenSpaceDrawQuad.TopLeft).Length;
            return Math.Min(width, height) * 0.5f;
        }

        private int chooseCurrentTargetIndex(List<TargetDescriptor> targets)
        {
            if (targets.Count == 0)
            {
                lockedTarget = null;
                return -1;
            }

            double currentTime = Time.Current;

            if (lockedTarget != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    TargetDescriptor target = targets[i];

                    if (!ReferenceEquals(target.Drawable, lockedTarget))
                        continue;

                    if (tryAdvanceRetainedRailTarget(targets, i, currentTime, out int advancedIndex))
                    {
                        lockedTarget = targets[advancedIndex].Drawable;
                        return advancedIndex;
                    }

                    if (shouldRetainRailTarget(targets, i, currentTime))
                        return i;

                    if (tryAdvanceRetainedTarget(targets, i, currentTime, out advancedIndex))
                    {
                        lockedTarget = targets[advancedIndex].Drawable;
                        return advancedIndex;
                    }

                    if (shouldRetainTarget(target, currentTime))
                        return i;

                    break;
                }
            }

            int pendingRelaxTargetIndex = findPendingRelaxTargetIndex(targets);

            if (pendingRelaxTargetIndex >= 0)
            {
                lockedTarget = targets[pendingRelaxTargetIndex].Drawable;
                return pendingRelaxTargetIndex;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                TargetDescriptor target = targets[i];

                if (target.Drawable is not DrawableSlider slider)
                    continue;

                if (currentTime < target.StartTime - scaleRealTimeWindow(60))
                    continue;

                if (!isSliderTargetActive(target, slider, currentTime))
                    continue;

                if (shouldSuppressRecentRetarget(target, currentTime))
                {
                    lockedTarget = null;
                    return -1;
                }

                lockedTarget = target.Drawable;
                return i;
            }

            if (shouldSuppressRecentRetarget(targets[0], currentTime))
            {
                lockedTarget = null;
                return -1;
            }

            lockedTarget = targets[0].Drawable;
            return 0;
        }

        private int findPendingRelaxTargetIndex(IReadOnlyList<TargetDescriptor> targets)
        {
            if (relaxController?.IsEnabled != true)
                return -1;

            for (int i = 0; i < targets.Count; i++)
            {
                if (isRelaxAwaitingPress(targets[i]))
                    return i;
            }

            return -1;
        }

        private TargetDescriptor? getFlowPreviousTarget(IReadOnlyList<TargetDescriptor> targets, int currentIndex)
        {
            if (currentIndex <= 0)
            {
                if (currentIndex < 0 || !hasLastResolvedTarget)
                    return null;

                TargetDescriptor current = targets[currentIndex];
                double elapsedSincePrevious = Time.Current - lastResolvedTarget.EndTime;

                if (elapsedSincePrevious < -1 || elapsedSincePrevious > scaleRealTimeWindow(260))
                    return null;

                if (lastResolvedTarget.StartTime >= current.StartTime)
                    return null;

                if (!isRailSegment(lastResolvedTarget, current))
                    return null;

                return lastResolvedTarget;
            }

            return targets[currentIndex - 1];
        }

        private IReadOnlyList<TargetDescriptor> getFlowHistoryTargets(TargetDescriptor current, TargetDescriptor? previous)
        {
            flowHistoryBuffer.Clear();

            if (resolvedFlowHistory.Count == 0)
                return flowHistoryBuffer;

            TargetDescriptor anchor = previous ?? current;
            double maxElapsed = scaleRealTimeWindow(460);

            for (int i = resolvedFlowHistory.Count - 1; i >= 0 && flowHistoryBuffer.Count < 4; i--)
            {
                TargetDescriptor candidate = resolvedFlowHistory[i];

                if (ReferenceEquals(candidate.HitObject, anchor.HitObject))
                    continue;

                if (candidate.StartTime >= anchor.StartTime)
                    continue;

                if (Time.Current - candidate.EndTime > maxElapsed)
                    break;

                if (!isRailSegment(candidate, anchor))
                {
                    if (flowHistoryBuffer.Count > 0)
                        break;

                    continue;
                }

                flowHistoryBuffer.Add(candidate);
                anchor = candidate;
            }

            flowHistoryBuffer.Reverse();
            return flowHistoryBuffer;
        }

        private bool shouldRetainTarget(TargetDescriptor target, double currentTime)
        {
            if (isRelaxAwaitingPress(target))
                return true;

            if (target.Drawable is DrawableSlider slider)
                return isSliderTargetActive(target, slider, currentTime);

            return currentTime <= target.StartTime + scaleRealTimeWindow(90);
        }

        private bool shouldRetainRailTarget(IReadOnlyList<TargetDescriptor> targets, int currentIndex, double currentTime)
        {
            if (currentIndex < 0 || currentIndex + 1 >= targets.Count)
                return false;

            TargetDescriptor current = targets[currentIndex];
            TargetDescriptor next = targets[currentIndex + 1];

            if (!current.Drawable.IsHit || !isRailHandoffCandidate(current, next))
                return false;

            double forcedAdvanceDelay = scaleRealTimeWindow(Math.Clamp(getGreatWindow(next.Drawable) * 1.9, 72, 150));

            if (currentTime >= next.StartTime + forcedAdvanceDelay)
                return false;

            return !hasReachedNaturalRailHandoff(current, next);
        }

        private bool tryAdvanceRetainedRailTarget(IReadOnlyList<TargetDescriptor> targets, int currentIndex, double currentTime, out int advancedIndex)
        {
            advancedIndex = -1;

            if (currentIndex < 0 || currentIndex + 1 >= targets.Count)
                return false;

            TargetDescriptor current = targets[currentIndex];
            TargetDescriptor next = targets[currentIndex + 1];

            if (!current.Drawable.IsHit || !isRailHandoffCandidate(current, next))
                return false;

            double handoffLead = scaleRealTimeWindow(Math.Clamp(getGreatWindow(next.Drawable) * 0.08, 0, 10));
            double forcedAdvanceDelay = scaleRealTimeWindow(Math.Clamp(getGreatWindow(next.Drawable) * 1.45, 44, 110));

            if (currentTime < next.StartTime - handoffLead)
                return false;

            if (!hasReachedNaturalRailHandoff(current, next) && currentTime < next.StartTime + forcedAdvanceDelay)
                return false;

            advancedIndex = currentIndex + 1;
            return true;
        }

        private bool isRailHandoffCandidate(TargetDescriptor current, TargetDescriptor next)
            => current.Drawable is DrawableHitCircle
               && next.Drawable is DrawableHitCircle
               && isRailSegment(current, next);

        private bool hasReachedNaturalRailHandoff(TargetDescriptor current, TargetDescriptor next)
        {
            Vector2 query = CurrentOutputPosition;
            ProjectionResult handoffProjection = projectionPointBuffer.Count >= 2
                ? projectOntoPolyline(projectionPointBuffer, query)
                : new ProjectionResult(query, next.ScreenSpacePosition - current.ScreenSpacePosition);
            Vector2 evaluationPoint = handoffProjection.Point;
            Vector2 segment = next.ScreenSpacePosition - current.ScreenSpacePosition;
            float segmentLengthSquared = segment.LengthSquared;

            if (segmentLengthSquared <= 0.0001f)
                return true;

            float progress = Math.Clamp(Vector2.Dot(evaluationPoint - current.ScreenSpacePosition, segment) / segmentLengthSquared, 0, 1);
            float currentDistance = (evaluationPoint - current.ScreenSpacePosition).Length;
            float nextDistance = (evaluationPoint - next.ScreenSpacePosition).Length;
            float radiusAllowance = Math.Max(current.Radius, next.Radius) * 0.08f;

            return progress >= 0.46f || nextDistance + radiusAllowance < currentDistance;
        }

        private bool tryAdvanceRetainedTarget(IReadOnlyList<TargetDescriptor> targets, int currentIndex, double currentTime, out int advancedIndex)
        {
            advancedIndex = -1;

            if (currentIndex < 0 || currentIndex + 1 >= targets.Count)
                return false;

            TargetDescriptor current = targets[currentIndex];
            TargetDescriptor next = targets[currentIndex + 1];

            if (!isPrimaryHitAchieved(current) || isRelaxAwaitingPress(current))
                return false;

            double nextLead = scaleRealTimeWindow(Math.Clamp(getGreatWindow(next.Drawable) * 0.45, 12, 32));
            bool isConcurrentOrSlider = (current.Drawable is DrawableSlider) || (next.StartTime <= current.EndTime + scaleRealTimeWindow(150));

            if (!isConcurrentOrSlider && !isRelaxAwaitingPress(next))
            {
                double handoffWindow = scaleRealTimeWindow(300);
                if (next.StartTime - current.StartTime > handoffWindow)
                    return false;
            }

            if (currentTime < next.StartTime - nextLead && !isRelaxAwaitingPress(next))
                return false;

            if (shouldSuppressRecentRetarget(next, currentTime))
                return false;

            advancedIndex = currentIndex + 1;
            return true;
        }

        private bool isPrimaryHitAchieved(TargetDescriptor target)
        {
            if (target.Drawable.IsHit || target.Drawable.AllJudged)
                return true;

            if (target.Drawable is DrawableSlider slider)
            {
                 return isSliderHeadHit(slider) || slider.HeadCircle?.AllJudged == true;
            }

            return false;
        }

        private bool shouldSuppressRecentRetarget(TargetDescriptor target, double currentTime)
        {
            if (!lastReleasedTargetPosition.HasValue)
                return false;

            float overlapRadius = Math.Max(target.Radius * 0.72f, 18f);

            if ((target.ScreenSpacePosition - lastReleasedTargetPosition.Value).Length > overlapRadius)
                return false;

            double timeSinceRelease = currentTime - lastReleasedTargetTime;

            if (timeSinceRelease < 0)
                return false;

            Vector2 releasedToRaw = lastRawPosition - lastReleasedTargetPosition.Value;
            Vector2 outwardDirection = normaliseOrZero(releasedToRaw);
            Vector2 movementDirection = normaliseOrZero(averagedVelocity);
            float movementAway = outwardDirection.LengthSquared > 0 && movementDirection.LengthSquared > 0
                ? Vector2.Dot(movementDirection, outwardDirection)
                : 0;
            bool meaningfulExit = releasedToRaw.Length > overlapRadius * 0.35f || movementAway > 0.18f;
            double suppressionWindow = scaleRealTimeWindow(meaningfulExit ? 120 : 68);

            return timeSinceRelease <= suppressionWindow;
        }

        private bool isSliderTargetActive(TargetDescriptor target, DrawableSlider slider, double currentTime)
        {
            if (isSliderBodyAssistActive(target, slider, currentTime))
                return currentTime <= target.EndTime + scaleRealTimeWindow(90);

            if (slider.HeadCircle.Judged)
                return false;

            return currentTime <= target.StartTime + getSliderHeadAssistWindow(slider);
        }

        private double getSliderHeadAssistWindow(DrawableSlider slider)
            => scaleRealTimeWindow(Math.Clamp(getGreatWindow(slider) * 1.25, 36, 95));

        private void updateDisplayState(AimAssistContext? context, TargetDescriptor? nextTarget, IReadOnlyList<TargetDescriptor> targets,
                                        IReadOnlyList<OsuPatternState> patternStates, int currentTargetIndex, TargetDescriptor? previousTarget)
        {
            if (context == null)
            {
                clearDisplayState();
                return;
            }

            CurrentTargetPosition = getDisplayCentre(context.Value.Drawable);
            CurrentBaseTargetRadius = getDrawableTargetRadius(context.Value.Drawable, context.Value.Drawable.HitObject);
            CurrentTargetRadius = context.Value.Radius;
            CurrentAdaptiveRadiusScale = CurrentBaseTargetRadius > 0
                ? CurrentTargetRadius / CurrentBaseTargetRadius
                : 1;
            bool suppressAssistPoint = !context.Value.AllowPassiveAssist;
            CurrentAssistPointPosition = suppressAssistPoint || CurrentTargetPosition.HasValue && (context.Value.DesiredPoint - CurrentTargetPosition.Value).Length <= 2f
                ? null
                : context.Value.DesiredPoint;
            CurrentNextTargetPosition = nextTarget?.Drawable != null ? nextTarget.Value.ScreenSpacePosition : null;
            CurrentNextTargetRadius = nextTarget?.Drawable != null ? nextTarget.Value.Radius : 0;
            CurrentFocusTime = context.Value.FocusTime;

            if (shouldShowFlowDebug())
                updateFlowDebugState(context.Value, targets, patternStates, currentTargetIndex, previousTarget);
            else
                clearFlowDebugState(context.Value.ModeName);
        }

        private void clearDisplayState()
        {
            CurrentTargetPosition = null;
            CurrentBaseTargetRadius = 0;
            CurrentTargetRadius = 0;
            CurrentAdaptiveRadiusScale = 1;
            CurrentAssistPointPosition = null;
            CurrentNextTargetPosition = null;
            CurrentNextTargetRadius = 0;
            CurrentFocusTime = null;
            clearFlowDebugState();
        }

        private void clearFlowDebugState(string modeName = @"idle")
        {
            CurrentFlowDebugModeName = modeName;
            CurrentPatternDebugLabel = string.Empty;
            CurrentFlowDebugAnchorPosition = null;
            debugFlowPathPoints = Array.Empty<Vector2>();
            debugFlowSourcePoints = Array.Empty<Vector2>();
        }

        private void updateFlowDebugState(AimAssistContext context, IReadOnlyList<TargetDescriptor> targets, IReadOnlyList<OsuPatternState> patternStates,
                                          int currentTargetIndex, TargetDescriptor? previousTarget)
        {
            bool exposeFlowDebug = shouldExposeFlowDebug(context);

            CurrentFlowDebugModeName = exposeFlowDebug
                ? getFlowDebugModeName(context)
                : context.ModeName;
            CurrentPatternDebugLabel = exposeFlowDebug
                ? describePattern(context.PatternState)
                : string.Empty;
            CurrentFlowDebugAnchorPosition = null;
            debugFlowPathPoints = Array.Empty<Vector2>();
            debugFlowSourcePoints = Array.Empty<Vector2>();

            bool showFlowPath = exposeFlowDebug;
            bool hasFlowPathContext = showFlowPath && projectionPointBuffer.Count >= 2;
            bool hasFlowContext = exposeFlowDebug;

            if (!hasFlowContext || !hasFlowPathContext)
            {
                tryPopulateUpcomingFlowDebug(targets, patternStates, currentTargetIndex, previousTarget);
                return;
            }

            CurrentFlowDebugAnchorPosition = context.ModeName is @"rail" or @"stream"
                ? context.DisplayPoint
                : getDisplayCentre(context.Drawable);

            debugFlowPathPoints = projectionPointBuffer.ToArray();

            if (railSourcePointBuffer.Count > 0)
                debugFlowSourcePoints = railSourcePointBuffer.ToArray();
        }

        private void tryPopulateUpcomingFlowDebug(IReadOnlyList<TargetDescriptor> targets, IReadOnlyList<OsuPatternState> patternStates, int currentTargetIndex, TargetDescriptor? previousTarget)
        {
            if (targets.Count == 0 || patternStates.Count == 0)
                return;

            int searchStart = Math.Max(0, currentTargetIndex);
            int previewIndex = -1;

            for (int i = searchStart; i < Math.Min(targets.Count, searchStart + 3); i++)
            {
                if (!patternStates[i].SupportsFlowPath)
                    continue;

                if (patternStates[i].SegmentEndIndex <= i)
                    continue;

                previewIndex = i;
                break;
            }

            if (previewIndex < 0)
                return;

            TargetDescriptor previewCurrent = targets[previewIndex];
            TargetDescriptor? previewNext = previewIndex + 1 < targets.Count ? targets[previewIndex + 1] : null;
            OsuPatternState previewState = patternStates[previewIndex];
            List<TargetDescriptor> previewRailTargets = buildRailTargets(previewCurrent, previewNext, targets, previewIndex, previewState);

            if (previewRailTargets.Count < 2)
                return;

            TargetDescriptor? previewPrevious = previewIndex > 0 ? targets[previewIndex - 1] : previousTarget;
            fillRailProjectionPointBuffer(previewPrevious, previewRailTargets, previewState.PatternInfo);

            if (projectionPointBuffer.Count < 2)
                return;

            CurrentFlowDebugModeName = previewState.Candidate switch
            {
                OsuPatternSegmentKind.Stream => @"stream",
                OsuPatternSegmentKind.BurstFlow when previewState.PatternInfo.IsGentleFlow => @"gentle-flow",
                OsuPatternSegmentKind.BurstFlow => @"burst-flow",
                OsuPatternSegmentKind.Jump => @"jump",
                _ => @"point"
            };
            CurrentPatternDebugLabel = describePattern(previewState);
            CurrentFlowDebugAnchorPosition = previewRailTargets[0].ScreenSpacePosition;
            debugFlowPathPoints = projectionPointBuffer.ToArray();

            if (railSourcePointBuffer.Count > 0)
                debugFlowSourcePoints = railSourcePointBuffer.ToArray();
        }

        private static bool shouldExposeFlowDebug(AimAssistContext context)
        {
            if (context.ModeName == @"stream")
                return true;

            if (!context.PatternState.SupportsFlowPath)
                return false;

            return context.PreviewPoint.HasValue;
        }

        private static string getFlowDebugModeName(AimAssistContext context)
        {
            if (context.PatternInfo.IsGentleFlow && context.PatternState.Candidate == OsuPatternSegmentKind.BurstFlow)
                return @"gentle-flow";

            if (context.PatternState.Candidate == OsuPatternSegmentKind.Stream)
                return @"stream";

            if (context.PatternState.Candidate == OsuPatternSegmentKind.BurstFlow)
                return @"burst-flow";

            if (context.PatternState.Candidate == OsuPatternSegmentKind.Jump)
                return @"jump";

            return context.ModeName;
        }

        private static string describePattern(OsuPatternState patternState)
        {
            OsuPatternInfo patternInfo = patternState.PatternInfo;
            string kind = patternInfo.Kind switch
            {
                OsuPatternKind.Single => @"single",
                OsuPatternKind.Stack => @"stack",
                OsuPatternKind.Burst => @"burst",
                OsuPatternKind.Stream => @"stream",
                _ => @"unknown"
            };

            string shape = patternInfo.StreamShape switch
            {
                OsuStreamShapeKind.None => @"-",
                OsuStreamShapeKind.Straight => @"straight",
                OsuStreamShapeKind.Arc => @"arc",
                OsuStreamShapeKind.ZigZag => @"zigzag",
                _ => @"-"
            };

            string spacing = patternInfo.StreamSpacing switch
            {
                OsuStreamSpacingKind.None => @"-",
                OsuStreamSpacingKind.Tight => @"tight",
                OsuStreamSpacingKind.Even => @"even",
                OsuStreamSpacingKind.Variable => @"variable",
                OsuStreamSpacingKind.Spaced => @"spaced",
                _ => @"-"
            };

            string flags = patternState.Candidate switch
            {
                OsuPatternSegmentKind.Stream => @"stream",
                OsuPatternSegmentKind.BurstFlow => @"flow",
                OsuPatternSegmentKind.Jump => @"jump",
                OsuPatternSegmentKind.Stack => @"stack",
                _ => @"point"
            };

            if (patternInfo.SupportsRailHandoff)
                flags += @" rail";

            if (patternInfo.IsLowDensityFlow)
                flags += @" low-density";

            if (patternInfo.IsGentleFlow)
                flags += @" gentle";

            return
                $"pattern: {kind}\n" +
                $"shape/spacing: {shape} / {spacing}\n" +
                $"chain: {patternInfo.ChainLength} ({patternInfo.ChainStartIndex}->{patternInfo.ChainEndIndex})\n" +
                $"density: {patternInfo.DensityWeight:0.00}  continuity: {patternInfo.ContinuityWeight:0.00}\n" +
                $"flags: {flags}";
        }

        private static bool isSliderHeadHit(DrawableSlider slider)
            => slider.HeadCircle.IsHit
               || slider.HeadCircle.Result.IsHit
               || slider.HeadCircle.HitAction.HasValue
               || slider.Tracking.Value;

        private bool isSliderBodyAssistActive(TargetDescriptor target, DrawableSlider slider, double currentTime)
        {
            if (isSliderHeadHit(slider))
                return true;

            return currentTime >= target.StartTime;
        }

        private Vector2 getDisplayCentre(DrawableOsuHitObject drawable)
        {
            switch (drawable)
            {
                case DrawableSlider slider when slider.HeadCircle.IsLoaded:
                    return slider.HeadCircle.ScreenSpaceDrawQuad.Centre;

                case DrawableHitCircle hitCircle when hitCircle.IsLoaded:
                    return hitCircle.ScreenSpaceDrawQuad.Centre;

                default:
                    return toScreenSpace(drawable.HitObject.StackedPosition);
            }
        }

        private bool hasActiveSpinner() => hasActiveSpinnerThisFrame;

        private void ensureHitResultHook()
        {
            if (hitResultsHooked || playfield == null)
                return;

            playfield.HitObjectContainer.NewResult += onHitObjectResult;
            hitResultsHooked = true;
        }

        private void onHitObjectResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (!result.HasResult || lockedTarget == null || lockedTarget is DrawableSlider)
                return;

            if (!ReferenceEquals(judgedObject.HitObject, lockedTarget.HitObject))
                return;

            if (judgedObject is DrawableOsuHitObject osuDrawable)
            {
                lastResolvedTarget = createTargetDescriptor(osuDrawable);
                hasLastResolvedTarget = true;

                if (osuDrawable is DrawableHitCircle)
                {
                    resolvedFlowHistory.Add(lastResolvedTarget);

                    if (resolvedFlowHistory.Count > 4)
                        resolvedFlowHistory.RemoveAt(0);
                }
            }

            lastReleasedTargetPosition = getDisplayCentre(lockedTarget);
            lastReleasedTargetTime = Time.Current;
            lockedTarget = null;
            lastIntentPassTime = double.NegativeInfinity;
        }
    }
}
