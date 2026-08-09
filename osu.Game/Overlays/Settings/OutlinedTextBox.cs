// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK.Graphics;

#nullable disable

namespace osu.Game.Overlays.Settings
{
    public partial class OutlinedTextBox : OsuTextBox
    {
        private const float border_thickness = 3;

        private Color4 borderColourFocused;
        private Color4 borderColourUnfocused;
        private IBindable<Colour4> themeColour;
        private IBindable<ThemeMode> themeMode;

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider colourProvider, OsuColour colour)
        {
            void updateThemeColours()
            {
                borderColourUnfocused = colour.Gray4.Opacity(0.5f);
                borderColourFocused = colourProvider?.Highlight1 ?? colour.Yellow;

                var textColour = colourProvider?.Content1 ?? (OverlayColourProvider.IsLightTheme ? Color4.Black : Color4.White);
                TextFlow.Colour = textColour;
                Placeholder.Colour = colourProvider?.Foreground1 ?? new Color4(180, 180, 180, 255);
                SetCaretColour(textColour);

                updateBorder();
            }

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => updateThemeColours(), true);
            }
            else
            {
                themeMode = OverlayColourProvider.CurrentTheme.GetBoundCopy();
                themeMode.BindValueChanged(_ => updateThemeColours(), true);
            }
        }

        protected override void OnFocus(FocusEvent e)
        {
            base.OnFocus(e);

            updateBorder();
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            base.OnFocusLost(e);

            updateBorder();
        }

        private void updateBorder()
        {
            BorderThickness = border_thickness;
            BorderColour = HasFocus ? borderColourFocused : borderColourUnfocused;
        }
    }
}
