// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;

namespace osu.Game.Graphics.Sprites
{
    /// <summary>
    /// Sprite text for skin layout components. Unlike <see cref="OsuSpriteText"/>, does not apply the global custom UI font override.
    /// </summary>
    public partial class SkinSpriteText : SpriteText
    {
        public SkinSpriteText()
        {
            Shadow = true;
            Font = OsuFont.Default;
        }
    }
}