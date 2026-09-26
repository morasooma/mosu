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
        /// MosuPp: the Mosu/Realistik RX calculator plus the MosuPp rules (see MOSUPP_CHANGELOG.md):
        /// CS PP Buff, Length Bonus, Spike Nerf, Low Accuracy Nerf, Point Variety Nerf, Aim-Focused Flow Guard,
        /// Stream-Only Guard, Simple Stream Nerf, One-Point Map Guard, Short Aim Nerf and Traceable = Hidden.
        /// </summary>
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.RelaxPpSystemMosuPp))]
        MosuPp
    }
}
