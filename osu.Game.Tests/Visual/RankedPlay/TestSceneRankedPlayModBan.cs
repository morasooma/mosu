using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.Rooms;
using osu.Game.Online.RankedPlay;
using osu.Game.Overlays.Mods;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Components;
using osu.Game.Users.Drawables;
using osuTK.Input;

namespace osu.Game.Tests.Visual.RankedPlay
{
    [HeadlessTest]
    public partial class TestSceneRankedPlayModBan : RankedPlayTestScene
    {
        private ModBanScreen screen = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("join room", () => JoinRoom(CreateDefaultRoom(MatchType.RankedPlay)));
            WaitForJoined();
            AddStep("add opponent", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));
            AddStep("enter ban phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.ModBan, state =>
            {
                state.RandomMods = true;
                state.BannableRandomMods = ["HD", "HR", "EZ"];
            }).WaitSafely());
        }

        [Test]
        public void SelectionPreviewConfirmationAndTimer()
        {
            AddStep("show ban panel", () => Add(new BanTestContainer(screen = new ModBanScreen())));
            AddUntilStep("panel loaded", () => screen.IsLoaded);
            AddStep("start timer", () => MultiplayerClient.StartCountdown(new RankedPlayStageCountdown
            {
                Stage = RankedPlayStage.ModBan,
                TimeRemaining = TimeSpan.FromSeconds(20),
            }).WaitSafely());
            AddUntilStep("timer running", () => screen.ChildrenOfType<RankedPlayStageDisplay>().Single()
                .ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().StartsWith("00:1", StringComparison.Ordinal)));
            AddUntilStep("timer above mods", () => screen.ChildrenOfType<RankedPlayStageDisplay>().Single().ScreenSpaceDrawQuad.BottomLeft.Y
                                                   < screen.ChildrenOfType<ModPanel>().First().ScreenSpaceDrawQuad.TopLeft.Y);
            AddStep("select HD", () => clickMod("HD"));
            AddUntilStep("preview shared", () => ((RankedPlayRoomState)MultiplayerClient.ClientRoom!.MatchState!).RandomModBanSelections.GetValueOrDefault(API.LocalUser.Value.Id) == "HD");
            AddAssert("not confirmed", () => ((RankedPlayRoomState)MultiplayerClient.ClientRoom!.MatchState!).RandomModBans.Count == 0);
            AddStep("opponent selects HR", () => MultiplayerClient.SendUserMatchRequest(2, new RankedPlayModBanRequest { Acronym = "HR", Confirm = false }).WaitSafely());
            AddUntilStep("opponent preview on HR", () => Math.Abs(marker("HR").Alpha - 0.45f) < 0.01f);
            AddStep("opponent confirms", () => MultiplayerClient.SendUserMatchRequest(2, new RankedPlayModBanRequest { Acronym = "HR" }).WaitSafely());
            AddUntilStep("opponent lock on HR", () => marker("HR").Alpha == 1);
            AddUntilStep("opponent avatar resolved", () => marker("HR").User?.Id == 2);
            AddAssert("avatar alongside mod", () => marker("HR").ScreenSpaceDrawQuad.TopLeft.X
                > screen.ChildrenOfType<ModPanel>().Single(p => p.Mod.Acronym == "HR").ScreenSpaceDrawQuad.TopRight.X);
            AddStep("switch to EZ", () => clickMod("EZ"));
            AddAssert("only one selection", () => screen.ChildrenOfType<ModPanel>().Count(p => p.Active.Value) == 1);
            AddStep("confirm", () =>
            {
                InputManager.MoveMouseTo(screen.ChildrenOfType<ShearedButton>().Single(b => b.Name == "Confirm Mod Ban"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("EZ locked", () => ((RankedPlayRoomState)MultiplayerClient.ClientRoom!.MatchState!).RandomModBans.GetValueOrDefault(API.LocalUser.Value.Id) == "EZ");
            AddUntilStep("own avatar locked", () => screen.ChildrenOfType<UpdateableAvatar>().Single(a => a.Name == "Player Ban EZ").Alpha == 1);
            AddAssert("selection disabled", () => screen.ChildrenOfType<ModPanel>().All(p => !p.Enabled.Value));
            AddAssert("button disabled", () => !screen.ChildrenOfType<ShearedButton>().Single().Enabled.Value);
        }

        [Test]
        public void SkipBan()
        {
            AddStep("show ban panel", () => Add(new BanTestContainer(screen = new ModBanScreen())));
            AddUntilStep("panel loaded", () => screen.IsLoaded);
            AddStep("skip ban", () => screen.ChildrenOfType<ShearedButton>().Single().TriggerClick());
            AddUntilStep("skip confirmed", () => ((RankedPlayRoomState)MultiplayerClient.ClientRoom!.MatchState!).RandomModBans.ContainsKey(API.LocalUser.Value.Id));
            AddAssert("no ban selected", () => ((RankedPlayRoomState)MultiplayerClient.ClientRoom!.MatchState!).RandomModBans[API.LocalUser.Value.Id] == null);
        }

        [Test]
        public void SpectatorSeesSelectionsButCannotChoose()
        {
            AddStep("show read only panel", () => Add(new BanTestContainer(screen = new ModBanScreen(true))));
            AddUntilStep("panel loaded", () => screen.IsLoaded);
            AddStep("opponent preview", () => MultiplayerClient.SendUserMatchRequest(2, new RankedPlayModBanRequest { Acronym = "HD", Confirm = false }).WaitSafely());
            AddUntilStep("preview displayed", () => Math.Abs(marker("HD").Alpha - 0.45f) < 0.01f);
            AddAssert("mods disabled", () => screen.ChildrenOfType<ModPanel>().All(p => !p.Enabled.Value));
            AddAssert("confirmation hidden", () => screen.ChildrenOfType<ShearedButton>().Single().Alpha == 0);
        }

        private partial class BanTestContainer : Container
        {
            [Cached]
            private readonly RankedPlayMatchInfo matchInfo = new RankedPlayMatchInfo();

            public BanTestContainer(ModBanScreen screen)
            {
                RelativeSizeAxes = Axes.Both;
                Children = [matchInfo, screen];
            }
        }

        private UpdateableAvatar marker(string acronym) => screen.ChildrenOfType<UpdateableAvatar>().Single(t => t.Name == $"Opponent Ban {acronym}");

        private void clickMod(string acronym)
        {
            InputManager.MoveMouseTo(screen.ChildrenOfType<ModPanel>().Single(p => p.Mod.Acronym == acronym));
            InputManager.Click(MouseButton.Left);
        }
    }
}
