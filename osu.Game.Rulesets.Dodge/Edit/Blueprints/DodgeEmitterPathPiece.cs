// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Objects.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeEmitterPathPiece : CompositeDrawable
    {
        private readonly DodgeBulletVisual source;
        private readonly DodgeBulletVisual movementEnd;
        private readonly Box movementPath;
        private readonly List<TrajectoryPiece> trajectories = new List<TrajectoryPiece>();
        private EmitterPathState cachedState;
        private bool stateValid;

        public RectangleF GamefieldBounds { get; private set; }

        internal int GeometryRebuildCount { get; private set; }

        public DodgeEmitterPathPiece()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                movementPath = new Box
                {
                    Origin = Anchor.CentreLeft,
                    Height = 2,
                    Alpha = 0,
                    Colour = Color4.Cyan,
                },
                source = new DodgeBulletVisual
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(DodgeBullet.SIZE),
                    Colour = Color4.White,
                },
                movementEnd = new DodgeBulletVisual
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(DodgeBullet.SIZE),
                    Colour = Color4.White,
                    Alpha = 0,
                },
            };
        }

        public void UpdateFrom(DodgeEmitter emitter)
        {
            var state = new EmitterPathState(
                emitter.Position,
                emitter.AimPosition,
                emitter.MovementEndPosition,
                emitter.MoveSource,
                emitter.EffectiveBulletCount,
                emitter.EffectiveSpreadAngle,
                emitter.EffectiveBurstCount,
                emitter.BulletSize,
                emitter.Shape,
                emitter.Colour,
                emitter.OutlineColour,
                emitter.OutlineThickness,
                Math.Clamp(emitter.Opacity, 0, 1),
                emitter.MovementType,
                emitter.WaveAmplitude,
                Math.Max(1, emitter.WaveCycles),
                emitter.WavePhase);

            if (stateValid && state == cachedState)
                return;

            cachedState = state;
            stateValid = true;
            GeometryRebuildCount++;

            int raysPerBurst = emitter.EffectiveBulletCount;
            ensureTrajectoryCount(raysPerBurst);

            source.Position = emitter.Position;
            source.Size = new Vector2(emitter.BulletSize);
            source.Shape = emitter.Shape;
            source.Direction = DodgeTrajectory.TangentAtProgress(
                emitter.Position,
                emitter.AimPosition,
                0,
                emitter.MovementType,
                emitter.WaveAmplitude,
                emitter.WaveCycles,
                emitter.WavePhase);
            source.FillColour = emitter.Colour;
            source.OutlineColour = emitter.OutlineColour;
            source.OutlineThickness = emitter.OutlineThickness;
            source.Alpha = Math.Clamp(emitter.Opacity, 0, 1);

            Vector2 movementDelta = emitter.MovementEndPosition - emitter.Position;
            bool hasMovement = emitter.MoveSource && emitter.EffectiveBurstCount > 1 && movementDelta.LengthSquared > 0;
            movementPath.Position = emitter.Position;
            movementPath.Width = movementDelta.Length;
            movementPath.Rotation = MathHelper.RadiansToDegrees(MathF.Atan2(movementDelta.Y, movementDelta.X));
            movementPath.Colour = emitter.Colour;
            movementPath.Alpha = hasMovement ? 0.55f * Math.Clamp(emitter.Opacity, 0, 1) : 0;
            movementEnd.Position = emitter.MovementEndPosition;
            movementEnd.Size = new Vector2(emitter.BulletSize);
            movementEnd.Shape = emitter.Shape;
            movementEnd.Direction = movementDelta;
            movementEnd.FillColour = emitter.Colour;
            movementEnd.OutlineColour = emitter.OutlineColour;
            movementEnd.OutlineThickness = emitter.OutlineThickness;
            movementEnd.Alpha = hasMovement ? 0.8f * Math.Clamp(emitter.Opacity, 0, 1) : 0;

            float left = emitter.Position.X;
            float top = emitter.Position.Y;
            float right = emitter.Position.X;
            float bottom = emitter.Position.Y;

            if (emitter.MoveSource)
                include(emitter.MovementEndPosition);

            for (int i = 0; i < trajectories.Count; i++)
            {
                int rayIndex = i;
                Vector2 burstSource = emitter.Position;
                Vector2 controlEndPosition = emitter.EndPositionAt(rayIndex);
                TrajectoryPiece trajectory = trajectories[i];

                trajectory.Path.Colour = emitter.Colour;
                trajectory.Path.Alpha = 0.28f * Math.Clamp(emitter.Opacity, 0, 1);
                trajectory.Path.ClearVertices();
                IReadOnlyList<Vector2> vertices = DodgeTrajectory.CreatePathVertices(
                    burstSource,
                    controlEndPosition,
                    1,
                    emitter.MovementType,
                    emitter.WaveAmplitude,
                    emitter.WaveCycles,
                    emitter.WavePhase);

                foreach (Vector2 vertex in vertices)
                {
                    trajectory.Path.AddVertex(vertex);
                    include(vertex);
                }

                trajectory.Path.Position = -trajectory.Path.PositionInBoundingBox(Vector2.Zero);

                trajectory.End.Position = controlEndPosition;
                trajectory.End.Size = new Vector2(emitter.BulletSize);
                trajectory.End.Shape = emitter.Shape;
                trajectory.End.Direction = DodgeTrajectory.TangentAtProgress(
                    burstSource,
                    controlEndPosition,
                    1,
                    emitter.MovementType,
                    emitter.WaveAmplitude,
                    emitter.WaveCycles,
                    emitter.WavePhase);
                trajectory.End.FillColour = emitter.Colour;
                trajectory.End.OutlineColour = emitter.OutlineColour;
                trajectory.End.OutlineThickness = emitter.OutlineThickness;
                trajectory.End.Alpha = 0.8f * Math.Clamp(emitter.Opacity, 0, 1);
            }

            GamefieldBounds = new RectangleF(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));

            void include(Vector2 point)
            {
                left = Math.Min(left, point.X);
                top = Math.Min(top, point.Y);
                right = Math.Max(right, point.X);
                bottom = Math.Max(bottom, point.Y);
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (source.ReceivePositionalInputAt(screenSpacePos))
                return true;

            if (movementEnd.Alpha > 0 && movementEnd.ReceivePositionalInputAt(screenSpacePos))
                return true;

            if (movementPath.Alpha > 0 && movementPath.ReceivePositionalInputAt(screenSpacePos))
                return true;

            foreach (TrajectoryPiece trajectory in trajectories)
            {
                if (trajectory.Path.ReceivePositionalInputAt(screenSpacePos)
                    || trajectory.End.ReceivePositionalInputAt(screenSpacePos))
                    return true;
            }

            return false;
        }

        private void ensureTrajectoryCount(int required)
        {
            while (trajectories.Count < required)
            {
                var trajectory = new TrajectoryPiece();
                trajectories.Add(trajectory);
                AddInternal(trajectory.Path);
                AddInternal(trajectory.End);
            }

            while (trajectories.Count > required)
            {
                TrajectoryPiece trajectory = trajectories[^1];
                trajectories.RemoveAt(trajectories.Count - 1);
                RemoveInternal(trajectory.Path, true);
                RemoveInternal(trajectory.End, true);
            }
        }

        private class TrajectoryPiece
        {
            public readonly Path Path = new Path
            {
                AutoSizeAxes = Axes.None,
                Size = UI.DodgePlayfield.BASE_SIZE,
                PathRadius = 0.5f,
                Alpha = 0.28f,
                Colour = Color4.White,
            };

            public readonly DodgeBulletVisual End = new DodgeBulletVisual
            {
                Origin = Anchor.Centre,
                Size = new Vector2(DodgeBullet.SIZE),
                Alpha = 0.8f,
                Colour = Color4.White,
            };
        }

        private readonly record struct EmitterPathState(
            Vector2 Position,
            Vector2 AimPosition,
            Vector2 MovementEndPosition,
            bool MoveSource,
            int BulletCount,
            float SpreadAngle,
            int BurstCount,
            float BulletSize,
            DodgeBulletShape Shape,
            Colour4 Colour,
            Colour4 OutlineColour,
            float OutlineThickness,
            float Opacity,
            DodgeMovementType MovementType,
            float WaveAmplitude,
            int WaveCycles,
            float WavePhase);
    }
}
