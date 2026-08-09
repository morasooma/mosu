using System;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osuTK;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Противодействует любой форме автоматической помощи в прицеливании:
    /// добавляет случайное смещение курсора, инвертирует поправки aim-assist
    /// и нарушает предсказуемость траектории движения.
    /// </summary>
    public class OsuModMosuAntiAimAssist : Mod, IApplicableToDrawableRuleset<OsuHitObject>
    {
        public override string Name => "Anti Aim Assist";

        public override string Acronym => "AA!";

        public override ModType Type => ModType.Mosu;

        public override LocalisableString Description => MosuModsStrings.ModAntiAimAssistDescription;

        public override double ScoreMultiplier => 1.0;

        public override bool HasImplementation => true;

        public override bool Ranked => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(OsuModMosuAimAssist),
            typeof(OsuModAutopilot),
            typeof(ModAutoplay),
            typeof(OsuModRelax),
        };

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAntiAimAssistIntensity), nameof(MosuModsStrings.ModAntiAimAssistIntensityDescription), 0,
            SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat Intensity { get; } = new BindableFloat(0.7f)
        {
            Default = 0.7f,
            MinValue = 0f,
            MaxValue = 1f,
            Precision = 0.01f,
        };

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAntiAimAssistRandomDrift), nameof(MosuModsStrings.ModAntiAimAssistRandomDriftDescription), 1,
            SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat RandomDrift { get; } = new BindableFloat(0.6f)
        {
            Default = 0.6f,
            MinValue = 0f,
            MaxValue = 1f,
            Precision = 0.01f,
        };

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            var osuPlayfield = drawableRuleset.Playfield as OsuPlayfield;
            if (osuPlayfield == null)
                return;

            osuPlayfield.AimAssistController.ApplyAntiAssist(this);
        }
    }
}
