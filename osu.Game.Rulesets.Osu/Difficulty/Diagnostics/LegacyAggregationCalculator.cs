// Derived from the pre-optimisation osu! aggregation implementation.
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See LICENCE in the repository root.
using System;
using System.Collections.Generic;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Diagnostics
{
    // Frozen aggregation reference used only by the explicit settings check.
    // Not a gameplay backend. Evaluators and
    // preparation are intentionally shared: this checks the timed optimisation.
    internal class LegacyAggregationCalculator : OsuDifficultyCalculator
    {
        private readonly bool antiAbuse;
        private readonly Action? prefixAdded;

        public LegacyAggregationCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap, Action? prefixAdded = null)
            : base(ruleset, beatmap)
        {
            antiAbuse = !beatmap.BeatmapInfo.GetOnlineStatus().BypassesMappingAntiAbuse();
            this.prefixAdded = prefixAdded;
        }

        protected override void OnTimedPrefixAdded(IBeatmap beatmap, osu.Game.Rulesets.Objects.HitObject addedObject)
        {
            base.OnTimedPrefixAdded(beatmap, addedObject);
            prefixAdded?.Invoke();
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            var skills = new List<Skill>
            {
                new LegacyAim(mods, true, antiAbuse), new LegacyAim(mods, false, antiAbuse),
                new LegacySpeed(mods), new LegacyReading(mods)
            };
            foreach (var mod in mods)
            {
                if (mod is OsuModFlashlight)
                {
                    skills.Add(new Flashlight(mods, beatmap.HitObjects.Count));
                    break;
                }
            }
            return skills.ToArray();
        }
    }

    internal class LegacySpeed : Speed
    {
        private readonly List<double> sorted = new List<double>();
        public LegacySpeed(Mod[] mods) : base(mods) { }
        public override double DifficultyValue() => LegacyHarmonic.Sum(GetTransformedDifficulties(ObjectDifficulties), sorted, HarmonicScale, DecayExponent, out ObjectWeightSum);
    }

    internal class LegacyReading : Reading
    {
        private readonly List<double> sorted = new List<double>();
        public LegacyReading(Mod[] mods) : base(mods) { }
        public override double DifficultyValue() => LegacyHarmonic.Sum(GetTransformedDifficulties(ObjectDifficulties), sorted, HarmonicScale, DecayExponent, out ObjectWeightSum);
    }

    internal static class LegacyHarmonic
    {
        public static double Sum(List<double> values, List<double> sorted, double scale, double exponent, out double weightSum)
        {
            sorted.Clear();
            foreach (double value in values)
            {
                if (value > 0)
                    sorted.Add(value);
            }
            sorted.Sort(static (a, b) => b.CompareTo(a));
            weightSum = 0;
            double difficulty = 0;
            for (int index = 0; index < sorted.Count; index++)
            {
                double weight = (1 + (scale / (1 + index))) / (DiffUtils.Pow(index, exponent) + 1 + (scale / (1 + index)));
                weightSum += weight;
                difficulty += sorted[index] * weight;
            }
            return difficulty;
        }
    }

    internal class LegacyAim : Aim
    {
        public LegacyAim(Mod[] mods, bool sliders, bool antiAbuse) : base(mods, sliders, antiAbuse) { }
        public override double DifficultyValue()
        {
            double difficulty = 0;
            double time = 0;

            var strains = getReducedStrainPeaks();

            // Difficulty is a continuous weighted sum of the sorted strains
            foreach (var entry in strains)
            {
                StrainPeak strain = entry.Peak;
                /* Weighting function can be thought of as:
                        b
                        ∫ DecayWeight^x dx
                        a
                    where a = startTime and b = endTime

                    Technically, the function below has been slightly modified from the equation above.
                    The real function would be
                        double weight = DiffUtils.Pow(DecayWeight, startTime) - DiffUtils.Pow(DecayWeight, endTime);
                        ...
                        return difficulty / Math.Log(1 / DecayWeight);
                    E.g. for a DecayWeight of 0.9, we're multiplying by 10 instead of 9.49122...

                    This change makes it so that a map composed solely of MaxSectionLength chunks will have the exact same value when summed in this class and StrainSkill.
                    Doing this ensures the relationship between strain values and difficulty values remains the same between the two classes.
                */
                double startTime = time;
                double endTime = time + strain.SectionLength / MaxSectionLength;

                double weight = DiffUtils.Pow(DecayWeight, startTime) - DiffUtils.Pow(DecayWeight, endTime);

                difficulty += strain.Value * weight;
                time = endTime;
            }

            return difficulty / (1 - DecayWeight);
        }

        /// <summary>
        /// Returns a sorted enumerable of strain peaks with the highest values reduced.
        /// </summary>
        /// <returns></returns>
        private readonly List<StrainPeak> reductionScratch = new List<StrainPeak>();
        private readonly List<(StrainPeak Peak, int Order)> sortedReductionScratch = new List<(StrainPeak, int)>();
        private static readonly IComparer<(StrainPeak Peak, int Order)> reductionComparer = Comparer<(StrainPeak Peak, int Order)>.Create(static (a, b) =>
        {
            int result = b.Peak.Value.CompareTo(a.Peak.Value);
            // LINQ's old ordering was stable. Equal strains may have different lengths.
            return result != 0 ? result : a.Order.CompareTo(b.Order);
        });

        private List<(StrainPeak Peak, int Order)> getReducedStrainPeaks()
        {
            const int reduced_section_time = 4000;
            const double reduced_strain_baseline = 0.727;

            // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These sections will not contribute to the difficulty.
            var strains = reductionScratch;
            strains.Clear();
            foreach (var peak in GetCurrentStrainPeaks())
            {
                if (peak.Value > 0)
                    strains.Add(peak);
            }

            const int chunk_size = 20;
            double time = 0;
            int skipCount = 0;

            // We are reducing the highest strains first to account for extreme difficulty spikes
            // Strains are split into 20ms chunks to try to mitigate inconsistencies caused by reducing strains
            while (strains.Count > skipCount && time < reduced_section_time)
            {
                StrainPeak strain = strains[skipCount];

                for (double addedTime = 0; addedTime < strain.SectionLength; addedTime += chunk_size)
                {
                    double scale = Math.Log10(Interpolation.Lerp(1, 10, Math.Clamp((time + addedTime) / reduced_section_time, 0, 1)));

                    // intentionally add at end and sort afterwards, should be cheaper.
                    strains.Add(new StrainPeak(
                        strain.Value * Interpolation.Lerp(reduced_strain_baseline, 1.0, scale),
                        Math.Min(chunk_size, strain.SectionLength - addedTime)
                    ));
                }

                time += strain.SectionLength;
                skipCount++;
            }

            sortedReductionScratch.Clear();
            for (int i = skipCount; i < strains.Count; i++)
                sortedReductionScratch.Add((strains[i], i));
            sortedReductionScratch.Sort(reductionComparer);
            return sortedReductionScratch;
        }
    }
}
