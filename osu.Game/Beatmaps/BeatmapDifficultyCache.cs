// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Statistics;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Screens.Select;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// A component which performs and acts as a central cache for difficulty calculations of beatmap/ruleset/mod combinations.
    /// Currently not persisted between game sessions.
    /// </summary>
    public partial class BeatmapDifficultyCache : MemoryCachingComponent<BeatmapDifficultyCache.DifficultyCacheLookup, StarDifficulty?>
    {
        // Results retain ruleset-specific attribute graphs. Song-select only needs a small recent window;
        // persisted fork display data covers revisits without keeping hundreds of full calculations alive.
        protected override int MaximumCacheEntries => 96;

        // Previews only need two numbers; never retain difficulty or performance graphs in this tier.
        private const int maximumPreviewEntries = 256;
        private readonly object previewLock = new object();
        private readonly Dictionary<PreviewKey, (StarDifficulty Value, LinkedListNode<PreviewKey> Node)> previewResults = new Dictionary<PreviewKey, (StarDifficulty, LinkedListNode<PreviewKey>)>();
        private readonly LinkedList<PreviewKey> previewOrder = new LinkedList<PreviewKey>();

        // Bounded exact-key identity for measured full/preview operations. Hash collisions
        // are resolved by equality, not treated as evidence of duplicated calculations.
        private static long nextTraceKeyId;
        private readonly object traceKeyLock = new object();
        private readonly Dictionary<PreviewKey, long> traceKeys = new Dictionary<PreviewKey, long>();
        private readonly Queue<PreviewKey> traceKeyOrder = new Queue<PreviewKey>();

        private readonly struct PreviewKey : IEquatable<PreviewKey>
        {
            public readonly Guid BeatmapId;
            public readonly string Checksum;
            private readonly string ruleset;
            private readonly Mod[] orderedMods;

            public PreviewKey(DifficultyCacheLookup lookup)
            {
                BeatmapId = lookup.BeatmapInfo.ID;
                Checksum = lookup.BeatmapInfo.MD5Hash;
                ruleset = lookup.Ruleset.ShortName;
                orderedMods = lookup.OrderedMods;
            }

            public bool Equals(PreviewKey other) => BeatmapId == other.BeatmapId && Checksum == other.Checksum
                                                      && ruleset == other.ruleset && orderedMods.SequenceEqual(other.orderedMods);
            public override bool Equals(object? other) => other is PreviewKey key && Equals(key);
            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(BeatmapId);
                hash.Add(Checksum);
                hash.Add(ruleset);
                foreach (var mod in orderedMods) hash.Add(mod);
                return hash.ToHashCode();
            }
        }

        internal int HeavyCount => CachedEntryCount;
        internal bool HasHeavy(BeatmapInfo beatmap) => CheckExists(new DifficultyCacheLookup(beatmap, beatmap.Ruleset, null), out _);
        internal bool HasLight(BeatmapInfo beatmap) => TryLightPreview(new DifficultyCacheLookup(beatmap, beatmap.Ruleset, null), out _);

        internal int PreviewCount
        {
            get { lock (previewLock) return previewResults.Count; }
        }

        internal bool TryLightPreview(DifficultyCacheLookup lookup, out StarDifficulty result)
        {
            var key = new PreviewKey(lookup);
            lock (previewLock)
            {
                if (previewResults.TryGetValue(key, out var entry))
                {
                    previewOrder.Remove(entry.Node);
                    previewOrder.AddLast(entry.Node);
                    result = entry.Value;
                    return true;
                }
            }
            result = default;
            return false;
        }

        private void storePreview(DifficultyCacheLookup lookup, StarDifficulty result)
        {
            var key = new PreviewKey(lookup);
            // Do not retain attributes, counts, full performance, or a completed computation task.
            var snapshot = new StarDifficulty(result.Stars, result.MaxCombo);
            lock (previewLock)
            {
                if (previewResults.Remove(key, out var old))
                    previewOrder.Remove(old.Node);
                previewResults.Add(key, (snapshot, previewOrder.AddLast(key)));
                while (previewResults.Count > maximumPreviewEntries)
                {
                    var oldest = previewOrder.First!;
                    previewResults.Remove(oldest.Value);
                    previewOrder.RemoveFirst();
                }
            }
        }

        /// <summary>
        /// Lightweight updates for set-header spreads, which must not request calculations of their own.
        /// Always delivered on the update thread and only for native, unmodded difficulty.
        /// </summary>
        internal event Action<Guid, string, double>? NativeDifficultyUpdated;
        internal event Action<DifficultyCacheLookup, StarDifficulty>? SongSelectDifficultyUpdated;
        internal event Action? SongSelectRatingsInvalidated;
        private long songSelectRatingEpoch;

        internal Task<StarDifficulty?> GetSongSelectDifficultyAsync(BeatmapInfo beatmap, RulesetInfo ruleset, Mod[] mods, bool performance, CancellationToken token)
        {
            var lookup = new DifficultyCacheLookup(beatmap, ruleset, mods, performance);
            token.ThrowIfCancellationRequested();
            if (CheckExists(lookup, out var existing) && existing != null)
                return Task.FromResult(existing);
            if (!performance && (TryLightPreview(lookup, out var preview) || TryGetValidatedPreview(lookup, out preview)))
                return Task.FromResult<StarDifficulty?>(preview);
            // Star-only cards reuse the existing preview admission/lane and pending-full
            // joins. PP uses at most two screen workers, never the selected-map lane.
            return performance ? getOrStartFull(lookup, token, compute) : joinFullOrPreview(lookup, token, 0);
            async Task<StarDifficulty?> compute()
            {
                var request = BeginCacheRequest(lookup);
                try
                {
                    var value = await Task.Run(() =>
                    {
                        using var budget = BackgroundCalculationLimiter.Enter(2, 50, "song-select", 2, token, deferWhileInteracting: true);
                        return computeDifficulty(lookup, token, performance);
                    }, token).ConfigureAwait(false);
                    return PublishValue(request, value, token) ? value : null;
                }
                finally { EndCacheRequest(request); }
            }
        }

        internal double GetCachedNativeStarRating(BeatmapInfo beatmap)
            => TryLightPreview(new DifficultyCacheLookup(beatmap, beatmap.Ruleset, null, false), out var preview)
                ? preview.Stars : GetInitialStarRating(beatmap);

        private void notifyNativeDifficulty(BeatmapInfo beatmap, double stars)
        {
            Guid id = beatmap.ID;
            string checksum = beatmap.MD5Hash;
            Schedule(() => NativeDifficultyUpdated?.Invoke(id, checksum, stars));
        }

        protected override void OnValueComputed(DifficultyCacheLookup lookup, StarDifficulty? value)
        {
            if (value != null)
            {
                storePreview(lookup, value.Value);
                var result = value.Value;
                long epoch = Interlocked.Read(ref songSelectRatingEpoch);
                Schedule(() =>
                {
                    if (epoch == Interlocked.Read(ref songSelectRatingEpoch))
                        SongSelectDifficultyUpdated?.Invoke(lookup, result);
                });
                if (lookup.OrderedMods.Length == 0 && lookup.Ruleset.Equals(lookup.BeatmapInfo.Ruleset))
                    notifyNativeDifficulty(lookup.BeatmapInfo, value.Value.Stars);
            }
        }

        public override void Clear() => WithCacheLock(() =>
        {
            invalidateSongSelectRatings();
            lock (pendingFullLock)
            {
                foreach (var pending in pendingFull.Values)
                    pending.Invalidated = true;
                pendingFull.Clear();
            }
            lock (previewLock)
            {
                previewResults.Clear();
                previewOrder.Clear();
            }
            base.Clear();
        });

        // Full selected-map calculations must never wait behind speculative carousel previews.
        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(1, nameof(BeatmapDifficultyCache));
        private readonly ThreadedTaskScheduler previewScheduler = new ThreadedTaskScheduler(1, $"{nameof(BeatmapDifficultyCache)}Preview");
        private readonly ThreadedTaskScheduler versionScheduler = new ThreadedTaskScheduler(1, $"{nameof(BeatmapDifficultyCache)}Version");

        // Calculator versions are fixed for the lifetime of a client process. Resolving the
        // version once avoids loading a working beatmap merely to read a cached display entry.
        private readonly ConcurrentDictionary<string, int> additionalInfoVersions = new ConcurrentDictionary<string, int>();

        private long previewAdmissionDeadline;

        /// <summary>
        /// Wheel/drag/inertial movement postpones speculative neighbour work, not
        /// selected-map calculations or cache hits. Stopwatch is independent of game clocks.
        /// </summary>
        internal void DeferPreviewCalculations(int milliseconds)
            => Interlocked.Exchange(ref previewAdmissionDeadline, Stopwatch.GetTimestamp()
                + (long)(milliseconds * (double)Stopwatch.Frequency / 1000));

        private async Task waitForPreviewAdmission(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double remaining = (Interlocked.Read(ref previewAdmissionDeadline) - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
                if (remaining <= 0)
                    return;
                await Task.Delay((int)Math.Clamp(Math.Ceiling(remaining), 1, 250), cancellationToken).ConfigureAwait(false);
            }
        }

        // Only calculations already requested by a full consumer may serve a preview.
        // Entries are removed on completion/invalidation; no completed result graph is retained here.
        private readonly object pendingFullLock = new object();
        private readonly Dictionary<DifficultyCacheLookup, PendingFullCalculation> pendingFull = new Dictionary<DifficultyCacheLookup, PendingFullCalculation>();
        private bool fullDisposed;

        private sealed class PendingFullCalculation
        {
            public readonly Task<StarDifficulty?> Task;
            public readonly string Checksum;
            public volatile bool Invalidated;
            public readonly CancellationToken OwnerCancellation;

            public PendingFullCalculation(Task<StarDifficulty?> task, string checksum, CancellationToken ownerCancellation)
            {
                Task = task;
                Checksum = checksum;
                OwnerCancellation = ownerCancellation;
            }
        }

        /// <summary>
        /// All bindables that should be updated along with the current ruleset + mods.
        /// </summary>
        private readonly WeakList<BindableStarDifficulty> trackedBindables = new WeakList<BindableStarDifficulty>();

        /// <summary>
        /// Cancellation sources used by tracked bindables.
        /// </summary>
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();

        /// <summary>
        /// Lock to be held when operating on <see cref="trackedBindables"/> or <see cref="linkedCancellationSources"/>.
        /// </summary>
        private readonly Lock bindableUpdateLock = new Lock();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentRuleset.BindValueChanged(_ => Scheduler.AddOnce(updateTrackedBindables));

            currentMods.BindValueChanged(mods =>
            {
                // A change in bindable here doesn't guarantee that mods have actually changed.
                // However, we *do* want to make sure that the mod *references* are the same;
                // `SequenceEqual()` without a comparer would fall back to `IEquatable`.
                // Failing to ensure reference equality can cause setting change tracking to fail later.
                if (mods.OldValue.SequenceEqual(mods.NewValue, ReferenceEqualityComparer.Instance))
                    return;

                modSettingChangeTracker?.Dispose();

                Scheduler.AddOnce(updateTrackedBindables);

                modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
                modSettingChangeTracker.SettingChanged += _ =>
                {
                    lock (bindableUpdateLock)
                    {
                        debouncedModSettingsChange?.Cancel();
                        debouncedModSettingsChange = Scheduler.AddDelayed(updateTrackedBindables, 100);
                    }
                };
            }, true);
        }

        /// <summary>
        /// Notify this cache that a beatmap has been invalidated/updated.
        /// </summary>
        /// <param name="oldBeatmap">The old beatmap model.</param>
        /// <param name="newBeatmap">The updated beatmap model.</param>
        private void invalidateSongSelectRatings()
        {
            Interlocked.Increment(ref songSelectRatingEpoch);
            Schedule(() => SongSelectRatingsInvalidated?.Invoke());
        }

        public void Invalidate(IBeatmapInfo oldBeatmap, IBeatmapInfo newBeatmap)
        {
            invalidateSongSelectRatings();
            WithCacheLock(() =>
            {
                lock (pendingFullLock)
                {
                    foreach (var key in pendingFull.Keys.Where(key => key.BeatmapInfo.Equals(oldBeatmap)).ToArray())
                    {
                        pendingFull[key].Invalidated = true;
                        pendingFull.Remove(key);
                    }
                }
                lock (previewLock)
                {
                    foreach (var key in previewResults.Keys.Where(key => oldBeatmap is BeatmapInfo local && key.BeatmapId == local.ID).ToArray())
                    {
                        previewOrder.Remove(previewResults[key].Node);
                        previewResults.Remove(key);
                    }
                }
                base.Invalidate(lookup => lookup.BeatmapInfo.Equals(oldBeatmap));
            });

            // The library updater can finish without an active difficulty-row consumer.
            if (newBeatmap is BeatmapInfo updated)
                notifyNativeDifficulty(updated, GetInitialStarRating(updated));

            lock (bindableUpdateLock)
            {
                bool trackedBindablesRefreshRequired = false;

                foreach (var bsd in trackedBindables.Where(bsd => bsd.BeatmapInfo.Equals(oldBeatmap)))
                {
                    bsd.BeatmapInfo = newBeatmap;
                    trackedBindablesRefreshRequired = true;
                }

                if (trackedBindablesRefreshRequired)
                    Scheduler.AddOnce(updateTrackedBindables);
            }
        }

        /// <summary>
        /// Invalidates every cached difficulty entry, e.g. when a global input of the
        /// calculation (such as the selected Relax PP system) has changed.
        /// </summary>
        public void InvalidateAll()
        {
            WithCacheLock(() =>
            {
                lock (pendingFullLock)
                {
                    foreach (var pending in pendingFull.Values)
                        pending.Invalidated = true;
                    pendingFull.Clear();
                }
                Clear();
            });

            Scheduler.AddOnce(updateTrackedBindables);
        }

        /// <summary>
        /// Retrieves a bindable containing the star difficulty of a <see cref="BeatmapInfo"/> that follows the currently-selected ruleset and mods.
        /// </summary>
        /// <param name="beatmapInfo">The <see cref="BeatmapInfo"/> to get the difficulty of.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> which stops updating the star difficulty for the given <see cref="BeatmapInfo"/>.</param>
        /// <param name="computationDelay">A delay in milliseconds before performing the calculation.</param>
        /// <param name="calculatePerformance">Whether to also calculate perfect-score performance attributes.</param>
        /// <param name="usePersistedAdditionalInfo">Whether this display request may discover the calculator version and use the validated persisted additional-info cache.</param>
        /// <returns>A bindable that is updated to contain the star difficulty when it becomes available. May be an approximation while in an initial calculating state.</returns>
        public IBindable<StarDifficulty> GetBindableDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0,
                                                               bool calculatePerformance = true, bool usePersistedAdditionalInfo = false)
            => getBindableDifficulty(beatmapInfo, cancellationToken, computationDelay, calculatePerformance, usePersistedAdditionalInfo, null);

        internal (BindableStarDifficulty Difficulty, Task<IReadOnlyList<StarDifficulty.CountStatistic>?> CountStatistics) GetBindableDifficultyWithCountStatistics(
            IBeatmapInfo beatmapInfo, CancellationToken cancellationToken, int computationDelay)
        {
            var completion = new TaskCompletionSource<IReadOnlyList<StarDifficulty.CountStatistic>?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var difficulty = getBindableDifficulty(beatmapInfo, cancellationToken, computationDelay, true, false, completion);
            return (difficulty, completion.Task);
        }

        private BindableStarDifficulty getBindableDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken, int computationDelay,
                                                              bool calculatePerformance, bool usePersistedAdditionalInfo,
                                                              TaskCompletionSource<IReadOnlyList<StarDifficulty.CountStatistic>?>? countCompletion)
        {
            var bindable = new BindableStarDifficulty(beatmapInfo, cancellationToken, computationDelay, calculatePerformance, usePersistedAdditionalInfo)
            {
                Value = getInitialDifficulty(beatmapInfo, currentRuleset.Value, currentMods.Value, calculatePerformance)
            };

            if (beatmapInfo is BeatmapInfo { IsExternalCatalogue: true } remoteBeatmap)
            {
                bindable.Value = new StarDifficulty(remoteBeatmap.StarRating, remoteBeatmap.MaxCombo ?? 0);
                countCompletion?.TrySetResult(null);
                return bindable;
            }

            lock (bindableUpdateLock)
            {
                var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(trackedUpdateCancellationSource.Token, cancellationToken);
                linkedCancellationSources.Add(linkedSource);

                if (countCompletion != null)
                    bindable.TrackCountStatistics = true;
                updateBindable(bindable, currentRuleset.Value, currentMods.Value, linkedSource, computationDelay, countCompletion);

                trackedBindables.Add(bindable);
            }

            return bindable;
        }

        private StarDifficulty getInitialDifficulty(IBeatmapInfo beatmapInfo, RulesetInfo? rulesetInfo, IEnumerable<Mod>? mods, bool calculatePerformance)
        {
            if (beatmapInfo is BeatmapInfo localBeatmap && rulesetInfo != null)
            {
                var requested = new DifficultyCacheLookup(localBeatmap, rulesetInfo, mods, calculatePerformance);
                if (CheckExists(requested, out StarDifficulty? exact) && exact != null)
                    return exact.Value;

                // A full result is also a valid star-only result. Conversely, a star-only result is
                // the best immediate visual seed while selected-map PP is calculated in parallel.
                var alternate = new DifficultyCacheLookup(localBeatmap, rulesetInfo, mods, !calculatePerformance);
                if (TryLightPreview(requested, out var preview))
                    return preview;
                if (CheckExists(alternate, out StarDifficulty? cached) && cached != null)
                    return cached.Value;
            }

            return new StarDifficulty(GetInitialStarRating(beatmapInfo), 0);
        }

        internal static double GetInitialStarRating(IBeatmapInfo beatmapInfo)
        {
            if (beatmapInfo is BeatmapInfo localBeatmapInfo && beatmapInfo.Ruleset.ShortName == RulesetInfo.DODGE_MODE_SHORTNAME)
            {
                double forkStarRating = ForkDataStore.Instance?.GetDodgeDifficulty(localBeatmapInfo.ID).StarRating ?? -1;

                // Keep showing the last successful calculation while a newer
                // Dodge difficulty version is being processed in the background.
                if (forkStarRating >= 0)
                    return forkStarRating;
            }

            return beatmapInfo.StarRating;
        }

        /// <summary>
        /// Retrieves the difficulty of a <see cref="IBeatmapInfo"/>.
        /// </summary>
        /// <param name="beatmapInfo">The <see cref="IBeatmapInfo"/> to get the difficulty of.</param>
        /// <param name="rulesetInfo">The <see cref="IRulesetInfo"/> to get the difficulty with.</param>
        /// <param name="mods">The <see cref="Mod"/>s to get the difficulty with.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> which stops computing the star difficulty.</param>
        /// <param name="computationDelay">In the case a cached lookup was not possible, a value in milliseconds of to wait until performing potentially intensive lookup.</param>
        /// <returns>
        /// The requested <see cref="StarDifficulty"/>, if non-<see langword="null"/>.
        /// A <see langword="null"/> return value indicates that the difficulty process failed or was interrupted early,
        /// and as such there is no usable star difficulty value to be returned.
        /// </returns>
        public virtual Task<StarDifficulty?> GetDifficultyAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                                CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
            rulesetInfo ??= beatmapInfo.Ruleset;

            var localBeatmapInfo = beatmapInfo as BeatmapInfo;
            var localRulesetInfo = rulesetInfo as RulesetInfo;

            // Difficulty can only be computed if the beatmap and ruleset are locally available.
            if (localBeatmapInfo == null || localRulesetInfo == null)
            {
                // If not, fall back to the existing star difficulty (e.g. from an online source).
                return Task.FromResult<StarDifficulty?>(new StarDifficulty(beatmapInfo.StarRating, (beatmapInfo as IBeatmapOnlineInfo)?.MaxCombo ?? 0));
            }

            var lookup = new DifficultyCacheLookup(localBeatmapInfo, localRulesetInfo, mods, true);
            if (computationDelay <= 0 || CheckExists(lookup, out _))
                return getOrStartFull(lookup, cancellationToken, () => GetAsync(lookup, cancellationToken));
            var request = BeginCacheRequest(lookup);
            return delayedFull();

            async Task<StarDifficulty?> delayedFull()
            {
                try
                {
                    // A debounce owns only admission, never the shared calculation lane.
                    await Task.Delay(computationDelay, cancellationToken).ConfigureAwait(false);
                    Task<StarDifficulty?>? admitted = null;
                    WithCacheLock(() =>
                    {
                        if (IsCacheRequestValid(request, cancellationToken))
                            admitted = getOrStartFull(lookup, cancellationToken, () => GetAsync(lookup, cancellationToken));
                    });
                    return admitted == null ? null : await admitted.ConfigureAwait(false);
                }
                finally
                {
                    EndCacheRequest(request);
                }
            }
        }

        private Task<StarDifficulty?> getOrStartFull(DifficultyCacheLookup lookup, CancellationToken token, Func<Task<StarDifficulty?>> start)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled<StarDifficulty?>(token);
            Task<StarDifficulty?> result = Task.FromResult<StarDifficulty?>(null);
            // Same lock order as invalidation: cache -> pending. Publish/register atomically,
            // never start a second decode/convert before discovering equivalent in-flight work.
            WithCacheLock(() =>
            {
                if (fullDisposed)
                    return;
                lock (pendingFullLock)
                {
                    if (pendingFull.TryGetValue(lookup, out var previous) && !previous.Invalidated
                        && previous.Checksum == lookup.BeatmapInfo.MD5Hash && !previous.OwnerCancellation.IsCancellationRequested)
                    {
                        result = join(previous);
                        return;
                    }
                    Task<StarDifficulty?> task = start();
                    result = task;
                    if (task.IsCompleted)
                        return;
                    pendingFull[lookup] = new PendingFullCalculation(task, lookup.BeatmapInfo.MD5Hash, token);
                    _ = task.ContinueWith(_ =>
                    {
                        lock (pendingFullLock)
                        {
                            if (pendingFull.TryGetValue(lookup, out var current) && ReferenceEquals(current.Task, task))
                                pendingFull.Remove(lookup);
                        }
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            });
            return result;

            async Task<StarDifficulty?> join(PendingFullCalculation pending)
            {
                // Every independent waiter remains invalidation-tracked even if a canceled
                // first owner is replaced in pendingFull before its worker actually stops.
                var waiter = BeginCacheRequest(lookup);
                try
                {
                    try
                    {
                        var value = await pending.Task.WaitAsync(token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        if (pending.Invalidated || !IsCacheRequestValid(waiter, token))
                            return null;
                        if (value != null || !pending.OwnerCancellation.IsCancellationRequested)
                            return value;
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested && pending.OwnerCancellation.IsCancellationRequested)
                    {
                        // The first consumer's cancellation must not poison an independent waiter.
                    }
                    token.ThrowIfCancellationRequested();
                    Task<StarDifficulty?>? retry = null;
                    WithCacheLock(() =>
                    {
                        // Admission is atomic with invalidation, including an orphaned old
                        // owner whose dictionary slot has already been reused by new work.
                        if (!pending.Invalidated && IsCacheRequestValid(waiter, token))
                            retry = getOrStartFull(lookup, token, start);
                    });
                    return retry == null ? null : await retry.ConfigureAwait(false);
                }
                finally
                {
                    EndCacheRequest(waiter);
                }
            }
        }

        private async Task<StarDifficulty?> joinFullOrPreview(DifficultyCacheLookup previewLookup, CancellationToken cancellationToken, int computationDelay)
        {
            var fullLookup = new DifficultyCacheLookup(previewLookup.BeatmapInfo, previewLookup.Ruleset, previewLookup.OrderedMods, true);
            PendingFullCalculation? pendingFullCalculation = null;
            lock (pendingFullLock)
            {
                if (pendingFull.TryGetValue(fullLookup, out var pending)
                    && pending.Checksum == previewLookup.BeatmapInfo.MD5Hash)
                    pendingFullCalculation = pending;
            }

            if (pendingFullCalculation != null)
            {
                try
                {
                    // The preview owns only its wait, never the selected-map calculation.
                    var full = await pendingFullCalculation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    lock (pendingFullLock)
                    {
                        if (!pendingFullCalculation.Invalidated && full?.DifficultyAttributes != null
                            && fullLookup.BeatmapInfo.MD5Hash == previewLookup.BeatmapInfo.MD5Hash)
                            return full;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    // A cancelled/faulted selected request must not suppress an active preview.
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (TryLightPreview(previewLookup, out var light))
                return light;
            // A selected-map request may have arrived during the preview's admission delay.
            // Recheck before converting; joining only before the delay misses this common race.
            if (computationDelay > 0)
            {
                // Task.Delay follows timer resolution and can wake just before the
                // requested duration. Keep admission on the same monotonic clock as scrolling.
                long delayStart = Stopwatch.GetTimestamp();
                double remaining = computationDelay;
                do
                {
                    await Task.Delay((int)Math.Ceiling(remaining), cancellationToken).ConfigureAwait(false);
                    remaining = computationDelay - Stopwatch.GetElapsedTime(delayStart).TotalMilliseconds;
                } while (remaining > 0);
            }
            await waitForPreviewAdmission(cancellationToken).ConfigureAwait(false);
            lock (pendingFullLock)
            {
                if (pendingFull.TryGetValue(fullLookup, out var pending)
                    && !pending.Invalidated && pending.Checksum == previewLookup.BeatmapInfo.MD5Hash)
                    pendingFullCalculation = pending;
                else
                    pendingFullCalculation = null;
            }
            if (pendingFullCalculation != null)
            {
                try
                {
                    var full = await pendingFullCalculation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    lock (pendingFullLock)
                    {
                        if (!pendingFullCalculation.Invalidated && full?.DifficultyAttributes != null)
                            return full;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    // The preview has an independent owner and must survive a failed full request.
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (CheckExists(fullLookup, out StarDifficulty? completedFull) && completedFull?.DifficultyAttributes != null)
                return completedFull;
            if (TryLightPreview(previewLookup, out light))
                return light;
            return await GetAsync(previewLookup, cancellationToken).ConfigureAwait(false);
        }

        protected override Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken cancellationToken = default)
            => computeValueAsync(lookup, cancellationToken);

        private Task<StarDifficulty?> computeValueAsync(DifficultyCacheLookup lookup, CancellationToken cancellationToken, PendingFullCalculation? failedFull = null)
        {
            return Task.Factory.StartNew(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CheckExists(lookup, out var existing))
                    return Task.FromResult(existing);

                if (!lookup.CalculatePerformance)
                {
                    // Admission can precede a full request while this preview waits behind
                    // another conversion. Recheck at the worker boundary before decoding.
                    var fullLookup = new DifficultyCacheLookup(lookup.BeatmapInfo, lookup.Ruleset, lookup.OrderedMods, true);
                    if (CheckExists(fullLookup, out StarDifficulty? completed) && completed?.DifficultyAttributes != null)
                        return Task.FromResult<StarDifficulty?>(completed);
                    PendingFullCalculation? full = null;
                    lock (pendingFullLock)
                    {
                        if (pendingFull.TryGetValue(fullLookup, out var pending) && !pending.Invalidated && !ReferenceEquals(pending, failedFull)
                            && !pending.OwnerCancellation.IsCancellationRequested && pending.Checksum == lookup.BeatmapInfo.MD5Hash)
                            full = pending;
                    }
                    if (full != null)
                        return waitForFull(full);
                    // Movement can resume after admission while this request is queued.
                    // Release the worker while waiting, rather than sleeping in its lane.
                    if (Interlocked.Read(ref previewAdmissionDeadline) > Stopwatch.GetTimestamp())
                        return deferAndRetry();
                }

                return Task.FromResult(computeDifficulty(lookup, cancellationToken, lookup.CalculatePerformance));
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously,
                lookup.CalculatePerformance ? updateScheduler : previewScheduler).Unwrap();

            async Task<StarDifficulty?> deferAndRetry()
            {
                await waitForPreviewAdmission(cancellationToken).ConfigureAwait(false);
                return await computeValueAsync(lookup, cancellationToken, failedFull).ConfigureAwait(false);
            }

            async Task<StarDifficulty?> waitForFull(PendingFullCalculation full)
            {
                try
                {
                    var value = await full.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (full.Invalidated)
                        return null;
                    if (value?.DifficultyAttributes != null)
                        return value;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Preview remains independently owned after a failed/cancelled full.
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (full.Invalidated)
                    return null;
                // Re-admit on the preview lane. Do not repeatedly rejoin the same failed
                // task if its dictionary-removal continuation has not run yet.
                return await computeValueAsync(lookup, cancellationToken, full).ConfigureAwait(false);
            }
        }

        protected override bool CacheNullValues => false;

        public Task<List<TimedDifficultyAttributes>> GetTimedDifficultyAttributesAsync(IWorkingBeatmap beatmap, Ruleset ruleset, Mod[] mods, CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(() => ruleset.CreateDifficultyCalculator(beatmap).CalculateTimed(mods, cancellationToken),
                cancellationToken,
                TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously,
                updateScheduler);
        }

        /// <summary>
        /// Updates all tracked <see cref="BindableStarDifficulty"/> using the current ruleset and mods.
        /// </summary>
        private void updateTrackedBindables()
        {
            lock (bindableUpdateLock)
            {
                cancelTrackedBindableUpdate();

                foreach (var b in trackedBindables)
                {
                    var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(trackedUpdateCancellationSource.Token, b.CancellationToken);
                    linkedCancellationSources.Add(linkedSource);

                    // Refreshing visible neighbour previews must not discard their original admission
                    // debounce. Full selected-map requests use the high-priority full lane immediately.
                    updateBindable(b, currentRuleset.Value, currentMods.Value, linkedSource,
                        b.CalculatePerformance ? 0 : b.ComputationDelay);
                }
            }
        }

        /// <summary>
        /// Cancels the existing update of all tracked <see cref="BindableStarDifficulty"/> via <see cref="updateTrackedBindables"/>.
        /// </summary>
        private void cancelTrackedBindableUpdate()
        {
            lock (bindableUpdateLock)
            {
                debouncedModSettingsChange?.Cancel();
                debouncedModSettingsChange = null;

                trackedUpdateCancellationSource.Cancel();
                trackedUpdateCancellationSource = new CancellationTokenSource();

                foreach (var c in linkedCancellationSources)
                    c.Dispose();

                linkedCancellationSources.Clear();
            }
        }

        /// <summary>
        /// Updates the value of a <see cref="BindableStarDifficulty"/> with a given ruleset + mods.
        /// </summary>
        /// <param name="bindable">The <see cref="BindableStarDifficulty"/> to update.</param>
        /// <param name="rulesetInfo">The <see cref="IRulesetInfo"/> to update with.</param>
        /// <param name="mods">The <see cref="Mod"/>s to update with.</param>
        /// <param name="linkedCancellationTokenSource">
        /// A cancellation token source that may be used to cancel this update.
        /// This token will be cancelled in one of two scenarios:
        /// <list type="bullet">
        /// <item>The owner of the bindable has requested the cancellation.</item>
        /// <item>An <see cref="Invalidate"/> call has been issued, and as such ongoing calculations must be aborted to avoid stale values being potentially written to bindables.</item>
        /// </list>
        /// </param>
        /// <param name="computationDelay">In the case a cached lookup was not possible, a value in milliseconds of to wait until performing potentially intensive lookup.</param>
        private void updateBindable(BindableStarDifficulty bindable, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods, CancellationTokenSource linkedCancellationTokenSource,
                                    int computationDelay = 0, TaskCompletionSource<IReadOnlyList<StarDifficulty.CountStatistic>?>? countCompletion = null)
        {
            if (bindable.TrackCountStatistics && countCompletion == null)
            {
                countCompletion = new TaskCompletionSource<IReadOnlyList<StarDifficulty.CountStatistic>?>(TaskCreationOptions.RunContinuationsAsynchronously);
                bindable.NotifyCountStatisticsRequested(countCompletion.Task);
            }

            // Each completion belongs to this exact ruleset/mod request; replacements notify
            // the wedge without initiating an extra difficulty calculation.
            // GetDifficultyAsync will fall back to existing data from IBeatmapInfo if not locally available
            // (contrary to GetAsync)
            getBindableDifficultyAsync(bindable, rulesetInfo, mods, linkedCancellationTokenSource.Token, computationDelay)
                .ContinueWith(task =>
                    {
                        // Rapid carousel/card recycling intentionally cancels most speculative
                        // difficulty requests. Those requests do not need to return to the update
                        // thread because no bindable value will be published. Scheduling their
                        // cleanup one-by-one can otherwise leave thousands of no-op delegates in
                        // this component's scheduler while entering gameplay.
                        if (linkedCancellationTokenSource.IsCancellationRequested)
                        {
                            // Observe a possible fault before dropping the completed task.
                            _ = task.Exception;
                            countCompletion?.TrySetCanceled();
                            cleanupLinkedCancellationSource(linkedCancellationTokenSource);
                            return;
                        }

                        // We're on a threadpool thread, but we should exit back to the update thread so consumers can safely handle value-changed events.
                        Schedule(() =>
                        {
                            if (!linkedCancellationTokenSource.IsCancellationRequested)
                            {
                                StarDifficulty? starDifficulty = null;
                                try
                                {
                                    starDifficulty = task.GetResultSafely();
                                }
                                catch (OperationCanceledException)
                                {
                                    countCompletion?.TrySetCanceled();
                                }
                                catch (Exception exception)
                                {
                                    Logger.Error(exception, "Failed to update beatmap difficulty");
                                }

                                if (starDifficulty != null)
                                    bindable.Value = starDifficulty.Value;

                                // Only unmodded playable counts can be reused by the wedge's unmodded display.
                                countCompletion?.TrySetResult(mods?.Any() == true ? null : starDifficulty?.CountStatistics);
                            }
                            else
                                countCompletion?.TrySetCanceled();

                            // Once the linked cancellation token source is of no remaining use to anybody, clean it up.
                            cleanupLinkedCancellationSource(linkedCancellationTokenSource);
                        });
                    },
                    // This continuation MUST run even if the antecedent `GetDifficultyAsync()` call was canceled in order to clean up `linkedCancellationTokenSource`.
                    // Due to this, `ContinueWith()` CANNOT accept `linkedCancellationTokenSource.Token` here, because if it did, then in an event of a cancellation,
                    // the continuation would never be scheduled for execution.
                    CancellationToken.None);
        }

        private void cleanupLinkedCancellationSource(CancellationTokenSource linkedCancellationTokenSource)
        {
            lock (bindableUpdateLock)
            {
                linkedCancellationSources.Remove(linkedCancellationTokenSource);
                linkedCancellationTokenSource.Dispose();
            }
        }

        /// <summary>
        /// Computes the difficulty defined by a <see cref="DifficultyCacheLookup"/> key, and stores it to the timed cache.
        /// </summary>
        /// <param name="key">The <see cref="DifficultyCacheLookup"/> that defines the computation parameters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The <see cref="StarDifficulty"/>.</returns>
        private Task<StarDifficulty?> getBindableDifficultyAsync(BindableStarDifficulty bindable, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods,
                                                                  CancellationToken cancellationToken, int computationDelay)
        {
            if (bindable.UsePersistedAdditionalInfo && bindable.CalculatePerformance
                && bindable.BeatmapInfo is BeatmapInfo beatmap && rulesetInfo is RulesetInfo ruleset)
                return GetAdditionalInfoDifficultyAsync(beatmap, ruleset, mods, cancellationToken, computationDelay);

            if (bindable.CalculatePerformance)
                return GetDifficultyAsync(bindable.BeatmapInfo, rulesetInfo, mods, cancellationToken, computationDelay);

            rulesetInfo ??= bindable.BeatmapInfo.Ruleset;
            if (bindable.BeatmapInfo is not BeatmapInfo localBeatmapInfo || rulesetInfo is not RulesetInfo localRulesetInfo)
                return Task.FromResult<StarDifficulty?>(new StarDifficulty(bindable.BeatmapInfo.StarRating,
                    (bindable.BeatmapInfo as IBeatmapOnlineInfo)?.MaxCombo ?? 0));

            var fullLookup = new DifficultyCacheLookup(localBeatmapInfo, localRulesetInfo, mods, true);
            if (CheckExists(fullLookup, out StarDifficulty? full) && full != null)
                return Task.FromResult<StarDifficulty?>(full);

            var lookup = new DifficultyCacheLookup(localBeatmapInfo, localRulesetInfo, mods, false);
            if (TryLightPreview(lookup, out var light))
                return Task.FromResult<StarDifficulty?>(light);

            if (TryGetValidatedPreview(lookup, out var preview))
                return Task.FromResult<StarDifficulty?>(preview);

            return bindable.UsePersistedAdditionalInfo
                ? resolvePreviewAsync(lookup, cancellationToken, computationDelay)
                : joinFullOrPreview(lookup, cancellationToken, computationDelay);
        }

        private async Task<StarDifficulty?> resolvePreviewAsync(DifficultyCacheLookup lookup, CancellationToken cancellationToken, int computationDelay)
        {
            // A fresh process must be able to use persisted SR before any selected
            // map was calculated. Discover the version without decoding the map.
            if (!additionalInfoVersions.ContainsKey(lookup.Ruleset.ShortName))
            {
                await Task.Factory.StartNew(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (additionalInfoVersions.ContainsKey(lookup.Ruleset.ShortName))
                        return;
                    var working = beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo);
                    int version = lookup.Ruleset.CreateInstance().CreateDifficultyCalculator(working).Version;
                    if (version > 0)
                        additionalInfoVersions.TryAdd(lookup.Ruleset.ShortName, version);
                }, cancellationToken, TaskCreationOptions.HideScheduler, versionScheduler).ConfigureAwait(false);
            }
            var fullLookup = new DifficultyCacheLookup(lookup.BeatmapInfo, lookup.Ruleset, lookup.OrderedMods, true);
            if (CheckExists(fullLookup, out StarDifficulty? full) && full != null)
                return full;
            if (TryGetValidatedPreview(lookup, out var persisted))
                return persisted;
            return await joinFullOrPreview(lookup, cancellationToken, computationDelay).ConfigureAwait(false);
        }

        // Only full persisted results carry a checksum and calculator version. Never treat the
        // unversioned BeatmapInfo.StarRating (including stable imports) as an exact preview.
        internal bool TryGetValidatedPreview(in DifficultyCacheLookup lookup, out StarDifficulty preview)
        {
            preview = default;
            if (lookup.CalculatePerformance
                || !lookup.Ruleset.Equals(lookup.BeatmapInfo.Ruleset)
                || !additionalInfoVersions.TryGetValue(lookup.Ruleset.ShortName, out int version)
                || version <= 0)
                return false;

            var store = ForkDataStore.Instance;
            if (store?.TryGetAdditionalInfo(lookup.BeatmapInfo.ID, lookup.Ruleset.ShortName,
                    ForkDataStore.CreateAdditionalInfoModsKey(lookup.Ruleset.ShortName, lookup.OrderedMods),
                    lookup.BeatmapInfo.MD5Hash, version, out var info) != true)
                return false;

            preview = new StarDifficulty(info.Stars, info.MaxCombo, info.Text);
            return true;
        }

        internal Task<StarDifficulty?> GetAdditionalInfoDifficultyAsync(BeatmapInfo beatmap, RulesetInfo ruleset, IEnumerable<Mod>? mods,
                                                                        CancellationToken cancellationToken, int computationDelay)
        {
            var lookup = new DifficultyCacheLookup(beatmap, ruleset, mods, true);
            // A persisted-only hit is not a full attributes result, and a debounce is not
            // active calculation work. Keep each caller's wait/admission independent; share
            // only the real full calculation once a persisted miss is admitted below.
            var request = BeginCacheRequest(lookup);
            return getAdditionalInfoDifficultyAsync(beatmap, ruleset, lookup, request, cancellationToken, computationDelay);
        }

        private async Task<StarDifficulty?> getAdditionalInfoDifficultyAsync(BeatmapInfo beatmap, RulesetInfo ruleset, DifficultyCacheLookup lookup,
                                                                             CacheRequest request, CancellationToken cancellationToken, int computationDelay)
        {
            try
            {
            string modsKey = ForkDataStore.CreateAdditionalInfoModsKey(ruleset.ShortName, lookup.OrderedMods);
            var store = ForkDataStore.Instance;
            if (additionalInfoVersions.TryGetValue(ruleset.ShortName, out int knownVersion)
                && store?.TryGetAdditionalInfo(beatmap.ID, ruleset.ShortName, modsKey, beatmap.MD5Hash, knownVersion, out var known) == true)
                return new StarDifficulty(known.Stars, known.MaxCombo, known.Text);

            // Version discovery and the first persisted lookup stay on the bounded worker.
            var (cachedDifficulty, version) = await Task.Factory.StartNew(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                int version;

                if (!additionalInfoVersions.TryGetValue(ruleset.ShortName, out version))
                {
                    var working = beatmapManager.GetWorkingBeatmap(beatmap);
                    version = ruleset.CreateInstance().CreateDifficultyCalculator(working).Version;
                    if (version > 0)
                        additionalInfoVersions.TryAdd(ruleset.ShortName, version);
                }

                // Do not delay cache hits, including the first one after starting the client.
                if (version > 0 && store?.TryGetAdditionalInfo(beatmap.ID, ruleset.ShortName, modsKey, beatmap.MD5Hash, version, out var cached) == true)
                    return ((StarDifficulty?)new StarDifficulty(cached.Stars, cached.MaxCombo, cached.Text), version);

                return ((StarDifficulty?)null, version);
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler).ConfigureAwait(false);

            if (cachedDifficulty != null)
                return IsCacheRequestValid(request, cancellationToken) ? cachedDifficulty : null;

            // A debounce is only waiting; it must not hold the single calculation worker.
            if (computationDelay > 0)
                await Task.Delay(computationDelay, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            Task<StarDifficulty?>? computation = null;
            WithCacheLock(() =>
            {
                // Admission and the alternate request's validity must be atomic with
                // invalidation: an obsolete debounce must not start a fresh cache generation.
                if (IsCacheRequestValid(request, cancellationToken))
                    computation = getOrStartFull(lookup, cancellationToken, () => GetAsync(lookup, cancellationToken));
            });
            if (computation == null)
                return null;
            var calculated = await computation.ConfigureAwait(false);
            return await Task.Factory.StartNew(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                // The in-memory result is invalidation-ordered. Keep Realm writes off the cache lock.
                if (!PublishValue(request, calculated, cancellationToken))
                    return null;

                if (calculated?.PerformanceAttributes != null && calculated.Value.DifficultyAttributes != null && version > 0
                    && IsCacheRequestValid(request, cancellationToken))
                {
                    var info = calculated.Value;
                    store?.SetAdditionalInfo(beatmap.ID, ruleset.ShortName, modsKey,
                        new ForkDataStore.AdditionalInfoData(beatmap.MD5Hash, version, info.Stars, info.MaxCombo,
                            BeatmapAdditionalInfoFormatter.Format(info)));
                }

                return calculated;
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler).ConfigureAwait(false);
            }
            finally
            {
                EndCacheRequest(request);
            }
        }

        protected virtual StarDifficulty? computeDifficulty(in DifficultyCacheLookup key, CancellationToken cancellationToken = default, bool calculatePerformance = true)
        {
            using var operation = StutterTraceEventSource.Log.Measure(calculatePerformance ? "DifficultyCache.ComputeFull" : "DifficultyCache.ComputePreview", getTraceKeyId(key));
            // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
            var beatmapInfo = key.BeatmapInfo;
            var rulesetInfo = key.Ruleset;

            try
            {
                // ThreadedTaskScheduler can dequeue a task just before its owner cancels.
                // Do not decode/convert an obsolete request in that window.
                cancellationToken.ThrowIfCancellationRequested();
                var ruleset = rulesetInfo.CreateInstance();
                Debug.Assert(ruleset != null);

                PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(key.BeatmapInfo));
                cancellationToken.ThrowIfCancellationRequested();
                IBeatmap playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, key.OrderedMods, cancellationToken);

                var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
                difficultyCalculator.PreparePerformanceCalculation = calculatePerformance;
                var difficulty = difficultyCalculator.Calculate(key.OrderedMods, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (key.OrderedMods.Length == 0 && rulesetInfo.ShortName == RulesetInfo.DODGE_MODE_SHORTNAME)
                {
                    ForkDataStore.Instance?.SetDodgeDifficulty(
                        beatmapInfo.ID,
                        beatmapInfo.MD5Hash,
                        difficulty,
                        difficultyCalculator.Version);
                }

                var performanceCalculator = ruleset.CreatePerformanceCalculator();
                if (!calculatePerformance)
                    return new StarDifficulty(difficulty);

                if (performanceCalculator == null)
                    return new StarDifficulty(difficulty, new PerformanceAttributes());

                ScoreProcessor scoreProcessor = ruleset.CreateScoreProcessor();
                scoreProcessor.Mods.Value = key.OrderedMods;
                using (StutterTraceEventSource.Log.Measure("DifficultyCache.AutoplayScore", playableBeatmap.HitObjects.Count))
                    scoreProcessor.ApplyBeatmap(playableBeatmap, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                ScoreInfo perfectScore = new ScoreInfo(key.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Passed = true,
                    Accuracy = 1,
                    Mods = key.OrderedMods,
                    MaxCombo = scoreProcessor.MaximumCombo,
                    Combo = scoreProcessor.MaximumCombo,
                    TotalScore = scoreProcessor.MaximumTotalScore,
                    Statistics = scoreProcessor.MaximumStatistics,
                    MaximumStatistics = scoreProcessor.MaximumStatistics
                };

                var performance = performanceCalculator.Calculate(perfectScore, difficulty);
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<StarDifficulty.CountStatistic>? counts = null;
                if (key.OrderedMods.Length == 0)
                    counts = playableBeatmap.GetStatistics()
                                            .Select(s => new StarDifficulty.CountStatistic(s.Name, s.BarDisplayLength, s.Content)).ToArray();

                // Counting may enumerate every hit object; invalidation can occur during that work.
                cancellationToken.ThrowIfCancellationRequested();
                return new StarDifficulty(difficulty, performance, counts);
            }
            catch (OperationCanceledException)
            {
                // no need to log, cancellations are expected as part of normal operation.
                return null;
            }
            catch (BeatmapInvalidForRulesetException invalidForRuleset)
            {
                if (rulesetInfo.Equals(beatmapInfo.Ruleset))
                    Logger.Error(invalidForRuleset, $"Failed to convert {beatmapInfo.OnlineID} to the beatmap's default ruleset ({beatmapInfo.Ruleset}).");

                return null;
            }
            catch (Exception unknownException)
            {
                Logger.Error(unknownException, "Failed to calculate beatmap difficulty");

                return null;
            }
        }

        private long getTraceKeyId(DifficultyCacheLookup lookup)
        {
            lock (traceKeyLock)
            {
                if (!StutterTraceEventSource.Log.IsEnabled())
                {
                    traceKeys.Clear();
                    traceKeyOrder.Clear();
                    return -1;
                }
                var key = new PreviewKey(lookup);
                if (traceKeys.TryGetValue(key, out long existing))
                    return existing;
                if (traceKeys.Count >= maximumPreviewEntries)
                    traceKeys.Remove(traceKeyOrder.Dequeue());
                long id = Interlocked.Increment(ref nextTraceKeyId);
                traceKeys.Add(key, id);
                traceKeyOrder.Enqueue(key);
                return id;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            WithCacheLock(() =>
            {
                fullDisposed = true;
                Clear();
            });
            modSettingChangeTracker?.Dispose();

            cancelTrackedBindableUpdate();
            updateScheduler.Dispose();
            previewScheduler.Dispose();
            versionScheduler.Dispose();
        }

        public readonly struct DifficultyCacheLookup : IEquatable<DifficultyCacheLookup>
        {
            public readonly BeatmapInfo BeatmapInfo;
            public readonly RulesetInfo Ruleset;
            public readonly Mod[] OrderedMods;
            public readonly bool CalculatePerformance;

            public DifficultyCacheLookup(BeatmapInfo beatmapInfo, RulesetInfo? ruleset, IEnumerable<Mod>? mods, bool calculatePerformance = true)
            {
                BeatmapInfo = beatmapInfo;
                // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
                Ruleset = ruleset ?? BeatmapInfo.Ruleset;
                OrderedMods = mods?.OrderBy(m => m.Acronym).Select(mod => mod.DeepClone()).ToArray() ?? Array.Empty<Mod>();
                CalculatePerformance = calculatePerformance;
            }

            public bool Equals(DifficultyCacheLookup other)
                => BeatmapInfo.Equals(other.BeatmapInfo)
                   && Ruleset.Equals(other.Ruleset)
                   && CalculatePerformance == other.CalculatePerformance
                   && OrderedMods.SequenceEqual(other.OrderedMods);

            public override int GetHashCode()
            {
                var hashCode = new HashCode();

                hashCode.Add(BeatmapInfo.ID);
                hashCode.Add(Ruleset.ShortName);
                hashCode.Add(CalculatePerformance);

                foreach (var mod in OrderedMods)
                    hashCode.Add(mod);

                return hashCode.ToHashCode();
            }
        }

        internal class BindableStarDifficulty : Bindable<StarDifficulty>
        {
            internal bool TrackCountStatistics;
            internal event Action<Task<IReadOnlyList<StarDifficulty.CountStatistic>?>>? CountStatisticsRequested;
            internal void NotifyCountStatisticsRequested(Task<IReadOnlyList<StarDifficulty.CountStatistic>?> task) => CountStatisticsRequested?.Invoke(task);
            public IBeatmapInfo BeatmapInfo;
            public readonly CancellationToken CancellationToken;
            public readonly int ComputationDelay;
            public readonly bool CalculatePerformance;
            public readonly bool UsePersistedAdditionalInfo;

            public BindableStarDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken, int computationDelay, bool calculatePerformance, bool usePersistedAdditionalInfo)
            {
                BeatmapInfo = beatmapInfo;
                CancellationToken = cancellationToken;
                ComputationDelay = computationDelay;
                CalculatePerformance = calculatePerformance;
                UsePersistedAdditionalInfo = usePersistedAdditionalInfo;
            }
        }

        /// <summary>
        /// A working beatmap that caches its playable representation.
        /// This is intended as single-use for when it is guaranteed that the playable beatmap can be reused.
        /// </summary>
        private class PlayableCachedWorkingBeatmap : IWorkingBeatmap
        {
            private readonly IWorkingBeatmap working;
            private IBeatmap? playable;

            public PlayableCachedWorkingBeatmap(IWorkingBeatmap working)
            {
                this.working = working;
            }

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod>? mods = null)
                => playable ??= working.GetPlayableBeatmap(ruleset, mods);

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
                => playable ??= working.GetPlayableBeatmap(ruleset, mods, cancellationToken);

            IBeatmapInfo IWorkingBeatmap.BeatmapInfo => working.BeatmapInfo;
            bool IWorkingBeatmap.BeatmapLoaded => working.BeatmapLoaded;
            bool IWorkingBeatmap.TrackLoaded => working.TrackLoaded;
            IBeatmap IWorkingBeatmap.Beatmap => working.Beatmap;
            Texture? IWorkingBeatmap.GetBackground() => working.GetBackground();
            Texture? IWorkingBeatmap.GetLegacyPreviewBackground() => working.GetLegacyPreviewBackground();
            Texture? IWorkingBeatmap.GetPanelBackground() => working.GetPanelBackground();
            Texture? IWorkingBeatmap.GetPanelBackground(int resolutionPercent) => working.GetPanelBackground(resolutionPercent);
            Texture? IWorkingBeatmap.GetLegacyPreviewBackground(int resolutionPercent) => working.GetLegacyPreviewBackground(resolutionPercent);
            Texture? IWorkingBeatmap.GetClassicPreviewBackground(int resolutionPercent) => working.GetClassicPreviewBackground(resolutionPercent);
            Waveform IWorkingBeatmap.Waveform => working.Waveform;
            Storyboard IWorkingBeatmap.Storyboard => working.Storyboard;
            ISkin IWorkingBeatmap.Skin => working.Skin;
            Track IWorkingBeatmap.Track => working.Track;
            Track IWorkingBeatmap.LoadTrack() => working.LoadTrack();
            Stream? IWorkingBeatmap.GetStream(string storagePath) => working.GetStream(storagePath);
            void IWorkingBeatmap.BeginAsyncLoad() => working.BeginAsyncLoad();
            void IWorkingBeatmap.CancelAsyncLoad() => working.CancelAsyncLoad();
            void IWorkingBeatmap.PrepareTrackForPreview(bool looping, double? offsetFromPreviewPoint) => working.PrepareTrackForPreview(looping, offsetFromPreviewPoint);
        }
    }
}
