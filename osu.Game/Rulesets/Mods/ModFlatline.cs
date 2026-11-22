using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModFlatline : ModSegmentRate
    {
        public override string Name => "Flatline";
        public override string Acronym => "FN";
        public override LocalisableString Description => SegmentRateModsStrings.FlatlineDescription;
        public override IconUsage? Icon => FontAwesome.Solid.ExchangeAlt;
        public override float IconScale => 0.7f;

        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.Direction), nameof(SegmentRateModsStrings.DirectionHint), 0)]
        public Bindable<FlatlineDirection> Direction { get; } = new Bindable<FlatlineDirection>(FlatlineDirection.Harder);

        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.MaxRate), nameof(SegmentRateModsStrings.MaxRateHint), 1)]
        public BindableDouble MaxRate { get; } = new BindableDouble(3) { MinValue = 1, MaxValue = 3, Precision = 0.1 };

        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.MinRate), nameof(SegmentRateModsStrings.MinRateHint), 2)]
        public BindableDouble MinRate { get; } = new BindableDouble(0.5) { MinValue = 0.25, MaxValue = 1, Precision = 0.05 };

        protected abstract Ruleset CreateRuleset();

        public override void ApplyToBeatmap(IBeatmap beatmap)
        {
            base.ApplyToBeatmap(beatmap);
            if (beatmap.HitObjects.Count < 2)
                return;

            var geometry = CreateGeometry(beatmap);
            var blocks = new List<SegmentRateGeometry.Span>();
            foreach (var active in geometry.ActiveWithoutBreaks())
            {
                for (double start = active.Start; start < active.End;)
                {
                    double end = Math.Min(start + 2000, active.End);
                    if (!geometry.ExtendThroughLongObjects(start, ref end) || end > active.End || geometry.IsNeutral(start, end))
                        break;
                    blocks.Add(new SegmentRateGeometry.Span(start, end));
                    start = end;
                }
            }

            if (blocks.Count == 0)
                return;

            var working = new FlatWorkingBeatmap(beatmap);
            var calculator = CreateRuleset().CreateDifficultyCalculator(working);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            // Probe the real native skills on the complete map at the allowed endpoints.
            // This captures rate-sensitive aim/rhythm/reading behaviour, not a note-density power law.
            // Interpolation is an approximation: it doesn't claim exact local star equality.
            double[] normal = Scores(blocks, calculator.CalculateLocalDifficulty(beatmap, cancellationToken: cancellation.Token));
            var positive = normal.Where(d => d > 1e-8 && double.IsFinite(d)).ToArray();
            if (positive.Length == 0)
                return;

            double peak = positive.Max();
            double easy = positive.Min();
            double target = Direction.Value switch
            {
                FlatlineDirection.Harder => peak,
                FlatlineDirection.Easier => easy,
                _ => Math.Sqrt(peak * easy),
            };

            double minimum = Direction.Value == FlatlineDirection.Harder ? 1 : MinRate.Value;
            double maximum = Direction.Value == FlatlineDirection.Easier ? 1 : MaxRate.Value;
            double[] slow = minimum == 1 ? normal : Scores(blocks, calculator.CalculateLocalDifficulty(beatmap, minimum, cancellation.Token));
            double[] fast = maximum == 1 ? normal : Scores(blocks, calculator.CalculateLocalDifficulty(beatmap, maximum, cancellation.Token));
            var segments = new List<SegmentRatePlan.Segment>();
            for (int i = 0; i < blocks.Count; i++)
            {
                if (normal[i] <= 1e-8 || !double.IsFinite(normal[i]))
                    continue;

                double rate = normal[i] < target
                    ? InterpolateRate(normal[i], fast[i], target, 1, maximum)
                    : InterpolateRate(slow[i], normal[i], target, minimum, 1);
                if (Math.Abs(rate - 1) > 0.001)
                    segments.Add(new SegmentRatePlan.Segment(blocks[i].Start, blocks[i].End, rate));
            }

            RatePlan = new SegmentRatePlan(segments);
            ApplyTiming(beatmap);
        }

        internal static double InterpolateRate(double lowDifficulty, double highDifficulty, double target, double lowRate, double highRate)
        {
            if (lowRate == highRate)
                return lowRate;
            if (!double.IsFinite(lowDifficulty) || !double.IsFinite(highDifficulty) || highDifficulty <= lowDifficulty || lowDifficulty <= 0)
                return 1;
            double amount = Math.Clamp(Math.Log(target / lowDifficulty) / Math.Log(highDifficulty / lowDifficulty), 0, 1);
            return Math.Clamp(lowRate * Math.Pow(highRate / lowRate, amount), lowRate, highRate);
        }

        private static double[] Scores(IReadOnlyList<SegmentRateGeometry.Span> blocks, IReadOnlyList<LocalDifficultySample> samples)
        {
            var scores = new double[blocks.Count];
            var weights = new double[blocks.Count];
            int block = 0;
            for (int i = 0; i < samples.Count && block < blocks.Count; i++)
            {
                var sample = samples[i];
                if (!double.IsFinite(sample.Difficulty) || sample.Difficulty <= 0)
                    continue;
                while (block < blocks.Count && blocks[block].End <= sample.Time)
                    block++;
                if (block == blocks.Count)
                    break;
                double end = i + 1 < samples.Count ? samples[i + 1].Time : sample.Time + 400;
                // Do not carry strain across genuine empty passages.
                end = Math.Min(end, sample.Time + 500);
                for (int b = block; b < blocks.Count && blocks[b].Start < end; b++)
                {
                    double weight = Math.Min(end, blocks[b].End) - Math.Max(sample.Time, blocks[b].Start);
                    if (weight <= 0)
                        continue;
                    scores[b] += sample.Difficulty * weight;
                    weights[b] += weight;
                }
            }

            for (int i = 0; i < scores.Length; i++)
                scores[i] = weights[i] > 0 ? scores[i] / weights[i] : 0;
            return scores;
        }
    }

    public enum FlatlineDirection
    {
        [LocalisableDescription(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.Harder))]
        Harder,
        [LocalisableDescription(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.Easier))]
        Easier,
        [LocalisableDescription(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.Both))]
        Both,
    }
}
