// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    public partial class DodgeArenaChangePlacementBlueprint : HitObjectPlacementBlueprint
    {
        public new DodgeArenaChange HitObject => (DodgeArenaChange)base.HitObject;

        // The arena at time zero defines the pre-game state and must be editable
        // even when the map's first timing point starts later. The base placement
        // validation rejects all objects before that timing point, which made the
        // initial arena appear placeable but silently discarded it on the second click.
        protected override bool IsValidForPlacement
            => HitObject.StartTime < EditorClock.TrackLength
               && (HitObject.StartTime <= 0 || base.IsValidForPlacement);

        private readonly DodgeArenaChangePiece piece;
        private Vector2 anchorPosition;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        public DodgeArenaChangePlacementBlueprint()
            : base(new DodgeArenaChange())
        {
            InternalChild = piece = new DodgeArenaChangePiece();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            HitObject.TargetRotation = settings.ArenaRotation.Value;
            HitObject.Colour = settings.ArenaBackgroundColour.Value;
            HitObject.Opacity = settings.ArenaBackgroundOpacity.Value;
            HitObject.OutlineColour = settings.ArenaBorderColour.Value;
            HitObject.BorderOpacity = settings.ArenaBorderOpacity.Value;
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

            switch (PlacementActive)
            {
                case PlacementState.Waiting:
                    anchorPosition = HitObject.TargetPosition;
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
            position = Vector2.Clamp(position, Vector2.Zero, UI.DodgePlayfield.BASE_SIZE);

            if (PlacementActive == PlacementState.Waiting)
            {
                if (!editorBeatmap.HitObjects.OfType<DodgeArenaChange>().Any())
                    HitObject.StartTime = 0;

                HitObject.TargetPosition = position;
                HitObject.TargetSize = new Vector2(DodgeArenaChange.MIN_SIZE);
                // Arena transitions always begin as four beats. Unlike projectiles,
                // their duration is not a placement-tool setting to inherit from a
                // previous arena; inheriting it can make a new arena unexpectedly
                // span most of the map.
                HitObject.Duration = beatSnapProvider.GetBeatLengthAtTime(HitObject.StartTime) * 4;
            }
            else if (PlacementActive == PlacementState.Active)
            {
                Vector2 topLeft = Vector2.ComponentMin(anchorPosition, position);
                Vector2 bottomRight = Vector2.ComponentMax(anchorPosition, position);

                HitObject.TargetPosition = topLeft;
                HitObject.TargetSize = new Vector2(
                    Math.Max(DodgeArenaChange.MIN_SIZE, bottomRight.X - topLeft.X),
                    Math.Max(DodgeArenaChange.MIN_SIZE, bottomRight.Y - topLeft.Y));
                HitObject.ClampToBaseBounds();
            }

            DodgeEditorTrackBounds.Constrain(HitObject, EditorClock.TrackLength, true);
            return new SnapResult(ToScreenSpace(position), fallbackTime);
        }

        public override bool ReplacesExistingObject(HitObject existing)
            => existing is DodgeArenaChange && base.ReplacesExistingObject(existing);
    }
}
