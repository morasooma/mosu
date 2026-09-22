// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class OsuPerformanceCalculatorLiveTest
    {
        private readonly ScoreInfo relaxScore = new ScoreInfo
        {
            Mods = new Mod[] { new OsuModRelax() }
        };

        [Test]
        public void SliderNestedJudgementsDoNotTriggerRelaxPerformanceRecalculation()
        {
            var calculator = new OsuPerformanceCalculator();

            Assert.That(calculator.ShouldCalculateLivePerformance(relaxScore, new SliderHeadCircle(), false), Is.False);
            Assert.That(calculator.ShouldCalculateLivePerformance(relaxScore, new SliderTick(), false), Is.False);
            Assert.That(calculator.ShouldCalculateLivePerformance(relaxScore, new SliderRepeat(new Slider()), false), Is.False);
        }

        [Test]
        public void RelaxPerformanceIsRecalculatedAtSliderCompletion()
        {
            var calculator = new OsuPerformanceCalculator();
            var slider = new Slider();
            var tail = new SliderTailCircle(slider);

            Assert.That(calculator.ShouldCalculateLivePerformance(relaxScore, tail, false), Is.True);

            slider.ClassicSliderBehaviour = true;
            tail.ClassicSliderBehaviour = true;

            Assert.That(calculator.ShouldCalculateLivePerformance(relaxScore, tail, false), Is.False);
            Assert.That(calculator.ShouldCalculateLivePerformance(relaxScore, slider, false), Is.True);
        }

        [Test]
        public void NonRelaxPerformanceKeepsExistingLiveBehaviour()
        {
            var calculator = new OsuPerformanceCalculator();
            var score = new ScoreInfo { Mods = System.Array.Empty<Mod>() };

            Assert.That(calculator.ShouldCalculateLivePerformance(score, new SliderTick(), false), Is.True);
        }

        [Test]
        [NonParallelizable]
        public void OnlyRealistikRelaxRequestsBackgroundLiveCalculation()
        {
            var calculator = new OsuPerformanceCalculator();
            var nonRelaxScore = new ScoreInfo { Mods = System.Array.Empty<Mod>() };
            ForkRelaxPpSystem previousSystem = RelaxPpSystemSelection.Current;

            try
            {
                RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;
                Assert.That(calculator.RequiresBackgroundLivePerformanceCalculation(relaxScore), Is.True);
                Assert.That(calculator.RequiresBackgroundLivePerformanceCalculation(nonRelaxScore), Is.False);

                RelaxPpSystemSelection.Current = ForkRelaxPpSystem.LazerVanilla;
                Assert.That(calculator.RequiresBackgroundLivePerformanceCalculation(relaxScore), Is.False);
            }
            finally
            {
                RelaxPpSystemSelection.Current = previousSystem;
            }
        }
    }
}
