// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Low Accuracy Nerf (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): scores below 75% accuracy lose
    /// most of their PP. Realistik alone keeps a lot for aim-heavy maps (aim only scales with acc^1.5).
    /// </summary>
    /// <remarks>
    /// Accuracy is the osu! formula on the judged objects: (6·300 + 2·100 + 50) / (6·judged).
    /// ≥ 75%: ×1. 75% → 70%: smooth drop to ×0.25 (no cliff at 70%). Below 70%: ×0.25·((acc − 55%) / 15%)³, ×0 at ≤ 55%.
    /// Examples: 72% ×0.51, 70% ×0.25, 67% ×0.13, 65% ×0.07, 60% ×0.01.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxAccuracyNerf
    {
        public const double NO_NERF_FROM = 0.75;
        public const double SOFT_LIMIT = 0.70;
        public const double MULTIPLIER_AT_SOFT_LIMIT = 0.25;
        public const double ZERO_AT = 0.55;

        public static double Multiplier(double accuracy)
        {
            if (double.IsNaN(accuracy) || accuracy >= NO_NERF_FROM)
                return 1;

            if (accuracy >= SOFT_LIMIT)
            {
                double t = (accuracy - SOFT_LIMIT) / (NO_NERF_FROM - SOFT_LIMIT);
                return MULTIPLIER_AT_SOFT_LIMIT + (1 - MULTIPLIER_AT_SOFT_LIMIT) * t * t * (3 - 2 * t);
            }

            double below = Math.Max(accuracy - ZERO_AT, 0) / (SOFT_LIMIT - ZERO_AT);
            return MULTIPLIER_AT_SOFT_LIMIT * below * below * below;
        }

        public static double Multiplier(RxScoreState score)
        {
            double judged = (double)score.Count300 + score.Count100 + score.Count50 + score.Misses;

            if (judged <= 0)
                return 1;

            return Multiplier((6.0 * score.Count300 + 2.0 * score.Count100 + score.Count50) / (6.0 * judged));
        }
    }
}
