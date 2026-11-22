using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class TimedDifficultyCancellationTest
    {
        [Test]
        public void TestCancelledTimedCalculationCanBeRepeatedWithoutStalePrefixState()
        {
            var map = new Beatmap<OsuHitObject>
            {
                HitObjects = Enumerable.Range(0, 200).Select(i => (OsuHitObject)new HitCircle
                {
                    StartTime = i * 100,
                    Position = new Vector2(100 + i % 3 * 150, 100 + i % 2 * 150)
                }).ToList()
            };
            var working = new FlatWorkingBeatmap(map);
            var ruleset = new OsuRuleset().RulesetInfo;
            using var cancellation = new CancellationTokenSource();
            var calculator = new CancellingCalculator(ruleset, working, cancellation);
            Assert.Throws<OperationCanceledException>(() => calculator.CalculateTimed(Array.Empty<Mod>(), cancellation.Token));
            Assert.That(calculator.Prefixes, Is.EqualTo(50));

            var resumed = calculator.CalculateTimed();
            var fresh = new OsuDifficultyCalculator(ruleset, working).CalculateTimed();
            Assert.That(resumed.Count, Is.EqualTo(map.HitObjects.Count));
            for (int i = 0; i < resumed.Count; i++)
            {
                Assert.That(resumed[i].Time, Is.EqualTo(fresh[i].Time));
                assertAttributesEqual(fresh[i].Attributes, resumed[i].Attributes);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestTimedFinalAttributesMatchFullCalculation(int variant)
        {
            var map = new Beatmap<OsuHitObject>
            {
                HitObjects = Enumerable.Range(0, 600).Select(i => (OsuHitObject)new HitCircle
                {
                    StartTime = i * 150 + (i / 30) * 3000,
                    Position = new Vector2(100 + i % 3 * 150, 100 + i % 2 * 150)
                }).ToList()
            };
            var working = new FlatWorkingBeatmap(map);
            var calculator = new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working);
            Mod[] mods = variant switch
            {
                1 => new Mod[] { new OsuModHidden(), new OsuModFlashlight() },
                2 => new Mod[] { new OsuModDoubleTime() },
                3 => new Mod[] { new OsuModHardRock() },
                _ => Array.Empty<Mod>()
            };
            var full = calculator.Calculate(mods);
            var timed = calculator.CalculateTimed(mods);
            assertAttributesEqual(full, timed[^1].Attributes);
        }

        private static void assertAttributesEqual(DifficultyAttributes expected, DifficultyAttributes actual)
        {
            foreach (var property in typeof(OsuDifficultyAttributes).GetProperties()
                         .Where(p => p.PropertyType == typeof(double) || p.PropertyType == typeof(int)))
            {
                object? oldValue = property.GetValue(expected);
                object? newValue = property.GetValue(actual);
                if (oldValue is double oldDouble && newValue is double newDouble)
                    Assert.That(BitConverter.DoubleToInt64Bits(newDouble), Is.EqualTo(BitConverter.DoubleToInt64Bits(oldDouble)), property.Name);
                else
                    Assert.That(newValue, Is.EqualTo(oldValue), property.Name);
            }
        }

        private class CancellingCalculator : OsuDifficultyCalculator
        {
            private readonly CancellationTokenSource cancellation;
            public int Prefixes;

            public CancellingCalculator(IRulesetInfo ruleset, IWorkingBeatmap working, CancellationTokenSource cancellation)
                : base(ruleset, working)
            {
                this.cancellation = cancellation;
            }

            protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
            {
                var attributes = base.CreateDifficultyAttributes(beatmap, mods, skills);
                if (++Prefixes == 50)
                    cancellation.Cancel();
                return attributes;
            }
        }
    }
}
