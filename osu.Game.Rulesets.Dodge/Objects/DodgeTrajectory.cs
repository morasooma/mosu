// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Dodge.UI;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects
{
    internal static class DodgeTrajectory
    {
        private const int exit_binary_search_iterations = 14;

        public static double CalculateExitTime(
            double startTime,
            double duration,
            Vector2 start,
            Vector2 controlEnd,
            float bulletSize,
            DodgeMovementType movementType = DodgeMovementType.Linear,
            float waveAmplitude = DodgeHitObject.DEFAULT_WAVE_AMPLITUDE,
            int waveCycles = DodgeHitObject.DEFAULT_WAVE_CYCLES,
            float wavePhase = 0)
        {
            if (duration <= 0)
                return startTime;

            return startTime + duration * CalculateExitProgress(
                start,
                controlEnd,
                bulletSize,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase);
        }

        public static Vector2 CalculateExitPosition(
            Vector2 start,
            Vector2 controlEnd,
            float bulletSize,
            DodgeMovementType movementType = DodgeMovementType.Linear,
            float waveAmplitude = DodgeHitObject.DEFAULT_WAVE_AMPLITUDE,
            int waveCycles = DodgeHitObject.DEFAULT_WAVE_CYCLES,
            float wavePhase = 0)
        {
            float progress = CalculateExitProgress(
                start,
                controlEnd,
                bulletSize,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase);
            return PositionAtProgress(start, controlEnd, progress, movementType, waveAmplitude, waveCycles, wavePhase);
        }

        public static float CalculateExitProgress(
            Vector2 start,
            Vector2 controlEnd,
            float bulletSize,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase)
        {
            if (movementType == DodgeMovementType.Linear)
                return 1 + calculateAdditionalProgress(start, controlEnd, bulletSize);

            float amplitude = Math.Abs(waveAmplitude);
            float searchEnd = 1 + calculateAdditionalProgress(start, controlEnd, bulletSize + amplitude * 2);

            if (searchEnd <= 1 || isOutside(PositionAtProgress(start, controlEnd, 1, movementType, waveAmplitude, waveCycles, wavePhase), bulletSize))
                return 1;

            int cycles = Math.Max(1, Math.Abs(waveCycles));
            int sampleCount = Math.Max(64, (int)Math.Ceiling((searchEnd - 1) * cycles * 32));
            float previous = 1;

            for (int i = 1; i <= sampleCount; i++)
            {
                float progress = 1 + (searchEnd - 1) * i / sampleCount;

                if (!isOutside(PositionAtProgress(start, controlEnd, progress, movementType, waveAmplitude, waveCycles, wavePhase), bulletSize))
                {
                    previous = progress;
                    continue;
                }

                float low = previous;
                float high = progress;

                for (int iteration = 0; iteration < exit_binary_search_iterations; iteration++)
                {
                    float middle = (low + high) / 2;

                    if (isOutside(PositionAtProgress(start, controlEnd, middle, movementType, waveAmplitude, waveCycles, wavePhase), bulletSize))
                        high = middle;
                    else
                        low = middle;
                }

                return high;
            }

            return searchEnd;
        }

        public static Vector2 PositionAtProgress(
            Vector2 start,
            Vector2 controlEnd,
            float progress,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase)
        {
            Vector2 displacement = controlEnd - start;
            Vector2 position = start + displacement * progress;

            if (movementType != DodgeMovementType.Sine || displacement.LengthSquared == 0 || waveAmplitude == 0)
                return position;

            Vector2 normal = new Vector2(-displacement.Y, displacement.X);
            normal.Normalize();
            int cycles = Math.Max(1, Math.Abs(waveCycles));
            float phase = MathHelper.DegreesToRadians(wavePhase);
            float offset = waveAmplitude * (MathF.Sin(MathHelper.TwoPi * cycles * progress + phase) - MathF.Sin(phase));
            return position + normal * offset;
        }

        public static Vector2 TangentAtProgress(
            Vector2 start,
            Vector2 controlEnd,
            float progress,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase)
        {
            Vector2 displacement = controlEnd - start;

            if (movementType != DodgeMovementType.Sine || displacement.LengthSquared == 0 || waveAmplitude == 0)
                return displacement;

            Vector2 normal = new Vector2(-displacement.Y, displacement.X);
            normal.Normalize();
            int cycles = Math.Max(1, Math.Abs(waveCycles));
            float phase = MathHelper.DegreesToRadians(wavePhase);
            return displacement
                   + normal * (waveAmplitude * MathHelper.TwoPi * cycles
                               * MathF.Cos(MathHelper.TwoPi * cycles * progress + phase));
        }

        public static IReadOnlyList<Vector2> CreatePathVertices(
            Vector2 start,
            Vector2 controlEnd,
            float maximumProgress,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase)
            => CreatePathVertices(
                start,
                controlEnd,
                0,
                maximumProgress,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase);

        public static IReadOnlyList<Vector2> CreatePathVertices(
            Vector2 start,
            Vector2 controlEnd,
            float minimumProgress,
            float maximumProgress,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase)
        {
            minimumProgress = Math.Max(0, minimumProgress);
            maximumProgress = Math.Max(minimumProgress, maximumProgress);
            int segmentCount = movementType == DodgeMovementType.Sine
                ? Math.Max(16, (int)Math.Ceiling((maximumProgress - minimumProgress) * Math.Max(1, Math.Abs(waveCycles)) * 24))
                : 1;
            var vertices = new Vector2[segmentCount + 1];

            for (int i = 0; i <= segmentCount; i++)
            {
                float progress = minimumProgress + (maximumProgress - minimumProgress) * i / segmentCount;
                vertices[i] = PositionAtProgress(start, controlEnd, progress, movementType, waveAmplitude, waveCycles, wavePhase);
            }

            return vertices;
        }

        public static int SuggestedSubdivisionCount(
            double startTime,
            double endTime,
            double movementStartTime,
            double duration,
            DodgeMovementType movementType,
            int waveCycles)
        {
            if (movementType != DodgeMovementType.Sine || duration <= 0)
                return 1;

            double overlapStart = Math.Max(movementStartTime, startTime);
            double overlapEnd = Math.Max(overlapStart, endTime);
            return Math.Max(1, (int)Math.Ceiling((overlapEnd - overlapStart) / duration * Math.Max(1, Math.Abs(waveCycles)) * 24));
        }

        private static float calculateAdditionalProgress(Vector2 start, Vector2 controlEnd, float bulletSize)
        {
            Vector2 direction = controlEnd - start;

            if (direction.LengthSquared == 0)
                return 0;

            float radius = Math.Max(0, bulletSize) / 2;
            float entry = 0;
            float exit = float.PositiveInfinity;

            if (!intersectAxis(controlEnd.X, direction.X, -radius, DodgePlayfield.WIDTH + radius, ref entry, ref exit)
                || !intersectAxis(controlEnd.Y, direction.Y, -radius, DodgePlayfield.HEIGHT + radius, ref entry, ref exit)
                || exit < entry
                || exit < 0
                || float.IsInfinity(exit))
            {
                return 0;
            }

            return exit;
        }

        private static bool isOutside(Vector2 position, float bulletSize)
        {
            float radius = Math.Max(0, bulletSize) / 2;
            return position.X < -radius
                   || position.X > DodgePlayfield.WIDTH + radius
                   || position.Y < -radius
                   || position.Y > DodgePlayfield.HEIGHT + radius;
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
