using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osu.Framework.Bindables;

namespace osu.Game.Scoring.Render
{
    public partial class TrimHandle : CompositeDrawable
    {
        private readonly bool isStart;

        public BindableDouble SelectedTime { get; } = new BindableDouble();

        public Action<double> OnTrimChanged;
        public Action<bool> OnDragStateChanged;

        private const float width = 16;
        private Circle knob;
        private Box line;

        public double TotalDuration { get; set; }

        public TrimHandle(bool isStart)
        {
            this.isStart = isStart;

            Width = width;
            RelativeSizeAxes = Axes.Y;
            Origin = isStart ? Anchor.TopRight : Anchor.TopLeft;
            Anchor = Anchor.TopLeft;

            Colour4 color = isStart ? Colour4.FromHex("#00FFAA") : Colour4.FromHex("#FF4444");

            InternalChildren = new Drawable[]
            {
                line = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 4,
                    Anchor = isStart ? Anchor.TopRight : Anchor.TopLeft,
                    Origin = isStart ? Anchor.TopRight : Anchor.TopLeft,
                    Colour = color,
                },
                knob = new Circle
                {
                    Size = new Vector2(16, 24),
                    Anchor = isStart ? Anchor.TopRight : Anchor.TopLeft,
                    Origin = isStart ? Anchor.TopRight : Anchor.TopLeft,
                    Y = 18, // Center it vertically on the 60px height (60/2 - 24/2 = 18)
                    Colour = color,
                }
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            knob.ScaleTo(1.2f, 200, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            knob.ScaleTo(1f, 200, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            OnDragStateChanged?.Invoke(true);
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (TotalDuration <= 0 || Parent == null) return;

            float newX = X + e.Delta.X;
            newX = Math.Clamp(newX, 0, Parent.DrawWidth);

            X = newX;
            SelectedTime.Value = (newX / Parent.DrawWidth) * TotalDuration;
            OnTrimChanged?.Invoke(SelectedTime.Value);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            base.OnDragEnd(e);
            OnDragStateChanged?.Invoke(false);
        }
    }
}
