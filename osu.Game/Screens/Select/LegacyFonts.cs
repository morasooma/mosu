// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Fork (ported from torii): central font source for the stable-style (legacy) song select UI.
    /// osu!stable's real font (Aller) lives in a separate resource pack; when it is available in
    /// <see cref="OsuFont"/> it can be wired back in here. For now the modern lazer font is used,
    /// which keeps the legacy chrome fully functional without shipping extra font assets.
    /// </summary>
    public static class LegacyFonts
    {
        public static FontUsage Get(float size, FontWeight weight = FontWeight.Regular)
            => OsuFont.GetFont(size: size, weight: weight);
    }
}
