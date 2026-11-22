// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Input.Bindings;
using osu.Game.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarChatButton : ToolbarOverlayToggleButton
    {
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        public ToolbarChatButton()
        {
            Hotkey = GlobalAction.ToggleChat;
            SetIcon(OsuIcon.Chat);
        }

        [BackgroundDependencyLoader(true)]
        private void load(ChatOverlay chat)
        {
            StateContainer = chat;
        }
    }
}
