// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    public partial class RetryButton : OsuAnimatedButton
    {
        private readonly Box background;

        [Resolved]
        private Player? player { get; set; }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public RetryButton()
        {
            Size = new Vector2(50, 30);

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = float.MaxValue
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(13),
                    Icon = FontAwesome.Solid.Redo,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            updateColours();

            if (player != null)
            {
                TooltipText = player is ReplayPlayer ? global::osu.Game.Localisation.ClientTranslationStrings.J5113 : global::osu.Game.Localisation.ClientTranslationStrings.J5114;
                Action = () => player.Restart();
            }
            else
            {
                TooltipText = global::osu.Game.Localisation.ClientTranslationStrings.J5114;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            colourProvider.PaletteVersion.BindValueChanged(_ => updateColours());
        }

        private void updateColours()
        {
            background.Colour = OverlayColourProvider.GetEffectiveContextColour(
                ThemeSurfaceContext.Results,
                SurfaceColourRole.Accent,
                colours.Green);
        }
    }
}
