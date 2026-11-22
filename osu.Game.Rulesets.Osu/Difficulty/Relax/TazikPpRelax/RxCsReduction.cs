// Adapted from taziksfear’s MosuPp 1.0.0 (PR #5, 60690a2, cache 40).

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPpRelax
{
    /// <summary>
    /// TazikPp rule "CS Reduction (RX)" (only for <c>ForkRelaxPpSystem.TazikPp</c>): playing a map with bigger circles than it has
    /// (DA with a lower CS, EZ) costs PP — total × (native radius / played radius)^k, k = <see cref="TazikPpBalance.CsReductionStrength"/>.
    /// </summary>
    /// <remarks>
    /// Only a reduction relative to the map's own CS counts: maps whose own CS is low are not touched, raising CS (HR, DA up) keeps
    /// the existing CS PP Buff. Applied last, after RP Balance, so neither the RP ±20% clamp nor Aim Control can absorb it.
    /// Radius (osu!px) = 54.4 − 4.48 · CS. k = 1.6 (owner: "CS 4 → 2 must take a lot"): CS 4 → 2 × 0.703 on top of the
    /// existing CS effects, about −45% in total for jump and control maps; CS 5 → 2 about −64%. EZ (CS × 0.5) counts the same.
    /// When changing it, bump <c>ForkDataStore.RELAX_TAZIK_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxCsReduction
    {
        public const double DEFAULT_STRENGTH = 1.6;

        public static double Multiplier(double nativeCs, double playedCs, double strength)
        {
            if (strength <= 0 || playedCs >= nativeCs)
                return 1;

            double ratio = radius(nativeCs) / radius(playedCs);
            return Math.Pow(Math.Clamp(ratio, 0, 1), strength);
        }

        private static double radius(double cs) => Math.Max(1, 54.4 - 4.48 * cs);
    }
}
