// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using System.Linq;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Mods
{
    public abstract partial class ModSelectColumn : CompositeDrawable, IHasAccentColour
    {
        public readonly Container TopLevelContent;

        public LocalisableString HeaderText
        {
            set => createHeaderText(value);
        }

        public Color4 AccentColour
        {
            get => headerBackground.Colour;
            set
            {
                headerBackground.Colour = value;

                var hsv = new Colour4(value.R, value.G, value.B, 1f).ToHSV();
                var trianglesColour = Colour4.FromHSV(hsv.X, hsv.Y + 0.2f, hsv.Z - 0.1f);
                triangles.Colour = ColourInfo.GradientVertical(trianglesColour, value);
            }
        }

        /// <summary>
        /// Determines whether this column should accept user input.
        /// </summary>
        public readonly Bindable<bool> Active = new BindableBool(true);

        public string SearchTerm
        {
            set => ItemsFlow.SearchTerm = value;
        }

        protected override bool ReceivePositionalInputAtSubTree(Vector2 screenSpacePos) => base.ReceivePositionalInputAtSubTree(screenSpacePos) && Active.Value;

        protected readonly Container ControlContainer;
        protected readonly ModSearchContainer ItemsFlow;

        private readonly Container headerContainer;
        private readonly TextFlowContainer headerText;
        private readonly Box headerBackground;
        private readonly Container contentContainer;
        private readonly BackdropBlurSurface contentBackground;
        internal BackdropBlurSurface OpaqueBackground => contentBackground;
        private readonly TrianglesV2 triangles;
        private IBindable<Colour4> themeColour = null!;
        private IBindable<float>? panelRadius;

        private const float header_height = 42;

        protected const float WIDTH = 320;

        protected ModSelectColumn()
        {
            Width = WIDTH;
            RelativeSizeAxes = Axes.Y;
            Shear = OsuGame.SHEAR;

            InternalChildren = new Drawable[]
            {
                TopLevelContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    CornerRadius = ModSelectPanel.CORNER_RADIUS,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        headerContainer = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = header_height + ModSelectPanel.CORNER_RADIUS,
                            Children = new Drawable[]
                            {
                                headerBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = header_height,
                                    Children = new Drawable[]
                                    {
                                        triangles = new TrianglesV2
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Shear = -OsuGame.SHEAR,
                                            Velocity = 0.7f,
                                            ClampAxes = Axes.Y
                                        },
                                        headerText = new OsuTextFlowContainer(t =>
                                        {
                                            t.Font = OsuFont.TorusAlternate.With(size: 17);
                                            t.Shadow = false;
                                            t.Colour = Colour4.Black;
                                        })
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Shear = -OsuGame.SHEAR,
                                            Padding = new MarginPadding { Horizontal = 17 }
                                        }
                                    }
                                }
                            }
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = header_height },
                            Child = contentContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                CornerRadius = ModSelectPanel.CORNER_RADIUS,
                                BorderThickness = 3,
                                Children = new Drawable[]
                                {
                                    contentBackground = new BackdropBlurSurface
                                    {
                                        RelativeSizeAxes = Axes.Both
                                    },
                                    new GridContainer
                                    {
                                        Padding = new MarginPadding { Top = 1, Bottom = 3 },
                                        RelativeSizeAxes = Axes.Both,
                                        RowDimensions = new[]
                                        {
                                            new Dimension(GridSizeMode.AutoSize),
                                            new Dimension()
                                        },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                ControlContainer = new Container
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Padding = new MarginPadding { Horizontal = 14 }
                                                }
                                            },
                                            new Drawable[]
                                            {
                                                new OsuScrollContainer(Direction.Vertical)
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    ClampExtension = 100,
                                                    ScrollbarOverlapsContent = false,
                                                    Child = ItemsFlow = new ModSearchContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Spacing = new Vector2(0, 7),
                                                        Padding = new MarginPadding(7)
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private void createHeaderText(LocalisableString text)
        {
            headerText.Clear();

            ITextPart part = headerText.AddText(text);
            part.DrawablePartsRecreated += applySemiBoldToFirstWord;
            applySemiBoldToFirstWord(part.Drawables);

            void applySemiBoldToFirstWord(IEnumerable<Drawable> d)
            {
                if (d.FirstOrDefault() is OsuSpriteText firstWord)
                    firstWord.Font = firstWord.Font.With(weight: FontWeight.SemiBold);
            }
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            panelRadius = colourProvider.GetRadiusBindable(osu.Game.Overlays.CustomTheme.GeometryRole.Panel, ModSelectPanel.CORNER_RADIUS);
            panelRadius.BindValueChanged(r =>
            {
                float radius = r.NewValue;
                TopLevelContent.CornerRadius = radius;
                contentContainer.CornerRadius = radius;
                headerContainer.Height = header_height + System.Math.Max(ModSelectPanel.CORNER_RADIUS, radius + 2f);
            }, true);

            colourProvider.PaletteVersion.BindValueChanged(_ => updateThemeColours(colourProvider));
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateThemeColours(colourProvider), true);
        }

        private void updateThemeColours(OverlayColourProvider colourProvider)
        {
            contentContainer.BorderColour = ColourInfo.GradientVertical(colourProvider.Background4, colourProvider.Background3);

            contentBackground.SurfaceColour = colourProvider.Background4;
        }
    }
}
