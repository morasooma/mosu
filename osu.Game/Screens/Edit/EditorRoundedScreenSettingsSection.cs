// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit
{
    public abstract partial class EditorRoundedScreenSettingsSection : CompositeDrawable
    {
        private const int header_height = 50;

        protected abstract string HeaderText { get; }

        protected FillFlowContainer Flow { get; private set; } = null!;

        private OsuSpriteText headerText = null!;
        private IBindable<Colour4>? themeColour;

        internal Color4 HeaderTextColour => headerText.Colour;

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider? colourProvider)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = header_height,
                    Padding = new MarginPadding { Horizontal = 20 },
                    Child = headerText = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = HeaderText,
                        Font = new FontUsage(size: 25, weight: "bold"),
                        Colour = colourProvider?.Content1 ?? Color4.White,
                    }
                },
                new Container
                {
                    Y = header_height,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Child = Flow = new FillFlowContainer
                    {
                        Padding = new MarginPadding { Horizontal = 20 },
                        Spacing = new Vector2(10),
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                    }
                }
            };

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(c => headerText.Colour = c.NewValue, true);
            }
        }
    }
}
