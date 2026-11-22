using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class ProgressiveDifficultyTest
    {
        [TestCase("5738687", 0)]
        [TestCase("1124896", 0)]
        [TestCase("4766797", 0)]
        [TestCase("4766797", 1)]
        [TestCase("1124896", 2)]
        [TestCase("1124896", 3)]
        [TestCase("5738687", 4)]
        [TestCase("5738687", 5)]
        [TestCase("5738687", 6)]
        [TestCase("5738687", 7)]
        [TestCase("5738687", 8)]
        public void TestQueriesMatchExistingTimedPathIncludingRewinds(string map, int variant)
        {
            var working = load(map);
            Mod[] mods = variant switch
            {
                1 => new Mod[] { new OsuModHidden(), new OsuModFlashlight() },
                2 => new Mod[] { new OsuModDoubleTime() },
                3 => new Mod[] { new OsuModHardRock() },
                4 => new Mod[] { new OsuModNightcore() },
                5 => new Mod[] { new OsuModDaycore() },
                6 => new Mod[] { new OsuModClassic(), new OsuModNoFail() },
                7 => new Mod[] { new OsuModTouchDevice(), new OsuModSpunOut() },
                8 => new Mod[] { new OsuModEasy(), new ModScoreV2() },
                _ => Array.Empty<Mod>()
            };
            var ruleset = new OsuRuleset().RulesetInfo;
            compareAllQueries(working, mods, ruleset);
        }

        [Test]
        public void TestAllEmbeddedMapsSparseQueriesAndRewinds()
        {
            var assembly = typeof(ProgressiveDifficultyTest).Assembly;
            int cases = 0;
            foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".osu", StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new LineBufferedReader(stream);
                var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                foreach (Mod[] mods in new[]
                         {
                             Array.Empty<Mod>(), new Mod[] { new OsuModHidden(), new OsuModFlashlight() },
                             new Mod[] { new OsuModDoubleTime() }, new Mod[] { new OsuModHardRock(), new OsuModHidden() }
                         })
                {
                    var working = new FlatWorkingBeatmap(decoded);
                    var ruleset = new OsuRuleset().RulesetInfo;
                    var timed = new OsuDifficultyCalculator(ruleset, working).CalculateTimed(mods);
                    using var cursor = new OsuDifficultyCalculator(ruleset, working).CreateProgressiveCalculation(mods);
                    if (timed.Count == 0)
                    {
                        Assert.That(cursor!.GetAttributesAtTime(0), Is.Null);
                        continue;
                    }
                    foreach (double time in timed.Where((_, i) => i % 17 == 0).Select(t => t.Time)
                                                 .Append(timed[^1].Time).Append(timed[0].Time).Append(timed[^1].Time))
                    {
                        int index = timed.BinarySearch(new TimedDifficultyAttributes(time, null!));
                        if (index < 0)
                            index = ~index - 1;
                        assertEqual(timed[Math.Clamp(index, 0, timed.Count - 1)].Attributes, cursor!.GetAttributesAtTime(time)!);
                    }
                    cases++;
                }
            }
            TestContext.Out.WriteLine($"Sparse/rewind exact parity: {cases} map/mod cases");
            Assert.That(cases, Is.GreaterThanOrEqualTo(100));
        }

        [Test]
        public void TestOverlapsDuplicateEndsAndSpinners()
        {
            var map = new Beatmap<osu.Game.Rulesets.Osu.Objects.OsuHitObject>
            {
                HitObjects = new List<osu.Game.Rulesets.Osu.Objects.OsuHitObject>
                {
                    new osu.Game.Rulesets.Osu.Objects.Spinner { StartTime = 100, EndTime = 5000 },
                    new osu.Game.Rulesets.Osu.Objects.HitCircle { StartTime = 200 },
                    new osu.Game.Rulesets.Osu.Objects.HitCircle { StartTime = 200 },
                    new osu.Game.Rulesets.Osu.Objects.Spinner { StartTime = 300, EndTime = 800 },
                    new osu.Game.Rulesets.Osu.Objects.HitCircle { StartTime = 1000 }
                }
            };
            compareAllQueries(new FlatWorkingBeatmap(map), Array.Empty<Mod>(), new OsuRuleset().RulesetInfo);
        }

        private static void compareAllQueries(FlatWorkingBeatmap working, Mod[] mods, osu.Game.Rulesets.IRulesetInfo ruleset)
        {
            var timed = new OsuDifficultyCalculator(ruleset, working).CalculateTimed(mods);
            using var cursor = new OsuDifficultyCalculator(ruleset, working).CreateProgressiveCalculation(mods);
            var times = timed.Select(t => t.Time).ToArray();
            foreach (double time in times)
                compare(time);
            foreach (double time in new[] { times[^1], times[0], times[times.Length / 2], times[^1], times[2], times[2], -1000.0, double.MaxValue })
                compare(time);
            using var sparse = new OsuDifficultyCalculator(ruleset, working).CreateProgressiveCalculation(mods);
            foreach (double time in times.Where((_, i) => i % 43 == 0).Append(times[^1]))
                assertEqual(expected(time), sparse.GetAttributesAtTime(time)!);

            void compare(double time) => assertEqual(expected(time), cursor.GetAttributesAtTime(time)!);
            DifficultyAttributes expected(double time)
            {
                int index = timed.BinarySearch(new TimedDifficultyAttributes(time, null!));
                if (index < 0)
                    index = ~index - 1;
                return timed[Math.Clamp(index, 0, timed.Count - 1)].Attributes;
            }
        }

        [Test]
        public void TestRecentPrefixWindowAvoidsReprocessingAndEvictsOldEntries()
        {
            var working = load("1124896");
            var ruleset = new OsuRuleset().RulesetInfo;
            var timed = new OsuDifficultyCalculator(ruleset, working).CalculateTimed();
            var calculator = new CountingCalculator(ruleset, working);
            using var cursor = calculator.CreateProgressiveCalculation(Array.Empty<Mod>());
            foreach (var entry in timed)
                cursor!.GetAttributesAtTime(entry.Time);
            int processed = calculator.Prefixes;
            int aggregated = calculator.Aggregations;
            foreach (var entry in timed.TakeLast(30).Reverse())
                assertEqual(entry.Attributes, cursor!.GetAttributesAtTime(entry.Time)!);
            Assert.That(calculator.Prefixes, Is.EqualTo(processed), "recent rewind should not rebuild skills");
            Assert.That(calculator.Aggregations, Is.EqualTo(aggregated), "recent rewind should not aggregate again");
            assertEqual(timed[0].Attributes, cursor!.GetAttributesAtTime(timed[0].Time)!);
            Assert.That(calculator.Prefixes, Is.GreaterThan(processed), "evicted rewind must rebuild correctly");
            assertEqual(timed[^1].Attributes, cursor.GetAttributesAtTime(timed[^1].Time)!);
            assertEqual(timed[timed.Count / 2].Attributes, cursor.GetAttributesAtTime(timed[timed.Count / 2].Time)!);
            assertEqual(timed[^1].Attributes, cursor.GetAttributesAtTime(timed[^1].Time)!);
        }

        private class CountingCalculator : OsuDifficultyCalculator
        {
            public int Prefixes { get; private set; }
            public int Aggregations { get; private set; }
            public CountingCalculator(osu.Game.Rulesets.IRulesetInfo ruleset, IWorkingBeatmap working) : base(ruleset, working) { }
            protected override void OnTimedPrefixAdded(IBeatmap beatmap, osu.Game.Rulesets.Objects.HitObject hitObject)
            {
                Prefixes++;
                base.OnTimedPrefixAdded(beatmap, hitObject);
            }
            protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, osu.Game.Rulesets.Difficulty.Skills.Skill[] skills)
            {
                Aggregations++;
                return base.CreateDifficultyAttributes(beatmap, mods, skills);
            }
        }

        [Test]
        public void TestCancellationDuringAdvanceRebuildsBeforeRetry()
        {
            var working = load("1124896");
            var ruleset = new OsuRuleset().RulesetInfo;
            var expected = new OsuDifficultyCalculator(ruleset, working).CalculateTimed();
            using var cancellation = new CancellationTokenSource();
            var calculator = new CancelDuringAdvance(ruleset, working, cancellation);
            using var cursor = calculator.CreateProgressiveCalculation(Array.Empty<Mod>());
            Assert.Throws<OperationCanceledException>(() => cursor!.GetAttributesAtTime(double.MaxValue, cancellation.Token));
            assertEqual(expected[^1].Attributes, cursor!.GetAttributesAtTime(double.MaxValue)!);
        }

        private class CancelDuringAdvance : OsuDifficultyCalculator
        {
            private readonly CancellationTokenSource cancellation;
            private int count;
            public CancelDuringAdvance(osu.Game.Rulesets.IRulesetInfo ruleset, IWorkingBeatmap working, CancellationTokenSource cancellation)
                : base(ruleset, working) => this.cancellation = cancellation;

            protected override void OnTimedPrefixAdded(IBeatmap beatmap, osu.Game.Rulesets.Objects.HitObject hitObject)
            {
                base.OnTimedPrefixAdded(beatmap, hitObject);
                if (++count == 50)
                    cancellation.Cancel();
            }
        }

        [Test]
        public void TestCancellationDisposalAndUnsupportedRelax()
        {
            var working = load("5738687");
            var calculator = new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working);
            Assert.That(calculator.SupportsProgressiveCalculation(new Mod[] { new OsuModRelax() }), Is.False);
            Assert.That(calculator.SupportsProgressiveCalculation(new Mod[] { new OsuModMagnetised() }), Is.False);
            Assert.That(calculator.CreateProgressiveCalculation(new Mod[] { new OsuModRelax() }), Is.Null);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => calculator.CreateProgressiveCalculation(Array.Empty<Mod>(), cancelled.Token));
            var cursor = calculator.CreateProgressiveCalculation(Array.Empty<Mod>());
            Assert.Throws<OperationCanceledException>(() => cursor.GetAttributesAtTime(1000, cancelled.Token));
            Assert.That(cursor.GetAttributesAtTime(1000), Is.Not.Null);
            cursor.Dispose();
            Assert.Throws<ObjectDisposedException>(() => cursor.GetAttributesAtTime(1000));
        }

        private static FlatWorkingBeatmap load(string name)
        {
            using var stream = typeof(ProgressiveDifficultyTest).Assembly.GetManifestResourceStream(
                $"osu.Game.Rulesets.Osu.Tests.Resources.Testing.Beatmaps.{name}.osu")!;
            using var reader = new LineBufferedReader(stream);
            return new FlatWorkingBeatmap(Decoder.GetDecoder<Beatmap>(reader).Decode(reader));
        }

        private static void assertEqual(DifficultyAttributes expected, DifficultyAttributes actual)
        {
            foreach (var property in typeof(OsuDifficultyAttributes).GetProperties())
            {
                if (property.PropertyType == typeof(double))
                    Assert.That(BitConverter.DoubleToInt64Bits((double)property.GetValue(actual)!),
                        Is.EqualTo(BitConverter.DoubleToInt64Bits((double)property.GetValue(expected)!)), property.Name);
                else if (property.PropertyType == typeof(int))
                    Assert.That(property.GetValue(actual), Is.EqualTo(property.GetValue(expected)), property.Name);
            }
        }
    }
}
