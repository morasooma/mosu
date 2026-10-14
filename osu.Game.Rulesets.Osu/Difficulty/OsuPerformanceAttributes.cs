// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceAttributes : PerformanceAttributes
    {
        [JsonProperty("aim")]
        public double Aim { get; set; }

        [JsonProperty("speed")]
        public double Speed { get; set; }

        [JsonProperty("accuracy")]
        public double Accuracy { get; set; }

        [JsonProperty("flashlight")]
        public double Flashlight { get; set; }

        [JsonProperty("reading")]
        public double Reading { get; set; }

        [JsonProperty("effective_miss_count")]
        public double EffectiveMissCount { get; set; }

        [JsonProperty("speed_deviation")]
        public double? SpeedDeviation { get; set; }

        [JsonProperty("combo_based_estimated_miss_count")]
        public double ComboBasedEstimatedMissCount { get; set; }

        [JsonProperty("score_based_estimated_miss_count")]
        public double? ScoreBasedEstimatedMissCount { get; set; }

        [JsonProperty("aim_estimated_slider_breaks")]
        public double AimEstimatedSliderBreaks { get; set; }

        [JsonProperty("speed_estimated_slider_breaks")]
        public double SpeedEstimatedSliderBreaks { get; set; }

        /// <summary>MosuPp "RP Balance (RX)": respect points of the map + mods (diagnostics, not serialised). Null outside MosuPp.</summary>
        [JsonIgnore]
        public double? MosuPpRespectPoints { get; set; }

        /// <summary>MosuPp "RP Balance (RX)": multiplier applied to this score's PP (diagnostics, not serialised). Null outside MosuPp.</summary>
        [JsonIgnore]
        public double? MosuPpRespectPointsMultiplier { get; set; }

        /// <summary>MosuPp "Aim Control (RX)": control PP of the map + mods (diagnostics, not serialised). Null outside MosuPp.</summary>
        [JsonIgnore]
        public double? MosuPpControlPp { get; set; }

        /// <summary>MosuPp "Aim Control (RX)": multiplier applied to this score's PP (diagnostics, not serialised). Null outside MosuPp.</summary>
        [JsonIgnore]
        public double? MosuPpControlMultiplier { get; set; }

        /// <summary>MosuPp Arles content confidence (0–1), shared with the lab. Null outside MosuPp.</summary>
        [JsonIgnore]
        public double? MosuPpArlesConfidence { get; set; }

        /// <summary>MosuPp Arles multiplier (1–1.3), independent of score accuracy and misses.</summary>
        [JsonIgnore]
        public double? MosuPpArlesMultiplier { get; set; }

        /// <summary>MosuPp: Realistik "spike + filler" weight used by this score (diagnostics, not serialised). Null outside MosuPp.</summary>
        [JsonIgnore]
        public double? MosuPpSpikeFillerWeight { get; set; }

        public override IEnumerable<PerformanceDisplayAttribute> GetAttributesForDisplay()
        {
            foreach (var attribute in base.GetAttributesForDisplay())
                yield return attribute;

            yield return new PerformanceDisplayAttribute(nameof(Aim), "Aim", Aim);
            yield return new PerformanceDisplayAttribute(nameof(Speed), "Speed", Speed);
            yield return new PerformanceDisplayAttribute(nameof(Accuracy), "Accuracy", Accuracy);
            yield return new PerformanceDisplayAttribute(nameof(Flashlight), "Flashlight Bonus", Flashlight);
            yield return new PerformanceDisplayAttribute(nameof(Reading), "Reading", Reading);
        }
    }
}
