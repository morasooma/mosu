// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class HudChip : CompositeDrawable
    {
        private readonly OsuSpriteText label;

        public HudChip(OsuColour colours, IconUsage icon, string text, Color4 accent)
        {
            Height = 38;
            Masking = true;
            CornerRadius = 9;
            BorderThickness = 2;
            BorderColour = colours.Gray4;
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1.Opacity(0.94f) },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(9, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = icon,
                            Size = new Vector2(17),
                            Colour = accent,
                        },
                        label = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Font = OsuFont.Default.With(size: 15, weight: FontWeight.Bold),
                        },
                    },
                },
            };
        }

        public void SetText(string text) => label.Text = text;

        public string Text => label.Text.ToString();
    }
}
