// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Screens.Play;

namespace osu.Game.Scoring.Render
{
    internal partial class RenderGameplayClockContainer : MasterGameplayClockContainer
    {
        public RenderGameplayClockContainer(WorkingBeatmap working, double gameplayStartTime)
            : base(working, gameplayStartTime)
        {
            // Seek to the start position; the ManualAdjustableClock source is NOT started
            // (IsRunning = false) so DecouplingFramedClock and InterpolatingFramedClock
            // will not advance in real time between our explicit AdvanceTo calls.
            Seek(StartTime);
        }

        /// <summary>
        /// Advance the gameplay clock to exactly <paramref name="time"/> (in beatmap time, ms).
        /// Uses GameplayClock.Seek so that InterpolatingFramedClock is properly reset and
        /// does not drift forward in real time between our advances.
        /// </summary>
        public override void Seek(double time)
        {
            GameplayClock.Seek(time);
        }

        public double TotalAppliedOffset => GameplayClock.TotalAppliedOffset;

        /// <summary>
        /// Offset to use when muxing the beatmap audio file into the rendered video.
        /// Uses <see cref="FramedBeatmapClock.UserAppliedOffset"/> which excludes platform
        /// latency compensation — platform offsets only correct live BASS output latency
        /// and must not be applied when muxing audio directly from beatmap files.
        /// </summary>
        public double MusicFileOffset => GameplayClock.UserAppliedOffset;

        protected override void StartGameplayClock()
        {
            // Do NOT call the base which would start the underlying audio track.
            // We keep the clock in a "started-but-not-running-in-real-time" state:
            // GameplayClock.Start() is called by the base GameplayClockContainer.Start()
            // to flip isPaused, but we rely on Seek() to drive time — not real-time advance.
            Logger.Log($"{nameof(RenderGameplayClockContainer)} keeping clock static (render mode).");
        }

        protected override void StopGameplayClock()
        {
            Logger.Log($"{nameof(RenderGameplayClockContainer)} stop (render mode).");
        }
    }
}
