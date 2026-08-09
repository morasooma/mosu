// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Dodge.Edit.Blueprints;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeBlueprintContainer : ComposeBlueprintContainer
    {
        public DodgeBlueprintContainer(DodgeHitObjectComposer composer)
            : base(composer)
        {
        }

        protected override SelectionHandler<HitObject> CreateSelectionHandler() => new DodgeSelectionHandler();

        public override HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(HitObject hitObject)
            => hitObject switch
            {
                DodgeBullet bullet => new DodgeBulletSelectionBlueprint(bullet),
                DodgeEmitter emitter => new DodgeEmitterSelectionBlueprint(emitter),
                DodgeArenaChange arenaChange => new DodgeArenaChangeSelectionBlueprint(arenaChange),
                DodgeBeam beam => new DodgeBeamSelectionBlueprint(beam),
                DodgeCameraChange cameraChange => new DodgeCameraChangeSelectionBlueprint(cameraChange),
                _ => null,
            };

        protected override bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<HitObject> blueprint, Vector2[] originalSnapPositions)> blueprints)
        {
            Vector2 distanceTravelled = e.ScreenSpaceMousePosition - e.ScreenSpaceMouseDownPosition;
            var reference = blueprints[0].blueprint;
            Vector2 targetPosition = blueprints[0].originalSnapPositions[0] + distanceTravelled;
            Vector2 remainingDelta = targetPosition - reference.ScreenSpaceSelectionPoint;

            return SelectionHandler.HandleMovement(new MoveSelectionEvent<HitObject>(reference, remainingDelta));
        }
    }
}
