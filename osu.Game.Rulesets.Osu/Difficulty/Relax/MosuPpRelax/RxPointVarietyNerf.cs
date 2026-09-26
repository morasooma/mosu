// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Point Variety Nerf (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): maps that only jump between
    /// 2–4 fixed spots (e.g. corner-to-corner back-and-forth spam) lose most of their aim and speed PP.
    /// </summary>
    /// <remarks>
    /// Positions are snapped to a 24 px grid. For every window of 32 objects (step 16) the number of distinct grid cells
    /// is counted and averaged over the map (maps shorter than 32 objects: the whole map is one window).
    /// Multiplier on aim and speed PP: 1 − 0.85 · (1 − smoothstep(averageDistinct, 3, 12)).
    /// Normal maps, including long wide-jump maps, have 20–30 distinct spots per window and are not touched.
    /// Examples (SS, RX): Kami no Kotoba "small CS" / "TURBO" (2 spots) ×0.20 / ×0.23 of total PP;
    /// Sky of Twilight, Novae Ruptis, Attack, mcr, Harumachi Clover ±0%.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxPointVarietyNerf
    {
        public const double GRID_SIZE = 24;
        public const int WINDOW_SIZE = 32;
        public const int WINDOW_STEP = 16;
        public const double MAX_NERF = 0.85;
        public const double FULL_NERF_AT = 3;
        public const double NO_NERF_FROM = 12;

        public static double Multiplier(RxBeatmap beatmap)
            => 1 - MAX_NERF * (1 - smoothStep(AverageDistinctPositions(beatmap), FULL_NERF_AT, NO_NERF_FROM));

        public static void Apply(RxNativePerformanceResult result, RxBeatmap beatmap)
        {
            double multiplier = Multiplier(beatmap);
            result.PpAim *= multiplier;
            result.PpSpeed *= multiplier;
        }

        public static double AverageDistinctPositions(RxBeatmap beatmap)
        {
            List<RxHitObject> objects = beatmap.HitObjects;
            if (objects.Count == 0)
                return NO_NERF_FROM;

            var cells = new (int X, int Y)[objects.Count];
            for (int i = 0; i < objects.Count; i++)
                cells[i] = ((int)(objects[i].Position.X / GRID_SIZE), (int)(objects[i].Position.Y / GRID_SIZE));

            if (cells.Length <= WINDOW_SIZE)
                return new HashSet<(int, int)>(cells).Count;

            var window = new HashSet<(int, int)>();
            double sum = 0;
            int windows = 0;

            for (int start = 0; start + WINDOW_SIZE <= cells.Length; start += WINDOW_STEP)
            {
                window.Clear();
                for (int i = start; i < start + WINDOW_SIZE; i++)
                    window.Add(cells[i]);

                sum += window.Count;
                windows++;
            }

            return sum / windows;
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
