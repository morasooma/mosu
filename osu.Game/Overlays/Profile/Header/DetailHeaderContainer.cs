// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays.Profile.Header.Components;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Header
{
    public partial class DetailHeaderContainer : CompositeDrawable
    {
        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();
        private Box background = null!;
        private Box divider = null!;
        private IBindable<Colour4> themeColour = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 10 },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new MainDetails
                            {
                                RelativeSizeAxes = Axes.X,
                                User = { BindTarget = User }
                            },
                            divider = new Box
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = 2,
                                Colour = colourProvider.Background6,
                                Margin = new MarginPadding { Horizontal = 15 }
                            },
                            new ExtendedDetails
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                User = { BindTarget = User }
                            }
                        }
                    }
                }
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Background5);
            themeColour.BindValueChanged(_ =>
            {
                background.Colour = colourProvider.Background5;
                divider.Colour = colourProvider.Background6;
            }, true);
        }
    }
}
