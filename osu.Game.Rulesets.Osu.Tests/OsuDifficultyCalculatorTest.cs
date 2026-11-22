// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed;
using osu.Game.Rulesets.Osu.Difficulty.Relax.Realistik;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class OsuDifficultyCalculatorTest : DifficultyCalculatorTest
    {
        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        [TestCase(6.5243170265483581d, 239, "diffcalc-test")]
        [TestCase(1.3041207716275136d, 54, "zero-length-sliders")]
        [TestCase(0.40867325147697559d, 4, "very-fast-slider")]
        [TestCase(0.87058175794353554d, 6, "nan-slider")]
        [TestCase(6.3059767387139756d, 2359, "801165")] // real world test
        public void Test(double expectedStarRating, int expectedMaxCombo, string name)
            => base.Test(expectedStarRating, expectedMaxCombo, name);

        [TestCase(9.4677607900646308d, 239, "diffcalc-test")]
        [TestCase(1.6553117101716419d, 54, "zero-length-sliders")]
        [TestCase(0.53588473186572561d, 4, "very-fast-slider")]
        public void TestClockRateAdjusted(double expectedStarRating, int expectedMaxCombo, string name)
            => Test(expectedStarRating, expectedMaxCombo, name, new OsuModDoubleTime());

        [TestCase(6.5243170265483581d, 239, "diffcalc-test")]
        [TestCase(1.3041207716275136d, 54, "zero-length-sliders")]
        [TestCase(0.40867325147697559d, 4, "very-fast-slider")]
        public void TestClassicMod(double expectedStarRating, int expectedMaxCombo, string name)
            => Test(expectedStarRating, expectedMaxCombo, name, new OsuModClassic());

        [Test]
        public void TestTimedExtended()
        {
            var beatmap = GetBeatmap("diffcalc-test");

            Dictionary<double, (double StarRating, int MaxCombo)> timedAttributes = CreateDifficultyCalculator(beatmap)
                                                                                    .CalculateTimed()
                                                                                    .ToDictionary(a => a.Time, a => (a.Attributes.StarRating, a.Attributes.MaxCombo));

            Assert.That(timedAttributes[500].MaxCombo, Is.EqualTo(1));
            Assert.That(timedAttributes[500].StarRating, Is.EqualTo(0).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[1000].MaxCombo, Is.EqualTo(2));
            Assert.That(timedAttributes[1000].StarRating, Is.EqualTo(0.16948857823852109).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[1500].MaxCombo, Is.EqualTo(3));
            Assert.That(timedAttributes[1500].StarRating, Is.EqualTo(0.26606949612932967).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[2000].MaxCombo, Is.EqualTo(4));
            Assert.That(timedAttributes[2000].StarRating, Is.EqualTo(0.35775631754708426).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[2500].MaxCombo, Is.EqualTo(5));
            Assert.That(timedAttributes[2500].StarRating, Is.EqualTo(0.56319343599556226).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[17000].MaxCombo, Is.EqualTo(62));
            Assert.That(timedAttributes[17000].StarRating, Is.EqualTo(5.6685465406782036).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[18000].MaxCombo, Is.EqualTo(63));
            Assert.That(timedAttributes[18000].StarRating, Is.EqualTo(5.7272669362777702).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[18125].MaxCombo, Is.EqualTo(64));
            Assert.That(timedAttributes[18125].StarRating, Is.EqualTo(5.7347762555961532).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[61500].MaxCombo, Is.EqualTo(160));
            Assert.That(timedAttributes[61500].StarRating, Is.EqualTo(6.5240565852553738).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[70000].MaxCombo, Is.EqualTo(176));
            Assert.That(timedAttributes[70000].StarRating, Is.EqualTo(6.5240609777925842).Within(CHECK_PRECISION));
            Assert.That(timedAttributes[74600].MaxCombo, Is.EqualTo(195));
            Assert.That(timedAttributes[74600].StarRating, Is.EqualTo(6.5240617806983261).Within(CHECK_PRECISION));
        }

        [TestCase("diffcalc-test")]
        [TestCase("801165")]
        public void TestTimedPrefixStatisticsMatchReference(string name)
        {
            IWorkingBeatmap working = GetBeatmap(name);
            IBeatmap playable = working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo);
            var timed = CreateDifficultyCalculator(working).CalculateTimed();

            for (int i = 0; i < timed.Count; i++)
            {
                var prefix = new Beatmap
                {
                    BeatmapInfo = playable.BeatmapInfo,
                    Difficulty = playable.Difficulty,
                    HitObjects = playable.HitObjects.Take(i + 1).ToList(),
                    Breaks = playable.Breaks,
                };
                var actual = (OsuDifficultyAttributes)timed[i].Attributes;
                var expectedScore = new OsuLegacyScoreSimulator().Simulate(working, prefix);
                double expectedNestedScore = osu.Game.Rulesets.Osu.Difficulty.Utils.LegacyScoreUtils.CalculateNestedScorePerObject(prefix, i + 1);

                Assert.That(actual.MaxCombo, Is.EqualTo(prefix.GetMaxCombo()), $"prefix {i}");
                Assert.That(actual.NestedScorePerObject, Is.EqualTo(expectedNestedScore), $"prefix {i}");
                Assert.That(actual.MaximumLegacyComboScore, Is.EqualTo(expectedScore.ComboScore), $"prefix {i}");
            }
        }

        [Test]
        public void TestTimedCalculationAllocationOn801165()
        {
            IWorkingBeatmap working = GetBeatmap("801165");
            // Warm JIT/resource setup before measuring timed-prefix cost.
            CreateDifficultyCalculator(working).CalculateTimed();
            long before = GC.GetAllocatedBytesForCurrentThread();
            CreateDifficultyCalculator(working).CalculateTimed();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Progress.WriteLine($"TIMED_801165_ALLOC bytes={allocated}");
            Assert.That(allocated, Is.LessThan(25 * 1024 * 1024), "Timed calculations must not recreate prefix-sized sorting and score graphs for every object.");
        }

        [TestCase("none")]
        [TestCase("DT")]
        [TestCase("HR")]
        [TestCase("MRX")]
        public void TestEveryTimedPrefixMatchesNonIncrementalReference(string mod)
        {
            Mod[] mods = mod switch
            {
                "DT" => new Mod[] { new OsuModDoubleTime() },
                "HR" => new Mod[] { new OsuModHardRock() },
                "MRX" => new Mod[] { new OsuModMosuRelax() },
                _ => Array.Empty<Mod>(),
            };
            var working = GetBeatmap("diffcalc-test");
            var expected = new NonIncrementalCalculator(working).CalculateTimed(mods);
            var calculator = CreateDifficultyCalculator(working);
            var actual = calculator.CalculateTimed(mods);
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int i = 0; i < actual.Count; i++)
                Assert.That(actual[i].Attributes, Is.EqualTo(expected[i].Attributes).UsingPropertiesComparer(), $"prefix {i}, {mod}");
            Assert.That(calculator.Calculate(mods), Is.EqualTo(CreateDifficultyCalculator(working).Calculate(mods)).UsingPropertiesComparer());
        }

        [TestCase("diffcalc-test", "none")]
        [TestCase("801165", "none")]
        [TestCase("diffcalc-test", "DT")]
        [TestCase("diffcalc-test", "HR")]
        [TestCase("diffcalc-test", "RX")]
        [TestCase("801165", "RX")]
        [TestCase("diffcalc-test", "MRX")]
        [TestCase("801165", "MRX")]
        public void TestStarOnlyAttributesMatchFullCalculation(string name, string mod)
        {
            Mod[] mods = mod switch
            {
                "DT" => new Mod[] { new OsuModDoubleTime() },
                "HR" => new Mod[] { new OsuModHardRock() },
                "RX" => new Mod[] { new OsuModRelax() },
                "MRX" => new Mod[] { new OsuModMosuRelax() },
                _ => Array.Empty<Mod>(),
            };
            var working = GetBeatmap(name);
            var full = (OsuDifficultyAttributes)CreateDifficultyCalculator(working).Calculate(mods);
            var previewCalculator = CreateDifficultyCalculator(working);
            previewCalculator.PreparePerformanceCalculation = false;
            var preview = (OsuDifficultyAttributes)previewCalculator.Calculate(mods);

            Assert.That(preview, Is.EqualTo(full).UsingPropertiesComparer(), $"all difficulty attributes, {name}/{mod}");
            Assert.That(preview.StarRating, Is.EqualTo(full.StarRating));
            Assert.That(preview.MaxCombo, Is.EqualTo(full.MaxCombo));
            Assert.That(ManagedRealistikRelaxCalculator.GetPreparedBeatmap(preview), Is.Null,
                "star-only requests must not prepare the independent Relax PP representation");
        }

        private class NonIncrementalCalculator : OsuDifficultyCalculator
        {
            public NonIncrementalCalculator(IWorkingBeatmap working) : base(new OsuRuleset().RulesetInfo, working) { }
            protected override void BeginTimedCalculation(IBeatmap beatmap, Mod[] mods) { }
            protected override void OnTimedPrefixAdded(IBeatmap prefixBeatmap, HitObject addedObject) { }
            protected override void EndTimedCalculation() { }
        }

        [Test]
        public void TestTimedRelaxPerformanceUsesSinglePreparedMap()
        {
            IWorkingBeatmap working = GetBeatmap("diffcalc-test");
            Mod[] mods = { new OsuModMosuRelax() };
            var attributes = CreateDifficultyCalculator(working).CalculateTimed(mods);
            var first = (OsuDifficultyAttributes)attributes.First().Attributes;
            var last = (OsuDifficultyAttributes)attributes.Last().Attributes;

            Assert.That(ManagedRealistikRelaxCalculator.GetPreparedBeatmap(first),
                Is.SameAs(ManagedRealistikRelaxCalculator.GetPreparedBeatmap(last)));

            var score = new ScoreInfo((BeatmapInfo)working.BeatmapInfo, new OsuRuleset().RulesetInfo)
            {
                Mods = mods,
                MaxCombo = last.MaxCombo,
                Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = last.HitCircleCount + last.SliderCount + last.SpinnerCount },
            };

            Assert.That(new OsuPerformanceCalculator().Calculate(score, last).Total, Is.GreaterThan(0));
        }

        [TestCase(239, "diffcalc-test")]
        [TestCase(54, "zero-length-sliders")]
        [TestCase(4, "very-fast-slider")]
        public void TestOffsetChanges(int expectedMaxCombo, string name)
        {
            const double offset_iterations = 400;
            var beatmap = GetBeatmap(name);

            var expectedAttributes = CreateDifficultyCalculator(beatmap).Calculate();

            for (int i = 0; i < offset_iterations; i++)
            {
                foreach (var beatmapHitObject in beatmap.Beatmap.HitObjects)
                    beatmapHitObject.StartTime++;

                var calc = CreateDifficultyCalculator(beatmap);

                var attributes = calc.Calculate();
                var timedAttributes = calc.CalculateTimed();

                Assert.That(attributes, Is.EqualTo(expectedAttributes).UsingPropertiesComparer());
                Assert.That(timedAttributes.Last().Attributes, Is.EqualTo(expectedAttributes).UsingPropertiesComparer());
            }
        }

        [Test]
        public void TestRhythmEvaluationDoesNotAllocatePerObject()
        {
            var working = GetBeatmap("diffcalc-test");
            var calculator = new ConstructionProbeCalculator(working, _ => { });
            var playable = working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo);
            var objects = calculator.CreateObjects(playable).ToArray();
            double sum = 0;
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var obj in objects)
                    sum += RhythmEvaluator.EvaluateDifficultyOf(obj);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int pass = 0; pass < 10; pass++)
            {
                foreach (var obj in objects)
                    sum += RhythmEvaluator.EvaluateDifficultyOf(obj);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Progress.WriteLine($"RHYTHM_ALLOC evaluations={objects.Length * 10} bytes={allocated}");
            Assert.That(double.IsFinite(sum), Is.True);
            Assert.That(allocated, Is.LessThan(1024), "bounded rhythm history must not allocate a list and islands for each object");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCancellationDuringDifficultyObjectConstruction(bool timed)
        {
            using var cancellation = new CancellationTokenSource();
            var calculator = new ConstructionProbeCalculator(GetBeatmap("diffcalc-test"), reads =>
            {
                if (reads == 16)
                    cancellation.Cancel();
            });

            Assert.Throws<OperationCanceledException>(() =>
            {
                if (timed)
                    calculator.CalculateTimed(cancellation.Token);
                else
                    calculator.Calculate(cancellation.Token);
            });

            // Cancellation happens inside construction, not before entering the calculator.
            // Merely throwing at the later skill loop would still read the complete map.
            Assert.That(calculator.LegacyOverrideCalled, Is.True);
            Assert.That(calculator.ObjectReads, Is.EqualTo(16));
            Assert.That(calculator.AvailableObjects, Is.GreaterThan(16));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestOriginalConstructionOverrideIsUsed(bool timed)
        {
            var working = GetBeatmap("diffcalc-test");
            var expected = CreateDifficultyCalculator(working).Calculate();
            var calculator = new ConstructionProbeCalculator(working, _ => { });
            var actual = timed ? calculator.CalculateTimed().Last().Attributes : calculator.Calculate();

            Assert.That(calculator.LegacyOverrideCalled, Is.True);
            Assert.That(calculator.ObjectReads, Is.EqualTo(2 * (calculator.AvailableObjects - 1)));
            Assert.That(actual, Is.EqualTo(expected).UsingPropertiesComparer());
        }

        private class ConstructionProbeCalculator : OsuDifficultyCalculator
        {
            private readonly Action<int> onRead;
            public bool LegacyOverrideCalled { get; private set; }
            public int ObjectReads { get; private set; }
            public int AvailableObjects { get; private set; }

            public ConstructionProbeCalculator(IWorkingBeatmap working, Action<int> onRead)
                : base(new OsuRuleset().RulesetInfo, working)
            {
                this.onRead = onRead;
            }

            public IEnumerable<DifficultyHitObject> CreateObjects(IBeatmap beatmap) => base.CreateDifficultyHitObjects(beatmap, Array.Empty<Mod>());

            // Deliberately override only the original extension point used by existing rulesets.
            protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
            {
                LegacyOverrideCalled = true;
                AvailableObjects = beatmap.HitObjects.Count;
                return base.CreateDifficultyHitObjects(new ConstructionProbeBeatmap(beatmap.HitObjects, () => onRead(++ObjectReads)), mods);
            }
        }

        private class ConstructionProbeBeatmap : Beatmap, IBeatmap
        {
            private readonly IReadOnlyList<HitObject> objects;

            public ConstructionProbeBeatmap(IReadOnlyList<HitObject> objects, Action onRead)
            {
                this.objects = new CountingHitObjects(objects, onRead);
            }

            IReadOnlyList<HitObject> IBeatmap.HitObjects => objects;
        }

        private class CountingHitObjects : IReadOnlyList<HitObject>
        {
            private readonly IReadOnlyList<HitObject> objects;
            private readonly Action onRead;

            public CountingHitObjects(IReadOnlyList<HitObject> objects, Action onRead)
            {
                this.objects = objects;
                this.onRead = onRead;
            }

            public int Count => objects.Count;

            public HitObject this[int index]
            {
                get
                {
                    onRead();
                    return objects[index];
                }
            }

            public IEnumerator<HitObject> GetEnumerator() => objects.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
