// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum ForkRelaxPpSystem
    {
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxPpSystemMosuRealistik))]
        MosuRealistik,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxPpSystemLazerVanilla))]
        LazerVanilla
    }
}
