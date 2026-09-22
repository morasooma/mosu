// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterface
{
    public partial class OsuDropdown<T> : Dropdown<T>, IKeyBindingHandler<GlobalAction>
    {
        private const float corner_radius = 5;

        protected override DropdownHeader CreateHeader() => new OsuDropdownHeader();

        protected override DropdownMenu CreateMenu() => new OsuDropdownMenu();

        public OsuDropdown()
        {
            if (Header is OsuDropdownHeader osuHeader)
                osuHeader.Dropdown = this;
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat) return false;

            if (e.Action == GlobalAction.Back)
                return Back();

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        #region OsuDropdownMenu

        public partial class OsuDropdownMenu : DropdownMenu, IKeyBindingHandler<GlobalAction>
        {
            public override bool HandleNonPositionalInput => State == MenuState.Open;

            private Sample? sampleOpen;
            private Sample? sampleClose;
            private IBindable<Colour4>? themeColour;
            private IBindable<ThemeMode>? themeMode;
            private readonly BackdropBlurSurface glassBackground;

            public new Color4 BackgroundColour
            {
                get => glassBackground.SurfaceColour;
                set => glassBackground.SurfaceColour = value;
            }

            // todo: this uses the same styling as OsuMenu. hopefully we can just use OsuMenu in the future with some refactoring
            public OsuDropdownMenu()
            {
                base.BackgroundColour = Color4.Transparent;
                MaskingContainer.Add(glassBackground = new BackdropBlurSurface
                {
                    Depth = float.MaxValue,
                    SurfaceColour = Color4.Black,
                });

                CornerRadius = corner_radius;

                MaskingContainer.CornerRadius = corner_radius;
                Alpha = 0;

                // todo: this uses the same styling as OsuMenu. hopefully we can just use OsuMenu in the future with some refactoring
                ItemsContainer.Padding = new MarginPadding(5);
            }

            [BackgroundDependencyLoader(true)]
            private void load(OverlayColourProvider? colourProvider, OsuColour colours, AudioManager audio)
            {
                if (colourProvider != null)
                {
                    themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                    themeColour.BindValueChanged(_ => applyMenuColours(colourProvider, colours), true);
                }
                else
                {
                    themeMode = OverlayColourProvider.CurrentTheme.GetBoundCopy();
                    themeMode.BindValueChanged(_ => applyMenuColours(null, colours), true);
                }

                sampleOpen = audio.Samples.Get(@"UI/dropdown-open");
                sampleClose = audio.Samples.Get(@"UI/dropdown-close");
            }

            private void applyMenuColours(OverlayColourProvider? colourProvider, OsuColour colours)
            {
                if (colourProvider == null)
                {
                    BackgroundColour = Color4.Black;
                    HoverColour = colours.PinkDarker;
                    SelectionColour = colours.PinkDarker.Opacity(0.5f);
                    return;
                }

                if (OverlayColourProvider.IsLightTheme)
                {
                    BackgroundColour = colourProvider.Background4;
                    HoverColour = colourProvider.Background3.Darken(0.08f);
                    SelectionColour = colourProvider.Highlight1.Opacity(0.2f);
                }
                else
                {
                    BackgroundColour = colourProvider.Background5;
                    HoverColour = colourProvider.Light4;
                    SelectionColour = colourProvider.Background3;
                }
            }

            // todo: this shouldn't be required after https://github.com/ppy/osu-framework/issues/4519 is fixed.
            private bool wasOpened;

            // todo: this uses the same styling as OsuMenu. hopefully we can just use OsuMenu in the future with some refactoring
            protected override void AnimateOpen()
            {
                wasOpened = true;
                this.FadeIn(300, Easing.OutQuint);
                sampleOpen?.Play();
            }

            protected override void AnimateClose()
            {
                if (wasOpened)
                {
                    this.FadeOut(300, Easing.OutQuint);
                    sampleClose?.Play();
                }
            }

            private Vector2? targetSize;

            // todo: this uses the same styling as OsuMenu. hopefully we can just use OsuMenu in the future with some refactoring
            protected override void UpdateSize(Vector2 newSize)
            {
                // TODO: should probably fix this at a framework level (this method is running every frame which can spam transforms)
                if (newSize == targetSize)
                    return;

                targetSize = newSize;

                if (Direction == Direction.Vertical)
                {
                    Width = newSize.X;
                    this.ResizeHeightTo(newSize.Y, 300, Easing.OutQuint);
                }
                else
                {
                    Height = newSize.Y;
                    this.ResizeWidthTo(newSize.X, 300, Easing.OutQuint);
                }
            }

            private Color4 hoverColour;

            public Color4 HoverColour
            {
                get => hoverColour;
                set
                {
                    hoverColour = value;
                    foreach (var c in ItemsContainer.OfType<DrawableOsuDropdownMenuItem>())
                        c.BackgroundColourHover = value;
                }
            }

            private Color4 selectionColour;

            public Color4 SelectionColour
            {
                get => selectionColour;
                set
                {
                    selectionColour = value;
                    foreach (var c in ItemsContainer.OfType<DrawableOsuDropdownMenuItem>())
                        c.BackgroundColourSelected = value;
                }
            }

            protected override Menu CreateSubMenu() => new OsuMenu(Direction.Vertical);

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new DrawableOsuDropdownMenuItem(item)
            {
                BackgroundColourHover = HoverColour,
                BackgroundColourSelected = SelectionColour
            };

            protected override ScrollContainer<Drawable> CreateScrollContainer(Direction direction) => new OsuScrollContainer(direction);

            public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
            {
                // logic copied from https://github.com/ppy/osu-framework/blob/baf865f1fd9e677310e7e432a7c6af99db7db914/osu.Framework/Graphics/UserInterface/Dropdown.cs#L702-L717
                var visibleMenuItemsList = VisibleMenuItems.ToList();

                if (visibleMenuItemsList.Count > 0)
                {
                    var currentPreselected = PreselectedItem;
                    int targetPreselectionIndex = visibleMenuItemsList.IndexOf(currentPreselected);

                    switch (e.Action)
                    {
                        case GlobalAction.SelectPrevious:
                            PreselectItem(targetPreselectionIndex - 1);
                            return true;

                        case GlobalAction.SelectNext:
                            PreselectItem(targetPreselectionIndex + 1);
                            return true;
                    }
                }

                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
            {
            }

            #region DrawableOsuDropdownMenuItem

            public partial class DrawableOsuDropdownMenuItem : DrawableDropdownMenuItem
            {
                // IsHovered is used
                public override bool HandlePositionalInput => true;

                public new Color4 BackgroundColourHover
                {
                    get => base.BackgroundColourHover;
                    set
                    {
                        base.BackgroundColourHover = value;
                        updateColours();
                    }
                }

                public new Color4 BackgroundColourSelected
                {
                    get => base.BackgroundColourSelected;
                    set
                    {
                        base.BackgroundColourSelected = value;
                        updateColours();
                    }
                }

                private void updateColours()
                {
                    BackgroundColour = BackgroundColourHover.Opacity(0);

                    UpdateBackgroundColour();
                    UpdateForegroundColour();
                }

                private IBindable<Colour4>? themeColour;

                public DrawableOsuDropdownMenuItem(MenuItem item)
                    : base(item)
                {
                    Foreground.Padding = new MarginPadding(2);
                    Foreground.AutoSizeAxes = Axes.Y;
                    Foreground.RelativeSizeAxes = Axes.X;

                    Masking = true;
                    CornerRadius = corner_radius;
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider? colourProvider)
                {
                    if (colourProvider != null)
                    {
                        themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                        themeColour.BindValueChanged(_ =>
                        {
                            ForegroundColour = colourProvider.Content1;
                            ForegroundColourHover = colourProvider.Content1;
                            ForegroundColourSelected = colourProvider.Content1;
                        }, true);
                    }

                    AddInternal(new HoverSounds());
                }

                protected override void UpdateBackgroundColour()
                {
                    double duration = OverlayColourProvider.ThemeTransitionDuration(100);
                    Background.FadeColour(IsPreSelected ? BackgroundColourHover : BackgroundColourSelected, duration, Easing.OutQuint);

                    if (IsPreSelected || IsSelected)
                        Background.FadeIn(duration, Easing.OutQuint);
                    else
                        Background.FadeOut(OverlayColourProvider.ThemeTransitionDuration(600), Easing.OutQuint);
                }

                protected override void UpdateForegroundColour()
                {
                    base.UpdateForegroundColour();

                    if (Foreground.Children.FirstOrDefault() is Content content)
                        content.Hovering = IsHovered;
                }

                protected override Drawable CreateContent() => new Content();

                protected new partial class Content : CompositeDrawable, IHasText
                {
                    public LocalisableString Text
                    {
                        get => Label.Text;
                        set => Label.Text = value;
                    }

                    public readonly OsuSpriteText Label;
                    public readonly SpriteIcon Chevron;

                    private const float chevron_offset = -3;
                    private IBindable<Colour4>? themeColour;

                    public Content()
                    {
                        RelativeSizeAxes = Axes.X;
                        AutoSizeAxes = Axes.Y;

                        InternalChildren = new Drawable[]
                        {
                            Chevron = new SpriteIcon
                            {
                                Icon = FontAwesome.Solid.ChevronRight,
                                Size = new Vector2(8),
                                Alpha = 0,
                                X = chevron_offset,
                                Y = 1,
                                Margin = new MarginPadding { Left = 3, Right = 3 },
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                            },
                            Label = new TruncatingSpriteText
                            {
                                Padding = new MarginPadding { Left = 15 },
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                            },
                        };
                    }

                    [BackgroundDependencyLoader(true)]
                    private void load(OverlayColourProvider? colourProvider)
                    {
                        if (colourProvider != null)
                        {
                            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                            themeColour.BindValueChanged(colour => Label.Colour = Chevron.Colour = colour.NewValue, true);
                        }
                        else
                            Chevron.Colour = Color4.Black;
                    }

                    private bool hovering;

                    public bool Hovering
                    {
                        get => hovering;
                        set
                        {
                            if (value == hovering)
                                return;

                            hovering = value;

                            if (hovering)
                            {
                                Chevron.FadeIn(400, Easing.OutQuint);
                                Chevron.MoveToX(0, 400, Easing.OutQuint);
                            }
                            else
                            {
                                Chevron.FadeOut(200);
                                Chevron.MoveToX(chevron_offset, 200, Easing.In);
                            }
                        }
                    }
                }
            }

            #endregion
        }

        #endregion

        public partial class OsuDropdownHeader : DropdownHeader
        {
            protected readonly SpriteText Text;

            protected override LocalisableString Label
            {
                get => Text.Text;
                set => Text.Text = value;
            }

            protected readonly SpriteIcon Chevron;

            public OsuDropdown<T>? Dropdown { get; set; }

            public OsuDropdownHeader()
            {
                Foreground.Padding = new MarginPadding(10);

                AutoSizeAxes = Axes.None;
                Margin = new MarginPadding { Bottom = 4 };
                CornerRadius = corner_radius;
                Height = 40;

                Foreground.Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            Text = new TruncatingSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                            },
                            Chevron = new SpriteIcon
                            {
                                Icon = FontAwesome.Solid.ChevronDown,
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Size = new Vector2(10),
                                Margin = new MarginPadding { Right = 2 },
                            },
                        }
                    }
                };

                AddInternal(new HoverClickSounds());
            }

            [Resolved]
            private OverlayColourProvider? colourProvider { get; set; }

            private IBindable<Colour4>? themeColour;
            private IBindable<ThemeMode>? themeMode;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (colourProvider != null)
                {
                    themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                    themeColour.BindValueChanged(_ => updateColour(), true);
                }
                else
                {
                    themeMode = OverlayColourProvider.CurrentTheme.GetBoundCopy();
                    themeMode.BindValueChanged(_ => updateColour(), true);
                }

                if (Dropdown != null)
                    Dropdown.Menu.StateChanged += _ => updateChevron();

                SearchBar.State.ValueChanged += _ => updateColour();
                Enabled.BindValueChanged(_ => updateColour());
                updateColour();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateColour();
                return false;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateColour();
            }

            private void updateColour()
            {
                bool hovered = Enabled.Value && IsHovered;
                bool light = OverlayColourProvider.IsLightTheme;

                var hoveredColour = light
                    ? colourProvider?.Background3.Darken(0.05f) ?? colours.PinkDarker
                    : colourProvider?.Light4 ?? colours.PinkDarker;
                var unhoveredColour = light
                    ? colourProvider?.Background1 ?? Color4.Black
                    : colourProvider?.Background5 ?? Color4.Black;
                var backgroundColour = SearchBar.State.Value == Visibility.Visible || !hovered ? unhoveredColour : hoveredColour;

                var textColour = colourProvider?.Content1 ?? Color4.White;
                Alpha = Enabled.Value ? 1 : 0.3f;

                Text.Colour = textColour;
                Chevron.Colour = textColour;

                if (Background.Child is Box backgroundBox)
                {
                    backgroundBox.FadeColour(backgroundColour, OverlayColourProvider.ThemeTransitionDuration(150), Easing.OutQuint);
                    Background.Colour = Color4.White;
                }

                BackgroundColour = unhoveredColour;
                BackgroundColourHover = hoveredColour;
            }

            private void updateChevron()
            {
                Debug.Assert(Dropdown != null);
                bool open = Dropdown.Menu.State == MenuState.Open;
                Chevron.ScaleTo(open ? new Vector2(1f, -1f) : Vector2.One, 300, Easing.OutQuint);
            }

            protected override DropdownSearchBar CreateSearchBar() => new OsuDropdownSearchBar
            {
                Padding = new MarginPadding { Right = 26 },
            };

            private partial class OsuDropdownSearchBar : DropdownSearchBar
            {
                protected override void PopIn() => this.FadeIn();

                protected override void PopOut() => this.FadeOut();

                protected override TextBox CreateTextBox() => new DropdownSearchTextBox
                {
                    FontSize = OsuFont.Default.Size,
                };

                private partial class DropdownSearchTextBox : OsuTextBox
                {
                    private IBindable<Colour4>? themeColour;

                    public DropdownSearchTextBox()
                    {
                        PlaceholderText = HomeStrings.SearchPlaceholder;
                    }

                    [BackgroundDependencyLoader]
                    private void load(OverlayColourProvider? colourProvider)
                    {
                        if (colourProvider == null)
                        {
                            BackgroundUnfocused = new Color4(10, 10, 10, 255);
                            BackgroundFocused = new Color4(10, 10, 10, 255);
                            return;
                        }

                        themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                        themeColour.BindValueChanged(_ =>
                        {
                            var background = OverlayColourProvider.IsLightTheme ? colourProvider.Background4 : colourProvider.Background5;
                            BackgroundUnfocused = background;
                            BackgroundFocused = background;
                            Colour = colourProvider.Content1;
                        }, true);
                    }

                    protected override void OnFocus(FocusEvent e)
                    {
                        base.OnFocus(e);
                        BorderThickness = 0;
                    }
                }
            }
        }
    }
}
