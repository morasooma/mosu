// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Objects.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeBulletPathPiece : CompositeDrawable
    {
        private readonly Path path;
        private readonly DodgeBulletVisual start;
        private readonly DodgeBulletVisual end;
        private BulletPathState cachedState;
        private bool stateValid;

        public RectangleF GamefieldBounds { get; private set; }

        internal int GeometryRebuildCount { get; private set; }

        internal Vector2 EndMarkerPosition => end.Position;

        public DodgeBulletPathPiece()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                path = new Path
                {
                    AutoSizeAxes = Axes.None,
                    RelativeSizeAxes = Axes.Both,
                    PathRadius = 0.5f,
                    Alpha = 0.32f,
                    Colour = Color4.White,
                },
                start = new DodgeBulletVisual
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(DodgeBullet.SIZE),
                    Colour = Color4.White,
                },
                end = new DodgeBulletVisual
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(DodgeBullet.SIZE),
                    Colour = Color4.White,
                },
            };
        }

        public void UpdateFrom(DodgeBullet bullet)
        {
            var state = new BulletPathState(
                bullet.Position,
                bullet.EndPosition,
                bullet.BulletSize,
                bullet.Shape,
                bullet.Colour,
                bullet.OutlineColour,
                bullet.OutlineThickness,
                Math.Clamp(bullet.Opacity, 0, 1),
                bullet.ContinueUntilExit,
                bullet.MovementType,
                bullet.WaveAmplitude,
                Math.Max(1, bullet.WaveCycles),
                bullet.WavePhase);

            if (stateValid && state == cachedState)
                return;

            cachedState = state;
            stateValid = true;
            GeometryRebuildCount++;

            float maximumProgress = bullet.ContinueUntilExit
                ? DodgeTrajectory.CalculateExitProgress(
                    bullet.Position,
                    bullet.EndPosition,
                    bullet.BulletSize,
                    bullet.MovementType,
                    bullet.WaveAmplitude,
                    bullet.WaveCycles,
                    bullet.WavePhase)
                : 1;

            start.Position = bullet.Position;
            end.Position = DodgeTrajectory.PositionAtProgress(
                bullet.Position,
                bullet.EndPosition,
                maximumProgress,
                bullet.MovementType,
                bullet.WaveAmplitude,
                bullet.WaveCycles,
                bullet.WavePhase);
            start.Size = end.Size = new Vector2(bullet.BulletSize);
            start.Shape = end.Shape = bullet.Shape;
            start.Direction = DodgeTrajectory.TangentAtProgress(
                bullet.Position,
                bullet.EndPosition,
                0,
                bullet.MovementType,
                bullet.WaveAmplitude,
                bullet.WaveCycles,
                bullet.WavePhase);
            end.Direction = DodgeTrajectory.TangentAtProgress(
                bullet.Position,
                bullet.EndPosition,
                maximumProgress,
                bullet.MovementType,
                bullet.WaveAmplitude,
                bullet.WaveCycles,
                bullet.WavePhase);
            start.FillColour = end.FillColour = bullet.Colour;
            start.OutlineColour = end.OutlineColour = bullet.OutlineColour;
            start.OutlineThickness = end.OutlineThickness = bullet.OutlineThickness;
            start.Alpha = end.Alpha = Math.Clamp(bullet.Opacity, 0, 1);
            path.Colour = bullet.Colour;
            path.Alpha = 0.32f * Math.Clamp(bullet.Opacity, 0, 1);
            path.ClearVertices();
            IReadOnlyList<Vector2> vertices = DodgeTrajectory.CreatePathVertices(
                bullet.Position,
                bullet.EndPosition,
                maximumProgress,
                bullet.MovementType,
                bullet.WaveAmplitude,
                bullet.WaveCycles,
                bullet.WavePhase);
            float left = bullet.Position.X;
            float top = bullet.Position.Y;
            float right = bullet.Position.X;
            float bottom = bullet.Position.Y;

            foreach (Vector2 vertex in vertices)
            {
                path.AddVertex(vertex);
                left = Math.Min(left, vertex.X);
                top = Math.Min(top, vertex.Y);
                right = Math.Max(right, vertex.X);
                bottom = Math.Max(bottom, vertex.Y);
            }

            // Path normalises its vertices into their bounding box internally.
            // Offset that normalisation so playfield coordinates (including
            // coordinates outside the playfield) remain aligned with the nodes.
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);
            GamefieldBounds = new RectangleF(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => start.ReceivePositionalInputAt(screenSpacePos)
               || end.ReceivePositionalInputAt(screenSpacePos)
               || path.ReceivePositionalInputAt(screenSpacePos);

        private readonly record struct BulletPathState(
            Vector2 Position,
            Vector2 EndPosition,
            float BulletSize,
            DodgeBulletShape Shape,
            Colour4 Colour,
            Colour4 OutlineColour,
            float OutlineThickness,
            float Opacity,
            bool ContinueUntilExit,
            DodgeMovementType MovementType,
            float WaveAmplitude,
            int WaveCycles,
            float WavePhase);
    }
}
