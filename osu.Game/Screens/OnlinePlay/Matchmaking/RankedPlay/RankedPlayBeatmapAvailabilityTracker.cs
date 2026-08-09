// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay
{
    public partial class RankedPlayBeatmapAvailabilityTracker : OnlinePlayBeatmapAvailabilityTracker
    {
        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private BeatmapLookupCache beatmapLookupCache { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private BeatmapModelDownloader beatmapDownloader { get; set; } = null!;

        private CancellationTokenSource? downloadCheckCancellation;
        private int? lastDownloadCheckedBeatmapId;
        private ScheduledDelegate? downloadRetry;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs(beatmapDownloader = new BeatmapModelDownloader(parent.Get<BeatmapManager>(), parent.Get<IAPIProvider>(), parent.Get<OsuConfigManager>()));
            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Availability.BindValueChanged(onBeatmapAvailabilityChanged, true);
            beatmapDownloader.DownloadFailed += onDownloadFailed;

            client.SettingsChanged += onSettingsChanged;
            onSettingsChanged(client.Room!.Settings);
        }

        private void onSettingsChanged(MultiplayerRoomSettings settings)
        {
            PlaylistItem.Value = new PlaylistItem(client.Room!.CurrentPlaylistItem);
            checkForAutomaticDownload(client.Room!.CurrentPlaylistItem);
            client.ChangeBeatmapAvailability(Availability.Value).FireAndForget();
        }

        private void onBeatmapAvailabilityChanged(ValueChangedEvent<BeatmapAvailability> availability)
        {
            client.ChangeBeatmapAvailability(availability.NewValue).FireAndForget();
        }

        private void checkForAutomaticDownload(MultiplayerPlaylistItem item)
        {
            // This method is called every time anything changes in the room.
            // This could result in download requests firing far too often, when we only expect them to fire once per beatmap.
            //
            // Without this check, we would see especially egregious behaviour when a user has hit the download rate limit.
            if (lastDownloadCheckedBeatmapId == item.BeatmapID)
                return;

            lastDownloadCheckedBeatmapId = item.BeatmapID;

            downloadCheckCancellation?.Cancel();
            downloadRetry?.Cancel();

            if (beatmapManager.IsAvailableLocally(new APIBeatmap { OnlineID = item.BeatmapID }))
                return;

            // In a perfect world we'd use BeatmapAvailability, but there's no event-driven flow for when a selection changes.
            // ie. if selection changes from "not downloaded" to another "not downloaded" we wouldn't get a value changed raised.
            var cancellation = downloadCheckCancellation = new CancellationTokenSource();
            beatmapLookupCache
                .GetBeatmapAsync(item.BeatmapID, cancellation.Token)
                .ContinueWith(resolved => Schedule(() =>
                {
                    if (cancellation.IsCancellationRequested || client.Room?.CurrentPlaylistItem.BeatmapID != item.BeatmapID)
                        return;

                    APIBeatmapSet? beatmapSet = resolved.GetResultSafely()?.BeatmapSet;

                    if (beatmapSet == null)
                    {
                        scheduleRetry(item.BeatmapID);
                        return;
                    }

                    if (!beatmapDownloader.Download(beatmapSet, config.Get<bool>(OsuSetting.PreferNoVideo))
                        && beatmapDownloader.GetExistingDownload(beatmapSet) == null
                        && !beatmapManager.IsAvailableLocally(new APIBeatmap { OnlineID = item.BeatmapID }))
                        scheduleRetry(item.BeatmapID);
                }));
        }

        private void onDownloadFailed(ArchiveDownloadRequest<IBeatmapSetInfo> request)
        {
            if (client.Room?.CurrentPlaylistItem.BeatmapID == request.Model.OnlineID)
                Schedule(() => scheduleRetry(request.Model.OnlineID));
        }

        private void scheduleRetry(int beatmapId)
        {
            downloadRetry?.Cancel();
            downloadRetry = Scheduler.AddDelayed(() =>
            {
                if (client.Room?.CurrentPlaylistItem.BeatmapID != beatmapId)
                    return;

                lastDownloadCheckedBeatmapId = null;
                checkForAutomaticDownload(client.Room.CurrentPlaylistItem);
            }, 3000);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
                client.SettingsChanged -= onSettingsChanged;

            if (beatmapDownloader.IsNotNull())
                beatmapDownloader.DownloadFailed -= onDownloadFailed;

            downloadCheckCancellation?.Cancel();
            downloadRetry?.Cancel();
        }
    }
}
