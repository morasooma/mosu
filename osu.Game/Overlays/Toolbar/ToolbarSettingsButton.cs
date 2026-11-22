// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Input.Bindings;
using osu.Game.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarSettingsButton : ToolbarOverlayToggleButton
    {
        public ToolbarSettingsButton()
        {
            ButtonContent.Width *= 1.4f;
            Hotkey = GlobalAction.ToggleSettings;
            SetIcon(OsuIcon.Settings);
        }

        [BackgroundDependencyLoader(true)]
        private void load(SettingsOverlay settings)
        {
            StateContainer = settings;
        }
    }
}
