// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Online.Legacy
{
    /// <summary>
    /// Defines the subset of lazer mods which can be represented losslessly by the stable protocol.
    /// </summary>
    public static class StableModCompatibility
    {
        private static readonly HashSet<string> supported_acronyms = new HashSet<string>
        {
            "CL", "NF", "EZ", "TD", "HD", "HR", "SD", "RX", "DT", "HT", "NC", "FL", "SO", "PF",
            "FI", "MR", "1K", "2K", "3K", "4K", "5K", "6K", "7K", "8K", "9K",
        };

        /// <summary>
        /// Catch's Classic mod has no gameplay implementation and cannot be selected.
        /// Only the other built-in modes need Classic for stable-compatible gameplay.
        /// </summary>
        public static bool RequiresClassic(int rulesetId) => rulesetId is 0 or 1 or 3;

        public static bool IsSupported(Mod mod)
        {
            // Mirror has a stable bit in mania only. osu! Mirror (including one
            // restored by a preset) must not sneak through the shared acronym list.
            if (mod is ModMirror)
                return mod.GetType().FullName == "osu.Game.Rulesets.Mania.Mods.ManiaModMirror";

            return supported_acronyms.Contains(mod.Acronym);
        }
    }
}
