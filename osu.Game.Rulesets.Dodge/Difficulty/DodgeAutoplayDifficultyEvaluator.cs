// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Replays;

namespace osu.Game.Rulesets.Dodge.Difficulty
{
    /// <summary>
    /// Converts measurements from the collision-free autoplay route into a
    /// user-facing star rating.
    /// </summary>
    public static class DodgeAutoplayDifficultyEvaluator
    {
        public static DodgeDifficultyAttributes Calculate(IBeatmap beatmap)
        {
            var generator = new DodgeAutoGenerator(beatmap);
            generator.Generate();
            DodgeAutoplayAnalysis analysis = generator.Analysis;

            if (analysis.ProjectileCount == 0)
                return new DodgeDifficultyAttributes();

            double movementDifficulty = analysis.MeanMovementRatio * 1.5
                                        + analysis.PeakMovementRatio * 0.35;
            double pressureDifficulty = analysis.MeanPressure * 1.8
                                        + analysis.PeakPressure * 0.65
                                        + analysis.WeightedStrain * 0.45;
            // A large number of slow bullets may stay on screen concurrently
            // without requiring much player movement. Attack frequency separates
            // dense patterns more reliably, so scale it non-linearly while route
            // pressure continues to account for genuinely dangerous overlap.
            double densityDifficulty = Math.Pow(analysis.ProjectileRate / 6, 2.1) * 0.55;
            double impossiblePatternDifficulty = Math.Log2(1 + analysis.TeleportCount) * 1.25;
            double starRating = 0.1
                                + movementDifficulty * 0.6
                                + pressureDifficulty * 0.4
                                + densityDifficulty
                                + impossiblePatternDifficulty;

            return new DodgeDifficultyAttributes
            {
                StarRating = Math.Clamp(starRating, 0, 15),
                MovementDifficulty = movementDifficulty,
                PressureDifficulty = pressureDifficulty,
                ProjectileRate = analysis.ProjectileRate,
                PeakActiveProjectiles = analysis.PeakActiveProjectiles,
                AutoplayTeleports = analysis.TeleportCount,
            };
        }
    }
}
