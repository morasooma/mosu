using System;

namespace osu.Game.Beatmaps.Timing
{
    /// <summary>Detects missing source progress across a long, uninterrupted wall-clock gap.</summary>
    internal sealed class GameplayFreezeRecovery
    {
        private double? wall;
        private double source;
        private double rate;

        public void Reset() => wall = null;

        public double GetMissingProgress(double wallMs, double sourceMs, double currentRate, bool running)
        {
            if (!running || currentRate <= 0 || !double.IsFinite(wallMs) || !double.IsFinite(sourceMs))
            {
                Reset();
                return 0;
            }

            double correction = 0;
            if (wall is double previousWall && currentRate == rate)
            {
                double gap = wallMs - previousWall;
                double progress = sourceMs - source;
                double missing = gap * currentRate - progress;
                // Ordinary audio reporting jitter is not a freeze. Never repair backwards
                // source jumps here: those indicate an intentional seek or discontinuity.
                if (gap >= 250 && progress >= 0 && missing >= 100 * currentRate)
                    correction = missing;
            }
            wall = wallMs;
            source = sourceMs + correction;
            rate = currentRate;
            return correction;
        }
    }
}
