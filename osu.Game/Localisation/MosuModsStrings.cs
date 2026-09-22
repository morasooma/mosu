// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class MosuModsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.MosuMods";

        /// <summary>
        /// "Aim Assist"
        /// </summary>
        public static LocalisableString ModAimAssistName => new TranslatableString(getKey(@"mod_aim_assist_name"), @"Aim Assist");
        /// <summary>
        /// "Cursor movement assistance for the Morasooma server."
        /// </summary>
        public static LocalisableString ModAimAssistDescription => new TranslatableString(getKey(@"mod_aim_assist_description"), @"Cursor movement assistance for the Morasooma server.");
        /// <summary>
        /// "Strength"
        /// </summary>
        public static LocalisableString ModAimAssistStrength => new TranslatableString(getKey(@"mod_aim_assist_strength"), @"Strength");
        /// <summary>
        /// "Controls the overall assist strength. Other parameters are calculated automatically."
        /// </summary>
        public static LocalisableString ModAimAssistStrengthDescription => new TranslatableString(getKey(@"mod_aim_assist_strength_description"), @"Controls the overall assist strength. Other parameters are calculated automatically.");
        /// <summary>
        /// "Show Debug"
        /// </summary>
        public static LocalisableString ModAimAssistShowDebug => new TranslatableString(getKey(@"mod_aim_assist_show_debug"), @"Show Debug");
        /// <summary>
        /// "Show real cursor (orange), assist (blue), target (green circle) and offset line. Enable for debugging."
        /// </summary>
        public static LocalisableString ModAimAssistShowDebugDescription => new TranslatableString(getKey(@"mod_aim_assist_show_debug_description"), @"Show real cursor (orange), assist (blue), target (green circle) and offset line. Enable for debugging.");

        /// <summary>
        /// "Anti Aim Assist"
        /// </summary>
        public static LocalisableString ModAntiAimAssistName => new TranslatableString(getKey(@"mod_anti_aim_assist_name"), @"Anti Aim Assist");
        /// <summary>
        /// "Counteracts aim assistance by adding random cursor drift and deviations."
        /// </summary>
        public static LocalisableString ModAntiAimAssistDescription => new TranslatableString(getKey(@"mod_anti_aim_assist_description"), @"Counteracts aim assistance by adding random cursor drift and deviations.");
        /// <summary>
        /// "Intensity"
        /// </summary>
        public static LocalisableString ModAntiAimAssistIntensity => new TranslatableString(getKey(@"mod_anti_aim_assist_intensity"), @"Intensity");
        /// <summary>
        /// "How strongly to deviate the cursor from targets. 0 = disabled, 1 = maximum."
        /// </summary>
        public static LocalisableString ModAntiAimAssistIntensityDescription => new TranslatableString(getKey(@"mod_anti_aim_assist_intensity_description"), @"How strongly to deviate the cursor from targets. 0 = disabled, 1 = maximum.");
        /// <summary>
        /// "Random drift"
        /// </summary>
        public static LocalisableString ModAntiAimAssistRandomDrift => new TranslatableString(getKey(@"mod_anti_aim_assist_random_drift"), @"Random drift");
        /// <summary>
        /// "Adds a constant noise to the cursor position."
        /// </summary>
        public static LocalisableString ModAntiAimAssistRandomDriftDescription => new TranslatableString(getKey(@"mod_anti_aim_assist_random_drift_description"), @"Adds a constant noise to the cursor position.");

        /// <summary>
        /// "Static BPM"
        /// </summary>
        public static LocalisableString ModStaticBpmName => new TranslatableString(getKey(@"mod_static_bpm_name"), @"Static BPM");
        /// <summary>
        /// "Adjusts playback speed so that the beatmap is always played at the selected BPM."
        /// </summary>
        public static LocalisableString ModStaticBpmDescription => new TranslatableString(getKey(@"mod_static_bpm_description"), @"Adjusts playback speed so that the beatmap is always played at the selected BPM.");
        /// <summary>
        /// "Target BPM"
        /// </summary>
        public static LocalisableString ModStaticBpmTargetBpm => new TranslatableString(getKey(@"mod_static_bpm_target_bpm"), @"Target BPM");
        /// <summary>
        /// "The BPM to which the beatmap will be automatically sped up or slowed down."
        /// </summary>
        public static LocalisableString ModStaticBpmTargetBpmDescription => new TranslatableString(getKey(@"mod_static_bpm_target_bpm_description"), @"The BPM to which the beatmap will be automatically sped up or slowed down.");

        /// <summary>
        /// "Target Difficulty"
        /// </summary>
        public static LocalisableString ModTargetDifficultyName => new TranslatableString(getKey(@"mod_target_difficulty_name"), @"Target Difficulty");
        /// <summary>
        /// "Adjusts playback speed so that the beatmap is always played at approximately the selected difficulty."
        /// </summary>
        public static LocalisableString ModTargetDifficultyDescription => new TranslatableString(getKey(@"mod_target_difficulty_description"), @"Adjusts playback speed so that the beatmap is always played at approximately the selected difficulty.");
        /// <summary>
        /// "Target difficulty"
        /// </summary>
        public static LocalisableString ModTargetDifficultyTarget => new TranslatableString(getKey(@"mod_target_difficulty_target"), @"Target difficulty");
        /// <summary>
        /// "Difficulty (star rating) to automatically speed up or slow down the beatmap."
        /// </summary>
        public static LocalisableString ModTargetDifficultyTargetDescription => new TranslatableString(getKey(@"mod_target_difficulty_target_description"), @"Difficulty (star rating) to automatically speed up or slow down the beatmap.");

        /// <summary>
        /// "Adjust pitch"
        /// </summary>
        public static LocalisableString ModAdjustPitch => new TranslatableString(getKey(@"mod_adjust_pitch"), @"Adjust pitch");
        /// <summary>
        /// "Whether audio pitch should scale with playback rate."
        /// </summary>
        public static LocalisableString ModAdjustPitchDescription => new TranslatableString(getKey(@"mod_adjust_pitch_description"), @"Whether audio pitch should scale with playback rate.");
        /// <summary>
        /// "Lock AR/OD"
        /// </summary>
        public static LocalisableString ModLockDifficultyAdjust => new TranslatableString(getKey(@"mod_lock_difficulty_adjust"), @"Lock AR/OD");
        /// <summary>
        /// "Keeps approach rate (AR) and overall difficulty (OD) original regardless of beatmap speed changes."
        /// </summary>
        public static LocalisableString ModLockDifficultyAdjustDescription => new TranslatableString(getKey(@"mod_lock_difficulty_adjust_description"), @"Keeps approach rate (AR) and overall difficulty (OD) original regardless of beatmap speed changes.");
        /// <summary>
        /// "Speed change"
        /// </summary>
        public static LocalisableString ModSpeedChange => new TranslatableString(getKey(@"mod_speed_change"), @"Speed change");
        /// <summary>
        /// "Yes"
        /// </summary>
        public static LocalisableString Yes => new TranslatableString(getKey(@"yes"), @"Yes");
        /// <summary>
        /// "No"
        /// </summary>
        public static LocalisableString No => new TranslatableString(getKey(@"no"), @"No");

        /// <summary>
        /// "Morasooma Relax"
        /// </summary>
        public static LocalisableString ModMosuRelaxName => new TranslatableString(getKey(@"mod_mosu_relax_name"), @"Morasooma Relax");
        /// <summary>
        /// "Alternative relax for the Morasooma server."
        /// </summary>
        public static LocalisableString ModMosuRelaxDescription => new TranslatableString(getKey(@"mod_mosu_relax_description"), @"Alternative relax for the Morasooma server.");
        /// <summary>
        /// "Dynamic drift"
        /// </summary>
        public static LocalisableString RelaxDynamicDrift => new TranslatableString(getKey(@"relax_dynamic_drift"), @"Dynamic drift");
        /// <summary>
        /// "Smooth timing shift during the beatmap."
        /// </summary>
        public static LocalisableString RelaxDynamicDriftDescription => new TranslatableString(getKey(@"relax_dynamic_drift_description"), @"Smooth timing shift during the beatmap.");
        /// <summary>
        /// "Slider tail offset"
        /// </summary>
        public static LocalisableString RelaxSliderTailOffset => new TranslatableString(getKey(@"relax_slider_tail_offset"), @"Slider tail offset");
        /// <summary>
        /// "Timing offset for releasing the slider tail."
        /// </summary>
        public static LocalisableString RelaxSliderTailOffsetDescription => new TranslatableString(getKey(@"relax_slider_tail_offset_description"), @"Timing offset for releasing the slider tail.");
        /// <summary>
        /// "Alternate threshold"
        /// </summary>
        public static LocalisableString RelaxAlternateThreshold => new TranslatableString(getKey(@"relax_alternate_threshold"), @"Alternate threshold");
        /// <summary>
        /// "BPM above which the relax mod starts alternating keys."
        /// </summary>
        public static LocalisableString RelaxAlternateThresholdDescription => new TranslatableString(getKey(@"relax_alternate_threshold_description"), @"BPM above which the relax mod starts alternating keys.");
        /// <summary>
        /// "Misalt probability"
        /// </summary>
        public static LocalisableString RelaxMisaltProbability => new TranslatableString(getKey(@"relax_misalt_probability"), @"Misalt probability");
        /// <summary>
        /// "Chance of a random misalt during streams."
        /// </summary>
        public static LocalisableString RelaxMisaltProbabilityDescription => new TranslatableString(getKey(@"relax_misalt_probability_description"), @"Chance of a random misalt during streams.");
        /// <summary>
        /// "Stream blind mode"
        /// </summary>
        public static LocalisableString RelaxStreamBlindMode => new TranslatableString(getKey(@"relax_stream_blind_mode"), @"Stream blind mode");
        /// <summary>
        /// "Enables blind mode for streams."
        /// </summary>
        public static LocalisableString RelaxStreamBlindModeDescription => new TranslatableString(getKey(@"relax_stream_blind_mode_description"), @"Enables blind mode for streams.");

        /// <summary>
        /// "Malevich Square"
        /// </summary>
        public static LocalisableString ModMalevichName => new TranslatableString(getKey(@"mod_malevich_name"), @"Malevich Square");
        /// <summary>
        /// "A black square grows from the centre of the screen with each combo, fully covering the playfield at max combo."
        /// </summary>
        public static LocalisableString ModMalevichDescription => new TranslatableString(getKey(@"mod_malevich_description"), @"A black square grows from the centre of the screen with each combo, fully covering the playfield at max combo.");
        /// <summary>
        /// "Max combo"
        /// </summary>
        public static LocalisableString ModMalevichMaxCombo => new TranslatableString(getKey(@"mod_malevich_max_combo"), @"Max combo");
        /// <summary>
        /// "Combo at which the square fully covers the playfield."
        /// </summary>
        public static LocalisableString ModMalevichMaxComboDescription => new TranslatableString(getKey(@"mod_malevich_max_combo_description"), @"Combo at which the square fully covers the playfield.");

        /// <summary>
        /// "Audio FX"
        /// </summary>
        public static LocalisableString ModAudioEffectsName => new TranslatableString(getKey(@"mod_audio_effects_name"), @"Audio FX");
        /// <summary>
        /// "Reshape the music without changing the beatmap speed."
        /// </summary>
        public static LocalisableString ModAudioEffectsDescription => new TranslatableString(getKey(@"mod_audio_effects_description"), @"Reshape the music without changing the beatmap speed.");
        /// <summary>
        /// "Preset"
        /// </summary>
        public static LocalisableString ModAudioEffectsPreset => new TranslatableString(getKey(@"mod_audio_effects_preset"), @"Preset");
        /// <summary>
        /// "Select the main sound profile."
        /// </summary>
        public static LocalisableString ModAudioEffectsPresetDescription => new TranslatableString(getKey(@"mod_audio_effects_preset_description"), @"Select the main sound profile.");
        /// <summary>
        /// "Intensity"
        /// </summary>
        public static LocalisableString ModAudioEffectsIntensity => new TranslatableString(getKey(@"mod_audio_effects_intensity"), @"Intensity");
        /// <summary>
        /// "Controls how strongly the selected preset is applied."
        /// </summary>
        public static LocalisableString ModAudioEffectsIntensityDescription => new TranslatableString(getKey(@"mod_audio_effects_intensity_description"), @"Controls how strongly the selected preset is applied.");
        /// <summary>
        /// "Pitch"
        /// </summary>
        public static LocalisableString ModAudioEffectsPitch => new TranslatableString(getKey(@"mod_audio_effects_pitch"), @"Pitch");
        /// <summary>
        /// "Changes pitch in semitones while keeping the original playback speed."
        /// </summary>
        public static LocalisableString ModAudioEffectsPitchDescription => new TranslatableString(getKey(@"mod_audio_effects_pitch_description"), @"Changes pitch in semitones while keeping the original playback speed.");
        /// <summary>
        /// "Affect hit sounds"
        /// </summary>
        public static LocalisableString ModAudioEffectsAffectHitSounds => new TranslatableString(getKey(@"mod_audio_effects_affect_hit_sounds"), @"Affect hit sounds");
        /// <summary>
        /// "Apply the pitch and selected preset to gameplay hit sounds too."
        /// </summary>
        public static LocalisableString ModAudioEffectsAffectHitSoundsDescription => new TranslatableString(getKey(@"mod_audio_effects_affect_hit_sounds_description"), @"Apply the pitch and selected preset to gameplay hit sounds too.");
        /// <summary>
        /// "original"
        /// </summary>
        public static LocalisableString ModAudioEffectsOriginalPitch => new TranslatableString(getKey(@"mod_audio_effects_original_pitch"), @"original");
        /// <summary>
        /// "semitones"
        /// </summary>
        public static LocalisableString ModAudioEffectsSemitones => new TranslatableString(getKey(@"mod_audio_effects_semitones"), @"semitones");
        /// <summary>
        /// "None (pitch only)"
        /// </summary>
        public static LocalisableString AudioPresetNone => new TranslatableString(getKey(@"audio_preset_none"), @"None (pitch only)");
        /// <summary>
        /// "Warm"
        /// </summary>
        public static LocalisableString AudioPresetWarm => new TranslatableString(getKey(@"audio_preset_warm"), @"Warm");
        /// <summary>
        /// "Bass boost"
        /// </summary>
        public static LocalisableString AudioPresetBassBoost => new TranslatableString(getKey(@"audio_preset_bass_boost"), @"Bass boost");
        /// <summary>
        /// "Bright"
        /// </summary>
        public static LocalisableString AudioPresetBright => new TranslatableString(getKey(@"audio_preset_bright"), @"Bright");
        /// <summary>
        /// "Radio"
        /// </summary>
        public static LocalisableString AudioPresetRadio => new TranslatableString(getKey(@"audio_preset_radio"), @"Radio");
        /// <summary>
        /// "Underwater"
        /// </summary>
        public static LocalisableString AudioPresetUnderwater => new TranslatableString(getKey(@"audio_preset_underwater"), @"Underwater");
        /// <summary>
        /// "Spacious"
        /// </summary>
        public static LocalisableString AudioPresetSpacious => new TranslatableString(getKey(@"audio_preset_spacious"), @"Spacious");
        /// <summary>
        /// "Chorus"
        /// </summary>
        public static LocalisableString AudioPresetChorus => new TranslatableString(getKey(@"audio_preset_chorus"), @"Chorus");
        /// <summary>
        /// "Phaser"
        /// </summary>
        public static LocalisableString AudioPresetPhaser => new TranslatableString(getKey(@"audio_preset_phaser"), @"Phaser");
        /// <summary>
        /// "Lo-fi"
        /// </summary>
        public static LocalisableString AudioPresetLoFi => new TranslatableString(getKey(@"audio_preset_lo_fi"), @"Lo-fi");
        /// <summary>
        /// "8D rotation"
        /// </summary>
        public static LocalisableString AudioPresetRotation => new TranslatableString(getKey(@"audio_preset_rotation"), @"8D rotation");


        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
