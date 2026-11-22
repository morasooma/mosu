// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Screens.OnlinePlay.Lounge.Components
{
    public enum RoomModeFilter
    {
        Open,

        [Description("Recently Ended"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J4801))]
        Ended,
        Participated,
        Owned,
    }
}
