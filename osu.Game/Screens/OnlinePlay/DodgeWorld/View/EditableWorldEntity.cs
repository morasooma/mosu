// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal abstract partial class EditableWorldEntity : CompositeDrawable
    {
        private readonly Func<bool> editing;
        private readonly Action<EditableWorldEntity> select;
        private readonly Container selectionBox;
        private readonly Container selectionOutline;
        private readonly Color4 selectionColour;
        private readonly List<EditorResizeHandle> resizeHandles = new List<EditorResizeHandle>();
        private Vector2 dragStartMouse;
        private Vector2 dragStartPosition;
        private Vector2 resizeStartMouse;
        private Vector2 resizeStartPosition;
        private Vector2 resizeStartSize;
        private Vector2 resizeStartScale;
        private bool selected;
        private bool resizeHandlesCreated;

        public string EntityId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Raised once when the user starts a drag or a resize, before anything has moved.
        /// </summary>
        /// <remarks>
        /// The editor uses this to snapshot the room for undo, so that a whole drag collapses into a
        /// single undo step rather than one per frame.
        /// </remarks>
        public Action? EditBegan { get; set; }

        /// <summary>
        /// What the editor currently has selected, so that a drag can be handed to it.
        /// </summary>
        /// <remarks>
        /// Set by the screen when the entity joins a room, like <see cref="EditBegan"/>. Draw order decides
        /// who is on top, and that is right for clicking; it is wrong for dragging, because the object the
        /// author is holding is the one they just selected, not whatever happens to be drawn over it.
        /// </remarks>
        public Func<EditableWorldEntity?>? CurrentSelection { get; set; }

        /// <summary>
        /// Told that this entity was clicked, and where. Set by the screen when the entity joins a room.
        /// </summary>
        /// <remarks>
        /// Separate from selecting, because a click is a question the screen answers rather than an order:
        /// clicking the same spot twice steps to whatever is behind. A drag still selects directly — that
        /// would otherwise cycle the selection while the object is being moved.
        /// </remarks>
        public Action<EditableWorldEntity, Vector2>? Clicked { get; set; }

        public virtual bool BlocksMovement => true;
        public virtual Vector2 CollisionSize => new Vector2(80, 45);
        public virtual Vector2 CollisionCentreOffset => new Vector2(0, -CollisionSize.Y / 2);
        public virtual float? FixedDepth => null;
        public virtual string LayoutKind => "entity";
        public virtual bool CanResize => false;

        /// <summary>Whether the editor is open, for an entity that shows the author more than the player.</summary>
        protected bool IsEditing => editing();

        /// <summary>
        /// The story flag this entity records once the player is done with it, or null when it is not a
        /// story point. Shared by every kind, because anything can become one.
        /// </summary>
        public string? StoryFlag { get; set; }

        /// <summary>What <see cref="StoryFlag"/> is raised to.</summary>
        public int StoryFlagValue { get; set; } = 1;

        /// <summary>Experience the server pays the first time this story point is reached.</summary>
        public int StoryExperience { get; set; }

        /// <summary>Coins the server pays the first time this story point is reached.</summary>
        public int StoryCoins { get; set; }

        /// <summary>What must already be true about the player's story for this entity to exist for them.</summary>
        public Model.StoryCondition Visibility { get; set; } = Model.StoryCondition.NONE;
        public virtual bool CanScale => true;

        /// <summary>
        /// Whether the author may turn this entity.
        /// </summary>
        /// <remarks>
        /// Off for anything that blocks movement. Collision is resolved against upright boxes, so a wall
        /// turned on screen would still stop the player along its old edges, and an obstacle that is not
        /// where it is drawn is worse than one that cannot be turned.
        /// </remarks>
        public virtual bool CanRotate => false;

        private float facing;

        /// <summary>
        /// Which way this entity is turned, in degrees, clockwise from upright.
        /// </summary>
        public float FacingDegrees
        {
            get => facing;
            set
            {
                if (!float.IsFinite(value))
                    return;

                facing = wrapDegrees(value);
                ApplyFacing(facing);
            }
        }

        private static float wrapDegrees(float value)
        {
            value %= 360;
            return value < 0 ? value + 360 : value;
        }

        /// <summary>
        /// Turns whatever should turn. Rotating the whole drawable by default, which a kind carrying a
        /// caption overrides so the caption stays readable.
        /// </summary>
        protected virtual void ApplyFacing(float degrees) => Rotation = degrees;

        public virtual bool CanStyle => false;
        public virtual bool CanRound => false;
        public virtual int StyleIndex => 0;
        public virtual bool Rounded => false;
        protected float SelectionCornerRadius
        {
            get => selectionOutline.CornerRadius;
            set => selectionOutline.CornerRadius = value;
        }

        protected EditableWorldEntity(Func<bool> editing, Action<EditableWorldEntity> select, Color4 selectionColour)
        {
            this.editing = editing;
            this.select = select;
            this.selectionColour = selectionColour;
            Anchor = Anchor.Centre;
            Origin = Anchor.BottomCentre;
            AddInternal(selectionBox = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Depth = -100,
                Child = selectionOutline = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 10,
                    BorderThickness = 3,
                    BorderColour = selectionColour,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Transparent },
                },
            });
        }

        public virtual void SetDisplayName(string value) => DisplayName = value;

        public virtual void SetEditing(bool value)
        {
            if (!value)
                selectionBox.Alpha = 0;
            else if (IsHovered)
                selectionBox.Alpha = 0.65f;
        }

        public void SetSelected(bool value)
        {
            selected = value;
            if (value && (CanResize || CanScale))
                ensureResizeHandles();

            foreach (EditorResizeHandle handle in resizeHandles)
                handle.Alpha = value ? 1 : 0;

            selectionBox.Alpha = value ? 1 : editing() && IsHovered ? 0.65f : 0;
        }

        private void ensureResizeHandles()
        {
            if (resizeHandlesCreated)
                return;

            resizeHandlesCreated = true;
            Anchor[] anchors = CanResize
                ? new[]
                {
                    Anchor.TopLeft, Anchor.TopCentre, Anchor.TopRight,
                    Anchor.CentreLeft, Anchor.CentreRight,
                    Anchor.BottomLeft, Anchor.BottomCentre, Anchor.BottomRight,
                }
                : new[] { Anchor.TopLeft, Anchor.TopRight, Anchor.BottomLeft, Anchor.BottomRight };

            foreach (Anchor anchor in anchors)
            {
                var handle = new EditorResizeHandle(anchor, selectionColour, beginResize, updateResize) { Alpha = 0 };
                resizeHandles.Add(handle);
                selectionBox.Add(handle);
            }
        }

        private void beginResize(Anchor anchor, Vector2 screenPosition)
        {
            if (Parent == null)
                return;

            EditBegan?.Invoke();
            select(this);
            resizeStartMouse = Parent.ToLocalSpace(screenPosition);
            resizeStartPosition = Position;
            resizeStartSize = Size;
            resizeStartScale = Scale;
        }

        private void updateResize(Anchor anchor, Vector2 screenPosition, bool lockAspectRatio)
        {
            if (Parent == null)
                return;

            Vector2 delta = Parent.ToLocalSpace(screenPosition) - resizeStartMouse;

            // Turned back into the object's own frame, so a handle on a rotated object grows the side it
            // is actually attached to rather than the side that side used to be.
            delta = rotate(delta, -Rotation);

            if (CanScale && !CanResize)
            {
                bool leftHandle = anchor is Anchor.TopLeft or Anchor.BottomLeft;
                bool topHandle = anchor is Anchor.TopLeft or Anchor.TopRight;
                float horizontal = delta.X * (leftHandle ? -1 : 1) / Math.Max(1, resizeStartSize.X * resizeStartScale.X);
                float vertical = delta.Y * (topHandle ? -1 : 1) / Math.Max(1, resizeStartSize.Y * resizeStartScale.Y);
                float factor = Math.Clamp(1 + (horizontal + vertical) / 2, 0.4f, 2.5f);
                Scale = resizeStartScale * factor;
                select(this);
                return;
            }

            float left = resizeStartPosition.X - resizeStartSize.X / 2;
            float right = resizeStartPosition.X + resizeStartSize.X / 2;
            float top = resizeStartPosition.Y - resizeStartSize.Y / 2;
            float bottom = resizeStartPosition.Y + resizeStartSize.Y / 2;

            bool movesLeft = anchor is Anchor.TopLeft or Anchor.CentreLeft or Anchor.BottomLeft;
            bool movesRight = anchor is Anchor.TopRight or Anchor.CentreRight or Anchor.BottomRight;
            bool movesTop = anchor is Anchor.TopLeft or Anchor.TopCentre or Anchor.TopRight;
            bool movesBottom = anchor is Anchor.BottomLeft or Anchor.BottomCentre or Anchor.BottomRight;

            if (movesLeft) left += delta.X;
            if (movesRight) right += delta.X;
            if (movesTop) top += delta.Y;
            if (movesBottom) bottom += delta.Y;

            if (lockAspectRatio && (movesLeft || movesRight) && (movesTop || movesBottom))
            {
                float scale = Math.Max((right - left) / resizeStartSize.X, (bottom - top) / resizeStartSize.Y);
                scale = Math.Max(scale, 32 / Math.Min(resizeStartSize.X, resizeStartSize.Y));
                float width = resizeStartSize.X * scale;
                float height = resizeStartSize.Y * scale;
                if (movesLeft) left = right - width;
                else right = left + width;
                if (movesTop) top = bottom - height;
                else bottom = top + height;
            }

            if (right - left < 32)
            {
                if (movesLeft) left = right - 32;
                else right = left + 32;
            }

            if (bottom - top < 32)
            {
                if (movesTop) top = bottom - 32;
                else bottom = top + 32;
            }

            Vector2 snappedTopLeft = CombatRules.SnapToGrid(new Vector2(left, top));
            Vector2 snappedBottomRight = CombatRules.SnapToGrid(new Vector2(right, bottom));
            left = snappedTopLeft.X;
            right = snappedBottomRight.X;
            top = snappedTopLeft.Y;
            bottom = snappedBottomRight.Y;

            // Where the edges ended up is an answer in the object's own frame; where its centre ends up is
            // one in its parent's, so the shift is turned back before it is applied.
            Vector2 centre = new Vector2((left + right) / 2, (top + bottom) / 2);

            Position = resizeStartPosition + rotate(centre - resizeStartPosition, Rotation);
            Size = new Vector2(Math.Max(32, right - left), Math.Max(32, bottom - top));
            select(this);
        }

        private static Vector2 rotate(Vector2 value, float degrees) => CombatRules.Rotate(value, degrees);

        public virtual void ResizeBy(Vector2 amount)
        {
            if (!CanResize)
                return;

            Size = new Vector2(Math.Clamp(Size.X + amount.X, 32, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.X), Math.Clamp(Size.Y + amount.Y, 32, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.Y));
        }

        public virtual void CycleStyle()
        {
        }

        public virtual void ToggleShape()
        {
        }

        public virtual void RestoreEditorDefaults()
        {
            Scale = Vector2.One;
        }

        public virtual void ApplySavedEditorState(float? width, float? height, int? style, bool? rounded, float? scale)
        {
            if (CanResize && width.HasValue && height.HasValue && float.IsFinite(width.Value) && float.IsFinite(height.Value))
                Size = new Vector2(Math.Clamp(width.Value, 32, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.X), Math.Clamp(height.Value, 32, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.Y));

            if (CanScale && scale.HasValue && float.IsFinite(scale.Value))
                Scale = new Vector2(Math.Clamp(scale.Value, 0.4f, 2.5f));
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!editing())
                return false;

            if (!selected)
                selectionBox.FadeTo(0.65f, 100);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (editing() && !selected)
                selectionBox.FadeOut(100);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e) => handleClick(e.ScreenSpaceMousePosition);

        /// <summary>
        /// A quick second click is the same question as a slow one.
        /// </summary>
        /// <remarks>
        /// osu!framework turns the second of two nearby clicks into a double-click and suppresses the
        /// click itself, so without this, clicking twice quickly to reach the object underneath did
        /// nothing — which is exactly the speed somebody clicks at when they mean "no, the other one".
        /// </remarks>
        protected override bool OnDoubleClick(DoubleClickEvent e) => handleClick(e.ScreenSpaceMousePosition);

        private bool handleClick(Vector2 screenSpacePosition)
        {
            if (!editing())
                return false;

            if (Clicked != null)
                Clicked(this, screenSpacePosition);
            else
                select(this);

            return true;
        }

        /// <summary>
        /// Whether a press here belongs to the selected entity rather than to this one.
        /// </summary>
        /// <remarks>
        /// Draw order decides who is on top, and that is the right answer for a click. It is the wrong one
        /// for a drag: the object the author is holding is the one they just selected, not whatever happens
        /// to be drawn over it.
        /// <para>
        /// Declining the press is what hands it over, and it has to be declined at
        /// <see cref="OnMouseDown"/> and not only at <see cref="OnDragStart"/>: osu!framework truncates the
        /// button-down input queue at whoever handled the press, so a drag never reaches anything behind it.
        /// The click still arrives here — clicks are propagated over that same queue front to back — so
        /// selecting an overlapping neighbour is one click away, as before.
        /// </para>
        /// </remarks>
        private bool defersToSelection(Vector2 screenSpacePosition) =>
            !selected
            && CurrentSelection?.Invoke() is EditableWorldEntity current
            && !ReferenceEquals(current, this)
            && current.ReceivePositionalInputAt(screenSpacePosition);

        protected override bool OnMouseDown(MouseDownEvent e) =>
            editing() && e.Button == MouseButton.Left && !e.CurrentState.Keyboard.Keys.IsPressed(Key.Space)
            && !defersToSelection(e.ScreenSpaceMousePosition);

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!editing() || e.Button != MouseButton.Left || Parent == null || e.CurrentState.Keyboard.Keys.IsPressed(Key.Space))
                return false;

            if (defersToSelection(e.ScreenSpaceMousePosition))
                return false;

            EditBegan?.Invoke();
            select(this);
            dragStartMouse = Parent.ToLocalSpace(e.ScreenSpaceMousePosition);
            dragStartPosition = Position;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            base.OnDrag(e);
            if (Parent == null)
                return;

            Vector2 delta = Parent.ToLocalSpace(e.ScreenSpaceMousePosition) - dragStartMouse;
            Vector2 next = dragStartPosition + delta;
            Position = CombatRules.SnapToGrid(next);
            select(this);
        }
    }
}
