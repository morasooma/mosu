// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;

namespace osu.Game.Graphics.UserInterface.PageSelector
{
    internal partial class PageEllipsis : CompositeDrawable
    {
        private IBindable<Colour4>? themeColour;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;

            OsuSpriteText text;

            InternalChildren = new Drawable[]
            {
                text = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                    Text = "...",
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                }
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Light3);
            themeColour.BindValueChanged(c => text.Colour = c.NewValue, true);
        }
    }
}
