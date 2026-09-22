// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class RelaxAimAntiAbuseTest : DifficultyCalculatorTest
    {
        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        [Test]
        public void StreamLabDtCs10AntiAbuseRegression()
        {
            (OsuDifficultyAttributes noRelax, OsuDifficultyAttributes relax, OsuPerformanceAttributes performance) = calculateDtCs10("5738687");

            Assert.That(noRelax.StarRating, Is.EqualTo(25.344259392896205).Within(CHECK_PRECISION));
            Assert.That(relax.StarRating, Is.EqualTo(16.460693826870777).Within(CHECK_PRECISION));
            Assert.That(relax.AimDifficulty / noRelax.AimDifficulty, Is.LessThan(0.60));
            // The standalone pinned calculator gives 5338.36pp here. The modern
            // point-spam/spaced-stream detector must retain a substantial anti-abuse cut.
            Assert.That(performance.Total, Is.LessThan(5338.36 * 0.40));
        }

        [TestCase("801165", 23.007947306269664, 6070.216342375583)]
        [TestCase("1124896", 15.4792980174585, 2096.118220828363)]
        [TestCase("1341554", 20.122490228193584, 3442.3858091785073)]
        [TestCase("2593923", 20.504812444348993, 5845.769634245016)]
        public void NormalMapDtCs10HybridParity(string name, double expectedStars, double hybridPerfectPp)
        {
            (_, OsuDifficultyAttributes relax, OsuPerformanceAttributes performance) = calculateDtCs10(name);

            Assert.That(relax.StarRating, Is.EqualTo(expectedStars).Within(CHECK_PRECISION));
            Assert.That(performance.Total, Is.EqualTo(hybridPerfectPp).Within(hybridPerfectPp * 0.01));
            Assert.That(relax.MosuRelaxAimDifficulty, Is.GreaterThan(0));
            Assert.That(relax.MosuRelaxSpeedDifficulty, Is.GreaterThan(0));
            Assert.That(relax.MosuRelaxReadingDifficulty, Is.GreaterThan(0));
            Assert.That(relax.RelaxStreamWeight, Is.InRange(0, 1));
            Assert.That(relax.RelaxVerticalAimPressure, Is.InRange(0, 1));
        }

        [TestCase("801165")]
        [TestCase("1124896")]
        [TestCase("1341554")]
        [TestCase("2593923")]
        [TestCase("relax-rate-95-regression")]
        public void LowerCustomRateDoesNotIncreaseRelaxStarsOrPerfectPp(string name)
        {
            (OsuDifficultyAttributes halfRateDifficulty, OsuPerformanceAttributes halfRatePerformance) = calculateAtRate(name, 0.5);
            (OsuDifficultyAttributes threeQuarterRateDifficulty, OsuPerformanceAttributes threeQuarterRatePerformance) = calculateAtRate(name, 0.75);
            (OsuDifficultyAttributes ninetyFivePercentRateDifficulty, OsuPerformanceAttributes ninetyFivePercentRatePerformance) = calculateAtRate(name, 0.95);
            (OsuDifficultyAttributes normalRateDifficulty, OsuPerformanceAttributes normalRatePerformance) = calculateAtRate(name, 1);

            Assert.Multiple(() =>
            {
                Assert.That(halfRateDifficulty.StarRating, Is.LessThanOrEqualTo(threeQuarterRateDifficulty.StarRating));
                Assert.That(threeQuarterRateDifficulty.StarRating, Is.LessThanOrEqualTo(ninetyFivePercentRateDifficulty.StarRating));
                Assert.That(ninetyFivePercentRateDifficulty.StarRating, Is.LessThanOrEqualTo(normalRateDifficulty.StarRating));
                Assert.That(halfRatePerformance.Total, Is.LessThanOrEqualTo(threeQuarterRatePerformance.Total));
                Assert.That(threeQuarterRatePerformance.Total, Is.LessThanOrEqualTo(ninetyFivePercentRatePerformance.Total));
                Assert.That(ninetyFivePercentRatePerformance.Total, Is.LessThanOrEqualTo(normalRatePerformance.Total));
            });
        }

        [Test]
        public void PointSpamRequiresFastLowMovementTransitions()
        {
            Assert.That(Aim.IsRelaxPointSpamTransition(130, 49.99), Is.True);
            Assert.That(Aim.IsRelaxPointSpamTransition(130.01, 49.99), Is.False);
            Assert.That(Aim.IsRelaxPointSpamTransition(100, 50), Is.False);
        }

        [Test]
        public void RelaxPatternWeightsFadeSmoothly()
        {
            Assert.That(Aim.CalculateRelaxPatternSpeedWeight(100), Is.EqualTo(1));
            Assert.That(Aim.CalculateRelaxPatternSpeedWeight(115), Is.InRange(0.49, 0.51));
            Assert.That(Aim.CalculateRelaxPatternSpeedWeight(130), Is.Zero);
            Assert.That(Aim.CalculateRelaxSpacedStreamGeometryWeight(150), Is.EqualTo(1));
            Assert.That(Aim.CalculateRelaxSpacedStreamGeometryWeight(182.5), Is.InRange(0.49, 0.51));
            Assert.That(Aim.CalculateRelaxSpacedStreamGeometryWeight(215), Is.Zero);
        }

        [Test]
        public void PointSpamPenaltyHasGraceAndBoundedMaximum()
        {
            Assert.That(Aim.CalculateRelaxPointSpamPenalty(4), Is.EqualTo(1));
            Assert.That(Aim.CalculateRelaxPointSpamPenalty(8), Is.LessThan(1));
            Assert.That(Aim.CalculateRelaxPointSpamPenalty(16), Is.EqualTo(0.15).Within(1e-12));
            Assert.That(Aim.CalculateRelaxPointSpamPenalty(100), Is.EqualTo(0.15).Within(1e-12));
        }

        [Test]
        public void SpacedStreamRequiresRegularRhythmAndMeaningfulMovement()
        {
            Assert.That(Aim.IsRelaxSpacedStreamTransition(90, 95, 100), Is.True);
            Assert.That(Aim.IsRelaxSpacedStreamTransition(90, 110, 100), Is.False);
            Assert.That(Aim.IsRelaxSpacedStreamTransition(90, 95, 49.99), Is.False);
            Assert.That(Aim.IsRelaxSpacedStreamTransition(90, 95, 50), Is.True);
            Assert.That(Aim.IsRelaxSpacedStreamTransition(90, 95, 215), Is.True);
            Assert.That(Aim.IsRelaxSpacedStreamTransition(90, 95, 215.01), Is.False);
        }

        [Test]
        public void SpacedStreamPenaltyStartsAfterEightTransitions()
        {
            Assert.That(Aim.CalculateRelaxSpacedStreamPenalty(8), Is.EqualTo(1));
            Assert.That(Aim.CalculateRelaxSpacedStreamPenalty(12), Is.LessThan(1));
            Assert.That(Aim.CalculateRelaxSpacedStreamPenalty(24), Is.EqualTo(0.35).Within(1e-12));
            Assert.That(Aim.CalculateRelaxSpacedStreamPenalty(100), Is.EqualTo(0.35).Within(1e-12));
        }

        [Test]
        public void IsolatedSpacedStreamMismatchOnlyDecaysEvidence()
        {
            int streak = Aim.CalculateNextRelaxPatternStreak(12, false, false);

            Assert.That(streak, Is.EqualTo(11));
            Assert.That(Aim.CalculateNextRelaxPatternStreak(streak, true, false), Is.EqualTo(12));
        }

        [Test]
        public void TimingBreakResetsSpacedStreamEvidence()
        {
            Assert.That(Aim.CalculateNextRelaxPatternStreak(24, true, true), Is.Zero);
        }

        [Test]
        public void PointSpamMismatchDecaysInsteadOfResettingEvidence()
        {
            Assert.That(Aim.CalculateNextRelaxPatternStreak(4, false, false), Is.EqualTo(3));
        }

        [Test]
        public void LookAheadPenalisesBeginningOfLongPattern()
        {
            int evidence = Aim.CalculateRelaxPatternEvidence(1, 15, 16);

            Assert.That(Aim.CalculateRelaxPointSpamPenalty(evidence), Is.EqualTo(0.15).Within(1e-12));
        }

        [Test]
        public void RankedStatusDoesNotDisableRelaxAntiAbuse()
        {
            (OsuDifficultyAttributes noRelax, OsuDifficultyAttributes relax, _) = calculateDtCs10("5738687", BeatmapOnlineStatus.Ranked);

            Assert.That(relax.AimDifficulty / noRelax.AimDifficulty, Is.LessThan(0.60));
        }

        [TestCase(BeatmapOnlineStatus.Ranked)]
        [TestCase(BeatmapOnlineStatus.Approved)]
        public void RankedStatusDisablesGeneralMappingAntiAbuse(BeatmapOnlineStatus status)
        {
            double unrankedAim = calculateAimAtCircleSize("801165", 2, BeatmapOnlineStatus.Pending);
            double rankedAim = calculateAimAtCircleSize("801165", 2, status);

            Assert.That(rankedAim, Is.GreaterThan(unrankedAim));
        }

        private (OsuDifficultyAttributes NoRelax, OsuDifficultyAttributes Relax, OsuPerformanceAttributes Performance) calculateDtCs10(
            string beatmapName, BeatmapOnlineStatus status = BeatmapOnlineStatus.Pending)
        {
            IWorkingBeatmap working = GetBeatmap(beatmapName);
            ((BeatmapInfo)working.BeatmapInfo).Status = status;
            var difficultyAdjust = new OsuModDifficultyAdjust();
            difficultyAdjust.CircleSize.Value = 10;

            Mod[] noRelaxMods = { new OsuModDoubleTime(), difficultyAdjust };
            Mod[] relaxMods = { new OsuModDoubleTime(), difficultyAdjust, new OsuModMosuRelax() };
            var calculator = new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working);
            var noRelax = (OsuDifficultyAttributes)calculator.Calculate(noRelaxMods);
            var relax = (OsuDifficultyAttributes)calculator.Calculate(relaxMods);
            int totalHits = relax.HitCircleCount + relax.SliderCount + relax.SpinnerCount;
            var perfectScore = new ScoreInfo((BeatmapInfo)working.BeatmapInfo, new OsuRuleset().RulesetInfo)
            {
                Mods = relaxMods,
                MaxCombo = relax.MaxCombo,
                Accuracy = 1,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Great] = totalHits,
                    [HitResult.SliderTailHit] = relax.SliderCount,
                },
            };
            var performance = (OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(perfectScore, relax);

            return (noRelax, relax, performance);
        }

        private double calculateAimAtCircleSize(string beatmapName, float circleSize, BeatmapOnlineStatus status)
        {
            IWorkingBeatmap working = GetBeatmap(beatmapName);
            ((BeatmapInfo)working.BeatmapInfo).Status = status;
            var difficultyAdjust = new OsuModDifficultyAdjust { CircleSize = { Value = circleSize } };

            return ((OsuDifficultyAttributes)new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working)
                                              .Calculate(new Mod[] { difficultyAdjust })).AimDifficulty;
        }

        private (OsuDifficultyAttributes Difficulty, OsuPerformanceAttributes Performance) calculateAtRate(string beatmapName, double rate)
        {
            IWorkingBeatmap working = GetBeatmap(beatmapName);
            Mod[] mods = rate < 1
                ? new Mod[] { new OsuModMosuRelax(), new OsuModHalfTime { SpeedChange = { Value = rate } } }
                : new Mod[] { new OsuModMosuRelax() };

            var difficulty = (OsuDifficultyAttributes)new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working).Calculate(mods);
            int totalHits = difficulty.HitCircleCount + difficulty.SliderCount + difficulty.SpinnerCount;
            var perfectScore = new ScoreInfo((BeatmapInfo)working.BeatmapInfo, new OsuRuleset().RulesetInfo)
            {
                Mods = mods,
                MaxCombo = difficulty.MaxCombo,
                Accuracy = 1,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Great] = totalHits,
                    [HitResult.SliderTailHit] = difficulty.SliderCount,
                },
            };

            var performance = (OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(perfectScore, difficulty);
            return (difficulty, performance);
        }

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) =>
            new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
