// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Overlays
{
    public enum ThemeMode
    {
        Default,
        Light,
        Dark,
    }

    internal static class ThemeModeResolver
    {
        public static ThemeMode Resolve(ThemeMode configuredTheme, bool forceLightTheme, bool isThirdPartyServer) =>
            forceLightTheme && !isThirdPartyServer ? ThemeMode.Light : configuredTheme;
    }
}
