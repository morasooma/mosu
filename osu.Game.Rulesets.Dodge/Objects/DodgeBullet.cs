// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects.Types;
using Newtonsoft.Json;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// The first Dodge gameplay object: a bullet moving linearly between two points.
    /// </summary>
    public class DodgeBullet : DodgeHitObject, IHasPosition, IHasDuration, IEditorTimelineEndTimeAdjustable, IContributesToGameplayDuration
    {
        public const float SIZE = 12;

        public float BulletSize { get; private set; } = SIZE;

        public Vector2 Position { get; set; }

        public Vector2 EndPosition { get; set; }

        public DodgeBulletShape Shape { get; set; }

        public bool ContinueUntilExit { get; set; }

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
            set => Duration = value - StartTime;
        }

        public double Duration { get; set; } = 1000;

        public void SetEditorTimelineEndTime(double endTime)
            => Duration = Math.Max(0, endTime - StartTime);

        public double MovementEndTime
        {
            get
            {
                double exitTime = DodgeTrajectory.CalculateExitTime(
                    StartTime,
                    Duration,
                    Position,
                    EndPosition,
                    BulletSize,
                    MovementType,
                    WaveAmplitude,
                    WaveCycles,
                    WavePhase,
                    MovementEasing);

                return ContinueUntilExit ? exitTime : EndTime;
            }
        }

        public Vector2 TrajectoryEndPosition => ContinueUntilExit
            ? DodgeTrajectory.CalculateExitPosition(
                Position,
                EndPosition,
                BulletSize,
                MovementType,
                WaveAmplitude,
                WaveCycles,
                WavePhase,
                MovementEasing)
            : EndPosition;

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);
            BulletSize = DodgeBeatmapSettings.GetBulletSize(difficulty);
        }

        public Vector2 Direction
        {
            get
            {
                Vector2 direction = EndPosition - Position;

                if (direction.LengthSquared == 0)
                    return Vector2.Zero;

                direction.Normalize();
                return direction;
            }
        }

        public Vector2 PositionAt(double time)
        {
            if (Duration <= 0)
                return EndPosition;

            double maximumProgress = (MovementEndTime - StartTime) / Duration;
            float progress = (float)Math.Clamp((time - StartTime) / Duration, 0, maximumProgress);
            return DodgeTrajectory.PositionAtProgress(
                Position,
                EndPosition,
                progress,
                MovementType,
                WaveAmplitude,
                WaveCycles,
                WavePhase,
                MovementEasing);
        }

        public Vector2 DirectionAt(double time)
        {
            float progress = Duration <= 0
                ? 1
                : (float)Math.Clamp((time - StartTime) / Duration, 0, (MovementEndTime - StartTime) / Duration);
            return DodgeTrajectory.TangentAtProgress(
                Position,
                EndPosition,
                progress,
                MovementType,
                WaveAmplitude,
                WaveCycles,
                WavePhase,
                MovementEasing);
        }

        public static bool IntersectsPlayer(Vector2 bulletPosition, Vector2 playerPosition, float bulletSize = SIZE, float playerSize = UI.DodgePlayer.SIZE)
        {
            float collisionDistance = (bulletSize + playerSize) / 2;

            return Math.Abs(bulletPosition.X - playerPosition.X) <= collisionDistance
                   && Math.Abs(bulletPosition.Y - playerPosition.Y) <= collisionDistance;
        }

        public static bool IntersectsPlayerSwept(
            Vector2 previousBulletPosition,
            Vector2 bulletPosition,
            Vector2 previousPlayerPosition,
            Vector2 playerPosition,
            float bulletSize = SIZE,
            float playerSize = UI.DodgePlayer.SIZE)
        {
            float collisionDistance = (bulletSize + playerSize) / 2;

            return relativeSegmentIntersectsBox(
                previousBulletPosition - previousPlayerPosition,
                bulletPosition - playerPosition,
                collisionDistance);
        }

        public static bool IsWithinGrazeDistance(
            Vector2 bulletPosition,
            Vector2 playerPosition,
            float grazeDistance,
            float bulletSize = SIZE,
            float playerSize = UI.DodgePlayer.SIZE)
        {
            float grazeRadius = (bulletSize + playerSize) / 2 + Math.Max(0, grazeDistance);

            return Math.Abs(bulletPosition.X - playerPosition.X) <= grazeRadius
                   && Math.Abs(bulletPosition.Y - playerPosition.Y) <= grazeRadius;
        }

        public static bool IsWithinGrazeDistanceSwept(
            Vector2 previousBulletPosition,
            Vector2 bulletPosition,
            Vector2 previousPlayerPosition,
            Vector2 playerPosition,
            float grazeDistance,
            float bulletSize = SIZE,
            float playerSize = UI.DodgePlayer.SIZE)
        {
            float grazeRadius = (bulletSize + playerSize) / 2 + Math.Max(0, grazeDistance);

            return relativeSegmentIntersectsBox(
                previousBulletPosition - previousPlayerPosition,
                bulletPosition - playerPosition,
                grazeRadius);
        }

        private static bool relativeSegmentIntersectsBox(Vector2 start, Vector2 end, float halfExtent)
        {
            Vector2 delta = end - start;
            float entry = 0;
            float exit = 1;

            return clipAxis(start.X, delta.X, halfExtent, ref entry, ref exit)
                   && clipAxis(start.Y, delta.Y, halfExtent, ref entry, ref exit);
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
