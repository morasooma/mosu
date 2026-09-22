// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
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

        protected override bool IsValidForPlacement => base.IsValidForPlacement && HitObject.StartTime < EditorClock.TrackLength;

        private readonly DodgeTriggerPiece piece;

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

            EndPlacement(true);
            return true;
        }

        public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
        {
            base.UpdateTimeAndPosition(screenSpacePosition, fallbackTime);

            HitObject.Position = composer.SnapToPositionGrid(ToLocalSpace(screenSpacePosition));
            HitObject.Action = settings.TriggerAction.Value;
            HitObject.Strength = settings.TriggerStrength.Value;
            HitObject.Colour = settings.TriggerColour.Value;
            HitObject.Duration = HitObject.IsTimedEffect ? settings.TriggerDuration.Value : 0;
            DodgeEditorTrackBounds.Constrain(HitObject, EditorClock.TrackLength, true);
            return new SnapResult(ToScreenSpace(HitObject.Position), fallbackTime);
        }

        public override bool ReplacesExistingObject(osu.Game.Rulesets.Objects.HitObject existing) => false;
    }
}
