// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyAttributes : DifficultyAttributes
    {
        private const int attrib_id_relax_flow_aim_bonus = 1001;
        private const int attrib_id_relax_jump_spike = 1002;
        private const int attrib_id_relax_wide_flow = 1003;
        private const int attrib_id_relax_flow_sections = 1004;
        private const int attrib_id_mosu_relax_aim = 1005;
        private const int attrib_id_mosu_relax_speed = 1006;
        private const int attrib_id_mosu_relax_reading = 1007;
        private const int attrib_id_mosu_relax_aim_strain_count = 1008;
        private const int attrib_id_mosu_relax_speed_strain_count = 1009;
        private const int attrib_id_mosu_relax_reading_note_count = 1010;
        private const int attrib_id_relax_stream_weight = 1011;
        private const int attrib_id_relax_vertical_aim_pressure = 1012;

        /// <summary>
        /// The difficulty corresponding to the aim skill.
        /// </summary>
        [JsonProperty("aim_difficulty")]
        public double AimDifficulty { get; set; }

        /// <summary>
        /// The number of <see cref="Slider"/>s weighted by difficulty.
        /// </summary>
        [JsonProperty("aim_difficult_slider_count")]
        public double AimDifficultSliderCount { get; set; }

        /// <summary>
        /// The difficulty corresponding to the speed skill.
        /// </summary>
        [JsonProperty("speed_difficulty")]
        public double SpeedDifficulty { get; set; }

        /// <summary>
        /// The number of clickable objects weighted by difficulty.
        /// Related to <see cref="SpeedDifficulty"/>
        /// </summary>
        [JsonProperty("speed_note_count")]
        public double SpeedNoteCount { get; set; }

        /// <summary>
        /// The difficulty corresponding to the flashlight skill.
        /// </summary>
        [JsonProperty("flashlight_difficulty")]
        public double FlashlightDifficulty { get; set; }

        /// <summary>
        /// The difficulty corresponding to the reading skill.
        /// </summary>
        [JsonProperty("reading_difficulty")]
        public double ReadingDifficulty { get; set; }

        /// <summary>
        /// Describes how much of <see cref="AimDifficulty"/> is contributed to by hitcircles or sliders.
        /// A value closer to 1.0 indicates most of <see cref="AimDifficulty"/> is contributed by hitcircles.
        /// A value closer to 0.0 indicates most of <see cref="AimDifficulty"/> is contributed by sliders.
        /// </summary>
        [JsonProperty("slider_factor")]
        public double SliderFactor { get; set; }

        /// <summary>
        /// Describes how much of <see cref="AimDifficultStrainCount"/> is contributed to by hitcircles or sliders
        /// A value closer to 0.0 indicates most of <see cref="AimDifficultStrainCount"/> is contributed by hitcircles
        /// A value closer to Infinity indicates most of <see cref="AimDifficultStrainCount"/> is contributed by sliders
        /// </summary>
        [JsonProperty("aim_top_weighted_slider_factor")]
        public double AimTopWeightedSliderFactor { get; set; }

        /// <summary>
        /// Describes how much of <see cref="SpeedDifficultStrainCount"/> is contributed to by hitcircles or sliders
        /// A value closer to 0.0 indicates most of <see cref="SpeedDifficultStrainCount"/> is contributed by hitcircles
        /// A value closer to Infinity indicates most of <see cref="SpeedDifficultStrainCount"/> is contributed by sliders
        /// </summary>
        [JsonProperty("speed_top_weighted_slider_factor")]
        public double SpeedTopWeightedSliderFactor { get; set; }

        [JsonProperty("aim_difficult_strain_count")]
        public double AimDifficultStrainCount { get; set; }

        [JsonProperty("speed_difficult_strain_count")]
        public double SpeedDifficultStrainCount { get; set; }

        [JsonProperty("reading_difficult_note_count")]
        public double ReadingDifficultNoteCount { get; set; }

        [JsonProperty("relax_flow_aim_bonus_ratio")]
        public double RelaxFlowAimBonusRatio { get; set; }

        [JsonProperty("relax_jump_spike_filler_weight")]
        public double RelaxJumpSpikeFillerWeight { get; set; }

        [JsonProperty("relax_wide_flow_pattern_weight")]
        public double RelaxWideFlowPatternWeight { get; set; }

        [JsonProperty("relax_flow_section_count")]
        public int RelaxFlowSectionCount { get; set; }

        [JsonProperty("relax_pattern_penalty_ratio")]
        public double RelaxPatternPenaltyRatio { get; set; } = 1;

        /// <summary>
        /// Confidence that the map's RX difficulty is stream-dominated. Used to gate
        /// the high-CS stream reward so jump maps do not receive it.
        /// </summary>
        [JsonProperty("relax_stream_weight")]
        public double RelaxStreamWeight { get; set; }

        /// <summary>
        /// Normalised pressure of repeated hard vertical circle jumps.
        /// </summary>
        [JsonProperty("relax_vertical_aim_pressure")]
        public double RelaxVerticalAimPressure { get; set; }

        /// <summary>
        /// Pinned Mosu/Realistik difficulty values used only by RX performance calculation.
        /// The normal attributes above remain the source of the displayed star rating.
        /// </summary>
        [JsonProperty("mosu_relax_aim_difficulty")]
        public double MosuRelaxAimDifficulty { get; set; }

        [JsonProperty("mosu_relax_speed_difficulty")]
        public double MosuRelaxSpeedDifficulty { get; set; }

        [JsonProperty("mosu_relax_reading_difficulty")]
        public double MosuRelaxReadingDifficulty { get; set; }

        [JsonProperty("mosu_relax_aim_difficult_strain_count")]
        public double MosuRelaxAimDifficultStrainCount { get; set; }

        [JsonProperty("mosu_relax_speed_difficult_strain_count")]
        public double MosuRelaxSpeedDifficultStrainCount { get; set; }

        [JsonProperty("mosu_relax_reading_difficult_note_count")]
        public double MosuRelaxReadingDifficultNoteCount { get; set; }

        [JsonProperty("nested_score_per_object")]
        public double NestedScorePerObject { get; set; }

        [JsonProperty("legacy_score_base_multiplier")]
        public double LegacyScoreBaseMultiplier { get; set; }

        [JsonProperty("maximum_legacy_combo_score")]
        public double MaximumLegacyComboScore { get; set; }

        /// <summary>
        /// The number of hitcircles in the beatmap.
        /// </summary>
        public int HitCircleCount { get; set; }

        /// <summary>
        /// The number of sliders in the beatmap.
        /// </summary>
        public int SliderCount { get; set; }

        /// <summary>
        /// The number of spinners in the beatmap.
        /// </summary>
        public int SpinnerCount { get; set; }

        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var v in base.ToDatabaseAttributes())
                yield return v;

            yield return (ATTRIB_ID_AIM, AimDifficulty);
            yield return (ATTRIB_ID_SPEED, SpeedDifficulty);
            yield return (ATTRIB_ID_READING, ReadingDifficulty);
            yield return (ATTRIB_ID_DIFFICULTY, StarRating);

            if (ShouldSerializeFlashlightDifficulty())
                yield return (ATTRIB_ID_FLASHLIGHT, FlashlightDifficulty);

            yield return (ATTRIB_ID_SLIDER_FACTOR, SliderFactor);

            yield return (ATTRIB_ID_AIM_DIFFICULT_STRAIN_COUNT, AimDifficultStrainCount);
            yield return (ATTRIB_ID_SPEED_DIFFICULT_STRAIN_COUNT, SpeedDifficultStrainCount);
            yield return (ATTRIB_ID_SPEED_NOTE_COUNT, SpeedNoteCount);
            yield return (ATTRIB_ID_AIM_DIFFICULT_SLIDER_COUNT, AimDifficultSliderCount);
            yield return (ATTRIB_ID_AIM_TOP_WEIGHTED_SLIDER_FACTOR, AimTopWeightedSliderFactor);
            yield return (ATTRIB_ID_SPEED_TOP_WEIGHTED_SLIDER_FACTOR, SpeedTopWeightedSliderFactor);
            yield return (ATTRIB_ID_NESTED_SCORE_PER_OBJECT, NestedScorePerObject);
            yield return (ATTRIB_ID_LEGACY_SCORE_BASE_MULTIPLIER, LegacyScoreBaseMultiplier);
            yield return (ATTRIB_ID_MAXIMUM_LEGACY_COMBO_SCORE, MaximumLegacyComboScore);
            yield return (ATTRIB_ID_READING_DIFFICULT_NOTE_COUNT, ReadingDifficultNoteCount);

            if (Mods.Any(m => m is OsuModRelax or OsuModMosuRelax))
            {
                yield return (attrib_id_relax_flow_aim_bonus, RelaxFlowAimBonusRatio);
                yield return (attrib_id_relax_jump_spike, RelaxJumpSpikeFillerWeight);
                yield return (attrib_id_relax_wide_flow, RelaxWideFlowPatternWeight);
                yield return (attrib_id_relax_flow_sections, RelaxFlowSectionCount);
                yield return (attrib_id_mosu_relax_aim, MosuRelaxAimDifficulty);
                yield return (attrib_id_mosu_relax_speed, MosuRelaxSpeedDifficulty);
                yield return (attrib_id_mosu_relax_reading, MosuRelaxReadingDifficulty);
                yield return (attrib_id_mosu_relax_aim_strain_count, MosuRelaxAimDifficultStrainCount);
                yield return (attrib_id_mosu_relax_speed_strain_count, MosuRelaxSpeedDifficultStrainCount);
                yield return (attrib_id_mosu_relax_reading_note_count, MosuRelaxReadingDifficultNoteCount);
                yield return (attrib_id_relax_stream_weight, RelaxStreamWeight);
                yield return (attrib_id_relax_vertical_aim_pressure, RelaxVerticalAimPressure);
            }
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            base.FromDatabaseAttributes(values, onlineInfo);

            AimDifficulty = values[ATTRIB_ID_AIM];
            SpeedDifficulty = values[ATTRIB_ID_SPEED];
            ReadingDifficulty = values[ATTRIB_ID_READING];
            StarRating = values[ATTRIB_ID_DIFFICULTY];
            FlashlightDifficulty = values.GetValueOrDefault(ATTRIB_ID_FLASHLIGHT);
            SliderFactor = values[ATTRIB_ID_SLIDER_FACTOR];
            AimDifficultStrainCount = values[ATTRIB_ID_AIM_DIFFICULT_STRAIN_COUNT];
            SpeedDifficultStrainCount = values[ATTRIB_ID_SPEED_DIFFICULT_STRAIN_COUNT];
            SpeedNoteCount = values[ATTRIB_ID_SPEED_NOTE_COUNT];
            AimDifficultSliderCount = values[ATTRIB_ID_AIM_DIFFICULT_SLIDER_COUNT];
            AimTopWeightedSliderFactor = values[ATTRIB_ID_AIM_TOP_WEIGHTED_SLIDER_FACTOR];
            SpeedTopWeightedSliderFactor = values[ATTRIB_ID_SPEED_TOP_WEIGHTED_SLIDER_FACTOR];
            NestedScorePerObject = values[ATTRIB_ID_NESTED_SCORE_PER_OBJECT];
            LegacyScoreBaseMultiplier = values[ATTRIB_ID_LEGACY_SCORE_BASE_MULTIPLIER];
            MaximumLegacyComboScore = values[ATTRIB_ID_MAXIMUM_LEGACY_COMBO_SCORE];
            ReadingDifficultNoteCount = values[ATTRIB_ID_READING_DIFFICULT_NOTE_COUNT];
            RelaxFlowAimBonusRatio = values.GetValueOrDefault(attrib_id_relax_flow_aim_bonus);
            RelaxJumpSpikeFillerWeight = values.GetValueOrDefault(attrib_id_relax_jump_spike);
            RelaxWideFlowPatternWeight = values.GetValueOrDefault(attrib_id_relax_wide_flow);
            RelaxFlowSectionCount = (int)values.GetValueOrDefault(attrib_id_relax_flow_sections);
            MosuRelaxAimDifficulty = values.GetValueOrDefault(attrib_id_mosu_relax_aim);
            MosuRelaxSpeedDifficulty = values.GetValueOrDefault(attrib_id_mosu_relax_speed);
            MosuRelaxReadingDifficulty = values.GetValueOrDefault(attrib_id_mosu_relax_reading);
            MosuRelaxAimDifficultStrainCount = values.GetValueOrDefault(attrib_id_mosu_relax_aim_strain_count);
            MosuRelaxSpeedDifficultStrainCount = values.GetValueOrDefault(attrib_id_mosu_relax_speed_strain_count);
            MosuRelaxReadingDifficultNoteCount = values.GetValueOrDefault(attrib_id_mosu_relax_reading_note_count);
            RelaxStreamWeight = values.GetValueOrDefault(attrib_id_relax_stream_weight);
            RelaxVerticalAimPressure = values.GetValueOrDefault(attrib_id_relax_vertical_aim_pressure);
            HitCircleCount = onlineInfo.CircleCount;
            SliderCount = onlineInfo.SliderCount;
            SpinnerCount = onlineInfo.SpinnerCount;
        }

        #region Newtonsoft.Json implicit ShouldSerialize() methods

        // The properties in this region are used implicitly by Newtonsoft.Json to not serialise certain fields in some cases.
        // They rely on being named exactly the same as the corresponding fields (casing included) and as such should NOT be renamed
        // unless the fields are also renamed.

        [UsedImplicitly]
        public bool ShouldSerializeFlashlightDifficulty() => Mods.Any(m => m is ModFlashlight);

        #endregion
    }
}
