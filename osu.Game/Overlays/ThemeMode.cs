// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Overlays
{
    public enum ThemeMode
    {
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.ThemeModeDefault))]
        Default,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.ThemeModeLight))]
        Light,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.ThemeModeDark))]
        Dark,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.ThemeModeCustom))]
        Custom,
    }

    internal static class ThemeModeResolver
    {
        public static ThemeMode Resolve(ThemeMode configuredTheme, bool forceLightTheme, bool isThirdPartyServer) =>
            forceLightTheme && !isThirdPartyServer ? ThemeMode.Light : configuredTheme;
    }
}
