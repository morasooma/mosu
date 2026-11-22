// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings.Sections.DebugSettings
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => global::osu.Game.Localisation.ClientTranslationStrings.J4027;

        [BackgroundDependencyLoader]
        private void load(FrameworkDebugConfigManager config, FrameworkConfigManager frameworkConfig)
        {
            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = global::osu.Game.Localisation.ClientTranslationStrings.J4028,
                Current = frameworkConfig.GetBindable<bool>(FrameworkSetting.ShowLogOverlay)
            })
            {
                FavouriteId = "FrameworkSetting.ShowLogOverlay",
            });

            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = global::osu.Game.Localisation.ClientTranslationStrings.J4029,
                Current = config.GetBindable<bool>(DebugSetting.BypassFrontToBackPass)
            })
            {
                FavouriteId = "DebugSetting.BypassFrontToBackPass",
            });
        }
    }
}
