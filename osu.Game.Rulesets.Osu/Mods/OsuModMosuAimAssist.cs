using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModMosuAimAssist : Mod, IApplicableToDrawableRuleset<OsuHitObject>, IHasConditionalSettings
    {
        public override string Name => "Aim Assist";

        public override string Acronym => "AA";

        public override IconUsage? Icon => OsuIcon.ModMosuAimAssist;

        public override ModType Type => ModType.Mosu;

        public override LocalisableString Description => MosuModsStrings.ModAimAssistDescription;


        public override bool HasImplementation => true;

        public override bool Ranked => Algorithm.Value == MosuAimAssistAlgorithm.Mosu;

        public override bool ValidForFreestyleAsRequiredMod => true;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(OsuModAutopilot),
            typeof(ModAutoplay),
            typeof(OsuModMagnetised),
            typeof(OsuModRepel),
            typeof(OsuModTransform),
            typeof(OsuModWiggle),
            typeof(OsuModBubbles),
            typeof(OsuModDepth),
            typeof(ModTouchDevice),
        };

        [SettingSource(typeof(AimAssistAlgorithmStrings), nameof(AimAssistAlgorithmStrings.Algorithm), nameof(AimAssistAlgorithmStrings.AlgorithmHint), -1)]
        public Bindable<MosuAimAssistAlgorithm> Algorithm { get; } = new Bindable<MosuAimAssistAlgorithm>();

        [SettingSource(typeof(AimAssistAlgorithmStrings), nameof(AimAssistAlgorithmStrings.V2Power), nameof(AimAssistAlgorithmStrings.V2PowerHint), 1)]
        public BindableFloat MapleV2Power { get; } = new BindableFloat(0.6f)
        {
            MinValue = 0,
            MaxValue = 0.8f,
            Precision = 0.01f,
        };

        [SettingSource(typeof(AimAssistAlgorithmStrings), nameof(AimAssistAlgorithmStrings.Sliders), nameof(AimAssistAlgorithmStrings.SlidersHint), 2)]
        public BindableBool MapleV2AssistOnSliders { get; } = new BindableBool();

        [SettingSource(typeof(AimAssistAlgorithmStrings), nameof(AimAssistAlgorithmStrings.V3Power), nameof(AimAssistAlgorithmStrings.V3PowerHint), 3)]
        public BindableFloat MapleV3Power { get; } = new BindableFloat(1.2f)
        {
            MinValue = 0,
            MaxValue = 1.5f,
            Precision = 0.01f,
        };

        [SettingSource(typeof(AimAssistAlgorithmStrings), nameof(AimAssistAlgorithmStrings.SliderPower), nameof(AimAssistAlgorithmStrings.SliderPowerHint), 4)]
        public BindableFloat MapleV3SliderPower { get; } = new BindableFloat(0.6f)
        {
            MinValue = 0,
            MaxValue = 0.6f,
            Precision = 0.01f,
        };

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAimAssistStrength), nameof(MosuModsStrings.ModAimAssistStrengthDescription), 0,
            SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat Strength { get; } = new BindableFloat(0.5f)
        {
            Default = 0.5f,
            MinValue = 0f,
            MaxValue = 0.7f,
            Precision = 0.01f,
        };

        [SettingSource(typeof(AimAssistAlgorithmStrings), nameof(AimAssistAlgorithmStrings.ShowRealCursor), nameof(AimAssistAlgorithmStrings.ShowRealCursorHint), 5)]
        public BindableBool ShowRealCursor { get; } = new BindableBool();

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAimAssistShowDebug), nameof(MosuModsStrings.ModAimAssistShowDebugDescription))]
        public BindableBool ShowDebug { get; } = new BindableBool(false);

        private readonly BindableBool showMosu = new BindableBool(true);
        private readonly BindableBool showMapleV2 = new BindableBool();
        private readonly BindableBool showMapleV3 = new BindableBool();

        public OsuModMosuAimAssist()
        {
            Algorithm.BindValueChanged(change =>
            {
                showMosu.Value = change.NewValue == MosuAimAssistAlgorithm.Mosu;
                showMapleV2.Value = change.NewValue == MosuAimAssistAlgorithm.MapleV2;
                showMapleV3.Value = change.NewValue == MosuAimAssistAlgorithm.MapleV3;
            }, true);
        }

        public IBindable<bool>? GetSettingVisibility(string propertyName) => propertyName switch
        {
            nameof(Strength) => showMosu,
            nameof(MapleV2Power) or nameof(MapleV2AssistOnSliders) => showMapleV2,
            nameof(MapleV3Power) or nameof(MapleV3SliderPower) => showMapleV3,
            _ => null,
        };

        // Keep the established penalty for Mosu. Maple must not bypass it by leaving the hidden
        // Mosu strength at zero; scale each algorithm's power against its normal default instead.
        internal float PerformancePenaltyStrength => Algorithm.Value switch
        {
            MosuAimAssistAlgorithm.MapleV2 => Math.Clamp(MapleV2Power.Value / 0.6f * 0.5f, 0, 0.7f),
            MosuAimAssistAlgorithm.MapleV3 => Math.Clamp(MapleV3Power.Value / 1.2f * 0.5f, 0, 0.7f),
            _ => Strength.Value,
        };

        internal MosuAimAssistProfile DerivedProfile => MosuAimAssistProfile.FromStrength(Strength.Value);

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            // The actual controller is in OsuPlayfield; we just store the flag here.
            // AimAssistController will read it when the mod is active.
        }
    }
}
