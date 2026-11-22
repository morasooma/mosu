// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModMosuRelax : Mod, IApplicableToDrawableRuleset<OsuHitObject>, IHasConditionalSettings
    {
        public override string Name => "Morasooma Relax";
        public override string Acronym => "MRX";
        public override ModType Type => ModType.Mosu;
        public override LocalisableString Description => MosuModsStrings.ModMosuRelaxDescription;
        public override bool Ranked => false;
        public override bool HasImplementation => true;
        
        public override Type[] IncompatibleMods => new[]
        {
            typeof(OsuModAutopilot),
            typeof(ModAutoplay),
            typeof(OsuModRelax),
            typeof(OsuModMagnetised),
            typeof(OsuModRepel),
            typeof(OsuModTransform),
            typeof(OsuModWiggle),
            typeof(OsuModBubbles),
            typeof(OsuModDepth),
            typeof(ModTouchDevice),
            typeof(OsuModAlternate),
            typeof(OsuModSingleTap)
        };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.Algorithm), nameof(RelaxAlgorithmStrings.AlgorithmHint), -1)]
        public Bindable<MosuRelaxAlgorithm> Algorithm { get; } = new Bindable<MosuRelaxAlgorithm>();

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.TargetUR), null, 20)]
        public BindableInt MapleTargetUR { get; } = new BindableInt(100) { MinValue = 0, MaxValue = 300 };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.Offset), null, 21)]
        public BindableInt MapleOffset { get; } = new BindableInt { MinValue = -50, MaxValue = 50 };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.CircleHold), null, 22)]
        public BindableInt MapleCircleHold { get; } = new BindableInt(80) { MinValue = 25, MaxValue = 100 };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.CircleError), null, 23)]
        public BindableInt MapleCircleHoldError { get; } = new BindableInt(20) { MinValue = 0, MaxValue = 75 };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.SliderHold), null, 24)]
        public BindableInt MapleSliderHold { get; } = new BindableInt(65) { MinValue = 25, MaxValue = 100 };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.SliderError), null, 25)]
        public BindableInt MapleSliderHoldError { get; } = new BindableInt(15) { MinValue = 0, MaxValue = 75 };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.AlternateBpm), null, 26)]
        public BindableInt MapleAlternateBPM { get; } = new BindableInt(100) { MinValue = 0, MaxValue = 500 };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.SliderOverride), null, 27)]
        public BindableBool MapleSliderAlternationOverride { get; } = new BindableBool();

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.WaitLate), null, 28)]
        public BindableBool MapleWaitLate { get; } = new BindableBool(true);

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.Prediction), null, 29)]
        public BindableBool MapleDirectionPrediction { get; } = new BindableBool(true);

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.PredictionAngle), null, 30)]
        public BindableInt MaplePredictionAngle { get; } = new BindableInt(45) { MinValue = 0, MaxValue = 90 };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.PredictionScale), null, 31)]
        public BindableFloat MaplePredictionScale { get; } = new BindableFloat(0.7f) { MinValue = 0, MaxValue = 1, Precision = 0.01f };

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.LowestHold), null, 32)]
        public BindableBool MapleLowestHoldTimes { get; } = new BindableBool();

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.PrimaryButton), null, 33)]
        public Bindable<MapleRelaxButton> MaplePrimaryButton { get; } = new Bindable<MapleRelaxButton>();

        [SettingSource(typeof(RelaxAlgorithmStrings), nameof(RelaxAlgorithmStrings.SecondaryButton), null, 34)]
        public Bindable<MapleRelaxButton> MapleSecondaryButton { get; } = new Bindable<MapleRelaxButton>(MapleRelaxButton.Right);

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3707), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3708), 0)]
        public Bindable<MosuRelaxPreset> Preset { get; } = new Bindable<MosuRelaxPreset>(MosuRelaxPreset.Reliable);

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxBaseOffsetCaption), nameof(ForkSettingsStrings.RelaxBaseOffsetHint), 1, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble BaseOffset { get; } = new BindableDouble(-1.0) { MinValue = -60, MaxValue = 60 };

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3709), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3710), 2, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble TimingVariance { get; } = new BindableDouble(8.0) { MinValue = 0, MaxValue = 40 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxDynamicDriftCaption), nameof(ForkSettingsStrings.RelaxDynamicDriftHint), 3, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble DynamicDrift { get; } = new BindableDouble(1.5) { MinValue = 0, MaxValue = 40 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxHoldTimeCaption), nameof(ForkSettingsStrings.RelaxHoldTimeHint), 4, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble HoldTime { get; } = new BindableDouble(38.0) { MinValue = 0, MaxValue = 100 };

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3711), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3712), 5, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble SliderTailOffset { get; } = new BindableDouble(0.0) { MinValue = -80, MaxValue = 80 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxSyncRadiusCaption), nameof(ForkSettingsStrings.RelaxSyncRadiusHint), 6, SettingControlType = typeof(SettingsSlider<float>))]
        public BindableFloat SyncRadius { get; } = new BindableFloat(26f) { MinValue = 0f, MaxValue = 120f };

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3713), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3714), 7, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble MaxSyncDelay { get; } = new BindableDouble(22.0) { MinValue = 0, MaxValue = 120 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxAlternateThresholdCaption), nameof(ForkSettingsStrings.RelaxAlternateThresholdHint), 8, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble AlternateThreshold { get; } = new BindableDouble(170.0) { MinValue = 0, MaxValue = 300 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxStableBpmCaption), nameof(ForkSettingsStrings.RelaxStableBpmHint), 9, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble StableBpm { get; } = new BindableDouble(236.0) { MinValue = 60, MaxValue = 600 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxMisaltProbabilityCaption), nameof(ForkSettingsStrings.RelaxMisaltProbabilityHint), 10, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble MisaltProbability { get; } = new BindableDouble(0.0) { MinValue = 0, MaxValue = 1 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxStreamBlindModeCaption), nameof(ForkSettingsStrings.RelaxStreamBlindModeHint), 11)]
        public BindableBool StreamBlindMode { get; } = new BindableBool(false);

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxBlindTapEnabledCaption), nameof(ForkSettingsStrings.RelaxBlindTapEnabledHint), 12)]
        public BindableBool BlindTapEnabled { get; } = new BindableBool(false);

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3715), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3716), 13)]
        public BindableBool AimIntentEnabled { get; } = new BindableBool(false);

        private bool applyingPreset;
        private readonly BindableBool showMosu = new BindableBool(true);
        private readonly BindableBool showMaple = new BindableBool();

        public IBindable<bool>? GetSettingVisibility(string propertyName) => propertyName switch
        {
            nameof(Algorithm) => null,
            nameof(MapleTargetUR) or nameof(MapleOffset) or nameof(MapleCircleHold) or nameof(MapleCircleHoldError)
                or nameof(MapleSliderHold) or nameof(MapleSliderHoldError) or nameof(MapleAlternateBPM)
                or nameof(MapleSliderAlternationOverride) or nameof(MapleWaitLate) or nameof(MapleDirectionPrediction)
                or nameof(MaplePredictionAngle) or nameof(MaplePredictionScale) or nameof(MapleLowestHoldTimes)
                or nameof(MaplePrimaryButton) or nameof(MapleSecondaryButton) => showMaple,
            _ => showMosu,
        };

        public OsuModMosuRelax()
        {
            Algorithm.BindValueChanged(change =>
            {
                showMosu.Value = change.NewValue != MosuRelaxAlgorithm.Maple;
                showMaple.Value = change.NewValue == MosuRelaxAlgorithm.Maple;
            }, true);

            Preset.BindValueChanged(preset =>
            {
                if (preset.NewValue == MosuRelaxPreset.Custom || applyingPreset)
                    return;

                applyPreset(preset.NewValue);
            });

            BaseOffset.BindValueChanged(_ => markPresetCustom());
            TimingVariance.BindValueChanged(_ => markPresetCustom());
            DynamicDrift.BindValueChanged(_ => markPresetCustom());
            HoldTime.BindValueChanged(_ => markPresetCustom());
            SliderTailOffset.BindValueChanged(_ => markPresetCustom());
            SyncRadius.BindValueChanged(_ => markPresetCustom());
            MaxSyncDelay.BindValueChanged(_ => markPresetCustom());
            AlternateThreshold.BindValueChanged(_ => markPresetCustom());
            StableBpm.BindValueChanged(_ => markPresetCustom());
            MisaltProbability.BindValueChanged(_ => markPresetCustom());
            StreamBlindMode.BindValueChanged(_ => markPresetCustom());
            BlindTapEnabled.BindValueChanged(_ => markPresetCustom());
            AimIntentEnabled.BindValueChanged(_ => markPresetCustom());

            applyPreset(Preset.Value);
        }

        private void markPresetCustom()
        {
            if (!applyingPreset)
                Preset.Value = MosuRelaxPreset.Custom;
        }

        private void applyPreset(MosuRelaxPreset preset)
        {
            RelaxPresetValues values = preset switch
            {
                MosuRelaxPreset.Natural => new RelaxPresetValues(5, 10, 2, 36, 0, 13, 10, 170, 172, 0, false, false),
                MosuRelaxPreset.Balanced => new RelaxPresetValues(4, 8, 2, 40, 0, 18, 16, 170, 190, 0, false, false),
                MosuRelaxPreset.Reliable => new RelaxPresetValues(-1, 8, 1.5, 38, 0, 26, 22, 170, 236, 0, false, false),
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
            };

            applyingPreset = true;

            try
            {
                BaseOffset.Value = values.BaseOffset;
                TimingVariance.Value = values.TimingVariance;
                DynamicDrift.Value = values.DynamicDrift;
                HoldTime.Value = values.HoldTime;
                SliderTailOffset.Value = values.SliderTailOffset;
                SyncRadius.Value = values.SyncRadius;
                MaxSyncDelay.Value = values.MaxSyncDelay;
                AlternateThreshold.Value = values.AlternateThreshold;
                StableBpm.Value = values.StableBpm;
                MisaltProbability.Value = values.MisaltProbability;
                StreamBlindMode.Value = values.StreamBlindMode;
                BlindTapEnabled.Value = values.BlindTapEnabled;
                AimIntentEnabled.Value = true;
            }
            finally
            {
                applyingPreset = false;
            }
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
        }

        private readonly record struct RelaxPresetValues(
            double BaseOffset,
            double TimingVariance,
            double DynamicDrift,
            double HoldTime,
            double SliderTailOffset,
            float SyncRadius,
            double MaxSyncDelay,
            double AlternateThreshold,
            double StableBpm,
            double MisaltProbability,
            bool StreamBlindMode,
            bool BlindTapEnabled);
    }

    public enum MosuRelaxPreset
    {
        [Description("Свои настройки"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3717))]
        Custom,

        [Description("Естественный"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3718))]
        Natural,

        [Description("Сбалансированный"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3719))]
        Balanced,

        [Description("Надёжный"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3720))]
        Reliable
    }
}
