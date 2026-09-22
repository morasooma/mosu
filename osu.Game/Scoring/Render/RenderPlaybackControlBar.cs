using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osuTK;
using osuTK.Input;

namespace osu.Game.Scoring.Render
{
    public partial class RenderPlaybackControlBar : CompositeDrawable
    {
        public BindableDouble TrimStartTime { get; } = new BindableDouble();
        public BindableDouble TrimEndTime { get; } = new BindableDouble();
        public BindableDouble CurrentTime { get; } = new BindableDouble();
        public double TotalDuration { get; set; }

        public Action? OnPlayPause;
        public Action<double>? OnSeekRelative;
        public Action? OnResetTrim;
        public Action? OnConfirm;
        public Action? OnSetStartToCurrent;
        public Action? OnSetEndToCurrent;

        public BindableBool IsPlaying { get; } = new BindableBool();

        private IconButton playPauseButton = null!;
        private OsuSpriteText timecodeText = null!;
        private OsuSpriteText periodText = null!;

        public RenderPlaybackControlBar()
        {
            RelativeSizeAxes = Axes.X;
            Height = 60;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Direction = FillDirection.Horizontal,
                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(10),
                    Margin = new MarginPadding { Left = 20 },
                    Children = new Drawable[]
                    {
                        new IconButton
                        {
                            Icon = FontAwesome.Solid.FastBackward,
                            Action = () => OnSeekRelative?.Invoke(-5000),
                            Size = new Vector2(40),
                            TooltipText = ReplayRenderStrings.SeekBackward
                        },
                        playPauseButton = new IconButton
                        {
                            Icon = FontAwesome.Solid.Play,
                            Action = () => OnPlayPause?.Invoke(),
                            Size = new Vector2(40),
                            TooltipText = ReplayRenderStrings.PlayPause
                        },
                        new IconButton
                        {
                            Icon = FontAwesome.Solid.FastForward,
                            Action = () => OnSeekRelative?.Invoke(5000),
                            Size = new Vector2(40),
                            TooltipText = ReplayRenderStrings.SeekForward
                        },
                        timecodeText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                            Margin = new MarginPadding { Left = 10, Top = 10 },
                        }
                    }
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Direction = FillDirection.Horizontal,
                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(15),
                    Margin = new MarginPadding { Right = 20 },
                    Children = new Drawable[]
                    {
                        periodText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 14),
                            Colour = colours.Yellow,
                            Margin = new MarginPadding { Top = 10, Right = 10 },
                        },
                        new RoundedButton
                        {
                            Text = ReplayRenderStrings.Reset,
                            Action = () => OnResetTrim?.Invoke(),
                            Width = 100,
                            Height = 40,
                        },
                        new RoundedButton
                        {
                            Text = ReplayRenderStrings.Confirm,
                            Action = () => OnConfirm?.Invoke(),
                            Width = 120,
                            Height = 40,
                            BackgroundColour = colours.Green,
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            IsPlaying.BindValueChanged(isPlaying => playPauseButton.Icon = isPlaying.NewValue ? FontAwesome.Solid.Pause : FontAwesome.Solid.Play, true);

            CurrentTime.BindValueChanged(_ => updateText());
            TrimStartTime.BindValueChanged(_ => updateText());
            TrimEndTime.BindValueChanged(_ => updateText());

            updateText();
        }

        private void updateText()
        {
            timecodeText.Text = $"{formatTime(CurrentTime.Value)} / {formatTime(TotalDuration)}";
            periodText.Text = $"Selected: {formatTime(TrimStartTime.Value)} → {formatTime(TrimEndTime.Value)} ({formatTime(TrimEndTime.Value - TrimStartTime.Value)})";
        }

        private string formatTime(double ms)
        {
            ms = Math.Max(0, ms);
            TimeSpan t = TimeSpan.FromMilliseconds(ms);
            return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds / 100:0}";
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat) return false;

            switch (e.Key)
            {
                case Key.Space:
                    OnPlayPause?.Invoke();
                    return true;
                case Key.Left:
                    OnSeekRelative?.Invoke(e.ShiftPressed ? -1000 : -5000);
                    return true;
                case Key.Right:
                    OnSeekRelative?.Invoke(e.ShiftPressed ? 1000 : 5000);
                    return true;
                case Key.I:
                    OnSetStartToCurrent?.Invoke();
                    return true;
                case Key.O:
                    OnSetEndToCurrent?.Invoke();
                    return true;
            }

            return base.OnKeyDown(e);
        }
    }
}
