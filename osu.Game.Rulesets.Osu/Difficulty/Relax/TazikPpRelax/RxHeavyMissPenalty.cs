// Adapted from taziksfear’s MosuPP 2.0.0 (PR #7, 3d3a196ed6540a3099d246dd5c6f662366c28db8).

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPpRelax
{
    /// <summary>
    /// TazikPp rule "Heavy Miss Penalty (RX)" (only for <c>ForkRelaxPpSystem.TazikPp</c>): scores with more than 15 misses
    /// lose PP much faster than with the base Realistik miss penalty alone.
    /// </summary>
    /// <remarks>
    /// Total × e^(−0.11 · max(0, missLoad − 15)), on top of the base Realistik miss penalty (which is unchanged). The miss
    /// load is the load of the missed notes (Σ min(1, (moment / level)^MissExponent)): a miss on a hard moment (load ~ level)
    /// costs a full miss, on a weak spot only its share, so easy-spot misses are shielded — the penalty is smooth in the
    /// miss load, no steps. Tuned on Sky of Twilight (/b/5208003), share of the SS PP kept: 15 misses 41% (unchanged), 20 →
    /// 20%, 25 → 10%, 30 → 5%, 40 → 1%. When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxHeavyMissPenalty
    {
        public const double FREE_MISSES = 15;
        public const double DECAY_PER_MISS = 0.11;

        public static double Multiplier(uint misses, double? missLoad = null)
            => Math.Exp(-DECAY_PER_MISS * Math.Max(0, (missLoad ?? (double)misses) - FREE_MISSES));
    }
}
