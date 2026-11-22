using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.IO.Network;
using osu.Game.Rulesets.Mania;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Overlays.Profile.Sections.Ranks;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class ReplayViewsProfileTest
    {
        [TestCase(true, true, 1)]
        [TestCase(true, false, 1)]
        [TestCase(false, true, 0)]
        [TestCase(false, false, 0)]
        public void TestExactlyOneMostViewedListOnMosuAndNoneOnOfficial(bool thirdParty, bool dedicatedSection, int count)
        {
            var user = new APIUser { ProfileOrder = dedicatedSection ? new[] { "historical", "replay_views" } : new[] { "historical" } };
            var sections = UserProfileOverlay.CreateSections(user, thirdParty);
            try
            {
                Assert.That(sections.Count(section => section.Identifier == "replay_views"), Is.EqualTo(thirdParty && dedicatedSection ? 1 : 0));
                Assert.That(sections.SelectMany(section => section.ChildrenOfType<DrawableProfileReplayScore>()).Count(), Is.Zero);
                int lists = sections.Sum(section => section.ChildrenOfType<PaginatedScoreContainer>().Count(page =>
                    typeof(PaginatedScoreContainer).GetField("type", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                        .GetValue(page) is ScoreType.MostViewed));
                Assert.That(lists, Is.EqualTo(count));
            }
            finally
            {
                foreach (var section in sections)
                    section.Dispose();
            }
        }

        [Test]
        public void TestViewCountDeserializesButIsNotSubmittedWithScores()
        {
            var score = JsonConvert.DeserializeObject<SoloScoreInfo>("{\"replay_views\":12345678901}")!;
            Assert.That(score.ReplayViews, Is.EqualTo(12345678901L));
            Assert.That(JsonConvert.SerializeObject(score), Does.Not.Contain("replay_views"));
            using var page = new ReplayPageProbe();
            using var drawable = page.Create(score);
            Assert.That(drawable, Is.TypeOf<DrawableProfileReplayScore>());
        }

        [Test]
        public void TestMostViewedRequestTargetsExistingScoreListRoute()
        {
            var request = new ReplayRequestProbe();
            Assert.That(request.Route, Is.EqualTo("users/42/scores/mostviewed"));
        }

        [Test]
        public void TestMostViewedRequestOmitsModeAndKeyVariant()
        {
            using var api = new DummyAPIAccess();
            var request = new ReplayRequestProbe();
            request.AttachAPI(api);
            using var web = request.Create();
            var parameters = (System.Collections.Generic.List<(string key, string value)>)typeof(WebRequest)
                .GetField("queryParameters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(web)!;
            Assert.That(parameters.Any(parameter => parameter.key is "mode" or "variant"), Is.False);
        }

        private class ReplayRequestProbe : GetUserScoresRequest
        {
            public ReplayRequestProbe() : base(42, ScoreType.MostViewed, new PaginationParameters(5), new ManiaRuleset().RulesetInfo, "4K") { }
            public string Route => Target;
            public WebRequest Create() => CreateWebRequest();
        }

        private partial class ReplayPageProbe : PaginatedScoreContainer
        {
            public ReplayPageProbe() : base(ScoreType.MostViewed, new Bindable<UserProfileData?>(), "test") { }
            public Drawable Create(SoloScoreInfo score) => CreateDrawableItem(score);
        }
    }
}
