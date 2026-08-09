using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Scoring.Render
{
    public partial class ReplayPeriodSelectorScreen : ReplayPlayer
    {
        private readonly Action<double, double> onConfirm;
        private double totalDuration;

        private Container timelineContainer;
        private RenderPlaybackControlBar playbackBar;
        private ArgonSongProgressGraph difficultyGraph;

        private TrimHandle startHandle;
        private TrimHandle endHandle;
        private Box leftOverlay;
        private Box rightOverlay;
        private Box currentTimeIndicator;

        public readonly BindableDouble TrimStartTime = new BindableDouble();
        public readonly BindableDouble TrimEndTime = new BindableDouble();

        private Container editorContainer;

        public ReplayPeriodSelectorScreen(Score score, Action<double, double> onConfirm) : base(score)
        {
            this.onConfirm = onConfirm;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Disable fail in selector screen so the clock never forcefully stops
            ValidForResume = true;

            totalDuration = Score.Replay.Frames.Count > 0 ? Score.Replay.Frames.Last().Time : GameplayClockContainer.CurrentTime;

            TrimStartTime.Value = 0;
            TrimEndTime.Value = totalDuration;

            // Hide the normal HUD to avoid showing the old thin SongProgress bar
            if (HUDOverlay != null)
                HUDOverlay.ShowHud.Value = false;

            // Shrink the gameplay area to make room for our editor interface at the bottom
            GameplayClockContainer.Scale = new osuTK.Vector2(0.8f);
            GameplayClockContainer.Anchor = Anchor.TopCentre;
            GameplayClockContainer.Origin = Anchor.TopCentre;

            Schedule(() =>
            {
                AddInternal(editorContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 150,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.FromHex("#111111"),
                            Alpha = 0.95f
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 60,
                            Padding = new MarginPadding { Horizontal = 30 },
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            Y = 20,
                            Child = timelineContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    difficultyGraph = new ArgonSongProgressGraph
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Blending = BlendingParameters.Additive,
                                    },
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 6,
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Colour = Colour4.FromHex("#333333"),
                                    },
                                    leftOverlay = new Box
                                    {
                                        RelativeSizeAxes = Axes.Y,
                                        Anchor = Anchor.TopLeft,
                                        Origin = Anchor.TopLeft,
                                        Colour = Colour4.Black.Opacity(0.6f),
                                    },
                                    rightOverlay = new Box
                                    {
                                        RelativeSizeAxes = Axes.Y,
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        Colour = Colour4.Black.Opacity(0.6f),
                                    },
                                    currentTimeIndicator = new Box
                                    {
                                        RelativeSizeAxes = Axes.Y,
                                        Width = 4,
                                        Anchor = Anchor.TopLeft,
                                        Origin = Anchor.TopCentre,
                                        Colour = Colour4.White,
                                        Alpha = 0.8f,
                                    }
                                }
                            }
                        },
                        playbackBar = new RenderPlaybackControlBar
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            TotalDuration = totalDuration,
                            OnPlayPause = togglePlayPause,
                            OnSeekRelative = seekRelative,
                            OnSetStartToCurrent = () => TrimStartTime.Value = Math.Min(GameplayClockContainer.CurrentTime, TrimEndTime.Value - 1000),
                            OnSetEndToCurrent = () => TrimEndTime.Value = Math.Max(GameplayClockContainer.CurrentTime, TrimStartTime.Value + 1000),
                            OnResetTrim = () =>
                            {
                                TrimStartTime.Value = 0;
                                TrimEndTime.Value = totalDuration;
                            },
                            OnConfirm = () =>
                            {
                                onConfirm?.Invoke(TrimStartTime.Value, TrimEndTime.Value);
                                this.Exit();
                            }
                        }
                    }
                });

                startHandle = new TrimHandle(true) { TotalDuration = totalDuration };
                endHandle = new TrimHandle(false) { TotalDuration = totalDuration };

                timelineContainer.AddRange(new Drawable[] { startHandle, endHandle });

                startHandle.SelectedTime.BindTo(TrimStartTime);
                endHandle.SelectedTime.BindTo(TrimEndTime);

                startHandle.OnDragStateChanged = dragging =>
                {
                    if (dragging)
                        GameplayClockContainer.Stop();
                };

                endHandle.OnDragStateChanged = dragging =>
                {
                    if (dragging)
                        GameplayClockContainer.Stop();
                };

                startHandle.OnTrimChanged = time =>
                {
                    if (time > TrimEndTime.Value - 1000)
                        TrimStartTime.Value = Math.Max(0, TrimEndTime.Value - 1000);

                    GameplayClockContainer.Seek(TrimStartTime.Value);
                };

                endHandle.OnTrimChanged = time =>
                {
                    if (time < TrimStartTime.Value + 1000)
                        TrimEndTime.Value = Math.Min(totalDuration, TrimStartTime.Value + 1000);

                    GameplayClockContainer.Seek(TrimEndTime.Value);
                };

                TrimStartTime.BindValueChanged(v =>
                {
                    leftOverlay.Width = startHandle.X;
                }, true);

                TrimEndTime.BindValueChanged(v =>
                {
                    rightOverlay.Width = timelineContainer.DrawWidth - endHandle.X;
                }, true);

                playbackBar.TrimStartTime.BindTo(TrimStartTime);
                playbackBar.TrimEndTime.BindTo(TrimEndTime);
                playbackBar.IsPlaying.BindValueChanged(_ => { }); // Dummy to initialize the property

                difficultyGraph.Objects = GameplayState.Beatmap.HitObjects;
            });
        }

        protected override void Update()
        {
            base.Update();

            if (playbackBar != null)
            {
                playbackBar.CurrentTime.Value = GameplayClockContainer.CurrentTime;
                
                // Update play/pause state
                playbackBar.IsPlaying.Value = !GameplayClockContainer.IsPaused.Value;
            }

            if (timelineContainer != null && totalDuration > 0)
            {
                float relativeTime = (float)(GameplayClockContainer.CurrentTime / totalDuration);
                currentTimeIndicator.X = relativeTime * timelineContainer.DrawWidth;

                // Stop playback when reaching the trim end bound
                if (GameplayClockContainer.CurrentTime >= TrimEndTime.Value && !GameplayClockContainer.IsPaused.Value)
                {
                    GameplayClockContainer.Stop();
                    GameplayClockContainer.Seek(TrimEndTime.Value);
                }

                if (!startHandle.IsDragged)
                    startHandle.X = (float)(Math.Clamp(TrimStartTime.Value / totalDuration, 0, 1) * timelineContainer.DrawWidth);
                    
                if (!endHandle.IsDragged)
                    endHandle.X = (float)(Math.Clamp(TrimEndTime.Value / totalDuration, 0, 1) * timelineContainer.DrawWidth);

                if (leftOverlay != null && rightOverlay != null)
                {
                    leftOverlay.Width = startHandle.X;
                    rightOverlay.Width = timelineContainer.DrawWidth - endHandle.X;
                }

                if (currentTimeIndicator != null)
                {
                    currentTimeIndicator.X = (float)(Math.Clamp(GameplayClockContainer.CurrentTime / totalDuration, 0, 1) * timelineContainer.DrawWidth);
                }
            }
        }

        private void togglePlayPause()
        {
            if (GameplayClockContainer.IsPaused.Value)
                GameplayClockContainer.Start();
            else
                GameplayClockContainer.Stop();
        }

        private void seekRelative(double amount)
        {
            GameplayClockContainer.Seek(GameplayClockContainer.CurrentTime + amount);
        }
    }
}
