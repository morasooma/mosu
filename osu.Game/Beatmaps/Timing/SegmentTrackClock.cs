using System;
using osu.Framework.Timing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Beatmaps.Timing
{
    /// <summary>
    /// Audio still plays the source file, but gameplay observes its precomputed warped timeline.
    /// Tempo and time mapping cancel out, so approach/scroll animations advance at a constant rate.
    /// </summary>
    public sealed class SegmentTrackClock : IAdjustableClock, IFrameBasedClock
    {
        private readonly IAdjustableClock source;
        private readonly SegmentRatePlan plan;
        private readonly Action<double> updateTempo;

        public SegmentTrackClock(IAdjustableClock source, SegmentRatePlan plan, Action<double> updateTempo)
        {
            this.source = source;
            this.plan = plan;
            this.updateTempo = updateTempo;
            CurrentTime = plan.Map(source.CurrentTime);
        }

        public double CurrentTime { get; private set; }
        public double ElapsedFrameTime { get; private set; }
        public double FramesPerSecond => 0;
        public bool IsRunning => source.IsRunning;
        public double Rate
        {
            get => source.Rate / plan.RateAt(source.CurrentTime);
            set => source.Rate = value * plan.RateAt(source.CurrentTime);
        }

        public void ProcessFrame()
        {
            (source as IFrameBasedClock)?.ProcessFrame();
            double previous = CurrentTime;
            CurrentTime = plan.Map(source.CurrentTime);
            ElapsedFrameTime = CurrentTime - previous;
            updateTempo(source.CurrentTime);
        }

        public bool Seek(double position)
        {
            double sourceTime = plan.Unmap(position);
            updateTempo(sourceTime);
            bool result = source.Seek(sourceTime);
            CurrentTime = plan.Map(source.CurrentTime);
            ElapsedFrameTime = 0;
            return result;
        }

        public void Reset()
        {
            source.Reset();
            updateTempo(source.CurrentTime);
            CurrentTime = plan.Map(source.CurrentTime);
            ElapsedFrameTime = 0;
        }

        public void Start()
        {
            updateTempo(source.CurrentTime);
            source.Start();
        }

        public void Stop() => source.Stop();
        public void ResetSpeedAdjustments() => source.ResetSpeedAdjustments();
    }
}
