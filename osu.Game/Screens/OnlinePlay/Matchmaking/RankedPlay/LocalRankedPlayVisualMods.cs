using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay
{
    /// <summary>
    /// Local rendering preference, deliberately absent from matchmaking and score contracts.
    /// A single default-configured visibility mod is locked in when queueing starts.
    /// </summary>
    public sealed class LocalRankedPlayVisualMods
    {
        public string Acronym { get; }

        public LocalRankedPlayVisualMods(string acronym = "None") => Acronym = acronym;

        public static IEnumerable<string> Available(Ruleset ruleset) => new[] { "None" }.Concat(
            ruleset.CreateAllMods().Where(isAllowed).Select(m => m.Acronym).Distinct());

        public Mod[] Resolve(Ruleset ruleset, IReadOnlyList<Mod> requiredMods)
        {
            var mod = ruleset.CreateAllMods().FirstOrDefault(m => isAllowed(m) && m.Acronym == Acronym);
            if (mod == null || requiredMods.Any(m => m.Acronym == Acronym)
                            || !ModUtils.CheckCompatibleSet(requiredMods.Append(mod)))
                return Array.Empty<Mod>();
            return new[] { mod };
        }

        private static bool isAllowed(Mod mod) => mod.Acronym is "HD" or "TC" or "CO" or "FI";
    }
}
