using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Localisation;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings
{
    /// <summary>Intercepts input above the entire control, including its reset button.</summary>
    internal partial class FavouriteSelectionTarget : Container
    {
        private readonly Action toggle;
        private readonly SpriteIcon star;
        private readonly Func<bool> selected;
        private readonly Func<bool> enabled;
        private readonly Func<bool> blockKeyboard;

        public FavouriteSelectionTarget(Action toggle, Func<bool> selected, Func<bool> enabled, Func<bool>? blockKeyboard = null)
        {
            this.toggle = toggle;
            this.selected = selected;
            this.enabled = enabled;
            this.blockKeyboard = blockKeyboard ?? enabled;
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.25f },
                star = new SpriteIcon
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Margin = new MarginPadding { Right = 3 },
                    Size = new osuTK.Vector2(20),
                    Icon = FontAwesome.Regular.Star,
                },
            };
        }

        protected override void Update()
        {
            base.Update();
            Alpha = enabled() ? 1 : 0;
            star.Icon = selected() ? FontAwesome.Solid.Star : FontAwesome.Regular.Star;
        }

        public override bool ReceivePositionalInputAt(osuTK.Vector2 screenSpacePos) => enabled() && base.ReceivePositionalInputAt(screenSpacePos);
        protected override bool OnMouseDown(MouseDownEvent e) => enabled();
        protected override bool OnDragStart(DragStartEvent e) => enabled();
        protected override bool OnKeyDown(KeyDownEvent e) => blockKeyboard() && e.Key is not (osuTK.Input.Key.Escape or osuTK.Input.Key.Tab)
            && !e.ControlPressed && !e.AltPressed;
        protected override bool OnClick(ClickEvent e)
        {
            if (!enabled())
                return false;
            toggle();
            return true;
        }
    }
}
