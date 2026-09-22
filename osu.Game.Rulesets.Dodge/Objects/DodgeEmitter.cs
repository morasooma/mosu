// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
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
    /// Emits several bullets from one point at the same time.
    /// The aim position controls the centre direction and travel distance.
    /// </summary>
    public class DodgeEmitter : DodgeHitObject, IHasPosition, IHasDuration, IEditorTimelineEndTimeAdjustable, IContributesToGameplayDuration
    {
        public const int DEFAULT_BULLET_COUNT = 5;
        public const int MIN_BULLET_COUNT = 2;
        public const int MAX_BULLET_COUNT = 32;

        public const float DEFAULT_SPREAD_ANGLE = 90;
        public const float MIN_SPREAD_ANGLE = 0;
        public const float MAX_SPREAD_ANGLE = 360;

        public const int DEFAULT_BURST_COUNT = 4;
        public const int MIN_BURST_COUNT = 1;
        public const int MAX_BURST_COUNT = 32;

        public const double DEFAULT_BURST_INTERVAL = 125;
        public const double MIN_BURST_INTERVAL = 25;
        public const double MAX_BURST_INTERVAL = 2000;

        public DodgeEmitter()
        {
            TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.Path;
        }

        public float BulletSize { get; private set; } = DodgeBullet.SIZE;

        public Vector2 Position { get; set; }

        public Vector2 AimPosition { get; set; }

        /// <summary>
        /// The source position used for the final burst. Intermediate bursts are distributed
        /// linearly between <see cref="Position"/> and this point.
        /// </summary>
        public Vector2 MovementEndPosition { get; set; }

        public DodgeBulletShape Shape { get; set; }

        public int BulletCount { get; set; } = DEFAULT_BULLET_COUNT;

        public float SpreadAngle { get; set; } = DEFAULT_SPREAD_ANGLE;

        public bool ContinueUntilExit { get; set; }

        public int BurstCount { get; set; } = MIN_BURST_COUNT;

        public double BurstInterval { get; set; } = DEFAULT_BURST_INTERVAL;

        /// <summary>
        /// The fraction of one beat between bursts. Zero preserves the exact
        /// millisecond interval of a legacy sidecar.
        /// </summary>
        public int BurstBeatDivisor { get; set; } = (int)DodgeEmitterBeatDivisor.Quarter;

        /// <summary>
        /// Additional angle applied to every successive burst. A non-zero value turns a repeated fan
        /// into a rotating spiral without requiring separate emitter objects.
        /// </summary>
        public float BurstRotation { get; set; }

        /// <summary>
        /// Whether repeated bursts move their source along
        /// <see cref="Position"/> to <see cref="MovementEndPosition"/>.
        /// </summary>
        public bool MoveSource { get; set; }

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
            get => StartTime + EmissionDuration + Duration;
            set => Duration = Math.Max(0, value - StartTime - EmissionDuration);
        }

        public double Duration { get; set; } = 1000;

        public void SetEditorTimelineEndTime(double endTime)
            => Duration = Math.Max(0, endTime - StartTime - EmissionDuration);

        public int EffectiveBulletCount => Math.Clamp(BulletCount, MIN_BULLET_COUNT, MAX_BULLET_COUNT);

        public float EffectiveSpreadAngle => Math.Clamp(SpreadAngle, MIN_SPREAD_ANGLE, MAX_SPREAD_ANGLE);

        public int EffectiveBurstCount => Math.Clamp(BurstCount, MIN_BURST_COUNT, MAX_BURST_COUNT);

        public double EffectiveBurstInterval => Math.Clamp(BurstInterval, MIN_BURST_INTERVAL, MAX_BURST_INTERVAL);

        public double EmissionDuration => (EffectiveBurstCount - 1) * EffectiveBurstInterval;

        public double MovementEndTime
        {
            get
            {
                double max = StartTime;
                int bursts = EffectiveBurstCount;
                int bullets = EffectiveBulletCount;

                for (int b = 0; b < bursts; b++)
                {
                    for (int i = 0; i < bullets; i++)
                    {
                        double exit = ExitTimeAt(b, i);
                        if (exit > max)
                            max = exit;
                    }
                }

                return max;
            }
        }

        public double EmissionTimeAt(int burstIndex)
        {
            burstIndex = Math.Clamp(burstIndex, 0, EffectiveBurstCount - 1);
            return StartTime + burstIndex * EffectiveBurstInterval;
        }

        public Vector2 SourcePositionAt(int burstIndex)
        {
            int count = EffectiveBurstCount;

            if (count <= 1 || !MoveSource)
                return Position;

            burstIndex = Math.Clamp(burstIndex, 0, count - 1);
            return Vector2.Lerp(Position, MovementEndPosition, (float)burstIndex / (count - 1));
        }

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);
            BulletSize = DodgeBeatmapSettings.GetBulletSize(difficulty);

            if (BurstBeatDivisor > 0)
                BurstInterval = IntervalForBeatLength(controlPointInfo.TimingPointAt(StartTime).BeatLength, BurstBeatDivisor);
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            base.CreateNestedHitObjects(cancellationToken);

            // Keep the final burst represented by the parent object, matching the
            // usual nested-before-parent judgement order used by score and health.
            for (int burstIndex = 0; burstIndex < EffectiveBurstCount - 1; burstIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddNested(new DodgeEmitterBurst
                {
                    StartTime = EmissionTimeAt(burstIndex),
                    BurstIndex = burstIndex,
                });
            }
        }

        public static double IntervalForBeatLength(double beatLength, int beatDivisor)
            => Math.Clamp(beatLength / Math.Max(1, beatDivisor), MIN_BURST_INTERVAL, MAX_BURST_INTERVAL);

        public Vector2 EndPositionAt(int index)
            => EndPositionAt(0, index);

        public Vector2 EndPositionAt(int burstIndex, int index)
        {
            Vector2 displacement = AimPosition - Position;
            Vector2 sourcePosition = SourcePositionAt(burstIndex);

            if (displacement == Vector2.Zero)
                return sourcePosition;

            float angle = MathHelper.DegreesToRadians(getAngleOffset(index) + BurstRotation * burstIndex);
            float sin = MathF.Sin(angle);
            float cos = MathF.Cos(angle);

            return sourcePosition + new Vector2(
                displacement.X * cos - displacement.Y * sin,
                displacement.X * sin + displacement.Y * cos);
        }

        public Vector2 PositionAt(int index, double time)
            => PositionAt(0, index, time);

        public Vector2 PositionAt(int burstIndex, int index, double time)
        {
            double emissionTime = EmissionTimeAt(burstIndex);

            if (Duration <= 0)
                return EndPositionAt(burstIndex, index);

            Vector2 sourcePosition = SourcePositionAt(burstIndex);
            Vector2 endPosition = EndPositionAt(burstIndex, index);
            double maximumProgress = (ExitTimeAt(burstIndex, index) - emissionTime) / Duration;
            float progress = (float)Math.Clamp((time - emissionTime) / Duration, 0, maximumProgress);
            return DodgeTrajectory.PositionAtProgress(
                sourcePosition,
                endPosition,
                progress,
                MovementType,
                WaveAmplitude,
                WaveCycles,
                WavePhase,
                MovementEasing);
        }

        public Vector2 DirectionAt(int burstIndex, int index, double time)
        {
            double emissionTime = EmissionTimeAt(burstIndex);
            float progress = Duration <= 0
                ? 1
                : (float)Math.Clamp((time - emissionTime) / Duration, 0, (ExitTimeAt(burstIndex, index) - emissionTime) / Duration);
            return DodgeTrajectory.TangentAtProgress(
                SourcePositionAt(burstIndex),
                EndPositionAt(burstIndex, index),
                progress,
                MovementType,
                WaveAmplitude,
                WaveCycles,
                WavePhase,
                MovementEasing);
        }

        public double ExitTimeAt(int index) => ExitTimeAt(0, index);

        public double ExitTimeAt(int burstIndex, int index)
        {
            double emissionTime = EmissionTimeAt(burstIndex);
            Vector2 sourcePosition = SourcePositionAt(burstIndex);
            Vector2 endPosition = EndPositionAt(burstIndex, index);

            if (!DodgeTrajectory.RayIntersectsPlayfield(sourcePosition, endPosition, BulletSize, WaveAmplitude))
                return emissionTime;

            return ContinueUntilExit
                ? DodgeTrajectory.CalculateExitTime(
                    emissionTime,
                    Duration,
                    sourcePosition,
                    endPosition,
                    BulletSize,
                    MovementType,
                    WaveAmplitude,
                    WaveCycles,
                    WavePhase,
                    MovementEasing)
                : emissionTime + Duration;
        }

        public Vector2 TrajectoryEndPositionAt(int index) => TrajectoryEndPositionAt(0, index);

        public Vector2 TrajectoryEndPositionAt(int burstIndex, int index)
        {
            Vector2 sourcePosition = SourcePositionAt(burstIndex);
            Vector2 endPosition = EndPositionAt(burstIndex, index);
            return ContinueUntilExit
                ? DodgeTrajectory.CalculateExitPosition(
                    sourcePosition,
                    endPosition,
                    BulletSize,
                    MovementType,
                    WaveAmplitude,
                    WaveCycles,
                    WavePhase,
                    MovementEasing)
                : endPosition;
        }

        private float getAngleOffset(int index)
        {
            int count = EffectiveBulletCount;
            index = Math.Clamp(index, 0, count - 1);
            float spread = EffectiveSpreadAngle;

            if (spread >= MAX_SPREAD_ANGLE)
                return index * MAX_SPREAD_ANGLE / count;

            return -spread / 2 + spread * index / (count - 1);
        }
    }
}
