// Derived in part from osu! aim aggregation. Copyright (c) ppy Pty Ltd.
// Licensed under the MIT Licence. See LICENCE in the repository root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Skills;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class AimPeakAggregationTest
    {
        [Test]
        public void TestReducedPeakMergeMatchesOriginalStableSort()
        {
            var random = new Random(9274);
            for (int test = 0; test < 250; test++)
            {
                var aim = new Aim(Array.Empty<Mod>(), true);
                var peaks = (List<VariableLengthStrainSkill.StrainPeak>)typeof(VariableLengthStrainSkill)
                    .GetField("strainPeaks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(aim)!;
                double[] lengths = { 0, 1, 19, 20, 20.5, 21, 399, 400, 1000 };
                for (int i = 0; i < test * 2; i++)
                {
                    double value = i % 11 == 0 ? 0 : i % 3 == 0 ? 50 : random.NextDouble() * 100;
                    peaks.Add(new VariableLengthStrainSkill.StrainPeak(value, lengths[random.Next(lengths.Length)]));
                }
                peaks.Sort();
                var originalPeaks = aim.GetCurrentStrainPeaks().ToArray();
                Assert.That(aim.DifficultyValue(), Is.EqualTo(original(originalPeaks)), $"case {test}");
                Assert.That(aim.DifficultyValue(), Is.EqualTo(original(originalPeaks)), "Repeated query");
                Assert.That(aim.GetCurrentStrainPeaks(), Is.EqualTo(originalPeaks), "Chronological state must not be mutated");
            }
        }

        private static double original(IEnumerable<VariableLengthStrainSkill.StrainPeak> peaks)
        {
            var strains = peaks.Where(p => p.Value > 0).ToList();
            double time = 0;
            int skip = 0;
            while (strains.Count > skip && time < 4000)
            {
                var peak = strains[skip];
                for (double added = 0; added < peak.SectionLength; added += 20)
                {
                    double scale = Math.Log10(Interpolation.Lerp(1, 10, Math.Clamp((time + added) / 4000, 0, 1)));
                    strains.Add(new VariableLengthStrainSkill.StrainPeak(
                        peak.Value * Interpolation.Lerp(0.727, 1.0, scale), Math.Min(20, peak.SectionLength - added)));
                }
                time += peak.SectionLength;
                skip++;
            }
            double result = 0;
            time = 0;
            foreach (var peak in strains.Skip(skip).OrderByDescending(p => p.Value))
            {
                double end = time + peak.SectionLength / 400;
                double weight = DiffUtils.Pow(0.9, time) - DiffUtils.Pow(0.9, end);
                result += peak.Value * weight;
                time = end;
            }
            return result / (1 - 0.9);
        }
    }
}
