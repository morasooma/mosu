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
    public partial class ForkRelaxSettings : SettingsSubsection
    {
        protected override LocalisableString Header => ForkSettingsStrings.RelaxHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<ForkRelaxPpSystem>
                {
                    Caption = ForkSettingsStrings.RelaxPpSystemCaption,
                    HintText = ForkSettingsStrings.RelaxPpSystemHint,
                    Current = config.GetBindable<ForkRelaxPpSystem>(OsuSetting.ForkRelaxPpSystem)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.RelaxEnableCaption,
                    HintText = ForkSettingsStrings.RelaxEnableHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkRelaxEnabled)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.RelaxBlindTapEnabledCaption,
                    HintText = ForkSettingsStrings.RelaxBlindTapEnabledHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkRelaxBlindTapEnabled)
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.RelaxBaseOffsetCaption,
                    HintText = ForkSettingsStrings.RelaxBaseOffsetHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkRelaxBaseOffset),
                    KeyboardStep = 1f,
                    LabelFormat = value => $"{value:0} ms"
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.RelaxVarianceCaption,
                    HintText = ForkSettingsStrings.RelaxVarianceHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkRelaxTimingVariance),
                    KeyboardStep = 1f,
                    LabelFormat = value => $"{value:0} ms"
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.RelaxHoldTimeCaption,
                    HintText = ForkSettingsStrings.RelaxHoldTimeHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkRelaxHoldTime),
                    KeyboardStep = 1f,
                    LabelFormat = value => $"{value:0} ms"
                }),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = ForkSettingsStrings.RelaxSyncRadiusCaption,
                    HintText = ForkSettingsStrings.RelaxSyncRadiusHint,
                    Current = config.GetBindable<float>(OsuSetting.ForkRelaxSyncRadius),
                    KeyboardStep = 1f,
                    LabelFormat = value => $"{value:0}px"
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.RelaxMaxSyncDelayCaption,
                    HintText = ForkSettingsStrings.RelaxMaxSyncDelayHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkRelaxMaxSyncDelay),
                    KeyboardStep = 1f,
                    LabelFormat = value => $"{value:0} ms"
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.RelaxStableBpmCaption,
                    HintText = ForkSettingsStrings.RelaxStableBpmHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkRelaxStableBpm),
                    KeyboardStep = 1f,
                    LabelFormat = value => $"{value:0} BPM"
                }),
            };
        }
    }
}
