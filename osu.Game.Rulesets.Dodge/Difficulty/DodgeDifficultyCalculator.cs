// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Dodge.Difficulty
{
    public class DodgeDifficultyCalculator : DifficultyCalculator
    {
        public override int Version => 2026072903;

        private IBeatmap? cachedAnalysisBeatmap;
        private DodgeDifficultyAttributes? cachedBaseAttributes;

        internal int AutoplayCalculationCount { get; private set; }

        public DodgeDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            // CalculateTimed() invokes this method once for every progressive
            // beatmap prefix. Running the beam-search route planner for every
            // prefix made a single difficulty graph allocate close to a gigabyte
            // on dense maps and caused long Gen2 pauses during gameplay.
            //
            // The full-map route is the expensive part and contains the useful
            // density/pressure measurements. Reuse it for progressive points and
            // cheaply ramp the displayed cumulative difficulty instead.
            bool isProgressiveCalculation = !ReferenceEquals(beatmap, Beatmap);
            IBeatmap analysisBeatmap = isProgressiveCalculation ? Beatmap : beatmap;
            DodgeDifficultyAttributes attributes;

            if (isProgressiveCalculation && !ReferenceEquals(cachedAnalysisBeatmap, analysisBeatmap))
            {
                // Timed difficulty is requested in the background while gameplay
                // is already running. The database value was computed by the full
                // calculator beforehand, so use it as the graph baseline instead
                // of allocating a complete autoplay search history mid-play.
                attributes = new DodgeDifficultyAttributes
                {
                    StarRating = Math.Max(0, analysisBeatmap.BeatmapInfo.StarRating),
                };
            }
            else
            {
                if (!ReferenceEquals(cachedAnalysisBeatmap, analysisBeatmap))
                {
                    cachedAnalysisBeatmap = analysisBeatmap;
                    cachedBaseAttributes = DodgeAutoplayDifficultyEvaluator.Calculate(analysisBeatmap);
                    AutoplayCalculationCount++;
                }

                attributes = clone(cachedBaseAttributes!);
            }

            if (isProgressiveCalculation && analysisBeatmap.HitObjects.Count > 0)
            {
                double progress = Math.Clamp((double)beatmap.HitObjects.Count / analysisBeatmap.HitObjects.Count, 0, 1);
                double cumulativeDifficulty = Math.Sqrt(progress);
                attributes.StarRating *= cumulativeDifficulty;
                attributes.MovementDifficulty *= cumulativeDifficulty;
                attributes.PressureDifficulty *= cumulativeDifficulty;
            }

            double clockRate = mods.OfType<IApplicableToRate>()
                                   .Aggregate(1.0, (rate, mod) => mod.ApplyToRate(0, rate));

            if (double.IsFinite(clockRate) && clockRate > 0)
            {
                // Keep this exponent in sync with ModMosuTargetDifficulty.
                // Dodge's calibrated star rating scales approximately by rate^1.35.
                double rateDifficultyMultiplier = Math.Pow(clockRate, 1.35);
                attributes.StarRating = Math.Clamp(attributes.StarRating * rateDifficultyMultiplier, 0, 15);
                attributes.MovementDifficulty *= rateDifficultyMultiplier;
                attributes.PressureDifficulty *= rateDifficultyMultiplier;
                attributes.ProjectileRate *= clockRate;
            }

            attributes.Mods = mods;
            attributes.MaxCombo = beatmap.HitObjects.Count;
            return attributes;
        }

        private static DodgeDifficultyAttributes clone(DodgeDifficultyAttributes source) => new DodgeDifficultyAttributes
        {
            StarRating = source.StarRating,
            MovementDifficulty = source.MovementDifficulty,
            PressureDifficulty = source.PressureDifficulty,
            ProjectileRate = source.ProjectileRate,
            PeakActiveProjectiles = source.PeakActiveProjectiles,
            AutoplayTeleports = source.AutoplayTeleports,
        };

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
            => Enumerable.Empty<DifficultyHitObject>();

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods) => Array.Empty<Skill>();
    }
}
