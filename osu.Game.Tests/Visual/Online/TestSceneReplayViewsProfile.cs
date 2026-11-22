using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections.Ranks;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneReplayViewsProfile : OsuTestScene
    {
        [Test]
        public void TestRealReplayRowsShowCountsAndLoadNextPage()
        {
            PaginatedScoreContainer page = null!;
            int requests = 0;
            AddStep("create replay top", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetUserScoresRequest scores)
                        return false;
                    requests++;
                    scores.TriggerSuccess(Enumerable.Range(requests == 1 ? 0 : 5, requests == 1 ? 6 : 2)
                        .Select(index => new SoloScoreInfo
                        {
                            ID = (ulong)(500 + index), ReplayViews = 20 - index, PP = 999, RulesetID = 0,
                            Rank = ScoreRank.A, Accuracy = 0.98, EndedAt = DateTimeOffset.UtcNow,
                            Beatmap = new APIBeatmap
                            {
                                DifficultyName = "Hard", Status = BeatmapOnlineStatus.Ranked,
                                BeatmapSet = new APIBeatmapSet { Title = $"Replay {index}", Artist = "Artist" }
                            }
                        }).ToList());
                    return true;
                };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink)) },
                    Child = page = new PaginatedScoreContainer(ScoreType.MostViewed,
                        new Bindable<UserProfileData?>(new UserProfileData(new APIUser { Id = 42 }, new OsuRuleset().RulesetInfo, null)), "Most viewed replays")
                    {
                        Width = 0.95f,
                    }
                };
            });
            AddUntilStep("five real rows admitted", () => page.ChildrenOfType<DrawableProfileReplayScore>().Count() == 5);
            AddAssert("individual counts displayed", () => page.ChildrenOfType<DrawableProfileReplayScore>()
                .SelectMany(row => row.ChildrenOfType<SpriteText>()).Any(text => text.Text.ToString() == "20"));
            AddAssert("view labels replace pp", () => page.ChildrenOfType<DrawableProfileReplayScore>()
                .SelectMany(row => row.ChildrenOfType<SpriteText>()).Count(text => text.Text.ToString() == "Views") == 5);
            AddAssert("metric within row bounds", () => page.ChildrenOfType<DrawableProfileReplayScore>().All(row =>
                row.ChildrenOfType<SpriteText>().Where(text => text.Text.ToString() == "Views").All(text =>
                    text.ScreenSpaceDrawQuad.AABBFloat.Right <= row.ScreenSpaceDrawQuad.AABBFloat.Right)));
            AddStep("load next page", () => page.ChildrenOfType<ShowMoreButton>().Single().TriggerClick());
            AddUntilStep("seven real rows admitted", () => page.ChildrenOfType<DrawableProfileReplayScore>().Count() == 7);
            AddAssert("two list requests", () => requests == 2);
            AddAssert("no more pages", () => page.ChildrenOfType<ShowMoreButton>().Single().Alpha == 0);
        }
    }
}
