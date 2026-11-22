using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>Ticks the editor grid using song time, including tempo adjustments.</summary>
    public partial class RecordingMetronome : Component
    {
        private readonly EditorBeatmap beatmap;
        private readonly EditorClock clock;
        private Sample tick = null!;
        private Sample downbeat = null!;
        private double lastBoundary = double.NaN;
        public bool Enabled { get; set; } = true;

        public RecordingMetronome(EditorBeatmap beatmap, EditorClock clock)
        {
            this.beatmap = beatmap;
            this.clock = clock;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            tick = audio.Samples.Get("UI/metronome-tick");
            downbeat = audio.Samples.Get("UI/metronome-tick-downbeat");
        }

        protected override void Update()
        {
            base.Update();
            if (!Enabled || !clock.IsRunning)
            {
                lastBoundary = double.NaN;
                return;
            }

            var timing = beatmap.ControlPointInfo.TimingPointAt(clock.CurrentTime);
            double step = timing.BeatLength / beatmap.BeatDivisor;
            if (step <= 0)
                return;
            long index = (long)Math.Floor((clock.CurrentTime - timing.Time) / step);
            double boundary = timing.Time + index * step;
            if (boundary == lastBoundary)
                return;

            // Only play the current boundary, never a burst of missed ticks after seeking/stalling.
            lastBoundary = boundary;
            if (clock.CurrentTime - boundary > Math.Min(step / 2, 80))
                return;
            long stepsPerBar = (long)timing.TimeSignature.Numerator * beatmap.BeatDivisor;
            (index % stepsPerBar == 0 ? downbeat : tick).Play();
        }
    }
}
