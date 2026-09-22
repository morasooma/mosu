// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Online
{
    public enum MosuConnectionRoute
    {
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.ConnectionRouteDirect))]
        Direct,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.ConnectionRouteProxy1))]
        Proxy1,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.ConnectionRouteProxy2))]
        Proxy2,
    }
}
