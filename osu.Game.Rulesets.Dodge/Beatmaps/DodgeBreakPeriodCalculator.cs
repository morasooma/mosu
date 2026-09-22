// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Dodge.Beatmaps
{
    public static class DodgeBreakPeriodCalculator
    {
        /// <summary>
        /// Keeps the break overlay hidden until the projectile's exit/fade transform
        /// has fully completed.
        /// </summary>
        public const double PROJECTILE_CLEARANCE = 200;

        public static IReadOnlyList<BreakPeriod> ExcludeMovingProjectiles(
            IEnumerable<BreakPeriod> breaks,
            IEnumerable<DodgeHitObject> hitObjects,
            float bulletSize)
        {
            DodgeHitObject[] objects = hitObjects.ToArray();

            if (objects.Length == 0)
                return breaks.ToArray();

            double continuedBulletEndTime = DodgeGameplayTiming.GetContinuedProjectileEndTime(
                objects,
                DodgePlayfield.CONTINUED_BULLET_GRACE_PERIOD);
            var movementPeriods = new List<MovementPeriod>();

            foreach (DodgeBullet bullet in objects.OfType<DodgeBullet>())
            {
                double movementEndTime = bullet.ContinueUntilExit
                    ? DodgeTrajectory.CalculateExitTime(
                        bullet.StartTime,
                        bullet.Duration,
                        bullet.Position,
                        bullet.EndPosition,
                        bulletSize,
                        bullet.MovementType,
                        bullet.WaveAmplitude,
                        bullet.WaveCycles,
                        bullet.WavePhase,
                        bullet.MovementEasing)
                    : bullet.EndTime;

                addMovementPeriod(bullet.StartTime, Math.Min(movementEndTime, continuedBulletEndTime) + PROJECTILE_CLEARANCE);
            }

            foreach (DodgeEmitter emitter in objects.OfType<DodgeEmitter>())
            {
                double movementEndTime = emitter.EndTime;

                if (emitter.ContinueUntilExit)
                {
                    movementEndTime = Enumerable.Range(0, emitter.EffectiveBurstCount)
                                                .SelectMany(burst => Enumerable.Range(0, emitter.EffectiveBulletCount)
                                                                               .Select(index => DodgeTrajectory.CalculateExitTime(
                                                                                   emitter.EmissionTimeAt(burst),
                                                                                   emitter.Duration,
                                                                                   emitter.SourcePositionAt(burst),
                                                                                   emitter.EndPositionAt(burst, index),
                                                                                   bulletSize,
                                                                                   emitter.MovementType,
                                                                                   emitter.WaveAmplitude,
                                                                                   emitter.WaveCycles,
                                                                                   emitter.WavePhase,
                                                                                   emitter.MovementEasing)))
                                                .Max();
                }

                addMovementPeriod(emitter.StartTime, Math.Min(movementEndTime, continuedBulletEndTime) + PROJECTILE_CLEARANCE);
            }

            foreach (DodgeBeam beam in objects.OfType<DodgeBeam>())
            {
                addMovementPeriod(beam.StartTime, beam.EndTime + PROJECTILE_CLEARANCE);
            }

            MovementPeriod[] mergedMovementPeriods = mergePeriods(movementPeriods);
            var adjustedBreaks = new List<BreakPeriod>();

            foreach (BreakPeriod breakPeriod in breaks)
            {
                double availableStart = breakPeriod.StartTime;

                foreach (MovementPeriod movement in mergedMovementPeriods)
                {
                    if (movement.EndTime <= availableStart)
                        continue;

                    if (movement.StartTime >= breakPeriod.EndTime)
                        break;

                    if (movement.StartTime > availableStart)
                        addBreak(availableStart, Math.Min(movement.StartTime, breakPeriod.EndTime));

                    availableStart = Math.Max(availableStart, movement.EndTime);

                    if (availableStart >= breakPeriod.EndTime)
                        break;
                }

                if (availableStart < breakPeriod.EndTime)
                    addBreak(availableStart, breakPeriod.EndTime);
            }

            return adjustedBreaks;

            void addMovementPeriod(double startTime, double endTime)
            {
                if (endTime > startTime)
                    movementPeriods.Add(new MovementPeriod(startTime, endTime));
            }

            void addBreak(double startTime, double endTime)
            {
                var adjusted = new BreakPeriod(startTime, endTime);

                if (adjusted.HasEffect)
                    adjustedBreaks.Add(adjusted);
            }
        }

        private static MovementPeriod[] mergePeriods(IEnumerable<MovementPeriod> periods)
        {
            MovementPeriod[] ordered = periods.OrderBy(period => period.StartTime).ToArray();

            if (ordered.Length == 0)
                return ordered;

            var merged = new List<MovementPeriod> { ordered[0] };

            for (int i = 1; i < ordered.Length; i++)
            {
                MovementPeriod current = ordered[i];
                MovementPeriod previous = merged[^1];

                if (current.StartTime <= previous.EndTime)
                    merged[^1] = new MovementPeriod(previous.StartTime, Math.Max(previous.EndTime, current.EndTime));
                else
                    merged.Add(current);
            }

            return merged.ToArray();
        }

        private readonly struct MovementPeriod
        {
            public readonly double StartTime;
            public readonly double EndTime;

            public MovementPeriod(double startTime, double endTime)
            {
                StartTime = startTime;
                EndTime = endTime;
            }
        }
    }
}
