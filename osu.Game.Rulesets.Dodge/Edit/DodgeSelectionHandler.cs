// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Utils;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeSelectionHandler : EditorSelectionHandler
    {
        private bool nudgeMovementActive;

        [Resolved]
        private Playfield playfield { get; set; } = null!;

        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        public override bool HandleMovement(MoveSelectionEvent<HitObject> moveEvent)
        {
            Vector2 localStart = playfield.HitObjectContainer.ToLocalSpace(moveEvent.Blueprint.ScreenSpaceSelectionPoint);
            Vector2 localEnd = playfield.HitObjectContainer.ToLocalSpace(moveEvent.Blueprint.ScreenSpaceSelectionPoint + moveEvent.ScreenSpaceDelta);
            localEnd = composer.SnapToPositionGrid(localEnd);
            Vector2 delta = localEnd - localStart;

            EditorBeatmap.PerformOnSelection(hitObject =>
            {
                if (hitObject is DodgeEmitter emitter)
                {
                    emitter.Position += delta;
                    emitter.AimPosition += delta;
                    emitter.MovementEndPosition += delta;
                    return;
                }

                if (hitObject is DodgeBeam beam)
                {
                    beam.Position += delta;
                    beam.EndPosition += delta;
                    return;
                }

                if (hitObject is not DodgeBullet bullet)
                {
                    if (hitObject is DodgeTrigger trigger)
                        trigger.Position += delta;

                    if (hitObject is DodgeArenaChange arenaChange)
                    {
                        arenaChange.TargetPosition += delta;
                        arenaChange.ClampToBaseBounds();
                    }

                    if (hitObject is DodgeCameraChange cameraChange)
                    {
                        cameraChange.Position += delta;
                        cameraChange.EndPosition += delta;
                    }

                    return;
                }

                bullet.Position += delta;
                bullet.EndPosition += delta;
            });

            return true;
        }

        protected override void OnSelectionChanged()
        {
            base.OnSelectionChanged();

            HitObject[] objects = DodgeSelectionTransformUtils.Transformable(SelectedItems);
            Quad quad = DodgeSelectionTransformUtils.GetSurroundingQuad(objects);
            SelectionBox.CanFlipX = objects.Length > 0 && quad.Width > 0;
            SelectionBox.CanFlipY = objects.Length > 0 && quad.Height > 0;
            SelectionBox.CanReverse = objects.Length > 1 || objects.Any(item => item is DodgeBullet or DodgeEmitter);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.ShiftPressed || !e.ControlPressed)
                return base.OnKeyDown(e);

            return e.Key switch
            {
                Key.Left => nudgeSelection(new Vector2(-1, 0)),
                Key.Right => nudgeSelection(new Vector2(1, 0)),
                Key.Up => nudgeSelection(new Vector2(0, -1)),
                Key.Down => nudgeSelection(new Vector2(0, 1)),
                _ => base.OnKeyDown(e),
            };
        }

        protected override void OnKeyUp(KeyUpEvent e)
        {
            base.OnKeyUp(e);

            if (nudgeMovementActive && !e.ControlPressed)
            {
                EditorBeatmap.EndChange();
                nudgeMovementActive = false;
            }
        }

        private bool nudgeSelection(Vector2 delta)
        {
            HitObject[] objects = DodgeSelectionTransformUtils.Transformable(SelectedItems);

            if (objects.Length == 0)
                return false;

            if (!nudgeMovementActive)
            {
                EditorBeatmap.BeginChange();
                nudgeMovementActive = true;
            }

            foreach (HitObject item in objects)
            {
                var points = DodgeSelectionTransformUtils.GetPoints(item);
                DodgeSelectionTransformUtils.SetPoints(item, points.Start + delta, points.End + delta);
                EditorBeatmap.Update(item);
            }

            return true;
        }

        public override SelectionRotationHandler CreateRotationHandler() => new DodgeSelectionRotationHandler();

        public override SelectionScaleHandler CreateScaleHandler() => new DodgeSelectionScaleHandler();

        public override bool HandleReverse()
        {
            DodgeHitObject[] objects = SelectedItems.OfType<DodgeHitObject>()
                                                    .OrderBy(item => item.StartTime)
                                                    .ToArray();

            if (objects.Length == 0)
                return false;

            double startTime = objects.Min(item => item.StartTime);
            double endTime = objects.Max(item => item.GetEndTime());

            foreach (DodgeHitObject item in objects)
            {
                if (objects.Length > 1)
                    item.StartTime = endTime - (item.GetEndTime() - startTime);

                switch (item)
                {
                    case DodgeBullet bullet:
                        (bullet.Position, bullet.EndPosition) = (bullet.EndPosition, bullet.Position);
                        break;

                    case DodgeBeam beam:
                        (beam.Position, beam.EndPosition) = (beam.EndPosition, beam.Position);
                        break;

                    case DodgeCameraChange camera:
                        (camera.Position, camera.EndPosition) = (camera.EndPosition, camera.Position);
                        break;

                    case DodgeEmitter emitter:
                    {
                        Vector2 movementOffset = emitter.MovementEndPosition - emitter.Position;
                        (emitter.Position, emitter.AimPosition) = (emitter.AimPosition, emitter.Position);

                        if (emitter.MoveSource)
                            emitter.MovementEndPosition = emitter.Position + movementOffset;

                        break;
                    }

                    case DodgeTrigger:
                        break;
                }

                EditorBeatmap.Update(item);
            }

            return true;
        }

        public override bool HandleFlip(Direction direction, bool flipOverOrigin)
        {
            HitObject[] objects = DodgeSelectionTransformUtils.Transformable(SelectedItems);

            if (objects.Length == 0)
                return false;

            Quad flipQuad = flipOverOrigin
                ? new Quad(composer.GridToolbox.StartPosition.Value.X, composer.GridToolbox.StartPosition.Value.Y, 0, 0)
                : DodgeSelectionTransformUtils.GetSurroundingQuad(objects);

            Vector2 flipAxis = direction == Direction.Vertical ? Vector2.UnitY : Vector2.UnitX;

            if (flipOverOrigin)
                flipAxis = GeometryUtils.RotateVector(flipAxis, -composer.GridToolbox.GridLinesRotation.Value);

            EditorBeatmap.PerformOnSelection(hitObject =>
            {
                if (!objects.Contains(hitObject))
                    return;

                var points = DodgeSelectionTransformUtils.GetPoints(hitObject);
                DodgeSelectionTransformUtils.SetPoints(
                    hitObject,
                    GeometryUtils.GetFlippedPosition(flipAxis, flipQuad, points.Start),
                    GeometryUtils.GetFlippedPosition(flipAxis, flipQuad, points.End));
            });

            return true;
        }
    }
}
