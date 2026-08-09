// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Overlays;

namespace osu.Game.Graphics.UserInterface
{
    public partial class OsuContextMenu : OsuMenu
    {
        private IBindable<ThemeMode> themeMode = null!;

        [Resolved]
        private OsuMenuSamples menuSamples { get; set; } = null!;

        public OsuContextMenu(bool playSamples)
            : base(Direction.Vertical, topLevelMenu: false, playSamples)
        {
            MaskingContainer.CornerRadius = 5;
            MaskingContainer.EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = Color4.Black.Opacity(0.1f),
                Radius = 4,
            };

            ItemsContainer.Padding = new MarginPadding { Vertical = DrawableOsuMenuItem.MARGIN_VERTICAL };

            MaxHeight = 250;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            themeMode = OverlayColourProvider.CurrentTheme.GetBoundCopy();
            themeMode.BindValueChanged(e => updateBackgroundColour(colours, e.NewValue), true);
        }

        private void updateBackgroundColour(OsuColour colours, ThemeMode theme)
        {
            if (theme == ThemeMode.Dark)
                BackgroundColour = Color4Extensions.FromHex(@"1a1a1a");
            else if (theme == ThemeMode.Light)
                BackgroundColour = Color4Extensions.FromHex(@"ebebeb");
            else
                BackgroundColour = colours.ContextMenuGray;
        }

        protected override void AnimateOpen()
        {
            if (PlaySamples && !WasOpened)
                menuSamples.PlayClickSample();

            base.AnimateOpen();
        }

        protected override Menu CreateSubMenu() => new OsuContextMenu(false); // sub menu samples are handled by OsuMenu.OnSubmenuOpen.
    }
}
