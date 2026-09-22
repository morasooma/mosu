// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Difficulty
{
    public abstract class PerformanceCalculator
    {
        protected readonly Ruleset Ruleset;

        protected PerformanceCalculator(Ruleset ruleset)
        {
            Ruleset = ruleset;
        }

        public Task<PerformanceAttributes> CalculateAsync(ScoreInfo score, DifficultyAttributes attributes, CancellationToken cancellationToken)
            => Task.Run(() => CreatePerformanceAttributes(score, attributes), cancellationToken);

        public PerformanceAttributes Calculate(ScoreInfo score, DifficultyAttributes attributes)
            => CreatePerformanceAttributes(score, attributes);

        public PerformanceAttributes Calculate(ScoreInfo score, IWorkingBeatmap beatmap)
            => Calculate(score, Ruleset.CreateDifficultyCalculator(beatmap).Calculate(score.Mods));

        /// <summary>
        /// Whether the live performance display can be recalculated after a judgement changes.
        /// </summary>
        /// <remarks>
        /// Some rulesets use score state which only becomes internally consistent after a
        /// multi-part hit object has completed.
        /// </remarks>
        public virtual bool ShouldCalculateLivePerformance(ScoreInfo score, HitObject hitObject, bool isReverting) => true;

        /// <summary>
        /// Whether live performance calculation is too expensive to run on the gameplay update thread.
        /// </summary>
        /// <remarks>
        /// Calculators opting into this path are run serially in the background. If judgements arrive
        /// faster than a calculation can complete, only the newest pending score state is retained.
        /// </remarks>
        public virtual bool RequiresBackgroundLivePerformanceCalculation(ScoreInfo score) => false;

        /// <summary>
        /// Whether reverting <paramref name="hitObject"/> should restore the live performance
        /// value from before that judgement rather than recalculate it.
        /// </summary>
        public virtual bool ShouldRestorePreviousLivePerformance(ScoreInfo score, HitObject hitObject) => false;

        /// <summary>
        /// Creates <see cref="PerformanceAttributes"/> to describe a score's performance.
        /// </summary>
        /// <param name="score">The score to create the attributes for.</param>
        /// <param name="attributes">The difficulty attributes for the beatmap relating to the score.</param>
        protected abstract PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes);
    }
}
