// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Mods
{
    public partial class ModButtonTooltip : VisibilityContainer, ITooltip<Mod>
    {
        private readonly OsuSpriteText descriptionText;
        private readonly Box background;
        private readonly OverlayColourProvider colourProvider;
        private readonly IBindable<Colour4> themeColour;

        protected override Container<Drawable> Content { get; }

        public ModButtonTooltip(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;
            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 5;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background6,
                },
                Content = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = 10, Right = 10, Top = 5, Bottom = 5 },
                    Children = new Drawable[]
                    {
                        descriptionText = new OsuSpriteText
                        {
                            Colour = colourProvider.Content1,
                            Font = OsuFont.GetFont(weight: FontWeight.Regular),
                        },
                    }
                },
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Background6);
            themeColour.BindValueChanged(_ => updateThemeColours(), true);
        }

        private void updateThemeColours()
        {
            background.Colour = colourProvider.Background6;
            descriptionText.Colour = colourProvider.Content1;
        }

        protected override void PopIn() => this.FadeIn(200, Easing.OutQuint);
        protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);

        private Mod? lastMod;

        public void SetContent(Mod mod)
        {
            if (mod.Equals(lastMod))
                return;

            lastMod = mod;

            UpdateDisplay(mod);
        }

        protected virtual void UpdateDisplay(Mod mod)
        {
            descriptionText.Text = mod.Description;
        }

        public void Move(Vector2 pos) => Position = pos;
    }
}
