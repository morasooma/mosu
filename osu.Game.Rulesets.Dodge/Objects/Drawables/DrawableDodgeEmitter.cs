// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    public partial class DrawableDodgeEmitter : DrawableHitObject<DodgeHitObject>, IDodgeCollisionSource
    {
        private DodgeEmitter Emitter => (DodgeEmitter)HitObject;

        [Resolved]
        private DodgePlayfield playfield { get; set; } = null!;

        private readonly List<DodgeBulletVisual> bulletVisualPool = new List<DodgeBulletVisual>();
        private readonly Stack<DodgeBulletVisual> availableBulletVisuals = new Stack<DodgeBulletVisual>();
        private readonly List<DodgeDirectionIndicator> arrowPool = new List<DodgeDirectionIndicator>();
        private readonly Stack<DodgeDirectionIndicator> availableArrows = new Stack<DodgeDirectionIndicator>();
        private readonly List<DodgeTrajectoryGuide> trajectoryGuides = new List<DodgeTrajectoryGuide>();
        private int[] trajectoryGuideBulletIndices = Array.Empty<int>();
        private float[] trajectoryGuideStartProgress = Array.Empty<float>();
        private readonly List<int> activeBulletIndices = new List<int>();
        private BulletRuntimeState[] bulletStates = Array.Empty<BulletRuntimeState>();
        private DodgeBulletVisual?[] activeBulletVisuals = Array.Empty<DodgeBulletVisual?>();
        private DodgeDirectionIndicator?[] activeArrows = Array.Empty<DodgeDirectionIndicator?>();
        private readonly BulletLifetimeContainer bulletContainer;
        private readonly Container nestedBurstContainer;
        private readonly DodgeEmitterBulletBatch bulletBatch;
        private readonly DodgeBulletVisual batchingProbe;
        private EmitterState cachedEmitterState;
        private bool emitterCacheValid;
        private int cameraAnchorVersion = -1;
        private bool activeSetDirty = true;
        private int nextActivationIndex;
        private double previousSimulationTime = double.NaN;
        private double cachedMovementEndTime;
        private double earliestActiveExitTime = double.PositiveInfinity;
        private bool trajectoryGuidesShowFullPaths;
        private bool completionPending;
        private Vector2 activeCollisionBoundsMin;
        private Vector2 activeCollisionBoundsMax;
        private bool activeCollisionBoundsValid;
        private double activeCollisionBoundsTime = double.NaN;
        private bool usesBatchedBulletVisuals;
        private DrawableDodgeEmitterBurst?[] burstDrawables = Array.Empty<DrawableDodgeEmitterBurst?>();

        public bool ShowFullTrajectories { get; set; }

        internal int BulletVisualCount => bulletVisualPool.Count;

        internal int AliveBulletVisualCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < bulletVisualPool.Count; i++)
                {
                    if (bulletVisualPool[i].IsAlive)
                        count++;
                }

                return count;
            }
        }

        internal int TrajectoryGuideCount => trajectoryGuides.Count;

        internal Vector2? FirstTrajectoryGuideEndPosition
            => trajectoryGuides.Count > 0
                ? trajectoryGuides[0].EndPosition
                : null;

        internal Vector2? FirstTrajectoryGuideStartPosition
            => trajectoryGuides.Count > 0
                ? trajectoryGuides[0].StartPosition
                : null;

        internal int TrajectoryGuideBufferedGeometryRebuildCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < trajectoryGuides.Count; i++)
                    count += trajectoryGuides[i].BufferedGeometryRebuildCount;

                return count;
            }
        }

        internal int GeometryCacheRebuildCount { get; private set; }

        internal int ActiveSimulationCount => activeBulletIndices.Count;

        internal int BatchedBulletVisualCount => bulletBatch.StateCount;

        internal int BatchedBulletStorageCapacity => bulletBatch.StorageCapacity;

        internal Vector2? FirstBatchedBulletPosition => bulletBatch.FirstPosition;

        internal bool BulletBatchAlwaysPresent => bulletBatch.AlwaysPresent;

        internal int LastFrameSimulatedBulletCount { get; private set; }

        internal int LastFrameTrajectoryGuideCandidateChecks { get; private set; }

        internal int LastFrameTrajectoryIntersectionTestCount { get; private set; }

        internal int LastFrameDynamicDirectionUpdateCount { get; private set; }

        public double CollisionStartTime => Emitter.StartTime;

        public double CollisionEndTime => getEffectiveMovementEndTime();

        public bool CollisionProcessingComplete => allBurstsJudged() && nextActivationIndex >= bulletStates.Length && allActiveBulletsCollidedOrExited();

        protected override double InitialLifetimeOffset => Emitter.TimePreempt;

        public DrawableDodgeEmitter(DodgeEmitter hitObject)
            : base(hitObject)
        {
            RelativeSizeAxes = Axes.Both;
            AddInternal(nestedBurstContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
            });
            AddInternal(bulletContainer = new BulletLifetimeContainer
            {
                RelativeSizeAxes = Axes.Both,
            });
            bulletContainer.Add(bulletBatch = new DodgeEmitterBulletBatch());
            bulletContainer.Add(batchingProbe = new DodgeBulletVisual
            {
                Alpha = 0,
                Size = Vector2.One,
            });
            ensureBulletCount();
        }

        public override IEnumerable<HitSampleInfo> GetSamples() => Array.Empty<HitSampleInfo>();

        public override void PlaySamples()
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateEmitterCache();
            playfield.RegisterCollisionSource(this);
        }

        protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
            => hitObject is DodgeEmitterBurst burst
                ? new DrawableDodgeEmitterBurst(burst)
                : base.CreateNestedHitObject(hitObject);

        protected override void AddNestedHitObject(DrawableHitObject hitObject)
        {
            base.AddNestedHitObject(hitObject);
            nestedBurstContainer.Add(hitObject);
        }

        protected override void ClearNestedHitObjects()
        {
            base.ClearNestedHitObjects();
            nestedBurstContainer.Clear(false);
        }

        protected override void Update()
        {
            base.Update();

            bool showFullTrajectories = ShowFullTrajectories
                                        || playfield.ShowFullProjectilePaths
                                        || Emitter.TrajectoryGuideStyle == DodgeTrajectoryGuideStyle.FullPath;
            syncTrajectoryGuides(showFullTrajectories || Emitter.TrajectoryGuideStyle == DodgeTrajectoryGuideStyle.Path);
            updateEmitterCache();

            bool shouldBatchBulletVisuals = cachedEmitterState.Shape == DodgeBulletShape.Circle
                                            && cachedEmitterState.OutlineThickness <= 0
                                            && batchingProbe.UsesDefaultFastCircle;

            if (usesBatchedBulletVisuals != shouldBatchBulletVisuals)
            {
                for (int i = 0; i < activeBulletVisuals.Length; i++)
                    releaseBulletVisual(i);

                usesBatchedBulletVisuals = shouldBatchBulletVisuals;
            }

            bulletBatch.BeginFrame();

            double currentTime = Time.Current;
            Vector2 cameraOffsetNow = playfield.CameraOffsetAt(currentTime);
            updateActiveSet(currentTime);
            finishExitedBullets(currentTime);
            LastFrameSimulatedBulletCount = activeBulletIndices.Count;
            LastFrameDynamicDirectionUpdateCount = 0;
            activeCollisionBoundsMin = new Vector2(float.PositiveInfinity);
            activeCollisionBoundsMax = new Vector2(float.NegativeInfinity);
            activeCollisionBoundsValid = false;
            activeCollisionBoundsTime = currentTime;
            for (int activeIndex = 0; activeIndex < activeBulletIndices.Count; activeIndex++)
            {
                int i = activeBulletIndices[activeIndex];
                ref BulletRuntimeState state = ref bulletStates[i];
                CachedBullet cachedBullet = state.Geometry;
                bool bulletCleared = !playfield.ProjectileExists(cachedBullet.EmissionTime, currentTime);
                double exitTime = cachedBullet.ExitTime;
                double timeFromEmission = currentTime - cachedBullet.EmissionTime;
                float bulletAppearanceProgress = timeFromEmission < 0
                    ? (float)Math.Clamp(1 + timeFromEmission / Math.Max(1, cachedEmitterState.TimePreempt), 0, 1)
                    : 1;

                if (timeFromEmission < -cachedEmitterState.TimePreempt)
                    continue;

                if (currentTime > exitTime)
                {
                    DodgeBulletVisual? expiredBullet = activeBulletVisuals[i];

                    if (expiredBullet != null && expiredBullet.Alpha != 0)
                        expiredBullet.Alpha = 0;

                    continue;
                }

                Vector2 bulletPosition = cachedBullet.PositionAt(currentTime, exitTime, cachedEmitterState, cameraOffsetNow);
                Vector2 bulletDirection = cachedBullet.MovementDelta;
                updatePositionSample(ref state, currentTime, bulletPosition);

                if (!state.Collided && !bulletCleared && currentTime >= cachedBullet.EmissionTime)
                {
                    includeInCollisionBounds(state.CurrentPosition);
                    includeInCollisionBounds(state.PreviousPosition);
                }

                if (cachedEmitterState.MovementType == DodgeMovementType.Sine
                    && cachedEmitterState.WaveAmplitude != 0)
                {
                    bulletDirection = cachedBullet.TangentAt(currentTime, exitTime, cachedEmitterState);

                    if (cachedEmitterState.Shape != DodgeBulletShape.Circle)
                        LastFrameDynamicDirectionUpdateCount++;
                }

                float bulletAlpha = !state.Collided && !bulletCleared
                    ? bulletAppearanceProgress * cachedEmitterState.Opacity
                    : 0;
                float bulletScale = 0.85f + 0.15f * bulletAppearanceProgress;

                if (usesBatchedBulletVisuals)
                {
                    bulletBatch.Add(
                        bulletPosition,
                        cachedEmitterState.BulletSize * bulletScale,
                        cachedEmitterState.FillColour,
                        bulletAlpha);
                }
                else
                {
                    DodgeBulletVisual bullet = acquireBulletVisual(i);
                    bullet.Position = bulletPosition;
                    bullet.Direction = bulletDirection;
                    bullet.Alpha = bulletAlpha;
                    bullet.Scale = new Vector2(bulletScale);
                }

                bool showArrow = !showFullTrajectories
                                 && cachedEmitterState.TrajectoryGuideStyle == DodgeTrajectoryGuideStyle.Arrow
                                 && timeFromEmission < 0;
                DodgeDirectionIndicator? arrow = showArrow ? acquireArrow(i) : activeArrows[i];

                if (arrow != null)
                {
                    Vector2 arrowDirection = bulletDirection;

                    if (arrowDirection.LengthSquared > 0)
                        arrowDirection.Normalize();

                    arrow.Position = bulletPosition + arrowDirection * (cachedEmitterState.BulletSize / 2 + 16);
                    arrow.Rotation = arrowDirection.LengthSquared == 0
                        ? 0
                        : MathHelper.RadiansToDegrees(MathF.Atan2(arrowDirection.Y, arrowDirection.X));
                    arrow.Colour = cachedEmitterState.FillColour;
                    arrow.Alpha = showArrow && arrowDirection.LengthSquared > 0
                        ? Math.Min(bulletAppearanceProgress * 5, 1) * 0.55f * cachedEmitterState.Opacity
                        : 0;
                }
            }

            bulletBatch.EndFrame();

            if (trajectoryGuides.Count > 0)
            {
                LastFrameTrajectoryGuideCandidateChecks = 0;

                for (int rayIndex = 0; rayIndex < trajectoryGuides.Count; rayIndex++)
                {
                    int bulletIndex = findGuideBulletIndex(rayIndex, currentTime, showFullTrajectories);

                    if (bulletIndex < 0)
                    {
                        trajectoryGuides[rayIndex].Alpha = 0;
                        trajectoryGuideBulletIndices[rayIndex] = -1;
                        trajectoryGuideStartProgress[rayIndex] = float.NaN;
                        continue;
                    }

                    ref BulletRuntimeState state = ref bulletStates[bulletIndex];
                    float startProgress = trajectoryStartProgress(state.Geometry, currentTime, showFullTrajectories);

                    if (trajectoryGuideBulletIndices[rayIndex] != bulletIndex
                        || trajectoryGuideStartProgress[rayIndex] != startProgress)
                    {
                        updateTrajectoryGuideGeometry(rayIndex, bulletIndex, showFullTrajectories, startProgress);
                        trajectoryGuideBulletIndices[rayIndex] = bulletIndex;
                        trajectoryGuideStartProgress[rayIndex] = startProgress;
                    }

                    trajectoryGuides[rayIndex].Position = playfield.CameraOffsetAt(currentTime) - state.Geometry.CameraAnchor;

                    double timeFromEmission = currentTime - state.Geometry.EmissionTime;
                    float appearanceProgress = timeFromEmission < 0
                        ? (float)Math.Clamp(1 + timeFromEmission / Math.Max(1, cachedEmitterState.TimePreempt), 0, 1)
                        : 1;
                    bool withinPreempt = currentTime >= visualStartTime(state.Geometry);
                    bool visible = showFullTrajectories
                        ? withinPreempt && currentTime <= effectiveExitTime(state.Geometry)
                        : withinPreempt && currentTime < state.Geometry.EmissionTime;
                    bool bulletCleared = !playfield.ProjectileExists(state.Geometry.EmissionTime, currentTime);
                    trajectoryGuides[rayIndex].Alpha = !state.Collided && !bulletCleared && visible
                        ? Math.Max(0.08f, appearanceProgress * 0.24f) * cachedEmitterState.Opacity
                        : 0;
                }
            }

            previousSimulationTime = currentTime;
        }

        public void ProcessCollisions(
            double currentTime,
            Vector2 previousPlayerPosition,
            Vector2 currentPlayerPosition,
            bool allowSweptCollision,
            bool isRewind)
        {
            if (!emitterCacheValid)
                updateEmitterCache();

            if (isRewind)
            {
                rewindState(currentTime);
                rebuildActiveSet(currentTime);
            }

            bool collided = false;
            LastFrameTrajectoryIntersectionTestCount = 0;
            double frameStartTime = allowSweptCollision
                ? currentTime - Math.Max(0, Clock.ElapsedFrameTime)
                : currentTime;

            bool emitterOutsideCollisionRange = cachedEmitterState.MovementType == DodgeMovementType.Linear
                                                && activeCollisionBoundsValid
                                                && activeCollisionBoundsTime == currentTime
                                                && !collisionBoundsOverlapPlayer(previousPlayerPosition, currentPlayerPosition);

            float maxGrazeDistance = (cachedEmitterState.BulletSize + playfield.PlayerSize) / 2 + Math.Max(0, playfield.GrazeDistance);
            Vector2 playerBoxMin = Vector2.ComponentMin(previousPlayerPosition, currentPlayerPosition) - new Vector2(maxGrazeDistance);
            Vector2 playerBoxMax = Vector2.ComponentMax(previousPlayerPosition, currentPlayerPosition) + new Vector2(maxGrazeDistance);

            for (int activeIndex = 0; !emitterOutsideCollisionRange && activeIndex < activeBulletIndices.Count; activeIndex++)
            {
                int index = activeBulletIndices[activeIndex];
                ref BulletRuntimeState state = ref bulletStates[index];
                CachedBullet bullet = state.Geometry;
                bool bulletCleared = !playfield.ProjectileExists(bullet.EmissionTime, currentTime);
                double exitTime = effectiveExitTime(bullet);

                if (currentTime < bullet.EmissionTime || frameStartTime > exitTime)
                    continue;

                if (!state.Collided && playfield.CollisionEnabled && state.PositionSampleValid && !bulletCleared)
                {
                    float bulletMinX = Math.Min(state.PreviousPosition.X, state.CurrentPosition.X);
                    float bulletMaxX = Math.Max(state.PreviousPosition.X, state.CurrentPosition.X);
                    if (bulletMaxX < playerBoxMin.X || bulletMinX > playerBoxMax.X)
                        continue;

                    float bulletMinY = Math.Min(state.PreviousPosition.Y, state.CurrentPosition.Y);
                    float bulletMaxY = Math.Max(state.PreviousPosition.Y, state.CurrentPosition.Y);
                    if (bulletMaxY < playerBoxMin.Y || bulletMinY > playerBoxMax.Y)
                        continue;
                }

                double sampleEndTime = Math.Min(currentTime, exitTime);
                double sampleStartTime = allowSweptCollision
                    ? Math.Max(bullet.EmissionTime, frameStartTime)
                    : sampleEndTime;
                bool withinGrazeRange = false;
                bool intersects = false;

                if (!state.Collided && playfield.CollisionEnabled && !bulletCleared)
                {
                    Vector2 currentPosition = allowSweptCollision
                        ? default
                        : bullet.PositionAt(sampleEndTime, exitTime, cachedEmitterState, playfield.CameraOffsetAt(sampleEndTime));
                    LastFrameTrajectoryIntersectionTestCount++;
                    withinGrazeRange = allowSweptCollision
                        ? intersectsPlayerAlongTrajectory(
                            bullet,
                            sampleStartTime,
                            sampleEndTime,
                            previousPlayerPosition,
                            currentPlayerPosition,
                            playfield.GrazeDistance)
                        : DodgeBullet.IsWithinGrazeDistance(
                            currentPosition,
                            currentPlayerPosition,
                            playfield.GrazeDistance,
                            cachedEmitterState.BulletSize,
                            playfield.PlayerSize);

                    if (withinGrazeRange)
                    {
                        if (playfield.GrazeDistance <= 0)
                        {
                            intersects = true;
                        }
                        else
                        {
                            LastFrameTrajectoryIntersectionTestCount++;
                            intersects = allowSweptCollision
                                ? intersectsPlayerAlongTrajectory(
                                    bullet,
                                    sampleStartTime,
                                    sampleEndTime,
                                    previousPlayerPosition,
                                    currentPlayerPosition,
                                    0)
                                : DodgeBullet.IntersectsPlayer(
                                    currentPosition,
                                    currentPlayerPosition,
                                    cachedEmitterState.BulletSize,
                                    playfield.PlayerSize);
                        }
                    }
                }

                if (intersects)
                {
                    state.Collided = true;
                    state.CollisionTime = currentTime;

                    if (!state.GrazeJudged)
                        judgeGraze(ref state, currentTime, false);

                    int burstIndex = index / cachedEmitterState.BulletCount;
                    judgeBurst(burstIndex, false);
                    collided = true;
                }
                else if (withinGrazeRange)
                {
                    playfield.ReportGrazeProximity();

                    if (!state.GrazeJudged)
                        judgeGraze(ref state, currentTime, true);
                }
            }

            if (collided)
                playfield.TriggerMissFeedback();

            finishExitedBullets(currentTime);
            judgeCompletedBursts(currentTime);

            if (completionPending && nextActivationIndex >= bulletStates.Length && activeBulletIndices.Count == 0)
            {
                completionPending = false;
                completeRemainingSuccessfully();
            }
        }

        private void ensureBulletCount()
        {
            int required = Emitter.EffectiveBulletCount * Emitter.EffectiveBurstCount;

            if (activeBulletIndices.Capacity < required)
                activeBulletIndices.Capacity = required;

            if (activeBulletVisuals.Length > required)
            {
                for (int i = required; i < activeBulletVisuals.Length; i++)
                    releaseBulletVisual(i);
            }

            if (bulletStates.Length != required)
            {
                for (int i = required; i < bulletStates.Length; i++)
                    playfield?.RevertGraze(bulletStates[i].GrazeResult);

                Array.Resize(ref bulletStates, required);
                Array.Resize(ref activeBulletVisuals, required);
                Array.Resize(ref activeArrows, required);
                activeSetDirty = true;
                emitterCacheValid = false;
            }
        }

        private void syncTrajectoryGuides(bool enabled)
        {
            // Burst geometry differs only by its source position. Reuse one
            // guide per fan ray instead of allocating BurstCount * BulletCount
            // heavyweight guide drawables for every emitter.
            int required = enabled ? Emitter.EffectiveBulletCount : 0;

            while (trajectoryGuides.Count < required)
            {
                var trajectoryGuide = new DodgeTrajectoryGuide
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Depth = 1,
                };

                trajectoryGuides.Add(trajectoryGuide);
                AddInternal(trajectoryGuide);
                emitterCacheValid = false;
            }

            while (trajectoryGuides.Count > required)
            {
                DodgeTrajectoryGuide trajectoryGuide = trajectoryGuides[^1];
                trajectoryGuides.RemoveAt(trajectoryGuides.Count - 1);
                RemoveInternal(trajectoryGuide, true);
            }

            if (trajectoryGuideBulletIndices.Length != required)
            {
                Array.Resize(ref trajectoryGuideBulletIndices, required);
                Array.Fill(trajectoryGuideBulletIndices, -1);
            }
            else
            {
                LastFrameTrajectoryGuideCandidateChecks = 0;
            }

            if (trajectoryGuideStartProgress.Length != required)
            {
                Array.Resize(ref trajectoryGuideStartProgress, required);
                Array.Fill(trajectoryGuideStartProgress, float.NaN);
            }
        }

        private void updateEmitterCache()
        {
            // ApplyDefaults can synchronously invalidate drawable state from the
            // editor before the next Update() gets a chance to resize these
            // arrays. Always reconcile counts before rebuilding geometry.
            ensureBulletCount();

            bool showFullTrajectories = ShowFullTrajectories
                                        || playfield.ShowFullProjectilePaths
                                        || Emitter.TrajectoryGuideStyle == DodgeTrajectoryGuideStyle.FullPath;
            var state = new EmitterState(
                Emitter.StartTime,
                Emitter.Duration,
                Emitter.TimePreempt,
                Emitter.EffectiveBulletCount,
                Emitter.EffectiveSpreadAngle,
                Emitter.EffectiveBurstCount,
                Emitter.EffectiveBurstInterval,
                Emitter.BurstRotation,
                Emitter.Position,
                Emitter.AimPosition,
                Emitter.MovementEndPosition,
                Emitter.MoveSource,
                Emitter.ContinueUntilExit,
                Emitter.BulletSize,
                Emitter.Shape,
                Emitter.Colour,
                Emitter.OutlineColour,
                Math.Clamp(Emitter.OutlineThickness, 0, 8),
                Math.Clamp(Emitter.Opacity, 0, 1),
                Emitter.MovementType,
                Emitter.MovementEasing,
                Emitter.WaveAmplitude,
                Math.Max(1, Emitter.WaveCycles),
                Emitter.WavePhase,
                Emitter.TrajectoryGuideStyle,
                playfield.ContinuedBulletEndTime);

            if (emitterCacheValid && state == cachedEmitterState && cameraAnchorVersion == playfield.CameraStateVersion)
            {
                if (trajectoryGuides.Count > 0 && trajectoryGuidesShowFullPaths != showFullTrajectories)
                    applyTrajectoryGuideProperties(showFullTrajectories);

                return;
            }

            cachedEmitterState = state;
            syncBurstDrawables();
            rebuildBulletCache();
            cameraAnchorVersion = playfield.CameraStateVersion;
            applyCachedVisualProperties();
            applyTrajectoryGuideProperties(showFullTrajectories);
            emitterCacheValid = true;
            activeSetDirty = true;
            GeometryCacheRebuildCount++;
        }

        private void rebuildBulletCache()
        {
            // All trigonometry and playfield-exit intersection work is invariant
            // while the editor-visible emitter state remains unchanged.
            cachedMovementEndTime = double.NegativeInfinity;
            int index = 0;

            for (int burstIndex = 0; burstIndex < cachedEmitterState.BurstCount; burstIndex++)
            {
                double emissionTime = Emitter.EmissionTimeAt(burstIndex);
                Vector2 sourcePosition = Emitter.SourcePositionAt(burstIndex);
                // The scroll-drift anchor only depends on the emission time, which
                // is shared by every bullet in this burst; evaluate it once instead
                // of once per ray.
                Vector2 emissionCameraOffset = playfield.CameraOffsetAt(emissionTime);

                for (int rayIndex = 0; rayIndex < cachedEmitterState.BulletCount; rayIndex++)
                {
                    Vector2 endPosition = Emitter.EndPositionAt(burstIndex, rayIndex);
                    Vector2 movementDelta = endPosition - sourcePosition;

                    double exitTime = playfield.HasCameraChanges
                        ? DodgeTrajectory.CalculateExitTimeWithCamera(
                            emissionTime,
                            cachedEmitterState.Duration,
                            sourcePosition,
                            endPosition,
                            cachedEmitterState.BulletSize,
                            cachedEmitterState.MovementType,
                            cachedEmitterState.WaveAmplitude,
                            cachedEmitterState.WaveCycles,
                            cachedEmitterState.WavePhase,
                            playfield.CameraOffsetAt,
                            playfield.ContinuedBulletEndTime,
                            cachedEmitterState.MovementEasing)
                        : DodgeTrajectory.CalculateExitTime(
                            emissionTime,
                            cachedEmitterState.Duration,
                            sourcePosition,
                            endPosition,
                            cachedEmitterState.BulletSize,
                            cachedEmitterState.MovementType,
                            cachedEmitterState.WaveAmplitude,
                            cachedEmitterState.WaveCycles,
                            cachedEmitterState.WavePhase,
                            cachedEmitterState.MovementEasing);

                    double finalExitTime = cachedEmitterState.ContinueUntilExit
                        ? exitTime
                        : emissionTime + cachedEmitterState.Duration;

                    float maximumProgress = cachedEmitterState.Duration > 0
                        ? (float)((finalExitTime - emissionTime) / cachedEmitterState.Duration)
                        : 0;
                    bulletStates[index++].Geometry = new CachedBullet(
                        emissionTime,
                        finalExitTime,
                        sourcePosition,
                        endPosition,
                        movementDelta,
                        maximumProgress,
                        // Scroll drift anchor: the bullet rides the field scroll
                        // accumulated since its own emission.
                        emissionCameraOffset);
                    cachedMovementEndTime = Math.Max(cachedMovementEndTime, finalExitTime);
                }
            }
        }

        private void applyCachedVisualProperties()
        {
            var bulletSize = new Vector2(cachedEmitterState.BulletSize);

            for (int i = 0; i < bulletStates.Length; i++)
            {
                CachedBullet cachedBullet = bulletStates[i].Geometry;
                DodgeBulletVisual? bullet = activeBulletVisuals[i];

                if (bullet != null)
                    applyVisualProperties(bullet, cachedBullet, bulletSize);
            }
        }

        private void applyTrajectoryGuideProperties(bool showFullTrajectories)
        {
            for (int i = 0; i < trajectoryGuides.Count; i++)
                trajectoryGuides[i].Colour = cachedEmitterState.FillColour;

            Array.Fill(trajectoryGuideBulletIndices, -1);
            Array.Fill(trajectoryGuideStartProgress, float.NaN);
            trajectoryGuidesShowFullPaths = showFullTrajectories;
        }

        private int findGuideBulletIndex(int rayIndex, double currentTime, bool showFullTrajectories)
        {
            double burstInterval = cachedEmitterState.BurstInterval;
            int latestEmittedBurst = currentTime < cachedEmitterState.StartTime
                ? -1
                : Math.Min(
                    cachedEmitterState.BurstCount - 1,
                    (int)Math.Floor((currentTime - cachedEmitterState.StartTime) / burstInterval));

            if (showFullTrajectories)
            {
                for (int burstIndex = latestEmittedBurst; burstIndex >= 0; burstIndex--)
                {
                    int bulletIndex = burstIndex * cachedEmitterState.BulletCount + rayIndex;
                    LastFrameTrajectoryGuideCandidateChecks++;
                    double exit = effectiveExitTime(bulletStates[bulletIndex].Geometry);

                    if (exit > bulletStates[bulletIndex].Geometry.EmissionTime && currentTime <= exit)
                        return bulletIndex;
                }
            }

            int upcomingBurst = latestEmittedBurst + 1;

            if (upcomingBurst < 0 || upcomingBurst >= cachedEmitterState.BurstCount)
                return -1;

            int upcomingBulletIndex = upcomingBurst * cachedEmitterState.BulletCount + rayIndex;
            CachedBullet upcoming = bulletStates[upcomingBulletIndex].Geometry;
            LastFrameTrajectoryGuideCandidateChecks++;
            return effectiveExitTime(upcoming) > upcoming.EmissionTime
                   && currentTime >= visualStartTime(upcoming)
                   && currentTime < upcoming.EmissionTime
                ? upcomingBulletIndex
                : -1;
        }

        private void updateTrajectoryGuideGeometry(int guideIndex, int bulletIndex, bool showFullTrajectories, float startProgress)
        {
            CachedBullet cachedBullet = bulletStates[bulletIndex].Geometry;
            trajectoryGuides[guideIndex].SetGeometry(
                cachedBullet.SourcePosition,
                cachedBullet.EndPosition,
                startProgress,
                showFullTrajectories ? cachedBullet.MaximumProgress : 1,
                cachedEmitterState.MovementType,
                cachedEmitterState.WaveAmplitude,
                cachedEmitterState.WaveCycles,
                cachedEmitterState.WavePhase,
                cachedEmitterState.MovementEasing);
        }

        private float trajectoryStartProgress(CachedBullet bullet, double currentTime, bool showFullTrajectories)
        {
            if (!showFullTrajectories || currentTime <= bullet.EmissionTime)
                return 0;

            if (cachedEmitterState.Duration <= 0)
                return bullet.MaximumProgress;

            return (float)Math.Clamp(
                (currentTime - bullet.EmissionTime) / cachedEmitterState.Duration,
                0,
                bullet.MaximumProgress);
        }

        private void updateActiveSet(double currentTime)
        {
            if (activeSetDirty || currentTime < previousSimulationTime)
            {
                if (currentTime < previousSimulationTime)
                    rewindState(currentTime);

                rebuildActiveSet(currentTime);
                return;
            }

            while (nextActivationIndex < bulletStates.Length
                   && visualStartTime(bulletStates[nextActivationIndex].Geometry) <= currentTime)
            {
                int index = nextActivationIndex++;
                double exit = effectiveExitTime(bulletStates[index].Geometry);

                if (exit > bulletStates[index].Geometry.EmissionTime)
                {
                    activeBulletIndices.Add(index);
                    earliestActiveExitTime = Math.Min(earliestActiveExitTime, exit);
                }
                else
                {
                    judgeGraze(ref bulletStates[index], exit, false);
                }
            }
        }

        private void rebuildActiveSet(double currentTime)
        {
            for (int i = 0; i < activeBulletVisuals.Length; i++)
                releaseBulletVisual(i);

            activeBulletIndices.Clear();
            nextActivationIndex = 0;
            earliestActiveExitTime = double.PositiveInfinity;

            while (nextActivationIndex < bulletStates.Length
                   && visualStartTime(bulletStates[nextActivationIndex].Geometry) <= currentTime)
            {
                ref BulletRuntimeState state = ref bulletStates[nextActivationIndex];
                double exit = effectiveExitTime(state.Geometry);

                if (exit > state.Geometry.EmissionTime && exit >= currentTime)
                {
                    activeBulletIndices.Add(nextActivationIndex);
                    earliestActiveExitTime = Math.Min(earliestActiveExitTime, exit);
                }
                else if (!state.GrazeJudged)
                {
                    judgeGraze(ref state, exit, false);
                }

                nextActivationIndex++;
            }

            activeSetDirty = false;
            previousSimulationTime = currentTime;
        }

        private void finishExitedBullets(double currentTime)
        {
            if (currentTime <= earliestActiveExitTime)
                return;

            for (int i = activeBulletIndices.Count - 1; i >= 0; i--)
            {
                int index = activeBulletIndices[i];
                ref BulletRuntimeState state = ref bulletStates[index];

                if (currentTime <= effectiveExitTime(state.Geometry))
                    continue;

                if (!state.GrazeJudged)
                    judgeGraze(ref state, effectiveExitTime(state.Geometry), false);

                releaseBulletVisual(index);
                int lastIndex = activeBulletIndices.Count - 1;
                activeBulletIndices[i] = activeBulletIndices[lastIndex];
                activeBulletIndices.RemoveAt(lastIndex);
            }

            earliestActiveExitTime = double.PositiveInfinity;

            for (int i = 0; i < activeBulletIndices.Count; i++)
                earliestActiveExitTime = Math.Min(earliestActiveExitTime, effectiveExitTime(bulletStates[activeBulletIndices[i]].Geometry));
        }

        private void updatePositionSample(ref BulletRuntimeState state, double time, Vector2 position)
        {
            if (!state.PositionSampleValid)
            {
                state.PreviousPosition = state.CurrentPosition = position;
                state.PreviousPositionTime = state.PositionTime = time;
                state.PositionSampleValid = true;
                return;
            }

            if (state.PositionTime == time)
            {
                state.CurrentPosition = position;
                return;
            }

            if (state.PositionTime < time)
            {
                state.PreviousPosition = state.CurrentPosition;
                state.PreviousPositionTime = state.PositionTime;
            }
            else
            {
                state.PreviousPosition = position;
                state.PreviousPositionTime = time;
            }

            state.CurrentPosition = position;
            state.PositionTime = time;
        }

        private void includeInCollisionBounds(Vector2 position)
        {
            activeCollisionBoundsMin = Vector2.ComponentMin(activeCollisionBoundsMin, position);
            activeCollisionBoundsMax = Vector2.ComponentMax(activeCollisionBoundsMax, position);
            activeCollisionBoundsValid = true;
        }

        private bool collisionBoundsOverlapPlayer(Vector2 previousPlayerPosition, Vector2 currentPlayerPosition)
        {
            float radius = (cachedEmitterState.BulletSize + playfield.PlayerSize) / 2 + Math.Max(0, playfield.GrazeDistance);
            Vector2 playerMin = Vector2.ComponentMin(previousPlayerPosition, currentPlayerPosition) - new Vector2(radius);
            Vector2 playerMax = Vector2.ComponentMax(previousPlayerPosition, currentPlayerPosition) + new Vector2(radius);
            return activeCollisionBoundsMax.X >= playerMin.X
                   && activeCollisionBoundsMin.X <= playerMax.X
                   && activeCollisionBoundsMax.Y >= playerMin.Y
                   && activeCollisionBoundsMin.Y <= playerMax.Y;
        }

        private bool allActiveBulletsCollidedOrExited()
        {
            for (int i = 0; i < activeBulletIndices.Count; i++)
            {
                if (!bulletStates[activeBulletIndices[i]].Collided)
                    return false;
            }

            return true;
        }

        private void syncBurstDrawables()
        {
            burstDrawables = new DrawableDodgeEmitterBurst?[Emitter.EffectiveBurstCount];

            foreach (DrawableDodgeEmitterBurst drawable in NestedHitObjects.OfType<DrawableDodgeEmitterBurst>())
            {
                if (drawable.HitObject.BurstIndex >= 0 && drawable.HitObject.BurstIndex < burstDrawables.Length - 1)
                    burstDrawables[drawable.HitObject.BurstIndex] = drawable;
            }
        }

        private bool isBurstJudged(int burstIndex)
        {
            if (burstIndex == cachedEmitterState.BurstCount - 1)
                return Result.HasResult;

            // Hand-authored test beatmaps may not have gone through ApplyDefaults(),
            // and therefore have no generated nested objects. There is no matching
            // score slot to wait for in that case.
            return burstIndex >= burstDrawables.Length
                   || burstDrawables[burstIndex] == null
                   || burstDrawables[burstIndex]!.Result.HasResult;
        }

        private void judgeBurst(int burstIndex, bool successful)
        {
            if (isBurstJudged(burstIndex))
                return;

            if (burstIndex == cachedEmitterState.BurstCount - 1)
            {
                if (successful)
                    ApplyMaxResult();
                else
                    ApplyMinResult();

                return;
            }

            if (burstIndex < burstDrawables.Length)
                burstDrawables[burstIndex]?.Judge(successful);
        }

        private void judgeCompletedBursts(double currentTime)
        {
            for (int burstIndex = 0; burstIndex < cachedEmitterState.BurstCount; burstIndex++)
            {
                if (isBurstJudged(burstIndex))
                    continue;

                int firstBulletIndex = burstIndex * cachedEmitterState.BulletCount;
                bool complete = true;

                for (int rayIndex = 0; rayIndex < cachedEmitterState.BulletCount; rayIndex++)
                {
                    ref BulletRuntimeState state = ref bulletStates[firstBulletIndex + rayIndex];

                    if (!state.Collided && currentTime <= effectiveExitTime(state.Geometry))
                    {
                        complete = false;
                        break;
                    }
                }

                if (complete)
                    judgeBurst(burstIndex, true);
            }
        }

        private bool allBurstsJudged()
        {
            for (int burstIndex = 0; burstIndex < cachedEmitterState.BurstCount; burstIndex++)
            {
                if (!isBurstJudged(burstIndex))
                    return false;
            }

            return true;
        }

        private void rewindState(double time)
        {
            // See DrawableDodgeHitObject: rewinding before the recorded result
            // time may still leave us after the emitter's real movement end.
            completionPending = time >= getEffectiveMovementEndTime();

            for (int i = 0; i < bulletStates.Length; i++)
            {
                ref BulletRuntimeState state = ref bulletStates[i];

                if (state.GrazeJudged && state.GrazeTime > time)
                {
                    playfield.RevertGraze(state.GrazeResult);
                    state.GrazeJudged = false;
                    state.GrazeTime = double.NegativeInfinity;
                    state.GrazeResult = null;
                }

                if (state.Collided && state.CollisionTime > time)
                {
                    state.Collided = false;
                    state.CollisionTime = double.NegativeInfinity;
                }
            }
        }

        private void judgeGraze(ref BulletRuntimeState state, double time, bool successful)
        {
            state.GrazeJudged = true;
            state.GrazeTime = time;
            state.GrazeResult = playfield.RegisterGraze(time, successful);
        }

        private bool intersectsPlayerAlongTrajectory(
            CachedBullet bullet,
            double startTime,
            double endTime,
            Vector2 playerStart,
            Vector2 playerEnd,
            float grazeDistance)
        {
            int subdivisions = DodgeTrajectory.SuggestedSubdivisionCount(
                startTime,
                endTime,
                bullet.EmissionTime,
                cachedEmitterState.Duration,
                cachedEmitterState.MovementType,
                cachedEmitterState.WaveCycles,
                cachedEmitterState.MovementEasing);
            double exitTime = effectiveExitTime(bullet);

            for (int i = 0; i < subdivisions; i++)
            {
                double firstTime = startTime + (endTime - startTime) * i / subdivisions;
                double secondTime = startTime + (endTime - startTime) * (i + 1) / subdivisions;
                Vector2 firstPlayer = Vector2.Lerp(playerStart, playerEnd, (float)i / subdivisions);
                Vector2 secondPlayer = Vector2.Lerp(playerStart, playerEnd, (float)(i + 1) / subdivisions);
                Vector2 firstBullet = bullet.PositionAt(firstTime, exitTime, cachedEmitterState, playfield.CameraOffsetAt(firstTime));
                Vector2 secondBullet = bullet.PositionAt(secondTime, exitTime, cachedEmitterState, playfield.CameraOffsetAt(secondTime));

                bool intersects = grazeDistance > 0
                    ? DodgeBullet.IsWithinGrazeDistanceSwept(
                        firstBullet,
                        secondBullet,
                        firstPlayer,
                        secondPlayer,
                        grazeDistance,
                        cachedEmitterState.BulletSize,
                        playfield.PlayerSize)
                    : DodgeBullet.IntersectsPlayerSwept(
                        firstBullet,
                        secondBullet,
                        firstPlayer,
                        secondPlayer,
                        cachedEmitterState.BulletSize,
                        playfield.PlayerSize);

                if (intersects)
                    return true;
            }

            return false;
        }

        private double visualStartTime(CachedBullet bullet)
            => bullet.EmissionTime - cachedEmitterState.TimePreempt;

        private double effectiveExitTime(CachedBullet bullet)
            => bullet.ExitTime;

        private DodgeBulletVisual acquireBulletVisual(int index)
        {
            DodgeBulletVisual? existing = activeBulletVisuals[index];

            if (existing != null)
                return existing;

            DodgeBulletVisual bullet;

            if (availableBulletVisuals.Count > 0)
                bullet = availableBulletVisuals.Pop();
            else
            {
                bullet = new DodgeBulletVisual
                {
                    Origin = Anchor.Centre,
                    Colour = Color4.White,
                    Alpha = 0,
                };
                bulletVisualPool.Add(bullet);
                bulletContainer.Add(bullet);
            }

            activeBulletVisuals[index] = bullet;
            applyVisualProperties(bullet, bulletStates[index].Geometry, new Vector2(cachedEmitterState.BulletSize));
            return bullet;
        }

        private void releaseBulletVisual(int index)
        {
            if (index < 0 || index >= activeBulletVisuals.Length)
                return;

            DodgeBulletVisual? bullet = activeBulletVisuals[index];

            if (bullet == null)
                return;

            bullet.Alpha = 0;
            activeBulletVisuals[index] = null;
            availableBulletVisuals.Push(bullet);
            releaseArrow(index);
        }

        private DodgeDirectionIndicator acquireArrow(int index)
        {
            DodgeDirectionIndicator? existing = activeArrows[index];

            if (existing != null)
                return existing;

            DodgeDirectionIndicator arrow;

            if (availableArrows.Count > 0)
                arrow = availableArrows.Pop();
            else
            {
                arrow = new DodgeDirectionIndicator();
                arrowPool.Add(arrow);
                bulletContainer.Add(arrow);
            }

            activeArrows[index] = arrow;
            return arrow;
        }

        private void releaseArrow(int index)
        {
            if (index < 0 || index >= activeArrows.Length)
                return;

            DodgeDirectionIndicator? arrow = activeArrows[index];

            if (arrow == null)
                return;

            arrow.Alpha = 0;
            activeArrows[index] = null;
            availableArrows.Push(arrow);
        }

        private void applyVisualProperties(DodgeBulletVisual bullet, CachedBullet cachedBullet, Vector2 bulletSize)
        {
            bullet.Size = bulletSize;
            bullet.Shape = cachedEmitterState.Shape;
            bullet.Direction = cachedBullet.TangentAt(cachedBullet.EmissionTime, cachedBullet.ExitTime, cachedEmitterState);
            bullet.FillColour = cachedEmitterState.FillColour;
            bullet.OutlineColour = cachedEmitterState.OutlineColour;
            bullet.OutlineThickness = cachedEmitterState.OutlineThickness;
            bullet.LifetimeStart = visualStartTime(cachedBullet);
            bullet.LifetimeEnd = Math.BitIncrement(effectiveExitTime(cachedBullet));
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!emitterCacheValid)
                updateEmitterCache();

            if (!DodgeGameplayTiming.HasReachedJudgementTime(
                    Time.Current,
                    getEffectiveMovementEndTime(),
                    playfield.GameplayEndTime))
            {
                return;
            }

            if (nextActivationIndex < bulletStates.Length || activeBulletIndices.Count > 0)
            {
                if (Time.Current < getEffectiveMovementEndTime())
                    return;
            }

            completionPending = false;
            completeRemainingSuccessfully();
        }

        private void completeRemainingSuccessfully()
        {
            judgeRemainingGrazes(false);

            for (int burstIndex = 0; burstIndex < cachedEmitterState.BurstCount; burstIndex++)
                judgeBurst(burstIndex, true);
        }

        private void judgeRemainingGrazes(bool successful)
        {
            for (int i = 0; i < bulletStates.Length; i++)
            {
                ref BulletRuntimeState state = ref bulletStates[i];

                if (state.GrazeJudged)
                    continue;

                judgeGraze(ref state, Time.Current, successful);
            }
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            updateEmitterCache();

            switch (state)
            {
                case ArmedState.Hit:
                    this.Delay(Math.Max(0, getEffectiveMovementEndTime() - Time.Current)).FadeOut(120).Expire();
                    break;

                case ArmedState.Miss:
                    this.Delay(Math.Max(0, getEffectiveMovementEndTime() - Time.Current)).FadeOut(160).Expire();
                    break;
            }
        }

        private double getEffectiveMovementEndTime()
            => cachedMovementEndTime;

        private Vector2 cameraAdjustedEndForExit(Vector2 authoredEnd, double emissionTime, double duration)
        {
            if (duration <= 0)
                return authoredEnd;

            return authoredEnd
                   + playfield.CameraOffsetAt(emissionTime + duration)
                   - playfield.CameraOffsetAt(emissionTime);
        }

        private readonly record struct EmitterState(
            double StartTime,
            double Duration,
            double TimePreempt,
            int BulletCount,
            float SpreadAngle,
            int BurstCount,
            double BurstInterval,
            float BurstRotation,
            Vector2 Position,
            Vector2 AimPosition,
            Vector2 MovementEndPosition,
            bool MoveSource,
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
            DodgeTrajectoryGuideStyle TrajectoryGuideStyle,
            double ContinuedBulletEndTime);

        private readonly record struct CachedBullet(
            double EmissionTime,
            double ExitTime,
            Vector2 SourcePosition,
            Vector2 EndPosition,
            Vector2 MovementDelta,
            float MaximumProgress,
            Vector2 CameraAnchor)
        {
            public Vector2 PositionAt(double time, double effectiveExitTime, EmitterState emitter, Vector2 cameraOffset)
            {
                Vector2 trajectoryPosition;

                if (emitter.Duration <= 0)
                {
                    trajectoryPosition = EndPosition;
                }
                else
                {
                    float progress = (float)Math.Clamp(
                        (Math.Min(time, effectiveExitTime) - EmissionTime) / emitter.Duration,
                        0,
                        MaximumProgress);
                    trajectoryPosition = DodgeTrajectory.PositionAtProgress(
                        SourcePosition,
                        EndPosition,
                        progress,
                        emitter.MovementType,
                        emitter.WaveAmplitude,
                        emitter.WaveCycles,
                        emitter.WavePhase,
                        emitter.MovementEasing);
                }

                return trajectoryPosition + cameraOffset - CameraAnchor;
            }

            public Vector2 TangentAt(double time, double effectiveExitTime, EmitterState emitter)
            {
                float progress = emitter.Duration <= 0
                    ? 1
                    : (float)Math.Clamp(
                        (Math.Min(time, effectiveExitTime) - EmissionTime) / emitter.Duration,
                        0,
                        MaximumProgress);
                return DodgeTrajectory.TangentAtProgress(
                    SourcePosition,
                    EndPosition,
                    progress,
                    emitter.MovementType,
                    emitter.WaveAmplitude,
                    emitter.WaveCycles,
                    emitter.WavePhase,
                    emitter.MovementEasing);
            }
        }

        private struct BulletRuntimeState
        {
            public CachedBullet Geometry;
            public Vector2 PreviousPosition;
            public Vector2 CurrentPosition;
            public double PreviousPositionTime;
            public double PositionTime;
            public bool PositionSampleValid;
            public bool GrazeJudged;
            public double GrazeTime;
            public JudgementResult? GrazeResult;
            public bool Collided;
            public double CollisionTime;
        }

        private partial class BulletLifetimeContainer : LifetimeManagementContainer
        {
            public void Add(Drawable bullet)
            {
                AddInternal(bullet);
            }

            public void Remove(Drawable bullet, bool dispose)
                => RemoveInternal(bullet, dispose);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                playfield?.UnregisterCollisionSource(this);

            base.Dispose(isDisposing);
        }
    }
}
