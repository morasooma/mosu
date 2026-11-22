// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Screens.Play.HUD
{
    public abstract partial class PerformancePointsCounter : RollingCounter<int>
    {
        public bool UsesFixedAnchor { get; set; }

        [Resolved(CanBeNull = true)]
        private ScoreProcessor scoreProcessor { get; set; }

        [Resolved]
        private GameplayState gameplayState { get; set; }

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap> sourceBeatmap { get; set; }

        [CanBeNull]
        private List<TimedDifficultyAttributes> timedAttributes;

        private readonly CancellationTokenSource loadCancellationSource = new CancellationTokenSource();

        private JudgementResult lastJudgement;
        private PerformanceCalculator performanceCalculator;
        private ScoreInfo scoreInfo;

        private Mod[] clonedMods;

        /// <summary>
        /// Some calculators (notably osu! Realistik Relax) run a full O(N) difficulty pass over
        /// the played beatmap prefix. Throttle only those calculators; lightweight calculators
        /// retain their original per-judgement update behaviour.
        /// </summary>
        private double lastLivePerformanceUpdateTime = double.NegativeInfinity;

        private const double live_performance_throttle_seconds = 1.5;

        private ProgressivePerformanceCalculation progressivePerformance;
        private LivePerformanceScoreSnapshot progressiveScoreSnapshot;
        private long progressiveEpoch;

        private BackgroundPerformanceCalculation pendingPerformanceCalculation;
        private bool backgroundPerformanceCalculationRunning;
        private long backgroundPerformanceCalculationVersion;
        private readonly LivePerformanceMissSnapshot liveMissSnapshot = new LivePerformanceMissSnapshot();

        [BackgroundDependencyLoader]
        private void load(BeatmapDifficultyCache difficultyCache)
        {
            if (gameplayState != null)
            {
                scoreProcessor ??= gameplayState.ScoreProcessor;
                gameplayState.LivePerformanceCounters.TryAdd(this, 0);
                performanceCalculator = gameplayState.Ruleset.CreatePerformanceCalculator();
                clonedMods = gameplayState.Mods.Select(m => m.DeepClone()).ToArray();

                scoreInfo = new ScoreInfo(gameplayState.Score.ScoreInfo.BeatmapInfo, gameplayState.Score.ScoreInfo.Ruleset) { Mods = clonedMods };

                // Keep the actual gameplay geometry for timed attributes. The prerelease also
                // requests a separately prepared RX-only reference to cap gains from mods.
                var gameplayWorkingBeatmap = new GameplayWorkingBeatmap(gameplayState.Beatmap, clonedMods, sourceBeatmap?.Value);
                var difficultyCalculator = gameplayState.Ruleset.CreateDifficultyCalculator(gameplayWorkingBeatmap);
                if (performanceCalculator != null && difficultyCalculator.SupportsProgressiveCalculation(clonedMods))
                {
                    progressiveScoreSnapshot = new LivePerformanceScoreSnapshot(scoreInfo);
                    progressivePerformance = new ProgressivePerformanceCalculation(
                        token => difficultyCalculator.CreateProgressiveCalculation(clonedMods, token),
                        performanceCalculator,
                        (result, epoch) => Schedule(() =>
                        {
                            if (loadCancellationSource.IsCancellationRequested || epoch != progressiveEpoch)
                                return;
                            publishPerformance(roundPerformance(result));
                        }),
                        exception =>
                        {
                            Logger.Error(exception, "Progressive live performance calculation failed");
                            Schedule(() => IsValid = false);
                        });
                    return;
                }

                difficultyCache.GetTimedDifficultyAttributesAsync(gameplayWorkingBeatmap, gameplayState.Ruleset, clonedMods, loadCancellationSource.Token)
                               .ContinueWith(task => Schedule(() =>
                               {
                                   timedAttributes = task.GetResultSafely();

                                   IsValid = true;

                                   if (lastJudgement != null)
                                       onJudgementChanged(lastJudgement, false);
                               }), TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement += onNewJudgement;
                scoreProcessor.JudgementReverted += onJudgementReverted;
                scoreProcessor.OnResetFromReplayFrame += liveMissSnapshot.Invalidate;
            }

            if (gameplayState?.LastJudgementResult.Value != null)
                onJudgementChanged(gameplayState.LastJudgementResult.Value, false);
        }

        private bool isValid;
        public virtual bool IsValid
        {
            get => isValid;
            set
            {
                isValid = value;
                if (!value && gameplayState != null)
                    gameplayState.LivePerformance.Value = null;
            }
        }

        private void publishPerformance(int value)
        {
            Current.Value = value;
            IsValid = true;
            if (gameplayState != null)
                gameplayState.LivePerformance.Value = value;
        }

        private void onNewJudgement(JudgementResult judgement) => onJudgementChanged(judgement, false);

        private void onJudgementReverted(JudgementResult judgement) => onJudgementChanged(judgement, true);

        private void onJudgementChanged(JudgementResult judgement, bool isReverting)
        {
            lastJudgement = judgement;
            if (isReverting)
                liveMissSnapshot.Invalidate();

            if (progressivePerformance != null && scoreProcessor != null)
            {
                scoreProcessor.PopulateScore(scoreInfo);
                if (!performanceCalculator.ShouldCalculateLivePerformance(scoreInfo, judgement.HitObject, isReverting))
                    return;
                if (isReverting)
                    IsValid = false;
                progressiveEpoch = progressivePerformance.Queue(progressiveScoreSnapshot.Create(scoreInfo), judgement.HitObject.GetEndTime(), isReverting);
                return;
            }

            var attrib = getAttributeAtTime(judgement);

            if (gameplayState == null || attrib == null || scoreProcessor == null)
            {
                IsValid = false;
                return;
            }

            scoreProcessor.PopulateScore(scoreInfo);

            // Some performance calculators cannot produce a meaningful live value while a
            // multi-part hit object is still being judged. Slider ticks, for example, update
            // combo before the slider itself has finished. Let the ruleset decide whether this
            // judgement represents a stable point for a performance recalculation.
            if (performanceCalculator == null || !performanceCalculator.ShouldCalculateLivePerformance(scoreInfo, judgement.HitObject, isReverting))
                return;

            if (performanceCalculator.RequiresBackgroundLivePerformanceCalculation(scoreInfo))
            {
                // Realistik Relax performs an O(passedObjects) pass which becomes progressively
                // more expensive on long maps. Keep that work off the gameplay update thread and
                // retain only the freshest pending score state while a calculation is running.
                double now = judgement.TimeAbsolute;

                if (!isReverting && now - lastLivePerformanceUpdateTime < live_performance_throttle_seconds * 1000)
                    return;

                lastLivePerformanceUpdateTime = now;
                ScoreInfo snapshot = createScoreSnapshot(scoreInfo);
                if (RelaxPpSystemSelection.UsesContextualMissEvidence)
                    snapshot.HitEvents = liveMissSnapshot.Capture(scoreInfo.HitEvents);
                queueBackgroundPerformanceCalculation(snapshot, attrib);
            }
            else
            {
                publishPerformance(roundPerformance(performanceCalculator.Calculate(scoreInfo, attrib)));
            }
        }

        private void queueBackgroundPerformanceCalculation(ScoreInfo score, DifficultyAttributes attributes)
        {
            pendingPerformanceCalculation = new BackgroundPerformanceCalculation(
                score,
                attributes,
                ++backgroundPerformanceCalculationVersion);

            if (!backgroundPerformanceCalculationRunning)
                startNextBackgroundPerformanceCalculation();
        }

        private void startNextBackgroundPerformanceCalculation()
        {
            if (pendingPerformanceCalculation == null || loadCancellationSource.IsCancellationRequested)
                return;

            BackgroundPerformanceCalculation calculation = pendingPerformanceCalculation;
            pendingPerformanceCalculation = null;
            backgroundPerformanceCalculationRunning = true;

            performanceCalculator.CalculateAsync(calculation.Score, calculation.Attributes, loadCancellationSource.Token)
                                 .ContinueWith(task =>
                                 {
                                     if (task.IsFaulted)
                                         Logger.Error(task.Exception.GetBaseException(), "Live performance calculation failed");

                                     PerformanceAttributes result = task.IsCompletedSuccessfully ? task.GetResultSafely() : null;

                                     if (loadCancellationSource.IsCancellationRequested)
                                         return;

                                     Schedule(() =>
                                     {
                                         backgroundPerformanceCalculationRunning = false;

                                         // A more recent score state supersedes this result. Starting the next
                                         // request before publishing prevents the counter from moving backwards.
                                         if (calculation.Version == backgroundPerformanceCalculationVersion && result != null)
                                         {
                                             publishPerformance(roundPerformance(result));
                                         }

                                         startNextBackgroundPerformanceCalculation();
                                     });
                                 }, TaskScheduler.Default);
        }

        /// <summary>
        /// Creates the small immutable subset used by performance calculators. ScoreInfo.DeepClone()
        /// also copies the ever-growing HitEvents collection, turning the attempted optimisation into
        /// another O(N) update-thread cost on long maps.
        /// </summary>
        private static ScoreInfo createScoreSnapshot(ScoreInfo source) => new ScoreInfo(source.BeatmapInfo?.Clone(), source.Ruleset.Clone())
        {
            Mods = source.Mods.Select(mod => mod.DeepClone()).ToArray(),
            Combo = source.Combo,
            MaxCombo = source.MaxCombo,
            Accuracy = source.Accuracy,
            Rank = source.Rank,
            Statistics = new Dictionary<HitResult, int>(source.Statistics),
            MaximumStatistics = new Dictionary<HitResult, int>(source.MaximumStatistics),
            TotalScore = source.TotalScore,
            TotalScoreWithoutMods = source.TotalScoreWithoutMods,
            LegacyTotalScore = source.LegacyTotalScore,
            IsLegacyScore = source.IsLegacyScore,
        };

        private static int roundPerformance(PerformanceAttributes performance)
            => (int)Math.Round(performance.Total, MidpointRounding.AwayFromZero);

        [CanBeNull]
        private DifficultyAttributes getAttributeAtTime(JudgementResult judgement)
        {
            if (timedAttributes == null || timedAttributes.Count == 0)
                return null;

            int attribIndex = timedAttributes.BinarySearch(new TimedDifficultyAttributes(judgement.HitObject.GetEndTime(), null));
            if (attribIndex < 0)
                attribIndex = ~attribIndex - 1;

            return timedAttributes[Math.Clamp(attribIndex, 0, timedAttributes.Count - 1)].Attributes;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement -= onNewJudgement;
                scoreProcessor.JudgementReverted -= onJudgementReverted;
                scoreProcessor.OnResetFromReplayFrame -= liveMissSnapshot.Invalidate;
            }

            if (gameplayState != null)
            {
                gameplayState.LivePerformanceCounters.TryRemove(this, out _);
                if (gameplayState.LivePerformanceCounters.Count == 0)
                    gameplayState.LivePerformance.Value = null;
            }
            loadCancellationSource?.Cancel();
            progressivePerformance?.Dispose();
            pendingPerformanceCalculation = null;
        }

        private sealed record BackgroundPerformanceCalculation(ScoreInfo Score, DifficultyAttributes Attributes, long Version);

        // TODO: This class shouldn't exist, but requires breaking changes to allow DifficultyCalculator to receive an IBeatmap.
        private class GameplayWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap gameplayBeatmap;
            private readonly Mod[] gameplayMods;
            private readonly WorkingBeatmap source;

            public GameplayWorkingBeatmap(IBeatmap gameplayBeatmap, Mod[] gameplayMods, WorkingBeatmap source)
                : base(gameplayBeatmap.BeatmapInfo, null)
            {
                this.gameplayBeatmap = gameplayBeatmap;
                this.gameplayMods = gameplayMods;
                this.source = source;
            }

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
            {
                // Only the prerelease asks for a second, smaller mod set on this wrapper.
                // In particular, do not return the already modded gameplay map as its baseline.
                if (mods.Count < gameplayMods.Length)
                    return (source ?? throw new InvalidOperationException("The Relax PP reference requires the original working beatmap."))
                        .GetPlayableBeatmap(ruleset, mods, cancellationToken);

                return gameplayBeatmap;
            }

            protected override IBeatmap GetBeatmap() => gameplayBeatmap;

            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected internal override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }
    }
}
