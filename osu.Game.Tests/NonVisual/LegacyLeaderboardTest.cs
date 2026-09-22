// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LegacyLeaderboardTest
    {
        [TestCase(true, false, BeatmapLeaderboardScope.Global)]
        [TestCase(false, true, BeatmapLeaderboardScope.Global)]
        [TestCase(false, false, BeatmapLeaderboardScope.Local)]
        public void TestInitialScope(bool isLoggedIn, bool usesStableProtocol, BeatmapLeaderboardScope expected)
            => Assert.That(LegacyLeaderboard.GetInitialScope(isLoggedIn, usesStableProtocol), Is.EqualTo(expected));

        [Test]
        public void TestRelaxUsesRelaxLeaderboard()
        {
            var ruleset = new RulesetInfo(RulesetInfo.OSU_MODE_SHORTNAME, "osu!", string.Empty, 0);
            RulesetInfo? result = LegacyLeaderboard.GetLeaderboardRuleset(ruleset, [new OsuModRelax()]);

            Assert.That(result?.OnlineID, Is.EqualTo(RulesetInfo.OSU_RELAX_ONLINE_ID));
        }

        [Test]
        public void TestNoSpecialModUsesVanillaLeaderboard()
        {
            var ruleset = new RulesetInfo(RulesetInfo.OSU_MODE_SHORTNAME, "osu!", string.Empty, 0);

            Assert.That(LegacyLeaderboard.GetLeaderboardRuleset(ruleset, []), Is.SameAs(ruleset));
        }
    }
}
