// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using NUnit.Framework;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Online.API;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class LeaderboardManagerTest
    {
        [Test]
        public void TestNotFoundMeansBeatmapUnavailable()
        {
            var exception = new APIException("Beatmap not found", null, HttpStatusCode.NotFound);

            Assert.That(LeaderboardManager.GetFailureState(exception), Is.EqualTo(LeaderboardFailState.BeatmapUnavailable));
        }

        [Test]
        public void TestOtherFailureMeansNetworkFailure()
        {
            var exception = new APIException("Service unavailable", null, HttpStatusCode.ServiceUnavailable);

            Assert.That(LeaderboardManager.GetFailureState(exception), Is.EqualTo(LeaderboardFailState.NetworkFailure));
        }

        [Test]
        public void TestStableRelaxLeaderboardUsesBaseRulesetAndRelaxBit()
        {
            RulesetInfo relaxRuleset = new OsuRuleset().RulesetInfo.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID);

            (RulesetInfo ruleset, LegacyMods specialModeMod) = LeaderboardManager.NormaliseStableRuleset(relaxRuleset);

            Assert.Multiple(() =>
            {
                Assert.That(ruleset.ShortName, Is.EqualTo(RulesetInfo.OSU_MODE_SHORTNAME));
                Assert.That(ruleset.OnlineID, Is.Zero);
                Assert.That(specialModeMod, Is.EqualTo(LegacyMods.Relax));
            });
        }

        [Test]
        public void TestStableNormalLeaderboardDoesNotAddSpecialMod()
        {
            RulesetInfo normalRuleset = new OsuRuleset().RulesetInfo;

            (RulesetInfo ruleset, LegacyMods specialModeMod) = LeaderboardManager.NormaliseStableRuleset(normalRuleset);

            Assert.Multiple(() =>
            {
                Assert.That(ruleset, Is.SameAs(normalRuleset));
                Assert.That(specialModeMod, Is.EqualTo(LegacyMods.None));
            });
        }
    }
}
