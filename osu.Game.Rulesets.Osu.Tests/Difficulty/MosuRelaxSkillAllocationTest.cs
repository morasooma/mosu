using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NUnit.Framework;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class MosuRelaxSkillAllocationTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestEveryGrowingPrefixMatchesOriginalAndReusesScratch(bool speed)
        {
            var skill = new MosuRelaxDifficultySkill(Array.Empty<Mod>(), speed ? MosuRelaxDifficultySkill.SkillKind.Speed : MosuRelaxDifficultySkill.SkillKind.Aim, true);
            var peaks = (List<double>)typeof(StrainSkill).GetField("strainPeaks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(skill)!;
            var current = typeof(StrainSkill).GetField("currentSectionPeak", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var strains = (List<double>)typeof(Skill).GetField("ObjectDifficulties", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(skill)!;
            var random = new Random(771);
            for (int n = 0; n < 1800; n++)
            {
                if (n > 0) peaks.Add(n % 11 == 0 ? 0 : n % 13 == 0 ? 50 : random.NextDouble() * 100);
                strains.Add(random.NextDouble() * 100);
                current.SetValue(skill, n % 7 == 0 ? 0d : 1000d);
                double[] values = skill.GetCurrentStrainPeaks().ToArray();
                double difficulty = originalDifficulty(values);
                Assert.That(skill.DifficultyValue(), Is.EqualTo(difficulty), $"prefix {n}");
                double singleStrain = difficulty / 10;
                double expectedCount = singleStrain <= 0 ? 0 : strains.Sum(strain => 1.1 / (1 + Math.Exp(-10 * (strain / singleStrain - .88))));
                Assert.That(skill.CountDifficultStrains(), Is.EqualTo(expectedCount), $"strain count {n}");
                Assert.That(skill.CalculateSectionSpikeFillerWeight(), Is.EqualTo(originalFiller(values)), $"filler {n}");
                Assert.That(skill.SectionCount, Is.EqualTo(values.Length));
                // Sorting/attenuation must never change the stored chronological strain peaks.
                Assert.That(skill.GetCurrentStrainPeaks(), Is.EqualTo(values));
            }
            skill.DifficultyValue();
            skill.CalculateSectionSpikeFillerWeight();
            skill.CountDifficultStrains();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                skill.DifficultyValue();
                skill.CalculateSectionSpikeFillerWeight();
                skill.CountDifficultStrains();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Progress.WriteLine($"MOSU_RELAX_AGGREGATION speed={speed} sections=1800 calls=3000 allocatedBytes={allocated}");
            Assert.That(allocated, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRecentHistoryMatchesOriginalInsertionOrder(bool antiAbuse)
        {
            var skill = new MosuRelaxDifficultySkill(Array.Empty<Mod>(), MosuRelaxDifficultySkill.SkillKind.Aim, antiAbuse);
            var historyField = typeof(MosuRelaxDifficultySkill).GetField("recentObjects", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var headField = typeof(MosuRelaxDifficultySkill).GetField("recentObjectHead", BindingFlags.Instance | BindingFlags.NonPublic);
            var countField = typeof(MosuRelaxDifficultySkill).GetField("recentObjectCount", BindingFlags.Instance | BindingFlags.NonPublic);
            var objects = new List<DifficultyHitObject>();
            var expected = new List<(double? VectorAngle, double StrainTime, double JumpDistance, double? Angle)>();
            var random = new Random(9327);
            OsuHitObject previous = new HitCircle { Position = new Vector2(256, 192) };
            OsuHitObject? previousPrevious = null;
            double time = 0;

            for (int i = 0; i < 1200; i++)
            {
                time += i % 17 == 0 ? 0 : 30 + random.Next(200);
                OsuHitObject current = i % 23 == 0
                    ? new Spinner { Duration = 300 }
                    : new HitCircle();
                current.Position = new Vector2(random.Next(512), random.Next(384));
                current.StartTime = time;
                current.Scale = .5f + (float)random.NextDouble();
                current.HitWindows = HitWindows.Empty;
                var difficultyObject = new DifficultyHitObject(current, previous, 1, objects, i);
                objects.Add(difficultyObject);

                Vector2 second = current.Position - previous.Position;
                double? vectorAngle = current is not Spinner && previousPrevious != null
                    ? Math.Atan2(Math.Abs(second.Y), Math.Abs(second.X)) : null;
                double? angle = null;
                if (previousPrevious != null)
                {
                    Vector2 first = previousPrevious.Position - previous.Position;
                    angle = Math.Abs(Math.Atan2(first.X * second.Y - first.Y * second.X, Vector2.Dot(first, second)));
                }
                double jumpDistance = current is Spinner ? 0 : second.Length * (52 / current.Radius);
                if (expected.Count == 8)
                    expected.RemoveAt(expected.Count - 1);
                expected.Insert(0, (vectorAngle, Math.Max(difficultyObject.DeltaTime, 50), jumpDistance, angle));

                skill.Process(difficultyObject);
                object history = historyField.GetValue(skill)!;
                int count = countField != null ? (int)countField.GetValue(skill)! : ((System.Collections.IList)history).Count;
                int head = headField != null ? (int)headField.GetValue(skill)! : 0;
                Assert.That(count, Is.EqualTo(expected.Count), $"object {i}");
                for (int n = 0; n < count; n++)
                {
                    object actual = ((System.Collections.IList)history)[(head + n) % 8]!;
                    var type = actual.GetType();
                    Assert.That(type.GetProperty("NormalisedVectorAngle")!.GetValue(actual), Is.EqualTo(expected[n].VectorAngle), $"object {i}, history {n}");
                    Assert.That(type.GetProperty("StrainTime")!.GetValue(actual), Is.EqualTo(expected[n].StrainTime));
                    Assert.That(type.GetProperty("JumpDistance")!.GetValue(actual), Is.EqualTo(expected[n].JumpDistance));
                    Assert.That(type.GetProperty("Angle")!.GetValue(actual), Is.EqualTo(expected[n].Angle));
                }
                previousPrevious = previous;
                previous = current;
            }

            string digest = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(skill.GetObjectDifficulties().ToArray().AsSpan())));
            TestContext.Progress.WriteLine($"RELAX_HISTORY antiAbuse={antiAbuse} strainsSHA256={digest} difficulty={skill.DifficultyValue():R} " +
                                           $"difficultCount={skill.CountDifficultStrains():R} filler={skill.CalculateSectionSpikeFillerWeight():R}");
            // Compare this diagnostic digest to the pre-change run on the same runtime/OS.
            // Do not pin libm-dependent floating-point bytes as a cross-platform expectation.
        }

        private static double originalDifficulty(double[] values)
        {
            var peaks = values.Where(p => p > 0).ToArray();
            if (peaks.Length == 0) return 0;
            int early = (int)Math.Floor(peaks.Length * .85);
            if (peaks.Length > 5 && early > 0)
            {
                double threshold = Math.Max(peaks.Take(early).Max() * 1.35, 1);
                for (int i = early; i < peaks.Length; i++)
                    if (peaks[i] > threshold) peaks[i] = threshold + (peaks[i] - threshold) * .62;
            }
            Array.Sort(peaks, (a, b) => b.CompareTo(a));
            double result = 0, weight = 1;
            foreach (double peak in peaks) { result += peak * weight; weight *= .9; }
            return result;
        }

        private static double originalFiller(double[] values)
        {
            double[] peaks = values.ToArray();
            if (peaks.Length < 150) return 0;
            Array.Sort(peaks);
            double reference = peaks.Skip(peaks.Length - Math.Min(peaks.Length, 3)).Average();
            if (reference <= .0001) return 0;
            int index = (int)Math.Round((peaks.Length - 1) * .60, MidpointRounding.AwayFromZero);
            double baseline = Math.Max(peaks[Math.Min(index, peaks.Length - 1)], .0001);
            double strength = smooth(reference / baseline, 1.35, 1.90);
            if (strength <= 0) return 0;
            double coverage = (double)peaks.Count(p => p >= reference * .72) / peaks.Length;
            return Math.Clamp(strength * (1 - smooth(coverage, .08, .24)) * smooth(peaks.Length, 150, 300), 0, 1);
        }
        private static double smooth(double value, double start, double end)
        {
            double x = Math.Clamp((value - start) / (end - start), 0, 1);
            return x * x * (3 - 2 * x);
        }
    }
}
