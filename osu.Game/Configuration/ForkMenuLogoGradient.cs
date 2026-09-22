// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum ForkMenuLogoGradient
    {
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.MenuLogoGradientClassic))]
        Classic,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.MenuLogoGradientWeb))]
        Web,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.MenuLogoGradientSunset))]
        Sunset,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.MenuLogoGradientOcean))]
        Ocean,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.MenuLogoGradientAurora))]
        Aurora,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.MenuLogoRandom))]
        Random,
    }
}
