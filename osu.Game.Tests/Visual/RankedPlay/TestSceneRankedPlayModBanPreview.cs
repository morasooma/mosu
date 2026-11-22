using System;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Online.Multiplayer;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.RankedPlay;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;

namespace osu.Game.Tests.Visual.RankedPlay
{
    public partial class TestSceneRankedPlayModBanPreview : RankedPlayTestScene
    {
        [Cached(typeof(PreviewTrackManager))]
        private readonly Components.TestScenePreviewTrackManager.TestPreviewTrackManager previewTracks =
            new Components.TestScenePreviewTrackManager.TestPreviewTrackManager(new osu.Framework.Audio.AudioAdjustments());

        private RankedPlayScreen screen = null!;

        protected override bool UseFreshStoragePerRun => true;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBeatmapApiProvider>(new BeatmapApiProvider(API, null));
            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Add(previewTracks);
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("create isolated demo room", () => JoinRoom(CreateDefaultRoom(MatchType.RankedPlay)));
            WaitForJoined();
            AddStep("add demo opponent", () => MultiplayerClient.AddUser(new APIUser { Id = 2, Username = "Opponent" }));
        }

        [Test]
        public void InteractiveBanPreview()
        {
            AddStep("show mod ban animation", () =>
            {
                LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!));
                screen.OnLoadComplete += _ => Scheduler.AddDelayed(showBanPhase, 1500);
            });
        }

        private void showBanPhase()
        {
                MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.ModBan, state =>
                {
                    state.RandomMods = true;
                    state.BannableRandomMods = ["AA", "AA!", "EZ", "HR", "HD", "RD", "TR", "SI", "BR", "DP"];
                }).WaitSafely();
                MultiplayerClient.StartCountdown(new RankedPlayStageCountdown
                {
                    Stage = RankedPlayStage.ModBan,
                    TimeRemaining = TimeSpan.FromSeconds(20),
                }).WaitSafely();

                Scheduler.AddDelayed(() => opponentChoice("HD", false).FireAndForget(), 5000);
                Scheduler.AddDelayed(() => opponentChoice("HR", false).FireAndForget(), 8500);
                Scheduler.AddDelayed(() => opponentChoice("HR", true).FireAndForget(), 12000);
        }

        private Task opponentChoice(string acronym, bool confirm)
            => MultiplayerClient.SendUserMatchRequest(2, new RankedPlayModBanRequest { Acronym = acronym, Confirm = confirm });
    }
}
