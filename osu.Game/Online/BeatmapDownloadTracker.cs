// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;

namespace osu.Game.Online
{
    public partial class BeatmapDownloadTracker : DownloadTracker<IBeatmapSetInfo>
    {
        [Resolved(CanBeNull = true)]
        protected BeatmapModelDownloader? Downloader { get; private set; }

        private ArchiveDownloadRequest<IBeatmapSetInfo>? attachedRequest;

        private IDisposable? realmSubscription;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public BeatmapDownloadTracker(IBeatmapSetInfo trackedItem)
            : base(trackedItem)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Downloader == null)
                return;

            Downloader.DownloadBegan += downloadBegan;
            Downloader.DownloadFailed += downloadFailed;

            subscribeToAvailability();
        }

        private int modelGeneration;

        internal void ChangeTrackedItem(IBeatmapSetInfo item)
        {
            modelGeneration++;
            realmSubscription?.Dispose();
            realmSubscription = null;
            if (stableAvailabilityChanged != null)
                StablePathManager.AvailabilityChanged -= stableAvailabilityChanged;
            stableAvailabilityChanged = null;
            attachDownload(null);
            TrackedItem = item;
            UpdateProgress(0);
            UpdateState(DownloadState.Unknown);
            if (IsLoaded && Downloader != null && item.OnlineID > 0)
                subscribeToAvailability();
        }

        private void subscribeToAvailability()
        {
            // Used to interact with manager classes that don't support interface types. Will eventually be replaced.
            var beatmapSetInfo = new BeatmapSetInfo { OnlineID = TrackedItem.OnlineID };

            // Required local for iOS. Will cause runtime crash if inlined.
            int onlineId = TrackedItem.OnlineID;
            int generation = modelGeneration;

            realmSubscription = realm.RegisterForNotifications(r => r.All<BeatmapSetInfo>().Where(s => s.OnlineID == onlineId && !s.DeletePending), (items, _) =>
            {
                updateAvailability(items.Any());
            });

            void updateAvailability(bool availableInRealm)
            {
                if (availableInRealm || StablePathManager.IsBeatmapSetAvailableLocally(onlineId))
                {
                    Schedule(() =>
                    {
                        if (generation != modelGeneration) return;
                        UpdateState(DownloadState.LocallyAvailable);
                        attachDownload(null);
                    });
                }
                else
                {
                    Schedule(() =>
                    {
                        if (generation != modelGeneration) return;
                        UpdateState(DownloadState.NotDownloaded);
                        attachDownload(Downloader.GetExistingDownload(beatmapSetInfo));
                    });
                }
            }

            void stableAvailabilityChanged() => Schedule(() =>
            {
                if (generation != modelGeneration) return;
                bool availableInRealm = realm.Run(r => r.All<BeatmapSetInfo>().Any(s => s.OnlineID == onlineId && !s.DeletePending));
                updateAvailability(availableInRealm);
            });

            this.stableAvailabilityChanged = stableAvailabilityChanged;
            StablePathManager.AvailabilityChanged += stableAvailabilityChanged;
        }

        private Action? stableAvailabilityChanged;

        private void downloadBegan(ArchiveDownloadRequest<IBeatmapSetInfo> request) => Schedule(() =>
        {
            if (checkEquality(request.Model, TrackedItem))
                attachDownload(request);
        });

        private void downloadFailed(ArchiveDownloadRequest<IBeatmapSetInfo> request) => Schedule(() =>
        {
            if (checkEquality(request.Model, TrackedItem))
                attachDownload(null);
        });

        private void attachDownload(ArchiveDownloadRequest<IBeatmapSetInfo>? request)
        {
            if (attachedRequest != null)
            {
                attachedRequest.Failure -= onRequestFailure;
                attachedRequest.DownloadProgressed -= onRequestProgress;
                attachedRequest.Success -= onRequestSuccess;
            }

            attachedRequest = request;

            if (attachedRequest != null)
            {
                if (attachedRequest.Progress == 1)
                {
                    UpdateProgress(1);
                    UpdateState(DownloadState.Importing);
                }
                else
                {
                    UpdateProgress(attachedRequest.Progress);
                    UpdateState(DownloadState.Downloading);

                    attachedRequest.Failure += onRequestFailure;
                    attachedRequest.DownloadProgressed += onRequestProgress;
                    attachedRequest.Success += onRequestSuccess;
                }
            }
            else
            {
                if (State.Value == DownloadState.Downloading)
                    UpdateState(DownloadState.NotDownloaded);
            }
        }

        private void onRequestSuccess(string _)
        {
            var request = attachedRequest;
            Schedule(() => { if (ReferenceEquals(request, attachedRequest) && request != null) UpdateState(DownloadState.Importing); });
        }

        private void onRequestProgress(float progress)
        {
            var request = attachedRequest;
            Schedule(() => { if (ReferenceEquals(request, attachedRequest) && request != null) UpdateProgress(progress); });
        }

        private void onRequestFailure(Exception e)
        {
            var request = attachedRequest;
            Schedule(() => { if (ReferenceEquals(request, attachedRequest) && request != null) attachDownload(null); });
        }

        private bool checkEquality(IBeatmapSetInfo x, IBeatmapSetInfo y) => x.OnlineID == y.OnlineID;

        #region Disposal

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            attachDownload(null);

            realmSubscription?.Dispose();

            if (stableAvailabilityChanged != null)
                StablePathManager.AvailabilityChanged -= stableAvailabilityChanged;

            if (Downloader != null)
            {
                Downloader.DownloadBegan -= downloadBegan;
                Downloader.DownloadFailed -= downloadFailed;
            }
        }

        #endregion
    }
}
