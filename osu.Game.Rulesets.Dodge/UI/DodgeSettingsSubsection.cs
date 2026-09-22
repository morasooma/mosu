// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Dodge.Configuration;
using osu.Game.Rulesets.Dodge.Localisation;

namespace osu.Game.Rulesets.Dodge.UI
{
    public partial class DodgeSettingsSubsection : RulesetSettingsSubsection
    {
        public DodgeSettingsSubsection(DodgeRuleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (DodgeRulesetConfigManager)Config;

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.PlayfieldDim,
                    HintText = DodgeEditorStrings.PlayfieldDimHint,
                    Current = config.GetBindable<double>(DodgeRulesetSetting.PlayfieldDim),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.GrazeIndicatorBrightness,
                    HintText = DodgeEditorStrings.GrazeIndicatorBrightnessHint,
                    Current = config.GetBindable<double>(DodgeRulesetSetting.GrazeIndicatorBrightness),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = DodgeEditorStrings.MissSound,
                    HintText = DodgeEditorStrings.MissSoundHint,
                    Current = config.GetBindable<bool>(DodgeRulesetSetting.MissSoundEnabled),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = DodgeEditorStrings.MissSoundVolume,
                    HintText = DodgeEditorStrings.MissSoundVolumeHint,
                    Current = config.GetBindable<double>(DodgeRulesetSetting.MissSoundVolume),
                    KeyboardStep = 5,
                    LabelFormat = value => $"{value:N0}%",
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = DodgeEditorStrings.EffectsEnabled,
                    HintText = DodgeEditorStrings.EffectsEnabledHint,
                    Current = config.GetBindable<bool>(DodgeRulesetSetting.EffectsEnabled),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = DodgeEditorStrings.PlayerTrailEnabled,
                    HintText = DodgeEditorStrings.PlayerTrailEnabledHint,
                    Current = config.GetBindable<bool>(DodgeRulesetSetting.PlayerTrailEnabled),
                }),
            };
        }
    }
}
