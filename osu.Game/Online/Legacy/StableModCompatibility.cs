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
        };

        public static bool IsSupported(Mod mod) => supported_acronyms.Contains(mod.Acronym);
    }
}
