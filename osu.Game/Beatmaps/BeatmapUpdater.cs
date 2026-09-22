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
using osu.Game.Database;
using osu.Game.Online.API;
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

        private const int update_queue_request_concurrency = 4;

        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(update_queue_request_concurrency, nameof(BeatmapUpdaterMetadataLookup));

        public BeatmapUpdater(IWorkingBeatmapCache workingBeatmapCache, BeatmapDifficultyCache difficultyCache, IAPIProvider api, Storage storage)
        {
            this.workingBeatmapCache = workingBeatmapCache;
            this.difficultyCache = difficultyCache;

            metadataLookup = new BeatmapUpdaterMetadataLookup(api, storage);
        }

        public void Queue(Live<BeatmapSetInfo> beatmapSet, MetadataLookupScope lookupScope = MetadataLookupScope.LocalCacheFirst)
        {
            Logger.Log($"Queueing change for local beatmap {beatmapSet}");
            Task.Factory.StartNew(() => beatmapSet.PerformRead(b => Process(b, lookupScope)), CancellationToken.None, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously,
                updateScheduler);
        }

        public void Process(BeatmapSetInfo beatmapSet, MetadataLookupScope lookupScope = MetadataLookupScope.LocalCacheFirst)
        {
            var ppResults = new Dictionary<Guid, (double Pp, int Version)>();
            var relaxResults = new Dictionary<Guid, ForkDataStore.RelaxBeatmapData>();
            var dodgeDifficultyResults = new Dictionary<Guid, (string Checksum, DifficultyAttributes Attributes, int Version)>();

            beatmapSet.Realm!.Write(_ =>
            {
                // Before we use below, we want to invalidate.
                workingBeatmapCache.Invalidate(beatmapSet);

                if (lookupScope != MetadataLookupScope.None)
                    metadataLookup.Update(beatmapSet, lookupScope == MetadataLookupScope.OnlineFirst);

                foreach (BeatmapInfo beatmap in beatmapSet.Beatmaps)
                {
                    var working = workingBeatmapCache.GetWorkingBeatmap(beatmap);

                    difficultyCache.Invalidate(beatmap, working.BeatmapInfo);

                    var ruleset = working.BeatmapInfo.Ruleset.CreateInstance();
                    var calculator = ruleset.CreateDifficultyCalculator(working);

                    var difficultyAttributes = calculator.Calculate();
                    beatmap.StarRating = difficultyAttributes.StarRating;

                    if (ruleset.RulesetInfo.ShortName == RulesetInfo.DODGE_MODE_SHORTNAME)
                    {
                        dodgeDifficultyResults[beatmap.ID] = (
                            beatmap.MD5Hash,
                            difficultyAttributes,
                            calculator.Version);
                    }

                    try
                    {
                        var performanceCalculator = ruleset.CreatePerformanceCalculator();

                        if (performanceCalculator != null)
                        {
                            var playableBeatmap = working.GetPlayableBeatmap(ruleset.RulesetInfo);
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
                            ppResults[beatmap.ID] = (double.IsFinite(performance.Total) ? performance.Total : 0, calculator.Version);

                            if (ruleset.GetModsFor(ModType.Automation).OfType<ModRelax>().FirstOrDefault() is ModRelax relaxMod)
                            {
                                Mod[] relaxMods = { relaxMod };
                                var relaxDifficulty = calculator.Calculate(relaxMods);
                                var playableRelaxBeatmap = working.GetPlayableBeatmap(ruleset.RulesetInfo, relaxMods);
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
                                double relaxPp = double.IsFinite(relaxPerformance.Total) ? relaxPerformance.Total : 0;
                                relaxResults[beatmap.ID] = new ForkDataStore.RelaxBeatmapData(relaxDifficulty.StarRating, relaxPp);
                            }
                        }
                        else
                        {
                            ppResults[beatmap.ID] = (0, calculator.Version);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Failed to compute PP for {beatmap.OnlineID}: {e.Message}");
                    }

                    beatmap.UpdateStatisticsFromBeatmap(working.Beatmap);
                }

                // And invalidate again afterwards as re-fetching the most up-to-date database metadata will be required.
                workingBeatmapCache.Invalidate(beatmapSet);
            });

            var forkStore = ForkDataStore.Instance;

            if (forkStore != null)
            {
                foreach (var (id, result) in ppResults)
                    forkStore.SetPP(id, result.Pp, result.Version);

                foreach (var (id, result) in relaxResults)
                    forkStore.SetRelaxData(id, result.StarRating, result.MaxPerformancePoints);

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
            if (metadataLookup.IsNotNull())
                metadataLookup.Dispose();

            if (updateScheduler.IsNotNull())
                updateScheduler.Dispose();
        }

        #endregion
    }
}
