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
            float wavePhase = 0,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
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
                wavePhase,
                movementEasing);
        }

        public static double CalculateExitTimeWithCamera(
            double startTime,
            double duration,
            Vector2 start,
            Vector2 controlEnd,
            float bulletSize,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase,
            Func<double, Vector2>? cameraOffsetAt,
            double hardEndTime,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
        {
            if (duration <= 0)
                return startTime;

            if (cameraOffsetAt == null)
            {
                return CalculateExitTime(
                    startTime,
                    duration,
                    start,
                    controlEnd,
                    bulletSize,
                    movementType,
                    waveAmplitude,
                    waveCycles,
                    wavePhase,
                    movementEasing);
            }

            Vector2 cameraAnchor = cameraOffsetAt(startTime);
            Vector2 cameraAdjustedEnd = controlEnd + cameraOffsetAt(startTime + duration) - cameraAnchor;
            double estimatedExitTime = CalculateExitTime(
                startTime,
                duration,
                start,
                cameraAdjustedEnd,
                bulletSize,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase,
                movementEasing);

            double maxSearchTime = Math.Max(startTime + Math.Max(duration, 3000), Math.Max(hardEndTime, estimatedExitTime + 2000));
            double searchEndTime = Math.Min(maxSearchTime, Math.Max(startTime + duration, estimatedExitTime > startTime ? estimatedExitTime + 500 : startTime + 2000));

            while (true)
            {
                double exitTime = findCameraAwareExitTime(
                    startTime,
                    searchEndTime,
                    startTime,
                    duration,
                    start,
                    controlEnd,
                    bulletSize,
                    movementType,
                    waveAmplitude,
                    waveCycles,
                    wavePhase,
                    cameraAnchor,
                    cameraOffsetAt,
                    movementEasing);

                if (exitTime >= 0)
                    return exitTime;

                if (searchEndTime >= maxSearchTime)
                    return isOutside(start, bulletSize) ? startTime : maxSearchTime;

                searchEndTime = Math.Min(maxSearchTime, searchEndTime + Math.Max(duration, 2000));
            }
        }

        private static double findCameraAwareExitTime(
            double searchStartTime,
            double searchEndTime,
            double movementStartTime,
            double duration,
            Vector2 start,
            Vector2 controlEnd,
            float bulletSize,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase,
            Vector2 cameraAnchor,
            Func<double, Vector2> cameraOffsetAt,
            DodgeMovementEasing movementEasing)
        {
            Vector2 positionAtStart = cameraAwarePositionAt(
                searchStartTime,
                movementStartTime,
                duration,
                start,
                controlEnd,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase,
                cameraAnchor,
                cameraOffsetAt,
                movementEasing);

            bool hasEnteredPlayfield = !isOutside(positionAtStart, bulletSize);

            double searchDuration = Math.Max(0, searchEndTime - searchStartTime);
            if (searchDuration <= 0)
                return hasEnteredPlayfield ? searchStartTime : -1;

            int sampleCount = Math.Clamp(
                (int)Math.Ceiling(searchDuration / 20),
                32,
                160);
            double previousTime = searchStartTime;

            for (int i = 1; i <= sampleCount; i++)
            {
                double currentTime = searchStartTime + searchDuration * i / sampleCount;
                Vector2 currentPosition = cameraAwarePositionAt(
                    currentTime,
                    movementStartTime,
                    duration,
                    start,
                    controlEnd,
                    movementType,
                    waveAmplitude,
                    waveCycles,
                    wavePhase,
                    cameraAnchor,
                    cameraOffsetAt,
                    movementEasing);

                if (isOutside(currentPosition, bulletSize))
                {
                    if (!hasEnteredPlayfield)
                        continue;

                    double low = previousTime;
                    double high = currentTime;

                    for (int iteration = 0; iteration < exit_binary_search_iterations; iteration++)
                    {
                        double middle = (low + high) / 2;
                        Vector2 middlePosition = cameraAwarePositionAt(
                            middle,
                            movementStartTime,
                            duration,
                            start,
                            controlEnd,
                            movementType,
                            waveAmplitude,
                            waveCycles,
                            wavePhase,
                            cameraAnchor,
                            cameraOffsetAt,
                            movementEasing);

                        if (isOutside(middlePosition, bulletSize))
                            high = middle;
                        else
                            low = middle;
                    }

                    return high;
                }

                hasEnteredPlayfield = true;
                previousTime = currentTime;
            }

            return -1;
        }

        private static Vector2 cameraAwarePositionAt(
            double time,
            double startTime,
            double duration,
            Vector2 start,
            Vector2 controlEnd,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase,
            Vector2 cameraAnchor,
            Func<double, Vector2> cameraOffsetAt,
            DodgeMovementEasing movementEasing)
        {
            float progress = (float)((time - startTime) / duration);
            return PositionAtProgress(
                       start,
                       controlEnd,
                       progress,
                       movementType,
                       waveAmplitude,
                       waveCycles,
                       wavePhase,
                       movementEasing)
                   + cameraOffsetAt(time)
                   - cameraAnchor;
        }

        public static Vector2 CalculateExitPosition(
            Vector2 start,
            Vector2 controlEnd,
            float bulletSize,
            DodgeMovementType movementType = DodgeMovementType.Linear,
            float waveAmplitude = DodgeHitObject.DEFAULT_WAVE_AMPLITUDE,
            int waveCycles = DodgeHitObject.DEFAULT_WAVE_CYCLES,
            float wavePhase = 0,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
        {
            float progress = CalculateExitProgress(
                start,
                controlEnd,
                bulletSize,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase,
                movementEasing);
            return PositionAtProgress(start, controlEnd, progress, movementType, waveAmplitude, waveCycles, wavePhase, movementEasing);
        }

        public static float CalculateExitProgress(
            Vector2 start,
            Vector2 controlEnd,
            float bulletSize,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
        {
            if (!RayIntersectsPlayfield(start, controlEnd, bulletSize, waveAmplitude))
                return 0;

            if (movementType == DodgeMovementType.Linear)
                return CalculateLinearExitProgress(start, controlEnd, bulletSize);

            float amplitude = Math.Abs(waveAmplitude);
            float searchEnd = Math.Max(1, CalculateLinearExitProgress(start, controlEnd, bulletSize + amplitude * 2));

            if (searchEnd <= 0)
                return 0;

            int cycles = Math.Max(1, Math.Abs(waveCycles));
            int sampleCount = Math.Max(64, (int)Math.Ceiling(searchEnd * cycles * 32));
            float previous = 0;
            bool hasEnteredPlayfield = !isOutside(
                PositionAtProgress(start, controlEnd, 0, movementType, waveAmplitude, waveCycles, wavePhase, movementEasing),
                bulletSize);

            for (int i = 1; i <= sampleCount; i++)
            {
                float progress = searchEnd * i / sampleCount;

                if (!isOutside(PositionAtProgress(start, controlEnd, progress, movementType, waveAmplitude, waveCycles, wavePhase, movementEasing), bulletSize))
                {
                    hasEnteredPlayfield = true;
                    previous = progress;
                    continue;
                }

                if (!hasEnteredPlayfield)
                    continue;

                float low = previous;
                float high = progress;

                for (int iteration = 0; iteration < exit_binary_search_iterations; iteration++)
                {
                    float middle = (low + high) / 2;

                    if (isOutside(PositionAtProgress(start, controlEnd, middle, movementType, waveAmplitude, waveCycles, wavePhase, movementEasing), bulletSize))
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
            float wavePhase,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
        {
            progress = movementEasing.Apply(progress);
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

        public static bool IsOutsidePlayfield(Vector2 position, float bulletSize)
            => isOutside(position, bulletSize);

        public static Vector2 TangentAtProgress(
            Vector2 start,
            Vector2 controlEnd,
            float progress,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
        {
            progress = movementEasing.Apply(progress);
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
            float wavePhase,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
            => CreatePathVertices(
                start,
                controlEnd,
                0,
                maximumProgress,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase,
                movementEasing);

        public static IReadOnlyList<Vector2> CreatePathVertices(
            Vector2 start,
            Vector2 controlEnd,
            float minimumProgress,
            float maximumProgress,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
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
                vertices[i] = PositionAtProgress(start, controlEnd, progress, movementType, waveAmplitude, waveCycles, wavePhase, movementEasing);
            }

            return vertices;
        }

        public static int SuggestedSubdivisionCount(
            double startTime,
            double endTime,
            double movementStartTime,
            double duration,
            DodgeMovementType movementType,
            int waveCycles,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
        {
            if (movementType != DodgeMovementType.Sine || duration <= 0)
                return 1;

            double overlapStart = Math.Max(movementStartTime, startTime);
            double overlapEnd = Math.Max(overlapStart, endTime);
            int easingMultiplier = movementEasing == DodgeMovementEasing.Linear ? 1 : 2;
            return Math.Max(1, (int)Math.Ceiling((overlapEnd - overlapStart) / duration * Math.Max(1, Math.Abs(waveCycles)) * 24 * easingMultiplier));
        }

        public static float CalculateLinearExitProgress(Vector2 start, Vector2 controlEnd, float bulletSize)
        {
            Vector2 direction = controlEnd - start;

            if (direction.LengthSquared == 0)
                return isOutside(start, bulletSize) ? 0 : 1;

            float radius = Math.Max(0, bulletSize) / 2;
            float entry = float.NegativeInfinity;
            float exit = float.PositiveInfinity;

            if (!intersectAxis(start.X, direction.X, -radius, DodgePlayfield.WIDTH + radius, ref entry, ref exit)
                || !intersectAxis(start.Y, direction.Y, -radius, DodgePlayfield.HEIGHT + radius, ref entry, ref exit)
                || exit < entry
                || exit < 0)
            {
                return 0;
            }

            return float.IsInfinity(exit) ? 1 : exit;
        }

        public static bool RayIntersectsPlayfield(Vector2 start, Vector2 controlEnd, float bulletSize, float waveAmplitude = 0)
        {
            float radius = Math.Max(0, bulletSize) / 2 + Math.Abs(waveAmplitude);

            if (!isOutside(start, bulletSize + Math.Abs(waveAmplitude) * 2))
                return true;

            Vector2 direction = controlEnd - start;

            if (direction.LengthSquared == 0)
                return false;

            float entry = float.NegativeInfinity;
            float exit = float.PositiveInfinity;

            if (!intersectAxis(start.X, direction.X, -radius, DodgePlayfield.WIDTH + radius, ref entry, ref exit)
                || !intersectAxis(start.Y, direction.Y, -radius, DodgePlayfield.HEIGHT + radius, ref entry, ref exit))
            {
                return false;
            }

            return exit >= entry && exit >= 0;
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
