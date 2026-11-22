using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Lists;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Mods
{
    internal static class SegmentBeatmapTiming
    {
        public static void Apply(IBeatmap beatmap, SegmentRatePlan plan, Action<HitObject, double, double, SegmentRatePlan> transformObject)
        {
            // Rebuild groups rather than mutating their time in place (the lookup lists are sorted).
            var original = beatmap.ControlPointInfo;
            var points = original.DeepClone();
            points.Clear();
            foreach (var point in original.AllControlPoints.Where(p => p is not TimingControlPoint))
                points.Add(plan.Map(point.Time), point.DeepClone());

            // Insert in chronological order: redundancy checks against a partially built
            // list must not discard a restoration point because of a future original point.
            foreach (double time in original.TimingPoints.Select(p => p.Time)
                                            .Concat(plan.Segments.SelectMany(s => new[] { s.Start, s.End })).Distinct().Order())
            {
                var timing = (TimingControlPoint)original.TimingPointAt(time).DeepClone();
                timing.BeatLength /= plan.RateAt(time);
                if (!original.TimingPoints.Any(p => p.Time == time))
                    timing.OmitFirstBarLine = true;
                points.Add(plan.Map(time), timing);
            }

            beatmap.ControlPointInfo = points;
            foreach (var obj in beatmap.HitObjects)
            {
                double start = obj.StartTime;
                double end = obj.GetEndTime();
                double duration = obj is IHasDuration sustained ? sustained.Duration : 0;
                obj.StartTime = plan.Map(start);
                // Sliders derive duration from the mapped red timing point. Calling their
                // Duration setter is unsupported. Holds/spinners/rolls use explicit duration.
                if (obj is IHasDuration hasDuration && obj is not IHasPathWithRepeats)
                    hasDuration.Duration = plan.Map(start + duration) - plan.Map(start);
                transformObject(obj, start, end, plan);
                // Recreate tails/repeats/ticks at mapped times, leaving AR/OD unchanged.
                if (obj is IHasDuration)
                    obj.ApplyDefaults(points, beatmap.Difficulty);
            }

            var breaks = new SortedList<BreakPeriod>(Comparer<BreakPeriod>.Default);
            breaks.AddRange(beatmap.Breaks.Select(b => new BreakPeriod(plan.Map(b.StartTime), plan.Map(b.EndTime))));
            beatmap.Breaks = breaks;
            beatmap.Bookmarks = beatmap.Bookmarks.Select(t => (int)Math.Round(plan.Map(t))).ToArray();
        }
    }
}
