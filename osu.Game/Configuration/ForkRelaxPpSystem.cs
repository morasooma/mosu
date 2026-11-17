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
        LazerVanilla,

        /// <summary>
        /// Temporary 2.0.0 prerelease: the original MosuPp 2 laboratory model.
        /// The approved 2.0.0 calculator remains in Relax/MosuPpRelax and on its release branch.
        /// </summary>
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxPpSystemMosuPp))]
        MosuPp
    }
}
