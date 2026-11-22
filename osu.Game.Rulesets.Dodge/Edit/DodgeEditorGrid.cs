// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Game.Utils;
using osuTK;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Screens.Edit.Compose.Components;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeEditorGrid : RectangularPositionSnapGrid
    {
        public bool IsGridVisible => Alpha > 0;

        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        private DodgeGridToolboxGroup gridToolbox = null!;

        public DodgeEditorGrid()
        {
            Size = DodgePlayfield.BASE_SIZE;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(DodgeEditorSettings settings, DodgeGridToolboxGroup gridToolbox)
        {
            this.gridToolbox = gridToolbox;
            Spacing.BindTo(gridToolbox.SpacingVector);
            GridLineRotation.BindTo(gridToolbox.GridLinesRotation);
            settings.GridSnapEnabled.BindValueChanged(enabled => Alpha = enabled.NewValue ? 1 : 0, true);
        }

        protected override void Update()
        {
            // Draw just the visible world window, not a 512 × 384 patch and
            // not an enormous offscreen buffer covering the whole map.
            Vector2 topLeft = Parent!.ToLocalSpace(composer.ScreenSpaceDrawQuad.TopLeft);
            Vector2 bottomRight = Parent.ToLocalSpace(composer.ScreenSpaceDrawQuad.BottomRight);
            Position = topLeft;
            Size = Vector2.ComponentMax(Vector2.One, bottomRight - topLeft);
            Vector2 worldOrigin = gridToolbox.StartPosition.Value;
            Vector2 relative = GeometryUtils.RotateVector(topLeft + Size / 2 - worldOrigin, GridLineRotation.Value);
            Vector2 drawSpacing = Spacing.Value * drawingSpacingFactor();
            Vector2 steps = Vector2.Divide(relative, drawSpacing);
            Vector2 nearCentre = worldOrigin + GeometryUtils.RotateVector(Vector2.Multiply(
                new Vector2(MathF.Round(steps.X), MathF.Round(steps.Y)), drawSpacing), -GridLineRotation.Value);
            StartPosition.Value = nearCentre - Position;
            base.Update();
        }

        protected override void CreateContent()
        {
            // Coarsen only the drawing when zoomed out. Actual snapping always
            // uses the mapper's chosen grid spacing.
            float factor = drawingSpacingFactor();
            var rotation = Quaternion.FromAxisAngle(Vector3.UnitZ, MathHelper.DegreesToRadians(GridLineRotation.Value));
            GenerateGridLines(Vector2.Transform(new Vector2(0, Spacing.Value.Y * factor), rotation), DrawSize);
            GenerateGridLines(Vector2.Transform(new Vector2(0, -Spacing.Value.Y * factor), rotation), DrawSize);
            GenerateGridLines(Vector2.Transform(new Vector2(Spacing.Value.X * factor, 0), rotation), DrawSize);
            GenerateGridLines(Vector2.Transform(new Vector2(-Spacing.Value.X * factor, 0), rotation), DrawSize);
        }

        private float drawingSpacingFactor()
        {
            float screenScale = ScreenSpaceDrawQuad.Width / Math.Max(1, DrawWidth);
            float factor = 1;
            while (Math.Min(Spacing.Value.X, Spacing.Value.Y) * factor * screenScale < 8 && factor < 65536)
                factor *= 2;
            return factor;
        }

        public override Vector2 GetSnappedPosition(Vector2 original)
        {
            Vector2 origin = gridToolbox.StartPosition.Value;
            Vector2 relative = GeometryUtils.RotateVector(original - origin, GridLineRotation.Value);
            Vector2 steps = Vector2.Divide(relative, Spacing.Value);
            return origin + GeometryUtils.RotateVector(Vector2.Multiply(
                new Vector2(MathF.Round(steps.X), MathF.Round(steps.Y)), Spacing.Value), -GridLineRotation.Value);
        }
    }
}
