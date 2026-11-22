using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.CustomTheme;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>
    /// An aligned, intentional icon button used in custom theme settings rows
    /// to revert an overridden parameter back to its inherited value.
    /// </summary>
    public partial class ThemeResetButton : OsuClickableContainer
    {
        public const float BUTTON_WIDTH = 26f;
        public const float BUTTON_HEIGHT = 24f;

        private Box background = null!;
        private SpriteIcon icon = null!;
        private IBindable<Colour4> backgroundColour = null!;
        private IBindable<float>? cornerRadius;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public override bool AcceptsFocus => true;

        public ThemeResetButton()
        {
            Size = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);
            Masking = true;
            CornerRadius = 5f;
            TooltipText = CommonStrings.RevertToDefault;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background3,
                },
                icon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = FontAwesome.Solid.Undo,
                    Size = new Vector2(11),
                    Colour = colourProvider.Light1,
                }
            };

            backgroundColour = colourProvider.GetColourBindable(OverlayColour.Background3);
            backgroundColour.BindValueChanged(_ => updateDisplay(), true);

            cornerRadius = colourProvider.GetRadiusBindable(GeometryRole.Button, 5f);
            cornerRadius.BindValueChanged(r => CornerRadius = Math.Clamp(r.NewValue, 2f, BUTTON_HEIGHT / 2f), true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Enabled.BindValueChanged(_ => updateDisplay(), true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateDisplay();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateDisplay();
            base.OnHoverLost(e);
        }

        private void updateDisplay()
        {
            if (!Enabled.Value)
            {
                icon.FadeColour(colourProvider.Light1.Opacity(0.3f), 200, Easing.OutQuint);
                background.FadeColour(colourProvider.Background3.Opacity(0.4f), 200, Easing.OutQuint);
                return;
            }

            icon.FadeColour(IsHovered ? colourProvider.Content1 : colourProvider.Light1, 200, Easing.OutQuint);
            background.FadeColour(IsHovered ? colourProvider.Background2 : colourProvider.Background3, 200, Easing.OutQuint);
        }
    }
}
