// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Online.API;
using Realms;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// A mod preset is a named collection of configured mods.
    /// Presets are presented to the user in the mod select overlay for convenience.
    /// </summary>
    public class ModPreset : RealmObject, IHasGuidPrimaryKey, ISoftDelete
    {
        /// <summary>
        /// The internal database ID of the preset.
        /// </summary>
        [PrimaryKey]
        public Guid ID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The ruleset that the preset is valid for.
        /// </summary>
        public RulesetInfo Ruleset { get; set; } = null!;

        /// <summary>
        /// The name of the mod preset.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The description of the mod preset.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The set of configured mods that are part of the preset.
        /// </summary>
        [Ignored]
        public ICollection<Mod> Mods
        {
            get
            {
                if (string.IsNullOrEmpty(ModsJson))
                    return Array.Empty<Mod>();

                try
                {
                    var apiMods = JsonConvert.DeserializeObject<IEnumerable<APIMod?>>(ModsJson) ?? Array.Empty<APIMod>();
                    var ruleset = Ruleset.CreateInstance();
                    var mods = new List<Mod>();
                    bool encounteredError = false;

                    foreach (APIMod? apiMod in apiMods)
                    {
                        if (apiMod == null)
                        {
                            encounteredError = true;
                            logInvalidMods(new JsonSerializationException("The preset contains a null mod entry."));
                            continue;
                        }

                        try
                        {
                            mods.Add(apiMod.ToMod(ruleset));
                        }
                        catch (Exception ex)
                        {
                            encounteredError = true;
                            logInvalidMods(ex);
                        }
                    }

                    if (!encounteredError)
                        lastInvalidModsJson = null;

                    return mods;
                }
                catch (Exception ex)
                {
                    logInvalidMods(ex);
                    return Array.Empty<Mod>();
                }
            }
            set
            {
                var apiMods = value.Select(mod => new APIMod(mod)).ToArray();
                ModsJson = JsonConvert.SerializeObject(apiMods);
                lastInvalidModsJson = null;
            }
        }

        private string? lastInvalidModsJson;

        private void logInvalidMods(Exception exception)
        {
            if (lastInvalidModsJson == ModsJson)
                return;

            lastInvalidModsJson = ModsJson;
            Logger.Error(exception, $"Failed to read mods for preset '{Name}'. The invalid entries will be ignored.");
        }

        /// <summary>
        /// The set of configured mods that are part of the preset, serialised as a JSON blob.
        /// </summary>
        [MapTo("Mods")]
        public string ModsJson { get; set; } = string.Empty;

        /// <summary>
        /// Whether the preset has been soft-deleted by the user.
        /// </summary>
        public bool DeletePending { get; set; }
    }
}
