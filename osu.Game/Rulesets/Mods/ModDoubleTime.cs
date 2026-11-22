// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModDoubleTime : ModRateAdjust
    {
        public override string Name => "Double Time";
        public override string Acronym => "DT";
        public override IconUsage? Icon => OsuIcon.ModDoubleTime;
        public override ModType Type => ModType.DifficultyIncrease;
        public override LocalisableString Description => global::osu.Game.Localisation.ClientTranslationStrings.J4211;
        public override bool Ranked => SpeedChange.IsDefault;

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J4212), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J4213), SettingControlType = typeof(MultiplierSettingsSlider))]
        public override BindableNumber<double> SpeedChange { get; } = new BindableDouble(1.5)
        {
            MinValue = 1.01,
            MaxValue = 3,
            Precision = 0.01,
        };

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J4189), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J4190))]
        public virtual BindableBool AdjustPitch { get; } = new BindableBool();

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                foreach (var description in base.SettingDescription)
                    yield return description;

                if (!AdjustPitch.IsDefault)
                    yield return (global::osu.Game.Localisation.ClientTranslationStrings.J4189, AdjustPitch.Value ? global::osu.Game.Localisation.ClientTranslationStrings.J4183 : global::osu.Game.Localisation.ClientTranslationStrings.J4214);
            }
        }

        private readonly RateAdjustModHelper rateAdjustHelper;

        protected ModDoubleTime()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);
        }

        public override void ApplyToTrack(IAdjustableAudioComponent track)
        {
            rateAdjustHelper.ApplyToTrack(track);
        }
    }
}
