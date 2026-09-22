// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Online.Multiplayer;
using Realms;

namespace osu.Game.Database
{
    public partial class RealmDetachedBeatmapStore : BeatmapStore
    {
        private readonly ManualResetEventSlim loaded = new ManualResetEventSlim();

        private readonly BindableList<BeatmapSetInfo> detachedBeatmapSets = new BindableList<BeatmapSetInfo>();

        private IDisposable? realmSubscription;

        private OperationArgs? pendingOperation;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public override IBindableList<BeatmapSetInfo> GetBeatmapSets(CancellationToken? cancellationToken)
        {
            loaded.Wait(cancellationToken ?? CancellationToken.None);
            lock (detachedBeatmapSets)
                return detachedBeatmapSets.GetBoundCopy();
        }

        [Resolved]
        private osu.Game.Rulesets.RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private osu.Game.Configuration.OsuConfigManager config { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        private const int stable_sets_per_frame = 25;

        private readonly List<BeatmapSetInfo> activeStableSets = new List<BeatmapSetInfo>();
        private readonly Queue<BeatmapSetInfo> pendingStableSets = new Queue<BeatmapSetInfo>();
        private int realmSetCount;
        private StableBeatmapLoadResult? pendingStableResult;
        private CancellationTokenSource? stableReloadCancellation;
        private Bindable<bool> useStableDirectory = null!;
        private Bindable<string> stableDirectoryPath = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            if (config.Get<bool>(osu.Game.Configuration.OsuSetting.ForkUseStableDirectoryDirectly))
            {
                var stableStorage = game?.GetStorageForStableInstall();
                if (stableStorage != null)
                {
                    var result = new StableBeatmapProvider(rulesets).GetBeatmaps(stableStorage);
                    StablePathManager.Replace(result.BeatmapPaths, result.AudioPaths, result.BeatmapSets);
                    activeStableSets.AddRange(result.BeatmapSets);
                }
            }

            realmSubscription = realm.RegisterForNotifications(r => r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected), beatmapSetsChanged);

            useStableDirectory = config.GetBindable<bool>(osu.Game.Configuration.OsuSetting.ForkUseStableDirectoryDirectly);
            stableDirectoryPath = config.GetBindable<string>(osu.Game.Configuration.OsuSetting.ForkStableDirectoryPath);
            useStableDirectory.BindValueChanged(_ => Schedule(reloadStableBeatmaps));
            stableDirectoryPath.BindValueChanged(_ =>
            {
                if (config.Get<bool>(osu.Game.Configuration.OsuSetting.ForkUseStableDirectoryDirectly))
                    Schedule(reloadStableBeatmaps);
            });
        }

        private void reloadStableBeatmaps()
        {
            stableReloadCancellation?.Cancel();
            stableReloadCancellation?.Dispose();
            stableReloadCancellation = new CancellationTokenSource();
            var cancellationToken = stableReloadCancellation.Token;

            if (!config.Get<bool>(osu.Game.Configuration.OsuSetting.ForkUseStableDirectoryDirectly))
            {
                beginStableReplacement(new StableBeatmapLoadResult([], new Dictionary<Guid, string>(), new Dictionary<Guid, string>()));
                return;
            }

            var stableStorage = game?.GetStorageForStableInstall();
            if (stableStorage == null)
            {
                beginStableReplacement(new StableBeatmapLoadResult([], new Dictionary<Guid, string>(), new Dictionary<Guid, string>()));
                return;
            }

            Task.Run(() => new StableBeatmapProvider(rulesets).GetBeatmaps(stableStorage, cancellationToken), cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully && !cancellationToken.IsCancellationRequested)
                    {
                        var result = task.GetResultSafely();
                        Schedule(() =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                                beginStableReplacement(result);
                        });
                    }
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private void beginStableReplacement(StableBeatmapLoadResult result)
        {
            pendingStableSets.Clear();
            pendingStableResult = result;
        }

        private void beatmapSetsChanged(IRealmCollection<BeatmapSetInfo> sender, ChangeSet? changes)
        {
            if (changes == null)
            {
                if (sender is RealmResetEmptySet<BeatmapSetInfo>)
                {
                    // Usually we'd reset stuff here, but doing so triggers a silly flow which ends up deadlocking realm.
                    // Additionally, user should not be at song select when realm is blocking all operations in the first place.
                    //
                    // Note that due to the catch-up logic below, once operations are restored we will still be in a roughly
                    // correct state. The only things that this return will change is the carousel will not empty *during* the blocking
                    // operation.
                    return;
                }

                // Detaching beatmaps takes some time, so let's make sure it doesn't run on the update thread.
                var frozenSets = sender.Freeze();

                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        // operations purposefully not wrapped in `Realm.Run()`.
                        // `frozenSets` is, as the name suggests, frozen, and thus documented as safe to access from any thread for reading.
                        // using `Realm.Run()` would only be misdirection here as it would take out a *second, non-frozen* realm instance.
                        var detached = frozenSets.Detach();

                        lock (detachedBeatmapSets)
                        {
                            detachedBeatmapSets.Clear();
                            detachedBeatmapSets.AddRange(detached);
                            realmSetCount = detached.Count;
                            detachedBeatmapSets.AddRange(activeStableSets);
                        }
                    }
                    finally
                    {
                        loaded.Set();

                        // Freezing a collection incurs a full freeze of the realm too,
                        // which wraps a separate new `SharedRealmHandle` representing the frozen realm:
                        // https://github.com/realm/realm-dotnet/blob/113c01264fc00f6cedf3c829caa9cfb30b963914/Realm/Realm/DatabaseTypes/RealmCollectionBase.cs#L180-L191
                        // (note suppressed CA2000 inspection above!)
                        // https://github.com/realm/realm-dotnet/blob/113c01264fc00f6cedf3c829caa9cfb30b963914/Realm/Realm/Handles/SharedRealmHandle.cs#L650-L655
                        // https://github.com/realm/realm-core/blob/f8752e180b7f288feadffafdef818068755efe0a/src/realm/object-store/impl/realm_coordinator.cpp#L296-L313
                        //
                        // The freezing API on the .NET side does not expose a direct way to eagerly clean up that frozen handle.
                        // It appears that handle is simply allowed to fall out of scope and eventually get picked up by GC.
                        // This is a problem when considering interactions with the `BlockAllOperations()` API,
                        // which is designed to support moving the realm to custom locations.
                        // Not eagerly disposing the realm associated with the frozen collection as below can cause `BlockAllOperations()` to time out and crash.
                        //
                        // If freezing objects and/or collections is going to be more widely used in the repository,
                        // this should be extracted to an extension method and the direct usage of the freezing APIs banned in kind via `BannedSymbols.txt`.
                        frozenSets.Realm.Dispose();
                    }
                }, TaskCreationOptions.LongRunning).FireAndForget();

                return;
            }


            // Queue one atomic snapshot per Realm notification. Applying individual index operations
            // across multiple queued notifications is unsafe because all ChangeSet indices are relative
            // to the collection state for their own notification.
            // Only the newest complete snapshot matters. Replacing the pending snapshot also prevents
            // multiple detached copies of a large library from accumulating between update frames.
            Interlocked.Exchange(ref pendingOperation, new OperationArgs
            {
                BeatmapSets = sender.Detach(),
            });
        }

        protected override void Update()
        {
            base.Update();

            // We can't start processing operations until we have finished detaching the initial list.
            if (!loaded.IsSet)
                return;

            processStableReplacement();

            // Every operation is a complete Realm snapshot, so intermediate snapshots are stale as
            // soon as a newer one is available. Atomically consume only the latest snapshot.
            var operation = Interlocked.Exchange(ref pendingOperation, null);

            if (operation == null)
                return;

            lock (detachedBeatmapSets)
            {
                // Publish the full snapshot as one collection change. Inserting each set separately
                // causes consumers such as BeatmapCarousel to enqueue one scheduler task per set
                // (tens of thousands for large libraries). Replace only the Realm-owned prefix so
                // directly loaded stable sets remain intact.
                detachedBeatmapSets.ReplaceRange(0, realmSetCount, operation.BeatmapSets);
                realmSetCount = operation.BeatmapSets.Count;
            }
        }

        private void processStableReplacement()
        {
            lock (detachedBeatmapSets)
            {
                if (pendingStableResult != null)
                {
                    int count = Math.Min(stable_sets_per_frame, activeStableSets.Count);
                    if (count > 0)
                    {
                        for (int i = 0; i < count; i++)
                            detachedBeatmapSets.Remove(activeStableSets[activeStableSets.Count - 1 - i]);

                        activeStableSets.RemoveRange(activeStableSets.Count - count, count);
                        return;
                    }

                    StablePathManager.Replace(pendingStableResult.BeatmapPaths, pendingStableResult.AudioPaths, pendingStableResult.BeatmapSets);
                    enqueueStableSets(pendingStableResult.BeatmapSets);
                    pendingStableResult = null;
                }

                if (pendingStableSets.Count == 0)
                    return;

                var batch = new List<BeatmapSetInfo>(stable_sets_per_frame);
                while (batch.Count < stable_sets_per_frame && pendingStableSets.TryDequeue(out var set))
                    batch.Add(set);

                int insertionIndex = realmSetCount + activeStableSets.Count;
                foreach (var set in batch)
                    detachedBeatmapSets.Insert(insertionIndex++, set);

                activeStableSets.AddRange(batch);
            }
        }

        private void enqueueStableSets(IEnumerable<BeatmapSetInfo> sets)
        {
            foreach (var set in sets)
                pendingStableSets.Enqueue(set);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            loaded.Set();
            loaded.Dispose();
            realmSubscription?.Dispose();
            stableReloadCancellation?.Cancel();
            stableReloadCancellation?.Dispose();
        }

        private record OperationArgs
        {
            public required IReadOnlyList<BeatmapSetInfo> BeatmapSets;
        }
    }
}
