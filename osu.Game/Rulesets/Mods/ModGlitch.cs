using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mods
{
    public class ModGlitch : ModSegmentRate
    {
        public override string Name => "Glitch";
        public override string Acronym => "GL";
        public override LocalisableString Description => SegmentRateModsStrings.GlitchDescription;
        public override IconUsage? Icon => FontAwesome.Solid.Bolt;
        public override float IconScale => 0.7f;

        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.Amount), nameof(SegmentRateModsStrings.AmountHint), 0)]
        public BindableInt Amount { get; } = new BindableInt(25) { MinValue = 0, MaxValue = 100 };

        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.Strength), nameof(SegmentRateModsStrings.StrengthHint), 1)]
        public BindableDouble Strength { get; } = new BindableDouble(1) { MinValue = 0, MaxValue = 1, Precision = 0.05 };

        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.Thirds), nameof(SegmentRateModsStrings.ThirdsHint), 2)]
        public BindableBool Thirds { get; } = new BindableBool(true);

        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.Fifths), nameof(SegmentRateModsStrings.FifthsHint), 3)]
        public BindableBool Fifths { get; } = new BindableBool(true);

        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.Sixths), nameof(SegmentRateModsStrings.SixthsHint), 4)]
        public BindableBool Sixths { get; } = new BindableBool(true);

        // Deliberately fixed, not wall-clock/random state and not a user-editable setting.
        public const int SEED = 42;

        public override void ApplyToBeatmap(IBeatmap beatmap)
        {
            base.ApplyToBeatmap(beatmap);
            if (beatmap.HitObjects.Count == 0 || Amount.Value == 0 || Strength.Value == 0)
                return;

            var targets = new List<int>();
            if (Thirds.Value) targets.Add(3);
            if (Fifths.Value) targets.Add(5);
            if (Sixths.Value) targets.Add(6);
            if (targets.Count == 0)
                return;

            var geometry = CreateGeometry(beatmap);
            var segments = new List<SegmentRatePlan.Segment>();
            double first = beatmap.HitObjects.Min(h => h.StartTime);
            double last = beatmap.HitObjects.Max(h => h.GetEndTime());
            uint random = SEED;
            double occupiedUntil = double.NegativeInfinity;
            IReadOnlyList<TimingControlPoint> timings = beatmap.ControlPointInfo.TimingPoints.Count > 0
                ? beatmap.ControlPointInfo.TimingPoints
                : new[] { TimingControlPoint.DEFAULT };

            for (int i = 0; i < timings.Count; i++)
            {
                var timing = timings[i];
                double step = timing.BeatLength / 4;
                if (!double.IsFinite(step) || step < 10)
                    continue;
                double sectionEnd = i + 1 < timings.Count ? Math.Min(last, timings[i + 1].Time) : last;
                double firstStep = Math.Ceiling((first - timing.Time) / step);
                double start = timing.Time + (i == 0 ? firstStep : Math.Max(0, firstStep)) * step;

                for (; start < sectionEnd; start += step)
                {
                    if (start < occupiedUntil)
                        continue;
                    double end = Math.Min(start + step, sectionEnd);
                    if (!geometry.IsActive(start, end) || !geometry.ExtendThroughLongObjects(start, ref end)
                        || geometry.IsNeutral(start, end) || end > last || end - start < 10)
                        continue;
                    if (next(ref random) % 100 >= Amount.Value)
                        continue;

                    int target = targets[(int)(next(ref random) % (uint)targets.Count)];
                    double rate = Math.Pow(target / 4.0, Strength.Value);
                    segments.Add(new SegmentRatePlan.Segment(start, end, rate));
                    occupiedUntil = end;
                }
            }

            RatePlan = new SegmentRatePlan(segments);
            ApplyTiming(beatmap);
        }

        // Explicit algorithm rather than System.Random's runtime-dependent implementation.
        private static uint next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
