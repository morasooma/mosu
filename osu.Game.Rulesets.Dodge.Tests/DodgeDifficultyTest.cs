// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Difficulty;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Mods;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeDifficultyTest
    {
        [Test]
        public void TestEmptyMapHasNoDifficulty()
        {
            DodgeDifficultyAttributes attributes = DodgeAutoplayDifficultyEvaluator.Calculate(new Beatmap<DodgeHitObject>());

            Assert.That(attributes.StarRating, Is.Zero);
        }

        [Test]
        public void TestDenseEmitterIsHarderThanSingleSafeBullet()
        {
            var safeBullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(0, 32),
                EndPosition = new Vector2(512, 32),
            };
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 16,
                SpreadAngle = 360,
            };

            DodgeDifficultyAttributes easy = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(safeBullet));
            DodgeDifficultyAttributes hard = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(emitter));

            Assert.That(easy.StarRating, Is.GreaterThan(0));
            Assert.That(hard.StarRating, Is.GreaterThan(easy.StarRating));
            Assert.That(hard.ProjectileRate, Is.GreaterThan(easy.ProjectileRate));
            Assert.That(hard.PeakActiveProjectiles, Is.GreaterThan(easy.PeakActiveProjectiles));
        }

        [Test]
        public void TestRulesetDifficultyCalculatorUsesAutoplayAnalysis()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(0, 192),
                EndPosition = new Vector2(512, 192),
            });
            var ruleset = new DodgeRuleset();
            var calculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));

            var attributes = (DodgeDifficultyAttributes)calculator.Calculate();

            Assert.That(attributes.StarRating, Is.GreaterThan(0));
            Assert.That(attributes.ProjectileRate, Is.GreaterThan(0));
            Assert.That(attributes.MaxCombo, Is.EqualTo(1));
        }

        [Test]
        public void TestTimedDifficultyDoesNotCalculateAutoplayRouteDuringGameplay()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(
                new DodgeBullet
                {
                    StartTime = 1000,
                    Duration = 1000,
                    Position = new Vector2(0, 64),
                    EndPosition = new Vector2(512, 64),
                },
                new DodgeEmitter
                {
                    StartTime = 2000,
                    Duration = 1000,
                    Position = new Vector2(256, 192),
                    AimPosition = new Vector2(512, 192),
                    BulletCount = 8,
                    SpreadAngle = 180,
                });
            beatmap.BeatmapInfo.StarRating = 4;
            var ruleset = new DodgeRuleset();
            var calculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));

            var timed = calculator.CalculateTimed();

            Assert.That(timed, Has.Count.EqualTo(2));
            Assert.That(calculator.AutoplayCalculationCount, Is.Zero);
            Assert.That(timed[0].Attributes.StarRating, Is.LessThan(timed[1].Attributes.StarRating));
            Assert.That(timed[1].Attributes.StarRating, Is.EqualTo(4));
            Assert.That(timed[0].Attributes.MaxCombo, Is.EqualTo(1));
            Assert.That(timed[1].Attributes.MaxCombo, Is.EqualTo(2));
        }

        [Test]
        public void TestLargerPlayerIncreasesDifficulty()
        {
            Beatmap<DodgeHitObject> smallPlayerMap = createBeatmap(createEmitter());
            Beatmap<DodgeHitObject> largePlayerMap = createBeatmap(createEmitter());
            DodgeBeatmapSettings.SetPlayerSize(smallPlayerMap.Difficulty, 8);
            DodgeBeatmapSettings.SetPlayerSize(largePlayerMap.Difficulty, 32);

            double smallPlayerStars = DodgeAutoplayDifficultyEvaluator.Calculate(smallPlayerMap).StarRating;
            double largePlayerStars = DodgeAutoplayDifficultyEvaluator.Calculate(largePlayerMap).StarRating;

            Assert.That(largePlayerStars, Is.GreaterThan(smallPlayerStars));

            static DodgeEmitter createEmitter() => new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 12,
                SpreadAngle = 360,
            };
        }

        [Test]
        public void TestRateModsChangeDifficulty()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 12,
                SpreadAngle = 180,
            });
            var ruleset = new DodgeRuleset();
            var calculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));

            double normal = calculator.Calculate().StarRating;
            double halfTime = calculator.Calculate(new[] { new DodgeModHalfTime() }).StarRating;
            double doubleTime = calculator.Calculate(new[] { new DodgeModDoubleTime() }).StarRating;

            Assert.That(halfTime, Is.LessThan(normal));
            Assert.That(doubleTime, Is.GreaterThan(normal));
            Assert.That(doubleTime / normal, Is.EqualTo(Math.Pow(1.5, 1.35)).Within(0.001));
        }

        [Test]
        public void TestStaticBpmChangesDifficulty()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 12,
                SpreadAngle = 180,
            });
            beatmap.BeatmapInfo.BPM = 120;
            var ruleset = new DodgeRuleset();
            var calculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));
            var staticBpm = new DodgeModMosuStaticBpm();
            staticBpm.TargetBpm.Value = 180;

            double normal = calculator.Calculate().StarRating;
            double adjusted = calculator.Calculate(new[] { staticBpm }).StarRating;

            Assert.That(adjusted, Is.GreaterThan(normal));
            Assert.That(adjusted / normal, Is.EqualTo(Math.Pow(1.5, 1.35)).Within(0.001));
        }

        [Test]
        public void TestContinuousImpossibleSectionCountsOnce()
        {
            var arena = new DodgeArenaChange
            {
                StartTime = 0,
                Duration = 0,
                TargetPosition = new Vector2(240, 176),
                TargetSize = new Vector2(32),
            };
            var blockingBullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 4000,
                Position = new Vector2(256, 192),
                EndPosition = new Vector2(256, 192),
            };

            DodgeDifficultyAttributes attributes = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(arena, blockingBullet));

            Assert.That(attributes.AutoplayTeleports, Is.EqualTo(1));
            Assert.That(attributes.StarRating, Is.LessThan(7));
        }

        private static Beatmap<DodgeHitObject> createBeatmap(params DodgeHitObject[] hitObjects)
        {
            var beatmap = new Beatmap<DodgeHitObject>();
            beatmap.HitObjects.AddRange(hitObjects);
            return beatmap;
        }
    }
}
