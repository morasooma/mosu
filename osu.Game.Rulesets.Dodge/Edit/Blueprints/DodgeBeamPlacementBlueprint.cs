// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeBeamPlacementBlueprint : HitObjectPlacementBlueprint
    {
        public new DodgeBeam HitObject => (DodgeBeam)base.HitObject;

        protected override bool IsValidForPlacement => base.IsValidForPlacement && HitObject.StartTime < EditorClock.TrackLength;

        private readonly DodgeBeamPathPiece pathPiece;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        public DodgeBeamPlacementBlueprint()
            : base(new DodgeBeam())
        {
            InternalChild = pathPiece = new DodgeBeamPathPiece();
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

            HitObject.BeamWidth = settings.BeamWidth.Value;
            HitObject.Colour = settings.BeamColour.Value;
            HitObject.OutlineColour = settings.BeamOutlineColour.Value;
            HitObject.Opacity = settings.BeamOpacity.Value;
            HitObject.OutlineThickness = settings.BeamOutlineThickness.Value;
            HitObject.Samples.Clear();

            if (PlacementActive == PlacementState.Waiting)
            {
                HitObject.Position = HitObject.EndPosition = position;

                DodgeHitObject? previousProjectile = editorBeatmap.HitObjects
                                                                    .OfType<DodgeHitObject>()
                                                                    .Where(hitObject => hitObject.StartTime <= HitObject.StartTime)
                                                                    .OrderBy(hitObject => hitObject.StartTime)
                                                                    .LastOrDefault();

                HitObject.Duration = previousProjectile is IHasDuration previousDuration ? previousDuration.Duration : 0;

                if (HitObject.Duration <= 0)
                    HitObject.Duration = beatSnapProvider.GetBeatLengthAtTime(HitObject.StartTime) * 4;
            }
            else if (PlacementActive == PlacementState.Active)
            {
                HitObject.EndPosition = position;
            }

            DodgeEditorTrackBounds.Constrain(HitObject, EditorClock.TrackLength, true);
            return new SnapResult(ToScreenSpace(position), fallbackTime);
        }

        public override bool ReplacesExistingObject(HitObject existing) => false;
    }
}
