// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Overlays.Notifications;
using osu.Game.Performance.Debug;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Beatmaps
{
    public class BeatmapUpdater : IBeatmapUpdater
    {
        private readonly IWorkingBeatmapCache workingBeatmapCache;

        private readonly BeatmapDifficultyCache difficultyCache;

        private readonly BeatmapUpdaterMetadataLookup metadataLookup;

        private readonly ThreadedTaskScheduler updateScheduler;
        private readonly OsuConfigManager config;
        private readonly BackgroundCalculationPolicy calculationPolicy;
        private readonly object queueLock = new object();
        private readonly Dictionary<Guid, PendingUpdate> pendingUpdates = new Dictionary<Guid, PendingUpdate>();
        private readonly CancellationTokenSource disposal = new CancellationTokenSource();
        private BeatmapCalculationNotification? calculationNotification;
        private int notificationGeneration;
        private bool disposed;

        public Action<Notification>? PostNotification { get; set; }

        internal bool HasPendingUpdates
        {
            get
            {
                lock (queueLock)
                    return pendingUpdates.Count > 0;
            }
        }

        private sealed class PendingUpdate
        {
            public MetadataLookupScope Scope;
            public bool Requeue;
        }

        public BeatmapUpdater(IWorkingBeatmapCache workingBeatmapCache, BeatmapDifficultyCache difficultyCache, IAPIProvider api, Storage storage, OsuConfigManager config)
        {
            this.config = config;
            calculationPolicy = new BackgroundCalculationPolicy(config);
            this.workingBeatmapCache = workingBeatmapCache;
            this.difficultyCache = difficultyCache;

            // The scheduler has a fixed thread count; changing this setting takes effect on restart.
            updateScheduler = new ThreadedTaskScheduler(Math.Clamp(config.Get<int>(OsuSetting.ForkBackgroundCalculationConcurrency), 1, 4),
                                                        nameof(BeatmapUpdater));
            metadataLookup = new BeatmapUpdaterMetadataLookup(api, storage);
        }

        public void Queue(Live<BeatmapSetInfo> beatmapSet, MetadataLookupScope lookupScope = MetadataLookupScope.LocalCacheFirst)
        {
            lock (queueLock)
            {
                if (disposed)
                    return;

                notificationGeneration++;
                if (calculationNotification == null && PostNotification != null)
                {
                    calculationNotification = new BeatmapCalculationNotification();
                    PostNotification.Invoke(calculationNotification);
                }

                if (pendingUpdates.TryGetValue(beatmapSet.ID, out var existing))
                {
                    // Latest request wins (in particular, an editor save suppresses older online lookups).
                    existing.Scope = lookupScope;
                    existing.Requeue = true;
                    return;
                }

                var pending = new PendingUpdate { Scope = lookupScope };
                pendingUpdates.Add(beatmapSet.ID, pending);

                Task.Factory.StartNew(() => processQueued(beatmapSet, pending), CancellationToken.None,
                    TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler);
            }
        }

        private void processQueued(Live<BeatmapSetInfo> beatmapSet, PendingUpdate pending)
        {
            while (true)
            {
                MetadataLookupScope scope;
                lock (queueLock)
                {
                    scope = pending.Scope;
                    pending.Requeue = false;
                }

                try
                {
                    if (!disposal.IsCancellationRequested)
                        beatmapSet.PerformRead(b =>
                        {
                            // A map can have been removed while waiting for the worker.
                            if (b != null && b.IsValid && !b.DeletePending)
                                Process(b, scope);
                        });
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $"Background calculation failed for beatmap set {beatmapSet.ID}");
                }

                lock (queueLock)
                {
                    if (pending.Requeue && !disposed)
                        continue;

                    pendingUpdates.Remove(beatmapSet.ID);
                    if (pendingUpdates.Count == 0)
                        _ = closeNotificationWhenIdle(notificationGeneration);
                    return;
                }
            }
        }

        private async Task closeNotificationWhenIdle(int generation)
        {
            // Imports arriving a little apart belong to one batch, not a stream of new toasts.
            await Task.Delay(2500).ConfigureAwait(false);
            lock (queueLock)
            {
                if (generation != notificationGeneration || pendingUpdates.Count != 0)
                    return;

                calculationNotification?.Close(false);
                calculationNotification = null;
            }
        }

        public void Process(BeatmapSetInfo beatmapSet, MetadataLookupScope lookupScope = MetadataLookupScope.LocalCacheFirst,
                            Func<BeatmapInfo, bool>? skipCalculation = null)
        {
            using var processPhase = PerformancePhaseLogger.Begin("BeatmapUpdater.Process", detail: PerformancePhaseLogger.Enabled ? $"set_id={beatmapSet.OnlineID} difficulties={beatmapSet.Beatmaps.Count} lookup={lookupScope}" : null);
            var ppResults = new Dictionary<Guid, (double Pp, int Version)>();
            var relaxResults = new Dictionary<Guid, ForkDataStore.RelaxBeatmapData>();
            var relaxStarResults = new Dictionary<Guid, (string Checksum, double Stars)>();
            var dodgeDifficultyResults = new Dictionary<Guid, (string Checksum, DifficultyAttributes Attributes, int Version)>();

            if (beatmapSet.DeletePending || beatmapSet.Beatmaps.Count == 0)
                return;

            var realm = beatmapSet.Realm!;
            Guid setId = beatmapSet.ID;
            string sourceHash = beatmapSet.Hash;
            var revisions = beatmapSet.Beatmaps.ToDictionary(b => b.ID, b => (b.MD5Hash, b.LastLocalUpdate));

            workingBeatmapCache.Invalidate(beatmapSet);

            // Detaching at a beatmap (rather than the set) preserves file mappings, including custom sidecars.
            // Decoding, network requests, limiter waits, stars and PP must never hold the Realm writer lock.
            beatmapSet = beatmapSet.Beatmaps[0].Detach().BeatmapSet!;

            if (lookupScope != MetadataLookupScope.None)
            {
                using (PerformancePhaseLogger.Begin("BeatmapUpdater.MetadataLookup"))
                    metadataLookup.Update(beatmapSet, lookupScope == MetadataLookupScope.OnlineFirst);
            }

            foreach (BeatmapInfo beatmap in beatmapSet.Beatmaps)
            {
                if (skipCalculation?.Invoke(beatmap) == true)
                    continue;

                using var beatmapPhase = PerformancePhaseLogger.Begin("BeatmapUpdater.Difficulty", detail: PerformancePhaseLogger.Enabled ? $"beatmap_id={beatmap.OnlineID} difficulty={beatmap.DifficultyName}" : null);
                WorkingBeatmap working;
                using (PerformancePhaseLogger.Begin("BeatmapUpdater.GetWorkingBeatmap"))
                    working = workingBeatmapCache.GetWorkingBeatmapForCalculation(beatmap);

                var ruleset = working.BeatmapInfo.Ruleset.CreateInstance();
                string mode = ruleset.RulesetInfo.ShortName;
                bool stars = calculationPolicy.Allows(mode, LibraryCalculation.Difficulty, true);
                bool pp = calculationPolicy.Allows(mode, LibraryCalculation.Performance, true);
                bool relaxStars = calculationPolicy.Allows(mode, LibraryCalculation.RelaxDifficulty, true);
                bool relaxPp = calculationPolicy.Allows(mode, LibraryCalculation.RelaxPerformance, true);
                if (!stars && !pp && !relaxStars && !relaxPp)
                {
                    if (config.Get<bool>(OsuSetting.ForkBackgroundCalculationObjectCounts))
                        beatmap.UpdateStatisticsFromBeatmap(working.Beatmap);
                    continue;
                }

                using var calculationSlot = BackgroundCalculationLimiter.Enter(config.Get<int>(OsuSetting.ForkBackgroundCalculationConcurrency),
                    config.Get<int>(OsuSetting.ForkBackgroundCalculationDutyPercent), mode, calculationPolicy.Concurrency(mode),
                    disposal.Token, deferWhileInteracting: true);
                var calculator = ruleset.CreateDifficultyCalculator(working);

                DifficultyAttributes? difficultyAttributes = null;
                if (stars || pp)
                {
                    using (PerformancePhaseLogger.Begin("BeatmapUpdater.CalculateDifficulty"))
                        difficultyAttributes = calculator.Calculate(disposal.Token);
                    if (stars && mode != RulesetInfo.DODGE_MODE_SHORTNAME)
                        beatmap.StarRating = difficultyAttributes.StarRating;
                }

                if (stars && mode == RulesetInfo.DODGE_MODE_SHORTNAME)
                {
                    dodgeDifficultyResults[beatmap.ID] = (
                        beatmap.MD5Hash,
                        difficultyAttributes!,
                        calculator.Version);
                }

                try
                {
                    var performanceCalculator = ruleset.CreatePerformanceCalculator();

                    if (pp && performanceCalculator != null)
                    {
                        using var preparationPhase = PerformancePhaseLogger.Begin("BeatmapUpdater.PreparePerfectScore");
                        var playableBeatmap = calculator.CalculatedBeatmap;
                        var scoreProcessor = ruleset.CreateScoreProcessor();
                        scoreProcessor.Mods.Value = Array.Empty<Mod>();
                        scoreProcessor.ApplyBeatmap(playableBeatmap);

                        var perfectScore = new ScoreInfo(beatmap, ruleset.RulesetInfo)
                        {
                            Passed = true,
                            Accuracy = 1,
                            Mods = Array.Empty<Mod>(),
                            MaxCombo = scoreProcessor.MaximumCombo,
                            Combo = scoreProcessor.MaximumCombo,
                            TotalScore = scoreProcessor.MaximumTotalScore,
                            Statistics = scoreProcessor.MaximumStatistics,
                            MaximumStatistics = scoreProcessor.MaximumStatistics
                        };

                        preparationPhase.End();
                        PerformanceAttributes performance;
                        using (PerformancePhaseLogger.Begin("BeatmapUpdater.CalculatePerformance"))
                            performance = performanceCalculator.Calculate(perfectScore, difficultyAttributes!);
                        ppResults[beatmap.ID] = (double.IsFinite(performance.Total) ? performance.Total : 0, calculator.Version);
                    }
                    else if (pp)
                    {
                        ppResults[beatmap.ID] = (0, calculator.Version);
                    }

                    if ((relaxStars || relaxPp) && ruleset.GetModsFor(ModType.Automation).OfType<ModRelax>().FirstOrDefault() is ModRelax relaxMod)
                    {
                        Mod[] relaxMods = { relaxMod };
                        DifficultyAttributes relaxDifficulty;
                        using (PerformancePhaseLogger.Begin("BeatmapUpdater.CalculateRelaxDifficulty"))
                            relaxDifficulty = calculator.Calculate(relaxMods, disposal.Token);
                        if (!relaxPp || performanceCalculator == null)
                        {
                            relaxStarResults[beatmap.ID] = (beatmap.MD5Hash, relaxDifficulty.StarRating);
                        }
                        else
                        {
                            using var relaxPreparationPhase = PerformancePhaseLogger.Begin("BeatmapUpdater.PreparePerfectRelaxScore");
                            var playableRelaxBeatmap = calculator.CalculatedBeatmap;
                            var relaxScoreProcessor = ruleset.CreateScoreProcessor();
                            relaxScoreProcessor.Mods.Value = relaxMods;
                            relaxScoreProcessor.ApplyBeatmap(playableRelaxBeatmap);

                            var perfectRelaxScore = new ScoreInfo(beatmap, ruleset.RulesetInfo)
                            {
                                Passed = true,
                                Accuracy = 1,
                                Mods = relaxMods,
                                MaxCombo = relaxScoreProcessor.MaximumCombo,
                                Combo = relaxScoreProcessor.MaximumCombo,
                                TotalScore = relaxScoreProcessor.MaximumTotalScore,
                                Statistics = relaxScoreProcessor.MaximumStatistics,
                                MaximumStatistics = relaxScoreProcessor.MaximumStatistics
                            };

                            relaxPreparationPhase.End();
                            PerformanceAttributes relaxPerformance;
                            using (PerformancePhaseLogger.Begin("BeatmapUpdater.CalculateRelaxPerformance"))
                                relaxPerformance = performanceCalculator.Calculate(perfectRelaxScore, relaxDifficulty);
                            double relaxPerformancePoints = double.IsFinite(relaxPerformance.Total) ? relaxPerformance.Total : 0;
                            relaxResults[beatmap.ID] = new ForkDataStore.RelaxBeatmapData(relaxDifficulty.StarRating, relaxPerformancePoints);
                        }
                    }
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to compute PP for {beatmap.OnlineID}: {e.Message}");
                }

                if (config.Get<bool>(OsuSetting.ForkBackgroundCalculationObjectCounts))
                {
                    using (PerformancePhaseLogger.Begin("BeatmapUpdater.UpdateStatistics"))
                        beatmap.UpdateStatisticsFromBeatmap(working.Beatmap);
                }
            }

            disposal.Token.ThrowIfCancellationRequested();

            realm.Write(_ =>
            {
                var current = realm.Find<BeatmapSetInfo>(setId);
                if (current == null || current.DeletePending || current.Hash != sourceHash
                    || current.Beatmaps.Count != revisions.Count
                    || current.Beatmaps.Any(b => !revisions.TryGetValue(b.ID, out var revision)
                                                || b.MD5Hash != revision.MD5Hash || b.LastLocalUpdate != revision.LastLocalUpdate))
                    return;

                // Apply only processing-owned fields. Never copy a stale snapshot's user settings, files or editor state.
                if (lookupScope != MetadataLookupScope.None)
                {
                    current.OnlineID = beatmapSet.OnlineID;
                    current.Status = beatmapSet.Status;
                    current.DateRanked = beatmapSet.DateRanked;
                    current.DateSubmitted = beatmapSet.DateSubmitted;
                }

                foreach (var result in beatmapSet.Beatmaps)
                {
                    var target = current.Beatmaps.Single(b => b.ID == result.ID);
                    if (lookupScope != MetadataLookupScope.None)
                    {
                        target.OnlineID = result.OnlineID;
                        target.OnlineMD5Hash = result.OnlineMD5Hash;
                        target.LastOnlineUpdate = result.LastOnlineUpdate;
                        target.Status = result.Status;
                        target.Metadata.Author.OnlineID = result.Metadata.Author.OnlineID;
                        target.Metadata.UserTags.Clear();
                        foreach (var tag in result.Metadata.UserTags)
                            target.Metadata.UserTags.Add(tag);
                    }

                    target.StarRating = result.StarRating;
                    target.Length = result.Length;
                    target.BPM = result.BPM;
                    target.EndTimeObjectCount = result.EndTimeObjectCount;
                    target.TotalObjectCount = result.TotalObjectCount;
                }

                // Keep the revision check and both stores' writes ordered against editor saves.
                // Only persistence remains here; all expensive work has already completed.
                persistForkResults();
                workingBeatmapCache.Invalidate(current);
                foreach (var result in beatmapSet.Beatmaps)
                {
                    using (PerformancePhaseLogger.Begin("BeatmapUpdater.InvalidateDifficultyCache"))
                        difficultyCache.Invalidate(result, current.Beatmaps.Single(b => b.ID == result.ID).Detach());
                }
            });

            void persistForkResults()
            {
                var forkStore = ForkDataStore.Instance;
                if (forkStore == null)
                    return;

                using var persistPhase = PerformancePhaseLogger.Begin("BeatmapUpdater.PersistForkResults");
                foreach (var (id, result) in ppResults)
                    forkStore.SetPP(id, result.Pp, result.Version);

                foreach (var (id, result) in relaxResults)
                    forkStore.SetRelaxData(id, result.StarRating, result.MaxPerformancePoints);

                foreach (var (id, result) in relaxStarResults)
                    forkStore.SetRelaxDifficulty(id, result.Checksum,
                        ForkDataStore.GetRelaxCalculationVersion(RelaxPpSystemSelection.Current), result.Stars);

                foreach (var (id, result) in dodgeDifficultyResults)
                    forkStore.SetDodgeDifficulty(id, result.Checksum, result.Attributes, result.Version);
            }
        }

        public void ProcessObjectCounts(BeatmapInfo beatmapInfo, MetadataLookupScope lookupScope = MetadataLookupScope.LocalCacheFirst)
        {
            beatmapInfo.Realm!.Write(_ =>
            {
                // Before we use below, we want to invalidate.
                workingBeatmapCache.Invalidate(beatmapInfo);

                var working = workingBeatmapCache.GetWorkingBeatmap(beatmapInfo);
                var beatmap = working.Beatmap;

                beatmapInfo.EndTimeObjectCount = beatmap.HitObjects.Count(h => h is IHasDuration);
                beatmapInfo.TotalObjectCount = beatmap.HitObjects.Count;

                // And invalidate again afterwards as re-fetching the most up-to-date database metadata will be required.
                workingBeatmapCache.Invalidate(beatmapInfo);
            });
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            lock (queueLock)
            {
                if (disposed)
                    return;

                disposed = true;
                disposal.Cancel();
                calculationNotification?.Close(false);
                calculationNotification = null;
            }

            if (updateScheduler.IsNotNull())
                updateScheduler.Dispose();

            if (metadataLookup.IsNotNull())
                metadataLookup.Dispose();
        }

        #endregion
    }
}
