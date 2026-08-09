// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Rulesets
{
    public partial class RulesetConfigCache : Component, IRulesetConfigCache
    {
        private readonly RealmAccess realm;
        private readonly RulesetStore rulesets;

        private readonly Dictionary<string, IRulesetConfigManager?> configCache = new Dictionary<string, IRulesetConfigManager?>();

        public RulesetConfigCache(RealmAccess realm, RulesetStore rulesets)
        {
            this.realm = realm;
            this.rulesets = rulesets;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var settingsStore = new SettingsStore(realm);

            // let's keep things simple for now and just retrieve all the required configs at startup..
            foreach (var ruleset in rulesets.AvailableRulesets)
            {
                if (string.IsNullOrEmpty(ruleset.ShortName))
                    continue;

                var instance = ruleset.CreateInstance();
                var config = instance.CreateConfig(settingsStore);

                cacheConfig(ruleset.ShortName, config);
                cacheConfig(instance.ShortName, config);

                foreach (string alias in getSpecialShortNames(ruleset))
                    cacheConfig(alias, config);
            }
        }

        public IRulesetConfigManager? GetConfigFor(Ruleset ruleset)
        {
            if (!IsLoaded)
                throw new InvalidOperationException($@"Cannot retrieve {nameof(IRulesetConfigManager)} before {nameof(RulesetConfigCache)} has loaded");

            foreach (string shortName in getCandidateShortNames(ruleset))
            {
                if (configCache.TryGetValue(shortName, out var existingConfig))
                    return existingConfig;
            }

            throw new InvalidOperationException($@"Attempted to retrieve {nameof(IRulesetConfigManager)} for an unavailable ruleset {ruleset.GetDisplayString()}");
        }

        private void cacheConfig(string shortName, IRulesetConfigManager? config)
        {
            if (string.IsNullOrEmpty(shortName))
                return;

            configCache[shortName] = config;
        }

        private IEnumerable<string> getCandidateShortNames(Ruleset ruleset)
        {
            if (!string.IsNullOrEmpty(ruleset.RulesetInfo.ShortName))
                yield return ruleset.RulesetInfo.ShortName;

            if (!string.IsNullOrEmpty(ruleset.ShortName) && ruleset.ShortName != ruleset.RulesetInfo.ShortName)
                yield return ruleset.ShortName;

            if (ruleset.RulesetInfo.IsSpecialRuleset())
            {
                string normalShortName = ruleset.RulesetInfo.CreateNormalRuleset().ShortName;

                if (!string.IsNullOrEmpty(normalShortName) && normalShortName != ruleset.RulesetInfo.ShortName && normalShortName != ruleset.ShortName)
                    yield return normalShortName;
            }
        }

        private IEnumerable<string> getSpecialShortNames(RulesetInfo ruleset)
        {
            if (!ruleset.HasSpecialRuleset())
                yield break;

            switch (ruleset.ShortName)
            {
                case RulesetInfo.OSU_MODE_SHORTNAME:
                    yield return RulesetInfo.OSU_RELAX_MODE_SHORTNAME;
                    yield return RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME;
                    break;

                case RulesetInfo.TAIKO_MODE_SHORTNAME:
                    yield return RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME;
                    break;

                case RulesetInfo.CATCH_MODE_SHORTNAME:
                    yield return RulesetInfo.CATCH_RELAX_MODE_SHORTNAME;
                    break;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // ensures any potential database operations are finalised before game destruction.
            foreach (var c in configCache.Values)
                c?.Dispose();
        }
    }
}
