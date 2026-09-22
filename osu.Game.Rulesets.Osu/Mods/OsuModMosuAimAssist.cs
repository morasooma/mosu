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
    public class OsuModMosuAimAssist : Mod, IApplicableToDrawableRuleset<OsuHitObject>
    {
        public override string Name => "Aim Assist";

        public override string Acronym => "AA";

        public override ModType Type => ModType.Mosu;

        public override LocalisableString Description => MosuModsStrings.ModAimAssistDescription;


        public override bool HasImplementation => true;

        public override bool Ranked => true;

        public override bool ValidForFreestyleAsRequiredMod => true;

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

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAimAssistShowDebug), nameof(MosuModsStrings.ModAimAssistShowDebugDescription))]
        public BindableBool ShowDebug { get; } = new BindableBool(false);

        internal MosuAimAssistProfile DerivedProfile => MosuAimAssistProfile.FromStrength(Strength.Value);

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            // The actual controller is in OsuPlayfield; we just store the flag here.
            // AimAssistController will read it when the mod is active.
        }
    }
}
