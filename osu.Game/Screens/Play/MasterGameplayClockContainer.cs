// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Overlays;
using osu.Game.Storyboards;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// A <see cref="GameplayClockContainer"/> which uses a <see cref="WorkingBeatmap"/> as a source.
    /// <para>
    /// This is the most complete <see cref="GameplayClockContainer"/> which takes into account all user and platform offsets,
    /// and provides implementations for user actions such as skipping or adjusting playback rates that may occur during gameplay.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This is intended to be used as a single controller for gameplay, or as a reference source for other <see cref="GameplayClockContainer"/>s.
    /// </remarks>
    public partial class MasterGameplayClockContainer : GameplayClockContainer, IBeatSyncProvider
    {
        /// <summary>
        /// Duration before gameplay start time required before skip button displays.
        /// </summary>
        public const double MINIMUM_SKIP_TIME = 1000;

        /// <summary>
        /// Minimum break duration for which skipping is useful and can retain the resume lead-in.
        /// </summary>
        public const double MINIMUM_BREAK_DURATION_FOR_SKIP = 2 * MINIMUM_SKIP_TIME;

        public readonly BindableNumber<double> UserPlaybackRate = new BindableDouble(1)
        {
            MinValue = 0.05,
            MaxValue = 2,
            Precision = 0.01,
        };

        private readonly IBeatmap beatmap;

        private Track track;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        /// <summary>
        /// Create a new master gameplay clock container.
        /// </summary>
        /// <param name="working">The beatmap to be used for time and metadata references.</param>
        /// <param name="gameplayStartTime">The latest time which should be used when introducing gameplay. Will be used when skipping forward.</param>
        public MasterGameplayClockContainer(WorkingBeatmap working, double gameplayStartTime)
            : base(working.Track, applyOffsets: true, requireDecoupling: true)
        {
            beatmap = working.Beatmap;
            track = working.Track;

            GameplayStartTime = gameplayStartTime;
            StartTime = findEarliestStartTime(gameplayStartTime, beatmap, working.Storyboard);
        }

        private static double findEarliestStartTime(double gameplayStartTime, IBeatmap beatmap, Storyboard storyboard)
        {
            // here we are trying to find the time to start playback from the "zero" point.
            // generally this is either zero, or some point earlier than zero in the case of storyboards, lead-ins etc.

            // start with the originally provided latest time (if before zero).
            double time = Math.Min(0, gameplayStartTime);

            // if a storyboard is present, it may dictate the appropriate start time by having events in negative time space.
            // this is commonly used to display an intro before the audio track start.
            double? firstStoryboardEvent = storyboard.EarliestEventTime;
            if (firstStoryboardEvent != null)
                time = Math.Min(time, firstStoryboardEvent.Value);

            // some beatmaps specify a current lead-in time which should be used instead of the ruleset-provided value when available.
            // this is not available as an option in the live editor but can still be applied via .osu editing.
            double firstHitObjectTime = beatmap.HitObjects.First().StartTime;
            if (beatmap.AudioLeadIn > 0)
                time = Math.Min(time, firstHitObjectTime - beatmap.AudioLeadIn);

            return time;
        }

        protected override void StartGameplayClock()
        {
            addAdjustmentsToTrack();
            base.StartGameplayClock();
        }

        /// <summary>
        /// Skip forward to the next valid skip point.
        /// </summary>
        /// <param name="fullLength"><c>true</c> to skip as close to gameplay as possible, or <c>false</c> to skip only to the next valid skip point.</param>
        public void Skip(bool fullLength = false)
        {
            if (GameplayClock.CurrentTime > GameplayStartTime - MINIMUM_SKIP_TIME)
                return;

            double skipTarget = GameplayStartTime - MINIMUM_SKIP_TIME;

            if (!fullLength && StartTime < -10000 && GameplayClock.CurrentTime < 0 && skipTarget > 6000)
                // double skip exception for storyboards with very long intros
                skipTarget = 0;

            Seek(skipTarget);
        }

        /// <summary>
        /// Safely skips the currently active beatmap break while retaining a short lead-in before gameplay resumes.
        /// </summary>
        /// <returns><c>true</c> if a seek was performed.</returns>
        public bool SkipBreak(BreakPeriod breakPeriod, int breakIndex, bool multiplayerServerAuthorised)
        {
            double currentTime = GameplayClock.CurrentTime;
            double skipTarget = breakPeriod.EndTime - MINIMUM_SKIP_TIME;

            if (breakIndex < 0
                || !double.IsFinite(currentTime)
                || !double.IsFinite(breakPeriod.StartTime)
                || !double.IsFinite(breakPeriod.EndTime)
                || !breakPeriod.HasEffect
                || breakPeriod.Duration < MINIMUM_BREAK_DURATION_FOR_SKIP
                || !breakPeriod.Contains(currentTime)
                || skipTarget <= breakPeriod.StartTime
                || currentTime >= skipTarget)
                return false;

            bool wasRunning = GameplayClock.IsRunning;
            Seek(skipTarget);

            double timeAfterSeek = GameplayClock.CurrentTime;
            return wasRunning && double.IsFinite(timeAfterSeek) && timeAfterSeek - currentTime >= 0.001;
        }

        /// <summary>
        /// Changes the backing clock to avoid using the originally provided track.
        /// </summary>
        public void StopUsingBeatmapClock()
        {
            removeAdjustmentsFromTrack();

            track = new TrackVirtual(track.Length);
            track.Seek(CurrentTime);
            if (IsRunning)
                track.Start();
            ChangeSource(track);

            addAdjustmentsToTrack();
        }

        public void UseSilentReferenceClockPlayback(double minimumTime, double maximumTime)
        {
            removeAdjustmentsFromTrack();

            var referenceTrack = new ReferenceClockTrack(Clock, minimumTime, maximumTime);
            referenceTrack.Seek(CurrentTime);

            if (IsRunning)
                referenceTrack.Start();

            track = referenceTrack;
            ChangeSource(track);

            addAdjustmentsToTrack();
        }

        private bool speedAdjustmentsApplied;

        private void addAdjustmentsToTrack()
        {
            if (speedAdjustmentsApplied)
                return;

            musicController.ResetTrackAdjustments();

            track.BindAdjustments(AdjustmentsFromMods);
            track.AddAdjustment(AdjustableProperty.Frequency, UserPlaybackRate);

            speedAdjustmentsApplied = true;
        }

        private void removeAdjustmentsFromTrack()
        {
            if (!speedAdjustmentsApplied)
                return;

            track.UnbindAdjustments(AdjustmentsFromMods);
            track.RemoveAdjustment(AdjustableProperty.Frequency, UserPlaybackRate);

            speedAdjustmentsApplied = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            removeAdjustmentsFromTrack();
        }

        ControlPointInfo IBeatSyncProvider.ControlPoints => beatmap.ControlPointInfo;
        ChannelAmplitudes IHasAmplitudes.CurrentAmplitudes => track.CurrentAmplitudes;
        IClock IBeatSyncProvider.Clock => this;

        private sealed class ReferenceClockTrack : Track
        {
            private readonly IFrameBasedClock referenceClock;
            private readonly double minimumTime;
            private readonly double maximumTime;

            private bool running;
            private double? lastReferenceTime;
            private double accumulated;

            public override double Rate => base.Rate * referenceClock.Rate;

            public ReferenceClockTrack(IFrameBasedClock referenceClock, double minimumTime, double maximumTime, string name = "virtual")
                : base(name)
            {
                this.referenceClock = referenceClock;
                this.minimumTime = Math.Min(minimumTime, maximumTime);
                this.maximumTime = Math.Max(minimumTime, maximumTime);

                accumulated = this.minimumTime;
                Length = double.PositiveInfinity;
            }

            public override bool Seek(double seek)
            {
                accumulated = Math.Clamp(seek, minimumTime, maximumTime);
                lastReferenceTime = null;

                return accumulated == seek;
            }

            public override Task<bool> SeekAsync(double seek) => Task.FromResult(Seek(seek));

            public override void Start()
            {
                running = true;
            }

            public override Task StartAsync()
            {
                Start();
                return Task.CompletedTask;
            }

            public override void Reset()
            {
                Seek(minimumTime);
                base.Reset();
            }

            public override void Stop()
            {
                if (running)
                {
                    running = false;
                    lastReferenceTime = null;
                }
            }

            public override Task StopAsync()
            {
                Stop();
                return Task.CompletedTask;
            }

            public override bool IsRunning => running;

            public override double CurrentTime => Math.Clamp(accumulated, minimumTime, maximumTime);

            protected override void UpdateState()
            {
                base.UpdateState();

                if (!running)
                    return;

                double refTime = referenceClock.CurrentTime;
                double? lastRefTime = lastReferenceTime;

                if (lastRefTime != null)
                    accumulated += (refTime - lastRefTime.Value) * Rate;

                lastReferenceTime = refTime;

                if (Rate > 0 && CurrentTime >= maximumTime)
                {
                    accumulated = maximumTime;
                    Stop();
                }
                else if (Rate < 0 && CurrentTime <= minimumTime)
                {
                    accumulated = minimumTime;
                    Stop();
                }
            }
        }
    }
}
