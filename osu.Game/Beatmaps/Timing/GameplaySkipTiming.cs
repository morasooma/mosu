using System;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Beatmaps.Timing
{
    /// <summary>Shared by the player and server-side playable-beatmap validation.</summary>
    public static class GameplaySkipTiming
    {
        public const double ResumeMargin = 250;
        public const double MinimumLeadIn = 1000;
        public const double MaximumBreakDuration = 10 * 60 * 1000;

        public static BreakPeriod[] GetBreaks(IBeatmap beatmap) => beatmap.Breaks
            .Where(b => b.HasEffect).Distinct().OrderBy(b => b.StartTime).ToArray();

        public static double GetBreakTarget(IBeatmap beatmap, BreakPeriod period)
        {
            double target = period.EndTime - MinimumLeadIn;
            if (period.StartTime < 0 || period.Duration < 2000 || period.Duration > MaximumBreakDuration)
                return period.StartTime;

            foreach (HitObject hitObject in beatmap.HitObjects)
            {
                // An authored break must not allow skipping the tail of a slider/hold.
                if (hitObject.StartTime < period.StartTime && hitObject.GetEndTime() > period.StartTime)
                    return period.StartTime;
                if (hitObject.StartTime < period.StartTime)
                    continue;

                double preempt = hitObject is IHasTimePreempt timed ? timed.TimePreempt : 2000;
                target = Math.Min(target, hitObject.StartTime - Math.Max(0, preempt) - ResumeMargin);
            }

            return target;
        }

        public static double GetIntroCeiling(IBeatmap beatmap) => beatmap.HitObjects.Count == 0 ? double.NegativeInfinity :
            beatmap.HitObjects.Min(h => h.StartTime - Math.Max(MinimumLeadIn,
                h is IHasTimePreempt timed ? timed.TimePreempt : 2000));
    }
}
