// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Judgements;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    public partial class DrawableDodgeHitObject : DrawableHitObject<DodgeHitObject>, IDodgeCollisionSource
    {
        private DodgeBullet Bullet => (DodgeBullet)HitObject;

        [Resolved]
        private DodgePlayfield playfield { get; set; } = null!;

        private readonly DodgeBulletVisual body;
        private readonly DodgeDirectionIndicator directionIndicator;
        private DodgeTrajectoryGuide? trajectoryGuide;
        private bool grazeJudged;
        private double grazeTime = double.NegativeInfinity;
        private JudgementResult? grazeResult;
        private double collisionTime = double.NegativeInfinity;
        private bool completionPending;
        private BulletState cachedBulletState;
        private bool bulletCacheValid;
        private Vector2 movementDelta;
        private Vector2 direction;
        private double cachedMovementEndTime;
        private float maximumProgress;
        private bool trajectoryGuideShowsFullPath;
        private float trajectoryGuideStartProgress = float.NaN;
        private Vector2 spawnCameraAnchor;
        private int cameraAnchorVersion = -1;
        private Vector2 previousBulletPosition;
        private bool hasPreviousBulletPosition;

        public bool ShowFullTrajectory { get; set; }

        internal int TrajectoryGuideCount => trajectoryGuide == null ? 0 : 1;

        internal Vector2? TrajectoryGuideStartPosition
            => trajectoryGuide != null
                ? cachedBulletState.Position + trajectoryGuide.StartPosition
                : null;

        internal Vector2? TrajectoryGuideEndPosition
            => trajectoryGuide != null
                ? cachedBulletState.Position + trajectoryGuide.EndPosition
                : null;

        internal int TrajectoryGuideBufferedGeometryRebuildCount => trajectoryGuide?.BufferedGeometryRebuildCount ?? 0;

        internal int GeometryCacheRebuildCount { get; private set; }

        public double CollisionStartTime => Bullet.StartTime;

        public double CollisionEndTime => getEffectiveMovementEndTime();

        public bool CollisionProcessingComplete => Result.HasResult;

        protected override double InitialLifetimeOffset => Bullet.TimePreempt;

        public DrawableDodgeHitObject(DodgeHitObject hitObject)
            : base(hitObject)
        {
            if (hitObject is not DodgeBullet)
                throw new System.ArgumentException($"Unsupported Dodge object type: {hitObject.GetType().Name}", nameof(hitObject));

            Size = new Vector2(48);
            Origin = Anchor.Centre;
            AddRangeInternal(new Drawable[]
            {
                body = new DodgeBulletVisual
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(Bullet.BulletSize),
                    Colour = Color4.White,
                    Alpha = 0,
                },
                directionIndicator = new DodgeDirectionIndicator
                {
                    Anchor = Anchor.Centre,
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            playfield.RegisterCollisionSource(this);
        }

        public override IEnumerable<HitSampleInfo> GetSamples() => Array.Empty<HitSampleInfo>();

        public override void PlaySamples()
        {
        }

        protected override void Update()
        {
            base.Update();

            bool showFullTrajectory = ShowFullTrajectory
                                      || playfield.ShowFullProjectilePaths
                                      || Bullet.TrajectoryGuideStyle == DodgeTrajectoryGuideStyle.FullPath;
            syncTrajectoryGuide(showFullTrajectory || Bullet.TrajectoryGuideStyle == DodgeTrajectoryGuideStyle.Path);
            updateBulletCache();

            double movementEndTime = getEffectiveMovementEndTime();
            Vector2 currentBulletPosition = positionAt(Math.Min(Time.Current, movementEndTime));
            previousBulletPosition = hasPreviousBulletPosition ? Position : currentBulletPosition;
            Position = currentBulletPosition;
            hasPreviousBulletPosition = true;

            if (cachedBulletState.Shape != DodgeBulletShape.Circle
                && cachedBulletState.MovementType == DodgeMovementType.Sine
                && cachedBulletState.WaveAmplitude != 0)
            {
                body.Direction = tangentAt(Math.Min(Time.Current, movementEndTime));
            }

            if (trajectoryGuide != null)
            {
                if (showFullTrajectory)
                {
                    float startProgress = progressAt(Math.Min(Time.Current, movementEndTime));

                    if (startProgress != trajectoryGuideStartProgress)
                        rebuildTrajectoryGuide(true, startProgress);
                }

                Vector2 uncameredPosition = DodgeTrajectory.PositionAtProgress(
                    cachedBulletState.Position,
                    cachedBulletState.EndPosition,
                    progressAt(Math.Min(Time.Current, movementEndTime)),
                    cachedBulletState.MovementType,
                    cachedBulletState.WaveAmplitude,
                    cachedBulletState.WaveCycles,
                    cachedBulletState.WavePhase,
                    cachedBulletState.MovementEasing);
                trajectoryGuide.Position = cachedBulletState.Position - uncameredPosition;
                bool visible = showFullTrajectory
                    ? Time.Current <= movementEndTime
                    : Time.Current < cachedBulletState.StartTime;
                trajectoryGuide.Alpha = visible
                    ? 0.24f * cachedBulletState.Opacity
                    : 0;
            }

            if (!Result.HasResult)
                updateAppearance(movementEndTime);

            if (Time.Current < cachedBulletState.StartTime)
                rewindState(Time.Current);
        }

        public void ProcessCollisions(
            double currentTime,
            Vector2 previousPlayerPosition,
            Vector2 currentPlayerPosition,
            bool allowSweptCollision,
            bool isRewind)
        {
            if (!bulletCacheValid)
                updateBulletCache();

            if (isRewind)
                rewindState(currentTime);

            double movementEndTime = getEffectiveMovementEndTime();

            if (playfield.CollisionEnabled
                && !Result.HasResult
                && playfield.ProjectileExists(Bullet.StartTime, currentTime)
                && currentTime >= cachedBulletState.StartTime
                && currentTime <= movementEndTime)
            {
                float maxGrazeDistance = (cachedBulletState.BulletSize + playfield.PlayerSize) / 2 + Math.Max(0, playfield.GrazeDistance);
                Vector2 playerBoxMin = Vector2.ComponentMin(previousPlayerPosition, currentPlayerPosition) - new Vector2(maxGrazeDistance);
                Vector2 playerBoxMax = Vector2.ComponentMax(previousPlayerPosition, currentPlayerPosition) + new Vector2(maxGrazeDistance);
                float bulletMinX = Math.Min(previousBulletPosition.X, Position.X);
                float bulletMaxX = Math.Max(previousBulletPosition.X, Position.X);
                float bulletMinY = Math.Min(previousBulletPosition.Y, Position.Y);
                float bulletMaxY = Math.Max(previousBulletPosition.Y, Position.Y);

                if (bulletMaxX < playerBoxMin.X || bulletMinX > playerBoxMax.X || bulletMaxY < playerBoxMin.Y || bulletMinY > playerBoxMax.Y)
                {
                    return;
                }

                Vector2 currentPosition = allowSweptCollision ? default : positionAt(currentTime);
                double sampleStartTime = Math.Max(cachedBulletState.StartTime, currentTime - Math.Max(0, Clock.ElapsedFrameTime));
                bool withinGrazeRange = allowSweptCollision
                    ? intersectsPlayerAlongTrajectory(
                        sampleStartTime,
                        currentTime,
                        previousPlayerPosition,
                        currentPlayerPosition,
                        playfield.GrazeDistance)
                    : DodgeBullet.IsWithinGrazeDistance(
                        currentPosition,
                        currentPlayerPosition,
                        playfield.GrazeDistance,
                        cachedBulletState.BulletSize,
                        playfield.PlayerSize);
                bool intersects = withinGrazeRange
                                  && (playfield.GrazeDistance <= 0
                                      || (allowSweptCollision
                                          ? intersectsPlayerAlongTrajectory(
                                              sampleStartTime,
                                              currentTime,
                                              previousPlayerPosition,
                                              currentPlayerPosition,
                                              0)
                                          : DodgeBullet.IntersectsPlayer(
                                              currentPosition,
                                              currentPlayerPosition,
                                              cachedBulletState.BulletSize,
                                              playfield.PlayerSize)));

                if (intersects)
                {
                    if (!grazeJudged)
                    {
                        grazeJudged = true;
                        grazeTime = currentTime;
                        grazeResult = playfield.RegisterGraze(currentTime, false);
                    }

                    collisionTime = currentTime;
                    playfield.TriggerMissFeedback();
                    ApplyMinResult();
                }
                else if (withinGrazeRange)
                {
                    playfield.ReportGrazeProximity();

                    if (!grazeJudged)
                    {
                        grazeJudged = true;
                        grazeTime = currentTime;
                        grazeResult = playfield.RegisterGraze(currentTime, true);
                    }
                }
            }

            if (!Result.HasResult && completionPending)
            {
                completionPending = false;
                completeSuccessfully(currentTime);
            }
        }

        private void updateAppearance(double movementEndTime)
        {
            // Cleared by a map ClearBullets trigger: hide instantly and fairly, without
            // touching the result (no miss, no score change).
            if (!playfield.ProjectileExists(Bullet.StartTime, Time.Current))
            {
                body.Alpha = 0;
                directionIndicator.Alpha = 0;

                if (trajectoryGuide != null)
                    trajectoryGuide.Alpha = 0;

                return;
            }

            if (Time.Current > movementEndTime)
            {
                // Result reversion happens before children update. During a
                // backwards scrub this drawable can therefore be Idle for one
                // frame even though the target time is already past its real
                // end. Do not flash the expired body while its result is being
                // re-applied later in the same frame.
                body.Alpha = 0;
                directionIndicator.Alpha = 0;
                return;
            }

            double timeFromStart = Time.Current - cachedBulletState.StartTime;

            if (timeFromStart < 0)
            {
                float progress = (float)Math.Clamp(1 + timeFromStart / Math.Max(1, cachedBulletState.TimePreempt), 0, 1);

                body.Alpha = progress * cachedBulletState.Opacity;
                body.Scale = new Vector2(0.85f + 0.15f * progress);
                bool showArrow = cachedBulletState.TrajectoryGuideStyle == DodgeTrajectoryGuideStyle.Arrow
                                 && !(ShowFullTrajectory
                                      || playfield.ShowFullProjectilePaths
                                      || cachedBulletState.TrajectoryGuideStyle == DodgeTrajectoryGuideStyle.FullPath);
                directionIndicator.Alpha = !showArrow || direction == Vector2.Zero
                    ? 0
                    : Math.Min(progress * 5, 1) * 0.55f * cachedBulletState.Opacity;
                return;
            }

            body.Alpha = cachedBulletState.Opacity;
            body.Scale = Vector2.One;
            directionIndicator.Alpha = 0;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!bulletCacheValid)
                updateBulletCache();

            if (!DodgeGameplayTiming.HasReachedJudgementTime(
                    Time.Current,
                    getEffectiveMovementEndTime(),
                    playfield.GameplayEndTime))
                return;

            completionPending = false;
            completeSuccessfully(Time.Current);
        }

        private void completeSuccessfully(double time)
        {
            if (!grazeJudged)
            {
                grazeJudged = true;
                grazeTime = time;
                grazeResult = playfield.RegisterGraze(time, false);
            }

            ApplyMaxResult();
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            switch (state)
            {
                case ArmedState.Hit:
                    this.ScaleTo(0.6f, 120).FadeOut(120).Expire();
                    break;

                case ArmedState.Miss:
                    this.ScaleTo(1.8f, 160).FadeOut(160).Expire();
                    break;
            }
        }

        private void syncTrajectoryGuide(bool enabled)
        {
            if (enabled == (trajectoryGuide != null))
                return;

            // The line is an opt-in trajectory visual. Avoid carrying an extra
            // drawable for projectiles using arrows or hidden guides.
            if (enabled)
            {
                trajectoryGuide = new DodgeTrajectoryGuide
                {
                    Anchor = Anchor.Centre,
                    Size = DodgePlayfield.BASE_SIZE * 4,
                    Alpha = 0,
                    Colour = Color4.White,
                    Depth = 1,
                };
                AddInternal(trajectoryGuide);
                bulletCacheValid = false;
            }
            else
            {
                RemoveInternal(trajectoryGuide!, true);
                trajectoryGuide = null;
            }
        }

        private void updateBulletCache()
        {
            bool showFullTrajectory = ShowFullTrajectory
                                      || playfield.ShowFullProjectilePaths
                                      || Bullet.TrajectoryGuideStyle == DodgeTrajectoryGuideStyle.FullPath;
            var state = new BulletState(
                Bullet.StartTime,
                Bullet.Duration,
                Bullet.TimePreempt,
                Bullet.Position,
                Bullet.EndPosition,
                Bullet.ContinueUntilExit,
                Bullet.BulletSize,
                Bullet.Shape,
                Bullet.Colour,
                Bullet.OutlineColour,
                Math.Clamp(Bullet.OutlineThickness, 0, 8),
                Math.Clamp(Bullet.Opacity, 0, 1),
                Bullet.MovementType,
                Bullet.MovementEasing,
                Bullet.WaveAmplitude,
                Math.Max(1, Bullet.WaveCycles),
                Bullet.WavePhase,
                Bullet.TrajectoryGuideStyle);

            if (bulletCacheValid && state == cachedBulletState)
            {
                if (trajectoryGuide != null && trajectoryGuideShowsFullPath != showFullTrajectory)
                    rebuildTrajectoryGuide(showFullTrajectory);

                return;
            }

            cachedBulletState = state;
            movementDelta = state.EndPosition - state.Position;
            direction = DodgeTrajectory.TangentAtProgress(
                state.Position,
                state.EndPosition,
                0,
                state.MovementType,
                state.WaveAmplitude,
                state.WaveCycles,
                state.WavePhase,
                state.MovementEasing);

            if (direction.LengthSquared > 0)
                direction.Normalize();

            double exitTime = playfield.HasCameraChanges
                ? DodgeTrajectory.CalculateExitTimeWithCamera(
                    state.StartTime,
                    state.Duration,
                    state.Position,
                    state.EndPosition,
                    state.BulletSize,
                    state.MovementType,
                    state.WaveAmplitude,
                    state.WaveCycles,
                    state.WavePhase,
                    playfield.CameraOffsetAt,
                    playfield.ContinuedBulletEndTime,
                    state.MovementEasing)
                : DodgeTrajectory.CalculateExitTime(
                    state.StartTime,
                    state.Duration,
                    state.Position,
                    state.EndPosition,
                    state.BulletSize,
                    state.MovementType,
                    state.WaveAmplitude,
                    state.WaveCycles,
                    state.WavePhase,
                    state.MovementEasing);

            cachedMovementEndTime = state.ContinueUntilExit ? exitTime : state.StartTime + state.Duration;
            maximumProgress = state.Duration > 0
                ? (float)((cachedMovementEndTime - state.StartTime) / state.Duration)
                : 0;
            body.Size = new Vector2(state.BulletSize);
            body.Shape = state.Shape;
            body.Direction = direction;
            body.FillColour = state.FillColour;
            body.OutlineColour = state.OutlineColour;
            body.OutlineThickness = state.OutlineThickness;
            directionIndicator.Colour = state.FillColour;
            directionIndicator.Position = direction * (state.BulletSize / 2 + 16);
            directionIndicator.Rotation = rotationFor(direction);

            if (trajectoryGuide != null)
                rebuildTrajectoryGuide(showFullTrajectory);

            bulletCacheValid = true;
            GeometryCacheRebuildCount++;
        }

        private void rebuildTrajectoryGuide(bool showFullTrajectory, float startProgress = 0)
        {
            trajectoryGuide!.Colour = cachedBulletState.FillColour;
            trajectoryGuide.SetGeometry(
                Vector2.Zero,
                movementDelta,
                showFullTrajectory ? startProgress : 0,
                showFullTrajectory ? maximumProgress : 1,
                cachedBulletState.MovementType,
                cachedBulletState.WaveAmplitude,
                cachedBulletState.WaveCycles,
                cachedBulletState.WavePhase,
                cachedBulletState.MovementEasing);

            trajectoryGuideShowsFullPath = showFullTrajectory;
            trajectoryGuideStartProgress = showFullTrajectory ? startProgress : 0;
        }

        private float progressAt(double time)
            => cachedBulletState.Duration <= 0
                ? maximumProgress
                : (float)Math.Clamp((time - cachedBulletState.StartTime) / cachedBulletState.Duration, 0, maximumProgress);

        private Vector2 positionAt(double time)
        {
            Vector2 trajectoryPosition;

            if (cachedBulletState.Duration <= 0)
            {
                trajectoryPosition = cachedBulletState.EndPosition;
            }
            else
            {
                float progress = (float)Math.Clamp(
                    (time - cachedBulletState.StartTime) / cachedBulletState.Duration,
                    0,
                    maximumProgress);
                trajectoryPosition = DodgeTrajectory.PositionAtProgress(
                    cachedBulletState.Position,
                    cachedBulletState.EndPosition,
                    progress,
                    cachedBulletState.MovementType,
                    cachedBulletState.WaveAmplitude,
                    cachedBulletState.WaveCycles,
                    cachedBulletState.WavePhase,
                    cachedBulletState.MovementEasing);
            }

            // Scroll drift: the bullet rides the field scroll accumulated since its own
            // spawn, so it always appears where it was placed and then drifts with the field.
            if (cameraAnchorVersion != playfield.CameraStateVersion)
            {
                spawnCameraAnchor = playfield.CameraOffsetAt(cachedBulletState.StartTime);
                cameraAnchorVersion = playfield.CameraStateVersion;
            }

            return trajectoryPosition + playfield.CameraOffsetAt(time) - spawnCameraAnchor;
        }

        private Vector2 tangentAt(double time)
        {
            float progress = cachedBulletState.Duration <= 0
                ? 1
                : (float)Math.Clamp(
                    (time - cachedBulletState.StartTime) / cachedBulletState.Duration,
                    0,
                    maximumProgress);
            return DodgeTrajectory.TangentAtProgress(
                cachedBulletState.Position,
                cachedBulletState.EndPosition,
                progress,
                cachedBulletState.MovementType,
                cachedBulletState.WaveAmplitude,
                cachedBulletState.WaveCycles,
                cachedBulletState.WavePhase,
                cachedBulletState.MovementEasing);
        }

        private bool intersectsPlayerAlongTrajectory(
            double startTime,
            double endTime,
            Vector2 playerStart,
            Vector2 playerEnd,
            float grazeDistance)
        {
            int subdivisions = DodgeTrajectory.SuggestedSubdivisionCount(
                startTime,
                endTime,
                cachedBulletState.StartTime,
                cachedBulletState.Duration,
                cachedBulletState.MovementType,
                cachedBulletState.WaveCycles,
                cachedBulletState.MovementEasing);

            for (int i = 0; i < subdivisions; i++)
            {
                double firstTime = startTime + (endTime - startTime) * i / subdivisions;
                double secondTime = startTime + (endTime - startTime) * (i + 1) / subdivisions;
                Vector2 firstPlayer = Vector2.Lerp(playerStart, playerEnd, (float)i / subdivisions);
                Vector2 secondPlayer = Vector2.Lerp(playerStart, playerEnd, (float)(i + 1) / subdivisions);

                bool intersects = grazeDistance > 0
                    ? DodgeBullet.IsWithinGrazeDistanceSwept(
                        positionAt(firstTime),
                        positionAt(secondTime),
                        firstPlayer,
                        secondPlayer,
                        grazeDistance,
                        cachedBulletState.BulletSize,
                        playfield.PlayerSize)
                    : DodgeBullet.IntersectsPlayerSwept(
                        positionAt(firstTime),
                        positionAt(secondTime),
                        firstPlayer,
                        secondPlayer,
                        cachedBulletState.BulletSize,
                        playfield.PlayerSize);

                if (intersects)
                    return true;
            }

            return false;
        }

        private double getEffectiveMovementEndTime()
            => cachedMovementEndTime;

        private void rewindState(double time)
        {
            // A seek can revert a result whose RawTime was recorded at a much
            // later frame even though the target time is still past this
            // bullet's actual end. Keep completion pending in that case so the
            // result is immediately re-applied and the expired bullet cannot
            // remain visible on the editor playfield.
            completionPending = time >= getEffectiveMovementEndTime();

            if (grazeJudged && grazeTime > time)
            {
                playfield.RevertGraze(grazeResult);
                grazeJudged = false;
                grazeTime = double.NegativeInfinity;
                grazeResult = null;
            }

            if (collisionTime > time)
                collisionTime = double.NegativeInfinity;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                playfield?.UnregisterCollisionSource(this);

            base.Dispose(isDisposing);
        }

        private static float rotationFor(Vector2 value)
            => value.LengthSquared == 0
                ? 0
                : MathHelper.RadiansToDegrees(MathF.Atan2(value.Y, value.X));

        private readonly record struct BulletState(
            double StartTime,
            double Duration,
            double TimePreempt,
            Vector2 Position,
            Vector2 EndPosition,
            bool ContinueUntilExit,
            float BulletSize,
            DodgeBulletShape Shape,
            Colour4 FillColour,
            Colour4 OutlineColour,
            float OutlineThickness,
            float Opacity,
            DodgeMovementType MovementType,
            DodgeMovementEasing MovementEasing,
            float WaveAmplitude,
            int WaveCycles,
            float WavePhase,
            DodgeTrajectoryGuideStyle TrajectoryGuideStyle);
    }
}
