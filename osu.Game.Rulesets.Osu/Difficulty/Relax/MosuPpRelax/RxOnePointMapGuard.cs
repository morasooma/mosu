// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "One-Point Map Guard (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): maps where (almost) every note
    /// is stacked on the same spot as the previous one and the whole map uses 1–2 spots (e.g. hehehe - Iyul' [HS]: all
    /// 1150 notes on the centre) give practically no PP with any mods (accuracy and HD reading included).
    /// </summary>
    /// <remarks>
    /// stacked share = notes closer than 5 px to the previous note / all notes;
    /// spots = <see cref="RxPointVarietyNerf.AverageDistinctPositions"/> (distinct 24 px cells per 32 notes).
    /// Multiplier on the total: 1 − 0.97 · smoothstep(stacked share, 60%, 90%) · (1 − smoothstep(spots, 2, 4)).
    /// Both are required, so full maps whose streams are stacked into points (still 5+ spots per 32 notes) and
    /// back-and-forth jump maps (nothing stacked) are not touched by this rule.
    /// Examples (SS, RX): Iyul' [HS] NM 134 → 4, HD+DT 264 → 8; every other tested map ±0%.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxOnePointMapGuard
    {
        public const double STACK_DISTANCE = 5;
        public const double STACKED_SHARE_START = 0.60;
        public const double STACKED_SHARE_FULL = 0.90;
        public const double SPOTS_FULL_BELOW = 2;
        public const double SPOTS_NONE_FROM = 4;
        public const double MAX_NERF = 0.97;

        public static double Multiplier(RxBeatmap beatmap)
        {
            double stacked = StackedShare(beatmap);
            if (stacked <= STACKED_SHARE_START)
                return 1;

            double spots = RxPointVarietyNerf.AverageDistinctPositions(beatmap);
            return 1 - MAX_NERF
                   * smoothStep(stacked, STACKED_SHARE_START, STACKED_SHARE_FULL)
                   * (1 - smoothStep(spots, SPOTS_FULL_BELOW, SPOTS_NONE_FROM));
        }

        public static double StackedShare(RxBeatmap beatmap)
        {
            var objects = beatmap.HitObjects;
            if (objects.Count < 2)
                return 0;

            int stacked = 0;
            for (int i = 1; i < objects.Count; i++)
            {
                double dx = objects[i].Position.X - objects[i - 1].Position.X;
                double dy = objects[i].Position.Y - objects[i - 1].Position.Y;
                if (dx * dx + dy * dy < STACK_DISTANCE * STACK_DISTANCE)
                    stacked++;
            }

            return (double)stacked / (objects.Count - 1);
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
