// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Dodge.Difficulty;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgePerformanceTest
    {
        private readonly DodgePerformanceCalculator calculator = new DodgePerformanceCalculator();

        [Test]
        public void TestRulesetProvidesPerformanceCalculator()
        {
            PerformanceCalculator? performanceCalculator = new DodgeRuleset().CreatePerformanceCalculator();

            Assert.That(performanceCalculator, Is.TypeOf<DodgePerformanceCalculator>());
        }

        [Test]
        public void TestDeadAirLikeFullComboCalibration()
        {
            DodgeDifficultyAttributes attributes = createAttributes(
                starRating: 4.269,
                movement: 3.475,
                path: 2.731,
                reading: 1.238,
                difficultSections: 31.42,
                relevantPatterns: 228,
                maxCombo: 228);
            DodgePerformanceAttributes performance = calculate(createScore(228, 0, 228), attributes);

            Assert.That(performance.Total, Is.EqualTo(231).Within(1));
        }

        [Test]
        public void TestMissesReducePerformanceMonotonically()
        {
            DodgeDifficultyAttributes attributes = createAttributes();

            double fullCombo = calculate(createScore(100, 0, 100), attributes).Total;
            double oneMiss = calculate(createScore(99, 1, 99), attributes).Total;
            double fiveMisses = calculate(createScore(95, 5, 50), attributes).Total;

            Assert.That(fullCombo, Is.GreaterThan(oneMiss));
            Assert.That(oneMiss, Is.GreaterThan(fiveMisses));
        }

        [Test]
        public void TestDeadAirElevenMissesRetainMeaningfulPerformance()
        {
            DodgeDifficultyAttributes attributes = createAttributes(
                starRating: 4.269,
                movement: 3.475,
                path: 2.731,
                reading: 1.238,
                difficultSections: 31.42,
                relevantPatterns: 228,
                maxCombo: 518);
            double fullCombo = calculate(createScore(518, 0, 518), attributes).Total;
            double elevenMisses = calculate(createScore(507, 11, 448), attributes).Total;

            Assert.Multiple(() =>
            {
                Assert.That(elevenMisses, Is.EqualTo(122).Within(2));
                Assert.That(elevenMisses / fullCombo, Is.InRange(0.5, 0.55));
            });
        }

        [Test]
        public void TestQuietWaterShortDifficultyCalibration()
        {
            DodgeDifficultyAttributes attributes = createAttributes(
                starRating: 4.353,
                movement: 3.243,
                path: 3.237,
                reading: 0.928,
                difficultSections: 3.68,
                relevantPatterns: 50,
                effectivePatterns: 25.79,
                maxCombo: 348);
            DodgePerformanceAttributes performance = calculate(createScore(348, 0, 348), attributes);

            Assert.Multiple(() =>
            {
                Assert.That(performance.Total, Is.EqualTo(116).Within(1));
                Assert.That(performance.LengthFactor, Is.EqualTo(Math.Sqrt(25.79 / 100)).Within(0.0001));
            });
        }

        [Test]
        public void TestEffectivePatternLengthRewardsSustainedPerformance()
        {
            DodgeDifficultyAttributes shortAttributes = createAttributes(
                difficultSections: 3,
                relevantPatterns: 50,
                effectivePatterns: 25);
            DodgeDifficultyAttributes fullLengthAttributes = createAttributes(
                difficultSections: 3,
                relevantPatterns: 200,
                effectivePatterns: 100);

            DodgePerformanceAttributes shortPerformance = calculate(createScore(100, 0, 100), shortAttributes);
            DodgePerformanceAttributes fullLengthPerformance = calculate(createScore(100, 0, 100), fullLengthAttributes);

            Assert.Multiple(() =>
            {
                Assert.That(shortPerformance.LengthFactor, Is.EqualTo(0.5).Within(0.0001));
                Assert.That(fullLengthPerformance.LengthFactor, Is.EqualTo(1).Within(0.0001));
                Assert.That(shortPerformance.Total / fullLengthPerformance.Total, Is.EqualTo(0.5).Within(0.0001));
            });
        }

        [Test]
        public void TestGrazeDoesNotAffectPerformance()
        {
            DodgeDifficultyAttributes attributes = createAttributes();
            ScoreInfo withoutGraze = createScore(100, 0, 100);
            ScoreInfo withGraze = createScore(100, 0, 100);
            withGraze.Statistics[HitResult.SmallBonus] = 1000;

            Assert.That(
                calculate(withGraze, attributes).Total,
                Is.EqualTo(calculate(withoutGraze, attributes).Total).Within(0.0001));
        }

        [Test]
        public void TestPerfectPaddingCannotHideMiss()
        {
            DodgeDifficultyAttributes shortAttributes = createAttributes(relevantPatterns: 10, maxCombo: 10);
            DodgeDifficultyAttributes paddedAttributes = createAttributes(relevantPatterns: 10, maxCombo: 100);

            double shortMap = calculate(createScore(9, 1, 9), shortAttributes).Total;
            double paddedMap = calculate(createScore(99, 1, 99), paddedAttributes).Total;

            Assert.That(paddedMap, Is.EqualTo(shortMap).Within(0.0001));
        }

        [Test]
        public void TestEnduranceBonusIsPositiveAndCapped()
        {
            DodgeDifficultyAttributes shortAttributes = createAttributes(difficultSections: 1);
            DodgeDifficultyAttributes sustainedAttributes = createAttributes(difficultSections: 1_000_000);

            DodgePerformanceAttributes shortMap = calculate(createScore(100, 0, 100), shortAttributes);
            DodgePerformanceAttributes sustainedMap = calculate(createScore(100, 0, 100), sustainedAttributes);

            Assert.Multiple(() =>
            {
                Assert.That(sustainedMap.Total, Is.GreaterThan(shortMap.Total));
                Assert.That(sustainedMap.Total / shortMap.Total, Is.LessThan(1.2));
                Assert.That(sustainedMap.EnduranceBonus, Is.LessThan(1.2));
            });
        }

        [Test]
        public void TestSkillComponentsAddToTotal()
        {
            DodgePerformanceAttributes performance = calculate(createScore(100, 0, 100), createAttributes());

            Assert.That(
                performance.Movement + performance.Path + performance.Reading,
                Is.EqualTo(performance.Total).Within(0.0001));
        }

        [Test]
        public void TestNoFailPenalisesMissedPlayButNotFullCombo()
        {
            DodgeDifficultyAttributes attributes = createAttributes();
            ScoreInfo normalFullCombo = createScore(100, 0, 100);
            ScoreInfo noFailFullCombo = createScore(100, 0, 100, new DodgeModNoFail());
            ScoreInfo normalMiss = createScore(99, 1, 99);
            ScoreInfo noFailMiss = createScore(99, 1, 99, new DodgeModNoFail());

            Assert.Multiple(() =>
            {
                Assert.That(calculate(noFailFullCombo, attributes).Total,
                    Is.EqualTo(calculate(normalFullCombo, attributes).Total).Within(0.0001));
                Assert.That(calculate(noFailMiss, attributes).Total,
                    Is.LessThan(calculate(normalMiss, attributes).Total));
            });
        }

        private DodgePerformanceAttributes calculate(ScoreInfo score, DodgeDifficultyAttributes attributes)
            => (DodgePerformanceAttributes)calculator.Calculate(score, attributes);

        private static ScoreInfo createScore(int perfectCount, int missCount, int maxCombo, params Mod[] mods)
            => new ScoreInfo
            {
                MaxCombo = maxCombo,
                Mods = mods,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = perfectCount,
                    [HitResult.Miss] = missCount,
                },
            };

        private static DodgeDifficultyAttributes createAttributes(
            double starRating = 4.5,
            double movement = 3.5,
            double path = 3,
            double reading = 2.5,
            double difficultSections = 20,
            int relevantPatterns = 100,
            double effectivePatterns = 0,
            int maxCombo = 100)
            => new DodgeDifficultyAttributes
            {
                StarRating = starRating,
                MovementDifficulty = movement,
                PathDifficulty = path,
                ReadingDifficulty = reading,
                DifficultSectionCount = difficultSections,
                RelevantPatternCount = relevantPatterns,
                EffectiveReadingPatternCount = effectivePatterns,
                MaxCombo = maxCombo,
            };
    }
}
