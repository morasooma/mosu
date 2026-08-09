// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterface
{
    public partial class BasicSearchTextBox : SearchTextBox
    {
        private SpriteIcon icon = null!;
        private IBindable<Colour4>? themeColour;
        private IBindable<ThemeMode>? themeMode;

        [Resolved(CanBeNull = true)]
        private OverlayColourProvider? colourProvider { get; set; }

        public BasicSearchTextBox()
        {
            Add(icon = new SpriteIcon
            {
                Icon = FontAwesome.Solid.Search,
                Origin = Anchor.CentreRight,
                Anchor = Anchor.CentreRight,
                Margin = new MarginPadding { Right = 10 },
                Size = new Vector2(20),
            });

            TextFlow.Padding = new MarginPadding { Right = 35 };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(colour => icon.Colour = colour.NewValue, true);
            }
            else
            {
                themeMode = OverlayColourProvider.CurrentTheme.GetBoundCopy();
                themeMode.BindValueChanged(_ => icon.Colour = OverlayColourProvider.IsLightTheme ? Color4.Black : Color4.White, true);
            }
        }
    }
}
