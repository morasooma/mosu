// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.JudgementCounter;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeGrazeScoringTest
    {
        [Test]
        public void TestGrazeUsesConfiguredBonusWithoutChangingAccuracy()
        {
            var beatmap = createGrazeBeatmap(250);
            var processor = new DodgeScoreProcessor(new DodgeRuleset());
            processor.ApplyBeatmap(beatmap);

            var graze = new DodgeGrazeHitObject { ScoreValue = 250 };
            processor.ApplyResult(new JudgementResult(graze, graze.CreateJudgement()) { Type = HitResult.SmallBonus });

            DodgeBullet bullet = (DodgeBullet)beatmap.HitObjects[0];
            processor.ApplyResult(new JudgementResult(bullet, bullet.CreateJudgement()) { Type = HitResult.Perfect });

            Assert.Multiple(() =>
            {
                Assert.That(processor.TotalScoreWithoutMods.Value, Is.EqualTo(1_000_250));
                Assert.That(processor.Accuracy.Value, Is.EqualTo(1));
                Assert.That(processor.Statistics[HitResult.SmallBonus], Is.EqualTo(1));
                Assert.That(processor.MaximumStatistics[HitResult.SmallBonus], Is.EqualTo(1));
            });
        }

        [Test]
        public void TestMissedGrazeStillAccountsForProjectile()
        {
            var beatmap = createGrazeBeatmap(250);
            var processor = new DodgeScoreProcessor(new DodgeRuleset());
            processor.ApplyBeatmap(beatmap);

            var graze = new DodgeGrazeHitObject { ScoreValue = 250 };
            processor.ApplyResult(new JudgementResult(graze, graze.CreateJudgement()) { Type = HitResult.IgnoreMiss });

            DodgeBullet bullet = (DodgeBullet)beatmap.HitObjects[0];
            processor.ApplyResult(new JudgementResult(bullet, bullet.CreateJudgement()) { Type = HitResult.Perfect });

            Assert.Multiple(() =>
            {
                Assert.That(processor.TotalScoreWithoutMods.Value, Is.EqualTo(1_000_000));
                Assert.That(processor.Accuracy.Value, Is.EqualTo(1));
                Assert.That(processor.Statistics[HitResult.IgnoreMiss], Is.EqualTo(1));
            });
        }

        [Test]
        public void TestGrazeDoesNotAffectTimingStatistics()
        {
            var graze = new DodgeGrazeHitObject();

            Assert.Multiple(() =>
            {
                Assert.That(graze.HitWindows, Is.SameAs(HitWindows.Empty));
                Assert.That(HitEventExtensions.AffectsUnstableRate(graze, HitResult.SmallBonus), Is.False);
            });
        }

        [Test]
        public void TestEnabledLegacyGrazeReceivesDefaultScore()
        {
            var difficulty = new BeatmapDifficulty
            {
                SliderMultiplier = DodgeBeatmapSettings.GetSliderMultiplier(16),
                SliderTickRate = 1,
            };

            Assert.That(DodgeBeatmapSettings.GetGrazeScore(difficulty), Is.EqualTo(DodgeBeatmapSettings.GRAZE_SCORE_DEFAULT));
        }

        [Test]
        public void TestMovingEmitterReservesGrazeForEveryBurstRay()
        {
            var beatmap = new Beatmap<DodgeHitObject>();
            beatmap.Difficulty.SliderMultiplier = DodgeBeatmapSettings.GetSliderMultiplier(16);
            beatmap.HitObjects.Add(new DodgeEmitter
            {
                BulletCount = 5,
                BurstCount = 4,
            });
            var processor = new DodgeScoreProcessor(new DodgeRuleset());
            processor.ApplyBeatmap(beatmap);

            Assert.That(processor.MaximumStatistics[HitResult.SmallBonus], Is.EqualTo(20));
        }

        [TestCase(HitResult.SmallBonus, true)]
        [TestCase(HitResult.Perfect, false)]
        [TestCase(HitResult.Miss, false)]
        public void TestGrazeCanBeIsolatedInJudgementCounter(HitResult result, bool expectedVisible)
        {
            Assert.That(
                JudgementCounterDisplay.IsResultVisible(JudgementCounterDisplay.DisplayMode.BonusesOnly, result),
                Is.EqualTo(expectedVisible));
        }

        private static Beatmap<DodgeHitObject> createGrazeBeatmap(double grazeScore)
        {
            var beatmap = new Beatmap<DodgeHitObject>();
            beatmap.Difficulty.SliderMultiplier = DodgeBeatmapSettings.GetSliderMultiplier(16);
            beatmap.Difficulty.SliderTickRate = DodgeBeatmapSettings.GetSliderTickRate(grazeScore);
            beatmap.HitObjects.Add(new DodgeBullet());
            return beatmap;
        }
    }
}
