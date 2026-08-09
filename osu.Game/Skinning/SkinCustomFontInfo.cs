// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Graphics;
using osu.Game.Rulesets;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Mosu-specific skin extension storing per-component custom font selections.
    /// Stored separately from layout JSON files for compatibility with vanilla osu!lazer.
    /// </summary>
    [Serializable]
    public class SkinCustomFontInfo
    {
        public const string FILENAME = "mosu-skin-fonts.json";

        public const int LATEST_VERSION = 1;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Version { get; set; } = LATEST_VERSION;

        public List<SkinCustomFontOverride> Overrides { get; set; } = new List<SkinCustomFontOverride>();

        public void SetOverridesForTarget(GlobalSkinnableContainers container, RulesetInfo? ruleset, IEnumerable<SkinCustomFontOverride> overrides)
        {
            string containerName = container.ToString();
            string rulesetName = getRulesetName(ruleset);

            Overrides.RemoveAll(o => o.Container == containerName && o.Ruleset == rulesetName);
            Overrides.AddRange(overrides);
        }

        public SkinCustomFontOverride? FindOverride(GlobalSkinnableContainers container, RulesetInfo? ruleset, IReadOnlyList<int> path)
        {
            string containerName = container.ToString();
            string rulesetName = getRulesetName(ruleset);

            return Overrides.FirstOrDefault(o =>
                o.Container == containerName
                && o.Ruleset == rulesetName
                && o.Path.SequenceEqual(path));
        }

        private static string getRulesetName(RulesetInfo? ruleset) => ruleset?.ShortName ?? @"global";
    }

    [Serializable]
    public class SkinCustomFontOverride
    {
        public string Container { get; set; } = string.Empty;

        public string Ruleset { get; set; } = @"global";

        public int[] Path { get; set; } = Array.Empty<int>();

        public string FontFamily { get; set; } = string.Empty;

        public FontWeight TextWeight { get; set; } = FontWeight.Regular;
    }
}