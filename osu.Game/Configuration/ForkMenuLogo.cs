// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum ForkMenuLogo
    {
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.MenuLogoMora))]
        Mora,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.MenuLogoCharacter))]
        Character,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.MenuLogoRandom))]
        Random,
    }

    public static class ForkMenuLogoExtensions
    {
        public static string GetTextureName(this ForkMenuLogo logo) => logo switch
        {
            ForkMenuLogo.Character => @"Menu/morasooma-logo",
            _ => @"Menu/mora-logo",
        };
    }
}
