// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Short Aim Nerf (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): short aim-only maps ("aim slop")
    /// lose up to 10% of the total PP. Long aim maps and short stream / mixed maps are not touched.
    /// </summary>
    /// <remarks>
    /// short = 1 − smoothstep(object count, 300, 700) (object count, so NM and DT are nerfed the same);
    /// aim focus = 1 − smoothstep(speed PP / aim PP of the SS reference, 0.5, 0.8).
    /// Multiplier on the total: 1 − 0.10 · short · aim focus.
    /// Examples (SS, RX): bbydoll, Harumachi Clover, drivers license [aim], Spider-Man, Kami no Kotoba −10%;
    /// Attack (500 objects) −5%; Crazy banger (586) −2%; Novae Ruptis, Sky of Twilight, long maps and stream maps ±0%.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxShortAimNerf
    {
        public const double MAX_NERF = 0.10;
        public const double SHORT_FULL_BELOW_OBJECTS = 300;
        public const double NOT_SHORT_FROM_OBJECTS = 700;
        public const double AIM_FOCUSED_BELOW = 0.5;
        public const double NOT_AIM_FOCUSED_FROM = 0.8;

        // speedAimRatio: speed PP / aim PP of the SS reference (see MosuPpRelaxCalculator).
        public static double Multiplier(RxBeatmap beatmap, double speedAimRatio)
        {
            double shortWeight = 1 - smoothStep(beatmap.HitObjects.Count, SHORT_FULL_BELOW_OBJECTS, NOT_SHORT_FROM_OBJECTS);
            double aimFocus = 1 - smoothStep(speedAimRatio, AIM_FOCUSED_BELOW, NOT_AIM_FOCUSED_FROM);
            return 1 - MAX_NERF * shortWeight * aimFocus;
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
