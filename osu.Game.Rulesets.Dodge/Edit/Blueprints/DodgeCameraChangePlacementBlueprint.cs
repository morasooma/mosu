// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeCameraChangePlacementBlueprint : HitObjectPlacementBlueprint
    {
        public new DodgeCameraChange HitObject => (DodgeCameraChange)base.HitObject;

        protected override bool IsValidForPlacement
            => HitObject.StartTime < EditorClock.TrackLength
               && (HitObject.StartTime <= 0 || base.IsValidForPlacement);

        private readonly DodgeCameraPathPiece pathPiece;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        public DodgeCameraChangePlacementBlueprint()
            : base(new DodgeCameraChange())
        {
            Child = pathPiece = new DodgeCameraPathPiece();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            BeginPlacement();
        }

        protected override void Update()
        {
            base.Update();
            pathPiece.UpdateFrom(HitObject);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return base.OnMouseDown(e);

            switch (PlacementActive)
            {
                case PlacementState.Waiting:
                    BeginPlacement(true);
                    return true;

                case PlacementState.Active:
                    EndPlacement(true);
                    return true;
            }

            return base.OnMouseDown(e);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => PlacementActive != PlacementState.Finished;

        protected override bool ReceivePositionalInputAtSubTree(Vector2 screenSpacePos)
            => ReceivePositionalInputAt(screenSpacePos);

        public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
        {
            base.UpdateTimeAndPosition(screenSpacePosition, fallbackTime);

            Vector2 position = composer.SnapToPositionGrid(ToLocalSpace(screenSpacePosition));

            if (PlacementActive == PlacementState.Waiting)
            {
                if (!editorBeatmap.HitObjects.OfType<DodgeCameraChange>().Any())
                    HitObject.StartTime = 0;

                HitObject.Position = HitObject.EndPosition = position;
                HitObject.Duration = beatSnapProvider.GetBeatLengthAtTime(HitObject.StartTime) * 4;
            }
            else if (PlacementActive == PlacementState.Active)
            {
                HitObject.EndPosition = position;
            }

            DodgeEditorTrackBounds.Constrain(HitObject, EditorClock.TrackLength, true);
            return new SnapResult(ToScreenSpace(position), fallbackTime);
        }

        public override bool ReplacesExistingObject(HitObject existing)
            => existing is DodgeCameraChange && base.ReplacesExistingObject(existing);
    }
}
