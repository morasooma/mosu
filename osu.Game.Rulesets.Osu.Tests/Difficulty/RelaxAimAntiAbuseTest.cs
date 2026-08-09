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
            Assert.That(relax.StarRating, Is.EqualTo(15.668217060378929).Within(CHECK_PRECISION));
            Assert.That(relax.AimDifficulty / noRelax.AimDifficulty, Is.LessThan(0.55));
            Assert.That(performance.Total, Is.EqualTo(4360.372224584713).Within(CHECK_PRECISION));
        }

        [TestCase("801165", 22.996952872144405, 20346.19092465274)]
        [TestCase("1124896", 15.4792980174585, 5707.034477558479)]
        [TestCase("1341554", 20.0665185495263, 11544.495415814439)]
        [TestCase("2593923", 20.505310254297655, 13192.647044291689)]
        public void NormalMapDtCs10RelaxControl(string name, double expectedStars, double expectedPerfectPp)
        {
            (_, OsuDifficultyAttributes relax, OsuPerformanceAttributes performance) = calculateDtCs10(name);

            Assert.That(relax.StarRating, Is.EqualTo(expectedStars).Within(CHECK_PRECISION));
            Assert.That(performance.Total, Is.EqualTo(expectedPerfectPp).Within(CHECK_PRECISION));
        }

        [Test]
        public void PointSpamRequiresFastLowMovementTransitions()
        {
            Assert.That(Aim.IsRelaxPointSpamTransition(100, 49.99), Is.True);
            Assert.That(Aim.IsRelaxPointSpamTransition(101, 49.99), Is.False);
            Assert.That(Aim.IsRelaxPointSpamTransition(100, 50), Is.False);
        }

        [Test]
        public void RelaxAimMultiplierIsAimOnlyBalance()
        {
            Assert.That(OsuPerformanceCalculator.RELAX_AIM_MULTIPLIER, Is.EqualTo(1.40));
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

            Assert.That(relax.AimDifficulty / noRelax.AimDifficulty, Is.LessThan(0.55));
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

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) =>
            new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
