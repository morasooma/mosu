// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class MosuModsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.MosuMods";

        public static LocalisableString ModAimAssistName => new TranslatableString(getKey(@"mod_aim_assist_name"), @"Aim Assist");
        public static LocalisableString ModAimAssistDescription => new TranslatableString(getKey(@"mod_aim_assist_description"), @"Cursor movement assistance for the Mosu server.");
        public static LocalisableString ModAimAssistStrength => new TranslatableString(getKey(@"mod_aim_assist_strength"), @"Strength");
        public static LocalisableString ModAimAssistStrengthDescription => new TranslatableString(getKey(@"mod_aim_assist_strength_description"), @"Controls the overall assist strength. Other parameters are calculated automatically.");
        public static LocalisableString ModAimAssistShowDebug => new TranslatableString(getKey(@"mod_aim_assist_show_debug"), @"Show Debug");
        public static LocalisableString ModAimAssistShowDebugDescription => new TranslatableString(getKey(@"mod_aim_assist_show_debug_description"), @"Show real cursor (orange), assist (blue), target (green circle) and offset line. Enable for debugging.");

        public static LocalisableString ModAntiAimAssistName => new TranslatableString(getKey(@"mod_anti_aim_assist_name"), @"Anti Aim Assist");
        public static LocalisableString ModAntiAimAssistDescription => new TranslatableString(getKey(@"mod_anti_aim_assist_description"), @"Counteracts aim assistance by adding random cursor drift and deviations.");
        public static LocalisableString ModAntiAimAssistIntensity => new TranslatableString(getKey(@"mod_anti_aim_assist_intensity"), @"Intensity");
        public static LocalisableString ModAntiAimAssistIntensityDescription => new TranslatableString(getKey(@"mod_anti_aim_assist_intensity_description"), @"How strongly to deviate the cursor from targets. 0 = disabled, 1 = maximum.");
        public static LocalisableString ModAntiAimAssistRandomDrift => new TranslatableString(getKey(@"mod_anti_aim_assist_random_drift"), @"Random drift");
        public static LocalisableString ModAntiAimAssistRandomDriftDescription => new TranslatableString(getKey(@"mod_anti_aim_assist_random_drift_description"), @"Adds a constant noise to the cursor position.");

        public static LocalisableString ModStaticBpmName => new TranslatableString(getKey(@"mod_static_bpm_name"), @"Static BPM");
        public static LocalisableString ModStaticBpmDescription => new TranslatableString(getKey(@"mod_static_bpm_description"), @"Adjusts playback speed so that the beatmap is always played at the selected BPM.");
        public static LocalisableString ModStaticBpmTargetBpm => new TranslatableString(getKey(@"mod_static_bpm_target_bpm"), @"Target BPM");
        public static LocalisableString ModStaticBpmTargetBpmDescription => new TranslatableString(getKey(@"mod_static_bpm_target_bpm_description"), @"The BPM to which the beatmap will be automatically sped up or slowed down.");

        public static LocalisableString ModTargetDifficultyName => new TranslatableString(getKey(@"mod_target_difficulty_name"), @"Target Difficulty");
        public static LocalisableString ModTargetDifficultyDescription => new TranslatableString(getKey(@"mod_target_difficulty_description"), @"Adjusts playback speed so that the beatmap is always played at approximately the selected difficulty.");
        public static LocalisableString ModTargetDifficultyTarget => new TranslatableString(getKey(@"mod_target_difficulty_target"), @"Target difficulty");
        public static LocalisableString ModTargetDifficultyTargetDescription => new TranslatableString(getKey(@"mod_target_difficulty_target_description"), @"Difficulty (star rating) to automatically speed up or slow down the beatmap.");

        public static LocalisableString ModAdjustPitch => new TranslatableString(getKey(@"mod_adjust_pitch"), @"Adjust pitch");
        public static LocalisableString ModAdjustPitchDescription => new TranslatableString(getKey(@"mod_adjust_pitch_description"), @"Whether audio pitch should scale with playback rate.");
        public static LocalisableString ModLockDifficultyAdjust => new TranslatableString(getKey(@"mod_lock_difficulty_adjust"), @"Lock AR/OD");
        public static LocalisableString ModLockDifficultyAdjustDescription => new TranslatableString(getKey(@"mod_lock_difficulty_adjust_description"), @"Keeps approach rate (AR) and overall difficulty (OD) original regardless of beatmap speed changes.");
        public static LocalisableString ModSpeedChange => new TranslatableString(getKey(@"mod_speed_change"), @"Speed change");
        public static LocalisableString Yes => new TranslatableString(getKey(@"yes"), @"Yes");
        public static LocalisableString No => new TranslatableString(getKey(@"no"), @"No");

        public static LocalisableString ModMosuRelaxName => new TranslatableString(getKey(@"mod_mosu_relax_name"), @"Mosu Relax");
        public static LocalisableString ModMosuRelaxDescription => new TranslatableString(getKey(@"mod_mosu_relax_description"), @"Alternative relax for the Mosu server.");
        public static LocalisableString RelaxDynamicDrift => new TranslatableString(getKey(@"relax_dynamic_drift"), @"Dynamic drift");
        public static LocalisableString RelaxDynamicDriftDescription => new TranslatableString(getKey(@"relax_dynamic_drift_description"), @"Smooth timing shift during the beatmap.");
        public static LocalisableString RelaxSliderTailOffset => new TranslatableString(getKey(@"relax_slider_tail_offset"), @"Slider tail offset");
        public static LocalisableString RelaxSliderTailOffsetDescription => new TranslatableString(getKey(@"relax_slider_tail_offset_description"), @"Timing offset for releasing the slider tail.");
        public static LocalisableString RelaxAlternateThreshold => new TranslatableString(getKey(@"relax_alternate_threshold"), @"Alternate threshold");
        public static LocalisableString RelaxAlternateThresholdDescription => new TranslatableString(getKey(@"relax_alternate_threshold_description"), @"BPM above which the relax mod starts alternating keys.");
        public static LocalisableString RelaxMisaltProbability => new TranslatableString(getKey(@"relax_misalt_probability"), @"Misalt probability");
        public static LocalisableString RelaxMisaltProbabilityDescription => new TranslatableString(getKey(@"relax_misalt_probability_description"), @"Chance of a random misalt during streams.");
        public static LocalisableString RelaxStreamBlindMode => new TranslatableString(getKey(@"relax_stream_blind_mode"), @"Stream blind mode");
        public static LocalisableString RelaxStreamBlindModeDescription => new TranslatableString(getKey(@"relax_stream_blind_mode_description"), @"Enables blind mode for streams.");

        public static LocalisableString ModMalevichName => new TranslatableString(getKey(@"mod_malevich_name"), @"Malevich Square");
        public static LocalisableString ModMalevichDescription => new TranslatableString(getKey(@"mod_malevich_description"), @"A black square grows from the centre of the screen with each combo, fully covering the playfield at max combo.");
        public static LocalisableString ModMalevichMaxCombo => new TranslatableString(getKey(@"mod_malevich_max_combo"), @"Max combo");
        public static LocalisableString ModMalevichMaxComboDescription => new TranslatableString(getKey(@"mod_malevich_max_combo_description"), @"Combo at which the square fully covers the playfield.");

        public static LocalisableString ModAudioEffectsName => new TranslatableString(getKey(@"mod_audio_effects_name"), @"Audio FX");
        public static LocalisableString ModAudioEffectsDescription => new TranslatableString(getKey(@"mod_audio_effects_description"), @"Reshape the music without changing the beatmap speed.");
        public static LocalisableString ModAudioEffectsPreset => new TranslatableString(getKey(@"mod_audio_effects_preset"), @"Preset");
        public static LocalisableString ModAudioEffectsPresetDescription => new TranslatableString(getKey(@"mod_audio_effects_preset_description"), @"Select the main sound profile.");
        public static LocalisableString ModAudioEffectsIntensity => new TranslatableString(getKey(@"mod_audio_effects_intensity"), @"Intensity");
        public static LocalisableString ModAudioEffectsIntensityDescription => new TranslatableString(getKey(@"mod_audio_effects_intensity_description"), @"Controls how strongly the selected preset is applied.");
        public static LocalisableString ModAudioEffectsPitch => new TranslatableString(getKey(@"mod_audio_effects_pitch"), @"Pitch");
        public static LocalisableString ModAudioEffectsPitchDescription => new TranslatableString(getKey(@"mod_audio_effects_pitch_description"), @"Changes pitch in semitones while keeping the original playback speed.");
        public static LocalisableString ModAudioEffectsAffectHitSounds => new TranslatableString(getKey(@"mod_audio_effects_affect_hit_sounds"), @"Affect hit sounds");
        public static LocalisableString ModAudioEffectsAffectHitSoundsDescription => new TranslatableString(getKey(@"mod_audio_effects_affect_hit_sounds_description"), @"Apply the pitch and selected preset to gameplay hit sounds too.");
        public static LocalisableString ModAudioEffectsOriginalPitch => new TranslatableString(getKey(@"mod_audio_effects_original_pitch"), @"original");
        public static LocalisableString ModAudioEffectsSemitones => new TranslatableString(getKey(@"mod_audio_effects_semitones"), @"semitones");
        public static LocalisableString AudioPresetNone => new TranslatableString(getKey(@"audio_preset_none"), @"None (pitch only)");
        public static LocalisableString AudioPresetWarm => new TranslatableString(getKey(@"audio_preset_warm"), @"Warm");
        public static LocalisableString AudioPresetBassBoost => new TranslatableString(getKey(@"audio_preset_bass_boost"), @"Bass boost");
        public static LocalisableString AudioPresetBright => new TranslatableString(getKey(@"audio_preset_bright"), @"Bright");
        public static LocalisableString AudioPresetRadio => new TranslatableString(getKey(@"audio_preset_radio"), @"Radio");
        public static LocalisableString AudioPresetUnderwater => new TranslatableString(getKey(@"audio_preset_underwater"), @"Underwater");
        public static LocalisableString AudioPresetSpacious => new TranslatableString(getKey(@"audio_preset_spacious"), @"Spacious");
        public static LocalisableString AudioPresetChorus => new TranslatableString(getKey(@"audio_preset_chorus"), @"Chorus");
        public static LocalisableString AudioPresetPhaser => new TranslatableString(getKey(@"audio_preset_phaser"), @"Phaser");
        public static LocalisableString AudioPresetLoFi => new TranslatableString(getKey(@"audio_preset_lo_fi"), @"Lo-fi");
        public static LocalisableString AudioPresetRotation => new TranslatableString(getKey(@"audio_preset_rotation"), @"8D rotation");


        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
