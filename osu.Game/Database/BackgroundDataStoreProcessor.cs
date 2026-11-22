// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Performance;
using osu.Game.Performance.Diagnostics;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Performs background updating of data stores at startup.
    /// </summary>
    public partial class BackgroundDataStoreProcessor : Component
    {
        private const string dodge_ruleset_short_name = "dodge";

        protected Task ProcessingTask { get; private set; } = null!;

        [Resolved]
        private RulesetStore rulesetStore { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private IBeatmapUpdater beatmapUpdater { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> gameBeatmap { get; set; } = null!;

        [Resolved]
        private ILocalUserPlayInfo? localUserPlayInfo { get; set; }

        [Resolved]
        private IHighPerformanceSessionManager? highPerformanceSessionManager { get; set; }

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IPerformanceDiagnosticsManager? performanceDiagnosticsManager { get; set; }

        private static int testRecalcRunning;
        private static int relaxRecalcRunning;
        private static int relaxRecalcPending;

        /// <summary>
        /// Singleton instance so the config-change subscription (which lives in
        /// <see cref="OsuGameBase"/>, ahead of component load order) can trigger
        /// the recalculation pass.
        /// </summary>
        private static BackgroundDataStoreProcessor? instance;

        private LocalCachedBeatmapMetadataSource localMetadataSource = null!;
        private BackgroundCalculationPolicy calculationPolicy = null!;
        private BackgroundCalculationFailures calculationFailures = null!;
        private readonly CancellationTokenSource shutdownCancellation = new CancellationTokenSource();
        private CancellationTokenSource processingCancellation = new CancellationTokenSource();
        private int libraryRunning;
        private int libraryPaused;
        [ThreadStatic]
        private static bool libraryWorker;

        public static bool LibraryRunning => instance != null && Volatile.Read(ref instance.libraryRunning) != 0;
        public static bool LibraryPaused => instance != null && Volatile.Read(ref instance.libraryPaused) != 0;

        public static void SetLibraryPaused(bool paused)
        {
            if (instance != null)
                Interlocked.Exchange(ref instance.libraryPaused, paused ? 1 : 0);
        }

        public static void StopLibraryProcessing() => instance?.processingCancellation.Cancel();

        public static void QueueLibraryProcessing()
        {
            var processor = instance;
            if (processor == null || Interlocked.CompareExchange(ref processor.libraryRunning, 1, 0) != 0)
                return;

            processor.startLibraryProcessing(false);
        }

        private void startLibraryProcessing(bool startup)
        {
            processingCancellation.Dispose();
            processingCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellation.Token);
            Interlocked.Exchange(ref libraryPaused, 0);
            ProcessingTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    // Run mandatory compatibility work before cancellable library preparation.
                    if (startup)
                    {
                        convertLegacyTotalScoreToStandardised();
                        upgradeScoreRanks();
                    }
                    libraryWorker = true;
                    if (startup && config.Get<bool>(OsuSetting.ForkBackgroundCalculationStartup))
                    {
                        int seconds = config.Get<int>(OsuSetting.ForkBackgroundCalculationDelay);
                        if (processingCancellation.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(seconds)))
                            return;
                    }

                    if (!startup || config.Get<bool>(OsuSetting.ForkBackgroundCalculationStartup))
                    {
                        populateMissingPerformancePoints();
                        clearOutdatedStarRatings();
                        populateOutdatedDodgeStarRatings();
                        populateMissingStarRatings();
                        if (config.Get<bool>(OsuSetting.ForkBackgroundCalculationOnlineMetadata))
                            processOnlineBeatmapSetsWithNoUpdate();
                        if (config.Get<bool>(OsuSetting.ForkBackgroundCalculationObjectCounts))
                            processBeatmapsWithMissingObjectCounts();
                        if (config.Get<bool>(OsuSetting.ForkBackgroundCalculationScoreStatistics))
                            processScoresWithMissingStatistics();
                        if (config.Get<bool>(OsuSetting.ForkBackgroundCalculationTags))
                            backpopulateUserTags();
                    }

                }
                catch (OperationCanceledException)
                {
                    Logger.Log("Library processing cancelled");
                }
                catch (ObjectDisposedException) when (shutdownCancellation.IsCancellationRequested)
                {
                    Logger.Log("Library processing aborted during shutdown");
                }
                finally
                {
                    libraryWorker = false;
                    Interlocked.Exchange(ref libraryRunning, 0);
                }
            }, TaskCreationOptions.LongRunning).ContinueWith(t =>
            {
                if (t.Exception != null)
                    Logger.Error(t.Exception.GetBaseException(), "Background data store processing failed");
            });
        }

        protected virtual int TimeToSleepDuringGameplay => 30000;

        protected virtual bool SkipProcessing => DebugUtils.IsNUnitRunning;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (SkipProcessing)
                return;

            localMetadataSource = new LocalCachedBeatmapMetadataSource(storage);
            calculationPolicy = new BackgroundCalculationPolicy(config);
            calculationFailures = new BackgroundCalculationFailures(storage);

            // The config-change subscription lives in OsuGameBase (created before any
            // component); register here so it can reach the recalculation pass.
            instance = this;

            Interlocked.Exchange(ref libraryRunning, 1);
            startLibraryProcessing(true);
        }

        /// <summary>
        /// Check whether the databased difficulty calculation version matches the latest ruleset provided version.
        /// If it doesn't, clear out any existing difficulties so they can be incrementally recalculated.
        /// </summary>
        private void clearOutdatedStarRatings()
        {
            foreach (var ruleset in rulesetStore.AvailableRulesets)
            {
                // Dodge difficulty belongs to fork.realm. Its previous value is
                // intentionally retained while a new calculator version runs;
                // never invalidate or version it through the upstream Realm.
                if (ruleset.ShortName == dodge_ruleset_short_name
                    || !calculationPolicy.Allows(ruleset.ShortName, LibraryCalculation.Difficulty)
                    || !calculationPolicy.Allows(ruleset.ShortName, LibraryCalculation.RefreshOutdated))
                    continue;

                // beatmap being passed in is arbitrary here. just needs to be non-null.
                int currentVersion = ruleset.CreateInstance().CreateDifficultyCalculator(gameBeatmap.Value).Version;

                if (ruleset.LastAppliedDifficultyVersion < currentVersion)
                {
                    Logger.Log($"Resetting star ratings for {ruleset.Name} (difficulty calculation version updated from {ruleset.LastAppliedDifficultyVersion} to {currentVersion})");

                    int countReset = 0;

                    realmAccess.Write(r =>
                    {
                        foreach (var b in r.All<BeatmapInfo>())
                        {
                            if (b.Ruleset.ShortName == ruleset.ShortName)
                            {
                                b.StarRating = -1;
                                countReset++;
                            }
                        }

                        r.Find<RulesetInfo>(ruleset.ShortName)!.LastAppliedDifficultyVersion = currentVersion;
                    });

                    Logger.Log($"Finished resetting {countReset} beatmap sets for {ruleset.Name}");
                }
            }
        }

        private void populateOutdatedDodgeStarRatings()
        {
            var forkStore = ForkDataStore.Instance;
            RulesetInfo? rulesetInfo = rulesetStore.AvailableRulesets.FirstOrDefault(r => r.ShortName == dodge_ruleset_short_name);

            if (forkStore == null || rulesetInfo == null
                || !calculationPolicy.Allows(dodge_ruleset_short_name, LibraryCalculation.Difficulty))
                return;

            Ruleset ruleset = rulesetInfo.CreateInstance();
            int currentVersion = ruleset.CreateDifficultyCalculator(gameBeatmap.Value).Version;
            HashSet<Guid> beatmapIds = new HashSet<Guid>();
            var initialRatings = new List<(Guid BeatmapId, double StarRating)>();

            realmAccess.Run(r =>
            {
                // Realm cannot translate comparisons through a linked object's property
                // (`b.Ruleset.ShortName`). Keep the persisted null check in the query and
                // perform the ruleset comparison after materialisation.
                foreach (var beatmap in r.All<BeatmapInfo>().Where(b => b.BeatmapSet != null))
                {
                    if (beatmap.Ruleset.ShortName != dodge_ruleset_short_name
                        || calculationFailures.ShouldSkip(beatmap.ID, beatmap.MD5Hash))
                        continue;

                    if (!calculationPolicy.Allows(dodge_ruleset_short_name, LibraryCalculation.RefreshOutdated)
                        && forkStore.GetDodgeDifficulty(beatmap.ID).StarRating >= 0)
                        continue;

                    if (forkStore.HasDodgeDifficultyAttributes(beatmap.ID, currentVersion, beatmap.MD5Hash))
                        continue;

                    // One-time seed for installations upgrading to the fork
                    // cache: preserve the last upstream-stored number on screen
                    // until the new calculation replaces it. This is read-only
                    // with respect to client.realm.
                    if (forkStore.GetDodgeDifficulty(beatmap.ID).StarRating < 0 && beatmap.StarRating >= 0)
                        initialRatings.Add((beatmap.ID, beatmap.StarRating));

                    beatmapIds.Add(beatmap.ID);
                }
            });

            foreach ((Guid beatmapId, double starRating) in initialRatings)
                forkStore.SetDodgeDifficulty(beatmapId, starRating, Math.Max(1, currentVersion - 1));

            if (beatmapIds.Count == 0)
                return;

            Logger.Log($"Found {beatmapIds.Count} Dodge beatmaps which require fork difficulty reprocessing.");
            var notification = showProgressNotification(beatmapIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3231, global::osu.Game.Localisation.ClientTranslationStrings.J3232);
            int processedCount = 0;
            int failedCount = 0;

            runCalculations(beatmapIds, id =>
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    return;

                updateNotificationProgress(notification, processedCount, beatmapIds.Count);
                sleepIfRequired();

                if (!calculationPolicy.Allows(dodge_ruleset_short_name, LibraryCalculation.Difficulty))
                    return;

                BeatmapInfo? beatmap = realmAccess.Run(r => r.Find<BeatmapInfo>(id)?.Detach());

                if (beatmap == null || calculationFailures.ShouldSkip(beatmap.ID, beatmap.MD5Hash))
                    return;

                try
                {
                    using var calculationSlot = enterCalculationSlot(beatmap);
                    IWorkingBeatmap working = beatmapManager.GetWorkingBeatmap(beatmap);
                    var difficulty = rulesetInfo.CreateInstance().CreateDifficultyCalculator(working).Calculate();
                    forkStore.SetDodgeDifficulty(id, beatmap.MD5Hash, difficulty, currentVersion);
                    Interlocked.Increment(ref processedCount);
                }
                catch (Exception e)
                {
                    recordCalculationFailure(beatmap, e, notification);
                    Logger.Log($"Background Dodge difficulty processing failed on {beatmap}: {e}");
                    Interlocked.Increment(ref failedCount);
                }
            });

            completeNotification(notification, processedCount, beatmapIds.Count, failedCount);
        }

        /// <remarks>
        /// This is split out from <see cref="processOnlineBeatmapSetsWithNoUpdate"/> as a separate process to prevent high server-side load
        /// from the <see cref="beatmapUpdater"/> firing online requests as part of the update.
        /// Star rating recalculations can be ran strictly locally.
        /// </remarks>
        private void populateMissingStarRatings()
        {
            HashSet<Guid> beatmapIds = new HashSet<Guid>();

            Logger.Log("Querying for beatmaps with missing star ratings...");

            realmAccess.Run(r =>
            {
                // Realm does not support filtering through the linked Ruleset object.
                foreach (var b in r.All<BeatmapInfo>().Where(b => b.StarRating < 0
                                                                  && b.BeatmapSet != null))
                {
                    if (b.Ruleset.ShortName == dodge_ruleset_short_name
                        || !calculationPolicy.Allows(b.Ruleset.ShortName, LibraryCalculation.Difficulty)
                        || calculationFailures.ShouldSkip(b.ID, b.MD5Hash))
                        continue;

                    beatmapIds.Add(b.ID);
                }
            });

            if (beatmapIds.Count == 0)
                return;

            Logger.Log($"Found {beatmapIds.Count} beatmaps which require star rating reprocessing.");

            var notification = showProgressNotification(beatmapIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3233, global::osu.Game.Localisation.ClientTranslationStrings.J3234);

            int processedCount = 0;
            int failedCount = 0;

            runCalculations(beatmapIds, id =>
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    return;

                updateNotificationProgress(notification, processedCount, beatmapIds.Count);

                sleepIfRequired();

                var beatmap = realmAccess.Run(r => r.Find<BeatmapInfo>(id)?.Detach());

                if (beatmap == null || !calculationPolicy.Allows(beatmap.Ruleset.ShortName, LibraryCalculation.Difficulty)
                    || calculationFailures.ShouldSkip(beatmap.ID, beatmap.MD5Hash))
                    return;

                try
                {
                    using var calculationSlot = enterCalculationSlot(beatmap);
                    var working = beatmapManager.GetWorkingBeatmap(beatmap);
                    var ruleset = working.BeatmapInfo.Ruleset.CreateInstance();

                    Debug.Assert(ruleset != null);

                    var calculator = ruleset.CreateDifficultyCalculator(working);

                    double starRating = calculator.Calculate().StarRating;
                    realmAccess.Write(r =>
                    {
                        if (r.Find<BeatmapInfo>(id) is BeatmapInfo liveBeatmapInfo)
                            liveBeatmapInfo.StarRating = starRating;
                    });
                    // Star rating processing runs on a dedicated background thread. Invalidating a
                    // cached working beatmap can notify UI consumers which may hold Realm-backed
                    // state, so dispatch the invalidation to the update thread. Calling this inline
                    // can race song select and crash inside the native Realm wrapper.
                    Scheduler.Add(() => ((IWorkingBeatmapCache)beatmapManager).Invalidate(beatmap));
                    Interlocked.Increment(ref processedCount);
                }
                catch (Exception e)
                {
                    recordCalculationFailure(beatmap, e, notification);
                    Logger.Log($"Background processing failed on {beatmap}: {e}");
                    Interlocked.Increment(ref failedCount);
                }
            });

            completeNotification(notification, processedCount, beatmapIds.Count, failedCount);
        }

        private void populateMissingPerformancePoints(bool relaxOnly = false)
        {
            var forkStore = ForkDataStore.Instance;

            if (forkStore == null)
            {
                Logger.Log("ForkDataStore not available, skipping PP calculation.");
                return;
            }

            HashSet<Guid> beatmapIds = new HashSet<Guid>();
            int skippedCount = 0;

            Logger.Log("Querying for beatmaps with missing performance points...");

            // Capability discovery creates a ruleset and automation mods. Do it once
            // per ruleset per pass rather than once for every beatmap in the library.
            var relaxSupport = new RulesetRelaxSupportCache();

            realmAccess.Run(r =>
            {
                foreach (var b in r.All<BeatmapInfo>().Where(b => b.BeatmapSet != null))
                {
                    if (!calculationPolicy.Get(b.Ruleset.ShortName).Enabled)
                        continue;
                    try
                    {
                        if (calculationFailures.ShouldSkip(b.ID, b.MD5Hash))
                        {
                            skippedCount++;
                            continue;
                        }

                        if (relaxOnly)
                        {
                            // Only the active Relax PP system's cache is inspected; the other
                            // system's stored data must never be invalidated by a recalculation.
                            if (relaxSupport.SupportsRelax(b.Ruleset)
                                && calculationPolicy.Allows(b.Ruleset.ShortName, LibraryCalculation.RelaxPerformance)
                                && !forkStore.HasRelaxData(b.ID))
                                beatmapIds.Add(b.ID);

                            continue;
                        }

                        bool supportsRelax = relaxSupport.SupportsRelax(b.Ruleset);
                        bool missingStandardPp = calculationPolicy.Allows(b.Ruleset.ShortName, LibraryCalculation.Performance) && !forkStore.HasPP(b.ID);
                        bool missingRelaxData = supportsRelax &&
                            ((calculationPolicy.Allows(b.Ruleset.ShortName, LibraryCalculation.RelaxPerformance) && !forkStore.HasRelaxData(b.ID))
                             || (calculationPolicy.Allows(b.Ruleset.ShortName, LibraryCalculation.RelaxDifficulty)
                                 && !forkStore.HasRelaxDifficulty(b.ID, b.MD5Hash, ForkDataStore.GetRelaxCalculationVersion(RelaxPpSystemSelection.Current))));

                        if (missingStandardPp || missingRelaxData)
                            beatmapIds.Add(b.ID);
                    }
                    catch (Exception exception)
                    {
                        // A removed custom ruleset must not abort discovery for every osu! map.
                        Logger.Error(exception, $"Skipping PP cache discovery for {b.ID} ({b.Ruleset.ShortName})");
                    }
                }
            });

            if (skippedCount > 0)
            {
                Logger.Log($"Skipped {skippedCount} background calculations after previous failures; unchanged maps will not be retried.");
                Scheduler.Add(() => notificationOverlay?.Post(new SimpleNotification { Text = BackgroundCalculationStrings.FailedMapsSkipped }));
            }

            if (beatmapIds.Count == 0)
                return;

            Logger.Log($"Found {beatmapIds.Count} beatmaps which require performance points processing.");

            var notification = showProgressNotification(
                beatmapIds.Count,
                relaxOnly ? global::osu.Game.Localisation.ClientTranslationStrings.J3235 : global::osu.Game.Localisation.ClientTranslationStrings.J3236,
                relaxOnly ? global::osu.Game.Localisation.ClientTranslationStrings.J3237 : global::osu.Game.Localisation.ClientTranslationStrings.J3238,
                1);

            int processedCount = 0;
            int failedCount = 0;

            runCalculations(beatmapIds, id =>
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    return;

                updateNotificationProgress(notification, processedCount, beatmapIds.Count);

                sleepIfRequired();

                var beatmap = realmAccess.Run(r => r.Find<BeatmapInfo>(id)?.Detach());

                if (beatmap == null || calculationFailures.ShouldSkip(beatmap.ID, beatmap.MD5Hash))
                    return;

                try
                {
                    var working = beatmapManager.GetWorkingBeatmap(beatmap);
                    var ruleset = working.BeatmapInfo.Ruleset.CreateInstance();

                    Debug.Assert(ruleset != null);

                    using var calculationSlot = enterCalculationSlot(beatmap);
                    var calculator = ruleset.CreateDifficultyCalculator(working);
                    var performanceCalculator = ruleset.CreatePerformanceCalculator();

                    bool standardPp = !relaxOnly && !forkStore.HasPP(id) && calculationPolicy.Allows(beatmap.Ruleset.ShortName, LibraryCalculation.Performance);
                    bool relaxPpEnabled = !forkStore.HasRelaxData(id) && calculationPolicy.Allows(beatmap.Ruleset.ShortName, LibraryCalculation.RelaxPerformance);
                    bool relaxStarsEnabled = calculationPolicy.Allows(beatmap.Ruleset.ShortName, LibraryCalculation.RelaxDifficulty);

                    if (standardPp)
                    {
                        int calculationVersion = calculator.Version;
                        var difficultyAttributes = calculator.Calculate();

                        if (beatmap.StarRating < 0 && calculationPolicy.Allows(beatmap.Ruleset.ShortName, LibraryCalculation.Difficulty))
                        {
                            realmAccess.Write(r =>
                            {
                                if (r.Find<BeatmapInfo>(id) is BeatmapInfo liveBeatmapInfo)
                                    liveBeatmapInfo.StarRating = difficultyAttributes.StarRating;
                            });

                            Scheduler.Add(() => ((IWorkingBeatmapCache)beatmapManager).Invalidate(beatmap));
                        }

                        double pp = 0;

                        if (performanceCalculator != null)
                        {
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

                            var performance = performanceCalculator.Calculate(perfectScore, difficultyAttributes);
                            pp = double.IsFinite(performance.Total) ? performance.Total : 0;
                        }

                        forkStore.SetPP(id, pp, calculationVersion);
                    }

                    if ((relaxPpEnabled || relaxStarsEnabled)
                        && ruleset.GetModsFor(ModType.Automation).OfType<ModRelax>().FirstOrDefault() is ModRelax relaxMod)
                    {
                        // Object-less maps cannot be scored by the RX core (it rejects empty
                        // beatmaps outright). Store zeroed data so they stop re-appearing as
                        // "missing" on every startup / PP-system switch and re-triggering the
                        // recalculation pass forever.
                        if (!working.Beatmap.HitObjects.Any())
                        {
                            if (relaxPpEnabled)
                                forkStore.SetRelaxData(id, 0, 0);
                            else
                                forkStore.SetRelaxDifficulty(id, beatmap.MD5Hash, ForkDataStore.GetRelaxCalculationVersion(RelaxPpSystemSelection.Current), 0);
                            Interlocked.Increment(ref processedCount);
                            return;
                        }

                        Mod[] relaxMods = { relaxMod };
                        var relaxDifficulty = calculator.Calculate(relaxMods);
                        double relaxPp = 0;

                        if (relaxPpEnabled && performanceCalculator != null)
                        {
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

                            var relaxPerformance = performanceCalculator.Calculate(perfectRelaxScore, relaxDifficulty);
                            relaxPp = double.IsFinite(relaxPerformance.Total) ? relaxPerformance.Total : 0;
                        }

                        if (relaxPpEnabled)
                            forkStore.SetRelaxData(id, relaxDifficulty.StarRating, relaxPp);
                        else
                            forkStore.SetRelaxDifficulty(id, beatmap.MD5Hash, ForkDataStore.GetRelaxCalculationVersion(RelaxPpSystemSelection.Current), relaxDifficulty.StarRating);
                    }

                    Interlocked.Increment(ref processedCount);
                }
                catch (Exception e)
                {
                    recordCalculationFailure(beatmap, e, notification);
                    Logger.Log($"Background PP processing failed on {beatmap} (id={id}, online_id={beatmap.OnlineID}, checksum={beatmap.MD5Hash}): {e}");
                    Interlocked.Increment(ref failedCount);
                }
            });

            completeNotification(notification, processedCount, beatmapIds.Count, failedCount);
        }

        /// <summary>
        /// Runs a bounded, read-only sample of the normal library SR and perfect-score PP calculations.
        /// This intentionally does not invalidate persistent caches or invoke online metadata lookups.
        /// </summary>
        public static void QueueTestRecalculation()
        {
            BackgroundDataStoreProcessor? processor = instance;
            if (processor == null || Interlocked.CompareExchange(ref testRecalcRunning, 1, 0) != 0)
                return;

            Task.Factory.StartNew(() => processor.runTestRecalculation(), TaskCreationOptions.LongRunning).ContinueWith(t =>
            {
                Interlocked.Exchange(ref testRecalcRunning, 0);
                if (t.Exception != null)
                    Logger.Error(t.Exception.GetBaseException(), "Test library recalculation failed");
            });
        }

        private void runTestRecalculation()
        {
            if (performanceTestActive())
            {
                notificationOverlay?.Post(new SimpleNotification { Text = global::osu.Game.Localisation.ClientTranslationStrings.J3239 });
                return;
            }

            // Detach from Realm before any decoding/calculation; never hold a database write lock here.
            var sample = realmAccess.Run(r => r.All<BeatmapInfo>()
                                             .Where(b => b.BeatmapSet != null)
                                             .AsEnumerable() // Realm's LINQ provider does not support Take().
                                             .Take(1000)
                                             .Select(b => b.Detach())
                                             .ToArray());

            var notification = showProgressNotification(sample.Length, global::osu.Game.Localisation.ClientTranslationStrings.J3240, global::osu.Game.Localisation.ClientTranslationStrings.J3241, 1);
            if (sample.Length == 0)
            {
                notificationOverlay?.Post(new SimpleNotification { Text = global::osu.Game.Localisation.ClientTranslationStrings.J3242 });
                return;
            }

            int processed = 0;
            int failed = 0;
            bool interruptedByBenchmark = false;
            var timer = Stopwatch.StartNew();
            TimeSpan cpuAtStart = Process.GetCurrentProcess().TotalProcessorTime;
            Logger.Log($"Test library recalculation started: {sample.Length} maps");

            foreach (var beatmap in sample)
            {
                if (notification?.CancellationToken.IsCancellationRequested == true)
                    break;

                updateNotificationProgress(notification, processed + failed, sample.Length);
                // An in-progress benchmark must never overlap the sample. Stop rather than
                // resuming a diagnostic run after the benchmark has finished.
                if (performanceTestActive())
                {
                    interruptedByBenchmark = true;
                    Logger.Log("Test library recalculation stopped for performance benchmark");
                    break;
                }
                sleepIfRequired();
                if (performanceTestActive())
                {
                    interruptedByBenchmark = true;
                    break;
                }

                try
                {
                    using var slot = enterCalculationSlot(beatmap);
                    var working = beatmapManager.GetWorkingBeatmap(beatmap);
                    var ruleset = working.BeatmapInfo.Ruleset.CreateInstance();
                    var calculator = ruleset.CreateDifficultyCalculator(working);
                    var attributes = calculator.Calculate();
                    double pp = 0;
                    var performanceCalculator = ruleset.CreatePerformanceCalculator();
                    if (performanceCalculator != null)
                    {
                        var playable = calculator.CalculatedBeatmap;
                        var processor = ruleset.CreateScoreProcessor();
                        processor.Mods.Value = Array.Empty<Mod>();
                        processor.ApplyBeatmap(playable);
                        var perfectScore = new ScoreInfo(beatmap, ruleset.RulesetInfo)
                        {
                            Passed = true,
                            Accuracy = 1,
                            Mods = Array.Empty<Mod>(),
                            MaxCombo = processor.MaximumCombo,
                            Combo = processor.MaximumCombo,
                            TotalScore = processor.MaximumTotalScore,
                            Statistics = processor.MaximumStatistics,
                            MaximumStatistics = processor.MaximumStatistics
                        };
                        pp = performanceCalculator.Calculate(perfectScore, attributes).Total;
                    }

                    Logger.Log($"Test recalculation {beatmap.ID}: SR={attributes.StarRating:F2}, PP={pp:F2}");
                    processed++;
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Test recalculation failed for {beatmap.ID}");
                    failed++;
                }
            }

            timer.Stop();
            double cpuSeconds = (Process.GetCurrentProcess().TotalProcessorTime - cpuAtStart).TotalSeconds;
            Logger.Log($"Test library recalculation finished: {processed} successful, {failed} failed, {timer.Elapsed.TotalSeconds:F1}s wall, {cpuSeconds:F1}s process CPU");
            if (notification != null)
            {
                notification.Text = $"Тестовый перерасчёт: {processed} успешно, {failed} ошибок, {timer.Elapsed.TotalSeconds:F1} с, CPU {cpuSeconds:F1} с"
                                    + (interruptedByBenchmark ? " (остановлен тестом производительности)" : "");
                notification.CompletionText = notification.Text;
                notification.Progress = (float)(processed + failed) / sample.Length;
                notification.State = interruptedByBenchmark || notification.CancellationToken.IsCancellationRequested
                    ? ProgressNotificationState.Cancelled
                    : ProgressNotificationState.Completed;
            }
        }

        /// <summary>
        /// Requests a background pass that fills in relax SR/PP cache entries missing for the
        /// currently-selected Relax PP system. Data stored for the other system is left intact.
        /// Safe to call repeatedly: concurrent passes are coalesced, and a request arriving while
        /// a pass is running re-runs it afterwards so a mid-run system switch is fully handled.
        /// </summary>
        public static void QueueRelaxPpRecalculation()
        {
            // The pass itself needs the component's resolved dependencies; if the
            // processor hasn't loaded yet there is nothing to recalculate with.
            if (instance == null)
            {
                Logger.Log("Relax PP recalculation requested before processor load; skipped.");
                return;
            }

            instance.queueRelaxPpRecalculation();
        }

        private void queueRelaxPpRecalculation()
        {
            if (Interlocked.CompareExchange(ref relaxRecalcRunning, 1, 0) != 0)
            {
                Interlocked.Exchange(ref relaxRecalcPending, 1);
                return;
            }

            Task.Factory.StartNew(() =>
            {
                try
                {
                    do
                    {
                        Interlocked.Exchange(ref relaxRecalcPending, 0);
                        populateMissingPerformancePoints(relaxOnly: true);
                    }
                    while (Interlocked.Exchange(ref relaxRecalcPending, 0) == 1);
                }
                finally
                {
                    Interlocked.Exchange(ref relaxRecalcRunning, 0);
                }
            }, TaskCreationOptions.LongRunning).ContinueWith(t =>
            {
                if (t.Exception?.GetBaseException() is ObjectDisposedException)
                {
                    Logger.Log("Relax PP recalculation aborted during shutdown");
                    return;
                }

                if (t.Exception != null)
                    Logger.Error(t.Exception.GetBaseException(), "Relax PP recalculation failed");
            });
        }

        private void processOnlineBeatmapSetsWithNoUpdate()
        {
            HashSet<Guid> beatmapSetIds = new HashSet<Guid>();

            Logger.Log("Querying for beatmap sets to reprocess...");

            realmAccess.Run(r =>
            {
                // BeatmapProcessor is responsible for both online and local processing.
                // In the case a user isn't logged in, it won't update LastOnlineUpdate and therefore re-queue,
                // causing overhead from the non-online processing to redundantly run every startup.
                //
                // We may eventually consider making the Process call more specific (or avoid this in any number
                // of other possible ways), but for now avoid queueing if the user isn't logged in at startup.
                if (api.IsLoggedIn)
                {
                    foreach (var b in r.All<BeatmapInfo>().Where(b => b.OnlineID > 0 && b.LastOnlineUpdate == null && b.BeatmapSet != null))
                        beatmapSetIds.Add(b.BeatmapSet!.ID);
                }
            });

            if (beatmapSetIds.Count == 0)
                return;

            Logger.Log($"Found {beatmapSetIds.Count} beatmap sets which require online updates.");

            var notification = showProgressNotification(beatmapSetIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3245, global::osu.Game.Localisation.ClientTranslationStrings.J3246);

            int processedCount = 0;
            int failedCount = 0;

            foreach (var id in beatmapSetIds)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, beatmapSetIds.Count);

                sleepIfRequired();

                realmAccess.Run(r =>
                {
                    var set = r.Find<BeatmapSetInfo>(id);

                    if (set != null)
                    {
                        try
                        {
                            beatmapUpdater.Process(set, skipCalculation: b => calculationFailures.ShouldSkip(b.ID, b.MD5Hash));
                            ++processedCount;
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Background processing failed on {set}: {e}");
                            ++failedCount;
                        }
                    }
                });
            }

            completeNotification(notification, processedCount, beatmapSetIds.Count, failedCount);
        }

        private void processBeatmapsWithMissingObjectCounts()
        {
            Logger.Log("Querying for beatmaps with missing hitobject counts to reprocess...");

            HashSet<Guid> beatmapIds = new HashSet<Guid>();

            realmAccess.Run(r =>
            {
                foreach (var b in r.All<BeatmapInfo>().Where(b => b.TotalObjectCount < 0 || b.EndTimeObjectCount < 0))
                {
                    if (!calculationFailures.ShouldSkip(b.ID, b.MD5Hash))
                        beatmapIds.Add(b.ID);
                }
            });

            if (beatmapIds.Count == 0)
                return;

            Logger.Log($"Found {beatmapIds.Count} beatmaps which require statistics population.");

            var notification = showProgressNotification(beatmapIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3247, global::osu.Game.Localisation.ClientTranslationStrings.J3248);

            int processedCount = 0;
            int failedCount = 0;

            foreach (var id in beatmapIds)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, beatmapIds.Count);

                sleepIfRequired();

                realmAccess.Run(r =>
                {
                    var beatmap = r.Find<BeatmapInfo>(id);

                    if (beatmap != null && !calculationFailures.ShouldSkip(beatmap.ID, beatmap.MD5Hash))
                    {
                        try
                        {
                            beatmapUpdater.ProcessObjectCounts(beatmap);
                            ++processedCount;
                        }
                        catch (Exception e)
                        {
                            recordCalculationFailure(beatmap, e, notification);
                            Logger.Log($"Background processing failed on {beatmap}: {e}");
                            ++failedCount;
                        }
                    }
                });
            }

            completeNotification(notification, processedCount, beatmapIds.Count, failedCount);
        }

        private void processScoresWithMissingStatistics()
        {
            HashSet<Guid> scoreIds = new HashSet<Guid>();

            Logger.Log("Querying for scores to reprocess...");

            realmAccess.Run(r =>
            {
                foreach (var score in r.All<ScoreInfo>().Where(s => !s.BackgroundReprocessingFailed))
                {
                    if (score.BeatmapInfo != null
                        && score.Statistics.Sum(kvp => kvp.Value) > 0
                        && score.MaximumStatistics.Sum(kvp => kvp.Value) == 0)
                    {
                        scoreIds.Add(score.ID);
                    }
                }
            });

            if (scoreIds.Count == 0)
                return;

            Logger.Log($"Found {scoreIds.Count} scores which require statistics population.");

            var notification = showProgressNotification(scoreIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3249, global::osu.Game.Localisation.ClientTranslationStrings.J3250);

            int processedCount = 0;
            int failedCount = 0;

            foreach (var id in scoreIds)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, scoreIds.Count);

                sleepIfRequired();

                try
                {
                    var score = scoreManager.Query(s => s.ID == id);

                    if (score != null)
                    {
                        scoreManager.PopulateMaximumStatistics(score);

                        // Can't use async overload because we're not on the update thread.
                        // ReSharper disable once MethodHasAsyncOverload
                        realmAccess.Write(r =>
                        {
                            r.Find<ScoreInfo>(id)!.MaximumStatisticsJson = JsonConvert.SerializeObject(score.MaximumStatistics);
                        });
                    }

                    ++processedCount;
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger.Log(@$"Failed to populate maximum statistics for {id}: {e}");
                    realmAccess.Write(r => r.Find<ScoreInfo>(id)!.BackgroundReprocessingFailed = true);
                    ++failedCount;
                }
            }

            completeNotification(notification, processedCount, scoreIds.Count, failedCount);
        }

        private void upgradeModMultipliers()
        {
            Logger.Log("Querying for scores that need mod multiplier upgrade...");

            HashSet<Guid> scoreIds = realmAccess.Run(r => new HashSet<Guid>(
                r.All<ScoreInfo>()
                 .Where(s => !s.BackgroundReprocessingFailed
                             && s.BeatmapInfo != null
                             && s.TotalScoreVersion < 30000020 // version number represents version with latest mod multiplier change
                             && s.TotalScoreWithoutMods > 0)
                 .AsEnumerable()
                 // must be done after materialisation, as realm doesn't want to support
                 // nested property predicates
                 .Where(s => s.Ruleset.IsLegacyRuleset())
                 .Select(s => s.ID)));

            Logger.Log($"Found {scoreIds.Count} scores which require mod multiplier upgrade.");

            if (scoreIds.Count == 0)
                return;

            var notification = showProgressNotification(scoreIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3251, global::osu.Game.Localisation.ClientTranslationStrings.J3252);

            int processedCount = 0;
            int failedCount = 0;

            foreach (var id in scoreIds)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, scoreIds.Count);

                sleepIfRequired();

                try
                {
                    // Can't use async overload because we're not on the update thread.
                    // ReSharper disable once MethodHasAsyncOverload
                    realmAccess.Write(r =>
                    {
                        ScoreInfo s = r.Find<ScoreInfo>(id)!;
                        if (s.BeatmapInfo == null)
                            return;

                        StandardisedScoreMigrationTools.UpdateToLatestScoreMultipliers(s, s.BeatmapInfo.Difficulty);
                        s.TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION;
                    });

                    ++processedCount;
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to upgrade mod multipliers for {id}: {e}");
                    realmAccess.Write(r => r.Find<ScoreInfo>(id)!.BackgroundReprocessingFailed = true);
                    ++failedCount;
                }
            }

            completeNotification(notification, processedCount, scoreIds.Count, failedCount);
        }

        private void convertLegacyTotalScoreToStandardised()
        {
            Logger.Log("Querying for scores that need total score conversion...");

            HashSet<Guid> scoreIds = realmAccess.Run(r => new HashSet<Guid>(
                r.All<ScoreInfo>()
                 .Where(s => !s.BackgroundReprocessingFailed
                             && s.BeatmapInfo != null
                             && s.IsLegacyScore
                             && s.TotalScoreVersion < LegacyScoreEncoder.LATEST_VERSION)
                 .AsEnumerable()
                 // must be done after materialisation, as realm doesn't want to support
                 // nested property predicates
                 .Where(s => s.Ruleset.IsLegacyRuleset())
                 .Select(s => s.ID)));

            Logger.Log($"Found {scoreIds.Count} scores which require total score conversion.");

            if (scoreIds.Count == 0)
                return;

            var notification = showProgressNotification(scoreIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3253, global::osu.Game.Localisation.ClientTranslationStrings.J3254);

            int processedCount = 0;
            int failedCount = 0;

            foreach (var id in scoreIds)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, scoreIds.Count);

                sleepIfRequired();

                try
                {
                    // Can't use async overload because we're not on the update thread.
                    // ReSharper disable once MethodHasAsyncOverload
                    realmAccess.Write(r =>
                    {
                        ScoreInfo s = r.Find<ScoreInfo>(id)!;
                        StandardisedScoreMigrationTools.UpdateFromLegacy(s, beatmapManager.GetWorkingBeatmap(s.BeatmapInfo));
                        s.TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION;
                    });

                    ++processedCount;
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to convert total score for {id}: {e}");
                    realmAccess.Write(r => r.Find<ScoreInfo>(id)!.BackgroundReprocessingFailed = true);
                    ++failedCount;
                }
            }

            completeNotification(notification, processedCount, scoreIds.Count, failedCount);
        }

        private void upgradeScoreRanks()
        {
            Logger.Log("Querying for scores that need rank upgrades...");

            HashSet<Guid> scoreIds = realmAccess.Run(r => new HashSet<Guid>(
                r.All<ScoreInfo>()
                 .Where(s => s.TotalScoreVersion < 30000013 && !s.BackgroundReprocessingFailed) // last total score version with a significant change to ranks
                 .AsEnumerable()
                 // must be done after materialisation, as realm doesn't support
                 // filtering on nested property predicates or projection via `.Select()`
                 .Where(s => s.Ruleset.IsLegacyRuleset())
                 .Select(s => s.ID)));

            Logger.Log($"Found {scoreIds.Count} scores which require rank upgrades.");

            if (scoreIds.Count == 0)
                return;

            var notification = showProgressNotification(scoreIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3255, global::osu.Game.Localisation.ClientTranslationStrings.J3256);

            int processedCount = 0;
            int failedCount = 0;

            foreach (var id in scoreIds)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, scoreIds.Count);

                sleepIfRequired();

                try
                {
                    // Can't use async overload because we're not on the update thread.
                    // ReSharper disable once MethodHasAsyncOverload
                    realmAccess.Write(r =>
                    {
                        ScoreInfo s = r.Find<ScoreInfo>(id)!;
                        s.Rank = StandardisedScoreMigrationTools.ComputeRank(s);
                        s.TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION;
                    });

                    ++processedCount;
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to update rank score {id}: {e}");
                    realmAccess.Write(r => r.Find<ScoreInfo>(id)!.BackgroundReprocessingFailed = true);
                    ++failedCount;
                }
            }

            completeNotification(notification, processedCount, scoreIds.Count, failedCount);
        }

        private void backpopulateMissingSubmissionAndRankDates()
        {
            if (!localMetadataSource.Available)
            {
                Logger.Log("Cannot backpopulate missing submission/rank dates because the local metadata cache is missing.");
                return;
            }

            try
            {
                if (!localMetadataSource.IsAtLeastVersion(2))
                {
                    Logger.Log("Cannot backpopulate missing submission/rank dates because the local metadata cache is too old.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error when trying to query version of local metadata cache: {ex}");
                return;
            }

            Logger.Log("Querying for beatmap sets that contain missing submission/rank date...");

            // find all ranked beatmap sets with missing date ranked or date submitted that have at least one difficulty ranked as well.
            // the reason for checking ranked status of the difficulties is that they can be locally modified or unknown too, and for those the lookup is likely to fail.
            // this is because metadata lookups are primarily based on file hash, so they will fail to match if the beatmap does not match the online version
            // (which is likely to be the case if the beatmap is locally modified or unknown).
            // that said, one difficulty in ranked state is enough for the backpopulation to work.
            HashSet<Guid> beatmapSetIds = realmAccess.Run(r => new HashSet<Guid>(
                r.All<BeatmapSetInfo>()
                 .Filter($@"{nameof(BeatmapSetInfo.StatusInt)} > 0 && ({nameof(BeatmapSetInfo.DateRanked)} == null || {nameof(BeatmapSetInfo.DateSubmitted)} == null) "
                         + $@"&& ANY {nameof(BeatmapSetInfo.Beatmaps)}.{nameof(BeatmapInfo.StatusInt)} > 0")
                 .AsEnumerable()
                 .Select(b => b.ID)));

            if (beatmapSetIds.Count == 0)
                return;

            Logger.Log($"Found {beatmapSetIds.Count} beatmap sets with missing submission/rank date.");

            var notification = showProgressNotification(beatmapSetIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3257, global::osu.Game.Localisation.ClientTranslationStrings.J3258);

            int processedCount = 0;
            int failedCount = 0;

            foreach (var id in beatmapSetIds)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, beatmapSetIds.Count);

                sleepIfRequired();

                try
                {
                    // Can't use async overload because we're not on the update thread.
                    // ReSharper disable once MethodHasAsyncOverload
                    bool succeeded = realmAccess.Write(r =>
                    {
                        BeatmapSetInfo beatmapSet = r.Find<BeatmapSetInfo>(id)!;

                        var beatmap = beatmapSet.Beatmaps.First(b => b.Status >= BeatmapOnlineStatus.Ranked);

                        bool lookupSucceeded = localMetadataSource.TryLookup(beatmap, out var result);

                        if (lookupSucceeded)
                        {
                            Debug.Assert(result != null);
                            beatmapSet.DateRanked = result.DateRanked;
                            beatmapSet.DateSubmitted = result.DateSubmitted;
                            return true;
                        }

                        Logger.Log($"Could not find {beatmapSet.GetDisplayString()} in local cache while backpopulating missing submission/rank date");
                        return false;
                    });

                    if (succeeded)
                        ++processedCount;
                    else
                        ++failedCount;
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to update ranked/submitted dates for beatmap set {id}: {e}");
                    ++failedCount;
                }
            }

            completeNotification(notification, processedCount, beatmapSetIds.Count, failedCount);
        }

        private void backpopulateUserTags()
        {
            if (!localMetadataSource.Available || !localMetadataSource.IsAtLeastVersion(3))
            {
                Logger.Log(@"Local metadata cache doesn't exist, or has too low version to backpopulate user tags, attempting refetch...");
                localMetadataSource.FetchCache().WaitSafely();

                if (!localMetadataSource.Available || !localMetadataSource.IsAtLeastVersion(3))
                {
                    Logger.Log(@"Local metadata cache refetch failed. Aborting user tags backpopulation.");
                    return;
                }
            }

            var lastPopulation = config.Get<DateTime?>(OsuSetting.LastOnlineTagsPopulation);
            // dropping time data here completely is intentional, because storing the date to config is a lossy operation
            // (truncates some ticks off of the date when it's being converted to string and back).
            // therefore, if precision isn't explicitly constrained, the condition below would always fail just because the date stored to config
            // is less accurate than the cache file's fetch date which is stored with higher precision in the filesystem metadata.
            var metadataSourceFetchDate = localMetadataSource.GetCacheFetchDate()?.Date;

            if (metadataSourceFetchDate <= lastPopulation)
            {
                Logger.Log(
                    $@"Skipping user tag population because the local metadata source hasn't been updated since the last time user tags were checked ({lastPopulation.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)})");
                return;
            }

            Logger.Log(@"Updating user tags");

            // while this is constrained to run every month or so (every time a new online.db cache is retrieved), there's some chance that this will still run much too often and be annoying to users.
            // if that turns out to be the case we may need a better way to debounce this (or just delete the backpopulation logic after some time has passed?)
            HashSet<Guid> beatmapIds = realmAccess.Run(r => new HashSet<Guid>(
                r.All<BeatmapInfo>()
                 .Filter($"{nameof(BeatmapInfo.StatusInt)} IN {{ 1,2,4 }}")
                 .AsEnumerable()
                 .Select(b => b.ID)));

            if (beatmapIds.Count == 0)
                return;

            Logger.Log($@"Checking for tag updates for {beatmapIds.Count} beatmaps.");

            var notification = showProgressNotification(beatmapIds.Count, global::osu.Game.Localisation.ClientTranslationStrings.J3259,
                global::osu.Game.Localisation.ClientTranslationStrings.J3260);

            int processedCount = 0;
            int updatedCount = 0;
            int failedCount = 0;

            foreach (var id in beatmapIds)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, beatmapIds.Count);

                sleepIfRequired();

                try
                {
                    var beatmap = realmAccess.Run(r => r.Find<BeatmapInfo>(id)?.Detach());

                    if (beatmap == null) continue;

                    bool lookupSucceeded = localMetadataSource.TryLookup(beatmap, out var result);

                    if (lookupSucceeded)
                    {
                        Debug.Assert(result != null);

                        HashSet<string> userTags = result.UserTags.ToHashSet();

                        if (!userTags.SetEquals(beatmap.Metadata.UserTags))
                        {
                            ++updatedCount;
                            realmAccess.Write(r =>
                            {
                                beatmap = r.Find<BeatmapInfo>(id);

                                if (beatmap == null)
                                    return;

                                beatmap.Metadata.UserTags.Clear();
                                beatmap.Metadata.UserTags.AddRange(userTags);
                            });
                        }
                    }
                    else
                    {
                        Logger.Log(@$"Could not find {beatmap.GetDisplayString()} in local cache while backpopulating missing user tags");
                    }

                    ++processedCount;
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger.Log(@$"Failed to update user tags for beatmap {id}: {e}");
                    ++failedCount;
                }
            }

            // Report the updated item count rather than the total processed. Users don't really care about noops here.
            completeNotification(notification, updatedCount, updatedCount, failedCount);

            config.SetValue(OsuSetting.LastOnlineTagsPopulation, metadataSourceFetchDate);
        }

        private void recordCalculationFailure(BeatmapInfo beatmap, Exception error, ProgressNotification? notification)
        {
            calculationFailures.Record(beatmap.ID, beatmap.MD5Hash, error,
                shutdownCancellation.IsCancellationRequested
                || (libraryWorker && processingCancellation.IsCancellationRequested)
                || notification?.State == ProgressNotificationState.Cancelled
                || notification?.CancellationToken.IsCancellationRequested == true);
        }

        private IDisposable enterCalculationSlot(BeatmapInfo beatmap)
            => BackgroundCalculationLimiter.Enter(config.Get<int>(OsuSetting.ForkBackgroundCalculationConcurrency),
                config.Get<int>(OsuSetting.ForkBackgroundCalculationDutyPercent), beatmap.Ruleset.ShortName,
                calculationPolicy.Concurrency(beatmap.Ruleset.ShortName),
                libraryWorker ? processingCancellation.Token : shutdownCancellation.Token, deferWhileInteracting: true);

        private void runCalculations(IEnumerable<Guid> ids, Action<Guid> calculate)
        {
            bool cancellable = libraryWorker;
            var token = cancellable ? processingCancellation.Token : shutdownCancellation.Token;
            Parallel.ForEach(ids, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.Get<int>(OsuSetting.ForkBackgroundCalculationConcurrency),
                CancellationToken = token,
            }, id =>
            {
                bool previous = libraryWorker;
                libraryWorker = cancellable;
                try
                {
                    token.ThrowIfCancellationRequested();
                    calculate(id);
                }
                finally
                {
                    libraryWorker = previous;
                }
            });
        }

        private void updateNotificationProgress(ProgressNotification? notification, int processedCount, int totalCount)
        {
            if (notification == null)
                return;

            Scheduler.Add(() =>
            {
                if (notification.State != ProgressNotificationState.Active)
                    return;
                notification.Text = notification.Text.ToString().Split('(').First().TrimEnd() + $" ({processedCount} of {totalCount})";
                notification.Progress = Math.Max(notification.Progress, (float)processedCount / totalCount);
            });
        }

        private void completeNotification(ProgressNotification? notification, int processedCount, int totalCount, int? failedCount = null)
        {
            if (notification == null)
                return;

            if (totalCount == 0)
            {
                notification.CompleteSilently();
            }
            else if (processedCount == totalCount)
            {
                notification.CompletionText = $"{processedCount} {notification.CompletionText}";
                notification.Progress = 1;
                notification.State = ProgressNotificationState.Completed;
            }
            else
            {
                notification.Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3262(processedCount, totalCount, notification.CompletionText);

                // We may have arrived here due to user cancellation or completion with failures.
                if (failedCount > 0)
                    notification.Text += $" Check logs for issues with {failedCount} failed items.";

                notification.State = ProgressNotificationState.Cancelled;
            }
        }

        private ProgressNotification? showProgressNotification(int totalCount, LocalisableString running, LocalisableString completed, int minimumCount = 1)
        {
            if (notificationOverlay == null)
                return null;

            if (totalCount < minimumCount)
                return null;

            ProgressNotification notification = new ProgressNotification
            {
                Text = running,
                CompletionText = completed,
                State = ProgressNotificationState.Active
            };

            notificationOverlay?.Post(notification);

            return notification;
        }

        protected override void Dispose(bool isDisposing)
        {
            shutdownCancellation.Cancel();
            processingCancellation.Cancel();
            if (ReferenceEquals(instance, this))
                instance = null;
            base.Dispose(isDisposing);
        }

        private bool performanceTestActive() => GameplayPerformanceSnapshot.BenchmarkRunnerActive || performanceDiagnosticsManager?.IsRunning == true;

        private void sleepIfRequired()
        {
            // Importantly, also sleep if high performance session is active.
            // If we don't do this, memory usage can become runaway due to GC running in a more lenient mode.
            var token = libraryWorker ? processingCancellation.Token : shutdownCancellation.Token;
            token.ThrowIfCancellationRequested();
            while ((libraryWorker && LibraryPaused)
                   || (localUserPlayInfo != null && localUserPlayInfo.PlayingState.Value != LocalUserPlayingState.NotPlaying)
                   || highPerformanceSessionManager?.IsSessionActive == true
                   || performanceTestActive())
            {
                Logger.Log("Background processing sleeping due to active gameplay...");
                if (token.WaitHandle.WaitOne(Math.Min(TimeToSleepDuringGameplay, 250)))
                    token.ThrowIfCancellationRequested();
            }
        }
    }
}
