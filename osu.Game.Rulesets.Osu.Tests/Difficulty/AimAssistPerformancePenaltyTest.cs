using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class AimAssistPerformancePenaltyTest
    {
        [TestCase(BeatmapOnlineStatus.Ranked)]
        [TestCase(BeatmapOnlineStatus.Approved)]
        [TestCase(BeatmapOnlineStatus.Pending)]
        [TestCase(BeatmapOnlineStatus.Graveyard)]
        [TestCase(BeatmapOnlineStatus.Loved)]
        [TestCase(BeatmapOnlineStatus.None)]
        public void MosuStrengthReducesAimAndTotalOnEveryMapStatus(BeatmapOnlineStatus status)
        {
            var attributes = new OsuDifficultyAttributes
            {
                AimDifficulty = 3,
                SpeedDifficulty = 2,
                AimDifficultStrainCount = 100,
                SpeedDifficultStrainCount = 100,
                SpeedNoteCount = 100,
                HitCircleCount = 100,
                MaxCombo = 100,
            };
            var score = new ScoreInfo
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Status = status,
                    Difficulty = new BeatmapDifficulty { OverallDifficulty = 8, DrainRate = 5 },
                },
                Accuracy = 1,
                MaxCombo = 100,
                Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = 100 },
                Mods = Array.Empty<Mod>(),
            };
            var calculator = new OsuPerformanceCalculator();
            var baseline = (OsuPerformanceAttributes)calculator.Calculate(score, attributes)!;
            double previous = baseline.Total;
            Assert.That(baseline.Aim, Is.GreaterThan(0));

            foreach (float strength in new[] { 0f, 0.25f, 0.5f, 0.7f })
            {
                // Exercise the same APIMod settings conversion used by the performance service.
                var assist = new OsuModMosuAimAssist { Strength = { Value = strength } };
                score.Mods = new[] { new APIMod(assist).ToMod(new OsuRuleset()) };
                var result = (OsuPerformanceAttributes)calculator.Calculate(score, attributes)!;
                Assert.That(result.Aim, Is.EqualTo(baseline.Aim * Math.Pow(1 - (double)strength, 1.25)).Within(1e-9));
                Assert.That(result.Speed, Is.EqualTo(baseline.Speed));
                Assert.That(result.Accuracy, Is.EqualTo(baseline.Accuracy));
                if (strength == 0)
                    Assert.That(result.Total, Is.EqualTo(baseline.Total));
                else
                    Assert.That(result.Total, Is.LessThan(previous));
                previous = result.Total;
            }
        }
    }
}
