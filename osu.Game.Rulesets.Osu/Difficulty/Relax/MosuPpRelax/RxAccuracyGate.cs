// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Accuracy Gate (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): accuracy PP is paid by how hard the map is.
    /// </summary>
    /// <remarks>
    /// Realistik accuracy PP = 1.52^OD · accuracy^24 · 2.83 · (circles / 1000)^0.3 (≤ 1.15) — it does not depend on the map's
    /// difficulty, so an easy OD 9 map pays as much accuracy PP as a hard one (about a third of the PP of easy maps, 5–9% of hard
    /// ones). In RX the notes are clicked by the game, accuracy is mostly being on the note, i.e. aim.
    /// Accuracy PP × (0.15 + 0.85 · smoothstep(skill PP, 200, 350)), skill PP = aim + speed PP of an SS on the map +
    /// mods after the MosuPp aim rules (one value per map + mods, the same for every accuracy).
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxAccuracyGate
    {
        /// <summary>Share of the accuracy PP an easy map keeps.</summary>
        public const double FLOOR = 0.15;

        /// <summary>Skill PP (aim + speed of an SS) from which accuracy PP starts to come back; the easy maps of the owner's list
        /// have 110–210.</summary>
        public const double SKILL_START = 200;

        /// <summary>Skill PP from which a map keeps all of its accuracy PP (mid tech / control maps have 330+).</summary>
        public const double SKILL_FULL = 350;

        public static double Multiplier(double skillPp) => FLOOR + (1 - FLOOR) * smoothStep(skillPp, SKILL_START, SKILL_FULL);

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
