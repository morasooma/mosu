// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.Components
{
    public partial class PlaylistHeader : SectionHeader
    {
        private readonly Room room;
        private PlaylistDownloadAllButton downloadAllButton = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        public PlaylistHeader(Room room)
            : base("Playlist")
        {
            this.room = room;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            AddInternal(downloadAllButton = new PlaylistDownloadAllButton(room)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Margin = new MarginPadding { Right = 2, Top = 2 },
            });

            room.PropertyChanged += onRoomPropertyChanged;
            updateDuration();
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Room.Playlist))
                updateDuration();
        }

        private void updateDuration()
        {
            DetailsText.Value = $"{room.Playlist.GetTotalDuration(rulesets)}";
            downloadAllButton?.RefreshPlaylist();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            room.PropertyChanged -= onRoomPropertyChanged;
        }

        private partial class PlaylistDownloadAllButton : IconButton
        {
            private const int max_parallel_downloads = 2;

            private readonly Room room;
            private readonly Queue<int> queuedSetIds = new Queue<int>();
            private readonly HashSet<int> queuedSetLookup = new HashSet<int>();
            private readonly List<int> playlistOrder = new List<int>();
            private readonly Dictionary<int, IBeatmapSetInfo> targetsById = new Dictionary<int, IBeatmapSetInfo>();
            private readonly Dictionary<int, List<IBeatmapInfo>> requiredBeatmapsBySetId = new Dictionary<int, List<IBeatmapInfo>>();

            private readonly FillFlowContainer trackerContainer;
            private ScheduledDelegate? scheduledQueueProcessing;

            private Bindable<bool> noVideoSetting = null!;

            [Resolved]
            private BeatmapModelDownloader beatmapDownloader { get; set; } = null!;

            [Resolved]
            private BeatmapManager beatmapManager { get; set; } = null!;

            [Resolved]
            private OsuConfigManager config { get; set; } = null!;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public PlaylistDownloadAllButton(Room room)
            {
                this.room = room;

                Size = new Vector2(24);
                Icon = FontAwesome.Solid.Download;
                IconScale = new Vector2(0.8f);
                TooltipText = ForkSettingsStrings.PlaylistDownloadAll;
                Action = queueMissingBeatmaps;

                AddInternal(trackerContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                });
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                noVideoSetting = config.GetBindable<bool>(OsuSetting.PreferNoVideo);
                IconHoverColour = colours.Yellow;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                refreshPlaylist();
            }

            public void RefreshPlaylist() => Scheduler.AddOnce(refreshPlaylist);

            private void refreshPlaylist()
            {
                playlistOrder.Clear();
                targetsById.Clear();
                requiredBeatmapsBySetId.Clear();
                queuedSetIds.Clear();
                queuedSetLookup.Clear();
                scheduledQueueProcessing?.Cancel();
                scheduledQueueProcessing = null;
                trackerContainer.Clear();

                foreach (PlaylistItem item in room.Playlist)
                {
                    IBeatmapSetInfo? beatmapSet = getBeatmapSet(item);

                    if (beatmapSet == null)
                        continue;

                    trackRequiredBeatmap(beatmapSet.OnlineID, item.Beatmap);

                    if (targetsById.ContainsKey(beatmapSet.OnlineID))
                        continue;

                    playlistOrder.Add(beatmapSet.OnlineID);
                    targetsById[beatmapSet.OnlineID] = beatmapSet;

                    var tracker = new BeatmapDownloadTracker(beatmapSet);
                    tracker.State.BindValueChanged(_ =>
                    {
                        processQueue();
                        updateState();
                    }, true);

                    trackerContainer.Add(tracker);
                }

                updateState();
            }

            private void trackRequiredBeatmap(int setId, IBeatmapInfo beatmap)
            {
                if (!requiredBeatmapsBySetId.TryGetValue(setId, out List<IBeatmapInfo>? beatmaps))
                    requiredBeatmapsBySetId[setId] = beatmaps = new List<IBeatmapInfo>();

                bool alreadyTracked = beatmaps.Any(existing =>
                    (!string.IsNullOrEmpty(existing.MD5Hash) && existing.MD5Hash == beatmap.MD5Hash)
                    || (existing.OnlineID > 0 && existing.OnlineID == beatmap.OnlineID));

                if (!alreadyTracked)
                    beatmaps.Add(beatmap);
            }

            private void queueMissingBeatmaps()
            {
                foreach (int setId in playlistOrder)
                {
                    if (!targetsById.ContainsKey(setId))
                        continue;

                    DownloadState state = getState(setId);

                    if (state != DownloadState.NotDownloaded || !queuedSetLookup.Add(setId))
                        continue;

                    queuedSetIds.Enqueue(setId);
                }

                processQueue();
                updateState();
            }

            private void processQueue()
            {
                int activeDownloads = playlistOrder.Count(isDownloadActive);

                while (activeDownloads < max_parallel_downloads && queuedSetIds.Count > 0)
                {
                    int setId = queuedSetIds.Dequeue();
                    queuedSetLookup.Remove(setId);

                    if (!targetsById.TryGetValue(setId, out IBeatmapSetInfo? beatmapSet) || getState(setId) != DownloadState.NotDownloaded)
                        continue;

                    if (beatmapDownloader.Download(beatmapSet, noVideoSetting.Value))
                        activeDownloads++;
                }

                scheduleQueueProcessingIfNeeded();
            }

            private void updateState()
            {
                int totalSets = playlistOrder.Count;
                int localSets = playlistOrder.Count(setId => getState(setId) == DownloadState.LocallyAvailable);
                int activeDownloads = playlistOrder.Count(isDownloadActive);
                int missingSets = playlistOrder.Count(setId => getState(setId) == DownloadState.NotDownloaded);

                Enabled.Value = totalSets > 0 && (missingSets > 0 || queuedSetIds.Count > 0);
                Alpha = totalSets == 0 ? 0.35f : Enabled.Value ? 1 : 0.55f;

                TooltipText = totalSets switch
                {
                    0 => ForkSettingsStrings.PlaylistNoMapsToDownload,
                    _ when activeDownloads > 0 || queuedSetIds.Count > 0 => ForkSettingsStrings.PlaylistDownloadProgress(localSets, totalSets, activeDownloads, queuedSetIds.Count),
                    _ when missingSets > 0 => ForkSettingsStrings.PlaylistMissingSets(missingSets),
                    _ => ForkSettingsStrings.PlaylistAllDownloaded
                };
            }

            private DownloadState getState(int setId)
            {
                DownloadState trackerState = getTrackerState(setId);

                if (trackerState == DownloadState.Downloading || trackerState == DownloadState.Importing)
                {
                    if (isDownloadActive(setId))
                        return trackerState;
                }

                bool setAvailableLocally = targetsById.TryGetValue(setId, out IBeatmapSetInfo? beatmapSet)
                                           && beatmapManager.QueryBeatmapSet(s => s.OnlineID == beatmapSet.OnlineID && !s.DeletePending) != null;

                if (!setAvailableLocally)
                    return DownloadState.NotDownloaded;

                return hasAllRequiredBeatmaps(setId)
                    ? DownloadState.LocallyAvailable
                    : DownloadState.NotDownloaded;
            }

            private DownloadState getTrackerState(int setId)
                => trackerContainer.Children.OfType<BeatmapDownloadTracker>()
                                   .FirstOrDefault(tracker => tracker.TrackedItem.OnlineID == setId)?.State.Value
                   ?? DownloadState.Unknown;

            private bool isDownloadActive(int setId)
                => targetsById.TryGetValue(setId, out IBeatmapSetInfo? beatmapSet)
                   && beatmapDownloader.GetExistingDownload(beatmapSet) != null;

            private void scheduleQueueProcessingIfNeeded()
            {
                if (queuedSetIds.Count == 0 || scheduledQueueProcessing != null)
                    return;

                scheduledQueueProcessing = Scheduler.AddDelayed(() =>
                {
                    scheduledQueueProcessing = null;
                    processQueue();
                    updateState();
                }, 500);
            }

            private bool hasAllRequiredBeatmaps(int setId)
            {
                return requiredBeatmapsBySetId.TryGetValue(setId, out List<IBeatmapInfo>? beatmaps)
                       && beatmaps.Count > 0
                       && beatmaps.All(isBeatmapAvailableLocally);
            }

            private bool isBeatmapAvailableLocally(IBeatmapInfo beatmap)
            {
                if (beatmap.OnlineID > 0)
                    return beatmapManager.IsAvailableLocally(beatmap);

                if (!string.IsNullOrEmpty(beatmap.MD5Hash))
                    return beatmapManager.QueryBeatmap(b => b.MD5Hash == beatmap.MD5Hash) != null;

                return false;
            }

            private static IBeatmapSetInfo? getBeatmapSet(PlaylistItem item)
            {
                if (item.Beatmap.BeatmapSet is IBeatmapSetInfo beatmapSet && beatmapSet.OnlineID > 0)
                    return beatmapSet;

                if (item.Beatmap is APIBeatmap apiBeatmap)
                {
                    if (apiBeatmap.BeatmapSet?.OnlineID > 0)
                        return apiBeatmap.BeatmapSet;

                    if (apiBeatmap.OnlineBeatmapSetID > 0)
                        return new BeatmapSetInfo { OnlineID = apiBeatmap.OnlineBeatmapSetID };
                }

                return item.Beatmap.BeatmapSet?.OnlineID > 0
                    ? new BeatmapSetInfo { OnlineID = item.Beatmap.BeatmapSet.OnlineID }
                    : null;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                scheduledQueueProcessing?.Cancel();
            }
        }
    }
}
