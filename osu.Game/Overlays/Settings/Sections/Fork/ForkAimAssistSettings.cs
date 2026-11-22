// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkAimAssistSettings : SettingsSubsection
    {
        protected override LocalisableString Header => ForkSettingsStrings.AimAssistHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.AimAssistEnableCaption,
                    HintText = ForkSettingsStrings.AimAssistEnableHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkAimAssistEnabled)
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistEnabled",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.AimAssistShowTargetsCaption,
                    HintText = ForkSettingsStrings.AimAssistShowTargetsHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkAimAssistShowTargets)
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistShowTargets",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.AimAssistShowFlowCaption,
                    HintText = ForkSettingsStrings.AimAssistShowFlowHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkAimAssistShowFlowDebug)
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistShowFlowDebug",
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.AimAssistStrengthCaption,
                    HintText = ForkSettingsStrings.AimAssistStrengthHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkAimAssistStrength),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistStrength",
                },
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = ForkSettingsStrings.AimAssistFovCaption,
                    HintText = ForkSettingsStrings.AimAssistFovHint,
                    Current = config.GetBindable<float>(OsuSetting.ForkAimAssistFovRadius),
                    KeyboardStep = 1f,
                    LabelFormat = value => global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4038(value)
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistFovRadius",
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.AimAssistIntentCaption,
                    HintText = ForkSettingsStrings.AimAssistIntentHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkAimAssistIntentThreshold),
                    KeyboardStep = 0.05f,
                    LabelFormat = value => $"{value:0.##}"
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistIntentThreshold",
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.AimAssistFrictionCaption,
                    HintText = ForkSettingsStrings.AimAssistFrictionHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkAimAssistDynamicFriction),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistDynamicFriction",
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.AimAssistJitterCaption,
                    HintText = ForkSettingsStrings.AimAssistJitterHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkAimAssistAntiJitterMs),
                    KeyboardStep = 1f,
                    LabelFormat = value => global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4039(value)
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistAntiJitterMs",
                },
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = ForkSettingsStrings.AimAssistOvershootCaption,
                    HintText = ForkSettingsStrings.AimAssistOvershootHint,
                    Current = config.GetBindable<float>(OsuSetting.ForkAimAssistOvershootAllowance),
                    KeyboardStep = 1f,
                    LabelFormat = value => global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4038(value)
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistOvershootAllowance",
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.AimAssistCenterCaption,
                    HintText = ForkSettingsStrings.AimAssistCenterHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkAimAssistCenterBias),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                })
                {
                    FavouriteId = "OsuSetting.ForkAimAssistCenterBias",
                },
            };
        }
    }
}
