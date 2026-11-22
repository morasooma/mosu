// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Footer
{
    public partial class ScreenBackButton : ShearedButton
    {
        public const float BUTTON_WIDTH = 240;

        private static readonly Colour4 default_darker = Color4Extensions.FromHex("#DE31AE");
        private static readonly Colour4 default_lighter = Color4Extensions.FromHex("#FF86DD");

        private IBindable<int>? paletteVersionBindable;

        public sealed override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            // Ensure clicks in the corner of the screen still trigger the back button.
            // Need to apply more than 1x inflation due to shear.
            var inputRectangle = DrawRectangle.Inflate(new MarginPadding
            {
                Left = OsuGame.SCREEN_EDGE_MARGIN * 2,
                Bottom = OsuGame.SCREEN_EDGE_MARGIN * 2,
            });

            return inputRectangle.Contains(ToLocalSpace(screenSpacePos));
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Width = BUTTON_WIDTH;

            ButtonContent.Child = new FillFlowContainer
            {
                X = -10f,
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(20f, 0f),
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(17f),
                        Icon = FontAwesome.Solid.ChevronLeft,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = OsuFont.TorusAlternate.With(size: 17),
                        Text = CommonStrings.Back,
                        UseFullGlyphHeight = false,
                    }
                }
            };

            DarkerColour = default_darker;
            LighterColour = default_lighter;
            TextColour = Color4.White;

            paletteVersionBindable = ColourProvider.PaletteVersion.GetBoundCopy();
            paletteVersionBindable.BindValueChanged(_ => updateColours(), true);
        }

        private void updateColours()
        {
            DarkerColour = OverlayColourProvider.GetEffectiveContextColour(ThemeSurfaceContext.FooterBack, SurfaceColourRole.ControlIdle, default_darker);
            LighterColour = OverlayColourProvider.GetEffectiveContextColour(ThemeSurfaceContext.FooterBack, SurfaceColourRole.ControlHover, default_lighter);
            TextColour = OverlayColourProvider.GetEffectiveContextColour(ThemeSurfaceContext.FooterBack, SurfaceColourRole.TextPrimary, Color4.White);
        }
    }
}
