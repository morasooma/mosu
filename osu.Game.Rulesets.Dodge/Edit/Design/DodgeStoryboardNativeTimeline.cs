// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Commands;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Edit.Design
{
    /// <summary>
    /// Storyboard objects drawn directly inside the normal editor timeline.
    /// Time-positioned children use the same <see cref="TimelinePart"/> mapping as Compose.
    /// </summary>
    internal partial class DodgeStoryboardTimeline : Container
    {
        private static readonly string[] layer_order = { "Background", "Fail", "Pass", "Foreground", "Overlay" };

        private readonly EditorBeatmap beatmap;
        private readonly EditorClock editorClock;
        private readonly Container timelineContent;
        private Container commandContent = null!;
        private readonly List<StoryboardCommandMarker> commandMarkers = new List<StoryboardCommandMarker>();

        private StoryboardBlueprintContainer? blueprintContainer;
        private StoryboardSprite? selectedSprite;
        private IStoryboardCommand? selectedCommand;
        private OverlayColourProvider? colourProvider;

        [Resolved]
        private Timeline editorTimeline { get; set; } = null!;

        public Action<double, double>? ObjectRangeChanged;
        public Action<IStoryboardCommand, double, double>? CommandRangeChanged;
        public Action<IStoryboardCommand>? CommandSelected;
        public Action<StoryboardLayer, StoryboardSprite>? SpriteSelected;
        public Action<IReadOnlyList<(StoryboardLayer Layer, StoryboardSprite Sprite)>>? SpritesDeleteRequested;

        public StoryboardSprite? SelectedSprite => selectedSprite;
        public IStoryboardCommand? SelectedCommand => selectedCommand;

        public DodgeStoryboardTimeline(EditorBeatmap beatmap, EditorClock editorClock)
        {
            this.beatmap = beatmap;
            this.editorClock = editorClock;
            RelativeSizeAxes = Axes.Both;
            InternalChild = timelineContent = new Container { RelativeSizeAxes = Axes.Both };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            colourProvider = colours;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            rebuild();
        }

        public void SetSelection(StoryboardSprite? sprite)
        {
            bool objectsMatch = storyboardObjectsMatch();

            if (ReferenceEquals(selectedSprite, sprite) && objectsMatch)
            {
                blueprintContainer?.SetSelection(sprite);
                return;
            }

            selectedSprite = sprite;
            selectedCommand = null;

            if (objectsMatch)
            {
                blueprintContainer?.SetSelection(sprite);
                rebuildCommandMarkers();
            }
            else
                rebuild();
        }

        private bool storyboardObjectsMatch()
        {
            if (blueprintContainer == null)
                return false;

            StoryboardSprite[] sprites = beatmap.Storyboard.Layers
                                               .SelectMany(layer => layer.Elements.OfType<StoryboardSprite>())
                                               .ToArray();
            return blueprintContainer.ItemCount == sprites.Length && sprites.All(blueprintContainer.Contains);
        }

        public void Refresh() => rebuild();

        public void RestoreCommandSelection(IStoryboardCommand command)
        {
            if (selectedSprite?.Commands.AllCommands.Any(candidate => ReferenceEquals(candidate, command)) != true)
                return;

            selectCommand(command, false);
        }

        private void rebuild()
        {
            if (colourProvider == null)
                return;

            timelineContent.Clear();
            commandMarkers.Clear();
            var items = new List<(StoryboardLayer Layer, StoryboardSprite Sprite, float Y)>();

            for (int layerIndex = 0; layerIndex < layer_order.Length; layerIndex++)
            {
                StoryboardLayer layer = beatmap.Storyboard.GetLayer(layer_order[layerIndex]);
                float laneY = 5 + layerIndex * 14;

                if (layerIndex > 0)
                {
                    timelineContent.Add(new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Y = laneY - 2,
                        Height = 1,
                        Colour = Color4.White,
                        Alpha = 0.07f,
                    });
                }

                foreach (StoryboardSprite sprite in layer.Elements.OfType<StoryboardSprite>())
                    items.Add((layer, sprite, laneY));
            }

            blueprintContainer = new StoryboardBlueprintContainer(
                items,
                colourProvider,
                screenDeltaToTime,
                onSelected: (layer, sprite) =>
                {
                    if (!ReferenceEquals(selectedSprite, sprite))
                        selectedCommand = null;

                    selectedSprite = sprite;
                    SpriteSelected?.Invoke(layer, sprite);
                    rebuildCommandMarkers();
                },
                onRangeChanged: (layer, sprite, start, end) =>
                {
                    if (!ReferenceEquals(selectedSprite, sprite))
                        SpriteSelected?.Invoke(layer, sprite);

                    selectedSprite = sprite;
                    ObjectRangeChanged?.Invoke(start, end);
                },
                onDelete: sprites => SpritesDeleteRequested?.Invoke(
                    sprites.Select(sprite => (
                               Layer: beatmap.Storyboard.Layers.First(layer => layer.Elements.Contains(sprite)),
                               Sprite: sprite))
                           .ToArray()))
            {
                RelativeSizeAxes = Axes.Both,
                Depth = -1,
            };
            timelineContent.Add(blueprintContainer);
            timelineContent.Add(new TimelinePart<Drawable>(
                commandContent = new Container { RelativeSizeAxes = Axes.Both })
            {
                RelativeSizeAxes = Axes.Both,
                Depth = -2,
            });
            blueprintContainer.SetSelection(selectedSprite);
            rebuildCommandMarkers();
        }

        private void rebuildCommandMarkers()
        {
            foreach (StoryboardCommandMarker marker in commandMarkers)
                commandContent.Remove(marker, true);
            commandMarkers.Clear();

            if (selectedSprite == null || colourProvider == null)
                return;

            StoryboardLayer? selectedLayer = beatmap.Storyboard.Layers.FirstOrDefault(
                layer => layer.Elements.Contains(selectedSprite));
            int selectedLayerIndex = selectedLayer == null
                ? 3
                : Math.Max(0, Array.IndexOf(layer_order, selectedLayer.Name));
            float markerY = 5 + selectedLayerIndex * 14;

            foreach (IStoryboardCommand command in selectedSprite.Commands.AllCommands)
            {
                var marker = new StoryboardCommandMarker(
                    command,
                    markerY,
                    ReferenceEquals(command, selectedCommand),
                    colourProvider,
                    () => selectCommand(command, true));
                commandMarkers.Add(marker);
                commandContent.Add(marker);
            }
        }

        private double screenDeltaToTime(float screenDeltaX)
        {
            double visibleRange = editorTimeline.VisibleRange;
            if (visibleRange <= 0)
                visibleRange = DodgeStoryboardEditing.DEFAULT_TIMELINE_SPAN;

            return screenDeltaX / Math.Max(1, editorTimeline.DrawWidth) * visibleRange;
        }

        private void selectCommand(IStoryboardCommand command, bool seek)
        {
            selectedCommand = command;
            CommandSelected?.Invoke(command);

            foreach (StoryboardCommandMarker marker in commandMarkers)
                marker.Selected = ReferenceEquals(marker.Command, command);

            if (seek)
                editorClock.Seek(Math.Clamp(command.StartTime, 0, editorClock.TrackLength));
        }

        internal partial class StoryboardBlueprintContainer : BlueprintContainer<StoryboardSprite>
        {
            private readonly Dictionary<StoryboardSprite, (StoryboardLayer Layer, float Y)> itemInfo;
            private readonly OverlayColourProvider colours;
            private readonly Func<float, double> screenDeltaToTime;
            private readonly Action<StoryboardLayer, StoryboardSprite> onSelected;
            private readonly Action<StoryboardLayer, StoryboardSprite, double, double> onRangeChanged;
            private readonly Action<IReadOnlyList<StoryboardSprite>> onDelete;

            [Resolved]
            private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

            [Resolved]
            private EditorClock editorClock { get; set; } = null!;

            [Resolved]
            private Timeline editorTimeline { get; set; } = null!;

            public StoryboardBlueprintContainer(
                IEnumerable<(StoryboardLayer Layer, StoryboardSprite Sprite, float Y)> items,
                OverlayColourProvider colours,
                Func<float, double> screenDeltaToTime,
                Action<StoryboardLayer, StoryboardSprite> onSelected,
                Action<StoryboardLayer, StoryboardSprite, double, double> onRangeChanged,
                Action<IReadOnlyList<StoryboardSprite>> onDelete)
            {
                itemInfo = items.ToDictionary(item => item.Sprite, item => (item.Layer, item.Y));
                this.colours = colours;
                this.screenDeltaToTime = screenDeltaToTime;
                this.onSelected = onSelected;
                this.onRangeChanged = onRangeChanged;
                this.onDelete = onDelete;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                foreach (StoryboardSprite sprite in itemInfo.Keys)
                    AddBlueprintFor(sprite);
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                => editorTimeline.ReceivePositionalInputAt(screenSpacePos);

            public bool Contains(StoryboardSprite? sprite) => sprite != null && itemInfo.ContainsKey(sprite);
            public int ItemCount => itemInfo.Count;

            public void SetSelection(StoryboardSprite? sprite)
            {
                if (sprite != null && !itemInfo.ContainsKey(sprite))
                    return;

                if (SelectedItems.Count == (sprite == null ? 0 : 1) &&
                    (sprite == null || ReferenceEquals(SelectedItems.Single(), sprite)))
                    return;

                SelectedItems.Clear();
                if (sprite != null)
                    SelectedItems.Add(sprite);
            }

            protected override SelectionBlueprint<StoryboardSprite>? CreateBlueprintFor(StoryboardSprite sprite)
            {
                (StoryboardLayer layer, float y) = itemInfo[sprite];
                return new StoryboardObjectBlueprint(
                    sprite,
                    layer,
                    y,
                    colours,
                    screenDeltaToTime,
                    (start, end) => onRangeChanged(layer, sprite, start, end));
            }

            protected override SelectionHandler<StoryboardSprite> CreateSelectionHandler()
                => new StoryboardSelectionHandler(onDelete);

            protected override SelectionBlueprintContainer CreateSelectionBlueprintContainer()
                => new StoryboardTimelineSelectionBlueprintContainer { RelativeSizeAxes = Axes.Both };

            protected override void OnBlueprintSelected(SelectionBlueprint<StoryboardSprite> blueprint)
            {
                base.OnBlueprintSelected(blueprint);
                (StoryboardLayer layer, _) = itemInfo[blueprint.Item];
                onSelected(layer, blueprint.Item);
            }

            protected override bool TryMoveBlueprints(
                DragEvent e,
                IList<(SelectionBlueprint<StoryboardSprite> blueprint, Vector2[] originalSnapPositions)> blueprints)
            {
                if (blueprints.Count == 0)
                    return false;

                double delta = screenDeltaToTime(e.ScreenSpaceMousePosition.X - e.ScreenSpaceMouseDownPosition.X);
                var reference = (StoryboardObjectBlueprint)blueprints[0].blueprint;

                if (!e.AltPressed && editorClock.TrackLength > 0)
                {
                    double snappedDelta = beatSnapProvider.SnapTime(reference.OriginalStart + delta) - reference.OriginalStart;
                    if (Math.Abs(snappedDelta) > 0.001)
                        delta = snappedDelta;
                }

                delta = Math.Max(delta, -blueprints.Min(item => ((StoryboardObjectBlueprint)item.blueprint).OriginalStart));

                foreach ((SelectionBlueprint<StoryboardSprite> blueprint, _) in blueprints)
                    ((StoryboardObjectBlueprint)blueprint).PreviewMove(delta);

                return SelectionHandler.HandleMovement(new MoveSelectionEvent<StoryboardSprite>(reference, new Vector2((float)delta, 0)));
            }

            protected override void DragOperationCompleted()
            {
                base.DragOperationCompleted();

                foreach (StoryboardObjectBlueprint blueprint in SelectionHandler.SelectedBlueprints.OfType<StoryboardObjectBlueprint>().ToArray())
                {
                    if (!blueprint.HasPendingRangeChange)
                        continue;

                    (double start, double end) = blueprint.PreviewRange;
                    (StoryboardLayer layer, _) = itemInfo[blueprint.Item];
                    onRangeChanged(layer, blueprint.Item, start, end);
                    break;
                }
            }

            protected override void SelectAll()
            {
                SelectedItems.Clear();
                SelectedItems.AddRange(itemInfo.Keys);
            }

            private partial class StoryboardSelectionHandler : SelectionHandler<StoryboardSprite>
            {
                private readonly Action<IReadOnlyList<StoryboardSprite>> deleteItems;

                public StoryboardSelectionHandler(Action<IReadOnlyList<StoryboardSprite>> deleteItems)
                {
                    this.deleteItems = deleteItems;
                }

                public override bool HandleMovement(MoveSelectionEvent<StoryboardSprite> moveEvent) => true;

                protected override void OnSelectionChanged()
                {
                    base.OnSelectionChanged();
                    SelectionBox.Hide();
                }

                protected override void DeleteItems(IEnumerable<StoryboardSprite> items)
                {
                    StoryboardSprite[] sprites = items.ToArray();
                    if (sprites.Length > 0)
                        deleteItems(sprites);
                }
            }

            private partial class StoryboardTimelineSelectionBlueprintContainer : SelectionBlueprintContainer
            {
                protected override Container<SelectionBlueprint<StoryboardSprite>> Content { get; }

                public StoryboardTimelineSelectionBlueprintContainer()
                {
                    AddInternal(new TimelinePart<SelectionBlueprint<StoryboardSprite>>(
                        Content = new Container<SelectionBlueprint<StoryboardSprite>>
                        {
                            RelativeSizeAxes = Axes.Both,
                        })
                    {
                        RelativeSizeAxes = Axes.Both,
                    });
                }
            }
        }

        internal partial class StoryboardObjectBlueprint : SelectionBlueprint<StoryboardSprite>
        {
            private const float handle_width = 7;

            private readonly Func<float, double> screenDeltaToTime;
            private readonly Action<double, double> rangeChanged;
            private readonly Container bar;
            private readonly Box fill;
            private readonly TimelineRangeHandle startHandle;
            private readonly TimelineRangeHandle endHandle;
            private readonly Color4 normalColour;

            private double previewStart;
            private double previewEnd;
            private double resizeStart;
            private double resizeEnd;

            [Resolved]
            private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

            [Resolved]
            private EditorClock editorClock { get; set; } = null!;

            public double OriginalStart => Math.Max(0, Item.StartTime);
            public bool HasPendingRangeChange => Math.Abs(previewStart - OriginalStart) > 0.001 ||
                                                 Math.Abs(previewEnd - originalEnd) > 0.001;
            public (double Start, double End) PreviewRange => (previewStart, previewEnd);
            private double originalEnd => Math.Max(OriginalStart + DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION, Item.EndTimeForDisplay);

            public override Vector2 ScreenSpaceSelectionPoint => bar.ScreenSpaceDrawQuad.Centre;
            public override osu.Framework.Graphics.Primitives.Quad SelectionQuad => bar.ScreenSpaceDrawQuad;

            public StoryboardObjectBlueprint(
                StoryboardSprite sprite,
                StoryboardLayer layer,
                float y,
                OverlayColourProvider colours,
                Func<float, double> screenDeltaToTime,
                Action<double, double> rangeChanged)
                : base(sprite)
            {
                this.screenDeltaToTime = screenDeltaToTime;
                this.rangeChanged = rangeChanged;
                normalColour = colourForLayer(layer.Name, colours);
                previewStart = OriginalStart;
                previewEnd = originalEnd;
                Name = $"Storyboard timeline object: {sprite.Path}";
                RelativePositionAxes = Axes.X;
                RelativeSizeAxes = Axes.X;
                Y = y;
                Height = 11;

                bar = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 3,
                    Children = new Drawable[]
                    {
                        fill = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = normalColour,
                            Alpha = 0.78f,
                        },
                        new TimelineMoveArea(beginMove, updateMove, finishMove)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                };
                startHandle = new TimelineRangeHandle("Storyboard start handle", Anchor.CentreLeft, beginResize, updateStart, finishResize);
                endHandle = new TimelineRangeHandle("Storyboard end handle", Anchor.CentreRight, beginResize, updateEnd, finishResize);
                InternalChildren = new Drawable[] { bar, startHandle, endHandle };

                updateVisual();
                updateSelectionVisual();
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                => bar.ScreenSpaceDrawQuad.AABBFloat.Inflate(new Vector2(3)).Contains(screenSpacePos);

            protected override void OnSelected()
            {
                updateSelectionVisual();
            }

            protected override void OnDeselected()
            {
                updateSelectionVisual();
            }

            protected override bool OnHover(HoverEvent e)
            {
                fill.Alpha = 1;
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateSelectionVisual();
                base.OnHoverLost(e);
            }

            public void PreviewMove(double delta)
            {
                previewStart = OriginalStart + delta;
                previewEnd = originalEnd + delta;
                updateVisual();
            }

            private void beginMove()
            {
                resizeStart = OriginalStart;
                resizeEnd = originalEnd;
            }

            private void updateMove(DragEvent e)
            {
                double delta = screenDeltaToTime(e.ScreenSpaceMousePosition.X - e.ScreenSpaceMouseDownPosition.X);
                if (!e.AltPressed && editorClock.TrackLength > 0)
                {
                    double snappedDelta = beatSnapProvider.SnapTime(resizeStart + delta) - resizeStart;
                    if (Math.Abs(snappedDelta) > 0.001)
                        delta = snappedDelta;
                }

                delta = Math.Max(delta, -resizeStart);
                previewStart = resizeStart + delta;
                previewEnd = resizeEnd + delta;
                updateVisual();
            }

            private void finishMove()
            {
                if (HasPendingRangeChange)
                    rangeChanged(previewStart, previewEnd);
            }

            private void beginResize()
            {
                resizeStart = previewStart;
                resizeEnd = previewEnd;
            }

            private void updateStart(DragEvent e)
            {
                double proposed = resizeStart + screenDeltaToTime(e.ScreenSpaceMousePosition.X - e.ScreenSpaceMouseDownPosition.X);
                if (!e.AltPressed && editorClock.TrackLength > 0)
                    proposed = beatSnapProvider.SnapTime(proposed);
                previewStart = Math.Clamp(proposed, 0, resizeEnd - DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION);
                updateVisual();
            }

            private void updateEnd(DragEvent e)
            {
                double proposed = resizeEnd + screenDeltaToTime(e.ScreenSpaceMousePosition.X - e.ScreenSpaceMouseDownPosition.X);
                if (!e.AltPressed && editorClock.TrackLength > 0)
                    proposed = beatSnapProvider.SnapTime(proposed);
                previewEnd = Math.Max(resizeStart + DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION, proposed);
                updateVisual();
            }

            private void finishResize()
            {
                if (HasPendingRangeChange)
                    rangeChanged(previewStart, previewEnd);
            }

            private void updateVisual()
            {
                X = (float)previewStart;
                Width = (float)Math.Max(DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION, previewEnd - previewStart);
            }

            private void updateSelectionVisual()
            {
                fill.Colour = IsSelected ? Color4.White : normalColour;
                fill.Alpha = IsSelected || IsHovered ? 1 : 0.78f;
                startHandle.Alpha = endHandle.Alpha = IsSelected ? 1 : 0;
            }

            private static Color4 colourForLayer(string layerName, OverlayColourProvider colours) => layerName switch
            {
                "Background" => colours.Background1,
                "Fail" => colours.Colour1,
                "Pass" => colours.Colour2,
                "Foreground" => colours.Colour3,
                "Overlay" => colours.Highlight1,
                _ => colours.Content2,
            };

            private partial class TimelineRangeHandle : CompositeDrawable
            {
                private readonly Action begin;
                private readonly Action<DragEvent> update;
                private readonly Action finish;
                private readonly Box box;

                public TimelineRangeHandle(string name, Anchor anchor, Action begin, Action<DragEvent> update, Action finish)
                {
                    this.begin = begin;
                    this.update = update;
                    this.finish = finish;
                    Name = name;
                    Anchor = anchor;
                    Origin = Anchor.Centre;
                    Size = new Vector2(handle_width * 2, 17);
                    InternalChild = box = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0.65f,
                    };
                }

                protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;

                protected override bool OnClick(ClickEvent e) => e.Button == MouseButton.Left;

                protected override bool OnDragStart(DragStartEvent e)
                {
                    if (e.Button != MouseButton.Left)
                        return false;

                    begin();
                    box.Alpha = 1;
                    return true;
                }

                protected override void OnDrag(DragEvent e)
                {
                    base.OnDrag(e);
                    update(e);
                }

                protected override void OnDragEnd(DragEndEvent e)
                {
                    box.Alpha = 0.65f;
                    finish();
                    base.OnDragEnd(e);
                }
            }

            private partial class TimelineMoveArea : CompositeDrawable
            {
                private readonly Action begin;
                private readonly Action<DragEvent> update;
                private readonly Action finish;

                public TimelineMoveArea(Action begin, Action<DragEvent> update, Action finish)
                {
                    this.begin = begin;
                    this.update = update;
                    this.finish = finish;
                    Name = "Storyboard timeline move area";
                }

                protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;

                protected override bool OnClick(ClickEvent e) => e.Button == MouseButton.Left;

                protected override bool OnDragStart(DragStartEvent e)
                {
                    if (e.Button != MouseButton.Left)
                        return false;

                    begin();
                    return true;
                }

                protected override void OnDrag(DragEvent e)
                {
                    base.OnDrag(e);
                    update(e);
                }

                protected override void OnDragEnd(DragEndEvent e)
                {
                    finish();
                    base.OnDragEnd(e);
                }
            }
        }

        private partial class StoryboardCommandMarker : CompositeDrawable
        {
            private readonly Box line;
            private readonly Action select;
            private readonly OverlayColourProvider colourProvider;
            private bool selected;

            public readonly IStoryboardCommand Command;

            public bool Selected
            {
                get => selected;
                set
                {
                    selected = value;
                    line.Colour = value ? Color4.White : colourProvider.Colour2;
                    line.Alpha = value ? 1 : 0.75f;
                }
            }

            public StoryboardCommandMarker(
                IStoryboardCommand command,
                float y,
                bool selected,
                OverlayColourProvider colourProvider,
                Action select)
            {
                Command = command;
                this.colourProvider = colourProvider;
                this.select = select;

                RelativePositionAxes = Axes.X;
                X = (float)command.StartTime;
                Y = y - 2;
                Width = 7;
                Height = 15;
                Origin = Anchor.TopCentre;
                Depth = float.MinValue;
                InternalChild = line = new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(3, 15),
                };
                Selected = selected;
            }

            protected override bool OnClick(ClickEvent e)
            {
                select();
                return true;
            }

            protected override bool OnHover(HoverEvent e)
            {
                line.ScaleTo(1.5f, 80);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                line.ScaleTo(1, 120);
                base.OnHoverLost(e);
            }
        }
    }
}
