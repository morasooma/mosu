// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Dodge.Configuration;
using osu.Game.Rulesets.Dodge.Skinning;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Objects.Drawables;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.UI
{
    [Cached]
    public partial class DodgePlayfield : Playfield
    {
        public const float WIDTH = 512;
        public const float HEIGHT = 384;
        public const double CONTINUED_BULLET_GRACE_PERIOD = 2000;
        private const double maximum_swept_collision_frame_time = 250;

        public static readonly Vector2 BASE_SIZE = new Vector2(WIDTH, HEIGHT);

        public DodgePlayer Player { get; private set; } = null!;

        public bool CollisionEnabled { get; }

        public bool ShowFullProjectilePaths { get; }

        private readonly bool showPlayer;
        private readonly List<DodgeHitObject> hitObjects;
                private readonly List<DodgeArenaChange> arenaChanges;
        private readonly List<DodgeCameraChange> cameraChanges;
        private readonly HashSet<DodgeHitObject> trackedHitObjects = new HashSet<DodgeHitObject>();
        private readonly List<IDodgeCollisionSource> collisionSources = new List<IDodgeCollisionSource>();
        private readonly List<IDodgeCollisionSource> scheduledCollisionSources = new List<IDodgeCollisionSource>();
        private readonly List<IDodgeCollisionSource> activeCollisionSources = new List<IDodgeCollisionSource>();
        private bool collisionScheduleDirty = true;
        private int nextCollisionSourceIndex;
        private DodgeArenaStateEvaluator arenaStateEvaluator;
        private DodgeCameraStateEvaluator cameraStateEvaluator;
        private bool cameraStateDirty;
        private bool arenaStateDirty;
        private bool gameplayEndTimeDirty;
        private Container arena = null!;
        private Container arenaOutline = null!;
        private Container kiaiWrapper = null!;
        private SkinnableDrawable arenaSkin = null!;
        private ArenaBackgroundBox arenaBackground = null!;
        private CircularContainer grazeIndicator = null!;
        private SkinnableSound missSound = null!;
        private bool grazeProximityReported;
        private bool grazeIndicatorVisible;
        private double lastGrazeEffectTime = double.NegativeInfinity;
        private double previousCollisionTime = double.NaN;
        private Vector2 previousPlayerPosition;
        private readonly Drawable? backgroundOverlay;
        private readonly float playerSpeed;
        private readonly float playerSize;
        private readonly float bulletSize;
        private readonly bool missSoundEnabled;
        private readonly double missSoundVolume;
        private readonly float playfieldDim;
        private readonly bool optimiseArenaFill;
        private Bindable<double> userDimLevel = null!;
        private Bindable<bool> lightenDuringBreaks = null!;

        public float ArenaBackgroundAlpha => arenaBackground.Alpha;

        public Colour4 ArenaBackgroundColour => arenaBackground.Colour;

        public Colour4 ArenaBorderColour => arena.BorderColour;

        /// <summary>Current kiai shake amplitude (degrees) as authored in the active arena keyframe.</summary>
        public float KiaiShakeAngle { get; private set; }

        public float GrazeIndicatorBrightness { get; }

        [Resolved(canBeNull: true)]
        private ScoreProcessor? scoreProcessor { get; set; }

        public float GrazeDistance { get; }

        public double GrazeScore { get; }

        public double GameplayEndTime { get; private set; }

        public double ContinuedBulletEndTime => GameplayEndTime + CONTINUED_BULLET_GRACE_PERIOD;

        internal int GameplayEndTimeRefreshCount { get; private set; }

        internal int ArenaStateRefreshCount { get; private set; }

        internal int RegisteredCollisionSourceCount => collisionSources.Count;

        internal int ActiveCollisionSourceCount => activeCollisionSources.Count;

        internal int LastFrameProcessedCollisionSourceCount { get; private set; }

        internal int LastRewindProcessedCollisionSourceCount { get; private set; }

        public int MissFeedbackCount { get; private set; }

        public int MissSoundPlayCount { get; private set; }

        public DodgePlayfield(
            IEnumerable<DodgeHitObject>? hitObjects = null,
            bool showPlayer = true,
            Drawable? backgroundOverlay = null,
            float playerSpeed = DodgePlayer.BASE_SPEED,
            float playerSize = DodgePlayer.SIZE,
            float grazeDistance = 0,
            double grazeScore = 0,
            bool showFullProjectilePaths = false,
            double playfieldDim = 1,
            double grazeIndicatorBrightness = DodgeRulesetConfigManager.DEFAULT_GRAZE_INDICATOR_BRIGHTNESS,
            bool missSoundEnabled = true,
            double missSoundVolume = 60,
            bool optimiseArenaFill = true)
        {
            this.showPlayer = showPlayer;
            this.backgroundOverlay = backgroundOverlay;
            this.hitObjects = hitObjects?.Distinct().ToList() ?? new List<DodgeHitObject>();
            this.playerSpeed = playerSpeed;
            this.playerSize = Math.Max(0, playerSize);
            GrazeDistance = Math.Max(0, grazeDistance);
            GrazeScore = Math.Max(0, grazeScore);
            ShowFullProjectilePaths = showFullProjectilePaths;
            this.playfieldDim = (float)Math.Clamp(playfieldDim, 0, 1);
            GrazeIndicatorBrightness = (float)Math.Clamp(grazeIndicatorBrightness, 0, 1);
            this.missSoundEnabled = missSoundEnabled;
            this.missSoundVolume = Math.Clamp(missSoundVolume, 0, 100);
            this.optimiseArenaFill = optimiseArenaFill;

            bulletSize = this.hitObjects.FirstOrDefault(hitObject => hitObject is DodgeBullet or DodgeEmitter) switch
            {
                DodgeBullet bullet => bullet.BulletSize,
                DodgeEmitter emitter => emitter.BulletSize,
                _ => DodgeBullet.SIZE,
            };

                        arenaChanges = this.hitObjects.OfType<DodgeArenaChange>().ToList();
            cameraChanges = this.hitObjects.OfType<DodgeCameraChange>().ToList();
            arenaStateEvaluator = null!;
            cameraStateEvaluator = null!;

            foreach (DodgeHitObject hitObject in this.hitObjects)
                trackHitObject(hitObject);

            refreshArenaStateEvaluator();
            refreshCameraStateEvaluator();
            refreshGameplayEndTime();
            CollisionEnabled = showPlayer;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            userDimLevel = config.GetBindable<double>(OsuSetting.DimLevel);
            lightenDuringBreaks = config.GetBindable<bool>(OsuSetting.LightenDuringBreaks);

            var children = new List<Drawable>
            {
                (kiaiWrapper = new Container
                {
                    Origin = Anchor.Centre,
                    Children = new Drawable[]
                    {
                        (arena = new Container
                        {
                            Origin = Anchor.Centre,
                            Size = BASE_SIZE,
                            Masking = true,
                            BorderThickness = 2,
                            BorderColour = Color4.White,
                            Children = new Drawable[]
                            {
                                arenaSkin = new SkinnableDrawable(
                                    new DodgeSkinComponentLookup(DodgeSkinComponents.Arena),
                                    _ => new DefaultArenaBackground
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.Black,
                                    },
                                    ConfineMode.ScaleToFit)
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                arenaBackground = new ArenaBackgroundBox
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black,
                                    Alpha = this.playfieldDim,
                                },
                                new SkinnableDrawable(
                                    new DodgeSkinComponentLookup(DodgeSkinComponents.ArenaBorder),
                                    _ => Drawable.Empty(),
                                    ConfineMode.ScaleToFit)
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                            },
                        }),
                        (arenaOutline = new Container
                        {
                            Origin = Anchor.Centre,
                            Size = BASE_SIZE,
                            Alpha = 0,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 2,
                                },
                                new Box
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 2,
                                },
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Y,
                                    Width = 2,
                                },
                                new Box
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    RelativeSizeAxes = Axes.Y,
                                    Width = 2,
                                },
                            },
                        }),
                    },
                }),
                new KiaiArenaShakeEffect(this, kiaiWrapper),
            };

            if (backgroundOverlay != null)
                children.Add(backgroundOverlay);

            // Scroll is applied per-object inside each drawable (anchored at the object's
            // own spawn time), not by moving a shared container. The arena frame, the
            // player and freshly-spawned objects therefore always stay where they were
            // placed, while live objects drift with the scroll.
            children.Add(HitObjectContainer);
            children.Add(grazeIndicator = new CircularContainer
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Size = new Vector2(CalculateGrazeIndicatorDiameter(GrazeDistance, bulletSize, playerSize)),
                Alpha = 0,
                Masking = true,
                BorderThickness = 1,
                BorderColour = new Colour4(0.72f, 0.48f, 1, 1),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    AlwaysPresent = true,
                },
            });
            children.Add(Player = new DodgePlayer
            {
                Alpha = showPlayer ? 1 : 0,
                MovementSpeed = playerSpeed,
                PlayerSize = playerSize,
            });
            children.Add(missSound = new SkinnableSound(DodgeMissSampleInfo.Default)
            {
                Volume = { Value = missSoundVolume / 100 },
            });

            InternalChildren = children;
        }

        protected override void Update()
        {
            grazeProximityReported = false;
            base.Update();

            RefreshCachedState();

            DodgeArenaState state = arenaStateEvaluator.Evaluate(Time.Current);
            Vector2 arenaCenter = state.Position + state.Size / 2;
            kiaiWrapper.Position = arenaCenter;
            arena.Position = Vector2.Zero;
            arena.Size = state.Size;
            arena.Rotation = state.Rotation;
            arena.BorderColour = state.BorderColour.Opacity(state.BorderOpacity);
            arenaOutline.Position = Vector2.Zero;
            arenaOutline.Size = state.Size;
            arenaOutline.Rotation = state.Rotation;
            arenaOutline.Colour = state.BorderColour.Opacity(state.BorderOpacity);
            KiaiShakeAngle = state.KiaiShakeAngle;
            arenaBackground.Colour = state.BackgroundColour;
            arenaBackground.Alpha = state.BackgroundOpacity * playfieldDim;

            // The default arena is an opaque black fallback. When skin performance mode has already
            // replaced the gameplay background with the renderer's black clear, drawing that fallback
            // writes the full arena to the backbuffer again without changing a pixel. Use the lowest
            // possible gameplay dim here so this remains safe even while break lightening is active.
            float minimumGameplayDim = Math.Max(
                (float)userDimLevel.Value - (lightenDuringBreaks.Value ? UserDimContainer.BREAK_LIGHTEN_AMOUNT : 0),
                0);

            bool defaultArenaSuppressed = false;

            if (arenaSkin.Drawable is DefaultArenaBackground defaultArenaBackground)
            {
                defaultArenaSuppressed = defaultArenaBackground.SuppressRendering = optimiseArenaFill
                                                                                     && scoreProcessor != null
                                                                                     && SkinPerformanceMode.ShouldBlackOutBackground(minimumGameplayDim);
            }

            arenaBackground.SuppressRendering = optimiseArenaFill
                                                && arenaSkin.Drawable is DefaultArenaBackground
                                                && state.BackgroundColour.R == 0
                                                && state.BackgroundColour.G == 0
                                                && state.BackgroundColour.B == 0;

            // Framework masking borders are produced while child pixels are rasterised. If both fills are
            // skipped there are no fragments on which that border can appear, so draw the same two-pixel
            // outline as four narrow quads instead of restoring a full-arena dummy fill.
            bool useLightweightOutline = defaultArenaSuppressed && arenaBackground.SuppressRendering;
            arena.BorderThickness = useLightweightOutline ? 0 : 2;
            arenaOutline.Alpha = useLightweightOutline ? 1 : 0;
            Player.ArenaPosition = state.Position;
            Player.ArenaSize = state.Size;
            Player.ArenaRotation = state.Rotation;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            double currentTime = Time.Current;
            bool hasPreviousFrame = !double.IsNaN(previousCollisionTime);
            double elapsed = hasPreviousFrame ? currentTime - previousCollisionTime : 0;
            bool isRewind = hasPreviousFrame && elapsed < 0;
            bool allowSweptCollision = hasPreviousFrame
                                       && elapsed > 0
                                       && elapsed <= maximum_swept_collision_frame_time;
            Vector2 collisionStartPlayerPosition = hasPreviousFrame
                ? previousPlayerPosition
                : Player.Position;

            if (isRewind)
            {
                // Rewind every registered source once so results and graze state
                // after the target time are reverted, including future objects.
                for (int i = 0; i < collisionSources.Count; i++)
                {
                    collisionSources[i].ProcessCollisions(
                        currentTime,
                        collisionStartPlayerPosition,
                        Player.Position,
                        false,
                        true);
                }

                LastFrameProcessedCollisionSourceCount = collisionSources.Count;
                LastRewindProcessedCollisionSourceCount = collisionSources.Count;
                rebuildCollisionSchedule(currentTime, false);
            }
            else
            {
                refreshCollisionSchedule(currentTime);
                LastFrameProcessedCollisionSourceCount = activeCollisionSources.Count;

                for (int i = 0; i < activeCollisionSources.Count; i++)
                {
                    activeCollisionSources[i].ProcessCollisions(
                        currentTime,
                        collisionStartPlayerPosition,
                        Player.Position,
                        allowSweptCollision,
                        false);
                }

                removeExpiredCollisionSources(currentTime);
            }

            previousCollisionTime = currentTime;
            previousPlayerPosition = Player.Position;
            grazeIndicator.Position = Player.Position;

            bool shouldShowGrazeIndicator = GrazeDistance > 0 && grazeProximityReported;

            if (shouldShowGrazeIndicator != grazeIndicatorVisible)
            {
                grazeIndicatorVisible = shouldShowGrazeIndicator;
                grazeIndicator.FadeTo(shouldShowGrazeIndicator ? GrazeIndicatorBrightness : 0, shouldShowGrazeIndicator ? 60 : 140);
            }
        }

        protected override void OnHitObjectAdded(HitObject hitObject)
        {
            base.OnHitObjectAdded(hitObject);

            if (hitObject is DodgeHitObject dodgeHitObject && !hitObjects.Contains(dodgeHitObject))
            {
                hitObjects.Add(dodgeHitObject);
                trackHitObject(dodgeHitObject);
                refreshGameplayEndTime();
            }

            if (hitObject is DodgeArenaChange arenaChange && !arenaChanges.Contains(arenaChange))
            {
                arenaChanges.Add(arenaChange);
                arenaStateDirty = true;
            }

            if (hitObject is DodgeCameraChange cameraChange && !cameraChanges.Contains(cameraChange))
            {
                cameraChanges.Add(cameraChange);
                cameraStateDirty = true;
            }
        }

        protected override void OnHitObjectRemoved(HitObject hitObject)
        {
            base.OnHitObjectRemoved(hitObject);

            if (hitObject is DodgeHitObject dodgeHitObject && hitObjects.Remove(dodgeHitObject))
            {
                untrackHitObject(dodgeHitObject);
                refreshGameplayEndTime();
            }

            if (hitObject is DodgeArenaChange arenaChange)
            {
                arenaChanges.Remove(arenaChange);
                arenaStateDirty = true;
            }

            if (hitObject is DodgeCameraChange cameraChange)
            {
                cameraChanges.Remove(cameraChange);
                cameraStateDirty = true;
            }
        }

        public double GetEffectiveMovementEndTime(DodgeBullet bullet)
            => Math.Min(bullet.MovementEndTime, ContinuedBulletEndTime);

        public double GetEffectiveMovementEndTime(DodgeEmitter emitter)
            => Math.Min(emitter.MovementEndTime, ContinuedBulletEndTime);

        public double GetEffectiveExitTime(DodgeEmitter emitter, int index)
            => Math.Min(emitter.ExitTimeAt(index), ContinuedBulletEndTime);

        public double GetEffectiveExitTime(DodgeEmitter emitter, int burstIndex, int rayIndex)
            => Math.Min(emitter.ExitTimeAt(burstIndex, rayIndex), ContinuedBulletEndTime);

        public void ReportGrazeProximity() => grazeProximityReported = true;

        public void TriggerMissFeedback()
        {
            MissFeedbackCount++;

            Player.TriggerMissFlash();

            if (!missSoundEnabled || missSoundVolume <= 0)
                return;

            MissSoundPlayCount++;
            missSound.Play();
        }

        public float PlayerSize => playerSize;

        public static float CalculateGrazeIndicatorDiameter(float grazeDistance, float projectileSize, float playerSize = DodgePlayer.SIZE)
            => Math.Max(0, playerSize) + Math.Max(0, projectileSize) + Math.Max(0, grazeDistance) * 2;

        public JudgementResult? RegisterGraze(double time, bool successful)
        {
            if (successful && GrazeDistance > 0 && time != lastGrazeEffectTime)
            {
                lastGrazeEffectTime = time;
                Player.TriggerGrazeEffect();
            }

            if (scoreProcessor == null || GrazeDistance <= 0)
                return null;

            var graze = new DodgeGrazeHitObject
            {
                StartTime = time,
                ScoreValue = GrazeScore,
            };

            var result = new JudgementResult(graze, graze.CreateJudgement())
            {
                Type = successful ? HitResult.SmallBonus : HitResult.IgnoreMiss,
            };

            scoreProcessor.ApplyResult(result);
            return result;
        }

        public void RevertGraze(JudgementResult? result)
        {
            if (result != null)
                scoreProcessor?.RevertResult(result);
        }

        internal void RegisterCollisionSource(IDodgeCollisionSource source)
        {
            if (!collisionSources.Contains(source))
            {
                collisionSources.Add(source);
                collisionScheduleDirty = true;
            }
        }

        internal void UnregisterCollisionSource(IDodgeCollisionSource source)
        {
            collisionSources.Remove(source);
            activeCollisionSources.Remove(source);
            collisionScheduleDirty = true;
        }

        private void refreshCollisionSchedule(double currentTime)
        {
            if (collisionScheduleDirty)
                rebuildCollisionSchedule(currentTime, true);

            while (nextCollisionSourceIndex < scheduledCollisionSources.Count
                   && scheduledCollisionSources[nextCollisionSourceIndex].CollisionStartTime <= currentTime)
            {
                activeCollisionSources.Add(scheduledCollisionSources[nextCollisionSourceIndex++]);
            }
        }

        private void rebuildCollisionSchedule(double currentTime, bool includeExpired)
        {
            scheduledCollisionSources.Clear();
            scheduledCollisionSources.AddRange(collisionSources);
            scheduledCollisionSources.Sort(compareCollisionSourceStartTime);
            activeCollisionSources.Clear();
            nextCollisionSourceIndex = 0;

            while (nextCollisionSourceIndex < scheduledCollisionSources.Count
                   && scheduledCollisionSources[nextCollisionSourceIndex].CollisionStartTime <= currentTime)
            {
                IDodgeCollisionSource source = scheduledCollisionSources[nextCollisionSourceIndex++];

                if (includeExpired || source.CollisionEndTime >= currentTime)
                    activeCollisionSources.Add(source);
            }

            collisionScheduleDirty = false;
        }

        private void removeExpiredCollisionSources(double currentTime)
        {
            for (int i = activeCollisionSources.Count - 1; i >= 0; i--)
            {
                if (!activeCollisionSources[i].CollisionProcessingComplete
                    && activeCollisionSources[i].CollisionEndTime >= currentTime)
                    continue;

                int lastIndex = activeCollisionSources.Count - 1;
                activeCollisionSources[i] = activeCollisionSources[lastIndex];
                activeCollisionSources.RemoveAt(lastIndex);
            }
        }

        private static int compareCollisionSourceStartTime(IDodgeCollisionSource first, IDodgeCollisionSource second)
            => first.CollisionStartTime.CompareTo(second.CollisionStartTime);

        private void refreshGameplayEndTime()
        {
            GameplayEndTime = DodgeGameplayTiming.GetGameplayEndTime(hitObjects);
            gameplayEndTimeDirty = false;
            GameplayEndTimeRefreshCount++;
        }

        private void refreshArenaStateEvaluator()
        {
            arenaStateEvaluator = new DodgeArenaStateEvaluator(arenaChanges);
            arenaStateDirty = false;
            ArenaStateRefreshCount++;
        }

        private void refreshCameraStateEvaluator()
        {
            cameraStateEvaluator = new DodgeCameraStateEvaluator(cameraChanges);
            cameraStateDirty = false;
            CameraStateVersion++;
        }

        /// <summary>
        /// Bumped whenever the camera scroll evaluator is rebuilt, so drawables
        /// can invalidate their cached spawn anchors.
        /// </summary>
        internal int CameraStateVersion { get; private set; }

        /// <summary>
        /// The accumulated field scroll offset at the given time. Drawables add
        /// the delta between this value at the current time and at their spawn
        /// time to their authored positions, so live objects drift with the
        /// scroll while freshly-spawned objects stay where they were placed.
        /// </summary>
        public Vector2 CameraOffsetAt(double time) => cameraStateEvaluator?.Evaluate(time) ?? Vector2.Zero;

        internal void RefreshCachedState()
        {
            if (gameplayEndTimeDirty)
                refreshGameplayEndTime();

            if (arenaStateDirty)
                refreshArenaStateEvaluator();

            if (cameraStateDirty)
                refreshCameraStateEvaluator();
        }

        private void trackHitObject(DodgeHitObject hitObject)
        {
            if (!trackedHitObjects.Add(hitObject))
                return;

            hitObject.StartTimeBindable.ValueChanged += onHitObjectStartTimeChanged;
            hitObject.DefaultsApplied += onHitObjectDefaultsApplied;
        }

        private void untrackHitObject(DodgeHitObject hitObject)
        {
            if (!trackedHitObjects.Remove(hitObject))
                return;

            hitObject.StartTimeBindable.ValueChanged -= onHitObjectStartTimeChanged;
            hitObject.DefaultsApplied -= onHitObjectDefaultsApplied;
        }

        private void onHitObjectStartTimeChanged(ValueChangedEvent<double> _)
        {
            gameplayEndTimeDirty = true;
            collisionScheduleDirty = true;

            // The bindable callback does not provide its owner. Rebuilding the
            // usually tiny arena and camera caches after an editor timing edit is
            // still much cheaper than hashing every hit object on every gameplay frame.
            arenaStateDirty = true;
            cameraStateDirty = true;
        }

        private void onHitObjectDefaultsApplied(HitObject hitObject)
        {
            gameplayEndTimeDirty = true;
            collisionScheduleDirty = true;

            if (hitObject is DodgeArenaChange)
                arenaStateDirty = true;

            if (hitObject is DodgeCameraChange)
                cameraStateDirty = true;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (DodgeHitObject hitObject in trackedHitObjects.ToArray())
                    untrackHitObject(hitObject);
            }

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Identifies the opaque black implementation used when the active skin has no Dodge arena texture.
        /// </summary>
        private partial class DefaultArenaBackground : Box
        {
            public bool SuppressRendering { get; set; }

            public override bool IsPresent => !SuppressRendering && base.IsPresent;
        }

        /// <summary>
        /// Keeps the arena background's semantic colour and alpha intact while avoiding a draw call when the
        /// default opaque black arena already produces the exact same output underneath it.
        /// </summary>
        private partial class ArenaBackgroundBox : Box
        {
            public bool SuppressRendering { get; set; }

            public override bool IsPresent => !SuppressRendering && base.IsPresent;
        }

        /// <summary>
        /// Smoothly oscillates the arena with a sine wave locked to the beat during kiai sections.
        /// Uses continuous Update() instead of transforms to avoid snapping/elastic artefacts.
        /// Fades in/out gracefully when kiai starts or ends.
        /// Amplitude is authored per <see cref="DodgeArenaChange"/> keyframe via KiaiShakeAngle.
        /// </summary>
        private partial class KiaiArenaShakeEffect : BeatSyncedContainer
        {
            private readonly DodgePlayfield playfield;
            private readonly Container target;

            /// <summary>Smoothed 0–1 intensity that fades in/out with kiai.</summary>
            private float kiaiIntensity;

            /// <summary>Beat index captured in OnNewBeat to drive the half-cycle direction.</summary>
            private int lastBeatIndex;

            /// <summary>Fallback amplitude when the keyframe value is 0 (effect disabled by default).</summary>
            private const float FADE_DURATION = 500f; // ms for full fade in / fade out

            public KiaiArenaShakeEffect(DodgePlayfield playfield, Container target)
            {
                this.playfield = playfield;
                this.target = target;
                RelativeSizeAxes = Axes.Both;
                Alpha = 0;
                AlwaysPresent = true;
            }

            protected override void OnNewBeat(
                int beatIndex,
                TimingControlPoint timingPoint,
                EffectControlPoint effectPoint,
                ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);
                lastBeatIndex = beatIndex;
            }

            protected override void Update()
            {
                base.Update();

                float amplitude = playfield.KiaiShakeAngle;

                // Smoothly fade intensity in/out when kiai state changes.
                bool active = IsKiaiTime && amplitude > 0;
                float delta = (float)(Time.Elapsed / FADE_DURATION);
                kiaiIntensity = Math.Clamp(kiaiIntensity + (active ? delta : -delta), 0f, 1f);

                if (kiaiIntensity < 0.001f)
                {
                    target.Rotation = 0;
                    return;
                }

                // One half-sine per beat, alternating direction each beat.
                // This creates a smooth pendulum perfectly locked to the music.
                double beatLength = TimingPoint.BeatLength;
                double phaseWithinBeat = Math.Clamp(TimeSinceLastBeat / beatLength, 0, 1);
                float direction = lastBeatIndex % 2 == 0 ? 1f : -1f;

                // sin(0→π) rises from 0 → 1 → 0 over one beat — zero-crossing at each beat boundary.
                target.Rotation = (float)(Math.Sin(phaseWithinBeat * Math.PI)
                                         * amplitude
                                         * kiaiIntensity
                                         * direction);
            }
        }
    }
}
