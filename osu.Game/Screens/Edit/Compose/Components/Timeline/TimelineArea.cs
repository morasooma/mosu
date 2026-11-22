// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Localisation;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    public partial class TimelineArea : CompositeDrawable
    {
        public Timeline Timeline = null!;

        private readonly Drawable userContent;
        private readonly bool enableHitSoundTracks;

        private Box timelineBackground = null!;
        private Box rightBox = null!;
        private Box zoomControlsBox = null!;
        private IBindable<Colour4>? rightBoxColourBindable;
        private IBindable<Colour4>? timelineBgColourBindable;
        private IBindable<Colour4>? zoomControlsColourBindable;
        private readonly Bindable<bool> composerFocusMode = new Bindable<bool>();

        public TimelineArea(Drawable? content = null, bool enableHitSoundTracks = false)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            userContent = content ?? Empty();
            this.enableHitSoundTracks = enableHitSoundTracks;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours, Editor? editor)
        {
            const float padding = 10;
            Container? trackLabels = null;

            InternalChildren = new Drawable[]
            {
                rightBox = new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = (enableHitSoundTracks ? 70 : 0) + 35 + HitObjectComposer.TOOLBOX_CONTRACTED_SIZE_RIGHT,
                    RelativeSizeAxes = Axes.Y,
                    Colour = colourProvider.Background4
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, enableHitSoundTracks ? 70 : 0),
                        new Dimension(GridSizeMode.Absolute, 35),
                        new Dimension(GridSizeMode.Absolute, HitObjectComposer.TOOLBOX_CONTRACTED_SIZE_RIGHT),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    timelineBackground = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Depth = float.MaxValue,
                                        Colour = colourProvider.Background5
                                    },
                                    Timeline = new Timeline(userContent) { EnableHitSoundTracks = enableHitSoundTracks },
                                }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Name = @"Sound controls",
                                Padding = new MarginPadding { Right = 5 },
                                Child = enableHitSoundTracks ? new RoundedButton
                                {
                                    Name = @"Toggle hit sound tracks",
                                    RelativeSizeAxes = Axes.X,
                                    Height = 24,
                                    Y = 28,
                                    Text = HitSoundTracksStrings.Sounds,
                                    Action = () =>
                                    {
                                        Timeline.ToggleHitSoundTracks();
                                        if (trackLabels != null)
                                            trackLabels.Alpha = Timeline.HitSoundTracksExpanded ? 1 : 0;
                                    },
                                } : Empty(),
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Name = @"Zoom controls",
                                Padding = new MarginPadding { Right = padding },
                                Children = new Drawable[]
                                {
                                    zoomControlsBox = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colourProvider.Background2,
                                    },
                                    new Container<TimelineButton>
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.Both,
                                        Children = new[]
                                        {
                                            new TimelineButton
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Size = new Vector2(1, 0.5f),
                                                Icon = FontAwesome.Solid.SearchPlus,
                                                Action = () => Timeline.AdjustZoomRelatively(1)
                                            },
                                            new TimelineButton
                                            {
                                                Anchor = Anchor.BottomLeft,
                                                Origin = Anchor.BottomLeft,
                                                RelativeSizeAxes = Axes.Both,
                                                Size = new Vector2(1, 0.5f),
                                                Icon = FontAwesome.Solid.SearchMinus,
                                                Action = () => Timeline.AdjustZoomRelatively(-1)
                                            },
                                        }
                                    }
                                }
                            },
                            new BeatDivisorControl { RelativeSizeAxes = Axes.Both }
                        },
                    },
                }
            };

            if (enableHitSoundTracks)
            {
                trackLabels = new Container
                {
                    Y = Timeline.ObjectAreaHeight,
                    Width = 70,
                    Height = HitSoundTrackEditor.TRACK_HEIGHT,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colourProvider.Background5 },
                        new OsuSpriteText { Text = HitSoundTracksStrings.Whistle, Y = 2, Font = OsuFont.Default.With(size: 12) },
                        new OsuSpriteText { Text = HitSoundTracksStrings.Finish, Y = 24, Font = OsuFont.Default.With(size: 12) },
                        new OsuSpriteText { Text = HitSoundTracksStrings.Clap, Y = 46, Font = OsuFont.Default.With(size: 12) },
                        new OsuSpriteText { Text = HitSoundTracksStrings.Bank, Y = 70, Font = OsuFont.Default.With(size: 12) },
                        new OsuSpriteText { Text = HitSoundTracksStrings.Volume, Y = 94, Font = OsuFont.Default.With(size: 12) },
                    },
                };
                AddInternal(trackLabels);
            }

            if (editor != null)
                composerFocusMode.BindTo(editor.ComposerFocusMode);

            rightBoxColourBindable = colourProvider.GetColourBindable(OverlayColour.Background4);
            rightBoxColourBindable.BindValueChanged(c => rightBox.Colour = c.NewValue, true);

            timelineBgColourBindable = colourProvider.GetColourBindable(OverlayColour.Background5);
            timelineBgColourBindable.BindValueChanged(c => timelineBackground.Colour = c.NewValue, true);

            zoomControlsColourBindable = colourProvider.GetColourBindable(OverlayColour.Background2);
            zoomControlsColourBindable.BindValueChanged(c => zoomControlsBox.Colour = c.NewValue, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            composerFocusMode.BindValueChanged(_ =>
            {
                // Transforms should be kept in sync with other usages of composer focus mode.
                if (!composerFocusMode.Value)
                    timelineBackground.FadeIn(750, Easing.OutQuint);
                else
                    timelineBackground.Delay(600).FadeTo(0.5f, 4000, Easing.OutQuint);
            }, true);
        }
    }
}
