// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class EditorResizeHandle : CircularContainer
    {
        private readonly Anchor resizeAnchor;
        private readonly Action<Anchor, Vector2> beginResize;
        private readonly Action<Anchor, Vector2, bool> updateResize;

        public EditorResizeHandle(Anchor anchor, Color4 colour, Action<Anchor, Vector2> beginResize, Action<Anchor, Vector2, bool> updateResize)
        {
            resizeAnchor = anchor;
            this.beginResize = beginResize;
            this.updateResize = updateResize;
            Anchor = anchor;
            Origin = Anchor.Centre;
            Size = new Vector2(12);
            Masking = true;
            BorderThickness = 2;
            BorderColour = Color4.White;
            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour };
        }

        protected override bool OnHover(HoverEvent e)
        {
            this.ScaleTo(1.45f, 100, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            this.ScaleTo(1, 100, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            beginResize(resizeAnchor, e.ScreenSpaceMousePosition);
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            base.OnDrag(e);
            updateResize(resizeAnchor, e.ScreenSpaceMousePosition, e.ShiftPressed);
        }
    }
}
