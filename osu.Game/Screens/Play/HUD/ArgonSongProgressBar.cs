// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    public partial class ArgonSongProgressBar : SongProgressBar, IHasTooltip
    {
        // Parent will handle restricting the area of valid input.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        private readonly float barHeight;

        private readonly RoundedBar playfieldBar;
        private readonly RoundedBar audioBar;

        private readonly Box background;

        private readonly ColourInfo mainColour;
        private ColourInfo catchUpColour;

        public double Progress { get; set; }

        private double trackTime => (EndTime - StartTime) * Progress;

        private float lastMouseX;
        private bool audioBarInFront;
        private bool usingCatchUpAccent;
        private ColourInfo lastAudioAccent;
        private float lastPlayfieldLength = -1;
        private float lastAudioLength = -1;

        private const int skin_perf_progress_steps = 256;

        public LocalisableString TooltipText
        {
            get
            {
                if (!Interactive)
                    return default;

                double progress = Math.Clamp(lastMouseX, 0, DrawWidth) / DrawWidth;

                TimeSpan currentSpan = TimeSpan.FromMilliseconds(Math.Round((EndTime - StartTime) * progress));

                int seconds = currentSpan.Duration().Seconds;
                int minutes = (int)Math.Floor(currentSpan.Duration().TotalMinutes);

                return $"{minutes}:{seconds:D2} ({progress:P0})";
            }
        }

        public ArgonSongProgressBar(float barHeight)
        {
            RelativeSizeAxes = Axes.X;
            Height = this.barHeight = barHeight;

            CornerRadius = 5;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Colour = OsuColour.Gray(0.2f),
                    Depth = float.MaxValue,
                },
                audioBar = new RoundedBar
                {
                    Name = "Audio bar",
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    CornerRadius = 5,
                    RelativeSizeAxes = Axes.Both
                },
                playfieldBar = new RoundedBar
                {
                    Name = "Playfield bar",
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    CornerRadius = 5,
                    AccentColour = mainColour = OsuColour.Gray(0.9f),
                    RelativeSizeAxes = Axes.Both
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            catchUpColour = colours.BlueDark;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            double duration = SkinPerformanceMode.ShouldSimplifyHud ? 0 : 200;

            // The track backdrop is a full-width translucent Box (~24k px/frame) mostly covered by the
            // progress bars anyway — leave it hidden in perf mode.
            if (!(SkinPerformanceMode.ShouldSimplifyHud && Performance.MosuOptimisationToggles.PerfSongProgressLite))
                background.FadeTo(0.3f, duration, Easing.In);

            playfieldBar.TransformTo(nameof(playfieldBar.AccentColour), mainColour, duration, Easing.In);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            base.OnMouseMove(e);

            lastMouseX = e.MousePosition.X;
            return false;
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (Interactive)
                this.ResizeHeightTo(barHeight * 3.5f, SkinPerformanceMode.ShouldSimplifyHud ? 0 : 200, Easing.Out);

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            this.ResizeHeightTo(barHeight, SkinPerformanceMode.ShouldSimplifyHud ? 0 : 800, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override void Update()
        {
            base.Update();

            const float colour_transition_threshold = 20000;

            if (SkinPerformanceMode.ShouldSimplifyHud)
            {
                setBarLengthIfChanged(playfieldBar, quantiseProgress((float)Progress), ref lastPlayfieldLength);
                setBarLengthIfChanged(audioBar, quantiseProgress((float)AudioProgress), ref lastAudioLength);

                bool audioInFront = trackTime > AudioTime;
                if (audioInFront != audioBarInFront)
                {
                    audioBarInFront = audioInFront;
                    ChangeInternalChildDepth(audioBar, audioInFront ? -1 : 1);
                }

                bool useCatchUp = Math.Abs(AudioTime - trackTime) > colour_transition_threshold;
                if (useCatchUp != usingCatchUpAccent)
                {
                    usingCatchUpAccent = useCatchUp;
                    audioBar.AccentColour = useCatchUp ? catchUpColour : mainColour;
                }

                return;
            }

            playfieldBar.Length = (float)Interpolation.Lerp(playfieldBar.Length, Progress, Math.Clamp(Time.Elapsed / 40, 0, 1));
            audioBar.Length = (float)Interpolation.Lerp(audioBar.Length, AudioProgress, Math.Clamp(Time.Elapsed / 40, 0, 1));

            bool audioInFrontAnimated = trackTime > AudioTime;
            if (audioInFrontAnimated != audioBarInFront)
            {
                audioBarInFront = audioInFrontAnimated;
                ChangeInternalChildDepth(audioBar, audioInFrontAnimated ? -1 : 1);
            }

            float timeDelta = (float)Math.Abs(AudioTime - trackTime);

            ColourInfo accent = Interpolation.ValueAt(
                Math.Min(timeDelta, colour_transition_threshold),
                mainColour,
                catchUpColour,
                0, colour_transition_threshold,
                Easing.OutQuint);

            if (!accent.Equals(lastAudioAccent))
            {
                lastAudioAccent = accent;
                audioBar.AccentColour = accent;
            }
        }

        private static float quantiseProgress(float progress)
            => (float)Math.Round(Math.Clamp(progress, 0, 1) * skin_perf_progress_steps) / skin_perf_progress_steps;

        private static void setBarLengthIfChanged(RoundedBar bar, float length, ref float lastLength)
        {
            if (Math.Abs(length - lastLength) < 0.0001f)
                return;

            lastLength = length;
            bar.Length = length;
        }

        private partial class RoundedBar : Container
        {
            private readonly Box fill;
            private readonly Container mask;
            private float length;

            public RoundedBar()
            {
                Masking = true;
                Children = new[]
                {
                    mask = new Container
                    {
                        Masking = true,
                        RelativeSizeAxes = Axes.Y,
                        Size = new Vector2(1),
                        Child = fill = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.White
                        }
                    }
                };
            }

            public float Length
            {
                get => length;
                set
                {
                    if (Math.Abs(length - value) < 0.0001f)
                        return;

                    length = value;
                    mask.Width = value * DrawWidth;
                }
            }

            public new float CornerRadius
            {
                get => base.CornerRadius;
                set
                {
                    base.CornerRadius = value;
                    mask.CornerRadius = value;
                }
            }

            public ColourInfo AccentColour
            {
                get => fill.Colour;
                set
                {
                    if (fill.Colour.Equals(value))
                        return;

                    fill.Colour = value;
                }
            }
        }
    }
}
