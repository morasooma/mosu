// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Dodge.Replays
{
    /// <summary>
    /// Measurements collected from the route selected by Dodge autoplay.
    /// </summary>
    public readonly record struct DodgeAutoplayAnalysis(
        int ProjectileCount,
        double Duration,
        double MeanMovementRatio,
        double PeakMovementRatio,
        double MeanPressure,
        double PeakPressure,
        int PeakActiveProjectiles,
        double WeightedStrain,
        int TeleportCount)
    {
        public double ProjectileRate => ProjectileCount / System.Math.Max(1, Duration / 1000);
    }
}
