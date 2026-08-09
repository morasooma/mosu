// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ScoreOnlineIdentityTest
    {
        [Test]
        public void TestMatchingScoreContext()
        {
            var first = createScore(100, 200, 300);
            var second = createScore(100, 200, 300);

            Assert.That(first.MatchesOnlineIDAndContext(second), Is.True);
        }

        [Test]
        public void TestSameScoreIdFromDifferentBeatmapDoesNotMatch()
        {
            var first = createScore(100, 200, 300);
            var second = createScore(100, 201, 300);

            Assert.That(first.MatchesOnlineIDAndContext(second), Is.False);
        }

        [Test]
        public void TestSameScoreAndBeatmapIdsFromDifferentDifficultyDoesNotMatch()
        {
            var first = createScore(100, 200, 300, beatmapHash: "difficulty-a");
            var second = createScore(100, 200, 300, beatmapHash: "difficulty-b");

            Assert.That(first.MatchesOnlineIDAndContext(second), Is.False);
        }

        [Test]
        public void TestSameScoreIdFromDifferentUserDoesNotMatch()
        {
            var first = createScore(100, 200, 300);
            var second = createScore(100, 200, 301);

            Assert.That(first.MatchesOnlineIDAndContext(second), Is.False);
        }

        [Test]
        public void TestRelaxApiRulesetMatchesStoredRelaxScore()
        {
            var baseRuleset = new OsuRuleset().RulesetInfo;
            var apiScore = createScore(100, 200, 300, baseRuleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));
            var storedScore = createScore(100, 200, 300, baseRuleset);
            storedScore.Mods = [new OsuModRelax()];

            Assert.That(apiScore.MatchesOnlineIDAndContext(storedScore), Is.True);
        }

        [Test]
        public void TestRelaxApiRulesetDoesNotMatchStoredStandardScore()
        {
            var baseRuleset = new OsuRuleset().RulesetInfo;
            var apiScore = createScore(100, 200, 300, baseRuleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));
            var storedScore = createScore(100, 200, 300, baseRuleset);

            Assert.That(apiScore.MatchesOnlineIDAndContext(storedScore), Is.False);
        }

        private static ScoreInfo createScore(long scoreId, int beatmapId, int userId, RulesetInfo? ruleset = null, string beatmapHash = "") => new ScoreInfo
        {
            OnlineID = scoreId,
            BeatmapInfo = new BeatmapInfo
            {
                OnlineID = beatmapId,
                MD5Hash = beatmapHash,
            },
            Ruleset = ruleset ?? new OsuRuleset().RulesetInfo,
            User = new APIUser
            {
                Id = userId,
                Username = $"user-{userId}"
            }
        };
    }
}
