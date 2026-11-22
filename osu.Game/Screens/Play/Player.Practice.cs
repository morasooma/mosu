#nullable disable

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Screens;
using osu.Game.Configuration;
using osuTK.Input;

namespace osu.Game.Screens.Play
{
    public abstract partial class Player
    {
        [Resolved]
        private OsuConfigManager practiceConfig { get; set; }

        public bool IsPracticeSession { get; internal set; }
        public double? PracticeStartTime { get; internal set; }

        private double practiceFailureTime;
        private bool practiceShiftHeld;
        private double? practiceShiftPendingSince;
        private const double practice_shift_activation_delay = 180;
        private PracticeTimeline practiceTimeline;

        private bool PracticeAvailable => this is SoloPlayer
                                          && Configuration.AllowUserInteraction && Configuration.AllowPause
                                          && !IsRankedPlaySession && !UsesSpecialGameplayRateMode
                                          && GameplayState.Mods.All(m => m.UserPlayable)
                                          && !DrawableRuleset.HasReplayLoaded.Value
                                          && practiceConfig.Get<bool>(OsuSetting.ForkPracticeMode);

        private void UpdatePracticeInput()
        {
            if (!PracticeAvailable || !this.IsCurrentScreen())
            {
                practiceShiftPendingSince = null;
                ClosePracticeTimeline();
                return;
            }

            var input = GetContainingInputManager();
            var keys = input.CurrentState.Keyboard.Keys;
            bool shift = keys.IsPressed(Key.ShiftLeft) || keys.IsPressed(Key.ShiftRight);
            bool shiftOnly = shift && keys.All(key => key == Key.ShiftLeft || key == Key.ShiftRight);
            if (shiftOnly && !practiceShiftHeld)
                practiceShiftPendingSince = Time.Current;

            // Once open, gameplay keys select a checkpoint rather than cancelling Shift.
            // Other chords (Tab, Ctrl, Super...) retain their normal shortcut behaviour.
            bool selecting = practiceTimeline?.State.Value == Visibility.Visible && shift
                             && keys.All(key => key == Key.ShiftLeft || key == Key.ShiftRight || IsPracticeGameplayKey(key));
            if (!shiftOnly && !selecting)
            {
                practiceShiftPendingSince = null;
                ClosePracticeTimeline();
            }
            else if (practiceShiftPendingSince.HasValue
                     && Time.Current - practiceShiftPendingSince.Value >= practice_shift_activation_delay)
            {
                practiceShiftPendingSince = null;
                if (!GameplayState.HasFailed && !GameplayState.HasPassed
                    && !GameplayClockContainer.IsPaused.Value && !IsResuming)
                {
                    GameplayClockContainer.Stop();
                    practiceTimeline ??= CreatePracticeTimeline();
                    practiceTimeline.Show();
                }
            }

            if (practiceTimeline?.State.Value == Visibility.Visible)
                practiceTimeline.Y = Math.Clamp(ToLocalSpace(input.CurrentState.Mouse.Position).Y,
                    practiceTimeline.Height / 2, Math.Max(practiceTimeline.Height / 2, DrawHeight - practiceTimeline.Height / 2));

            // Latch the physical Shift until release: releasing another shortcut key must
            // not turn Super+Shift (or any other chord) into a fresh practice gesture.
            practiceShiftHeld = shift;
        }

        private PracticeTimeline CreatePracticeTimeline()
        {
            var timeline = new PracticeTimeline(GameplayState.Beatmap, time =>
            {
                IsPracticeSession = true;
                PracticeStartTime = time;
                practiceTimeline.Hide();
                // A checkpoint starts a new practice attempt. Reuse the loader restart
                // path so processors, HUD counters and object lifetimes are initialised
                // at the checkpoint, rather than seeking a paused live player in-place.
                // Failed-score continuation deliberately keeps the existing player.
                if (PrepareLoaderForRestart != null && Restart(true))
                    return;

                SetGameplayStartTime(time);
                ScheduleAfterChildren(() =>
                {
                    if (this.IsCurrentScreen() && !GameplayState.HasFailed)
                    {
                        DrawableRuleset.AllowOneFrameClockSeek();
                        GameplayClockContainer.Start();
                    }
                });
            }, e => e.ShiftPressed && IsPracticeGameplayKey(e.Key)
                    && e.CurrentState.Keyboard.Keys.All(key => key == Key.ShiftLeft || key == Key.ShiftRight || IsPracticeGameplayKey(key)))
            {
                Depth = float.MinValue,
            };
            AddInternal(timeline);
            return timeline;
        }

        private bool IsPracticeGameplayKey(Key key)
        {
            if (KeyCombination.IsModifierKey(KeyCombination.FromKey(key)))
                return false;

            return DrawableRuleset.GameplayKeyBindings.Any(binding =>
                binding.KeyCombination.Keys.Length == 1
                && binding.KeyCombination.Keys[0] == KeyCombination.FromKey(key));
        }

        private void ClosePracticeTimeline()
        {
            if (practiceTimeline?.State.Value != Visibility.Visible)
                return;

            practiceTimeline.Hide();
            if (this.IsCurrentScreen() && PauseOverlay.State.Value != Visibility.Visible && !GameplayState.HasFailed)
                Resume();
        }

        private void ContinuePracticeAfterFailure()
        {
            if (!GameplayState.HasFailed || !PracticeAvailable)
                return;

            IsPracticeSession = true;
            PracticeStartTime = practiceFailureTime;
            GameplayState.HasFailed = false;
            HealthProcessor.ApplyNewJudgementsWhenFailed = true;
            FailOverlay.Hide();
            // Practice-enabled gameplay does not run the destructive fail animation, so all
            // objects, score and health can be retained on the same player instance.
            // The clock was stopped at failure; avoid even a same-time seek, which could
            // revert the last judgement and change the score/health being preserved.
            ValidForResume = true;
            Resume();
        }
    }
}
