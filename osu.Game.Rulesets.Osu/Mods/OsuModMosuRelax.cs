// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    public class OsuModMosuRelax : Mod, IApplicableToDrawableRuleset<OsuHitObject>
    {
        public override string Name => "Mosu Relax";
        public override string Acronym => "MRX";
        public override ModType Type => ModType.Mosu;
        public override LocalisableString Description => MosuModsStrings.ModMosuRelaxDescription;
        public override double ScoreMultiplier => 0.10;
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

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxBaseOffsetCaption), nameof(ForkSettingsStrings.RelaxBaseOffsetHint), 0, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble BaseOffset { get; } = new BindableDouble(2.0) { MinValue = -60, MaxValue = 60 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxVarianceCaption), nameof(ForkSettingsStrings.RelaxVarianceHint), 1, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble TimingVariance { get; } = new BindableDouble(7.0) { MinValue = 0, MaxValue = 40 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxDynamicDriftCaption), nameof(ForkSettingsStrings.RelaxDynamicDriftHint), 2, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble DynamicDrift { get; } = new BindableDouble(3.0) { MinValue = 0, MaxValue = 40 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxHoldTimeCaption), nameof(ForkSettingsStrings.RelaxHoldTimeHint), 3, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble HoldTime { get; } = new BindableDouble(38.0) { MinValue = 0, MaxValue = 100 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxSliderTailOffsetCaption), nameof(ForkSettingsStrings.RelaxSliderTailOffsetHint), 4, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble SliderTailOffset { get; } = new BindableDouble(0.0) { MinValue = -80, MaxValue = 80 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxSyncRadiusCaption), nameof(ForkSettingsStrings.RelaxSyncRadiusHint), 5, SettingControlType = typeof(SettingsSlider<float>))]
        public BindableFloat SyncRadius { get; } = new BindableFloat(26f) { MinValue = 0f, MaxValue = 120f };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxMaxSyncDelayCaption), nameof(ForkSettingsStrings.RelaxMaxSyncDelayHint), 6, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble MaxSyncDelay { get; } = new BindableDouble(22.0) { MinValue = 0, MaxValue = 120 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxAlternateThresholdCaption), nameof(ForkSettingsStrings.RelaxAlternateThresholdHint), 7, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble AlternateThreshold { get; } = new BindableDouble(170.0) { MinValue = 0, MaxValue = 300 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxStableBpmCaption), nameof(ForkSettingsStrings.RelaxStableBpmHint), 8, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble StableBpm { get; } = new BindableDouble(236.0) { MinValue = 60, MaxValue = 600 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxMisaltProbabilityCaption), nameof(ForkSettingsStrings.RelaxMisaltProbabilityHint), 9, SettingControlType = typeof(SettingsSlider<double>))]
        public BindableDouble MisaltProbability { get; } = new BindableDouble(0.0) { MinValue = 0, MaxValue = 1 };

        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxStreamBlindModeCaption), nameof(ForkSettingsStrings.RelaxStreamBlindModeHint), 10)]
        public BindableBool StreamBlindMode { get; } = new BindableBool(false);
        
        [SettingSource(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxBlindTapEnabledCaption), nameof(ForkSettingsStrings.RelaxBlindTapEnabledHint), 11)]
        public BindableBool BlindTapEnabled { get; } = new BindableBool(false);
        
        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
        }
    }
}
