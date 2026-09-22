// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Dodge.UI;

namespace osu.Game.Rulesets.Dodge.Difficulty
{
    /// <summary>
    /// Converts route availability and movement measurements from autoplay
    /// analysis into a user-facing star rating.
    /// </summary>
    public static class DodgeAutoplayDifficultyEvaluator
    {
        public static DodgeDifficultyAttributes Calculate(IBeatmap beatmap)
        {
            // Difficulty measures the authored hitboxes. Gameplay autoplay adds
            // a small safety margin for robust replay playback, which must not
            // alter star rating or player-size comparisons.
            var generator = new DodgeAutoGenerator(beatmap, collisionSafetyMargin: 0);
            generator.Generate();
            return FromAnalysis(generator.Analysis);
        }

        /// <summary>
        /// Converts an already generated autoplay analysis into difficulty
        /// attributes. This avoids generating the same expensive route twice in
        /// editor diagnostics and regression tooling.
        /// </summary>
        public static DodgeDifficultyAttributes FromAnalysis(DodgeAutoplayAnalysis analysis)
        {
            if (analysis.ProjectileCount == 0)
                return new DodgeDifficultyAttributes();

            // Each component is converted to the same star-equivalent scale
            // before combining them. A p-norm rewards maps which demand several
            // skills without linearly counting correlated movement/path strain
            // twice. Player size and speed are already part of route geometry.
            double movementDifficulty = 6 * Math.Pow(Math.Max(0, analysis.MovementDifficulty), 1.1);
            double pathDifficulty = 2.7 * Math.Pow(Math.Max(0, analysis.PathDifficulty), 0.9);
            double readingDifficulty = 1.5 * Math.Pow(Math.Max(0, analysis.ReadingDifficulty), 1.1);
            const double skill_norm_exponent = 1.5;
            double combinedDifficulty = 0.6 * Math.Pow(
                Math.Pow(movementDifficulty, skill_norm_exponent)
                + Math.Pow(pathDifficulty, skill_norm_exponent)
                + Math.Pow(readingDifficulty, skill_norm_exponent),
                1 / skill_norm_exponent);
            // Collision geometry already captures most of player-size impact,
            // but route selection can occasionally make a larger player move
            // slightly less. A modest symmetric correction keeps the setting
            // monotonic without the old large-player-only double penalty.
            double playerSizeMultiplier = Math.Pow(
                Math.Clamp(analysis.PlayerSize / DodgePlayer.SIZE, 0.375, 2),
                0.35);
            // Restore the middle of the scale after urgency weighting removes
            // slow, non-imminent pattern load, while retaining useful separation
            // between genuinely high multi-skill maps. A linear map compressed
            // the regression corpus back into a sub-one-star range.
            double calibratedDifficulty = combinedDifficulty * combinedDifficulty / 2.5;
            double starRating = 0.15 + calibratedDifficulty * playerSizeMultiplier;
            // A fallback means that this bounded heuristic planner failed to
            // find a route. It is useful diagnostic information, but is not a
            // proof that no human-perfect route exists (pixel gaps are the
            // obvious counterexample). Never turn planner failure into 15 stars.
            bool isUnclearable = false;

            return new DodgeDifficultyAttributes
            {
                StarRating = Math.Clamp(starRating, 0, 15),
                MovementDifficulty = movementDifficulty,
                PathDifficulty = pathDifficulty,
                // Retain the legacy field for API consumers until they migrate
                // to the correctly named path difficulty attribute.
                PressureDifficulty = pathDifficulty,
                ReadingDifficulty = readingDifficulty,
                DifficultSectionCount = analysis.DifficultSectionCount,
                RelevantPatternCount = analysis.RelevantPatternCount,
                EffectiveReadingPatternCount = analysis.EffectiveReadingPatternCount,
                ProjectileRate = analysis.ProjectileRate,
                PeakActiveProjectiles = analysis.PeakActiveProjectiles,
                PeakConcurrentPatterns = analysis.PeakConcurrentPatterns,
                AutoplayTeleports = analysis.TeleportCount,
                AutoplayCollisions = analysis.CollisionCount,
                IsUnclearable = isUnclearable,
            };
        }
    }
}
