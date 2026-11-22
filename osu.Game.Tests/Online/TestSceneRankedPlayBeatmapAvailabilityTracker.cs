using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual.Multiplayer;

namespace osu.Game.Tests.Online
{
    [HeadlessTest]
    public partial class TestSceneRankedPlayBeatmapAvailabilityTracker : MultiplayerTestScene
    {
        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        private BeatmapInfo map = null!;
        private RankedPlayBeatmapAvailabilityTracker tracker = null!;
        private readonly List<DownloadBeatmapSetRequest> downloads = new List<DownloadBeatmapSetRequest>();

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBeatmapApiProvider>(new BeatmapApiProvider(API, null));
            return dependencies;
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("import local revision", () =>
            {
                var imported = beatmaps.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).GetResultSafely()!;
                map = beatmaps.GetAllUsableBeatmapSets().Single(set => set.ID == imported.ID).Beatmaps.First(info => info.Ruleset.OnlineID == 0);
                downloads.Clear();

                var api = (DummyAPIAccess)API;
                var handler = api.HandleRequest;
                api.HandleRequest = request =>
                {
                    switch (request)
                    {
                        case GetBeatmapsRequest lookup:
                            lookup.TriggerSuccess(new GetBeatmapsResponse
                            {
                                Beatmaps = new List<APIBeatmap>
                                {
                                    new APIBeatmap
                                    {
                                        OnlineID = map.OnlineID,
                                        Checksum = map.MD5Hash,
                                        BeatmapSet = new APIBeatmapSet { OnlineID = map.BeatmapSet!.OnlineID },
                                    }
                                }
                            });
                            return true;

                        case DownloadBeatmapSetRequest download:
                            downloads.Add(download);
                            return true;

                        default:
                            return handler?.Invoke(request) ?? false;
                    }
                };
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestDownloadsOnlyWhenMatchRevisionIsMissing(bool matchingChecksum)
        {
            joinRoom(matchingChecksum);
            AddStep("load ranked tracker", () => Child = tracker = new RankedPlayBeatmapAvailabilityTracker());
            AddUntilStep("availability resolved", () => matchingChecksum
                ? tracker.Availability.Value.State == DownloadState.LocallyAvailable
                : tracker.Availability.Value.State is DownloadState.NotDownloaded or DownloadState.Downloading);
            if (!matchingChecksum)
                AddUntilStep("redownload despite same online ID being installed", () => downloads.Count == 1);
            AddWaitStep("allow repeated room notifications", 3);
            AddAssert("only missing revision requested", () => downloads.Count == (matchingChecksum ? 0 : 1));
            AddStep("repeat settings notification", () => MultiplayerClient.ChangeServerRoomSettings(MultiplayerClient.ServerRoom!.Settings).WaitSafely());
            AddWaitStep("allow duplicate download check", 3);
            AddAssert("no duplicate download", () => downloads.Count == (matchingChecksum ? 0 : 1));
        }

        [Test]
        public void TestChecksumChangeInSamePlaylistSlotStartsDownload()
        {
            joinRoom(true);
            AddStep("load ranked tracker", () => Child = tracker = new RankedPlayBeatmapAvailabilityTracker());
            AddUntilStep("original revision available", () => tracker.Availability.Value.State == DownloadState.LocallyAvailable);
            AddAssert("no download initially", () => downloads.Count == 0);
            AddStep("replace checksum without changing slot or beatmap ID", () =>
            {
                var item = new MultiplayerPlaylistItem(new PlaylistItem(MultiplayerClient.ClientRoom!.CurrentPlaylistItem));
                item.BeatmapChecksum = new string('0', 32);
                MultiplayerClient.PlaylistItemChanged(item).WaitSafely();
            });
            AddUntilStep("replacement revision requested", () => downloads.Count == 1);
            AddUntilStep("old revision not announced as available", () => tracker.Availability.Value.State is DownloadState.NotDownloaded or DownloadState.Downloading);
        }

        [Test]
        public void TestRedownloadedRevisionBecomesAvailable()
        {
            joinRoom(true);
            AddStep("install a different revision of the same set", () =>
                beatmaps.Import(new ImportTask(TestResources.GetTestBeatmapForImport(true))).WaitSafely());
            AddAssert("required revision is absent", () => beatmaps.QueryOnlineBeatmapId(map.OnlineID, map.MD5Hash) == null);
            AddStep("load ranked tracker", () => Child = tracker = new RankedPlayBeatmapAvailabilityTracker());
            AddUntilStep("redownload requested", () => downloads.Count == 1);
            // Exercise the real import/availability path without accessing a download mirror.
            AddStep("import downloaded revision", () => beatmaps.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).WaitSafely());
            AddUntilStep("required revision available again", () => tracker.Availability.Value.State == DownloadState.LocallyAvailable);
            AddAssert("exact revision resolves for gameplay", () => beatmaps.QueryOnlineBeatmapId(map.OnlineID, map.MD5Hash) != null);
        }

        private void joinRoom(bool matchingChecksum)
        {
            AddStep("join ranked room", () =>
            {
                var room = CreateDefaultRoom(MatchType.RankedPlay);
                room.Playlist = [new PlaylistItem(new APIBeatmap
                {
                    OnlineID = map.OnlineID,
                    Checksum = matchingChecksum ? map.MD5Hash : new string('0', 32),
                    BeatmapSet = new APIBeatmapSet { OnlineID = map.BeatmapSet!.OnlineID },
                })];
                JoinRoom(room);
            });
            WaitForJoined();
        }
    }
}
