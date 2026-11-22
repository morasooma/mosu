// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    public partial class TimelineDragBox : DragBox
    {
        public double MinTime { get; private set; }

        public double MaxTime { get; private set; }

        private double? startTime;
        private readonly bool selectRows;

        public TimelineDragBox(bool selectRows = false)
        {
            this.selectRows = selectRows;
        }

        [Resolved]
        private Timeline timeline { get; set; } = null!;

        protected override Drawable CreateBox() => new Box
        {
            RelativeSizeAxes = selectRows ? Axes.None : Axes.Y,
            Alpha = 0.3f
        };

        public override void HandleDrag(MouseButtonEvent e)
        {
            startTime ??= timeline.TimeAtScreenSpacePosition(e.ScreenSpaceMouseDownPosition);
            double endTime = timeline.TimeAtScreenSpacePosition(e.ScreenSpaceMousePosition);

            MinTime = Math.Min(startTime.Value, endTime);
            MaxTime = Math.Max(startTime.Value, endTime);

            Vector2 mouseStart = ToLocalSpace(e.ScreenSpaceMouseDownPosition);
            Vector2 mouseEnd = ToLocalSpace(e.ScreenSpaceMousePosition);
            Box.X = Math.Min(mouseStart.X, mouseEnd.X);
            Box.Width = Math.Abs(mouseEnd.X - mouseStart.X);
            if (selectRows)
            {
                Vector2 start = Box.Parent!.ToLocalSpace(e.ScreenSpaceMouseDownPosition);
                Vector2 end = Box.Parent.ToLocalSpace(e.ScreenSpaceMousePosition);
                Box.Y = Math.Min(start.Y, end.Y);
                Box.Height = Math.Max(1, Math.Abs(end.Y - start.Y));
            }
        }

        public override void Hide()
        {
            base.Hide();
            startTime = null;
        }
    }
}
