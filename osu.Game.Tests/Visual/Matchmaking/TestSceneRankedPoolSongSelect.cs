using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Screens.OnlinePlay.Matchmaking.Queue;
using osu.Game.Screens.OnlinePlay.Playlists;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual.OnlinePlay;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Matchmaking
{
    public partial class TestSceneRankedPoolSongSelect : OnlinePlayTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        [Cached(typeof(INotificationOverlay))]
        private readonly NotificationOverlay notificationOverlay = new NotificationOverlay();

        private RelaxPoolSongSelect screen = null!;
        private Room room = null!;

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            Dependencies.Cache(new RealmRulesetStore(Realm));
            var manager = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default);
            Dependencies.Cache(manager);
            Dependencies.Cache(Realm);
            var store = new RealmDetachedBeatmapStore();
            Dependencies.CacheAs<BeatmapStore>(store);
            Add(store);
            manager.Import(TestResources.CreateTestBeatmapSetInfo(3));
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("open pool selector", () => LoadScreen(screen = new RelaxPoolSongSelect(room = new Room())));
            AddUntilStep("selector ready", () => screen.IsCurrentScreen() && !screen.IsFiltering && screen.Beatmap.Value.BeatmapInfo.OnlineID > 0);
        }

        [Test]
        public void TestFooterClickAddsSelectedDifficulty()
        {
            AddUntilStep("add button visible", () => ScreenFooter.ChildrenOfType<AddToPlaylistFooterButton>().SingleOrDefault()?.IsPresent == true);
            AddWaitStep("finish footer animation", 5);
            AddStep("click actual add button", () =>
            {
                InputManager.MoveMouseTo(ScreenFooter.ChildrenOfType<AddToPlaylistFooterButton>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("selected difficulty added", () => room.Playlist.Count == 1);
            AddAssert("correct online difficulty", () => room.Playlist.Single().Beatmap.OnlineID == screen.Beatmap.Value.BeatmapInfo.OnlineID);
        }

        [Test]
        public void TestEnterAddsSelectedDifficulty()
        {
            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddUntilStep("selected difficulty added", () => room.Playlist.Count == 1);
        }
    }
}
