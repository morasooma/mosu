using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.OnlinePlay.DailyChallenge;
using osu.Game.Tests.Visual.OnlinePlay;
using osuTK;

namespace osu.Game.Tests.Visual.DailyChallenge
{
    public partial class TestSceneDailyChallengeProposals : OnlinePlayTestScene
    {
        [TestCase(1024, 768)]
        [TestCase(1366, 768)]
        [TestCase(1920, 1080)]
        public void TestListsButtonsAndModeration(int width, int height)
        {
            DailyChallengeProposalsScreen screen = null!;
            bool mutated = false;
            AddStep("configure native API", () =>
            {
                RelativeSizeAxes = Axes.None;
                Size = new Vector2(width, height);
                var api = (DummyAPIAccess)API;
                var fallback = api.HandleRequest;
                api.HandleRequest = request =>
                {
                    switch (request)
                    {
                        case GetDailyChallengeProposalStatusRequest status:
                            status.TriggerSuccess(new DailyChallengeProposalOverview { CanManage = true, CanSubmit = true });
                            return true;
                        case GetDailyChallengeProposalsRequest list:
                            list.TriggerSuccess(new DailyChallengeProposalPage
                            {
                                Total = 2,
                                Items = new[]
                                {
                                    new DailyChallengeProposal { ID = 1, OwnerID = 42, BeatmapID = 100, RulesetID = 0, Revision = 1, Status = "pending", RequiredMods = new[] { new APIMod { Acronym = "HD" } } },
                                    new DailyChallengeProposal { ID = 2, OwnerID = 43, BeatmapID = 101, RulesetID = 0, Revision = 1, Status = "approved" },
                                }
                            });
                            return true;
                        case MutateDailyChallengeProposalRequest mutation:
                            mutated = true;
                            mutation.TriggerSuccess(new DailyChallengeProposalMutation { ID = 1 });
                            return true;
                    }
                    return fallback?.Invoke(request) ?? false;
                };
            });
            AddStep("open proposals", () => LoadScreen(screen = new DailyChallengeProposalsScreen()));
            AddUntilStep("screen current and lists populated", () => screen.IsCurrentScreen() &&
                screen.ChildrenOfType<DrawableRoomPlaylist>().Count() == 2 &&
                screen.ChildrenOfType<DrawableRoomPlaylist>().All(p => p.Items.Count == 2));
            AddStep("finish transforms", () => FinishTransforms(true));
            AddAssert("lists are visible and have useful height", () => screen.ChildrenOfType<DrawableRoomPlaylist>()
                .All(p => p.IsPresent && p.DrawHeight > 80 && p.DrawWidth > 600));
            AddAssert("controls remain within the screen", () => screen.ChildrenOfType<RoundedButton>()
                .Where(b => b.IsPresent).All(b => b.ScreenSpaceDrawQuad.TopLeft.X >= screen.ScreenSpaceDrawQuad.TopLeft.X - 1 &&
                    b.ScreenSpaceDrawQuad.BottomRight.X <= screen.ScreenSpaceDrawQuad.BottomRight.X + 1));
            AddStep("select proposal", () =>
            {
                var list = screen.ChildrenOfType<DrawableRoomPlaylist>().First();
                list.SelectedItem.Value = list.Items.First();
            });
            AddUntilStep("moderation actions enabled", () => screen.ChildrenOfType<RoundedButton>().Any(b => b.Enabled.Value && b.Text.ToString() == "Add to queue"));
            AddStep("approve", () => screen.ChildrenOfType<RoundedButton>().Single(b => b.Text.ToString() == "Add to queue").TriggerClick());
            AddUntilStep("request sent", () => mutated);
        }

        [Test]
        public void TestRevokedModeratorCannotKeepQueueOpen()
        {
            DailyChallengeProposalsScreen screen = null!;
            AddStep("deny authority", () =>
            {
                var api = (DummyAPIAccess)API;
                api.HandleRequest = request =>
                {
                    if (request is not GetDailyChallengeProposalStatusRequest status) return false;
                    status.TriggerSuccess(new DailyChallengeProposalOverview { CanManage = false });
                    return true;
                };
            });
            AddStep("open proposals", () => LoadScreen(screen = new DailyChallengeProposalsScreen()));
            AddUntilStep("screen exits", () => screen.IsLoaded && !screen.IsCurrentScreen());
        }
    }
}
