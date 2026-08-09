// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Screens.Edit;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Commands;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Edit.Design
{
    /// <summary>
    /// A storyboard-specific timeline. It deliberately edits only storyboard commands and
    /// remains independent from the Dodge hit object timeline.
    /// </summary>
    internal partial class LegacyDodgeStoryboardTimeline : CompositeDrawable
    {
        private const float label_width = 120;
        private const float ruler_top = 52;
        private const float ruler_height = 28;

        private readonly EditorClock editorClock;
        private readonly Box background;
        private readonly OsuSpriteText titleText;
        private readonly OsuSpriteText currentTimeText;
        private readonly OsuSpriteText selectionText;
        private readonly RoundedButton collapseButton;
        private readonly RoundedButton snapButton;
        private readonly OsuSpriteText rulerLabel;
        private readonly Container detailContent;
        private readonly Container ruler;
        private readonly OsuScrollContainer scroll;
        private readonly FillFlowContainer rows;
        private readonly Container playheadOverlay;
        private readonly Box playheadLine;
        private readonly OsuSpriteText emptyState;
        private readonly List<TimelineCommandBar> commandBars = new List<TimelineCommandBar>();

        private OverlayColourProvider colourProvider = null!;
        private OsuColour colours = null!;
        private bool paletteLoaded;
        private bool collapsed;
        private bool snapEnabled = true;
        private StoryboardSprite? sprite;
        private IStoryboardCommand? selectedCommand;
        private double viewStart;
        private double viewEnd = DodgeStoryboardEditing.DEFAULT_TIMELINE_SPAN;

        public Action<double, double>? ObjectRangeChanged;
        public Action<IStoryboardCommand, double, double>? CommandRangeChanged;
        public Action<IStoryboardCommand>? CommandSelected;
        public Action? CollapseRequested;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        public StoryboardSprite? SelectedSprite => sprite;
        public double ViewStart => viewStart;
        public double ViewEnd => viewEnd;
        public IStoryboardCommand? SelectedCommand => selectedCommand;

        public bool Collapsed
        {
            get => collapsed;
            set
            {
                collapsed = value;
                detailContent.Alpha = value ? 0 : 1;
                selectionText.Alpha = value ? 0 : 1;
                collapseButton.Text = value
                    ? DodgeEditorStrings.StoryboardTimelineExpand
                    : DodgeEditorStrings.StoryboardTimelineCollapse;
            }
        }

        public LegacyDodgeStoryboardTimeline(EditorClock editorClock)
        {
            this.editorClock = editorClock;
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                titleText = new OsuSpriteText
                {
                    Position = new Vector2(8, 5),
                    Text = DodgeEditorStrings.StoryboardTimeline,
                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                },
                currentTimeText = new OsuSpriteText
                {
                    Position = new Vector2(300, 7),
                    Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                },
                selectionText = new OsuSpriteText
                {
                    Position = new Vector2(8, 30),
                    Font = OsuFont.GetFont(size: 12),
                },
                collapseButton = new RoundedButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-6, 4),
                    Size = new Vector2(78, 26),
                    Text = DodgeEditorStrings.StoryboardTimelineCollapse,
                    Action = () => CollapseRequested?.Invoke(),
                },
                snapButton = new RoundedButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-90, 4),
                    Size = new Vector2(74, 26),
                    Text = DodgeEditorStrings.StoryboardTimelineSnap,
                    Action = toggleSnap,
                },
                headerButton("−", -170, () => adjustZoom(1.35)),
                headerButton("+", -206, () => adjustZoom(0.74)),
                headerButton("К курсору", -288, centreOnPlayhead, 76),
                headerButton("К объекту", -370, centreOnObject, 76),
                detailContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.Absolute, ruler_top),
                                new Dimension(GridSizeMode.Absolute, ruler_height),
                                new Dimension(),
                            },
                            Content = new[]
                            {
                                new Drawable[] { new Container() },
                                new Drawable[]
                                {
                                    new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        ColumnDimensions = new[]
                                        {
                                            new Dimension(GridSizeMode.Absolute, label_width),
                                            new Dimension(),
                                        },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                rulerLabel = new OsuSpriteText
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    X = 8,
                                                    Width = label_width - 8,
                                                    Text = "ВРЕМЯ",
                                                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                                                },
                                                ruler = new Container
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Masking = true,
                                                },
                                            },
                                        },
                                    },
                                },
                                new Drawable[]
                                {
                                    scroll = new OsuScrollContainer(Direction.Vertical)
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Child = rows = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 2),
                                            Padding = new MarginPadding { Bottom = 6 },
                                        },
                                    },
                                },
                            },
                        },
                        emptyState = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Y = 35,
                            Text = DodgeEditorStrings.StoryboardTimelineEmpty,
                            Font = OsuFont.GetFont(size: 14),
                        },
                        playheadOverlay = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding
                            {
                                Left = label_width,
                                Top = ruler_top,
                            },
                            Child = playheadLine = new Box
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = 2,
                            },
                        },
                    },
                },
            };

            playheadOverlay.BypassAutoSizeAxes = Axes.Both;
        }

        private static RoundedButton headerButton(string text, float right, Action action, float width = 30) => new RoundedButton
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(right, 4),
            Size = new Vector2(width, 26),
            Text = text,
            Action = action,
        };

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours)
        {
            this.colourProvider = colourProvider;
            this.colours = colours;

            // These are the same palette roles used by the production editor timelines.
            background.Colour = colourProvider.Background6;
            titleText.Colour = colourProvider.Content1;
            currentTimeText.Colour = colourProvider.Colour2;
            selectionText.Colour = colourProvider.Content2;
            rulerLabel.Colour = colourProvider.Content2;
            emptyState.Colour = colourProvider.Content2;
            playheadLine.Colour = colourProvider.Colour2;
            updateSnapButton();

            paletteLoaded = true;
            rebuild();
        }

        public void SetSelection(StoryboardSprite? selectedSprite)
        {
            sprite = selectedSprite;
            selectedCommand = null;
            (viewStart, viewEnd) = DodgeStoryboardEditing.CalculateTimelineView(sprite, editorClock.CurrentTimeAccurate);
            if (paletteLoaded)
                rebuild();
        }

        public void RestoreCommandSelection(IStoryboardCommand command)
        {
            if (sprite?.Commands.AllCommands.Any(candidate => ReferenceEquals(candidate, command)) != true)
                return;

            selectCommand(command);
        }

        protected override void Update()
        {
            base.Update();

            double currentTime = editorClock.CurrentTimeAccurate;
            currentTimeText.Text = $"СЕЙЧАС  {DodgeStoryboardEditing.FormatTime(currentTime)}";

            float progress = (float)((currentTime - viewStart) / Math.Max(1, viewEnd - viewStart));
            playheadLine.X = progress;
            playheadLine.RelativePositionAxes = Axes.X;
            playheadOverlay.Alpha = progress is >= 0 and <= 1 ? 1 : 0;
        }

        private void centreOnPlayhead()
        {
            double span = Math.Max(1, viewEnd - viewStart);
            viewStart = Math.Max(0, editorClock.CurrentTimeAccurate - span / 2);
            viewEnd = viewStart + span;
            rebuild();
        }

        private void centreOnObject()
        {
            (viewStart, viewEnd) = DodgeStoryboardEditing.CalculateTimelineView(sprite, editorClock.CurrentTimeAccurate);
            rebuild();
        }

        private void adjustZoom(double factor)
        {
            double oldSpan = Math.Max(250, viewEnd - viewStart);
            double newSpan = Math.Clamp(oldSpan * factor, 250, Math.Max(1000, editorClock.TrackLength));
            double currentTime = editorClock.CurrentTimeAccurate;
            double centre = currentTime >= viewStart && currentTime <= viewEnd
                ? currentTime
                : (viewStart + viewEnd) / 2;
            viewStart = Math.Max(0, centre - newSpan / 2);
            viewEnd = viewStart + newSpan;
            rebuild();
        }

        private void pan(double fraction)
        {
            double span = Math.Max(1, viewEnd - viewStart);
            double delta = span * fraction;
            viewStart = Math.Max(0, viewStart + delta);
            viewEnd = viewStart + span;
            rebuild();
        }

        private void toggleSnap()
        {
            snapEnabled = !snapEnabled;
            updateSnapButton();
        }

        private void updateSnapButton()
        {
            if (!paletteLoaded && colourProvider == null)
                return;

            snapButton.BackgroundColour = snapEnabled ? colourProvider.Highlight1 : colourProvider.Background3;
            snapButton.Text = snapEnabled
                ? "Snap: вкл"
                : "Snap: выкл";
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (e.ControlPressed)
                adjustZoom(e.ScrollDelta.Y > 0 ? 0.8 : 1.25);
            else
                pan(e.ScrollDelta.Y > 0 ? -0.1 : 0.1);

            return true;
        }

        private void rebuild()
        {
            rebuildRuler();
            rows.Clear();
            commandBars.Clear();
            emptyState.Alpha = sprite == null ? 1 : 0;

            if (sprite == null)
            {
                selectionText.Text = DodgeEditorStrings.StoryboardTimelineHelp;
                return;
            }

            double start = sprite.StartTime;
            double end = sprite.EndTimeForDisplay;
            selectionText.Text = selectedCommand == null
                ? $"{sprite.Path}  |  {DodgeStoryboardEditing.FormatTime(start)}–{DodgeStoryboardEditing.FormatTime(end)}  |  {DodgeEditorStrings.StoryboardTimelineRangeHelp}"
                : $"{commandTrackName(selectedCommand)}  |  ключ {DodgeStoryboardEditing.FormatTime(selectedCommand.StartTime)}–{DodgeStoryboardEditing.FormatTime(selectedCommand.EndTime)}  |  переход: {selectedCommand.Easing}";

            var objectTrack = new TimelineTrack(
                DodgeEditorStrings.StoryboardTimelineObjectRange.ToString(),
                viewStart,
                viewEnd,
                seek);
            objectTrack.AddRange(
                start,
                end,
                DodgeStoryboardEditing.CanRetime(sprite),
                (newStart, newEnd) => ObjectRangeChanged?.Invoke(newStart, newEnd),
                snapTime);
            rows.Add(objectTrack);

            foreach (IGrouping<string, IStoryboardCommand> group in sprite.Commands.AllCommands
                                                                               .OrderBy(command => command.StartTime)
                                                                               .GroupBy(commandTrackName))
            {
                var track = new TimelineTrack(group.Key, viewStart, viewEnd, seek);
                foreach (IStoryboardCommand command in group)
                {
                    var bar = new TimelineCommandBar(
                        command,
                        viewStart,
                        viewEnd,
                        () => selectCommand(command),
                        disableSnap => seek(command.StartTime, disableSnap),
                        DodgeStoryboardEditing.CanRetime(sprite),
                        (newStart, newEnd) => CommandRangeChanged?.Invoke(command, newStart, newEnd),
                        snapTime);
                    commandBars.Add(bar);
                    track.AddCommand(bar);
                }

                rows.Add(track);
            }

            if (!DodgeStoryboardEditing.CanRetime(sprite))
            {
                rows.Add(new OsuSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 22,
                    Text = DodgeEditorStrings.StoryboardRangeLocked,
                    Font = OsuFont.GetFont(size: 12),
                    Colour = colours.Yellow,
                });
            }
        }

        private void selectCommand(IStoryboardCommand command)
        {
            selectedCommand = command;
            CommandSelected?.Invoke(command);
            foreach (TimelineCommandBar bar in commandBars)
                bar.Selected = ReferenceEquals(bar.Command, command);

            selectionText.Text =
                $"{commandTrackName(command)}  |  ключ {DodgeStoryboardEditing.FormatTime(command.StartTime)}–{DodgeStoryboardEditing.FormatTime(command.EndTime)}  |  переход: {command.Easing}";
        }

        private static string commandTrackName(IStoryboardCommand command) => command switch
        {
            StoryboardAlphaCommand => "ПРОЗРАЧНОСТЬ",
            StoryboardXCommand or StoryboardYCommand => "ПОЗИЦИЯ",
            StoryboardScaleCommand or StoryboardVectorScaleCommand => "РАЗМЕР",
            StoryboardRotationCommand => "ПОВОРОТ",
            StoryboardColourCommand => "ЦВЕТ",
            StoryboardBlendingParametersCommand => "СВЕЧЕНИЕ",
            StoryboardFlipHCommand => "ОТРАЖЕНИЕ X",
            StoryboardFlipVCommand => "ОТРАЖЕНИЕ Y",
            _ => command.PropertyName.ToUpperInvariant(),
        };

        private void rebuildRuler()
        {
            ruler.Clear();
            ruler.Add(new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = colourProvider.Background2,
            });

            double span = Math.Max(1, viewEnd - viewStart);
            double interval = chooseTickInterval(span);
            double firstTick = Math.Ceiling(viewStart / interval) * interval;

            for (double time = firstTick; time <= viewEnd; time += interval)
            {
                float position = (float)((time - viewStart) / span);
                ruler.Add(new Box
                {
                    RelativePositionAxes = Axes.X,
                    X = position,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Width = 1,
                    Height = 7,
                    Colour = colourProvider.Background1,
                });
                ruler.Add(new OsuSpriteText
                {
                    RelativePositionAxes = Axes.X,
                    X = position,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = DodgeStoryboardEditing.FormatTime(time),
                    Font = OsuFont.GetFont(size: 10),
                    Colour = colourProvider.Content2,
                });
            }
        }

        private double snapTime(double time, bool disableSnap)
            => snapEnabled && !disableSnap ? beatSnapProvider.SnapTime(time) : time;

        private void seek(double time, bool disableSnap = false)
            => editorClock.Seek(Math.Clamp(snapTime(time, disableSnap), 0, editorClock.TrackLength));

        private static double chooseTickInterval(double span)
        {
            double ideal = span / 8;
            double[] intervals = { 100, 250, 500, 1000, 2000, 5000, 10000, 30000, 60000, 120000 };
            return intervals.FirstOrDefault(interval => interval >= ideal, intervals[^1]);
        }

        private partial class TimelineTrack : GridContainer
        {
            private readonly TimelineTrackBody body;
            private readonly OsuSpriteText label;

            public TimelineTrack(string text, double viewStart, double viewEnd, Action<double, bool> seek)
            {
                RelativeSizeAxes = Axes.X;
                Height = 26;

                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, label_width),
                    new Dimension(),
                };
                Content = new[]
                {
                    new Drawable[]
                    {
                        label = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 8,
                            Width = label_width - 12,
                            Text = text,
                            Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                        },
                        body = new TimelineTrackBody(viewStart, viewEnd, seek)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider) => label.Colour = colourProvider.Content1;

            public void AddRange(
                double start,
                double end,
                bool editable,
                Action<double, double> rangeChanged,
                Func<double, bool, double> snap)
                => body.Add(new TimelineRangeBar(start, end, body.ViewStart, body.ViewEnd, editable, rangeChanged, body.Seek, snap));

            public void AddCommand(TimelineCommandBar command) => body.Add(command);
        }

        private partial class TimelineTrackBody : Container
        {
            public readonly double ViewStart;
            public readonly double ViewEnd;
            public readonly Action<double, bool> Seek;

            private readonly Box background;
            private readonly Box centreLine;

            public TimelineTrackBody(double viewStart, double viewEnd, Action<double, bool> seek)
            {
                ViewStart = viewStart;
                ViewEnd = viewEnd;
                Seek = seek;
                Masking = true;

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    centreLine = new Box
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                background.Colour = colourProvider.Background5;
                centreLine.Colour = colourProvider.Background2;
            }

            protected override bool OnClick(ClickEvent e)
            {
                float progress = Math.Clamp(ToLocalSpace(e.ScreenSpaceMousePosition).X / Math.Max(1, DrawWidth), 0, 1);
                Seek(ViewStart + progress * (ViewEnd - ViewStart), e.AltPressed);
                return true;
            }
        }

        private partial class TimelineRangeBar : CompositeDrawable
        {
            private const double minimum_duration = DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION;

            private readonly double viewStart;
            private readonly double viewEnd;
            private readonly bool editable;
            private readonly Action<double, double> rangeChanged;
            private readonly Action<double, bool> seek;
            private readonly Func<double, bool, double> snap;
            private readonly Box fill;
            private readonly OsuSpriteText durationText;
            private readonly List<TimelineRangeHandle> handles = new List<TimelineRangeHandle>();

            private double previewStart;
            private double previewEnd;
            private RangeDragMode dragMode;

            public TimelineRangeBar(
                double start,
                double end,
                double viewStart,
                double viewEnd,
                bool editable,
                Action<double, double> rangeChanged,
                Action<double, bool> seek,
                Func<double, bool, double> snap)
            {
                previewStart = start;
                previewEnd = end;
                this.viewStart = viewStart;
                this.viewEnd = viewEnd;
                this.editable = editable;
                this.rangeChanged = rangeChanged;
                this.seek = seek;
                this.snap = snap;
                Height = 18;
                Y = 4;
                Masking = false;

                durationText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = DodgeStoryboardEditing.FormatDuration(end - start),
                    Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                };

                fill = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                };

                var children = new List<Drawable>
                {
                    fill,
                    durationText,
                };

                if (editable)
                {
                    children.Add(addHandle(new TimelineRangeHandle(RangeDragMode.ResizeStart, beginDrag, updateDrag, finishDrag)
                    {
                        Width = 8,
                    }));
                    children.Add(addHandle(new TimelineRangeHandle(RangeDragMode.ResizeEnd, beginDrag, updateDrag, finishDrag)
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Width = 8,
                    }));
                }

                InternalChildren = children;
                updateVisual();
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                var barColour = editable ? colourProvider.Highlight1 : colourProvider.Light4;
                var foregroundColour = OsuColour.ForegroundTextColourFor(barColour);

                fill.Colour = barColour;
                durationText.Colour = foregroundColour;
                foreach (TimelineRangeHandle handle in handles)
                    handle.HandleColour = foregroundColour;
            }

            private TimelineRangeHandle addHandle(TimelineRangeHandle handle)
            {
                handles.Add(handle);
                return handle;
            }

            protected override bool OnClick(ClickEvent e)
            {
                seek(previewStart, e.AltPressed);
                return true;
            }

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (!editable || e.Button != MouseButton.Left)
                    return false;

                beginDrag(RangeDragMode.Move);
                return true;
            }

            protected override void OnDrag(DragEvent e)
            {
                base.OnDrag(e);
                updateDrag(RangeDragMode.Move, e.Delta.X, e.AltPressed);
            }

            protected override void OnDragEnd(DragEndEvent e)
            {
                base.OnDragEnd(e);
                finishDrag(RangeDragMode.Move);
            }

            private void beginDrag(RangeDragMode mode) => dragMode = mode;

            private void updateDrag(RangeDragMode mode, float pixelDelta, bool disableSnap)
            {
                if (dragMode != mode || Parent == null)
                    return;

                double delta = pixelDelta / Math.Max(1, Parent.DrawWidth) * (viewEnd - viewStart);

                switch (mode)
                {
                    case RangeDragMode.Move:
                        delta = snap(previewStart + delta, disableSnap) - previewStart;
                        delta = Math.Max(delta, -previewStart);
                        previewStart += delta;
                        previewEnd += delta;
                        break;

                    case RangeDragMode.ResizeStart:
                        previewStart = Math.Clamp(snap(previewStart + delta, disableSnap), 0, previewEnd - minimum_duration);
                        break;

                    case RangeDragMode.ResizeEnd:
                        previewEnd = Math.Max(previewStart + minimum_duration, snap(previewEnd + delta, disableSnap));
                        break;
                }

                updateVisual();
            }

            private void finishDrag(RangeDragMode mode)
            {
                if (dragMode != mode)
                    return;

                dragMode = RangeDragMode.None;
                rangeChanged(previewStart, previewEnd);
            }

            private void updateVisual()
            {
                double span = Math.Max(1, viewEnd - viewStart);
                RelativePositionAxes = Axes.X;
                RelativeSizeAxes = Axes.X;
                X = (float)((previewStart - viewStart) / span);
                Width = Math.Max(0.004f, (float)((previewEnd - previewStart) / span));
                durationText.Text = DodgeStoryboardEditing.FormatDuration(previewEnd - previewStart);
            }
        }

        private partial class TimelineRangeHandle : CompositeDrawable
        {
            private readonly RangeDragMode mode;
            private readonly Action<RangeDragMode> begin;
            private readonly Action<RangeDragMode, float, bool> update;
            private readonly Action<RangeDragMode> end;
            private readonly Box handle;

            public Colour4 HandleColour
            {
                set => handle.Colour = value;
            }

            public TimelineRangeHandle(
                RangeDragMode mode,
                Action<RangeDragMode> begin,
                Action<RangeDragMode, float, bool> update,
                Action<RangeDragMode> end)
            {
                this.mode = mode;
                this.begin = begin;
                this.update = update;
                this.end = end;
                RelativeSizeAxes = Axes.Y;
                InternalChild = handle = new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(3, 12),
                };
            }

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (e.Button != MouseButton.Left)
                    return false;

                begin(mode);
                return true;
            }

            protected override void OnDrag(DragEvent e)
            {
                base.OnDrag(e);
                update(mode, e.Delta.X, e.AltPressed);
            }

            protected override void OnDragEnd(DragEndEvent e)
            {
                base.OnDragEnd(e);
                end(mode);
            }

            protected override bool OnClick(ClickEvent e) => true;
        }

        private partial class TimelineCommandBar : CompositeDrawable
        {
            private readonly Box fill;
            private readonly Action select;
            private readonly Action<bool> seek;
            private readonly double viewStart;
            private readonly double viewEnd;
            private readonly bool editable;
            private readonly Action<double, double> rangeChanged;
            private readonly Func<double, bool, double> snap;
            private readonly List<TimelineRangeHandle> handles = new List<TimelineRangeHandle>();

            private OverlayColourProvider colourProvider = null!;
            private double previewStart;
            private double previewEnd;
            private RangeDragMode dragMode;
            private bool selected;

            public readonly IStoryboardCommand Command;

            public bool Selected
            {
                set
                {
                    selected = value;
                    if (IsLoaded)
                        updateColour();
                }
            }

            public TimelineCommandBar(
                IStoryboardCommand command,
                double viewStart,
                double viewEnd,
                Action select,
                Action<bool> seek,
                bool editable,
                Action<double, double> rangeChanged,
                Func<double, bool, double> snap)
            {
                Command = command;
                this.select = select;
                this.seek = seek;
                this.viewStart = viewStart;
                this.viewEnd = viewEnd;
                this.editable = editable;
                this.rangeChanged = rangeChanged;
                this.snap = snap;
                previewStart = command.StartTime;
                previewEnd = command.EndTime;
                Y = 6;
                Height = 14;

                fill = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                };

                var children = new List<Drawable> { fill };
                if (editable)
                {
                    children.Add(addHandle(new TimelineRangeHandle(RangeDragMode.ResizeStart, beginDrag, updateDrag, finishDrag)
                    {
                        Width = 5,
                    }));
                    children.Add(addHandle(new TimelineRangeHandle(RangeDragMode.ResizeEnd, beginDrag, updateDrag, finishDrag)
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Width = 5,
                    }));
                }

                InternalChildren = children;
                updateVisual();
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                this.colourProvider = colourProvider;
                updateColour();
            }

            private TimelineRangeHandle addHandle(TimelineRangeHandle handle)
            {
                handles.Add(handle);
                return handle;
            }

            private void updateColour()
            {
                var barColour = selected ? colourProvider.Highlight1 : colourProvider.Colour3;
                var foregroundColour = OsuColour.ForegroundTextColourFor(barColour);

                fill.Colour = barColour;
                foreach (TimelineRangeHandle handle in handles)
                    handle.HandleColour = foregroundColour;
            }

            protected override bool OnClick(ClickEvent e)
            {
                select();
                seek(e.AltPressed);
                return true;
            }

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (!editable || e.Button != MouseButton.Left)
                    return false;

                select();
                beginDrag(RangeDragMode.Move);
                return true;
            }

            protected override void OnDrag(DragEvent e)
            {
                base.OnDrag(e);
                updateDrag(RangeDragMode.Move, e.Delta.X, e.AltPressed);
            }

            protected override void OnDragEnd(DragEndEvent e)
            {
                base.OnDragEnd(e);
                finishDrag(RangeDragMode.Move);
            }

            private void beginDrag(RangeDragMode mode)
            {
                select();
                dragMode = mode;
            }

            private void updateDrag(RangeDragMode mode, float pixelDelta, bool disableSnap)
            {
                if (dragMode != mode || Parent == null)
                    return;

                double delta = pixelDelta / Math.Max(1, Parent.DrawWidth) * (viewEnd - viewStart);

                switch (mode)
                {
                    case RangeDragMode.Move:
                        delta = snap(previewStart + delta, disableSnap) - previewStart;
                        delta = Math.Max(delta, -previewStart);
                        previewStart += delta;
                        previewEnd += delta;
                        break;

                    case RangeDragMode.ResizeStart:
                        previewStart = Math.Clamp(snap(previewStart + delta, disableSnap), 0, previewEnd);
                        break;

                    case RangeDragMode.ResizeEnd:
                        previewEnd = Math.Max(previewStart, snap(previewEnd + delta, disableSnap));
                        break;
                }

                updateVisual();
            }

            private void finishDrag(RangeDragMode mode)
            {
                if (dragMode != mode)
                    return;

                dragMode = RangeDragMode.None;
                rangeChanged(previewStart, previewEnd);
            }

            private void updateVisual()
            {
                double span = Math.Max(1, viewEnd - viewStart);
                RelativePositionAxes = Axes.X;
                RelativeSizeAxes = Axes.X;
                X = (float)((previewStart - viewStart) / span);
                Width = Math.Max(0.004f, (float)((previewEnd - previewStart) / span));
            }
        }

        private enum RangeDragMode
        {
            None,
            Move,
            ResizeStart,
            ResizeEnd,
        }
    }
}
