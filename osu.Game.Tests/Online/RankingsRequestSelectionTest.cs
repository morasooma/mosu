// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays;
using osu.Game.Overlays.Rankings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Users;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class RankingsRequestSelectionTest
    {
        private RulesetInfo ruleset = null!;

        [SetUp]
        public void SetUp() => ruleset = new OsuRuleset().RulesetInfo;

        [TestCase(RankingsScope.Performance, UserRankingsType.Performance)]
        [TestCase(RankingsScope.Score, UserRankingsType.Score)]
        public void TestThirdPartyLazerUsesStandardRankings(RankingsScope scope, UserRankingsType expectedType)
        {
            var request = RankingsOverlay.CreateScopedRequest(scope, ruleset, CountryCode.Unknown, null, useStandardLazerRankings: true);

            Assert.That(request, Is.TypeOf<GetUserRankingsRequest>());
            Assert.That(((GetUserRankingsRequest)request!).Type, Is.EqualTo(expectedType));
        }

        [TestCase(RankingsScope.Performance, WeeklyRankingsType.Performance)]
        [TestCase(RankingsScope.Score, WeeklyRankingsType.Score)]
        [TestCase(RankingsScope.TopScorePp, WeeklyRankingsType.BestScorePp)]
        public void TestMorasoomaKeepsWeeklyRankings(RankingsScope scope, WeeklyRankingsType expectedType)
        {
            var request = RankingsOverlay.CreateScopedRequest(scope, ruleset, CountryCode.Unknown, null, useStandardLazerRankings: false);

            Assert.That(request, Is.TypeOf<GetWeeklyRankingsRequest>());
            Assert.That(((GetWeeklyRankingsRequest)request!).Type, Is.EqualTo(expectedType));
        }

        [Test]
        public void TestThirdPartyDoesNotCallMorasoomaBestScorePpEndpoint()
        {
            var request = RankingsOverlay.CreateScopedRequest(RankingsScope.TopScorePp, ruleset, CountryCode.Unknown, null, useStandardLazerRankings: true);

            Assert.That(request, Is.Null);
        }

        [Test]
        public void TestThirdPartyProfileIsAlwaysRefetched()
        {
            Assert.That(UserProfileOverlay.ShouldReuseDisplayedProfile(isThirdPartyServer: true, sameUser: true, sameRuleset: true, sameVariant: true), Is.False);
        }

        [Test]
        public void TestMorasoomaProfileReuseIsUnchanged()
        {
            Assert.That(UserProfileOverlay.ShouldReuseDisplayedProfile(isThirdPartyServer: false, sameUser: true, sameRuleset: true, sameVariant: true), Is.True);
        }
    }
}
