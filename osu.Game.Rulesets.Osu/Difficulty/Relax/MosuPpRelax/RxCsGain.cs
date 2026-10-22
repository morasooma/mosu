// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "CS Gain (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): smaller circles pay only on maps that are hard by
    /// themselves. Two maps with the same star rating — easy jumps at CS 6.2 and hard jumps at CS 4 — are not equally
    /// hard, and DA CS 8 turns the first into a farm map and the second into an impossible one.
    /// </summary>
    /// <remarks>
    /// Reference CS = the map's own CS, at most <see cref="REFERENCE_MAX_CS"/> (normal maps up to CS 5 keep their CS as it is).
    /// Gain = final SS PP at the played CS / final SS PP at the reference CS, same mods, everything included (distances in circle
    /// radii, small circle bonus, CS PP Buff, Aim Control, RP, Arles, type scale). How hard the map is by itself = its final SS PP
    /// at the reference CS ("the star rating before the change", in PP). The map keeps gain^w, w = smoothstep(reference SS PP,
    /// <see cref="PP_START"/>, <see cref="PP_FULL"/>): easy maps keep their PP at the reference CS, hard maps the whole gain.
    /// One multiplier per map + mods (from an SS, like RP Balance). Lower CS is <see cref="RxCsReduction"/>.
    /// RP and Arles at the reference CS come from the same playable beatmap with the circle diameter of that CS.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxCsGain
    {
        public const double REFERENCE_MAX_CS = 5.0;
        public const double PP_START = 800;
        public const double PP_FULL = 2000;

        public static float ReferenceCs(float nativeCs) => Math.Min(nativeCs, (float)REFERENCE_MAX_CS);

        /// <summary>Circle diameter in osu! pixels at <paramref name="circleSize"/> (as OsuHitObject.Radius).</summary>
        public static double Diameter(float circleSize)
            => 2 * OsuHitObject.OBJECT_RADIUS * LegacyRulesetExtensions.CalculateScaleFromCircleSize(circleSize, true);

        public static double Weight(double referencePp) => smoothStep(referencePp, PP_START, PP_FULL);

        /// <param name="gain">Final SS PP at the played CS / final SS PP at the reference CS.</param>
        /// <param name="referencePp">Final SS PP at the reference CS.</param>
        public static double Multiplier(double gain, double referencePp)
            => gain <= 1 || double.IsNaN(gain) ? 1 : Math.Pow(gain, Weight(referencePp) - 1);

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
