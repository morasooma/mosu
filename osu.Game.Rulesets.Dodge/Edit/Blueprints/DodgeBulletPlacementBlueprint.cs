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
    public partial class DodgeBulletPlacementBlueprint : HitObjectPlacementBlueprint
    {
        public new DodgeBullet HitObject => (DodgeBullet)base.HitObject;

        protected override bool IsValidForPlacement => base.IsValidForPlacement && HitObject.StartTime < EditorClock.TrackLength;

        private readonly DodgeBulletPathPiece pathPiece;
        private float? lockedTravelSpeed;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        public DodgeBulletPlacementBlueprint()
            : base(new DodgeBullet())
        {
            Child = pathPiece = new DodgeBulletPathPiece();
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
            HitObject.ContinueUntilExit = settings.BulletContinueUntilExit.Value;
            HitObject.TrajectoryGuideStyle = settings.BulletTrajectoryGuideStyle.Value;
            HitObject.MovementType = settings.BulletMovementType.Value;
            HitObject.MovementEasing = settings.BulletMovementEasing.Value;
            HitObject.WaveAmplitude = settings.BulletWaveAmplitude.Value;
            HitObject.WaveCycles = settings.BulletWaveCycles.Value;
            HitObject.WavePhase = settings.BulletWavePhase.Value;
            HitObject.Shape = settings.BulletShape.Value;
            HitObject.Colour = settings.BulletColour.Value;
            HitObject.OutlineColour = settings.BulletOutlineColour.Value;
            HitObject.Opacity = settings.BulletOpacity.Value;
            HitObject.OutlineThickness = settings.BulletOutlineThickness.Value;

            if (PlacementActive == PlacementState.Waiting)
            {
                HitObject.Position = HitObject.EndPosition = position;
                DodgeHitObject? previousProjectile = editorBeatmap.HitObjects
                                                                    .OfType<DodgeHitObject>()
                                                                    .Where(hitObject => hitObject.StartTime <= HitObject.StartTime)
                                                                    .Where(hitObject => hitObject is DodgeBullet or DodgeEmitter)
                                                                    .OrderBy(hitObject => hitObject.StartTime)
                                                                    .LastOrDefault();

                HitObject.Duration = previousProjectile is IHasDuration previousDuration ? previousDuration.Duration : 0;

                if (HitObject.Duration <= 0)
                    HitObject.Duration = beatSnapProvider.GetBeatLengthAtTime(HitObject.StartTime) * 4;

                DodgeBullet? previousBullet = editorBeatmap.HitObjects
                                                           .OfType<DodgeBullet>()
                                                           .Where(bullet => bullet.StartTime <= HitObject.StartTime)
                                                           .OrderBy(bullet => bullet.StartTime)
                                                           .LastOrDefault();

                if (settings.BulletLockFlight.Value && previousBullet != null)
                {
                    HitObject.Duration = previousBullet.Duration;
                    lockedTravelSpeed = DodgeBulletPlacementUtils.GetTravelSpeed(previousBullet.Position, previousBullet.EndPosition, previousBullet.Duration);
                }
                else
                {
                    lockedTravelSpeed = null;
                }
            }
            else if (PlacementActive == PlacementState.Active)
            {
                HitObject.EndPosition = settings.BulletLockFlight.Value && lockedTravelSpeed.HasValue
                    ? DodgeBulletPlacementUtils.GetEndPositionForSpeed(HitObject.Position, position, lockedTravelSpeed.Value, HitObject.Duration)
                    : position;
            }

            DodgeEditorTrackBounds.Constrain(HitObject, EditorClock.TrackLength, true);
            return new SnapResult(ToScreenSpace(position), fallbackTime);
        }

        public override bool ReplacesExistingObject(HitObject existing) => false;
    }
}
