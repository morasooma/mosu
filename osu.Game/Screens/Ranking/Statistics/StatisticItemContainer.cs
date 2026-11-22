// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics
{
    /// <summary>
    /// Wraps a <see cref="StatisticItem"/> to add a header and suitable layout for use in <see cref="ResultsScreen"/>.
    /// </summary>
    internal partial class StatisticItemContainer : CompositeDrawable
    {
        private readonly Box background;
        private IBindable<Colour4>? themeColour;
        private IBindable<Colour4>? accentColourBindable;
        private IBindable<ThemeMode>? themeModeBindable;
        private IBindable<CustomThemeDefinition>? customThemeBindable;
        private readonly Container mainContainer;
        private readonly Container edgeEffectContainer;
        private IBindable<float>? radiusBindable;

        [Resolved(canBeNull: true)]
        private OverlayColourProvider? colourProvider { get; set; }
        /// <summary>
        /// Creates a new <see cref="StatisticItemContainer"/>.
        /// </summary>
        /// <param name="item">The <see cref="StatisticItem"/> to display.</param>
        public StatisticItemContainer(StatisticItem item)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Padding = new MarginPadding(5);
            Width = item.FullWidth ? 1 : 0.5f;

            InternalChild = mainContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 20,
                CornerExponent = 2.5f,
                Children = new[]
                {
                    background = new Box
                    {
                        Colour = ColourInfo.GradientVertical(
                            OsuColour.Gray(0.25f).Opacity(0.8f),
                            OsuColour.Gray(0.18f).Opacity(0.95f)
                        ),
                        RelativeSizeAxes = Axes.Both,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(2),
                        Children = new[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding(15) { Top = 40 },
                                Child = item.CreateContent()
                            },
                        }
                    },
                    LocalisableString.IsNullOrEmpty(item.Name)
                        ? Empty()
                        : new StatisticItemHeader
                        {
                            Text = item.Name
                        },
                }
            };

            AddInternal(edgeEffectContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 20,
                CornerExponent = 2.5f,
                EdgeEffect = new EdgeEffectParameters
                {
                    Radius = 2,
                    Hollow = true,
                    Colour = OsuColour.Gray(0.18f),
                    Type = EdgeEffectType.Shadow,
                },
                Children = new[]
                {
                    new Box
                    {
                        Colour = Color4.Transparent,
                        RelativeSizeAxes = Axes.Both,
                    },
                }
            });
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            themeModeBindable = OverlayColourProvider.CurrentTheme.GetBoundCopy();
            themeModeBindable.BindValueChanged(_ => updateThemeColours());

            customThemeBindable = OverlayColourProvider.CustomTheme.GetBoundCopy();
            customThemeBindable.BindValueChanged(_ => updateThemeColours());

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => updateThemeColours());

                accentColourBindable = colourProvider.GetColourBindable(OverlayColour.Highlight1);
                accentColourBindable.BindValueChanged(_ => updateThemeColours());

                radiusBindable = colourProvider.GetRadiusBindable(osu.Game.Overlays.CustomTheme.GeometryRole.Card, 20f);
                radiusBindable.BindValueChanged(r =>
                {
                    mainContainer.CornerRadius = r.NewValue;
                    edgeEffectContainer.CornerRadius = r.NewValue;
                }, true);
            }

            updateThemeColours();
        }

        private void updateThemeColours()
        {
            if (OverlayColourProvider.CurrentTheme.Value == ThemeMode.Custom && colourProvider != null)
            {
                mainContainer.BorderThickness = 1.5f;
                mainContainer.BorderColour = colourProvider.Highlight1;

                background.Colour = ColourInfo.GradientVertical(
                    colourProvider.Background4.Opacity(0.85f),
                    colourProvider.Background5.Opacity(0.95f));
                return;
            }

            mainContainer.BorderThickness = 0;

            float topGray = OverlayColourProvider.IsDarkTheme ? 0.08f : OverlayColourProvider.IsLightTheme ? 0.90f : 0.25f;
            float bottomGray = OverlayColourProvider.IsDarkTheme ? 0.05f : OverlayColourProvider.IsLightTheme ? 0.94f : 0.18f;
            background.Colour = ColourInfo.GradientVertical(
                OsuColour.Gray(topGray).Opacity(0.8f),
                OsuColour.Gray(bottomGray).Opacity(0.95f));
        }
    }
}
