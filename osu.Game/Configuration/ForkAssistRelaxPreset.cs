// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum ForkAssistRelaxPreset
    {
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.PresetNone))]
        None,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.PresetSoft))]
        Soft,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.PresetBalanced))]
        Balanced,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.PresetSticky))]
        Sticky
    }

    public static class ForkAssistRelaxPresetExtensions
    {
        public static void Apply(this ForkAssistRelaxPreset preset, OsuConfigManager config)
        {
            if (preset == ForkAssistRelaxPreset.None)
                return;

            PresetValues values = getValues(preset);

            config.SetValue(OsuSetting.ForkAimAssistEnabled, true);
            config.SetValue(OsuSetting.ForkAimAssistStrength, values.AimStrength);
            config.SetValue(OsuSetting.ForkAimAssistFovRadius, values.AimFovRadius);
            config.SetValue(OsuSetting.ForkAimAssistIntentThreshold, values.AimIntentThreshold);
            config.SetValue(OsuSetting.ForkAimAssistDynamicFriction, values.AimDynamicFriction);
            config.SetValue(OsuSetting.ForkAimAssistAntiJitterMs, values.AimAntiJitterMs);
            config.SetValue(OsuSetting.ForkAimAssistOvershootAllowance, values.AimOvershootAllowance);
            config.SetValue(OsuSetting.ForkAimAssistCenterBias, values.AimCenterBias);

            config.SetValue(OsuSetting.ForkRelaxEnabled, true);
            config.SetValue(OsuSetting.ForkRelaxBaseOffset, values.RelaxBaseOffset);
            config.SetValue(OsuSetting.ForkRelaxTimingVariance, values.RelaxTimingVariance);
            config.SetValue(OsuSetting.ForkRelaxDynamicDrift, values.RelaxDynamicDrift);
            config.SetValue(OsuSetting.ForkRelaxHoldTime, values.RelaxHoldTime);
            config.SetValue(OsuSetting.ForkRelaxSliderTailOffset, values.RelaxSliderTailOffset);
            config.SetValue(OsuSetting.ForkRelaxSyncRadius, values.RelaxSyncRadius);
            config.SetValue(OsuSetting.ForkRelaxMaxSyncDelay, values.RelaxMaxSyncDelay);
            config.SetValue(OsuSetting.ForkRelaxStableBpm, values.RelaxStableBpm);
            config.SetValue(OsuSetting.ForkRelaxMisaltProbability, values.RelaxMisaltProbability);
        }

        public static LocalisableString GetDisplayName(this ForkAssistRelaxPreset preset)
            => preset switch
            {
                ForkAssistRelaxPreset.Soft => ForkSettingsStrings.PresetSoft,
                ForkAssistRelaxPreset.Balanced => ForkSettingsStrings.PresetBalanced,
                ForkAssistRelaxPreset.Sticky => ForkSettingsStrings.PresetSticky,
                _ => ForkSettingsStrings.PresetNone
            };

        public static LocalisableString GetSummary(this ForkAssistRelaxPreset preset)
            => preset switch
            {
                ForkAssistRelaxPreset.Soft => ForkSettingsStrings.PresetSoftSummary,
                ForkAssistRelaxPreset.Balanced => ForkSettingsStrings.PresetBalancedSummary,
                ForkAssistRelaxPreset.Sticky => ForkSettingsStrings.PresetStickySummary,
                _ => ForkSettingsStrings.PresetNoneSummary
            };

        private static PresetValues getValues(ForkAssistRelaxPreset preset)
            => preset switch
            {
                ForkAssistRelaxPreset.Soft => new PresetValues(
                    AimStrength: 0.44,
                    AimFovRadius: 110f,
                    AimIntentThreshold: 0.05,
                    AimDynamicFriction: 0.28,
                    AimAntiJitterMs: 25.0,
                    AimOvershootAllowance: 10f,
                    AimCenterBias: 0.45,
                    RelaxBaseOffset: 10.0,
                    RelaxTimingVariance: 12.0,
                    RelaxDynamicDrift: 2.0,
                    RelaxHoldTime: 36.0,
                    RelaxSliderTailOffset: 0.0,
                    RelaxSyncRadius: 13f,
                    RelaxMaxSyncDelay: 10.0,
                    RelaxStableBpm: 172.0,
                    RelaxMisaltProbability: 0.01),

                ForkAssistRelaxPreset.Balanced => new PresetValues(
                    AimStrength: 0.68,
                    AimFovRadius: 140f,
                    AimIntentThreshold: -0.12,
                    AimDynamicFriction: 0.48,
                    AimAntiJitterMs: 35.0,
                    AimOvershootAllowance: 16f,
                    AimCenterBias: 0.58,
                    RelaxBaseOffset: 6.0,
                    RelaxTimingVariance: 9.0,
                    RelaxDynamicDrift: 4.0,
                    RelaxHoldTime: 40.0,
                    RelaxSliderTailOffset: 0.0,
                    RelaxSyncRadius: 18f,
                    RelaxMaxSyncDelay: 16.0,
                    RelaxStableBpm: 190.0,
                    RelaxMisaltProbability: 0.02),

                ForkAssistRelaxPreset.Sticky => new PresetValues(
                    AimStrength: 0.92,
                    AimFovRadius: 185f,
                    AimIntentThreshold: -0.35,
                    AimDynamicFriction: 0.72,
                    AimAntiJitterMs: 28.0,
                    AimOvershootAllowance: 22f,
                    AimCenterBias: 0.52,
                    RelaxBaseOffset: 2.0,
                    RelaxTimingVariance: 7.0,
                    RelaxDynamicDrift: 3.0,
                    RelaxHoldTime: 38.0,
                    RelaxSliderTailOffset: 0.0,
                    RelaxSyncRadius: 26f,
                    RelaxMaxSyncDelay: 22.0,
                    RelaxStableBpm: 236.0,
                    RelaxMisaltProbability: 0.0),

                _ => throw new InvalidEnumArgumentException(nameof(preset), (int)preset, typeof(ForkAssistRelaxPreset))
            };

        private readonly record struct PresetValues(
            double AimStrength,
            float AimFovRadius,
            double AimIntentThreshold,
            double AimDynamicFriction,
            double AimAntiJitterMs,
            float AimOvershootAllowance,
            double AimCenterBias,
            double RelaxBaseOffset,
            double RelaxTimingVariance,
            double RelaxDynamicDrift,
            double RelaxHoldTime,
            double RelaxSliderTailOffset,
            float RelaxSyncRadius,
            double RelaxMaxSyncDelay,
            double RelaxStableBpm,
            double RelaxMisaltProbability);
    }
}
