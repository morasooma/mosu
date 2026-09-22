// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Relax.Realistik;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    /// <summary>
    /// Verifies that the selected Relax PP system switches the difficulty/performance
    /// pipelines between the fork's Mosu/Realistik calculator and vanilla lazer.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class RelaxPpSystemSelectionTest : DifficultyCalculatorTest
    {
        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        private ForkRelaxPpSystem originalSystem;

        [SetUp]
        public void SetUpSystem() => originalSystem = RelaxPpSystemSelection.Current;

        [TearDown]
        public void TearDownSystem() => RelaxPpSystemSelection.Current = originalSystem;

        [Test]
        public void MosuSystemDelegatesToRealistikCalculator()
        {
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;

            (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance) = calculateRelax("801165");

            Assert.Multiple(() =>
            {
                Assert.That(performance.Total, Is.GreaterThan(0));
                // The pinned Mosu skills only run for the fork system.
                Assert.That(difficulty.MosuRelaxAimDifficulty, Is.GreaterThan(0));
            });
        }

        [Test]
        public void VanillaSystemUsesUpstreamRelaxFormula()
        {
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.LazerVanilla;

            (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance) = calculateRelax("801165");

            Assert.Multiple(() =>
            {
                // Upstream lazer relax performance zeroes the tapping components.
                Assert.That(performance.Speed, Is.Zero);
                Assert.That(performance.Accuracy, Is.Zero);
                Assert.That(performance.Aim, Is.GreaterThan(0));
                Assert.That(performance.Total, Is.GreaterThan(0));
                // The pinned Mosu skills are skipped entirely.
                Assert.That(difficulty.MosuRelaxAimDifficulty, Is.Zero);
            });
        }

        [Test]
        public void SystemsProduceDifferentRelaxValues()
        {
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;
            (OsuDifficultyAttributes mosuDifficulty, OsuPerformanceAttributes mosuPerformance) = calculateRelax("801165");

            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.LazerVanilla;
            (OsuDifficultyAttributes vanillaDifficulty, OsuPerformanceAttributes vanillaPerformance) = calculateRelax("801165");

            Assert.Multiple(() =>
            {
                Assert.That(vanillaPerformance.Total, Is.Not.EqualTo(mosuPerformance.Total).Within(1));
                Assert.That(vanillaDifficulty.StarRating, Is.Not.EqualTo(mosuDifficulty.StarRating).Within(0.01));
            });
        }

        [Test]
        public void VanillaRelaxStarsAreNotAntiAbusedOnFarmMap()
        {
            // The fork system cuts relax stars heavily on the point-spam farm map
            // (see RelaxAimAntiAbuseTest); vanilla applies only the flat upstream reductions.
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;
            double forkStars = calculateRelaxDtCs10("5738687").StarRating;

            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.LazerVanilla;
            double vanillaStars = calculateRelaxDtCs10("5738687").StarRating;

            Assert.That(forkStars, Is.EqualTo(16.460693826870777).Within(CHECK_PRECISION));
            Assert.That(vanillaStars, Is.GreaterThan(forkStars));
        }

        [Test]
        public void VanillaSystemAimRegression()
        {
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.LazerVanilla;

            (OsuDifficultyAttributes difficulty, OsuPerformanceAttributes performance) = calculateRelax("801165");

            Assert.Multiple(() =>
            {
                // Pinned so an accidental application of RELAX_AIM_MULTIPLIER (1.40) is caught.
                Assert.That(performance.Aim, Is.EqualTo(115.31861096583869).Within(0.01));
                Assert.That(performance.Total, Is.EqualTo(135.72563660558254).Within(0.01));
                Assert.That(difficulty.StarRating, Is.EqualTo(5.4748744944159631).Within(0.01));
            });
        }

        [Test]
        public void SwitchingBackToForkSystemRestoresRealistikResults()
        {
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;
            OsuPerformanceAttributes first = calculateRelax("801165").Performance;

            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.LazerVanilla;
            calculateRelax("801165");

            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;
            OsuPerformanceAttributes second = calculateRelax("801165").Performance;

            Assert.That(second.Total, Is.EqualTo(first.Total).Within(0.0001));
        }

        [Test]
        public void RelaxPerformanceCacheDistinguishesAccuracyAtSameCombo()
        {
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;
            IWorkingBeatmap working = GetBeatmap("801165");
            Mod[] mods = { new OsuModMosuRelax() };
            var difficulty = (OsuDifficultyAttributes)new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working).Calculate(mods);
            ScoreInfo perfect = createPerfectScore(working, difficulty, mods);
            double perfectPp = ((OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(perfect, difficulty)).Total;

            ScoreInfo imperfect = createPerfectScore(working, difficulty, mods);
            imperfect.Statistics[HitResult.Great]--;
            imperfect.Statistics[HitResult.Ok] = 1;
            double imperfectPp = ((OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(imperfect, difficulty)).Total;

            Assert.That(imperfectPp, Is.LessThan(perfectPp));
        }

        [Test]
        public void RelaxPerformanceRetainsPreparedMapAfterFallbackCacheEviction()
        {
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;
            IWorkingBeatmap working = GetBeatmap("801165");
            Mod[] mods = { new OsuModMosuRelax() };
            var difficulty = (OsuDifficultyAttributes)new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working).Calculate(mods);
            var score = createPerfectScore(working, difficulty, mods);
            double expected = ((OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(score, difficulty)).Total;

            // Preparing the same map with different difficulty settings must stay cheap until PP is
            // requested, while the original attributes retain their already converted map.
            for (int i = 0; i < 40; i++)
            {
                var variedMods = new Mod[]
                {
                    new OsuModMosuRelax(),
                    new OsuModDifficultyAdjust { CircleSize = { Value = 4 + i * 0.05f } },
                };
                var variedAttributes = new OsuDifficultyAttributes { Mods = variedMods };
                ManagedRealistikRelaxCalculator.Prepare(working.Beatmap, variedMods, variedAttributes);
            }

            double actual = ((OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(score, difficulty)).Total;
            Assert.That(actual, Is.EqualTo(expected).Within(0.0001));
        }

        private (OsuDifficultyAttributes Difficulty, OsuPerformanceAttributes Performance) calculateRelax(string beatmapName)
            => calculateRelax(beatmapName, Array.Empty<Mod>());

        private (OsuDifficultyAttributes Difficulty, OsuPerformanceAttributes Performance) calculateRelax(string beatmapName, Mod[] extraMods)
        {
            IWorkingBeatmap working = GetBeatmap(beatmapName);

            Mod[] mods = new Mod[extraMods.Length + 1];
            extraMods.CopyTo(mods, 0);
            mods[^1] = new OsuModMosuRelax();

            var difficulty = (OsuDifficultyAttributes)new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working).Calculate(mods);
            var performance = (OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(createPerfectScore(working, difficulty, mods), difficulty);

            return (difficulty, performance);
        }

        private OsuDifficultyAttributes calculateRelaxDtCs10(string beatmapName)
        {
            var difficultyAdjust = new OsuModDifficultyAdjust { CircleSize = { Value = 10 } };
            return calculateRelax(beatmapName, new Mod[] { new OsuModDoubleTime(), difficultyAdjust }).Difficulty;
        }

        private static ScoreInfo createPerfectScore(IWorkingBeatmap working, OsuDifficultyAttributes difficulty, Mod[] mods)
        {
            int totalHits = difficulty.HitCircleCount + difficulty.SliderCount + difficulty.SpinnerCount;

            return new ScoreInfo((BeatmapInfo)working.BeatmapInfo, new OsuRuleset().RulesetInfo)
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
        }

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) =>
            new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
