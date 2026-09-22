// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Dodge.Difficulty
{
    public class DodgePerformanceAttributes : PerformanceAttributes
    {
        [JsonProperty("movement")]
        public double Movement { get; set; }

        [JsonProperty("path")]
        public double Path { get; set; }

        [JsonProperty("reading")]
        public double Reading { get; set; }

        [JsonProperty("effective_accuracy")]
        public double EffectiveAccuracy { get; set; }

        [JsonProperty("effective_miss_count")]
        public int EffectiveMissCount { get; set; }

        [JsonProperty("endurance_bonus")]
        public double EnduranceBonus { get; set; }

        [JsonProperty("length_factor")]
        public double LengthFactor { get; set; }

        public override IEnumerable<PerformanceDisplayAttribute> GetAttributesForDisplay()
        {
            foreach (PerformanceDisplayAttribute attribute in base.GetAttributesForDisplay())
                yield return attribute;

            yield return new PerformanceDisplayAttribute(nameof(Movement), "Movement", Movement);
            yield return new PerformanceDisplayAttribute(nameof(Path), "Path", Path);
            yield return new PerformanceDisplayAttribute(nameof(Reading), "Reading", Reading);
        }
    }
}
