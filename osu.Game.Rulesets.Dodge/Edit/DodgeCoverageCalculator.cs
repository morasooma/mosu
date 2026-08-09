// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>
    /// Calculates how many bullet trajectories can collide with the player at each editor grid position.
    /// </summary>
    public static class DodgeCoverageCalculator
    {
        public const int DEFAULT_COLUMNS = 64;
        public const int DEFAULT_ROWS = 48;

        public sealed class CalculationInput
        {
            internal readonly Trajectory[] Trajectories;
            internal readonly float PlayerSize;

            internal CalculationInput(Trajectory[] trajectories, float playerSize)
            {
                Trajectories = trajectories;
                PlayerSize = playerSize;
            }
        }

        /// <summary>
        /// Computes a lightweight fingerprint of the properties which can affect coverage.
        /// Arena position and size are intentionally excluded because coverage is expressed in base playfield coordinates.
        /// </summary>
        public static int CalculateStateHash(IEnumerable<DodgeHitObject> hitObjects, float playerSize = DodgePlayer.SIZE)
        {
            var hash = new HashCode();
            var threats = new List<DodgeHitObject>();

            foreach (DodgeHitObject hitObject in hitObjects)
            {
                switch (hitObject)
                {
                    case DodgeBullet bullet:
                        threats.Add(bullet);
                        hash.Add(bullet.StartTime);
                        hash.Add(bullet.Duration);
                        hash.Add(bullet.Position);
                        hash.Add(bullet.EndPosition);
                        hash.Add(bullet.BulletSize);
                        hash.Add(bullet.ContinueUntilExit);
                        hash.Add(bullet.MovementType);
                        hash.Add(bullet.WaveAmplitude);
                        hash.Add(bullet.WaveCycles);
                        hash.Add(bullet.WavePhase);
                        break;

                    case DodgeEmitter emitter:
                        threats.Add(emitter);
                        hash.Add(emitter.StartTime);
                        hash.Add(emitter.Duration);
                        hash.Add(emitter.Position);
                        hash.Add(emitter.AimPosition);
                        hash.Add(emitter.MovementEndPosition);
                        hash.Add(emitter.BulletSize);
                        hash.Add(emitter.BulletCount);
                        hash.Add(emitter.SpreadAngle);
                        hash.Add(emitter.BurstCount);
                        hash.Add(emitter.BurstInterval);
                        hash.Add(emitter.BurstBeatDivisor);
                        hash.Add(emitter.MoveSource);
                        hash.Add(emitter.ContinueUntilExit);
                        hash.Add(emitter.MovementType);
                        hash.Add(emitter.WaveAmplitude);
                        hash.Add(emitter.WaveCycles);
                        hash.Add(emitter.WavePhase);
                        break;
                }
            }

            hash.Add(DodgeGameplayTiming.GetGameplayEndTime(threats));
            hash.Add(playerSize);
            return hash.ToHashCode();
        }

        public static int[] CalculateHitCounts(
            IEnumerable<DodgeHitObject> hitObjects,
            int columns = DEFAULT_COLUMNS,
            int rows = DEFAULT_ROWS,
            float playerSize = DodgePlayer.SIZE)
            => CalculateHitCounts(CreateCalculationInput(hitObjects, playerSize), columns, rows);

        /// <summary>
        /// Captures all mutable hit object state needed by the expensive coverage calculation.
        /// This must be called on the editor update thread before calculation is moved to a worker thread.
        /// </summary>
        public static CalculationInput CreateCalculationInput(IEnumerable<DodgeHitObject> hitObjects, float playerSize = DodgePlayer.SIZE)
        {
            DodgeHitObject[] objects = hitObjects.ToArray();

            if (objects.Length == 0)
                return new CalculationInput(Array.Empty<Trajectory>(), playerSize);

            double gameplayEndTime = DodgeGameplayTiming.GetGameplayEndTime(objects);
            double continuedBulletEndTime = gameplayEndTime + DodgePlayfield.CONTINUED_BULLET_GRACE_PERIOD;
            var trajectories = new List<Trajectory>();

            foreach (DodgeBullet bullet in objects.OfType<DodgeBullet>())
            {
                double effectiveEndTime = Math.Min(bullet.MovementEndTime, continuedBulletEndTime);
                Vector2[] points = bullet.Duration <= 0
                    ? new[] { bullet.EndPosition, bullet.EndPosition }
                    : DodgeTrajectory.CreatePathVertices(
                                         bullet.Position,
                                         bullet.EndPosition,
                                         (float)Math.Max(0, (effectiveEndTime - bullet.StartTime) / bullet.Duration),
                                         bullet.MovementType,
                                         bullet.WaveAmplitude,
                                         bullet.WaveCycles,
                                         bullet.WavePhase)
                                     .ToArray();
                trajectories.Add(new Trajectory(points, bullet.BulletSize));
            }

            foreach (DodgeEmitter emitter in objects.OfType<DodgeEmitter>())
            {
                for (int burst = 0; burst < emitter.EffectiveBurstCount; burst++)
                {
                    for (int i = 0; i < emitter.EffectiveBulletCount; i++)
                    {
                        double effectiveEndTime = Math.Min(emitter.ExitTimeAt(burst, i), continuedBulletEndTime);
                        Vector2 endPosition = emitter.EndPositionAt(burst, i);
                        Vector2[] points = emitter.Duration <= 0
                            ? new[] { endPosition, endPosition }
                            : DodgeTrajectory.CreatePathVertices(
                                                 emitter.SourcePositionAt(burst),
                                                 endPosition,
                                                 (float)Math.Max(0, (effectiveEndTime - emitter.EmissionTimeAt(burst)) / emitter.Duration),
                                                 emitter.MovementType,
                                                 emitter.WaveAmplitude,
                                                 emitter.WaveCycles,
                                                 emitter.WavePhase)
                                             .ToArray();
                        trajectories.Add(new Trajectory(points, emitter.BulletSize));
                    }
                }
            }

            return new CalculationInput(trajectories.ToArray(), playerSize);
        }

        public static int[] CalculateHitCounts(CalculationInput input, int columns = DEFAULT_COLUMNS, int rows = DEFAULT_ROWS, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);

            var hitCounts = new int[columns * rows];

            foreach (Trajectory trajectory in input.Trajectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (int i = 1; i < trajectory.Points.Length; i++)
                {
                    addTrajectory(
                        hitCounts,
                        columns,
                        rows,
                        trajectory.Points[i - 1],
                        trajectory.Points[i],
                        trajectory.BulletSize,
                        input.PlayerSize,
                        cancellationToken);
                }
            }

            return hitCounts;
        }

        public static Vector2 CellCentre(int column, int row, int columns = DEFAULT_COLUMNS, int rows = DEFAULT_ROWS)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);

            return new Vector2(
                (column + 0.5f) * DodgePlayfield.WIDTH / columns,
                (row + 0.5f) * DodgePlayfield.HEIGHT / rows);
        }

        private static void addTrajectory(
            int[] hitCounts,
            int columns,
            int rows,
            Vector2 start,
            Vector2 end,
            float bulletSize,
            float playerSize,
            CancellationToken cancellationToken)
        {
            float cellWidth = DodgePlayfield.WIDTH / columns;
            float cellHeight = DodgePlayfield.HEIGHT / rows;
            float collisionDistance = (Math.Max(0, bulletSize) + Math.Max(0, playerSize)) / 2;

            int minimumColumn = Math.Clamp((int)Math.Floor((Math.Min(start.X, end.X) - collisionDistance) / cellWidth) - 1, 0, columns - 1);
            int maximumColumn = Math.Clamp((int)Math.Ceiling((Math.Max(start.X, end.X) + collisionDistance) / cellWidth) + 1, 0, columns - 1);
            int minimumRow = Math.Clamp((int)Math.Floor((Math.Min(start.Y, end.Y) - collisionDistance) / cellHeight) - 1, 0, rows - 1);
            int maximumRow = Math.Clamp((int)Math.Ceiling((Math.Max(start.Y, end.Y) + collisionDistance) / cellHeight) + 1, 0, rows - 1);

            for (int row = minimumRow; row <= maximumRow; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (int column = minimumColumn; column <= maximumColumn; column++)
                {
                    Vector2 centre = CellCentre(column, row, columns, rows);

                    if (segmentIntersectsBox(start, end, centre - new Vector2(collisionDistance), centre + new Vector2(collisionDistance)))
                        hitCounts[row * columns + column]++;
                }
            }
        }

        internal readonly struct Trajectory
        {
            public readonly Vector2[] Points;
            public readonly float BulletSize;

            public Trajectory(Vector2[] points, float bulletSize)
            {
                Points = points;
                BulletSize = bulletSize;
            }
        }

        private static bool segmentIntersectsBox(Vector2 start, Vector2 end, Vector2 minimum, Vector2 maximum)
        {
            Vector2 direction = end - start;
            float entry = 0;
            float exit = 1;

            return intersectAxis(start.X, direction.X, minimum.X, maximum.X, ref entry, ref exit)
                   && intersectAxis(start.Y, direction.Y, minimum.Y, maximum.Y, ref entry, ref exit)
                   && exit >= entry;
        }

        private static bool intersectAxis(float origin, float direction, float minimum, float maximum, ref float entry, ref float exit)
        {
            if (direction == 0)
                return origin >= minimum && origin <= maximum;

            float first = (minimum - origin) / direction;
            float second = (maximum - origin) / direction;

            if (first > second)
                (first, second) = (second, first);

            entry = Math.Max(entry, first);
            exit = Math.Min(exit, second);
            return exit >= entry;
        }
    }
}
