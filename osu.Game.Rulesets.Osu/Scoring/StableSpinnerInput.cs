using System;

namespace osu.Game.Rulesets.Osu.Scoring
{
    /// <summary>
    /// Stable's angular input filtering. Sample intervals are in beatmap milliseconds;
    /// velocity is radians per real-time millisecond. Live lazer input is resampled
    /// separately, rather than treating every update as a stable input frame.
    /// </summary>
    public sealed class StableSpinnerInput
    {
        private double averageInterval = 1000d / 60;
        private int stationarySamples;
        public double TargetVelocity { get; private set; }

        public void Sample(double angleDelta, double interval, double rate, bool tracking)
        {
            if (interval <= 0 || rate <= 0)
                return;

            double damping = Math.Pow(0.999, interval);
            averageInterval = damping * averageInterval + (1 - damping) * interval;
            if (angleDelta == 0)
            {
                TargetVelocity = stationarySamples++ < 1 ? TargetVelocity / 3 : 0;
                return;
            }

            stationarySamples = 0;
            if (!tracking)
                angleDelta = 0;
            double divisor = averageInterval / rate > 17.333332697550457 ? interval / rate : 1000d / 60;
            TargetVelocity = Math.Abs(angleDelta) < Math.PI ? angleDelta / divisor : 0;
        }
    }
}
