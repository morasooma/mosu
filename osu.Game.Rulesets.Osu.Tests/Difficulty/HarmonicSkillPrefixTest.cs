using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class HarmonicSkillPrefixTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestEveryPrefixAndTransformInvalidation(bool transformed)
        {
            var skill = new TestSkill();
            var random = new Random(4317);
            for (int count = 0; count < 1500; count++)
            {
                if (count > 0)
                    skill.Values.Add(count % 11 == 0 ? 0 : count % 13 == 0 ? 10 : random.NextDouble() * 100);
                skill.Transform = transformed && count < 80;
                assertOriginal(skill);
                assertOriginal(skill); // Repeated query without Process must be identical.
            }

            // Derived transforms can modify, remove or reorder existing values.
            skill.Values[0] = double.NaN;
            skill.Values[3] = double.PositiveInfinity;
            skill.Values[4] = double.NegativeInfinity;
            assertOriginal(skill);
            skill.Values.Reverse();
            assertOriginal(skill);
            skill.Values.RemoveRange(0, 1000);
            assertOriginal(skill);
            skill.Scale = 20;
            skill.Exponent = 0.7;
            assertOriginal(skill);
            skill.Values.Clear();
            assertOriginal(skill);
            skill.Values.AddRange(new[] { -0.0, 0, 7, 7, 3, 1, double.NaN });
            assertOriginal(skill);
            skill.Values.AddRange(Enumerable.Range(0, 3000).Select(_ => random.NextDouble()));
            assertOriginal(skill); // Large batch must take the full-sort path.
        }

        private static void assertOriginal(TestSkill skill)
        {
            var values = skill.TransformedValues().Where(v => v > 0).ToList();
            values.Sort((a, b) => b.CompareTo(a));
            double expected = 0;
            double weightSum = 0;
            for (int i = 0; i < values.Count; i++)
            {
                double weight = (1 + skill.Scale / (1 + i)) / (DiffUtils.Pow(i, skill.Exponent) + 1 + skill.Scale / (1 + i));
                weightSum += weight;
                expected += values[i] * weight;
            }
            Assert.That(skill.DifficultyValue(), Is.EqualTo(expected));
            Assert.That(skill.WeightSum, Is.EqualTo(weightSum));
        }

        private class TestSkill : HarmonicSkill
        {
            public readonly List<double> Values = new List<double>();
            public bool Transform;
            public double Scale = 1;
            public double Exponent = 0.9;
            public double WeightSum => ObjectWeightSum;
            protected override double HarmonicScale => Scale;
            protected override double DecayExponent => Exponent;

            public TestSkill()
                : base(Array.Empty<Mod>())
            {
                // Keep the base non-empty while supplying an independently mutable transform.
                ObjectDifficulties.Add(1);
            }

            public List<double> TransformedValues() => Transform
                ? Values.Select(v => v * Values.Count / 80.0).ToList()
                : Values;

            protected override List<double> GetTransformedDifficulties(List<double> difficulties) => TransformedValues();
            protected override double ObjectDifficultyOf(DifficultyHitObject current) => 0;
        }
    }
}
