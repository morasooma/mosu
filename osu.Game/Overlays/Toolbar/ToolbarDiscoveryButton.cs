using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Discovery;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarDiscoveryButton : ToolbarOverlayToggleButton
    {
        public ToolbarDiscoveryButton()
        {
            SetIcon(OsuIcon.NavigationCompass);
            TooltipMain = DiscoveryStrings.Title;
            TooltipSub = DiscoveryStrings.Description;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DiscoveryOverlay discoveries, OsuGame game)
        {
            StateContainer = discoveries;
            if (game != null)
                Action = () =>
                {
                    if (discoveries.State.Value == Visibility.Visible) discoveries.Hide();
                    else game.ShowDiscovery();
                };
        }
    }
}
