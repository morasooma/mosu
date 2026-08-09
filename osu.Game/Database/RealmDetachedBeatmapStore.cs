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

        private readonly Queue<OperationArgs> pendingOperations = new Queue<OperationArgs>();

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

        [Resolved]
        private OsuGame game { get; set; } = null!;

        private const int stable_sets_per_frame = 25;

        private readonly List<BeatmapSetInfo> activeStableSets = new List<BeatmapSetInfo>();
        private readonly Queue<BeatmapSetInfo> pendingStableSets = new Queue<BeatmapSetInfo>();
        private int stableSetsToRemove;
        private StableBeatmapLoadResult? pendingStableResult;
        private CancellationTokenSource? stableReloadCancellation;
        private Bindable<bool> useStableDirectory = null!;
        private Bindable<string> stableDirectoryPath = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            if (config.Get<bool>(osu.Game.Configuration.OsuSetting.ForkUseStableDirectoryDirectly))
            {
                var stableStorage = game.GetStorageForStableInstall();
                if (stableStorage != null)
                {
                    var provider = new StableBeatmapProvider(rulesets);
                    var result = provider.GetBeatmaps(stableStorage);
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

            var stableStorage = game.GetStorageForStableInstall();

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
            stableSetsToRemove = activeStableSets.Count;
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
                        realm.Run(_ =>
                        {
                            var detached = frozenSets.Detach();

                            lock (detachedBeatmapSets)
                            {
                                detachedBeatmapSets.Clear();
                                detachedBeatmapSets.AddRange(detached);
                                detachedBeatmapSets.AddRange(activeStableSets);
                            }
                        });
                    }
                    finally
                    {
                        loaded.Set();
                    }
                }, TaskCreationOptions.LongRunning).FireAndForget();

                return;
            }

            if (changes.InsertedIndices.Length == 1 && changes.DeletedIndices.Length == 1)
            {
                lock (detachedBeatmapSets)
                {
                    var deletedSet = detachedBeatmapSets[changes.DeletedIndices[0]];
                    var insertedSet = sender[changes.InsertedIndices[0]];

                    // this handles beatmap updates using a heuristic that a beatmap update will preserve the online ID.
                    // it relies on the fact that updates are performed by removing the old set and adding a new one, in a single transaction.
                    // instead of removing the old set and adding a new one to the collection too, which would trigger consumers' logic related to set removals,
                    // move the deleted set to the index occupied by the new one and then replace it in-place.
                    // due to this, the operation can be presented to consumer in a manner that permits them to actually handle this as a replace operation
                    // and not trigger any set removal logic that may result in selections changing or similar undesirable side effects.
                    if (deletedSet.OnlineID == insertedSet.OnlineID)
                    {
                        pendingOperations.Enqueue(new OperationArgs
                        {
                            Type = OperationType.MoveAndReplace,
                            BeatmapSet = insertedSet.Detach(),
                            Index = changes.DeletedIndices[0],
                            NewIndex = changes.InsertedIndices[0],
                        });

                        return;
                    }
                }
            }

            foreach (int i in changes.DeletedIndices.OrderDescending())
            {
                pendingOperations.Enqueue(new OperationArgs
                {
                    Type = OperationType.Remove,
                    Index = i,
                });
            }

            foreach (int i in changes.InsertedIndices)
            {
                pendingOperations.Enqueue(new OperationArgs
                {
                    Type = OperationType.Insert,
                    BeatmapSet = sender[i].Detach(),
                    Index = i,
                });
            }

            foreach (int i in changes.NewModifiedIndices)
            {
                pendingOperations.Enqueue(new OperationArgs
                {
                    Type = OperationType.Update,
                    BeatmapSet = sender[i].Detach(),
                    Index = i,
                });
            }
        }

        protected override void Update()
        {
            base.Update();

            // We can't start processing operations until we have finished detaching the initial list.
            if (!loaded.IsSet)
                return;

            processStableReplacement();

            if (pendingOperations.Count == 0)
                return;

            lock (detachedBeatmapSets)
            {
                // If this ever leads to performance issues, we could dequeue a limited number of operations per update frame.
                while (pendingOperations.TryDequeue(out var op))
                {
                    switch (op.Type)
                    {
                        case OperationType.Insert:
                            detachedBeatmapSets.Insert(op.Index, op.BeatmapSet!);
                            break;

                        case OperationType.Update:
                            detachedBeatmapSets.ReplaceRange(op.Index, 1, new[] { op.BeatmapSet! });
                            break;

                        case OperationType.MoveAndReplace:
                            detachedBeatmapSets.Move(op.Index, op.NewIndex!.Value);
                            detachedBeatmapSets.ReplaceRange(op.NewIndex!.Value, 1, [op.BeatmapSet!]);
                            break;

                        case OperationType.Remove:
                            detachedBeatmapSets.RemoveAt(op.Index);
                            break;
                    }
                }
            }
        }

        private void processStableReplacement()
        {
            lock (detachedBeatmapSets)
            {
                if (stableSetsToRemove > 0)
                {
                    int count = Math.Min(stable_sets_per_frame, stableSetsToRemove);
                    detachedBeatmapSets.RemoveRange(detachedBeatmapSets.Count - count, count);
                    activeStableSets.RemoveRange(activeStableSets.Count - count, count);
                    stableSetsToRemove -= count;
                    return;
                }

                if (pendingStableResult != null)
                {
                    StablePathManager.Replace(pendingStableResult.BeatmapPaths, pendingStableResult.AudioPaths, pendingStableResult.BeatmapSets);
                    enqueueStableSets(pendingStableResult.BeatmapSets);
                    pendingStableResult = null;
                }

                if (pendingStableSets.Count == 0)
                    return;

                var batch = new List<BeatmapSetInfo>(stable_sets_per_frame);

                while (batch.Count < stable_sets_per_frame && pendingStableSets.TryDequeue(out var set))
                    batch.Add(set);

                detachedBeatmapSets.AddRange(batch);
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
            public OperationType Type;
            public BeatmapSetInfo? BeatmapSet;
            public int Index;
            public int? NewIndex;
        }

        private enum OperationType
        {
            Insert,
            Update,
            Remove,
            MoveAndReplace,
        }
    }
}
