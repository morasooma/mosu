// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class GetScoresRequestTest
    {
        [Test]
        public void TestUsesBeatmapOnlineIdByDefault()
        {
            var request = new TestGetScoresRequest(new BeatmapInfo { OnlineID = 123 });

            Assert.That(request.RequestTarget, Is.EqualTo("beatmaps/123/scores"));
        }

        [Test]
        public void TestUsesResolvedBeatmapOnlineIdWhenProvided()
        {
            var request = new TestGetScoresRequest(new BeatmapInfo { OnlineID = 123 }, 456);

            Assert.That(request.RequestTarget, Is.EqualTo("beatmaps/456/scores"));
        }

        private class TestGetScoresRequest : GetScoresRequest
        {
            public string RequestTarget => Target;

            public TestGetScoresRequest(BeatmapInfo beatmap, int? beatmapOnlineId = null)
                : base(beatmap, new OsuRuleset().RulesetInfo, beatmapOnlineId: beatmapOnlineId)
            {
            }
        }
    }
}
