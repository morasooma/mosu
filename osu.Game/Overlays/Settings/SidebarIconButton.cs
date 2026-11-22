// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.Containers;

namespace osu.Game.Overlays.Settings
{
    public partial class SidebarIconButton : SidebarButton
    {
        private const float selection_indicator_height_active = 18;
        private const float selection_indicator_height_inactive = 4;

        private CircularContainer selectionIndicator;
        private Container textIconContent;
        private OsuSpriteText labelText;

        protected void RefreshLabel()
        {
            if (labelText != null)
                labelText.Text = Label;
        }

        // always consider as part of flow, even when not visible (for the sake of the initial animation).
        public override bool IsPresent => true;

        public required SettingsSection Section { get; init; }

        protected virtual bool UseSmallerButton => Section.UseSmallerSidebarButton;
        protected virtual LocalisableString Label => Section.Header;
        protected virtual Drawable CreateIcon() => Section.CreateIcon();

        private bool selected;

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;

                if (IsLoaded)
                    UpdateState();
            }
        }

        public SidebarIconButton()
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            const float icon_size = 20;

            float scale = UseSmallerButton ? 0.8f : 1;

            Height = 46 * scale;
            Padding = new MarginPadding { Horizontal = 5 + (UseSmallerButton ? icon_size : 0), Vertical = 2.5f * scale };

            AddRange(new Drawable[]
            {
                textIconContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(0.6f),
                    Children = new Drawable[]
                    {
                        new ConstrainedIconContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.Centre,
                            X = icon_size,
                            Size = new Vector2(icon_size * scale),
                            Icon = CreateIcon(),
                            Margin = new MarginPadding { Left = 25 }
                        },
                        labelText = new OsuSpriteText
                        {
                            Text = Label,
                            Font = OsuFont.Default.With(size: OsuFont.DEFAULT_FONT_SIZE * scale),
                            Position = new Vector2(50, 0),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                    }
                },
                selectionIndicator = new CircularContainer
                {
                    Colour = ColourProvider.Highlight1,

                    Alpha = 0,
                    Width = 4,
                    Height = selection_indicator_height_inactive,
                    Masking = true,
                    CornerRadius = 1.5f,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding
                    {
                        Left = 9,
                    },
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White
                    }
                },
            });
        }

        protected override void UpdateState()
        {
            base.UpdateState();

            selectionIndicator.Colour = ColourProvider.Highlight1;

            if (Selected)
            {
                textIconContent.FadeColour(ColourProvider.Content1, OverlayColourProvider.ThemeTransitionDuration(FADE_DURATION), Easing.OutQuint);

                selectionIndicator.FadeIn(OverlayColourProvider.ThemeTransitionDuration(FADE_DURATION), Easing.OutQuint);
                selectionIndicator.ResizeHeightTo(selection_indicator_height_active, OverlayColourProvider.ThemeTransitionDuration(FADE_DURATION), Easing.OutElasticHalf);
            }
            else
            {
                textIconContent.FadeColour(IsHovered ? ColourProvider.Light1 : ColourProvider.Light3, OverlayColourProvider.ThemeTransitionDuration(FADE_DURATION), Easing.OutQuint);

                selectionIndicator.FadeOut(OverlayColourProvider.ThemeTransitionDuration(FADE_DURATION), Easing.OutQuint);
                selectionIndicator.ResizeHeightTo(selection_indicator_height_inactive, OverlayColourProvider.ThemeTransitionDuration(FADE_DURATION), Easing.OutQuint);
            }
        }
    }
}
