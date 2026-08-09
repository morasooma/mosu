// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.Graphics.UserInterface
{
    public partial class ShearedSearchTextBox : CompositeDrawable, IHasCurrentValue<string>
    {
        private const float corner_radius = 7;

        private readonly Box background;
        private readonly SpriteIcon searchIcon;
        private IBindable<Colour4> themeColour = null!;
        private OverlayColourProvider colourProvider = null!;
        protected readonly InnerSearchTextBox TextBox;

        public Bindable<string> Current
        {
            get => TextBox.Current;
            set => TextBox.Current = value;
        }

        public bool HoldFocus
        {
            get => TextBox.HoldFocus;
            set => TextBox.HoldFocus = value;
        }

        public LocalisableString PlaceholderText
        {
            get => TextBox.PlaceholderText;
            set => TextBox.PlaceholderText = value;
        }

        public new bool HasFocus => TextBox.HasFocus;

        public void TakeFocus() => TextBox.TakeFocus();

        public void KillFocus() => TextBox.KillFocus();

        public bool SelectAll() => TextBox.SelectAll();

        public ShearedSearchTextBox()
        {
            Height = 42;
            Shear = OsuGame.SHEAR;
            Masking = true;
            CornerRadius = corner_radius;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            TextBox = CreateInnerTextBox(),
                            searchIcon = new SpriteIcon
                            {
                                Icon = FontAwesome.Solid.Search,
                                Origin = Anchor.Centre,
                                Anchor = Anchor.Centre,
                                Size = new Vector2(16),
                                Shear = -Shear
                            }
                        }
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 50),
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;
            themeColour = colourProvider.GetColourBindable(OverlayColour.Background3);
            themeColour.BindValueChanged(_ =>
            {
                background.Colour = this.colourProvider.Background3;
                searchIcon.Colour = this.colourProvider.Content1;
            }, true);
        }

        public override bool HandleNonPositionalInput => TextBox.HandleNonPositionalInput;

        protected virtual InnerSearchTextBox CreateInnerTextBox() => new InnerSearchTextBox();

        protected partial class InnerSearchTextBox : SearchTextBox
        {
            private OverlayColourProvider colourProvider = null!;
            private IBindable<Colour4> themeColour = null!;
            public InnerSearchTextBox()
            {
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;
                RelativeSizeAxes = Axes.Both;
                Size = Vector2.One;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                this.colourProvider = colourProvider;
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => updateThemeColours(), true);

                Placeholder.Font = OsuFont.GetFont(size: FontSize, weight: FontWeight.SemiBold);
                PlaceholderText = CommonStrings.InputSearch;

                CornerRadius = corner_radius;
                TextContainer.Shear = -OsuGame.SHEAR;
            }

            private void updateThemeColours()
            {
                BackgroundFocused = colourProvider.Background4;
                BackgroundUnfocused = colourProvider.Background4;
                Placeholder.Colour = colourProvider.Foreground1;
                TextFlow.Colour = colourProvider.Content1;
                SetCaretColour(TextFlow.Colour);
            }

            protected override SpriteText CreatePlaceholder() => new SearchPlaceholder();

            internal partial class SearchPlaceholder : SpriteText
            {
                public override void Show()
                {
                    this
                        .MoveToY(0, 250, Easing.OutQuint)
                        .FadeIn(250, Easing.OutQuint);
                }

                public override void Hide()
                {
                    this
                        .MoveToY(3, 250, Easing.OutQuint)
                        .FadeOut(250, Easing.OutQuint);
                }
            }

            protected override Drawable GetDrawableCharacter(char c) => new FallingDownContainer
            {
                AutoSizeAxes = Axes.Both,
                Child = new OsuSpriteText { Text = c.ToString(), Font = OsuFont.GetFont(size: 20, weight: FontWeight.SemiBold) },
            };
        }
    }
}
