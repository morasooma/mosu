// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
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

        [TestCase(MosuAimAssistAlgorithm.Mosu, true)]
        [TestCase(MosuAimAssistAlgorithm.MapleV2, false)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3, false)]
        public void TestAimAssistAlgorithmAndRelaxRankability(MosuAimAssistAlgorithm algorithm, bool rankedAlone)
        {
            var beatmapInfo = new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty());
            var assist = new OsuModMosuAimAssist { Algorithm = { Value = algorithm } };
            Assert.That(assist.Ranked, Is.EqualTo(rankedAlone));
            foreach (bool thirdParty in new[] { false, true })
            {
                MosuServerEnvironment.IsThirdPartyServer = thirdParty;
                Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[] { assist }), Is.EqualTo(rankedAlone));
                Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new Mod[] { new OsuModRelax() }), Is.True);
                foreach (Mod relax in new Mod[] { new OsuModRelax(), new OsuModMosuRelax() })
                {
                    foreach (Mod[] mods in new[] { new Mod[] { assist, relax }, new Mod[] { relax, assist } })
                    {
                        Assert.That(osu.Game.Utils.ModUtils.CheckCompatibleSet(mods, out _), Is.True);
                        Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, mods), Is.False);
                        Assert.That(ModPerformancePointHelper.ModAwardsPerformancePoints(beatmapInfo, mods, assist), Is.False);
                        Assert.That(ModPerformancePointHelper.ModAwardsPerformancePoints(beatmapInfo, mods, relax), Is.False);
                    }
                    Assert.That(assist.IncompatibleMods.Any(type => type.IsInstanceOfType(relax)), Is.False);
                    Assert.That(relax.IncompatibleMods.Any(type => type.IsInstanceOfType(assist)), Is.False);
                }
            }
        }

        [Test]
        public void TestAlgorithmSwitchImmediatelyUpdatesRankability()
        {
            var beatmapInfo = new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty());
            var assist = new OsuModMosuAimAssist();
            Mod[] mods = { assist };
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, mods), Is.True);
            assist.Algorithm.Value = MosuAimAssistAlgorithm.MapleV2;
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, mods), Is.False);
            assist.Algorithm.Value = MosuAimAssistAlgorithm.MapleV3;
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, mods), Is.False);
            assist.Algorithm.Value = MosuAimAssistAlgorithm.Mosu;
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, mods), Is.True);
        }

        [Test]
        public void TestSegmentRateModsNeverAwardPerformancePoints()
        {
            var beatmapInfo = new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty());
            foreach (bool thirdParty in new[] { false, true })
            {
                MosuServerEnvironment.IsThirdPartyServer = thirdParty;
                foreach (Mod mod in new Mod[] { new OsuModGlitch(), new OsuModFlatline() })
                    Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new[] { mod }), Is.False, mod.Acronym);
            }
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
