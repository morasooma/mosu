using System;
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
    public class OsuDifficultyBatchTest
    {
        [Test]
        public void TestEveryLegacyCombinationAcrossAllEmbeddedMaps()
        {
            int maps = 0, cases = 0;
            var assembly = typeof(OsuDifficultyBatchTest).Assembly;
            foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".osu", StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new LineBufferedReader(stream);
                var working = new FlatWorkingBeatmap(Decoder.GetDecoder<Beatmap>(reader).Decode(reader));
                var ruleset = new OsuRuleset().RulesetInfo;
                var calculator = new OsuDifficultyCalculator(ruleset, working);
                var combinations = calculator.CreateDifficultyAdjustmentModCombinations()
                                             .Select(m => osu.Game.Utils.ModUtils.FlattenMod(m).Append(new OsuModClassic()).ToArray()).ToArray();
                var actual = calculator.CalculateAllLegacyCombinations().ToArray();
                Assert.That(actual.Length, Is.EqualTo(combinations.Length));
                for (int i = 0; i < actual.Length; i++)
                {
                    assertEqual(new OsuDifficultyCalculator(ruleset, working).Calculate(combinations[i]), actual[i]);
                    cases++;
                }
                maps++;
            }
            TestContext.Out.WriteLine($"Batch exact parity: {maps} maps / {cases} combinations");
            Assert.That(maps, Is.GreaterThanOrEqualTo(25));
        }

        [Test]
        public void TestConfiguredRatesFallbackCancellationAndRepeatedEnumeration()
        {
            using var stream = typeof(OsuDifficultyBatchTest).Assembly.GetManifestResourceStream(
                "osu.Game.Rulesets.Osu.Tests.Resources.Testing.Beatmaps.1124896.osu")!;
            using var reader = new LineBufferedReader(stream);
            var working = new FlatWorkingBeatmap(Decoder.GetDecoder<Beatmap>(reader).Decode(reader));
            var ruleset = new OsuRuleset().RulesetInfo;
            var dt = new OsuModDoubleTime(); dt.SpeedChange.Value = 1.6; dt.LockDifficultyAdjust.Value = true;
            var hidden = new OsuModHidden(); hidden.OnlyFadeApproachCircles.Value = true;
            Mod[][] combinations =
            {
                new Mod[] { dt }, new Mod[] { dt, new OsuModTouchDevice() },
                new Mod[] { hidden }, new Mod[] { hidden, new OsuModFlashlight() },
                new Mod[] { new OsuModMagnetised() }, new Mod[] { new OsuModRelax() },
                Array.Empty<Mod>(), new Mod[] { new OsuModFlashlight() }
            };
            var calculator = new OsuDifficultyCalculator(ruleset, working);
            var batch = calculator.CalculateBatch(combinations);
            foreach (var results in new[] { batch.ToArray(), batch.ToArray() })
                for (int i = 0; i < combinations.Length; i++)
                    assertEqual(calculator.Calculate(combinations[i]), results[i]);
            using var cancellation = new CancellationTokenSource();
            using var enumerator = calculator.CalculateBatch(combinations, cancellation.Token).GetEnumerator();
            Assert.That(enumerator.MoveNext(), Is.True);
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => enumerator.MoveNext());
            assertEqual(new OsuDifficultyCalculator(ruleset, working).Calculate(), calculator.Calculate());
        }

        [Test]
        public void TestPerfectScorePerformanceMatchesForEachBatchResult()
        {
            using var stream = typeof(OsuDifficultyBatchTest).Assembly.GetManifestResourceStream(
                "osu.Game.Rulesets.Osu.Tests.Resources.Testing.Beatmaps.1124896.osu")!;
            using var reader = new LineBufferedReader(stream);
            var working = new FlatWorkingBeatmap(Decoder.GetDecoder<Beatmap>(reader).Decode(reader));
            var ruleset = new OsuRuleset();
            var baseline = new OsuDifficultyCalculator(ruleset.RulesetInfo, working);
            var batch = new OsuDifficultyCalculator(ruleset.RulesetInfo, working);
            var combinations = baseline.CreateDifficultyAdjustmentModCombinations()
                .Select(m => osu.Game.Utils.ModUtils.FlattenMod(m).Append(new OsuModClassic()).ToArray()).ToArray();
            int index = 0;
            foreach (var actual in batch.CalculateBatch(combinations))
            {
                var expected = baseline.Calculate(combinations[index]);
                var oldPerformance = calculatePerformance(expected, prepared(baseline));
                var newPerformance = calculatePerformance(actual, prepared(batch));
                foreach (var property in typeof(OsuPerformanceAttributes).GetProperties().Where(p => p.PropertyType == typeof(double) || p.PropertyType == typeof(double?)))
                {
                    object? a = property.GetValue(oldPerformance), b = property.GetValue(newPerformance);
                    Assert.That(a == null ? b == null : b is double value && BitConverter.DoubleToInt64Bits((double)a) == BitConverter.DoubleToInt64Bits(value), Is.True, property.Name);
                }
                index++;
            }
            Assert.That(index, Is.EqualTo(combinations.Length));

            PerformanceAttributes calculatePerformance(DifficultyAttributes attributes, IBeatmap playable)
            {
                var processor = ruleset.CreateScoreProcessor();
                processor.Mods.Value = attributes.Mods;
                processor.ApplyBeatmap(playable);
                var score = new osu.Game.Scoring.ScoreInfo(working.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Mods = attributes.Mods, Accuracy = 1, Combo = processor.MaximumCombo, MaxCombo = processor.MaximumCombo,
                    Statistics = processor.MaximumStatistics, MaximumStatistics = processor.MaximumStatistics,
                    TotalScore = processor.MaximumTotalScore
                };
                return new OsuPerformanceCalculator().Calculate(score, attributes);
            }
        }

        [Test]
        public void TestPreparationReuseBoundaryAndPreservedCalculatedBeatmap()
        {
            using var stream = typeof(OsuDifficultyBatchTest).Assembly.GetManifestResourceStream(
                "osu.Game.Rulesets.Osu.Tests.Resources.Testing.Beatmaps.5738687.osu")!;
            using var reader = new LineBufferedReader(stream);
            var source = new FlatWorkingBeatmap(Decoder.GetDecoder<Beatmap>(reader).Decode(reader));
            var ruleset = new OsuRuleset().RulesetInfo;
            var shared = new OsuDifficultyBatch(source);
            var plain = shared.GetPlayableBeatmap(ruleset, Array.Empty<Mod>(), CancellationToken.None);
            Assert.That(shared.GetPlayableBeatmap(ruleset, new Mod[] { new OsuModTouchDevice(), new OsuModFlashlight() }, CancellationToken.None), Is.SameAs(plain));
            Assert.That(shared.GetPlayableBeatmap(ruleset, new Mod[] { new OsuModHidden() }, CancellationToken.None), Is.Not.SameAs(plain));
            shared.GetPlayableBeatmap(ruleset, new Mod[] { new OsuModMagnetised() }, CancellationToken.None);
            Assert.That(shared.GetPlayableBeatmap(ruleset, Array.Empty<Mod>(), CancellationToken.None), Is.Not.SameAs(plain));
            var calculator = new OsuDifficultyCalculator(ruleset, source);
            Assert.That(calculator.CalculateBatch(new[] { Array.Empty<Mod>() }).Count(), Is.EqualTo(1));
            Assert.That(prepared(calculator).HitObjects.Count, Is.EqualTo(source.Beatmap.HitObjects.Count));
            Assert.That(OsuDifficultyBatch.SupportsSource(new FlatWorkingBeatmap(plain)), Is.False, "typed objects are not independent playable clones");
            var overridden = new OverriddenWorkingBeatmap(source.Beatmap);
            Assert.That(OsuDifficultyBatch.SupportsSource(overridden), Is.False);
            new OsuDifficultyCalculator(ruleset, overridden).CalculateBatch(new[] { Array.Empty<Mod>(), Array.Empty<Mod>() }).ToArray();
            Assert.That(overridden.Requests, Is.EqualTo(2), "custom preparation must not be bypassed");
        }

        private sealed class OverriddenWorkingBeatmap : FlatWorkingBeatmap
        {
            public int Requests { get; private set; }
            public OverriddenWorkingBeatmap(IBeatmap beatmap) : base(beatmap) { }
            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, System.Collections.Generic.IReadOnlyList<Mod> mods, CancellationToken token)
            {
                Requests++;
                return base.GetPlayableBeatmap(ruleset, mods, token);
            }
        }

        [Test]
        [Explicit("Manual warmed full-batch difficulty throughput and allocation measurement")]
        [TestCase(false)]
        [TestCase(true)]
        public void MeasureAllMapsAndLegacyMods(bool includePerformance)
        {
            var assembly = typeof(OsuDifficultyBatchTest).Assembly;
            var maps = assembly.GetManifestResourceNames().Where(name => name.EndsWith(".osu", StringComparison.Ordinal)).Select(resource =>
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new LineBufferedReader(stream);
                return new FlatWorkingBeatmap(Decoder.GetDecoder<Beatmap>(reader).Decode(reader));
            }).ToArray();
            var ruleset = new OsuRuleset().RulesetInfo;
            var combinations = new OsuDifficultyCalculator(ruleset, maps[0]).CreateDifficultyAdjustmentModCombinations()
                .Select(m => osu.Game.Utils.ModUtils.FlattenMod(m).Append(new OsuModClassic()).ToArray()).ToArray();
            foreach (bool independentAim in new[] { true, false })
            {
                for (int run = 0; run < 3; run++)
                {
                    // Alternate order on each repeat. The first pair warms both paths.
                    foreach (bool batch in run % 2 == 0 ? new[] { false, true } : new[] { true, false })
                    {
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        double sink = 0;
                        foreach (var map in maps)
                        {
                            var rulesetInstance = new OsuRuleset();
                            var performanceCalculator = new OsuPerformanceCalculator();
                            OsuDifficultyCalculator calculator = independentAim && !batch ? new IndependentAimCalculator(ruleset, map) : new OsuDifficultyCalculator(ruleset, map);
                            var results = batch ? calculator.CalculateBatch(combinations) : combinations.Select(mods => calculator.Calculate(mods));
                            foreach (var result in results)
                            {
                                sink += result.StarRating;
                                if (includePerformance)
                                {
                                    var processor = rulesetInstance.CreateScoreProcessor();
                                    processor.Mods.Value = result.Mods;
                                    processor.ApplyBeatmap(prepared(calculator));
                                    var score = new osu.Game.Scoring.ScoreInfo(map.BeatmapInfo, ruleset)
                                    {
                                        Mods = result.Mods, Accuracy = 1, Combo = processor.MaximumCombo, MaxCombo = processor.MaximumCombo,
                                        Statistics = processor.MaximumStatistics, MaximumStatistics = processor.MaximumStatistics,
                                        TotalScore = processor.MaximumTotalScore
                                    };
                                    sink += performanceCalculator.Calculate(score, result).Total;
                                }
                            }
                        }
                        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                        double ms = timer.Elapsed.TotalMilliseconds;
                        if (run > 0)
                            TestContext.Out.WriteLine($"pp={includePerformance}; batch={batch}; independentBaselineAim={independentAim}; run={run}; maps={maps.Length}; combinations={combinations.Length}; elapsed={ms:F2}ms; bytes={bytes}; digest={sink:R}");
                    }
                }
            }
        }

        private sealed class IndependentAimCalculator : OsuDifficultyCalculator
        {
            public IndependentAimCalculator(IRulesetInfo ruleset, IWorkingBeatmap working) : base(ruleset, working) { }
            protected override osu.Game.Rulesets.Difficulty.Skills.Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
            {
                var result = base.CreateSkills(beatmap, mods);
                result[1] = new osu.Game.Rulesets.Osu.Difficulty.Skills.Aim(mods, false, !WorkingBeatmap.BeatmapInfo.GetOnlineStatus().BypassesMappingAntiAbuse());
                return result;
            }
        }

        private static IBeatmap prepared(DifficultyCalculator calculator)
            => (IBeatmap)typeof(DifficultyCalculator).GetProperty("CalculatedBeatmap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(calculator)!;

        private static void assertEqual(DifficultyAttributes expected, DifficultyAttributes actual)
        {
            foreach (var property in typeof(OsuDifficultyAttributes).GetProperties())
            {
                if (property.PropertyType == typeof(double))
                    Assert.That(BitConverter.DoubleToInt64Bits((double)property.GetValue(actual)!), Is.EqualTo(BitConverter.DoubleToInt64Bits((double)property.GetValue(expected)!)), property.Name);
                else if (property.PropertyType == typeof(int))
                    Assert.That(property.GetValue(actual), Is.EqualTo(property.GetValue(expected)), property.Name);
            }
            Assert.That(actual.Mods.Select(m => m.Acronym), Is.EqualTo(expected.Mods.Select(m => m.Acronym)));
        }
    }
}
