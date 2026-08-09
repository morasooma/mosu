// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public partial class NumberPiece : DirectChildrenLifeOptimisingContainer
    {
        private readonly SkinnableSpriteText number;

        public string Text
        {
            get => number.Text.ToString();
            set => number.Text = value;
        }

        public NumberPiece()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            number = new SkinnableSpriteText(new OsuSkinComponentLookup(OsuSkinComponents.HitCircleText), _ => new OsuSpriteText
            {
                Font = OsuFont.Numeric.With(size: 40),
                UseFullGlyphHeight = false,
            }, confineMode: ConfineMode.NoScaling)
            {
                Text = @"1"
            };

            if (SkinPerformanceMode.Enabled)
            {
                // The glow container has no visual content of its own, but its edge effect
                // forces a buffered draw path. The number remains pixel-identical.
                Child = number;
            }
            else
            {
                Children = new Drawable[]
                {
                    new Container
                    {
                        Masking = true,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Glow,
                            Radius = 60,
                            Colour = Color4.White.Opacity(0.5f),
                        },
                    },
                    number
                };
            }
        }
    }
}
