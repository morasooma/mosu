// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Legacy-style text with a strong compact shadow. This deliberately uses one glyph layer:
    /// duplicating every label eight times for an outline made populated score lists GPU-bound.
    /// </summary>
    public partial class StrokedLegacyText : OsuSpriteText
    {
        public StrokedLegacyText()
        {
            Shadow = true;
            ShadowColour = new Color4(0f, 0f, 0f, 0.85f);
        }
    }
}
