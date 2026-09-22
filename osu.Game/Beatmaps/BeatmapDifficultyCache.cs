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
        // Full performance entries may retain ruleset-specific prepared data, while star-only entries are cheap.
        // Keep enough history for ordinary song-select browsing without making the cache effectively unbounded.
        protected override int MaximumCacheEntries => 512;

        // Full selected-map calculations must never wait behind speculative carousel previews.
        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(1, nameof(BeatmapDifficultyCache));
        private readonly ThreadedTaskScheduler previewScheduler = new ThreadedTaskScheduler(1, $"{nameof(BeatmapDifficultyCache)}Preview");

        // Calculator versions are fixed for the lifetime of a client process. Resolving the
        // version once avoids loading a working beatmap merely to read a cached display entry.
        private readonly ConcurrentDictionary<string, int> additionalInfoVersions = new ConcurrentDictionary<string, int>();

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
        public void Invalidate(IBeatmapInfo oldBeatmap, IBeatmapInfo newBeatmap)
        {
            base.Invalidate(lookup => lookup.BeatmapInfo.Equals(oldBeatmap));

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
            base.Invalidate(_ => true);

            Scheduler.AddOnce(updateTrackedBindables);
        }

        /// <summary>
        /// Retrieves a bindable containing the star difficulty of a <see cref="BeatmapInfo"/> that follows the currently-selected ruleset and mods.
        /// </summary>
        /// <param name="beatmapInfo">The <see cref="BeatmapInfo"/> to get the difficulty of.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> which stops updating the star difficulty for the given <see cref="BeatmapInfo"/>.</param>
        /// <param name="computationDelay">A delay in milliseconds before performing the calculation.</param>
        /// <param name="calculatePerformance">Whether to also calculate perfect-score performance attributes.</param>
        /// <param name="usePersistedAdditionalInfo">Whether an unmodded calculation may use the persisted additional-info cache.</param>
        /// <returns>A bindable that is updated to contain the star difficulty when it becomes available. May be an approximation while in an initial calculating state.</returns>
        public IBindable<StarDifficulty> GetBindableDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0,
                                                               bool calculatePerformance = true, bool usePersistedAdditionalInfo = false)
        {
            var bindable = new BindableStarDifficulty(beatmapInfo, cancellationToken, calculatePerformance, usePersistedAdditionalInfo)
            {
                Value = getInitialDifficulty(beatmapInfo, currentRuleset.Value, currentMods.Value, calculatePerformance)
            };

            lock (bindableUpdateLock)
            {
                var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(trackedUpdateCancellationSource.Token, cancellationToken);
                linkedCancellationSources.Add(linkedSource);

                updateBindable(bindable, currentRuleset.Value, currentMods.Value, linkedSource, computationDelay);

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

            return GetAsync(new DifficultyCacheLookup(localBeatmapInfo, localRulesetInfo, mods, true), cancellationToken, computationDelay);
        }

        protected override Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(() =>
            {
                if (CheckExists(lookup, out var existing))
                    return existing;

                return computeDifficulty(lookup, cancellationToken, lookup.CalculatePerformance);
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously,
                lookup.CalculatePerformance ? updateScheduler : previewScheduler);
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

                    updateBindable(b, currentRuleset.Value, currentMods.Value, linkedSource);
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
        private void updateBindable(BindableStarDifficulty bindable, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods, CancellationTokenSource linkedCancellationTokenSource, int computationDelay = 0)
        {
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
                            cleanupLinkedCancellationSource(linkedCancellationTokenSource);
                            return;
                        }

                        // We're on a threadpool thread, but we should exit back to the update thread so consumers can safely handle value-changed events.
                        Schedule(() =>
                        {
                            if (!linkedCancellationTokenSource.IsCancellationRequested)
                            {
                                StarDifficulty? starDifficulty = task.GetResultSafely();

                                if (starDifficulty != null)
                                    bindable.Value = starDifficulty.Value;
                            }

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
        /// Computes the difficulty for a bindable star difficulty instance, and stores it to the timed cache.
        /// </summary>
        /// <param name="bindable">The bindable star difficulty to compute.</param>
        /// <param name="rulesetInfo">The ruleset to compute difficulty for.</param>
        /// <param name="mods">The mods to compute difficulty with.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="computationDelay">Delay before computing difficulty.</param>
        /// <returns>The <see cref="StarDifficulty"/>.</returns>
        private Task<StarDifficulty?> getBindableDifficultyAsync(BindableStarDifficulty bindable, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods,
                                                                  CancellationToken cancellationToken, int computationDelay)
        {
            if (bindable.UsePersistedAdditionalInfo && bindable.CalculatePerformance
                && bindable.BeatmapInfo is BeatmapInfo beatmap && rulesetInfo is RulesetInfo ruleset)
                return getAdditionalInfoDifficultyAsync(beatmap, ruleset, mods, cancellationToken, computationDelay);

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
            return GetAsync(lookup, cancellationToken, computationDelay);
        }

        private Task<StarDifficulty?> getAdditionalInfoDifficultyAsync(BeatmapInfo beatmap, RulesetInfo ruleset, IEnumerable<Mod>? mods,
                                                                       CancellationToken cancellationToken, int computationDelay)
        {
            Mod[] orderedMods = mods?.OrderBy(m => m.Acronym).Select(m => m.DeepClone()).ToArray() ?? Array.Empty<Mod>();
            string modsKey = ForkDataStore.CreateModsKey(orderedMods);
            var store = ForkDataStore.Instance;
            if (additionalInfoVersions.TryGetValue(ruleset.ShortName, out int knownVersion)
                && store?.TryGetAdditionalInfo(beatmap.ID, ruleset.ShortName, modsKey, beatmap.MD5Hash, knownVersion, out var known) == true)
                return Task.FromResult<StarDifficulty?>(new StarDifficulty(known.Stars, known.MaxCombo, known.Text));

            return Task.Factory.StartNew(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lookup = new DifficultyCacheLookup(beatmap, ruleset, orderedMods, true);
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
                    return (StarDifficulty?)new StarDifficulty(cached.Stars, cached.MaxCombo, cached.Text);

                if (computationDelay > 0)
                    Task.Delay(computationDelay, cancellationToken).Wait(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                StarDifficulty? calculated = computeDifficulty(lookup, cancellationToken, true);
                if (calculated != null && !cancellationToken.IsCancellationRequested)
                    StoreValue(lookup, calculated);

                if (calculated?.PerformanceAttributes != null && calculated.Value.DifficultyAttributes != null
                    && version > 0 && !cancellationToken.IsCancellationRequested)
                {
                    var info = calculated.Value;
                    store?.SetAdditionalInfo(beatmap.ID, ruleset.ShortName, modsKey,
                        new ForkDataStore.AdditionalInfoData(beatmap.MD5Hash, version, info.Stars, info.MaxCombo,
                            BeatmapAdditionalInfoFormatter.Format(info)));
                }

                return calculated;
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler);
        }

        private StarDifficulty? computeDifficulty(in DifficultyCacheLookup key, CancellationToken cancellationToken = default, bool calculatePerformance = true)
        {
            // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
            var beatmapInfo = key.BeatmapInfo;
            var rulesetInfo = key.Ruleset;

            try
            {
                var ruleset = rulesetInfo.CreateInstance();
                Debug.Assert(ruleset != null);

                PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(key.BeatmapInfo));
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
                scoreProcessor.ApplyBeatmap(playableBeatmap);
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

                return new StarDifficulty(difficulty, performance);
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

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            modSettingChangeTracker?.Dispose();

            cancelTrackedBindableUpdate();
            updateScheduler.Dispose();
            previewScheduler.Dispose();
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

        private class BindableStarDifficulty : Bindable<StarDifficulty>
        {
            public IBeatmapInfo BeatmapInfo;
            public readonly CancellationToken CancellationToken;
            public readonly bool CalculatePerformance;
            public readonly bool UsePersistedAdditionalInfo;

            public BindableStarDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken, bool calculatePerformance, bool usePersistedAdditionalInfo)
            {
                BeatmapInfo = beatmapInfo;
                CancellationToken = cancellationToken;
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
