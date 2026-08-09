// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeSelectionScaleHandler : SelectionScaleHandler
    {
        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private readonly BindableList<HitObject> selectedItems = new BindableList<HitObject>();
        private Dictionary<HitObject, (Vector2 Start, Vector2 End)>? originalPoints;
        private Dictionary<HitObject, float>? originalWaveAmplitudes;
        private Vector2? defaultOrigin;

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

        private HitObject[] selectedObjects => DodgeSelectionTransformUtils.Transformable(selectedItems);

        private void updateState()
        {
            var quad = DodgeSelectionTransformUtils.GetSurroundingQuad(selectedObjects);
            CanScaleX.Value = quad.Width > 0;
            CanScaleY.Value = quad.Height > 0;
            CanScaleDiagonally.Value = CanScaleX.Value && CanScaleY.Value;
        }

        public override void Begin()
        {
            if (OperationInProgress.Value)
                throw new InvalidOperationException("A scale is already in progress.");

            base.Begin();
            changeHandler?.BeginChange();

            originalPoints = selectedObjects.ToDictionary(item => item, DodgeSelectionTransformUtils.GetPoints);
            originalWaveAmplitudes = selectedObjects
                                     .Where(item => item is DodgeBullet or DodgeEmitter)
                                     .ToDictionary(item => item, item => ((DodgeHitObject)item).WaveAmplitude);
            OriginalSurroundingQuad = DodgeSelectionTransformUtils.GetSurroundingQuad(originalPoints.Keys);
            defaultOrigin = OriginalSurroundingQuad.Value.Centre;
        }

        public override void Update(Vector2 scale, Vector2? origin = null, Axes adjustAxis = Axes.Both, float axisRotation = 0)
        {
            if (!OperationInProgress.Value || originalPoints == null || defaultOrigin == null)
                throw new InvalidOperationException("Scaling must be started before it is updated.");

            if ((adjustAxis & Axes.X) == 0)
                scale.X = 1;
            if ((adjustAxis & Axes.Y) == 0)
                scale.Y = 1;

            Vector2 actualOrigin = origin ?? defaultOrigin.Value;

            foreach (var (item, points) in originalPoints)
            {
                DodgeSelectionTransformUtils.SetPoints(
                    item,
                    GeometryUtils.GetScaledPosition(scale, actualOrigin, points.Start, axisRotation),
                    GeometryUtils.GetScaledPosition(scale, actualOrigin, points.End, axisRotation));

                if (item is DodgeHitObject projectile && originalWaveAmplitudes != null
                                                      && originalWaveAmplitudes.TryGetValue(item, out float originalAmplitude))
                {
                    Vector2 direction = points.End - points.Start;
                    Vector2 normal = direction.LengthSquared == 0
                        ? Vector2.UnitY
                        : new Vector2(-direction.Y, direction.X).Normalized();
                    Vector2 scaledNormal = GeometryUtils.GetScaledPosition(scale, Vector2.Zero, normal, axisRotation);
                    projectile.WaveAmplitude = originalAmplitude * scaledNormal.Length;
                }
            }
        }

        public override void Commit()
        {
            if (!OperationInProgress.Value)
                throw new InvalidOperationException("No scale is in progress.");

            changeHandler?.EndChange();
            base.Commit();
            originalPoints = null;
            originalWaveAmplitudes = null;
            OriginalSurroundingQuad = null;
            defaultOrigin = null;
        }
    }
}
