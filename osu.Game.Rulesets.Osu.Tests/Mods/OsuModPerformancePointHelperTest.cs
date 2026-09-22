// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    [NonParallelizable]
    public class OsuModPerformancePointHelperTest
    {
        private bool previousStableProtocol;
        private bool previousThirdPartyServer;

        [SetUp]
        public void SetUp()
        {
            previousStableProtocol = MosuServerEnvironment.UsesStableProtocol;
            previousThirdPartyServer = MosuServerEnvironment.IsThirdPartyServer;
            MosuServerEnvironment.UsesStableProtocol = false;
            MosuServerEnvironment.IsThirdPartyServer = false;
        }

        [TearDown]
        public void TearDown()
        {
            MosuServerEnvironment.UsesStableProtocol = previousStableProtocol;
            MosuServerEnvironment.IsThirdPartyServer = previousThirdPartyServer;
        }

        [Test]
        public void TestMosuUsesServerRankedModPolicy()
        {
            var beatmapInfo = new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty());

            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[] { new OsuModClassic() }), Is.True);
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[] { new OsuModApproachDifferent() }), Is.True);
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[]
            {
                new OsuModAudioEffects
                {
                    Preset = { Value = AudioEffectPreset.Radio },
                    Intensity = { Value = 0.8 },
                    Pitch = { Value = -3 },
                    AffectHitSounds = { Value = true },
                },
            }), Is.True);
        }

        [Test]
        public void TestThirdPartyServerStillUsesModRankedFlag()
        {
            MosuServerEnvironment.IsThirdPartyServer = true;
            var beatmapInfo = new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty());

            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[] { new OsuModClassic() }), Is.False);
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[]
            {
                new OsuModAudioEffects
                {
                    Preset = { Value = AudioEffectPreset.Rotation },
                    Intensity = { Value = 1 },
                    Pitch = { Value = 12 },
                    AffectHitSounds = { Value = true },
                },
            }), Is.True);
        }

        [Test]
        public void TestClassicIsRankedForStableProtocol()
        {
            MosuServerEnvironment.UsesStableProtocol = true;
            var beatmapInfo = new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty());

            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[] { new OsuModClassic() }), Is.True);
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[] { new OsuModClassic(), new OsuModDifficultyAdjust() }), Is.False);
        }

        [Test]
        public void TestFlashlightSettingsMakeScoreUnranked()
        {
            var beatmapInfo = new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty());

            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[] { new OsuModFlashlight() }), Is.True);
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo,
                new Mod[] { new OsuModFlashlight { SizeMultiplier = { Value = 2 } } }), Is.False);
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo,
                new Mod[] { new OsuModFlashlight { ComboBasedSize = { Value = false } } }), Is.False);
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo,
                new Mod[] { new OsuModFlashlight { FollowDelay = { Value = 240 } } }), Is.False);
        }

        [TestCase(0, false)]
        [TestCase(1.9f, false)]
        [TestCase(2, true)]
        [TestCase(10, true)]
        [TestCase(10.1f, false)]
        public void TestDifficultyAdjustCircleSizeRankedRange(float circleSize, bool expected)
        {
            var beatmapInfo = new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty
            {
                CircleSize = 5,
                OverallDifficulty = 8,
            });

            var difficultyAdjust = new OsuModDifficultyAdjust
            {
                CircleSize = { Value = circleSize },
            };

            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new[] { difficultyAdjust }), Is.EqualTo(expected));
        }
    }
}
