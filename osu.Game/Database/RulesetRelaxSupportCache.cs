using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Database
{
    /// <summary>
    /// Caches ruleset capabilities for one PP discovery pass, not for the lifetime of the client.
    /// Does not retain Realm objects, ruleset instances or mod lists.
    /// </summary>
    internal sealed class RulesetRelaxSupportCache
    {
        private readonly Dictionary<string, bool> supportsRelax = new Dictionary<string, bool>(StringComparer.Ordinal);

        public bool SupportsRelax(RulesetInfo info)
        {
            string shortName = info.ShortName;
            if (supportsRelax.TryGetValue(shortName, out bool supported))
                return supported;

            // Only cache successful discovery. Unavailable/custom rulesets still follow
            // the caller's error handling and may be retried on a later map or pass.
            supported = info.CreateInstance().GetModsFor(ModType.Automation).OfType<ModRelax>().Any();
            supportsRelax.Add(shortName, supported);
            return supported;
        }
    }
}
