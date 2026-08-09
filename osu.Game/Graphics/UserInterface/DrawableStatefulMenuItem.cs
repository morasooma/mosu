// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterface
{
    public partial class DrawableStatefulMenuItem : DrawableOsuMenuItem
    {
        protected new StatefulMenuItem Item => (StatefulMenuItem)base.Item;

        public override bool CloseMenuOnClick => false;

        public DrawableStatefulMenuItem(StatefulMenuItem item)
            : base(item)
        {
        }

        protected override TextContainer CreateTextContainer() => new ToggleTextContainer(Item);

        private partial class ToggleTextContainer : TextContainer
        {
            private readonly StatefulMenuItem menuItem;
            private readonly Bindable<object> state;
            private readonly SpriteIcon stateIcon;
            private IBindable<ThemeMode> themeMode = null!;
            [Resolved(CanBeNull = true)]
            private OverlayColourProvider? colourProvider { get; set; }

            public ToggleTextContainer(StatefulMenuItem menuItem)
            {
                this.menuItem = menuItem;

                state = menuItem.State.GetBoundCopy();

                CheckboxContainer.Add(stateIcon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(10),
                    AlwaysPresent = true,
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                state.BindValueChanged(updateState, true);
                themeMode = OverlayColourProvider.CurrentTheme.GetBoundCopy();
                themeMode.BindValueChanged(_ => updateIconColour(), true);
            }

            private void updateState(ValueChangedEvent<object> state)
            {
                var icon = menuItem.GetIconForState(state.NewValue);

                if (icon == null)
                    stateIcon.Alpha = 0;
                else
                {
                    stateIcon.Alpha = 1;
                    stateIcon.Icon = icon.Value;
                }

                updateIconColour();
            }

            private void updateIconColour()
            {
                stateIcon.Colour = menuItem.Type switch
                {
                    MenuItemType.Destructive => Color4.Red,
                    MenuItemType.Highlighted => Color4Extensions.FromHex("ffcc22"),
                    _ => colourProvider != null && OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Colour4.White,
                };
            }
        }
    }
}
