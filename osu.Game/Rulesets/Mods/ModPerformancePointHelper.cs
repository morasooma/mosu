// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Online;

namespace osu.Game.Rulesets.Mods
{
    public static class ModPerformancePointHelper
    {
        private static readonly HashSet<string> unrankedModAcronyms = new HashSet<string>
        {
            "AP",
            "RD",
            "WU",
            "WD",
            "MG",
            "AS",
            "MRX",
            "FP",
        };

        public static bool ModsAwardPerformancePoints(IBeatmapInfo? beatmapInfo, IReadOnlyCollection<Mod> mods)
            => mods.All(mod => ModAwardsPerformancePoints(beatmapInfo, mods, mod));

        public static bool ModAwardsPerformancePoints(IBeatmapInfo? beatmapInfo, IReadOnlyCollection<Mod> mods, Mod mod)
        {
            if (!mod.UserPlayable || !mod.HasImplementation)
                return false;

            // Difficulty Adjust has fork-specific ranked ranges and is intentionally
            // evaluated separately from the mod's generic Ranked flag.
            if (mod.Acronym == "DA")
                return isDifficultyAdjustRanked(beatmapInfo, mods);

            if (!mod.Ranked || unrankedModAcronyms.Contains(mod.Acronym))
                return false;

            return true;
        }

        private static bool isDifficultyAdjustRanked(IBeatmapInfo? beatmapInfo, IReadOnlyCollection<Mod> mods)
        {
            if (beatmapInfo == null)
                return false;

            var ruleset = beatmapInfo.Ruleset.CreateInstance();

            if (ruleset == null)
                return false;

            BeatmapDifficulty adjustedDifficulty = ruleset.GetAdjustedDisplayDifficulty(beatmapInfo, mods);

            // Circle size is only a gameplay difficulty setting in osu!, but CS 0 is a
            // valid value for Difficulty Adjust. Do not use CS > 0 as an applicability
            // check here, as that allowed exactly CS 0 to bypass the ranked range.
            if (ruleset.RulesetInfo.OnlineID == 0
                && (adjustedDifficulty.CircleSize < 2 || adjustedDifficulty.CircleSize > 10))
                return false;

            return adjustedDifficulty.OverallDifficulty >= 4;
        }
    }
}
