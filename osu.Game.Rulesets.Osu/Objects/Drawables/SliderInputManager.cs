// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Play;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    public partial class SliderInputManager : Component, IRequireHighFrequencyMousePosition
    {
        /// <summary>
        /// Whether the slider is currently being tracked.
        /// </summary>
        public bool Tracking { get; private set; }

        [Resolved]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved(CanBeNull = true)]
        private DrawableOsuRuleset? drawableRuleset { get; set; }

        private bool stableRelax => osu.Game.Online.MosuServerEnvironment.UsesStableProtocol
            && drawableRuleset?.Mods.Any(m => m is OsuModRelax) == true;

        /// <summary>
        /// The point in time after which we can accept any key for tracking. Before this time, we may need to restrict tracking to the key used to hit the head circle.
        ///
        /// This is a requirement to stop the case where a player holds down one key (from before the slider) and taps the second key while maintaining full scoring (tracking) of sliders.
        /// Visually, this special case can be seen below (time increasing from left to right):
        ///
        ///  Z  Z+X  Z
        ///      o========o
        ///
        /// Without this logic, tracking would continue through the entire slider even though no key hold action is directly attributing to it.
        ///
        /// In all other cases, no special handling is required (either key being pressed is allowable as valid tracking).
        ///
        /// The reason for storing this as a time value (rather than a bool) is to correctly handle rewind scenarios.
        /// </summary>
        private double? timeToAcceptAnyKeyAfter;

        /// <summary>
        /// The actions that were pressed in the previous frame.
        /// </summary>
        private readonly List<OsuAction> lastPressedActions = new List<OsuAction>();

        private Vector2? screenSpaceMousePosition;
        private readonly DrawableSlider slider;
        private StableSliderPosition? stableSliderPosition;
        private StableSliderTrackingState stableTracking = new StableSliderTrackingState();

        public SliderInputManager(DrawableSlider slider)
        {
            this.slider = slider;
            this.slider.HitObjectApplied += resetState;
        }

        /// <summary>
        /// This component handles all input of the slider, so it should receive input no matter the position.
        /// </summary>
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            screenSpaceMousePosition = e.ScreenSpaceMousePosition;
            return base.OnMouseMove(e);
        }

        private bool isAutomaticReplayFrame => slider.OsuActionInputManager?.ReplayInputHandler is OsuFramedReplayInputHandler handler
            && !handler.IsRecordedGameplayFrame;

        protected override void Update()
        {
            base.Update();
            if (isAutomaticReplayFrame)
                return;

            updateTracking(IsMouseInFollowArea(Tracking));
        }

        internal void HandleStableHeadPress(OsuAction action)
        {
            // Stable dispatches one gameplay press per frame and gives the left key
            // priority when both keys become pressed together. Lazer dispatches the
            // actions individually, so the second action must not replace that choice.
            if (slider.OsuActionInputManager is { } inputManager)
            {
                int newButtons;
                if (inputManager.ReplayInputHandler is OsuFramedReplayInputHandler handler)
                    newButtons = handler.StableNewButtons;
                else
                {
                    int buttons = (inputManager.PressedActions.Contains(OsuAction.LeftButton) ? 1 : 0)
                                  | (inputManager.PressedActions.Contains(OsuAction.RightButton) ? 2 : 0);
                    newButtons = buttons & ~inputManager.StableButtonState.Buttons;
                }
                action = (newButtons & 1) != 0 ? OsuAction.LeftButton
                    : (newButtons & 2) != 0 ? OsuAction.RightButton : action;
            }
            stableTracking.HandleHeadPress(action, Time.Current);
        }

        public void PostProcessHeadJudgement(DrawableSliderHead head)
        {
            if (osu.Game.Online.MosuServerEnvironment.UsesStableProtocol)
                return;

            if (!head.Judged || !head.Result.IsHit)
                return;

            if (!IsMouseInFollowArea(true))
                return;

            Debug.Assert(screenSpaceMousePosition != null);

            Vector2 mousePositionInSlider = slider.ToLocalSpace(screenSpaceMousePosition.Value) - slider.OriginPosition;

            // When the head is hit late:
            // - If the cursor has at all times been within range of the expanded follow area, hit all nested objects that have been passed through.
            // - If the cursor has at some point left the expanded follow area, miss those nested objects instead.

            bool allTicksInRange = true;

            foreach (var nested in slider.NestedHitObjects.OfType<DrawableOsuHitObject>())
            {
                // Skip nested objects that are already judged.
                if (nested.Judged)
                    continue;

                // Stop the process when a nested object is reached that can't be hit before the current time.
                if (nested.HitObject.StartTime > Time.Current)
                    break;

                float radius = getFollowRadius(true);
                double objectProgress = Math.Clamp((nested.HitObject.StartTime - slider.HitObject.StartTime) / slider.HitObject.Duration, 0, 1);
                Vector2 objectPosition = slider.HitObject.CurvePositionAt(objectProgress);

                // When the first nested object that is further outside the follow area is reached,
                // forcefully miss all other nested objects that would otherwise be valid to be hit.
                // This covers a case of a slider overlapping itself that requires tracking to a tick on an outer edge.
                if ((objectPosition - mousePositionInSlider).LengthSquared > radius * radius)
                {
                    allTicksInRange = false;
                    break;
                }
            }

            foreach (var nested in slider.NestedHitObjects.OfType<DrawableOsuHitObject>())
            {
                // Skip nested objects that are already judged.
                if (nested.Judged)
                    continue;

                // Stop the process when a nested object is reached that can't be hit before the current time.
                if (nested.HitObject.StartTime > Time.Current)
                    break;

                if (allTicksInRange)
                    nested.HitForcefully();
                else
                    nested.MissForcefully();
            }

            // If all ticks were hit so far, enable tracking the full extent.
            // If any ticks were missed, assume tracking would've broken at some point, and should only activate if the cursor is within the slider ball.
            // For the second case, this may be the last chance we have to enable tracking before other objects get judged, otherwise the same would normally happen via Update().
            updateTracking(allTicksInRange || IsMouseInFollowArea(false));
        }

        public void TryJudgeNestedObject(DrawableOsuHitObject nestedObject, double timeOffset)
        {
            if (isAutomaticReplayFrame)
                return;

            double judgementClock = osu.Game.Online.MosuServerEnvironment.UsesStableProtocol ? (int)Time.Current : Time.Current;
            switch (nestedObject)
            {
                case DrawableSliderRepeat:
                case DrawableSliderTick:
                    if (osu.Game.Online.MosuServerEnvironment.UsesStableProtocol ? judgementClock < (int)nestedObject.HitObject.StartTime : timeOffset < 0)
                        return;

                    break;

                case DrawableSliderTail:
                    double tailJudgeTime = osu.Game.Online.MosuServerEnvironment.UsesStableProtocol
                        ? Math.Max(slider.HitObject.StartTime + (int)slider.HitObject.Duration / 2, (int)slider.HitObject.EndTime - 36)
                        : nestedObject.HitObject.StartTime + SliderEventGenerator.TAIL_LENIENCY;
                    if (judgementClock < tailJudgeTime)
                        return;

                    // Ensure the tail can only activate after all previous ticks/repeats already have.
                    //
                    // This covers the edge case where the lenience may allow the tail to activate before
                    // the last tick, changing ordering of score/combo awarding.
                    var lastTick = slider.NestedHitObjects.LastOrDefault(o => o.HitObject is SliderTick || o.HitObject is SliderRepeat);
                    if (lastTick?.Judged == false)
                        return;

                    break;

                default:
                    return;
            }

            if (!slider.HeadCircle.Judged && !osu.Game.Online.MosuServerEnvironment.UsesStableProtocol)
                return;

            double checkpoint = nestedObject is DrawableSliderTail
                ? Math.Max(slider.HitObject.StartTime + (int)slider.HitObject.Duration / 2, (int)slider.HitObject.EndTime - 36)
                : (int)nestedObject.HitObject.StartTime;
            if (osu.Game.Online.MosuServerEnvironment.UsesStableProtocol ? stableTracking.CanHitCheckpoint(checkpoint) : Tracking)
                nestedObject.HitForcefully();
            else if (osu.Game.Online.MosuServerEnvironment.UsesStableProtocol || timeOffset >= 0)
                nestedObject.MissForcefully();
        }

        /// <summary>
        /// Whether the mouse is currently in the follow area.
        /// </summary>
        /// <param name="expanded">Whether to test against the maximum area of the follow circle.</param>
        public bool IsMouseInFollowArea(bool expanded)
        {
            if (screenSpaceMousePosition is not Vector2 pos)
                return false;

            float radius = getFollowRadius(expanded);

            double followProgress = Math.Clamp((Time.Current - slider.HitObject.StartTime) / slider.HitObject.Duration, 0, 1);
            Vector2 followCirclePosition = slider.HitObject.CurvePositionAt(followProgress);
            if (osu.Game.Online.MosuServerEnvironment.UsesStableProtocol)
            {
                stableSliderPosition ??= new StableSliderPosition(slider.HitObject.Path.CalculatedPath,
                    slider.HitObject.StartTime, slider.HitObject.Velocity, slider.HitObject.RepeatCount + 1);
                followCirclePosition = stableSliderPosition.PositionAt((int)Time.Current);
            }
            Vector2 mousePositionInSlider = slider.ToLocalSpace(pos) - slider.OriginPosition;

            float distanceSquared = (mousePositionInSlider - followCirclePosition).LengthSquared;
            return osu.Game.Online.MosuServerEnvironment.UsesStableProtocol
                ? distanceSquared < radius * radius
                : distanceSquared <= radius * radius;
        }

        /// <summary>
        /// Retrieves the radius of the follow area.
        /// </summary>
        /// <param name="expanded">Whether to return the maximum area of the follow circle.</param>
        private float getFollowRadius(bool expanded)
        {
            float radius = (float)slider.HitObject.Radius;

            if (expanded)
                radius *= DrawableSliderBall.FOLLOW_AREA;

            return radius;
        }

        /// <summary>
        /// Updates the tracking state.
        /// </summary>
        /// <param name="isValidTrackingPosition">Whether the current mouse position is valid to begin tracking.</param>
        private void updateTracking(bool isValidTrackingPosition)
        {
            if (gameplayClock?.IsRewinding == true)
            {
                if (osu.Game.Online.MosuServerEnvironment.UsesStableProtocol)
                    stableTracking.Rewind(Time.Current);
                var trackingHistory = slider.Result.TrackingHistory;
                while (trackingHistory.TryPeek(out var historyEntry) && Time.Current < historyEntry.time)
                    trackingHistory.Pop();

                Debug.Assert(trackingHistory.Count > 0);

                Tracking = trackingHistory.Peek().tracking;
                return;
            }

            bool wasTracking = Tracking;
            if (osu.Game.Online.MosuServerEnvironment.UsesStableProtocol && slider.OsuActionInputManager is { } inputManager)
            {
                var buttons = inputManager.ReplayInputHandler is OsuFramedReplayInputHandler handler
                    ? handler.StableButtonState
                    : inputManager.StableButtonState;
                // Live play and replay playback must use the same acquisition and key-transfer
                // rules. In particular, hitting the head does not grant expanded follow range.
                stableTracking.Update(Time.Current, slider.HitObject.StartTime, slider.Judged,
                    isValidTrackingPosition, buttons, stableRelax);
                Tracking = stableTracking.Tracking;
                if (wasTracking != Tracking)
                    slider.Result.TrackingHistory.Push((Time.Current, Tracking));
                return;
            }

            // from the point at which the head circle is hit, this will be non-null.
            // it may be null if the head circle was missed.
            OsuAction? headCircleHitAction = getInitialHitAction();

            if (headCircleHitAction == null)
                timeToAcceptAnyKeyAfter = null;

            // if the head circle was hit with a specific key, tracking should only occur while that key is pressed.
            if (headCircleHitAction != null && timeToAcceptAnyKeyAfter == null)
            {
                var otherKey = headCircleHitAction == OsuAction.RightButton ? OsuAction.LeftButton : OsuAction.RightButton;

                // we can start accepting any key once all other keys have been released in the previous frame.
                if (!lastPressedActions.Contains(otherKey))
                    timeToAcceptAnyKeyAfter = Time.Current;
            }

            if (slider.OsuActionInputManager == null)
                return;

            lastPressedActions.Clear();
            bool validTrackingAction = stableRelax;

            foreach (OsuAction action in slider.OsuActionInputManager.PressedActions)
            {
                if (isValidTrackingAction(action))
                    validTrackingAction = true;

                lastPressedActions.Add(action);
            }

            Tracking =
                (!osu.Game.Online.MosuServerEnvironment.UsesStableProtocol || Time.Current >= slider.HitObject.StartTime)
                // even in an edge case where current time has exceeded the slider's time, we may not have finished judging.
                // we don't want to potentially update from Tracking=true to Tracking=false at this point.
                && (!slider.AllJudged || Time.Current <= slider.HitObject.GetEndTime())
                // in valid position range
                && isValidTrackingPosition
                // valid action
                && validTrackingAction;

            if (wasTracking != Tracking)
                slider.Result.TrackingHistory.Push((Time.Current, Tracking));
        }

        private OsuAction? getInitialHitAction() => slider.HeadCircle?.HitAction;

        /// <summary>
        /// Check whether a given user input is a valid tracking action.
        /// </summary>
        private bool isValidTrackingAction(OsuAction action)
        {
            OsuAction? hitAction = getInitialHitAction();

            // if the head circle was hit, we may not yet be allowed to accept any key, so we must use the initial hit action.
            if (hitAction.HasValue && (!timeToAcceptAnyKeyAfter.HasValue || Time.Current <= timeToAcceptAnyKeyAfter.Value))
                return action == hitAction;

            return action == OsuAction.LeftButton || action == OsuAction.RightButton;
        }

        private void resetState(DrawableHitObject obj)
        {
            Tracking = false;
            stableSliderPosition = null;
            stableTracking = new StableSliderTrackingState();
            timeToAcceptAnyKeyAfter = null;
            lastPressedActions.Clear();
            screenSpaceMousePosition = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            slider.HitObjectApplied -= resetState;
        }
    }
}
