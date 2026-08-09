// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeSelectionRotationHandler : SelectionRotationHandler
    {
        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private readonly BindableList<HitObject> selectedItems = new BindableList<HitObject>();
        private Dictionary<HitObject, (Vector2 Start, Vector2 End)>? originalPoints;
        private Dictionary<DodgeArenaChange, float>? originalArenaRotations;

        [BackgroundDependencyLoader]
        private void load(EditorBeatmap editorBeatmap)
        {
            selectedItems.BindTo(editorBeatmap.SelectedHitObjects);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            selectedItems.CollectionChanged += (_, _) => updateState();
            updateState();
        }

        private HitObject[] selectedObjects => DodgeSelectionTransformUtils.Transformable(selectedItems, true);

        private void updateState()
        {
            HitObject[] objects = selectedObjects;
            CanRotateAroundSelectionOrigin.Value = objects.Length > 0;
            CanRotateAroundPlayfieldOrigin.Value = objects.Length > 0;
        }

        public override void Begin()
        {
            if (OperationInProgress.Value)
                throw new InvalidOperationException("A rotation is already in progress.");

            base.Begin();
            changeHandler?.BeginChange();

            originalPoints = selectedObjects.ToDictionary(item => item, DodgeSelectionTransformUtils.GetPoints);
            originalArenaRotations = selectedObjects.OfType<DodgeArenaChange>().ToDictionary(item => item, item => item.TargetRotation);
            DefaultOrigin = DodgeSelectionTransformUtils.GetSurroundingQuad(originalPoints.Keys).Centre;
        }

        public override void Update(float rotation, Vector2? origin = null)
        {
            if (!OperationInProgress.Value || originalPoints == null || DefaultOrigin == null)
                throw new InvalidOperationException("Rotation must be started before it is updated.");

            Vector2 actualOrigin = origin ?? DefaultOrigin.Value;

            foreach (var (item, points) in originalPoints)
            {
                DodgeSelectionTransformUtils.SetPoints(
                    item,
                    GeometryUtils.RotatePointAroundOrigin(points.Start, actualOrigin, rotation),
                    GeometryUtils.RotatePointAroundOrigin(points.End, actualOrigin, rotation));

                if (item is DodgeArenaChange arena && originalArenaRotations != null && originalArenaRotations.TryGetValue(arena, out float initialRotation))
                {
                    arena.TargetRotation = initialRotation + rotation;
                }
            }
        }

        public override void Commit()
        {
            if (!OperationInProgress.Value)
                throw new InvalidOperationException("No rotation is in progress.");

            changeHandler?.EndChange();
            base.Commit();
            originalPoints = null;
            originalArenaRotations = null;
            DefaultOrigin = null;
        }
    }
}
