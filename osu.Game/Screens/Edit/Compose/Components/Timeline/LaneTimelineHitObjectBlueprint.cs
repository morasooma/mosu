// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using JetBrains.Annotations;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    /// <summary>
    /// Compact timeline representation used by rulesets which expose fixed
    /// editor lanes. Detailed handles only appear while hovered or selected.
    /// </summary>
    public partial class LaneTimelineHitObjectBlueprint : SelectionBlueprint<HitObject>
    {
        internal const float IDLE_HEIGHT = 5;
        private const float detailed_height = 12;
        // Compact rows should read as one expanded layer stack rather than
        // three widely separated timelines. The idle bars are 5px high, so a
        // 6px step leaves only a single pixel between neighbouring objects.
        internal const float GROUP_ROW_SPACING = 6;

        [UsedImplicitly]
        private readonly Bindable<double> startTime;

        private readonly Container bar;
        private readonly Circle startMarker;
        private readonly OsuSpriteText shortName;
        private readonly OsuSpriteText groupCount;
        private readonly TimelineHitObjectBlueprint.DragArea? durationHandle;
        private readonly Func<HitObject, double> getDisplayEndTime;
        private float expandedRowSpacing = GROUP_ROW_SPACING;
        private readonly float minimumMarkerWidth;
        private bool durationEditable;
        private bool canAdjustDuration => Item is IHasDuration
                                          && (Item is not IEditorTimelineEndTimeAdjustable adjustable || adjustable.CanAdjustEditorTimelineEndTime);

        public Action<DragEvent?>? OnDragHandled;

        public int LaneIndex { get; }
        public int GroupCount { get; private set; } = 1;
        public int GroupIndex { get; private set; }
        public int Row { get; set; }
        public bool IsGroupExpanded { get; private set; }
        public bool IsLaneVisible { get; private set; } = true;
        public bool IsDetailed => IsSelected || IsHovered;
        public bool IsGroupMarkerVisible => Alpha > 0;
        public override bool IsSelectable => IsGroupMarkerVisible && base.IsSelectable;
        public double TimelineEndTime => Math.Max(Item.StartTime, getDisplayEndTime(Item));

        public LaneTimelineHitObjectBlueprint(HitObject item, EditorTimelineLane lane, Func<HitObject, double> getDisplayEndTime, float minimumMarkerWidth = 9)
            : base(item)
        {
            this.getDisplayEndTime = getDisplayEndTime;
            this.minimumMarkerWidth = minimumMarkerWidth;
            durationEditable = canAdjustDuration;
            LaneIndex = lane.Index;
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;
            RelativePositionAxes = Axes.X;
            RelativeSizeAxes = Axes.X;
            Height = 16;

            startTime = item.StartTimeBindable.GetBoundCopy();
            startTime.BindValueChanged(time => X = (float)time.NewValue, true);

            AddRangeInternal(new Drawable[]
            {
                bar = new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = IDLE_HEIGHT,
                    Padding = new MarginPadding { Horizontal = -4 },
                    CornerRadius = IDLE_HEIGHT / 2,
                    Masking = true,
                    BorderColour = Color4.White,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = lane.Colour,
                    },
                },
                startMarker = new Circle
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Size = new Vector2(minimumMarkerWidth, minimumMarkerWidth > 9 ? 16 : 9),
                    Colour = lane.Colour,
                },
                shortName = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Text = lane.ShortName,
                    Font = OsuFont.GetFont(size: 7, weight: FontWeight.Bold),
                    Colour = OsuColour.ForegroundTextColourFor(lane.Colour),
                },
                groupCount = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Position = new Vector2(8, -1),
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                    Colour = lane.Colour.Lighten(0.3f),
                    Alpha = 0,
                },
            });

            if (item is IHasDuration)
            {
                AddInternal(durationHandle = new TimelineHitObjectBlueprint.DragArea(item)
                {
                    Alpha = 0,
                    OnDragHandled = e => OnDragHandled?.Invoke(e),
                });
            }
        }

        protected override void Update()
        {
            base.Update();

            double displayDuration = TimelineEndTime - Item.StartTime;
            float duration = (float)displayDuration;

            if (Width != duration)
                Width = duration;

            // Keep a real pixel-sized grab target even for a zero-duration event.
            // Do not manufacture a time range just to make a marker clickable.
            float padding = Math.Max(4, (minimumMarkerWidth - DrawWidth) / 2);
            bar.Padding = new MarginPadding { Horizontal = -padding };

            if (durationEditable != canAdjustDuration)
            {
                durationEditable = canAdjustDuration;
                updateDetailedState();
            }

            if (durationHandle != null)
            {
                double editableDuration = Item.GetEndTime() - Item.StartTime;
                durationHandle.X = displayDuration > 0
                    ? (float)((editableDuration - displayDuration) / displayDuration)
                    : 0;
            }
        }

        public void SetGroupState(int count, int index, bool expanded, bool laneVisible, float rowSpacing)
        {
            bool rowSpacingChanged = Math.Abs(expandedRowSpacing - rowSpacing) > 0.01f;
            bool visualStateChanged =
                GroupCount != count ||
                GroupIndex != index ||
                IsGroupExpanded != expanded ||
                IsLaneVisible != laneVisible ||
                rowSpacingChanged;

            GroupCount = count;
            GroupIndex = index;
            IsGroupExpanded = expanded;
            IsLaneVisible = laneVisible;
            expandedRowSpacing = rowSpacing;

            int representativeIndex = count / 2;
            bool representative = index == representativeIndex;
            bool visible = laneVisible && (count == 1 || expanded || representative || IsSelected);
            Alpha = visible ? 1 : 0;
            groupCount.Text = count > 1 && !expanded && representative ? $"×{count}" : string.Empty;
            groupCount.Alpha = count > 1 && !expanded && representative ? 1 : 0;

            // Vertical layout is assigned by TimelineBlueprintContainer. Reset
            // any previous group fan-out before that row position is applied.
            Position = new Vector2(Position.X, 0);

            // TimelineBlueprintContainer performs lane layout every frame. Do not
            // enqueue another set of transforms unless the visual state changed.
            // Doing so here unconditionally retained millions of superseded
            // transforms and grew the managed heap by several GB.
            if (visualStateChanged)
                updateDetailedState();
        }

        protected override void OnSelected() => updateDetailedState();

        protected override void OnDeselected() => updateDetailedState();

        protected override bool OnHover(HoverEvent e)
        {
            updateDetailedState();
            // Do not terminate hover propagation: an overlapping, already selected
            // blueprint must also be hovered so the container can prioritise it.
            return false;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateDetailedState();
            base.OnHoverLost(e);
        }

        private void updateDetailedState()
        {
            // A selected layer may contain dozens of objects. Keep every bar
            // inside its allocated row instead of allowing fixed 5px bars and
            // 9px markers to merge into one solid block.
            float maximumRowHeight = expandedRowSpacing * 0.7f;
            float targetHeight = Math.Min(IsDetailed ? detailed_height : minimumMarkerWidth > 9 ? 10 : IDLE_HEIGHT, maximumRowHeight);
            bar.ResizeHeightTo(targetHeight, 100, Easing.OutQuint);
            bar.CornerRadius = targetHeight / 2;
            bar.BorderThickness = IsSelected ? Math.Min(2, targetHeight / 3) : 0;

            float desiredMarkerScale = IsDetailed ? 1.35f : IsGroupExpanded && GroupCount > 1 ? 0.6f : 1;
            float maximumMarkerScale = maximumRowHeight / startMarker.Height;
            float markerScale = Math.Min(desiredMarkerScale, maximumMarkerScale);
            startMarker.ScaleTo(new Vector2(minimumMarkerWidth > 9 ? 1 : markerScale, markerScale), 100, Easing.OutQuint);

            float desiredTextScale = IsDetailed ? 1.1f : 1;
            float maximumTextScale = maximumRowHeight / 9;
            shortName.ScaleTo(Math.Min(desiredTextScale, maximumTextScale), 100, Easing.OutQuint);
            durationHandle?.FadeTo(durationEditable && IsSelected && expandedRowSpacing >= 4 ? 1 : 0, 100, Easing.OutQuint);
        }

        protected override bool ShouldBeConsideredForInput(Drawable child)
            => IsLaneVisible && IsGroupMarkerVisible
               && (child != durationHandle || IsSelected && durationEditable && expandedRowSpacing >= 4)
               && base.ShouldBeConsideredForInput(child);

        protected override bool ReceivePositionalInputAtSubTree(Vector2 screenSpacePos)
            => IsLaneVisible && IsGroupMarkerVisible && ReceivePositionalInputAt(screenSpacePos);

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (!IsLaneVisible || !IsGroupMarkerVisible)
                return false;

            RectangleF hitArea = bar.ScreenSpaceDrawQuad.AABBFloat.Inflate(new Vector2(2, IsGroupExpanded ? 1 : 5));
            float minimumScreenWidth = (ToScreenSpace(new Vector2(minimumMarkerWidth, 0)) - ToScreenSpace(Vector2.Zero)).Length;
            float markerLeft = ScreenSpaceSelectionPoint.X - minimumScreenWidth / 2;
            float markerRight = ScreenSpaceSelectionPoint.X + minimumScreenWidth / 2;
            float left = Math.Min(hitArea.Left, markerLeft);
            float right = Math.Max(hitArea.Right, markerRight);
            hitArea = new RectangleF(left, hitArea.Top, right - left, hitArea.Height);

            if (durationEditable && IsSelected && durationHandle != null)
                hitArea = RectangleF.Union(hitArea, durationHandle.ScreenSpaceDrawQuad.AABBFloat);

            // Input stays inside its assigned type band (or detail row), even
            // when a hovered marker or duration handle is larger than the band.
            float rowHeight = expandedRowSpacing * 0.9f;
            float screenRowHeight = (ToScreenSpace(new Vector2(0, rowHeight)) - ToScreenSpace(Vector2.Zero)).Length;
            hitArea = new RectangleF(
                hitArea.Left,
                ScreenSpaceSelectionPoint.Y - screenRowHeight / 2,
                hitArea.Width,
                screenRowHeight);
            return hitArea.Contains(screenSpacePos);
        }

        protected override bool ComputeIsMaskedAway(RectangleF maskingBounds)
            => !IsLaneVisible || !maskingBounds.IntersectsWith(SelectionQuad.AABBFloat.Inflate(new Vector2(4)));

        public override Quad SelectionQuad => RectangleF.Union(bar.ScreenSpaceDrawQuad.AABBFloat, startMarker.ScreenSpaceDrawQuad.AABBFloat);

        public override Vector2 ScreenSpaceSelectionPoint => startMarker.ScreenSpaceDrawQuad.Centre;
    }
}
