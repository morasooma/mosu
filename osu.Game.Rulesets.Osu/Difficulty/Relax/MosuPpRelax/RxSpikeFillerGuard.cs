// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Spike Filler Guard (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): the Realistik "spike + filler" anti-farm
    /// (× (1 − 0.12 · JumpSpikeFillerWeight); Realistik −8%, MosuPp 0.7.0 −14%, 0.9.0 −12%) does not apply to maps with several hard moments — they are not one spike in filler.
    /// </summary>
    /// <remarks>
    /// The Realistik check compares the 3 hardest aim sections with the 60th percentile and fires on almost every long map (about two
    /// thirds of the test list got the full −8%), e.g. katagiri – Sendan Life, whose calm repeated 1-2 parts lower the percentile.
    /// A hard moment (see <see cref="RxRespectPoints"/>) is a run of ≥ 8 map seconds at ≥ 60% of the hardest 20 s (gaps ≤ 3 s merged).
    /// Anti-farm weight × (1 − smoothstep(hard moments, 1.5, 2.5)): one hard moment keeps the anti-farm (Attack, ganbare!kumokosan,
    /// Hills Have Eyes, DK – Zaplatit'), two halve it (glass beach), three and more remove it (Sendan Life: 4).
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxSpikeFillerGuard
    {
        public const double MOMENTS_START = 1.5;
        public const double MOMENTS_FULL = 2.5;

        /// <summary>Anti-farm aim penalty of the Realistik pattern multiplier at full spike weight.</summary>
        public const double ANTI_FARM_AIM_PENALTY = 0.12; // 0.9.0: 0.14 in 0.7.0–0.8.0, 0.08 in Realistik

        /// <summary>
        /// Lifts the anti-farm part (1 − 0.12 · weight) out of <paramref name="patternMultiplier"/> by the share of the guard.
        /// </summary>
        public static double Apply(double patternMultiplier, double spikeFillerWeight, int hardMoments)
        {
            double weight = Math.Clamp(spikeFillerWeight, 0, 1);
            double guardedWeight = weight * (1 - smoothStep(hardMoments, MOMENTS_START, MOMENTS_FULL));
            double lifted = (1 - ANTI_FARM_AIM_PENALTY * guardedWeight) / (1 - ANTI_FARM_AIM_PENALTY * weight);
            return Math.Min(1, patternMultiplier * lifted);
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
