// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Screens;
using osu.Game.Configuration;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Creates the solo song select screen matching the user's preferred visual style
    /// (modern lazer, restored 2024-era implementation, or skinned stable-style).
    /// </summary>
    public static class SongSelectFactory
    {
        public static IScreen CreateSoloSongSelect(ForkSongSelectStyle style)
            => style.UsesV1Screen() ? new osu.Game.Screens.SelectLegacy.PlaySongSelect() : new SoloSongSelect();

        /// <summary>
        /// Convenience overload resolving the style from config.
        /// </summary>
        public static IScreen CreateSoloSongSelect(OsuConfigManager config)
            => CreateSoloSongSelect(config.Get<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle));
    }
}
