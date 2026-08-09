// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Overlays;

namespace osu.Game.Screens.Ranking.Statistics
{
    /// <summary>
    /// Wraps a <see cref="StatisticItem"/> to add a header and suitable layout for use in <see cref="ResultsScreen"/>.
    /// </summary>
    internal partial class StatisticItemContainer : CompositeDrawable
    {
        private readonly Box background;
        private IBindable<Colour4> themeColour = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;
        /// <summary>
        /// Creates a new <see cref="StatisticItemContainer"/>.
        /// </summary>
        /// <param name="item">The <see cref="StatisticItem"/> to display.</param>
        public StatisticItemContainer(StatisticItem item)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Padding = new MarginPadding(5);

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 10,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(5),
                        Children = new[]
                        {
                            LocalisableString.IsNullOrEmpty(item.Name)
                                ? Empty()
                                : new StatisticItemHeader { Text = item.Name },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding(20) { Top = 45 },
                                Child = item.CreateContent()
                            }
                        }
                    },
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateThemeColours(), true);
        }

        private void updateThemeColours()
        {
            float topGray = OverlayColourProvider.IsDarkTheme ? 0.08f : OverlayColourProvider.IsLightTheme ? 0.90f : 0.25f;
            float bottomGray = OverlayColourProvider.IsDarkTheme ? 0.05f : OverlayColourProvider.IsLightTheme ? 0.94f : 0.18f;

            background.Colour = ColourInfo.GradientVertical(
                OsuColour.Gray(topGray).Opacity(0.8f),
                OsuColour.Gray(bottomGray).Opacity(0.95f));
        }
    }
}
