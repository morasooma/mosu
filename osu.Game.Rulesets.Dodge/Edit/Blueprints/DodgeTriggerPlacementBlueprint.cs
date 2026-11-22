// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Game.Screens.Edit;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeTriggerPlacementBlueprint : HitObjectPlacementBlueprint
    {
        public new DodgeTrigger HitObject => (DodgeTrigger)base.HitObject;

        protected override bool IsValidForPlacement => HitObject.StartTime >= 0 && HitObject.StartTime < EditorClock.TrackLength;

        private readonly DodgeTriggerPiece piece;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        public DodgeTriggerPlacementBlueprint()
            : base(new DodgeTrigger())
        {
            Child = piece = new DodgeTriggerPiece();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            BeginPlacement();
        }

        protected override void Update()
        {
            base.Update();
            piece.UpdateFrom(HitObject);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return base.OnMouseDown(e);

            if (PlacementActive == PlacementState.Finished || !composer.CursorInPlacementArea)
                return false;

            UpdateTimeAndPosition(e.ScreenSpaceMousePosition, editorBeatmap.SnapTime(EditorClock.CurrentTime, null));
            EndPlacement(true);
            return true;
        }

        public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
        {
            base.UpdateTimeAndPosition(screenSpacePosition, fallbackTime);

            HitObject.StartTime = Math.Max(0, fallbackTime);
            HitObject.Position = composer.SnapToPositionGrid(ToLocalSpace(screenSpacePosition));
            HitObject.Action = settings.TriggerAction.Value;
            HitObject.Strength = settings.TriggerStrength.Value;
            HitObject.SourcePosition = new Vector2(settings.TriggerSourceX.Value, settings.TriggerSourceY.Value);
            HitObject.MoveFromCurrentPosition = settings.TriggerMoveFromCurrent.Value;
            HitObject.TargetPosition = HitObject.Action is DodgeTriggerAction.MovePlayer or DodgeTriggerAction.ResizeArena
                ? HitObject.Position : new Vector2(settings.TriggerTargetX.Value, settings.TriggerTargetY.Value);
            HitObject.TargetSize = new Vector2(settings.TriggerWidth.Value, settings.TriggerHeight.Value);
            HitObject.Easing = settings.TriggerEasing.Value;
            HitObject.Colour = settings.TriggerColour.Value;
            HitObject.Duration = HitObject.IsTimedEffect
                ? beatSnapProvider.GetBeatLengthAtTime(HitObject.StartTime) * settings.TriggerDurationSnapUnits.Value
                : 0;
            HitObject.Samples.Clear();
            DodgeEditorTrackBounds.Constrain(HitObject, EditorClock.TrackLength, true);
            return new SnapResult(ToScreenSpace(HitObject.Position), fallbackTime);
        }

        public override bool ReplacesExistingObject(osu.Game.Rulesets.Objects.HitObject existing) => false;
    }
}
