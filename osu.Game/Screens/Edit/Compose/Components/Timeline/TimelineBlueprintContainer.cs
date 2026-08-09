// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    [Cached]
    internal partial class TimelineBlueprintContainer : EditorBlueprintContainer
    {
        [Resolved(CanBeNull = true)]
        private Timeline timeline { get; set; }

        [Resolved(CanBeNull = true)]
        private EditorClock editorClock { get; set; }

        private Bindable<HitObject> placement;
        private SelectionBlueprint<HitObject> placementBlueprint;

        private bool hitObjectDragged;
        private readonly IEditorTimelineLayoutProvider laneLayout;
        private TimelineLaneOverlay laneOverlay;

        /// <remarks>
        /// Positional input must be received outside the container's bounds,
        /// in order to handle timeline blueprints which are stacked offscreen.
        /// </remarks>
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => timeline.ReceivePositionalInputAt(screenSpacePos);

        public TimelineBlueprintContainer(HitObjectComposer composer)
            : base(composer)
        {
            laneLayout = composer as IEditorTimelineLayoutProvider;

            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            Height = laneLayout == null ? 0.6f : 0.9f;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(new SelectableAreaBackground
            {
                Colour = Color4.Black,
                Depth = float.MaxValue,
                Blending = BlendingParameters.Additive,
            });

            if (laneLayout != null)
                AddInternal(laneOverlay = new TimelineLaneOverlay(laneLayout));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            placement = Beatmap.PlacementObject.GetBoundCopy();
            placement.ValueChanged += placementChanged;
        }

        private void placementChanged(ValueChangedEvent<HitObject> obj)
        {
            if (obj.NewValue == null)
            {
                if (placementBlueprint != null)
                {
                    SelectionBlueprints.Remove(placementBlueprint, true);
                    placementBlueprint = null;
                }
            }
            else
            {
                placementBlueprint = CreateBlueprintFor(obj.NewValue).AsNonNull();

                // just to show the border. using the selection state doesn't seem to backfire.
                // if it does then we'll probably want to just make `new` object above rather than rely on `CreateBlueprintFor`.
                placementBlueprint.State = SelectionState.Selected;

                // TODO: this is out of order, causing incorrect stacking height.
                SelectionBlueprints.Add(placementBlueprint);
            }
        }

        protected override SelectionBlueprintContainer CreateSelectionBlueprintContainer() => new TimelineSelectionBlueprintContainer { RelativeSizeAxes = Axes.Both };

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!base.ReceivePositionalInputAt(e.ScreenSpaceMouseDownPosition))
                return false;

            return base.OnDragStart(e);
        }

        protected override bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<HitObject> blueprint, Vector2[] originalSnapPositions)> blueprints)
        {
            Vector2 distanceTravelled = e.ScreenSpaceMousePosition - e.ScreenSpaceMouseDownPosition;

            // The final movement position, relative to movementBlueprintOriginalPosition.
            Vector2 movePosition = blueprints.First().originalSnapPositions.First() + distanceTravelled;

            // Retrieve a snapped position.
            var result = timeline?.FindSnappedPositionAndTime(movePosition) ?? new SnapResult(movePosition, null);

            var referenceBlueprint = blueprints.First().blueprint;
            bool moved = SelectionHandler.HandleMovement(new MoveSelectionEvent<HitObject>(referenceBlueprint, result.ScreenSpacePosition - referenceBlueprint.ScreenSpaceSelectionPoint));
            if (moved)
                ApplySnapResultTime(result, referenceBlueprint.Item.StartTime);
            return moved;
        }

        private float dragTimeAccumulated;

        protected override void Update()
        {
            if (IsDragged || hitObjectDragged)
                handleScrollViaDrag();
            else
                dragTimeAccumulated = 0;

            if (Composer != null && timeline != null)
            {
                Composer.Playfield.PastLifetimeExtension = timeline.VisibleRange / 2;
                Composer.Playfield.FutureLifetimeExtension = timeline.VisibleRange / 2;
            }

            base.Update();

            updateLaneVisibleBlueprints();
            updateSamplePointContractedState();
            updateStacking();
        }

        private void updateLaneVisibleBlueprints()
        {
            if (laneLayout == null || timeline == null || editorClock == null)
                return;

            double halfRange = timeline.VisibleRange / 2;
            double visibleStart = editorClock.CurrentTime - halfRange;
            double visibleEnd = editorClock.CurrentTime + halfRange;

            foreach (HitObject hitObject in Beatmap.HitObjects)
            {
                if (hitObject.StartTime > visibleEnd || laneLayout.GetTimelineDisplayEndTime(hitObject) < visibleStart)
                    continue;

                AddBlueprintFor(hitObject);
            }

            // EditorBlueprintContainer initially creates blueprints for every
            // drawable exposed by the composer, and HitObjectAdded does the same
            // for newly-added objects. Iterate the actual blueprint collection
            // here rather than a separately-maintained subset, otherwise objects
            // which started outside the visible range never get removed and still
            // consume vertical rows when their layer is expanded.
            foreach (HitObject hitObject in SelectionBlueprints.OfType<LaneTimelineHitObjectBlueprint>()
                                                               .Select(blueprint => blueprint.Item)
                                                               .Where(hitObject => Beatmap.HitObjects.Contains(hitObject))
                                                               .Distinct()
                                                               .ToArray())
            {
                bool stillVisible = hitObject.StartTime <= visibleEnd
                                    && laneLayout.GetTimelineDisplayEndTime(hitObject) >= visibleStart;

                // Dodge uses direct (non-pooled) drawables, so Composer.HitObjects
                // contains every object in the beatmap. Retaining blueprints based
                // on that collection made every object remain on the timeline after
                // a backwards seek. Keep only an explicitly selected object outside
                // the current timeline range.
                if (stillVisible || Beatmap.SelectedHitObjects.Contains(hitObject))
                    continue;

                RemoveBlueprintFor(hitObject);
            }
        }

        public Bindable<bool> SamplePointContracted = new Bindable<bool>();

        private void updateSamplePointContractedState()
        {
            const double absolute_minimum_gap = 31; // assumes single letter bank name for default banks
            double minimumGap = absolute_minimum_gap;

            if (timeline == null || editorClock == null)
                return;

            // Find the smallest time gap between any two sample point pieces
            double smallestTimeGap = double.PositiveInfinity;
            double lastTime = double.PositiveInfinity;

            // The blueprints are ordered in reverse chronological order
            foreach (var selectionBlueprint in SelectionBlueprints)
            {
                var hitObject = selectionBlueprint.Item;

                // Only check the hit objects which are visible in the timeline
                // SelectionBlueprints can contain hit objects which are not visible in the timeline due to selection keeping them alive
                if (hitObject.StartTime > editorClock.CurrentTime + timeline.VisibleRange / 2)
                    continue;

                if (hitObject.GetEndTime() < editorClock.CurrentTime - timeline.VisibleRange / 2)
                    break;

                for (int i = 0; i < hitObject.Samples.Count; i++)
                {
                    var sample = hitObject.Samples[i];

                    if (!HitSampleInfo.ALL_BANKS.Contains(sample.Bank))
                        minimumGap = Math.Max(minimumGap, absolute_minimum_gap + sample.Bank.Length * 3);
                }

                if (hitObject is IHasRepeats hasRepeats)
                {
                    smallestTimeGap = Math.Min(smallestTimeGap, hasRepeats.Duration / hasRepeats.SpanCount() / 2);

                    for (int i = 0; i < hasRepeats.NodeSamples.Count; i++)
                    {
                        var node = hasRepeats.NodeSamples[i];

                        for (int j = 0; j < node.Count; j++)
                        {
                            var sample = node[j];

                            if (!HitSampleInfo.ALL_BANKS.Contains(sample.Bank))
                                minimumGap = Math.Max(minimumGap, absolute_minimum_gap + sample.Bank.Length * 3);
                        }
                    }
                }

                double gap = lastTime - hitObject.GetEndTime();

                // If the gap is less than 1ms, we can assume that the objects are stacked on top of each other
                // Contracting doesn't make sense in this case
                if (gap > 1 && gap < smallestTimeGap)
                    smallestTimeGap = gap;

                lastTime = hitObject.StartTime;
            }

            double smallestAbsoluteGap = ((TimelineSelectionBlueprintContainer)SelectionBlueprints).ContentRelativeToAbsoluteFactor.X * smallestTimeGap;
            SamplePointContracted.Value = smallestAbsoluteGap < minimumGap;
        }

        private readonly Stack<HitObject> currentConcurrentObjects = new Stack<HitObject>();

        private void updateStacking()
        {
            if (laneLayout != null)
            {
                updateLaneLayout();
                return;
            }

            // because only blueprints of objects which are alive (via pooling) are displayed in the timeline, it's feasible to do this every-update.

            const int stack_offset = 5;

            // after the stack gets this tall, we can presume there is space underneath to draw subsequent blueprints.
            const int stack_reset_count = 3;

            currentConcurrentObjects.Clear();

            for (int i = SelectionBlueprints.Count - 1; i >= 0; i--)
            {
                var b = SelectionBlueprints[i];

                // remove objects from the stack as long as their end time is in the past.
                while (currentConcurrentObjects.TryPeek(out HitObject hitObject))
                {
                    if (Precision.AlmostBigger(hitObject.GetEndTime(), b.Item.StartTime, 1))
                        break;

                    currentConcurrentObjects.Pop();
                }

                // if the stack gets too high, we should have space below it to display the next batch of objects.
                // importantly, we only do this if time has incremented, else a stack of hitobjects all at the same time value would start to overlap themselves.
                if (currentConcurrentObjects.TryPeek(out HitObject h) && !Precision.AlmostEquals(h.StartTime, b.Item.StartTime, 1))
                {
                    if (currentConcurrentObjects.Count >= stack_reset_count)
                        currentConcurrentObjects.Clear();
                }

                b.Y = -(stack_offset * currentConcurrentObjects.Count);

                currentConcurrentObjects.Push(b.Item);
            }
        }

        private void updateLaneLayout()
        {
            LaneLayoutGroup[] groups = SelectionBlueprints
                                       .OfType<LaneTimelineHitObjectBlueprint>()
                                       .GroupBy(blueprint => new TimelineGroup(
                                           blueprint.LaneIndex,
                                           laneLayout.GetTimelineGroupKey(blueprint.Item) ?? blueprint.Item))
                                       .Select(group => new LaneLayoutGroup(group.Key.Lane, group.ToArray()))
                                       .ToArray();

            int[] selectedLanes = groups.Where(group => group.Blueprints.Any(blueprint => blueprint.IsSelected))
                                        .Select(group => group.LaneIndex)
                                        .Distinct()
                                        .ToArray();
            int activeLane = selectedLanes.Length == 1 ? selectedLanes[0] : -1;

            int[] laneRowCounts = new int[laneLayout.TimelineLaneCount];

            for (int lane = 0; lane < laneRowCounts.Length; lane++)
            {
                bool expanded = lane == activeLane;
                bool visible = activeLane < 0 || lane == activeLane;

                foreach (LaneLayoutGroup group in groups.Where(group => group.LaneIndex == lane))
                    group.Expanded = expanded;

                if (!visible)
                {
                    laneRowCounts[lane] = 0;
                    continue;
                }

                if (!expanded)
                {
                    laneRowCounts[lane] = 1;
                    continue;
                }

                // When a layer is selected every object receives its own row,
                // even when its time span does not overlap another object. The
                // complete layer is then distributed over the full timeline
                // height, making every item an independent click target.
                LaneTimelineHitObjectBlueprint[] laneBlueprints = groups.Where(group => group.LaneIndex == lane)
                                                                         .SelectMany(group => group.Blueprints)
                                                                         .OrderBy(blueprint => blueprint.Item.StartTime)
                                                                         .ThenBy(blueprint => blueprint.TimelineEndTime)
                                                                         .ToArray();

                for (int row = 0; row < laneBlueprints.Length; row++)
                    laneBlueprints[row].Row = row;

                laneRowCounts[lane] = Math.Max(1, laneBlueprints.Length);
            }

            float[] laneCentres = new float[laneRowCounts.Length];
            float[] laneRowSteps = new float[laneRowCounts.Length];
            int visibleLaneCount = laneRowCounts.Count(rows => rows > 0);
            bool hasActiveLane = activeLane >= 0;
            float laneBandHeight = visibleLaneCount == 0 ? 0 : DrawHeight / visibleLaneCount;
            int visibleLaneIndex = 0;

            for (int lane = 0; lane < laneRowCounts.Length; lane++)
            {
                if (laneRowCounts[lane] == 0)
                {
                    laneCentres[lane] = float.NaN;
                    continue;
                }

                if (hasActiveLane)
                {
                    // A selected object turns its type into the only visible
                    // layer. Place that layer at the visual centre instead of
                    // keeping its original (for example, bullet = top) slot.
                    laneCentres[lane] = 0;
                }
                else
                {
                    laneCentres[lane] = -DrawHeight / 2 + laneBandHeight * (visibleLaneIndex + 0.5f);
                    visibleLaneIndex++;
                }

                // The selected type owns the entire timeline height. Use one
                // evenly-sized vertical row for each overlapping interval, so
                // a dense layer remains directly clickable instead of becoming
                // a compact stack at its old lane position.
                laneRowSteps[lane] = hasActiveLane ? DrawHeight / laneRowCounts[lane] : 0;
            }

            laneOverlay.SetLaneLayout(laneCentres);

            foreach (LaneLayoutGroup group in groups)
            {
                bool laneVisible = laneRowCounts[group.LaneIndex] > 0;
                float rowStep = laneRowSteps[group.LaneIndex];

                for (int i = 0; i < group.Blueprints.Length; i++)
                {
                    LaneTimelineHitObjectBlueprint blueprint = group.Blueprints[i];
                    float rowY = !laneVisible
                        ? 0
                        : hasActiveLane
                            ? -DrawHeight / 2 + (blueprint.Row + 0.5f) * rowStep
                            : laneCentres[group.LaneIndex];
                    blueprint.SetGroupState(group.Blueprints.Length, i, group.Expanded, laneVisible, rowStep);
                    blueprint.Y += rowY;
                }
            }

        }

        protected override SelectionHandler<HitObject> CreateSelectionHandler() => new TimelineSelectionHandler(laneLayout);

        protected override SelectionBlueprint<HitObject> CreateBlueprintFor(HitObject item)
        {
            if (laneLayout != null)
            {
                return new LaneTimelineHitObjectBlueprint(item, laneLayout.GetTimelineLane(item), laneLayout.GetTimelineDisplayEndTime)
                {
                    OnDragHandled = e => hitObjectDragged = e != null,
                };
            }

            return new TimelineHitObjectBlueprint(item)
            {
                OnDragHandled = e => hitObjectDragged = e != null,
            };
        }

        protected sealed override DragBox CreateDragBox() => new TimelineDragBox();

        protected override void UpdateSelectionFromDragBox(HashSet<HitObject> selectionBeforeDrag)
        {
            Composer.BlueprintContainer.CommitIfPlacementActive();

            var dragBox = (TimelineDragBox)DragBox;
            double minTime = dragBox.MinTime;
            double maxTime = dragBox.MaxTime;

            SelectedItems.RemoveAll(hitObject => !shouldBeSelected(hitObject));

            foreach (var hitObject in Beatmap.HitObjects.Except(SelectedItems).Where(shouldBeSelected))
            {
                Composer.Playfield.SetKeepAlive(hitObject, true);
                SelectedItems.Add(hitObject);
            }

            bool shouldBeSelected(HitObject hitObject)
            {
                if (selectionBeforeDrag.Contains(hitObject))
                    return true;

                double midTime = (hitObject.StartTime + hitObject.GetEndTime()) / 2;
                return minTime <= midTime && midTime <= maxTime;
            }
        }

        private void handleScrollViaDrag()
        {
            // The amount of time dragging before we reach maximum drag speed.
            const float time_ramp_multiplier = 5000;

            // A maximum drag speed to ensure things don't get out of hand.
            const float max_velocity = 10;

            if (timeline == null) return;

            var mousePos = timeline.ToLocalSpace(InputManager.CurrentState.Mouse.Position);

            // for better UX do not require the user to drag all the way to the edge and beyond to initiate a drag-scroll.
            // this is especially important in scenarios like fullscreen, where mouse confine will usually be on
            // and the user physically *won't be able to* drag beyond the edge of the timeline
            // (since its left edge is co-incident with the window edge).
            const float scroll_tolerance = 40;

            float leftBound = timeline.BoundingBox.TopLeft.X + scroll_tolerance;
            float rightBound = timeline.BoundingBox.TopRight.X - scroll_tolerance;

            float amount = 0;

            if (mousePos.X > rightBound)
                amount = mousePos.X - rightBound;
            else if (mousePos.X < leftBound)
                amount = mousePos.X - leftBound;

            if (amount == 0)
            {
                dragTimeAccumulated = 0;
                return;
            }

            amount = Math.Sign(amount) * Math.Min(max_velocity, MathF.Pow(Math.Clamp(Math.Abs(amount), 0, scroll_tolerance), 2));
            dragTimeAccumulated += (float)Clock.ElapsedFrameTime;

            timeline.ScrollBy(amount * (float)Clock.ElapsedFrameTime * Math.Min(1, dragTimeAccumulated / time_ramp_multiplier));
        }

        private partial class SelectableAreaBackground : CompositeDrawable
        {
            [Resolved]
            private OsuColour colours { get; set; }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            {
                float localY = ToLocalSpace(screenSpacePos).Y;
                return DrawRectangle.Top <= localY && DrawRectangle.Bottom >= localY;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.Both;
                Alpha = 0.1f;

                AddRangeInternal(new[]
                {
                    // fade out over intro time, outside the valid time bounds.
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 200,
                        Origin = Anchor.TopRight,
                        Colour = ColourInfo.GradientHorizontal(Color4.White.Opacity(0), Color4.White),
                    },
                    new Box
                    {
                        Colour = Color4.White,
                        RelativeSizeAxes = Axes.Both,
                    }
                });
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.FadeColour(colours.BlueLighter, 120, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.FadeColour(Color4.Black, 600, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }

        protected partial class TimelineSelectionBlueprintContainer : SelectionBlueprintContainer
        {
            protected override HitObjectOrderedSelectionContainer Content { get; }

            public Vector2 ContentRelativeToAbsoluteFactor => Content.RelativeToAbsoluteFactor;

            public TimelineSelectionBlueprintContainer()
            {
                AddInternal(new TimelinePart<SelectionBlueprint<HitObject>>(Content = new HitObjectOrderedSelectionContainer { RelativeSizeAxes = Axes.Both }) { RelativeSizeAxes = Axes.Both });
            }

            public override void ChangeChildDepth(SelectionBlueprint<HitObject> child, float newDepth)
            {
                // timeline blueprint container also contains a blueprint for current placement, if present
                // (see `placementChanged()` callback above).
                // because the current placement hitobject is generally going to be mutated during the placement,
                // it is possible for `Content`'s children to become unsorted when the user moves the placement around,
                // which can culminate in a critical failure when attempting to binary-search children here
                // using `HitObjectOrderedSelectionContainer`'s custom comparer.
                // thus, always force a re-sort of objects before attempting to change child depth to avoid this scenario.
                Content.Sort();
                base.ChangeChildDepth(child, newDepth);
            }
        }

        private readonly record struct TimelineGroup(int Lane, object Key);

        private sealed class LaneLayoutGroup
        {
            public readonly int LaneIndex;
            public readonly LaneTimelineHitObjectBlueprint[] Blueprints;
            public bool Expanded;
            public readonly double StartTime;
            public readonly double EndTime;

            public LaneLayoutGroup(int laneIndex, LaneTimelineHitObjectBlueprint[] blueprints)
            {
                LaneIndex = laneIndex;
                Blueprints = blueprints;
                StartTime = blueprints.Min(blueprint => blueprint.Item.StartTime);
                // Only the selected lane is row-packed, so use the same span that is actually
                // drawn on the timeline. Persistent arena states and ContinueUntilExit bullets
                // must count as overlaps while their bars are still visible.
                EndTime = blueprints.Max(blueprint => blueprint.TimelineEndTime);
            }
        }

        private partial class TimelineLaneOverlay : CompositeDrawable
        {
            private readonly List<OsuSpriteText> labels = new List<OsuSpriteText>();
            private readonly List<Box> separators = new List<Box>();

            public TimelineLaneOverlay(IEditorTimelineLayoutProvider layout)
            {
                RelativeSizeAxes = Axes.Both;
                Depth = -10;

                for (int i = 0; i < layout.TimelineLaneCount; i++)
                {
                    EditorTimelineLane lane = layout.GetTimelineLaneByIndex(i);

                    var label = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = lane.Name,
                        Font = OsuFont.GetFont(size: 9, weight: FontWeight.Bold),
                        Colour = lane.Colour,
                        Alpha = 0.65f,
                    };
                    labels.Add(label);
                    AddInternal(label);

                    if (i > 0)
                    {
                        var separator = new Box
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = 1,
                            Colour = Color4.White,
                            Alpha = 0.08f,
                        };
                        separators.Add(separator);
                        AddInternal(separator);
                    }
                }
            }

            public void SetLaneLayout(float[] laneCentres)
            {
                for (int lane = 0; lane < labels.Count; lane++)
                {
                    bool visible = !float.IsNaN(laneCentres[lane]);
                    labels[lane].Alpha = visible ? 0.65f : 0;
                    labels[lane].Position = new Vector2(5, visible ? laneCentres[lane] : 0);

                    if (lane == 0)
                        continue;

                    bool previousVisible = !float.IsNaN(laneCentres[lane - 1]);
                    separators[lane - 1].Y = visible && previousVisible
                        ? (laneCentres[lane - 1] + laneCentres[lane]) / 2
                        : 0;
                    separators[lane - 1].Alpha = visible && previousVisible ? 0.08f : 0;
                }
            }
        }
    }
}
