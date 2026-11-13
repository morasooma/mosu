// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using System.Linq;
using MosuPpRxCs;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class RxMissWeightingTest
    {
        [TestCase(-0.1, 0.25)]
        [TestCase(0, 0.25)]
        [TestCase(0.5, 0.625)]
        [TestCase(1, 1)]
        [TestCase(10, 1)]
        public void TestDistance(double distance, double expected)
            => Assert.That(RxMissWeighting.DistanceFactor(distance), Is.EqualTo(expected).Within(1e-12));

        [Test]
        public void TestEasyAndNearMissesCostLess()
        {
            var weights = RxMissWeighting.Weights(new[] { 0.09, 1.0, 1.0, 1.0 },
                new[] { true, true, true, false }, new[] { 0.0, 0.0, 1.0, 5.0 });
            Assert.That(weights, Is.EqualTo(new[] { 0.0225, 0.25, 1.0, 0.0 }).Within(1e-12));
            Assert.That(weights.Sum(), Is.LessThan(3));
        }

        [Test]
        public void TestRejectsMismatchedEvidence()
            => Assert.Throws<ArgumentException>(() => RxMissWeighting.Weights(new[] { 1.0 }, Array.Empty<bool>(), Array.Empty<double>()));

        [Test]
        public void TestRejectsNonFiniteDistance()
            => Assert.Throws<ArgumentOutOfRangeException>(() => RxMissWeighting.DistanceFactor(double.NaN));

        [Test]
        public void TestNativePenaltyUsesWeightWithoutChangingAccuracy()
        {
            var attributes = new RxDifficultyAttributes
            {
                CircleCount = 500, MaxCombo = 500, AimStrainForPp = 4, SpeedStrainForPp = 4,
                ReadingStrainForPerformance = 4, AimDifficultStrainCount = 100,
                SpeedDifficultStrainCount = 100, ReadingDifficultNoteCount = 100, Od = 10, Ar = 9, Cs = 4,
            };
            var score = new RxScoreState { Count300 = 498, Misses = 2, MaxCombo = 200 };
            var flat = RxPerformanceCalculator.Calculate(attributes, score, RxMods.Relax);
            score.MissLoad = 2;
            var full = RxPerformanceCalculator.Calculate(attributes, score, RxMods.Relax);
            Assert.That(full.Pp, Is.EqualTo(flat.Pp));
            score.MissLoad = 0.25;
            var weighted = RxPerformanceCalculator.Calculate(attributes, score, RxMods.Relax);
            Assert.That(weighted.EffectiveMissCount, Is.EqualTo(0.25));
            Assert.That(weighted.Pp, Is.GreaterThan(flat.Pp));
            Assert.That(weighted.PpAccuracy, Is.EqualTo(flat.PpAccuracy));
            Assert.That(score.Misses, Is.EqualTo(2));
            attributes.SliderCount = 100;
            score.MaxCombo = 1;
            score.MissLoad = null;
            double comboFlat = RxPerformanceCalculator.Calculate(attributes, score, RxMods.Relax).EffectiveMissCount;
            score.MissLoad = 0.25;
            Assert.That(RxPerformanceCalculator.Calculate(attributes, score, RxMods.Relax).EffectiveMissCount,
                Is.EqualTo(comboFlat - 2 + 0.25));
        }

        [Test]
        public void TestNativeBridgePreservesWeightedMisses()
        {
            string text = "osu file format v14\n[General]\nMode:0\n[Difficulty]\nCircleSize:4\nOverallDifficulty:10\nApproachRate:9\n[TimingPoints]\n0,500,4,2,0,100,1,0\n[HitObjects]\n"
                + string.Join("\n", Enumerable.Range(0, 100).Select(i => $"{(i % 2 == 0 ? 100 : 400)},192,{1000 + i * 125},1,0,0:0:0:0:"));
            var beatmap = OsuBeatmapParser.Parse(text);
            var score = new RxScoreState { Count300 = 98, Misses = 2, MaxCombo = 50 };
            var flat = MosuRxCalculator.Calculate(new RxCalculationRequest { Beatmap = beatmap, Score = score });
            score.MissLoad = 0.25;
            var weighted = MosuRxCalculator.Calculate(new RxCalculationRequest { Beatmap = beatmap, Score = score });
            Assert.That(weighted.EffectiveMissCount, Is.EqualTo(0.25));
            Assert.That(weighted.Pp, Is.GreaterThan(flat.Pp));
            Assert.That(weighted.PpAccuracy, Is.EqualTo(flat.PpAccuracy));
        }

        [Test]
        public void TestHeavyPenaltyUsesSameWeight()
        {
            Assert.That(RxHeavyMissPenalty.Multiplier(25, 2), Is.EqualTo(1));
            Assert.That(RxHeavyMissPenalty.Multiplier(25, 25), Is.EqualTo(RxHeavyMissPenalty.Multiplier(25)));
        }

        [Test]
        public void TestNormalClientHasNoLabEntryPoint()
            => Assert.That(typeof(RxOwnerRules).Assembly.GetType("osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax.MosuPpRelaxCalculator")!
                .GetMethod("CalculatePerformanceForLab"), Is.Null);
    }
}
