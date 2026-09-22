// This file is originally created by GooGuTeam.

using System.Collections.Generic;
using System.IO;
using osu.Framework.Extensions;

namespace osu.Game.Rulesets
{
    public class RulesetHashCache
    {
        public readonly Dictionary<string, string> RulesetsHashes = new Dictionary<string, string>();

        public RulesetHashCache(RulesetStore store)
        {
            foreach (var rulesetInfo in store.AvailableRulesets)
            {
                if (rulesetInfo.OnlineID >= 0 && rulesetInfo.OnlineID <= 3)
                    continue;

                Ruleset instance = rulesetInfo.CreateInstance();
                string assemblyLocation = instance.GetType().Assembly.Location;

                // Bundled assemblies do not have a readable on-disk location on Android.
                // Omitting the hash is preferable to preventing the game from starting;
                // callers already treat missing ruleset hashes as optional.
                if (string.IsNullOrEmpty(assemblyLocation) || !File.Exists(assemblyLocation))
                    continue;

                using var str = File.OpenRead(assemblyLocation);
                RulesetsHashes[instance.ShortName] = str.ComputeMD5Hash();
            }
        }

        public string? GetHash(string shortName)
        {
            RulesetsHashes.TryGetValue(shortName, out string? hash);
            return hash;
        }

        public string? GetHash(RulesetInfo rulesetInfo) => GetHash(rulesetInfo.ShortName);

        public string? GetHash(Ruleset ruleset) => GetHash(ruleset.ShortName);
    }
}
