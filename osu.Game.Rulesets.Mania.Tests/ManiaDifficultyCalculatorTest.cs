// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests
{
    public class ManiaDifficultyCalculatorTest : DifficultyCalculatorTest
    {
        protected override string ResourceAssembly => "osu.Game.Rulesets.Mania.Tests";

        [TestCase(2.3493769750220914d, 242, "diffcalc-test")]
        public void Test(double expectedStarRating, int expectedMaxCombo, string name)
            => base.Test(expectedStarRating, expectedMaxCombo, name);

        [TestCase(2.797245912537965d, 242, "diffcalc-test")]
        public void TestClockRateAdjusted(double expectedStarRating, int expectedMaxCombo, string name)
            => Test(expectedStarRating, expectedMaxCombo, name, new ManiaModDoubleTime());

        [Test]
        public void TestOrdinaryDoubleTimeMapDoesNotTriggerVibroGate()
        {
            var calculator = (ManiaDifficultyCalculator)CreateDifficultyCalculator(GetBeatmap("diffcalc-test"));
            calculator.Calculate(new[] { new ManiaModDoubleTime() });

            Assert.Multiple(() =>
            {
                Assert.That(calculator.VibroCandidateObjectRatio, Is.LessThanOrEqualTo(0.40));
                Assert.That(calculator.LongestVibroDuration, Is.LessThan(1500));
            });
        }

        [Test]
        public void TestSlowdownCannotIncreaseGeneratedMicroLnPp()
        {
            var working = new FlatWorkingBeatmap(createMicroLnVibroBeatmap(26));
            var halfTime = new ManiaModHalfTime { SpeedChange = { Value = 0.8 } };

            double noModPp = calculatePerfectPp(working, System.Array.Empty<Mod>());
            double halfTimePp = calculatePerfectPp(working, new Mod[] { halfTime });

            Assert.That(halfTimePp, Is.LessThan(noModPp));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestGeneratedVibroDifficultyIsMonotonicAcrossSlowdownRange(bool useChordVibro)
        {
            var working = new FlatWorkingBeatmap(useChordVibro ? createChordVibroBeatmap(40) : createMicroLnVibroBeatmap(26));
            (double previousStars, double previousPp) = calculatePerfectMetrics(working, System.Array.Empty<Mod>());
            double[] speeds = { 0.95, 0.90, 0.85, 0.80, 0.75, 0.70, 0.65, 0.60, 0.55, 0.50 };

            foreach (double speed in speeds)
            {
                var halfTime = new ManiaModHalfTime { SpeedChange = { Value = speed } };
                (double currentStars, double currentPp) = calculatePerfectMetrics(working, new Mod[] { halfTime });

                TestContext.Out.WriteLine($"{speed:F2}x: {currentStars:F6}*, {currentPp:F6}pp");
                Assert.That(currentStars, Is.LessThanOrEqualTo(previousStars), $"SR increased at {speed:F2}x");
                Assert.That(currentPp, Is.LessThanOrEqualTo(previousPp), $"PP increased at {speed:F2}x");
                previousStars = currentStars;
                previousPp = currentPp;
            }
        }

        [TestCase(BeatmapOnlineStatus.Ranked)]
        [TestCase(BeatmapOnlineStatus.Approved)]
        public void TestRankedMapBypassesVibroPenalty(BeatmapOnlineStatus status)
        {
            var unrankedWorking = new FlatWorkingBeatmap(createMicroLnVibroBeatmap(26));
            var rankedWorking = new FlatWorkingBeatmap(createMicroLnVibroBeatmap(26));
            ((BeatmapInfo)rankedWorking.BeatmapInfo).Status = status;

            var ruleset = new ManiaRuleset();
            var unranked = (ManiaDifficultyAttributes)new ManiaDifficultyCalculator(ruleset.RulesetInfo, unrankedWorking).Calculate();
            var ranked = (ManiaDifficultyAttributes)new ManiaDifficultyCalculator(ruleset.RulesetInfo, rankedWorking).Calculate();

            Assert.That(ranked.StarRating, Is.GreaterThan(unranked.StarRating));
        }

        [TestCase(BeatmapOnlineStatus.Ranked)]
        [TestCase(BeatmapOnlineStatus.Approved)]
        public void TestRankedMapBypassesLowOverallDifficultyPenalty(BeatmapOnlineStatus status)
        {
            var ruleset = new ManiaRuleset();
            var unrankedWorking = new FlatWorkingBeatmap(createOrdinaryBeatmap(0));
            var rankedWorking = new FlatWorkingBeatmap(createOrdinaryBeatmap(0));
            ((BeatmapInfo)unrankedWorking.BeatmapInfo).Ruleset = ruleset.RulesetInfo;
            ((BeatmapInfo)rankedWorking.BeatmapInfo).Ruleset = ruleset.RulesetInfo;
            ((BeatmapInfo)rankedWorking.BeatmapInfo).Status = status;

            var unranked = (ManiaDifficultyAttributes)new ManiaDifficultyCalculator(ruleset.RulesetInfo, unrankedWorking).Calculate();
            var ranked = (ManiaDifficultyAttributes)new ManiaDifficultyCalculator(ruleset.RulesetInfo, rankedWorking).Calculate();

            Assert.That(ranked.StarRating, Is.GreaterThan(unranked.StarRating));
        }

        [Test]
        public void TestSeparatelyMappedSlowMicroLnDifficultyStillTriggersVibroGate()
        {
            var calculator = new ManiaDifficultyCalculator(
                new ManiaRuleset().RulesetInfo,
                new FlatWorkingBeatmap(createMicroLnVibroBeatmap(32.5)));

            calculator.Calculate();

            Assert.Multiple(() =>
            {
                Assert.That(calculator.VibroCandidateObjectRatio, Is.GreaterThan(0.90));
                Assert.That(calculator.LongestVibroDuration, Is.GreaterThan(1500));
            });
        }

        private static ManiaBeatmap createMicroLnVibroBeatmap(double rowInterval)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                Difficulty = new BeatmapDifficulty { OverallDifficulty = 5 }
            };

            for (int i = 0; i < 500; i++)
            {
                beatmap.HitObjects.Add(new HoldNote
                {
                    StartTime = i * rowInterval,
                    Duration = rowInterval * 2,
                    Column = i % 2,
                });
            }

            return beatmap;
        }

        private static ManiaBeatmap createChordVibroBeatmap(double rowInterval)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                Difficulty = new BeatmapDifficulty { OverallDifficulty = 5 }
            };

            for (int row = 0; row < 160; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    beatmap.HitObjects.Add(new Note
                    {
                        StartTime = row * rowInterval,
                        Column = column,
                    });
                }
            }

            return beatmap;
        }

        private static ManiaBeatmap createOrdinaryBeatmap(float overallDifficulty)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                Difficulty = new BeatmapDifficulty { OverallDifficulty = overallDifficulty }
            };

            for (int i = 0; i < 100; i++)
            {
                beatmap.HitObjects.Add(new Note
                {
                    StartTime = i * 200,
                    Column = i % 4,
                });
            }

            return beatmap;
        }

        private static double calculatePerfectPp(IWorkingBeatmap working, Mod[] mods)
            => calculatePerfectMetrics(working, mods).Pp;

        private static (double Stars, double Pp) calculatePerfectMetrics(IWorkingBeatmap working, Mod[] mods)
        {
            var ruleset = new ManiaRuleset();
            var attributes = (ManiaDifficultyAttributes)new ManiaDifficultyCalculator(ruleset.RulesetInfo, working).Calculate(mods);
            var score = new ScoreInfo((BeatmapInfo)working.BeatmapInfo, ruleset.RulesetInfo)
            {
                Mods = mods,
                Accuracy = 1,
                MaxCombo = attributes.MaxCombo,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = attributes.MaxCombo,
                },
            };

            return (attributes.StarRating, new ManiaPerformanceCalculator().Calculate(score, attributes).Total);
        }

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new ManiaDifficultyCalculator(new ManiaRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new ManiaRuleset();
    }
}
