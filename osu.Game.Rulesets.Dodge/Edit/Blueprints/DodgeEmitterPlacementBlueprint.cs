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
    public partial class DodgeEmitterPlacementBlueprint : HitObjectPlacementBlueprint
    {
        public new DodgeEmitter HitObject => (DodgeEmitter)base.HitObject;

        protected override bool IsValidForPlacement => base.IsValidForPlacement && HitObject.StartTime < EditorClock.TrackLength;

        private readonly DodgeEmitterPathPiece pathPiece;
        private bool placingMovementEnd;

        public bool IsPlacingMovementEnd => placingMovementEnd;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        public DodgeEmitterPlacementBlueprint()
            : base(new DodgeEmitter())
        {
            InternalChild = pathPiece = new DodgeEmitterPathPiece();
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
                    if (HitObject.MoveSource && !placingMovementEnd)
                    {
                        placingMovementEnd = true;
                        HitObject.MovementEndPosition = HitObject.Position;
                        return true;
                    }

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
            HitObject.BulletCount = settings.EmitterBulletCount.Value;
            HitObject.SpreadAngle = settings.EmitterSpreadAngle.Value;
            HitObject.ContinueUntilExit = settings.EmitterContinueUntilExit.Value;
            HitObject.TrajectoryGuideStyle = settings.EmitterTrajectoryGuideStyle.Value;
            HitObject.MovementType = settings.EmitterMovementType.Value;
            HitObject.WaveAmplitude = settings.EmitterWaveAmplitude.Value;
            HitObject.WaveCycles = settings.EmitterWaveCycles.Value;
            HitObject.WavePhase = settings.EmitterWavePhase.Value;
            HitObject.Shape = settings.EmitterShape.Value;
            HitObject.BurstCount = settings.EmitterRepeating.Value
                ? settings.EmitterBurstCount.Value
                : DodgeEmitter.MIN_BURST_COUNT;
            HitObject.BurstBeatDivisor = (int)settings.EmitterBurstBeatDivisor.Value;
            HitObject.BurstInterval = DodgeEmitter.IntervalForBeatLength(
                editorBeatmap.ControlPointInfo.TimingPointAt(HitObject.StartTime).BeatLength,
                HitObject.BurstBeatDivisor);
            HitObject.MoveSource = settings.EmitterRepeating.Value && settings.EmitterMoving.Value;
            HitObject.Colour = settings.EmitterColour.Value;
            HitObject.OutlineColour = settings.EmitterOutlineColour.Value;
            HitObject.Opacity = settings.EmitterOpacity.Value;
            HitObject.OutlineThickness = settings.EmitterOutlineThickness.Value;

            if (PlacementActive == PlacementState.Waiting)
            {
                HitObject.Position = HitObject.AimPosition = HitObject.MovementEndPosition = position;
                HitObject.Duration = editorBeatmap.HitObjects
                                                  .OfType<DodgeHitObject>()
                                                  .Where(hitObject => hitObject.StartTime <= HitObject.StartTime)
                                                  .Where(hitObject => hitObject is DodgeBullet or DodgeEmitter)
                                                  .OrderBy(hitObject => hitObject.StartTime)
                                                  .Select(hitObject => ((IHasDuration)hitObject).Duration)
                                                  .LastOrDefault();

                if (HitObject.Duration <= 0)
                    HitObject.Duration = beatSnapProvider.GetBeatLengthAtTime(HitObject.StartTime) * 4;
            }
            else if (PlacementActive == PlacementState.Active && !placingMovementEnd)
            {
                HitObject.AimPosition = position;
            }
            else if (PlacementActive == PlacementState.Active)
            {
                HitObject.MovementEndPosition = position;
            }

            DodgeEditorTrackBounds.Constrain(HitObject, EditorClock.TrackLength, true);
            return new SnapResult(ToScreenSpace(position), fallbackTime);
        }

        public override bool ReplacesExistingObject(HitObject existing) => false;
    }
}
