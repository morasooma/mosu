// Adapted from taziksfear’s MosuPP 2.0.0 (PR #7, 3d3a196ed6540a3099d246dd5c6f662366c28db8).
using System;
using TazikPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPpRelax
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
