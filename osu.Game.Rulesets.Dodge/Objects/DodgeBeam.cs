// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Dodge.Judgements;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Types;
using Newtonsoft.Json;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// A beam danger zone defined by a line segment (Position → EndPosition)
    /// representing the beam centerline length and an adjustable thickness (BeamWidth).
    /// </summary>
    public class DodgeBeam : DodgeHitObject, IHasPosition, IHasDuration, IContributesToGameplayDuration
    {
        public const float DEFAULT_WIDTH = 40;
        public const float MIN_WIDTH = 8;
        public const float MAX_WIDTH = 256;

        public DodgeBeam()
        {
            Colour = Colour4.White;
            Samples.Clear();
        }

        /// <summary>
        /// Start point of the beam centerline.
        /// </summary>
        public Vector2 Position { get; set; }

        /// <summary>
        /// End point of the beam centerline.
        /// Defines the beam length, direction, and orientation.
        /// </summary>
        public Vector2 EndPosition { get; set; }

        public float X
        {
            get => Position.X;
            set => Position = new Vector2(value, Y);
        }

        public float Y
        {
            get => Position.Y;
            set => Position = new Vector2(X, value);
        }

        [JsonIgnore]
        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = Math.Max(0, value - StartTime);
        }

        public double Duration { get; set; } = 500;

        /// <summary>
        /// Width (thickness) of the beam perpendicular to its centerline.
        /// </summary>
        public float BeamWidth { get; set; } = DEFAULT_WIDTH;

        protected override void ApplyDefaultsToSelf(osu.Game.Beatmaps.ControlPoints.ControlPointInfo controlPointInfo, osu.Game.Beatmaps.IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);
            Samples.Clear();
        }

        /// <summary>
        /// Length of the beam centerline between <see cref="Position"/> and <see cref="EndPosition"/>.
        /// </summary>
        [JsonIgnore]
        public float BeamLength => Math.Max(1f, (EndPosition - Position).Length);

        /// <summary>
        /// Midpoint of the beam centerline between <see cref="Position"/> and <see cref="EndPosition"/>.
        /// </summary>
        [JsonIgnore]
        public Vector2 BeamCenter => (Position + EndPosition) / 2;

        /// <summary>
        /// Normalised direction along the beam centerline (from <see cref="Position"/> to <see cref="EndPosition"/>).
        /// </summary>
        [JsonIgnore]
        public Vector2 BeamDirection
        {
            get
            {
                Vector2 d = EndPosition - Position;
                float len = d.Length;
                return len < float.Epsilon ? Vector2.UnitX : d / len;
            }
        }

        /// <summary>
        /// Normalised direction perpendicular to the beam centerline (thickness direction).
        /// </summary>
        [JsonIgnore]
        public Vector2 PerpendicularDirection
        {
            get
            {
                Vector2 dir = BeamDirection;
                return new Vector2(-dir.Y, dir.X);
            }
        }

        /// <summary>
        /// Rotation angle of the beam line in degrees.
        /// </summary>
        [JsonIgnore]
        public float BeamRotation => MathHelper.RadiansToDegrees(
            MathF.Atan2(EndPosition.Y - Position.Y, EndPosition.X - Position.X));

        /// <summary>
        /// Tests whether a player's position intersects the beam rectangle.
        /// </summary>
        public static bool IntersectsPlayer(
            Vector2 beamCenter,
            Vector2 beamDirection,
            Vector2 perpDirection,
            float beamLength,
            float beamWidth,
            Vector2 playerPosition,
            float playerSize)
        {
            Vector2 rel = playerPosition - beamCenter;
            float lengthDist = Math.Abs(Vector2.Dot(rel, beamDirection));
            float widthDist = Math.Abs(Vector2.Dot(rel, perpDirection));

            float halfLength = beamLength / 2 + playerSize / 2;
            float halfWidth = beamWidth / 2 + playerSize / 2;

            return lengthDist <= halfLength && widthDist <= halfWidth;
        }

        /// <summary>
        /// Tests whether a player is within graze distance of the beam rectangle.
        /// </summary>
        public static bool IsWithinGrazeDistance(
            Vector2 beamCenter,
            Vector2 beamDirection,
            Vector2 perpDirection,
            float beamLength,
            float beamWidth,
            Vector2 playerPosition,
            float playerSize,
            float grazeDistance)
        {
            Vector2 rel = playerPosition - beamCenter;
            float lengthDist = Math.Abs(Vector2.Dot(rel, beamDirection));
            float widthDist = Math.Abs(Vector2.Dot(rel, perpDirection));

            float extra = Math.Max(0, grazeDistance);
            float halfLength = beamLength / 2 + playerSize / 2 + extra;
            float halfWidth = beamWidth / 2 + playerSize / 2 + extra;

            return lengthDist <= halfLength && widthDist <= halfWidth;
        }

        public static bool IntersectsPlayerSwept(
            Vector2 beamCenter,
            Vector2 beamDirection,
            Vector2 perpDirection,
            float beamLength,
            float beamWidth,
            Vector2 previousPlayerPosition,
            Vector2 currentPlayerPosition,
            float playerSize)
            => IntersectsPlayerSwept(
                beamCenter,
                beamCenter,
                beamDirection,
                perpDirection,
                beamLength,
                beamWidth,
                previousPlayerPosition,
                currentPlayerPosition,
                playerSize);

        public static bool IntersectsPlayerSwept(
            Vector2 previousBeamCenter,
            Vector2 currentBeamCenter,
            Vector2 beamDirection,
            Vector2 perpDirection,
            float beamLength,
            float beamWidth,
            Vector2 previousPlayerPosition,
            Vector2 currentPlayerPosition,
            float playerSize)
        {
            Vector2 relStart = previousPlayerPosition - previousBeamCenter;
            Vector2 relEnd = currentPlayerPosition - currentBeamCenter;
            Vector2 localStart = new Vector2(Vector2.Dot(relStart, beamDirection), Vector2.Dot(relStart, perpDirection));
            Vector2 localEnd = new Vector2(Vector2.Dot(relEnd, beamDirection), Vector2.Dot(relEnd, perpDirection));

            float halfLength = beamLength / 2 + playerSize / 2;
            float halfWidth = beamWidth / 2 + playerSize / 2;

            return relativeSegmentIntersectsBox(localStart, localEnd, halfLength, halfWidth);
        }

        public static bool IsWithinGrazeDistanceSwept(
            Vector2 beamCenter,
            Vector2 beamDirection,
            Vector2 perpDirection,
            float beamLength,
            float beamWidth,
            Vector2 previousPlayerPosition,
            Vector2 currentPlayerPosition,
            float playerSize,
            float grazeDistance)
            => IsWithinGrazeDistanceSwept(
                beamCenter,
                beamCenter,
                beamDirection,
                perpDirection,
                beamLength,
                beamWidth,
                previousPlayerPosition,
                currentPlayerPosition,
                playerSize,
                grazeDistance);

        public static bool IsWithinGrazeDistanceSwept(
            Vector2 previousBeamCenter,
            Vector2 currentBeamCenter,
            Vector2 beamDirection,
            Vector2 perpDirection,
            float beamLength,
            float beamWidth,
            Vector2 previousPlayerPosition,
            Vector2 currentPlayerPosition,
            float playerSize,
            float grazeDistance)
        {
            Vector2 relStart = previousPlayerPosition - previousBeamCenter;
            Vector2 relEnd = currentPlayerPosition - currentBeamCenter;
            Vector2 localStart = new Vector2(Vector2.Dot(relStart, beamDirection), Vector2.Dot(relStart, perpDirection));
            Vector2 localEnd = new Vector2(Vector2.Dot(relEnd, beamDirection), Vector2.Dot(relEnd, perpDirection));

            float extra = Math.Max(0, grazeDistance);
            float halfLength = beamLength / 2 + playerSize / 2 + extra;
            float halfWidth = beamWidth / 2 + playerSize / 2 + extra;

            return relativeSegmentIntersectsBox(localStart, localEnd, halfLength, halfWidth);
        }

        private static bool relativeSegmentIntersectsBox(Vector2 start, Vector2 end, float halfExtentX, float halfExtentY)
        {
            Vector2 delta = end - start;
            float entry = 0;
            float exit = 1;

            return clipAxis(start.X, delta.X, halfExtentX, ref entry, ref exit)
                   && clipAxis(start.Y, delta.Y, halfExtentY, ref entry, ref exit);
        }

        private static bool clipAxis(float start, float delta, float halfExtent, ref float entry, ref float exit)
        {
            if (Math.Abs(delta) < float.Epsilon)
                return Math.Abs(start) <= halfExtent;

            float first = (-halfExtent - start) / delta;
            float second = (halfExtent - start) / delta;

            if (first > second)
                (first, second) = (second, first);

            entry = Math.Max(entry, first);
            exit = Math.Min(exit, second);
            return entry <= exit;
        }
    }
}
