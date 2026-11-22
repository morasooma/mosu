// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Dodge.Edit.Blueprints;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeBlueprintContainer : ComposeBlueprintContainer
    {
        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        private readonly DodgeHitObjectComposer composer;
        private bool panning;
        private Vector2 panStartMouse;
        private Vector2 panStartCentre;
        private Vector2 panWorldPerPixel;

        // Navigation must work beyond the original 512 × 384 rectangle.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => composer.IsCanvasPosition(screenSpacePos);

        protected override void LoadComplete()
        {
            base.LoadComplete();
            AddInternal(new CanvasNavigation(this) { RelativeSizeAxes = osu.Framework.Graphics.Axes.Both, Depth = float.MinValue });
        }

        private bool beginPan(DragStartEvent e)
        {
            panning = true;
            panStartMouse = e.ScreenSpaceMouseDownPosition;
            panStartCentre = new Vector2(settings.EditorCentreX.Value, settings.EditorCentreY.Value);
            Vector2 origin = ToLocalSpace(panStartMouse);
            panWorldPerPixel = new Vector2(
                (ToLocalSpace(panStartMouse + Vector2.UnitX) - origin).X,
                (ToLocalSpace(panStartMouse + Vector2.UnitY) - origin).Y);
            return true;
        }

        private void pan(DragEvent e)
        {
            Vector2 centre = panStartCentre - Vector2.Multiply(e.ScreenSpaceMousePosition - panStartMouse, panWorldPerPixel);
            settings.EditorCentreX.Value = centre.X;
            settings.EditorCentreY.Value = centre.Y;
        }

        private bool zoom(ScrollEvent e)
        {
            Vector2 mouseWorld = ToLocalSpace(e.ScreenSpaceMousePosition);
            Vector2 centre = new Vector2(settings.EditorCentreX.Value, settings.EditorCentreY.Value);
            float oldZoom = settings.EditorZoom.Value;
            settings.EditorZoom.Value *= MathF.Pow(1.15f, e.ScrollDelta.Y);
            Vector2 newCentre = mouseWorld - (mouseWorld - centre) * (oldZoom / settings.EditorZoom.Value);
            settings.EditorCentreX.Value = newCentre.X;
            settings.EditorCentreY.Value = newCentre.Y;
            return true;
        }

        public DodgeBlueprintContainer(DodgeHitObjectComposer composer)
            : base(composer)
        {
            this.composer = composer;
        }

        // Navigation is opt-in. Do not steal middle-click deletion or modified
        // wheel commands from the editor's existing (and rebindable) actions.
        private partial class CanvasNavigation : osu.Framework.Graphics.Containers.CompositeDrawable
        {
            private readonly DodgeBlueprintContainer owner;

            public CanvasNavigation(DodgeBlueprintContainer owner) => this.owner = owner;

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                => owner.settings.NavigateEditorView.Value && owner.ReceivePositionalInputAt(screenSpacePos);

            protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;

            protected override bool OnDragStart(DragStartEvent e)
                => e.Button == MouseButton.Left && owner.settings.NavigateEditorView.Value && owner.beginPan(e);

            protected override void OnDrag(DragEvent e) => owner.pan(e);

            protected override void OnDragEnd(DragEndEvent e) => owner.panning = false;

            protected override bool OnScroll(ScrollEvent e)
                => !owner.panning && !e.ControlPressed && !e.AltPressed && !e.ShiftPressed && !e.SuperPressed && owner.zoom(e);
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
                DodgeTrigger trigger => new DodgeTriggerSelectionBlueprint(trigger),
                _ => null,
            };

        protected override void UpdateSelectionFromDragBox(HashSet<HitObject> selectionBeforeDrag)
        {
            var quad = DragBox.Box.ScreenSpaceDrawQuad;
            foreach (var blueprint in SelectionBlueprints)
            {
                Vector2 point = blueprint is DodgeBulletSelectionBlueprint bullet
                    ? bullet.ScreenSpaceProjectilePoint : blueprint.ScreenSpaceSelectionPoint;
                bool selected = selectionBeforeDrag.Contains(blueprint.Item)
                                || blueprint.IsSelectable && quad.Contains(point);
                if (selected)
                    blueprint.Select();
                else
                    blueprint.Deselect();
            }
        }

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
