using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Screens.Play.HUD;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// A mouse-positioned object-density timeline, independent of the normal pause menu.
    /// </summary>
    public partial class PracticeTimeline : OverlayContainer
    {
        private readonly double startTime;
        private readonly double endTime;
        private readonly Action<double> selectTime;
        private readonly Func<KeyDownEvent, bool>? isGameplayKey;
        private readonly Box marker;
        private readonly OsuSpriteText timeLabel;

        protected override bool BlockNonPositionalInput => false;

        public PracticeTimeline(IBeatmap beatmap, Action<double> selectTime, Func<KeyDownEvent, bool>? isGameplayKey = null)
        {
            this.selectTime = selectTime;
            this.isGameplayKey = isGameplayKey;
            (startTime, endTime) = beatmap.CalculatePlayableBounds();
            RelativeSizeAxes = Axes.X;
            Height = 100;
            Origin = Anchor.CentreLeft;
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Black, Alpha = 0.85f },
                new DefaultSongProgressGraph(trackDrawInfo: false)
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 25, Bottom = 8 },
                    Objects = beatmap.HitObjects,
                    FillColour = Colour4.Cyan,
                },
                marker = new Box { RelativeSizeAxes = Axes.Y, Width = 2, Colour = Colour4.White },
                timeLabel = new OsuSpriteText
                {
                    Position = new Vector2(8, 4),
                    Font = OsuFont.GetFont(size: 18),
                    Text = PracticeStrings.SelectTime,
                },
            };
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            float x = Math.Clamp(ToLocalSpace(e.ScreenSpaceMousePosition).X, 0, DrawWidth);
            marker.X = x;
            timeLabel.Text = PracticeStrings.Time(TimeSpan.FromMilliseconds(timeAt(x)).ToString(@"mm\:ss"));
            return true;
        }

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button == MouseButton.Left)
                selectTime(timeAt(ToLocalSpace(e.ScreenSpaceMousePosition).X));
            return true;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat || isGameplayKey?.Invoke(e) != true)
                return false;

            selectTime(timeAt(ToLocalSpace(e.ScreenSpaceMousePosition).X));
            return true;
        }

        private double timeAt(float x) => startTime + Math.Clamp(x / Math.Max(1, DrawWidth), 0, 1) * (endTime - startTime);

        protected override void PopIn() => this.FadeIn();
        protected override void PopOut() => this.FadeOut();
    }
}
