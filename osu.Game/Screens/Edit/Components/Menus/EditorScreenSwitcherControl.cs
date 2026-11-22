// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Components.Menus
{
    public partial class EditorScreenSwitcherControl : OsuTabControl<EditorScreenMode>
    {
        public EditorScreenSwitcherControl()
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;

            TabContainer.RelativeSizeAxes &= ~Axes.X;
            TabContainer.AutoSizeAxes = Axes.X;
            TabContainer.Spacing = Vector2.Zero;
        }

        private Box internalBackground = null!;
        private IBindable<Colour4>? accentColourBindable;
        private IBindable<Colour4>? bgColourBindable;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            AddInternal(internalBackground = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colourProvider.Background2,
            });

            accentColourBindable = colourProvider.GetColourBindable(OverlayColour.Light3);
            accentColourBindable.BindValueChanged(c => AccentColour = c.NewValue, true);

            bgColourBindable = colourProvider.GetColourBindable(OverlayColour.Background2);
            bgColourBindable.BindValueChanged(c => internalBackground.Colour = c.NewValue, true);
            colourProvider.PaletteVersion.BindValueChanged(_ => internalBackground.Colour = colourProvider.Background2);
        }

        protected override Dropdown<EditorScreenMode> CreateDropdown() => null!;

        protected override TabItem<EditorScreenMode> CreateTabItem(EditorScreenMode value) => new TabItem(value);

        private partial class TabItem : OsuTabItem
        {
            private readonly Box background;
            private Color4 backgroundIdleColour;
            private Color4 backgroundHoverColour;
            private IBindable<Colour4>? idleColourBindable;
            private IBindable<Colour4>? hoverColourBindable;

            public TabItem(EditorScreenMode value)
                : base(value)
            {
                Text.Margin = new MarginPadding(10);
                Text.Anchor = Anchor.CentreLeft;
                Text.Origin = Anchor.CentreLeft;

                Text.Font = OsuFont.TorusAlternate;

                Add(background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = float.MaxValue,
                });

                Bar.Expire();
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                idleColourBindable = colourProvider.GetColourBindable(OverlayColour.Background2);
                idleColourBindable.BindValueChanged(c =>
                {
                    backgroundIdleColour = c.NewValue;
                    if (!IsHovered)
                        background.Colour = c.NewValue;
                }, true);

                hoverColourBindable = colourProvider.GetColourBindable(OverlayColour.Background1);
                hoverColourBindable.BindValueChanged(c =>
                {
                    backgroundHoverColour = c.NewValue;
                    if (IsHovered)
                        background.Colour = c.NewValue;
                }, true);

                colourProvider.PaletteVersion.BindValueChanged(_ =>
                {
                    backgroundIdleColour = colourProvider.Background2;
                    backgroundHoverColour = colourProvider.Background1;
                    background.Colour = IsHovered ? backgroundHoverColour : backgroundIdleColour;
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                background.Colour = backgroundIdleColour;
            }

            protected override void FadeHovered()
            {
                base.FadeHovered();
                background.FadeColour(backgroundHoverColour, TRANSITION_LENGTH, Easing.OutQuint);
            }

            protected override void FadeUnhovered()
            {
                base.FadeUnhovered();
                background.FadeColour(backgroundIdleColour, TRANSITION_LENGTH, Easing.OutQuint);
            }
        }
    }
}
