// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>Test balance: no extra penalty through 90 seconds; one third of the maximum at two minutes, full at ten.</summary>
    internal static class RxLongMapPenalty
    {
        public static double Multiplier(double playedSeconds, double maximumPenalty)
        {
            if (!double.IsFinite(playedSeconds)) throw new ArgumentOutOfRangeException(nameof(playedSeconds));
            if (!double.IsFinite(maximumPenalty) || maximumPenalty < 0 || maximumPenalty > 0.5)
                throw new ArgumentOutOfRangeException(nameof(maximumPenalty));
            double entry = RxMath.SmoothStep(playedSeconds, 90, 120);
            double longPart = RxMath.SmoothStep(playedSeconds, 120, 600);
            return 1 - maximumPenalty * (entry + 2 * longPart) / 3;
        }
    }
}
