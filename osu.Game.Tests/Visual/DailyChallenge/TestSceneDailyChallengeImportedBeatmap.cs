using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Metadata;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual.Metadata;
using osu.Game.Tests.Visual.OnlinePlay;

namespace osu.Game.Tests.Visual.DailyChallenge
{
    [HeadlessTest]
    public partial class TestSceneDailyChallengeImportedBeatmap : OnlinePlayTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        [Cached(typeof(MetadataClient))]
        private readonly TestMetadataClient metadataClient = new TestMetadataClient();

        private BeatmapManager manager = null!;
        private BeatmapInfo map = null!;
        private Room room = null!;
        private Screens.OnlinePlay.DailyChallenge.DailyChallenge daily = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBeatmapApiProvider>(new BeatmapApiProvider(API, null));
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            Dependencies.Cache(new osu.Game.Rulesets.RealmRulesetStore(Realm));
            Dependencies.Cache(manager = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(Realm);
            var store = new RealmDetachedBeatmapStore();
            Dependencies.CacheAs<BeatmapStore>(store);
            Add(store);
            Add(metadataClient);
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("import before online metadata is committed", () =>
            {
                var imported = manager.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).GetResultSafely()!;
                map = manager.GetAllUsableBeatmapSets().Single(s => s.ID == imported.ID).Beatmaps.First(b => b.Ruleset.OnlineID == 0);
                Realm.Write(r => r.Find<BeatmapInfo>(map.ID)!.OnlineMD5Hash = string.Empty);
                room = new Room
                {
                    Name = "Freshly imported daily challenge",
                    Playlist = [new PlaylistItem(map)],
                    EndDate = DateTimeOffset.UtcNow.AddHours(12),
                    Category = RoomCategory.DailyChallenge,
                };
                API.Perform(new CreateRoomRequest(room));
            });
            AddAssert("legacy metadata-gated lookup cannot resolve import", () => manager.QueryOnlineBeatmapId(map.OnlineID) == null);
            AddStep("open daily", () => LoadScreen(daily = new Screens.OnlinePlay.DailyChallenge.DailyChallenge(room)));
            AddUntilStep("daily current", () => daily.IsCurrentScreen());
        }

        [Test]
        public void TestEntryResolvesExactImportedRevision()
        {
            AddUntilStep("availability verifies downloaded file", () => daily.ChildrenOfType<OnlinePlayBeatmapAvailabilityTracker>()
                .Single().Availability.Value.State == DownloadState.LocallyAvailable);
            AddAssert("working beatmap matches daily item", () => daily.Beatmap.Value.BeatmapInfo.ID == map.ID);
            AddAssert("working checksum matches daily item", () => daily.Beatmap.Value.BeatmapInfo.MD5Hash == map.MD5Hash);
        }

        [Test]
        public void TestStartRestoresWorkingBeatmapBeforeLoadingPlayer()
        {
            AddUntilStep("availability verifies downloaded file", () => daily.ChildrenOfType<OnlinePlayBeatmapAvailabilityTracker>()
                .Single().Availability.Value.State == DownloadState.LocallyAvailable);
            AddStep("start with stale working beatmap", () =>
            {
                daily.Beatmap.Value = (WorkingBeatmap)manager.DefaultBeatmap;
                // Invoke the same callback as the ready button, without waiting another frame.
                typeof(Screens.OnlinePlay.DailyChallenge.DailyChallenge)
                    .GetMethod("startPlay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(daily, null);
            });
            AddAssert("correct revision selected before loader starts", () => daily.Beatmap.Value.BeatmapInfo.ID == map.ID);
            AddUntilStep("player loader pushed", () => Stack.CurrentScreen is Screens.Play.PlayerLoader);
        }
    }
}
