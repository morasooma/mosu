using osu.Framework.Allocation;
using osu.Game.Localisation;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarThemeLibraryButton : ToolbarOverlayToggleButton
    {
        public ToolbarThemeLibraryButton()
        {
            SetIcon(OsuIcon.NavigationPalette);
            TooltipMain = ThemeLibraryStrings.Library;
            TooltipSub = ThemeLibraryStrings.OpenHint;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ThemeLibraryOverlay themes, OsuGame game)
        {
            StateContainer = themes;
            if (game != null)
                Action = () =>
                {
                    if (themes.State.Value == osu.Framework.Graphics.Containers.Visibility.Visible) themes.Hide();
                    else game.ShowThemeLibrary();
                };
        }
    }
}
