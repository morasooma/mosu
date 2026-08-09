// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Performance;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class AimAssistController : Drawable
    {
        private const double movement_history_window = 60;
        private const double intent_grace_window = 55;
        private const float stationary_release_speed = 55f;
        private const float engaged_base_offset_speed = 90f;
        private const float engaged_boost_offset_speed = 300f;
        private const float stationary_hold_speed_threshold = 35f;
        private const float stationary_hold_radius_padding = 20f;

        private Bindable<bool> enabled = null!;
        private Bindable<double> strength = null!;
        private Bindable<float> fovRadius = null!;
        private Bindable<double> intentThreshold = null!;
        private Bindable<double> dynamicFriction = null!;
        private Bindable<double> antiJitterMs = null!;
        private Bindable<double> centerBias = null!;
        private Bindable<float> overshootAllowance = null!;
        private Bindable<bool> showFlowDebug = null!;

        private readonly Queue<MovementSample> recentMovement = new Queue<MovementSample>();
        private readonly List<TargetDescriptor> targetBuffer = new List<TargetDescriptor>();
        private readonly List<OsuPatternNode> patternNodeBuffer = new List<OsuPatternNode>(32);
        private readonly List<TargetDescriptor> railTargetBuffer = new List<TargetDescriptor>(6);
        private readonly List<Vector2> projectionPointBuffer = new List<Vector2>(72);
        private readonly List<float> projectionArcLengthBuffer = new List<float>(72);
        private readonly List<Vector2> railSourcePointBuffer = new List<Vector2>(8);
        private readonly List<double> railSourceTimeBuffer = new List<double>(8);
        private readonly List<TargetDescriptor> railDescriptorBuffer = new List<TargetDescriptor>(12);
        private readonly List<TargetDescriptor> resolvedFlowHistory = new List<TargetDescriptor>(4);
        private readonly List<TargetDescriptor> flowHistoryBuffer = new List<TargetDescriptor>(4);

        private IBeatmap? beatmap;
        private OsuInputManager? inputManager;
        private OsuPlayfield? playfield;
        private RelaxController? relaxController;
        private DrawableRuleset? drawableRuleset;
        private OsuModMosuAimAssist? mosuAimAssistMod;
        private OsuModMosuAntiAimAssist? mosuAntiAimAssistMod;

        private DrawableOsuHitObject? lockedTarget;
        private Vector2 assistOffset;
        private Vector2 averagedVelocity;
        private Vector2 lastRawPosition;
        private bool hasLastRawPosition;
        private bool lastPrimaryPressed;
        private bool drivingCursor;
        private bool hitResultsHooked;
        private double antiJitterRemaining;
        private Vector2 antiJitterOutputAnchor;
        private double lastIntentPassTime = double.NegativeInfinity;
        private float lastPositiveIntentScore = -1;
        private DrawableOsuHitObject? lastContextDrawable;
        private Vector2 targetSwitchAnchorOffset;
        private double lastTargetSwitchTime = double.NegativeInfinity;
        private float lastTargetSwitchCarryWeight;
        private Vector2? lastReleasedTargetPosition;
        private double lastReleasedTargetTime = double.NegativeInfinity;
        private bool hasLastResolvedTarget;
        private TargetDescriptor lastResolvedTarget;
        private bool wasSpinnerActiveLastFrame;
        private bool hasActiveSpinnerThisFrame;
        private bool hasStreamSmoothingState;
        private Vector2 streamSmoothedPoint;
        private Vector2 streamSmoothedTangent;
        private bool hasStreamPathTimingState;
        private double streamPathStartTime;
        private double streamPathEndTime;
        private double streamPathLastSampleTime;
        private float streamPathProgress;
        private bool hasCommittedStreamProjectionState;
        private double committedStreamStartTime;
        private double committedStreamEndTime;
        private OsuStreamShapeKind committedStreamShape;
        private OsuStreamSpacingKind committedStreamSpacing;
        private readonly List<Vector2> committedStreamPathBuffer = new List<Vector2>(128);
        private readonly List<Vector2> committedStreamSourceBuffer = new List<Vector2>(24);
        private readonly List<double> committedStreamSourceTimeBuffer = new List<double>(24);
        private bool hasAdaptiveRadiusState;
        private float smoothedAdaptiveRadius;
        private DrawableOsuHitObject? adaptiveRadiusTarget;
        private Vector2[] debugFlowPathPoints = Array.Empty<Vector2>();
        private Vector2[] debugFlowSourcePoints = Array.Empty<Vector2>();
        private Vector2 previousOffsetVelocity;
        private bool hasPreviousOffsetVelocity;

        private Vector2 antiAssistDrift;
        private Vector2 antiAssistVelocity;

        public bool IsEnabled => isAssistEnabled();
        public bool IsDrivingCursor => drivingCursor;

        /// <summary>
        /// Whether the mod's ShowDebug is turned on. Forces cursor debug visuals (real vs assisted + radius).
        /// </summary>
        public bool ForceShowDebugFromMod => mosuAimAssistMod?.ShowDebug.Value ?? false;

        private AimAssistTargetDisplay? debugDisplay;

        /// <summary>
        /// Registers the debug display so this controller can wake it when the mod's ShowDebug is switched on.
        /// </summary>
        /// <remarks>
        /// The display drops out of the update traversal entirely while hidden (see
        /// <see cref="MosuOptimisationToggles.LazyDebugOverlays"/>), so it cannot poll
        /// <see cref="ForceShowDebugFromMod"/> itself. This controller always runs, so it pushes instead.
        /// </remarks>
        public void AttachDebugDisplay(AimAssistTargetDisplay display) => debugDisplay = display;
        public bool PassedActivationFilters { get; private set; }
        public Vector2 CurrentOutputPosition { get; private set; }

        /// <summary>
        /// The real (hardware) cursor position reported by the user input, before any assist offset is applied.
        /// Used for debug display (raw vs assisted) and for intent detection.
        /// </summary>
        public Vector2 RawCursorPosition { get; private set; }

        public Vector2? CurrentTargetPosition { get; private set; }
        public float CurrentBaseTargetRadius { get; private set; }
        public float CurrentTargetRadius { get; private set; }
        public float CurrentAdaptiveRadiusScale { get; private set; } = 1;
        public Vector2? CurrentAssistPointPosition { get; private set; }
        public Vector2? CurrentNextTargetPosition { get; private set; }
        public float CurrentNextTargetRadius { get; private set; }
        public float CurrentOffsetMagnitude => assistOffset.Length;
        public bool IsAimAssistEnabled => getMosuAimAssistProfile().HasValue || enabled.Value;
        public bool IsAimAssistDeclaredMod => mosuAimAssistMod != null;
        public long AdjustedFrameCount { get; private set; }
        public float MaxAdjustmentMagnitude { get; private set; }
        public float CurrentIntentScore { get; private set; }
        public bool IsAntiJitterActive => antiJitterRemaining > 0;
        public string CurrentModeName { get; private set; } = @"idle";
        public string CurrentFlowDebugModeName { get; private set; } = @"idle";
        public bool IsAntiAimActive => mosuAntiAimAssistMod != null;
        public double? CurrentFocusTime { get; private set; }
        public string CurrentPatternDebugLabel { get; private set; } = string.Empty;
        public Vector2? CurrentFlowDebugAnchorPosition { get; private set; }
        public IReadOnlyList<Vector2> CurrentFlowDebugPathPoints => debugFlowPathPoints;
        public IReadOnlyList<Vector2> CurrentFlowDebugSourcePoints => debugFlowSourcePoints;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IBeatmap? beatmap)
        {
            this.beatmap = beatmap;
            enabled = config.GetBindable<bool>(OsuSetting.ForkAimAssistEnabled);
            strength = config.GetBindable<double>(OsuSetting.ForkAimAssistStrength);
            fovRadius = config.GetBindable<float>(OsuSetting.ForkAimAssistFovRadius);
            intentThreshold = config.GetBindable<double>(OsuSetting.ForkAimAssistIntentThreshold);
            dynamicFriction = config.GetBindable<double>(OsuSetting.ForkAimAssistDynamicFriction);
            antiJitterMs = config.GetBindable<double>(OsuSetting.ForkAimAssistAntiJitterMs);
            centerBias = config.GetBindable<double>(OsuSetting.ForkAimAssistCenterBias);
            overshootAllowance = config.GetBindable<float>(OsuSetting.ForkAimAssistOvershootAllowance);
            showFlowDebug = config.GetBindable<bool>(OsuSetting.ForkAimAssistShowFlowDebug);
        }

        public void ApplyAntiAssist(OsuModMosuAntiAimAssist mod)
        {
            mosuAntiAimAssistMod = mod;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager() as OsuInputManager;
            playfield = this.FindClosestParent<OsuPlayfield>();
            relaxController = playfield?.RelaxController;
            drawableRuleset = this.FindClosestParent<DrawableRuleset>();
            cacheMosuAimAssistMod();
            ensureHitResultHook();

            if (inputManager != null)
            {
                CurrentOutputPosition = inputManager.CurrentState.Mouse.Position;

                if (tryGetRawPosition(out Vector2 rawPosition))
                {
                    lastRawPosition = rawPosition;
                    hasLastRawPosition = true;
                    RawCursorPosition = rawPosition;
                }
                else
                    hasLastRawPosition = false;
            }
        }

        protected override void Update()
        {
            base.Update();

            inputManager ??= GetContainingInputManager() as OsuInputManager;
            playfield ??= this.FindClosestParent<OsuPlayfield>();
            relaxController ??= playfield?.RelaxController;
            drawableRuleset ??= this.FindClosestParent<DrawableRuleset>();
            cacheMosuAimAssistMod();
            ensureHitResultHook();

            // Must run before the early returns below: the display is not traversed while hidden, so this is the
            // only thing that can bring it back when the mod's ShowDebug checkbox is ticked mid-session.
            if (ForceShowDebugFromMod)
                debugDisplay?.SetDebugVisible(true);

            if (inputManager == null || playfield == null)
                return;

            if (inputManager.ReplayInputHandler != null)
            {
                hasLastRawPosition = false;
                clearState(inputManager.CurrentState.Mouse.Position, restoreOriginalPosition: false);
                return;
            }

            if (!tryGetRawPosition(out Vector2 rawPosition))
            {
                if (inputManager.ReplayInputHandler != null || inputManager.ReplayBotActive)
                {
                    hasLastRawPosition = false;
                    clearState(inputManager.CurrentState.Mouse.Position, restoreOriginalPosition: false);
                }

                return;
            }

            double elapsed = Math.Abs(Clock.ElapsedFrameTime);

            if (elapsed <= 0)
                return;

            Vector2 rawDelta = hasLastRawPosition ? rawPosition - lastRawPosition : Vector2.Zero;

            lastRawPosition = rawPosition;
            hasLastRawPosition = true;
            RawCursorPosition = rawPosition;

            updateMovementHistory(rawDelta, elapsed);
            updateAntiJitterState(elapsed);

            if (!isAssistEnabled())
            {
                clearState(rawPosition);
                return;
            }

            List<TargetDescriptor> targets = getTargets();
            int currentTargetIndex = chooseCurrentTargetIndex(targets);
            TargetDescriptor? currentTarget = currentTargetIndex >= 0 ? targets[currentTargetIndex] : null;
            TargetDescriptor? previousTarget = getFlowPreviousTarget(targets, currentTargetIndex);
            TargetDescriptor? nextTarget = currentTargetIndex >= 0 && currentTargetIndex + 1 < targets.Count ? targets[currentTargetIndex + 1] : null;

            if (hasActiveSpinner())
            {
                clearTargetState();
                Vector2 releaseOffset = updateAssistOffset(Vector2.Zero, false, rawPosition, rawDelta, elapsed, 0, 0, false, false, false, getStrengthFactor());
                float spinnerReleaseSpeed = Math.Max(420f, releaseOffset.Length * 38f);
                assistOffset = moveTowards(releaseOffset, Vector2.Zero, spinnerReleaseSpeed * (float)elapsed / 1000f);

                if (!wasSpinnerActiveLastFrame && releaseOffset.Length > 0.001f)
                {
                    float spinnerReleaseFloor = Math.Min(26f, Math.Max(14f, releaseOffset.Length * 0.24f));

                    if (assistOffset.Length < spinnerReleaseFloor)
                        assistOffset = normaliseOrZero(releaseOffset) * spinnerReleaseFloor;
                }

                applyJerkLimiter(elapsed);
                applyOutput(rawPosition);
                wasSpinnerActiveLastFrame = true;
                return;
            }

            wasSpinnerActiveLastFrame = false;

            if (currentTarget == null)
            {
                PassedActivationFilters = false;
                CurrentIntentScore = 0;
                CurrentModeName = @"idle";
                CurrentFlowDebugModeName = @"idle";
                updateDisplayState(null, nextTarget, targets, Array.Empty<OsuPatternState>(), currentTargetIndex, previousTarget);

                assistOffset = updateAssistOffset(Vector2.Zero, false, rawPosition, rawDelta, elapsed, 0, 0, false, false, false, getStrengthFactor());
                applyModeTransition(@"idle", elapsed);
                applyJerkLimiter(elapsed);
                applyOutput(rawPosition);
                return;
            }

            OsuPatternState[] patternStates = analyzePatternStates(targets, currentTargetIndex, previousTarget);
            AimAssistContext context = createContext(currentTarget.Value, previousTarget, nextTarget, targets, patternStates, currentTargetIndex, rawPosition);
            context = applyAdaptiveRadiusSmoothing(context, elapsed);
            context = applyContinuousStreamSmoothing(context, rawPosition, elapsed);
            registerTargetContext(context);
            updateDisplayState(context, nextTarget, targets, patternStates, currentTargetIndex, previousTarget);

            float contextStrengthFactor = getStrengthFactor(context);
            ActivationState activation = evaluateActivation(context, rawPosition, contextStrengthFactor);

            PassedActivationFilters = activation.Passed;
            CurrentIntentScore = activation.IntentScore;
            CurrentModeName = context.ModeName;
            float radialFrictionAmount = activation.Passed
                ? getDynamicFrictionAmount(context, rawPosition)
                : 0;

            Vector2 targetOffset = activation.Passed
                ? computeEngagedOffset(context, activation, rawPosition, contextStrengthFactor)
                : Vector2.Zero;

            bool pointMode = context.ModeName == @"point";
            bool streamMode = context.ModeName == @"stream";
            bool shortRepeatSliderMode = context.ModeName == @"slider-repeat";

            if (mosuAntiAimAssistMod != null)
            {
                applyAntiAssistEffects(elapsed);
            }
            else
            {
                assistOffset = updateAssistOffset(targetOffset, activation.Passed, rawPosition, rawDelta, elapsed, activation.EngageAmount, radialFrictionAmount, pointMode, shortRepeatSliderMode, streamMode, contextStrengthFactor);
            }

            applyModeTransition(context.ModeName, elapsed);
            applyJerkLimiter(elapsed);
            applyOutput(rawPosition);
        }

        private void applyAntiAssistEffects(double elapsed)
        {
            if (mosuAntiAimAssistMod == null) return;

            float dt = (float)elapsed / 1000f;
            float intensity = mosuAntiAimAssistMod.Intensity.Value;
            float driftStrength = mosuAntiAimAssistMod.RandomDrift.Value;

            Vector2 targetAntiOffset = Vector2.Zero;

            // 1. Random Drift
            if (driftStrength > 0)
            {
                float t = (float)Time.Current / 1000f;
                Vector2 noise = new Vector2(
                    (float)(Math.Sin(t * 1.7f) * 0.5f + Math.Sin(t * 3.1f) * 0.3f + Math.Sin(t * 7.4f) * 0.2f),
                    (float)(Math.Cos(t * 1.5f) * 0.5f + Math.Cos(t * 4.2f) * 0.3f + Math.Cos(t * 6.8f) * 0.2f)
                );
                targetAntiOffset += noise * (driftStrength * 80f);
            }

            // 2. Target Repulsion
            if (intensity > 0 && CurrentTargetPosition.HasValue)
            {
                Vector2 toTarget = CurrentTargetPosition.Value - (lastRawPosition + assistOffset);
                float dist = toTarget.Length;

                if (dist < 180f)
                {
                    float repulsionFactor = (float)Math.Pow(1f - Math.Clamp(dist / 180f, 0, 1), 2);
                    targetAntiOffset -= normaliseOrZero(toTarget) * intensity * repulsionFactor * 130f;
                }
            }

            // Smoothly move assistOffset towards the anti-aim target
            assistOffset = smoothDamp(assistOffset, targetAntiOffset, ref antiAssistVelocity, 0.05f, 10000f, dt);
        }

        private void clearState(Vector2 rawPosition, bool restoreOriginalPosition = true)
        {
            clearTargetState();
            assistOffset = Vector2.Zero;
            previousOffsetVelocity = Vector2.Zero;
            hasPreviousOffsetVelocity = false;
            antiAssistVelocity = Vector2.Zero;
            antiAssistDrift = Vector2.Zero;
            wasSpinnerActiveLastFrame = false;

            releaseVirtualCursor(restoreOriginalPosition);
            CurrentOutputPosition = restoreOriginalPosition
                ? rawPosition
                : inputManager?.CurrentState.Mouse.Position ?? rawPosition;
            RawCursorPosition = rawPosition;
        }

        private void clearTargetState()
        {
            lockedTarget = null;
            PassedActivationFilters = false;
            CurrentIntentScore = 0;
            CurrentModeName = @"idle";
            CurrentFlowDebugModeName = @"idle";
            antiJitterRemaining = 0;
            lastPositiveIntentScore = -1;
            lastIntentPassTime = double.NegativeInfinity;
            lastContextDrawable = null;
            targetSwitchAnchorOffset = Vector2.Zero;
            lastTargetSwitchTime = double.NegativeInfinity;
            lastTargetSwitchCarryWeight = 0;
            lastReleasedTargetPosition = null;
            lastReleasedTargetTime = double.NegativeInfinity;
            hasLastResolvedTarget = false;
            lastResolvedTarget = default;
            resolvedFlowHistory.Clear();
            flowHistoryBuffer.Clear();
            clearStreamSmoothingState();
            clearCommittedStreamProjectionState();
            clearAdaptiveRadiusState();
            clearDisplayState();
        }

        private void clearStreamSmoothingState()
        {
            hasStreamSmoothingState = false;
            streamSmoothedPoint = Vector2.Zero;
            streamSmoothedTangent = Vector2.Zero;
            clearStreamOutputMotionState();
            clearStreamPathTimingState();
        }

        private void clearStreamPathTimingState()
        {
            hasStreamPathTimingState = false;
            streamPathStartTime = 0;
            streamPathEndTime = 0;
            streamPathLastSampleTime = double.NegativeInfinity;
            streamPathProgress = 0;
        }

        private void clearCommittedStreamProjectionState()
        {
            hasCommittedStreamProjectionState = false;
            committedStreamStartTime = 0;
            committedStreamEndTime = 0;
            committedStreamShape = OsuStreamShapeKind.None;
            committedStreamSpacing = OsuStreamSpacingKind.None;
            committedStreamPathBuffer.Clear();
            committedStreamSourceBuffer.Clear();
            committedStreamSourceTimeBuffer.Clear();
            clearStreamPathTimingState();
        }

        private void clearStreamOutputMotionState()
        {
            // Stream output motion state was removed when the spring-based offset model became authoritative.
        }

        private void clearAdaptiveRadiusState()
        {
            hasAdaptiveRadiusState = false;
            smoothedAdaptiveRadius = 0;
            adaptiveRadiusTarget = null;
        }

        private void registerTargetContext(AimAssistContext context)
        {
            if (ReferenceEquals(lastContextDrawable, context.Drawable))
                return;

            if (lastContextDrawable != null)
            {
                lastTargetSwitchTime = Time.Current;
                targetSwitchAnchorOffset = assistOffset;
                lastTargetSwitchCarryWeight = getTargetSwitchCarryWeight(context);
            }

            lastContextDrawable = context.Drawable;
        }

        private static float getTargetSwitchCarryWeight(AimAssistContext context)
        {
            float carryWeight = context.ModeName switch
            {
                @"stream" => 0.52f,
                @"rail" => 0.44f,
                @"point" => Math.Max(0.36f, Math.Max(context.PointFlowBias, context.PatternInfo.ContinuityWeight * 0.72f)),
                _ => 0
            };

            if (context.ModeName == @"point")
            {
                if (context.PreviewPoint.HasValue)
                    carryWeight = Math.Max(carryWeight, 0.34f);

                if (context.PatternInfo.Kind == OsuPatternKind.Burst)
                    carryWeight = Math.Max(carryWeight, 0.42f + context.PatternInfo.ContinuityWeight * 0.28f);
                else if (context.PatternState.Candidate == OsuPatternSegmentKind.Jump)
                    carryWeight = Math.Max(carryWeight, 0.24f + context.PatternState.JumpSeverity * 0.26f);
            }

            return Math.Clamp(carryWeight, 0, 1);
        }

        private double getGameplayRate()
        {
            double rate = (Clock as IGameplayClock)?.GetTrueGameplayRate() ?? Clock.Rate;
            return Math.Max(0.05, Math.Abs(rate));
        }

        private double scaleRealTimeWindow(double milliseconds)
            => milliseconds * getGameplayRate();

        private double toRealTimeWindow(double gameplayMilliseconds)
            => gameplayMilliseconds / getGameplayRate();

        protected override void Dispose(bool isDisposing)
        {
            if (hitResultsHooked && playfield != null)
                playfield.HitObjectContainer.NewResult -= onHitObjectResult;

            base.Dispose(isDisposing);
        }

        private static Vector2 normaliseOrZero(Vector2 value)
        {
            float length = value.Length;
            return length <= 0.0001f ? Vector2.Zero : value / length;
        }

        private static Vector2 interpolate(Vector2 start, Vector2 end, float amount)
            => start + (end - start) * Math.Clamp(amount, 0, 1);

        private static Vector2 moveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
        {
            Vector2 change = target - current;
            float distance = change.Length;

            if (distance <= maxDistanceDelta || distance <= 0.0001f)
                return target;

            return current + change / distance * maxDistanceDelta;
        }

        private static Vector2 dampVector(Vector2 current, Vector2 target, float duration, double elapsed)
        {
            return new Vector2(
                (float)Interpolation.DampContinuously(current.X, target.X, duration, elapsed),
                (float)Interpolation.DampContinuously(current.Y, target.Y, duration, elapsed));
        }

        private static Vector2 clampLength(Vector2 value, float maxLength)
        {
            float length = value.Length;

            if (length <= maxLength || length <= 0.0001f)
                return value;

            return value / length * maxLength;
        }

        private static Vector2 projectOntoTangentLine(Vector2 position, Vector2 anchor, Vector2 tangent)
        {
            Vector2 tangentDirection = normaliseOrZero(tangent);

            if (tangentDirection.LengthSquared <= 0.0001f)
                return anchor;

            return anchor + tangentDirection * Vector2.Dot(position - anchor, tangentDirection);
        }

        private static Vector2 getTangentLineCorrectionVector(Vector2 position, Vector2 anchor, Vector2 tangent)
            => projectOntoTangentLine(position, anchor, tangent) - position;

        private static float getTangentLineCorrectionDistance(Vector2 position, Vector2 anchor, Vector2 tangent)
            => getTangentLineCorrectionVector(position, anchor, tangent).Length;

        private ProjectionResult getStreamCorridorProjection(Vector2 query, AimAssistContext context)
        {
            if (context.ModeName == @"stream" && projectionPointBuffer.Count >= 2)
                return projectOntoPolyline(projectionPointBuffer, query);

            Vector2 tangentDirection = normaliseOrZero(context.Tangent);

            if (tangentDirection.LengthSquared <= 0.0001f)
                tangentDirection = normaliseOrZero(context.DesiredPoint - query);

            if (tangentDirection.LengthSquared <= 0.0001f)
                return new ProjectionResult(context.DesiredPoint, Vector2.Zero);

            Vector2 point = projectOntoTangentLine(query, context.DesiredPoint, tangentDirection);
            return new ProjectionResult(point, tangentDirection);
        }

        private Vector2 getStreamCorridorCorrectionVector(Vector2 query, AimAssistContext context)
            => getStreamCorridorProjection(query, context).Point - query;

        private float getStreamCorridorDistance(Vector2 query, AimAssistContext context)
            => getStreamCorridorCorrectionVector(query, context).Length;

        private static Vector2 blendDirections(Vector2 from, Vector2 to, float amount)
        {
            Vector2 blended = interpolate(from, to, amount);

            if (blended.LengthSquared <= 0.0001f)
                return to.LengthSquared > 0.0001f ? normaliseOrZero(to) : normaliseOrZero(from);

            return normaliseOrZero(blended);
        }

        private AimAssistContext applyContinuousStreamSmoothing(AimAssistContext context, Vector2 rawPosition, double elapsed)
        {
            if (context.ModeName != @"stream")
            {
                clearStreamSmoothingState();
                return context;
            }

            Vector2 targetPoint = context.DesiredPoint;
            Vector2 targetTangent = normaliseOrZero(context.Tangent);

            if (targetTangent.LengthSquared <= 0.0001f)
            {
                clearStreamSmoothingState();
                return context;
            }

            float cornerSharpness = getStreamCornerSharpness(context, targetTangent);
            Vector2 targetDirection = getRoundedStreamTargetDirection(context, targetTangent, cornerSharpness);
            float shapeSmoothingBias = context.PatternInfo.StreamShape switch
            {
                OsuStreamShapeKind.ZigZag => 0.34f,
                OsuStreamShapeKind.Arc => 0.18f,
                _ => 0f
            };
            float spacingSmoothingBias = context.PatternInfo.StreamSpacing switch
            {
                OsuStreamSpacingKind.Spaced => 0.28f,
                OsuStreamSpacingKind.Variable => 0.18f,
                _ => 0f
            };
            float lowDensityFlowBias = context.PatternInfo.IsLowDensityFlow
                ? Math.Clamp((0.62f - context.PatternInfo.DensityWeight) / 0.62f, 0, 1) * Math.Max(0.52f, context.PatternInfo.ContinuityWeight)
                : 0;
            float smoothingBias = Math.Max(Math.Max(shapeSmoothingBias, spacingSmoothingBias), lowDensityFlowBias * 0.58f);

            if (!hasStreamSmoothingState || shouldResetStreamSmoothingState(targetPoint, context.Radius, context.PatternInfo))
            {
                hasStreamSmoothingState = true;
                streamSmoothedPoint = targetPoint;
                streamSmoothedTangent = targetDirection;
            }
            else
            {
                float tangentDuration = (float)Interpolation.Lerp(18f, 74f + 52f * smoothingBias, cornerSharpness);
                float pointDuration = (float)Interpolation.Lerp(16f, 62f + 44f * smoothingBias, cornerSharpness);

                if (lowDensityFlowBias > 0)
                {
                    tangentDuration = (float)Interpolation.Lerp(tangentDuration, 104f + 78f * smoothingBias, lowDensityFlowBias);
                    pointDuration = (float)Interpolation.Lerp(pointDuration, 92f + 66f * smoothingBias, lowDensityFlowBias);
                }

                float tangentBlend = getContinuousSmoothingFactor(elapsed, tangentDuration);
                float pointBlend = getContinuousSmoothingFactor(elapsed, pointDuration);
                Vector2 smoothedTangent = blendDirections(streamSmoothedTangent, targetDirection, tangentBlend);
                Vector2 projectedPoint = projectOntoTangentLine(streamSmoothedPoint, targetPoint, smoothedTangent);

                if (lowDensityFlowBias > 0)
                    pointBlend *= 1 - lowDensityFlowBias * 0.24f;

                streamSmoothedTangent = smoothedTangent;
                streamSmoothedPoint = interpolate(projectedPoint, targetPoint, pointBlend);
            }

            return new AimAssistContext(
                context.ModeName,
                context.Drawable,
                streamSmoothedPoint,
                streamSmoothedTangent,
                streamSmoothedTangent,
                streamSmoothedPoint,
                context.Radius,
                getTangentLineCorrectionDistance(rawPosition, streamSmoothedPoint, streamSmoothedTangent),
                context.FocusTime,
                context.PreemptTime,
                context.AllowPassiveAssist,
                context.PreviewPoint,
                context.PreviewRadius,
                context.PointFlowBias,
                context.PatternInfo,
                context.PatternState);
        }

        private AimAssistContext applyAdaptiveRadiusSmoothing(AimAssistContext context, double elapsed)
        {
            float baseRadius = getDrawableTargetRadius(context.Drawable, context.Drawable.HitObject);

            if (baseRadius <= 0.01f || context.Radius <= 0.01f)
            {
                clearAdaptiveRadiusState();
                return context;
            }

            float desiredScale = context.Radius / baseRadius;
            desiredScale = dampAdaptiveRadiusScale(context, desiredScale);
            float targetRadius = baseRadius * desiredScale;
            bool sameTarget = ReferenceEquals(adaptiveRadiusTarget, context.Drawable);

            if (!hasAdaptiveRadiusState)
            {
                hasAdaptiveRadiusState = true;
                smoothedAdaptiveRadius = targetRadius;
            }
            else
            {
                float duration = getAdaptiveRadiusBlendDuration(context, sameTarget);
                float blend = getContinuousSmoothingFactor(elapsed, duration);
                smoothedAdaptiveRadius = (float)Interpolation.Lerp(smoothedAdaptiveRadius, targetRadius, blend);
            }

            adaptiveRadiusTarget = context.Drawable;
            return new AimAssistContext(
                context.ModeName,
                context.Drawable,
                context.DesiredPoint,
                context.IntentDirection,
                context.Tangent,
                context.DisplayPoint,
                smoothedAdaptiveRadius,
                context.DistanceToDesired,
                context.FocusTime,
                context.PreemptTime,
                context.AllowPassiveAssist,
                context.PreviewPoint,
                context.PreviewRadius,
                context.PointFlowBias,
                context.PatternInfo,
                context.PatternState);
        }

        private float dampAdaptiveRadiusScale(AimAssistContext context, float desiredScale)
        {
            float minScale = 0.82f;
            float maxScale = 1.28f;
            float influence = 0.7f;
            float jumpAssistWeight = getPointJumpAssistWeight(context);

            if (context.ModeName is @"slider" or @"slider-repeat")
            {
                minScale = 0.92f;
                maxScale = 1.12f;
                influence = 0.45f;
            }
            else if (context.PatternState.SupportsFlowPath)
            {
                minScale = 0.9f;
                maxScale = context.PatternState.IsCommittedStream && context.PatternInfo.IsSpacedStream ? 1.2f : 1.14f;
                influence = context.PatternState.IsCommittedStream && context.PatternInfo.IsSpacedStream ? 0.58f : 0.42f;
            }
            else if (context.PatternState.Candidate == OsuPatternSegmentKind.BurstFlow || context.PatternInfo.Kind == OsuPatternKind.Burst)
            {
                minScale = 0.88f;
                maxScale = 1.18f;
                influence = 0.56f;
            }
            else if (context.PreviewPoint.HasValue)
            {
                minScale = 0.84f;
                maxScale = 1.24f;
                influence = 0.78f;
            }

            if (jumpAssistWeight > 0)
            {
                minScale = (float)Interpolation.Lerp(minScale, 0.92f, jumpAssistWeight);
                maxScale = (float)Interpolation.Lerp(maxScale, 1.38f, jumpAssistWeight);
                influence = (float)Interpolation.Lerp(influence, 0.92f, jumpAssistWeight);
            }

            desiredScale = (float)Interpolation.Lerp(1f, desiredScale, influence);
            return Math.Clamp(desiredScale, minScale, maxScale);
        }

        private float getAdaptiveRadiusBlendDuration(AimAssistContext context, bool sameTarget)
        {
            float duration = sameTarget ? 82f : 126f;
            float jumpAssistWeight = getPointJumpAssistWeight(context);

            if (context.PatternState.SupportsFlowPath)
                duration = sameTarget ? 108f : 148f;
            else if (context.PatternState.Candidate == OsuPatternSegmentKind.BurstFlow || context.PatternInfo.Kind == OsuPatternKind.Burst)
                duration = sameTarget ? 94f : 136f;
            else if (context.ModeName is @"slider" or @"slider-repeat")
                duration = sameTarget ? 72f : 110f;

            if (jumpAssistWeight > 0)
                duration = (float)Interpolation.Lerp(duration, sameTarget ? 62f : 88f, jumpAssistWeight);

            return duration;
        }

        private float getPointJumpAssistWeight(AimAssistContext context)
        {
            if (context.ModeName != @"point"
                || !context.PreviewPoint.HasValue
                || context.PointFlowBias > 0
                || context.PatternState.SupportsFlowPath)
                return 0;

            float previewRadius = context.PreviewRadius > 0 ? context.PreviewRadius : context.Radius;
            float referenceRadius = Math.Max(1f, Math.Min(context.Radius, previewRadius));
            float spacingRatio = (context.PreviewPoint.Value - context.DesiredPoint).Length / Math.Max(1f, referenceRadius * 2f);
            return Math.Clamp((spacingRatio - 1.35f) / 1.85f, 0, 1);
        }

        private static float getGentleFlowAssistWeight(OsuPatternInfo patternInfo)
        {
            if (!patternInfo.IsGentleFlow)
                return 0;

            float densityWeight = Math.Clamp((0.42f - patternInfo.DensityWeight) / 0.42f, 0, 1);
            float spacingWeight = Math.Clamp((1.95f - patternInfo.AverageSpacingRatio) / 0.95f, 0, 1);
            float continuityWeight = Math.Clamp((patternInfo.ContinuityWeight - 0.56f) / 0.34f, 0, 1);

            return Math.Clamp(densityWeight * 0.44f + spacingWeight * 0.34f + continuityWeight * 0.22f, 0, 1);
        }

        private bool shouldResetStreamSmoothingState(Vector2 targetPoint, float radius, OsuPatternInfo patternInfo)
        {
            float continuityThreshold = Math.Max(radius * 5.8f, 84f);

            if (patternInfo.IsSpacedStream)
                continuityThreshold = Math.Max(continuityThreshold, radius * 7.4f);
            else if (patternInfo.IsVariableStream)
                continuityThreshold = Math.Max(continuityThreshold, radius * 6.6f);

            return (targetPoint - streamSmoothedPoint).Length > continuityThreshold;
        }

        private static float getContinuousSmoothingFactor(double elapsed, float duration)
        {
            if (elapsed <= 0)
                return 0;

            return 1 - (float)Math.Exp(-elapsed / Math.Max(1f, duration));
        }

        private float getStreamCornerSharpness(AimAssistContext context, Vector2 tangentDirection)
        {
            if (!context.PreviewPoint.HasValue || tangentDirection.LengthSquared <= 0.0001f)
                return 0;

            Vector2 futureDirection = normaliseOrZero(context.PreviewPoint.Value - context.DesiredPoint);

            if (futureDirection.LengthSquared <= 0.0001f)
                return 0;

            float alignment = Vector2.Dot(tangentDirection, futureDirection);
            return Math.Clamp((0.82f - alignment) / 1.42f, 0, 1);
        }

        private Vector2 getRoundedStreamTargetDirection(AimAssistContext context, Vector2 tangentDirection, float cornerSharpness)
        {
            if (!context.PreviewPoint.HasValue || cornerSharpness <= 0)
                return tangentDirection;

            Vector2 futureDirection = normaliseOrZero(context.PreviewPoint.Value - context.DesiredPoint);

            if (futureDirection.LengthSquared <= 0.0001f)
                return tangentDirection;

            return blendDirections(tangentDirection, futureDirection, (float)Interpolation.Lerp(0.04f, 0.32f, cornerSharpness));
        }

        private void cacheMosuAimAssistMod()
        {
            mosuAimAssistMod = drawableRuleset?.Mods.OfType<OsuModMosuAimAssist>().FirstOrDefault();
        }

        private MosuAimAssistProfile? getMosuAimAssistProfile() => mosuAimAssistMod?.DerivedProfile;

        private bool isAssistEnabled() => getMosuAimAssistProfile().HasValue || enabled.Value || mosuAntiAimAssistMod != null;

        private double getConfiguredStrength() => getMosuAimAssistProfile()?.Strength ?? strength.Value;

        private float getConfiguredFovRadius() => getMosuAimAssistProfile()?.FovRadius ?? fovRadius.Value;

        private double getConfiguredIntentThreshold() => getMosuAimAssistProfile()?.IntentThreshold ?? intentThreshold.Value;

        private double getConfiguredDynamicFriction() => getMosuAimAssistProfile()?.DynamicFriction ?? dynamicFriction.Value;

        private double getConfiguredAntiJitterMs() => getMosuAimAssistProfile()?.AntiJitterMs ?? antiJitterMs.Value;

        private double getConfiguredCenterBias() => getMosuAimAssistProfile()?.CenterBias ?? centerBias.Value;

        private float getConfiguredOvershootAllowance() => getMosuAimAssistProfile()?.OvershootAllowance ?? overshootAllowance.Value;

        private bool shouldShowFlowDebug() => showFlowDebug.Value;

        private float getStrengthFactor()
            => Math.Clamp((float)Math.Pow(Math.Clamp((float)getConfiguredStrength(), 0, 1), 0.55f), 0, 1);

        private float getStrengthFactor(AimAssistContext context)
        {
            float configuredStrengthFactor = getStrengthFactor();
            return configuredStrengthFactor * getAdaptiveStrengthMultiplier(context, configuredStrengthFactor);
        }

        private float getAdaptiveStrengthMultiplier(AimAssistContext context, float configuredStrengthFactor)
        {
            float radiusScale = getSmallNoteStrengthScale(context.Radius);

            // Sliders and slider-repeats should be attenuated — they're easy to track naturally
            if (context.ModeName is @"slider" or @"slider-repeat")
            {
                float sliderAttenuation = context.ModeName == @"slider-repeat" ? 0.72f : 0.58f;
                sliderAttenuation = (float)Interpolation.Lerp(sliderAttenuation, sliderAttenuation + 0.12f, configuredStrengthFactor);
                return Math.Clamp(sliderAttenuation * radiusScale, 0.3f, 0.85f);
            }

            // Streams use full strength — handled by stream corridor logic
            if (context.ModeName != @"point")
                return Math.Clamp(1f * radiusScale, 0.4f, 1f);

            float isolatedWeight = !context.PreviewPoint.HasValue && context.PointFlowBias <= 0 ? 1 : 0;
            float singleWeight = context.PatternInfo.Kind == OsuPatternKind.Single ? 1 : 0;
            float lowDensityWeight = Math.Clamp((0.28f - context.PatternInfo.DensityWeight) / 0.28f, 0, 1);
            float lowContinuityWeight = Math.Clamp((0.42f - context.PatternInfo.ContinuityWeight) / 0.42f, 0, 1);
            float jumpSeverity = context.PatternState.JumpSeverity;
            float lowJumpWeight = 1 - Math.Clamp(jumpSeverity / 0.52f, 0, 1);
            float flowWeight = Math.Max(context.PointFlowBias, context.PatternState.SupportsFlowPath ? 0.82f : context.PatternInfo.ContinuityWeight * 0.74f);

            double delta = context.FocusTime - Time.Current;
            double leadWindow = scaleRealTimeWindow(Math.Clamp(Interpolation.Lerp(120, 190, configuredStrengthFactor), 90, 190));
            float preFocusWeight = delta <= 0 ? 0 : (float)Math.Clamp(delta / Math.Max(1, leadWindow), 0, 1);
            float timingEaseWeight = (float)Interpolation.Lerp(0.58f, 1f, preFocusWeight);

            float easyPatternWeight = Math.Max(singleWeight * 0.92f, lowDensityWeight * (0.45f + 0.55f * lowContinuityWeight));
            easyPatternWeight = Math.Max(easyPatternWeight, isolatedWeight * 0.86f);

            float attenuationWeight = easyPatternWeight * lowJumpWeight * (1 - flowWeight * 0.9f) * timingEaseWeight;
            // Moderate attenuation scaling — enough to differentiate presets, but easy patterns stay quiet
            attenuationWeight *= (float)Interpolation.Lerp(1f, 0.7f, configuredStrengthFactor);

            // Jumps should bypass attenuation — they're hard and need the help
            float jumpProtection = Math.Clamp(jumpSeverity / 0.35f, 0, 1);

            float minimumMultiplier = isolatedWeight > 0.5f
                ? (float)Interpolation.Lerp(0.46f, 0.65f, configuredStrengthFactor)
                : (float)Interpolation.Lerp(0.62f, 0.78f, configuredStrengthFactor);

            // Jumps get a higher floor — never attenuate jump assist
            if (jumpProtection > 0)
                minimumMultiplier = (float)Interpolation.Lerp(minimumMultiplier, 1f, jumpProtection * 0.85f);

            if (context.PatternInfo.Kind == OsuPatternKind.Stack)
                minimumMultiplier = Math.Max(minimumMultiplier, 0.7f);

            float baseMultiplier = Math.Clamp((float)Interpolation.Lerp(1f, minimumMultiplier, attenuationWeight), 0.4f, 1f);
            return Math.Clamp(baseMultiplier * radiusScale, 0.25f, 1f);
        }

        /// <summary>
        /// Reduces assist strength on small notes (high CS / small on-screen radius).
        /// Small notes are harder to "cheat" with large pulls and players report over-assist.
        /// </summary>
        private static float getSmallNoteStrengthScale(float radius)
        {
            if (radius <= 0)
                return 1f;

            // Reference radius ~30px roughly corresponds to normal ~CS5-6 on typical playfield scale.
            // Below this we progressively weaken the assist (down to ~30% at very small notes).
            const float refRadius = 30f;
            const float minScale = 0.30f;
            float scale = radius / refRadius;
            return Math.Clamp(scale, minScale, 1f);
        }

        private void applyJerkLimiter(double elapsed)
        {
            if (elapsed <= 0)
                return;

            // The SmoothDamp spring explicitly controls previousOffsetVelocity and provides acceleration smoothing natively.
            // Hardcoded frame-based jerk limits are no longer needed and would conflict with the spring.
            hasPreviousOffsetVelocity = true;
        }

        private void applyModeTransition(string currentModeName, double elapsed)
        {
            // Spring physics fluidly bridges mode targets, so legacy manual cross-fade state is no longer required.
        }

        private readonly struct MovementSample
        {
            public readonly double Time;
            public readonly Vector2 Velocity;

            public MovementSample(double time, Vector2 velocity)
            {
                Time = time;
                Velocity = velocity;
            }
        }

        private bool tryGetRawPosition(out Vector2 rawPosition)
        {
            rawPosition = Vector2.Zero;

            if (inputManager == null)
                return false;

            if (inputManager.ReplayBotActive)
            {
                if (!inputManager.HasReplayCursorPosition)
                    return false;

                rawPosition = inputManager.ReplayCursorPosition;
                return true;
            }

            if (!inputManager.HasOriginalUserCursorPosition)
                return false;

            rawPosition = inputManager.OriginalUserCursorPosition;
            return true;
        }

        private readonly struct TargetDescriptor
        {
            public readonly DrawableOsuHitObject Drawable;
            public readonly OsuHitObject HitObject;
            public readonly double StartTime;
            public readonly double EndTime;
            public readonly Vector2 BaseLocalPosition;
            public readonly Vector2 ScreenSpacePosition;
            public readonly float Radius;

            public TargetDescriptor(DrawableOsuHitObject drawable, OsuHitObject hitObject, double startTime, double endTime, Vector2 baseLocalPosition, Vector2 screenSpacePosition, float radius)
            {
                Drawable = drawable;
                HitObject = hitObject;
                StartTime = startTime;
                EndTime = endTime;
                BaseLocalPosition = baseLocalPosition;
                ScreenSpacePosition = screenSpacePosition;
                Radius = radius;
            }
        }

        private readonly struct AimAssistContext
        {
            public readonly string ModeName;
            public readonly DrawableOsuHitObject Drawable;
            public readonly Vector2 DesiredPoint;
            public readonly Vector2 IntentDirection;
            public readonly Vector2 Tangent;
            public readonly Vector2 DisplayPoint;
            public readonly float Radius;
            public readonly float DistanceToDesired;
            public readonly double FocusTime;
            public readonly double PreemptTime;
            public readonly bool AllowPassiveAssist;
            public readonly Vector2? PreviewPoint;
            public readonly float PreviewRadius;
            public readonly float PointFlowBias;
            public readonly OsuPatternInfo PatternInfo;
            public readonly OsuPatternState PatternState;

            public AimAssistContext(string modeName, DrawableOsuHitObject drawable, Vector2 desiredPoint, Vector2 intentDirection, Vector2 tangent, Vector2 displayPoint,
                                    float radius, float distanceToDesired, double focusTime, double preemptTime, bool allowPassiveAssist, Vector2? previewPoint, float previewRadius,
                                    float pointFlowBias, OsuPatternInfo patternInfo = default, OsuPatternState patternState = default)
            {
                ModeName = modeName;
                Drawable = drawable;
                DesiredPoint = desiredPoint;
                IntentDirection = intentDirection;
                Tangent = tangent;
                DisplayPoint = displayPoint;
                Radius = radius;
                DistanceToDesired = distanceToDesired;
                FocusTime = focusTime;
                PreemptTime = preemptTime;
                AllowPassiveAssist = allowPassiveAssist;
                PreviewPoint = previewPoint;
                PreviewRadius = previewRadius;
                PointFlowBias = pointFlowBias;
                PatternInfo = patternInfo;
                PatternState = patternState;
            }
        }

        private readonly struct ActivationState
        {
            public readonly bool Passed;
            public readonly float IntentScore;
            public readonly float EngageAmount;
            public readonly float PointEarlySettleWeight;
            public readonly float PointCaptureReleaseWeight;

            public ActivationState(bool passed, float intentScore, float engageAmount, float pointEarlySettleWeight, float pointCaptureReleaseWeight)
            {
                Passed = passed;
                IntentScore = intentScore;
                EngageAmount = engageAmount;
                PointEarlySettleWeight = pointEarlySettleWeight;
                PointCaptureReleaseWeight = pointCaptureReleaseWeight;
            }
        }

        private readonly struct ProjectionResult
        {
            public readonly Vector2 Point;
            public readonly Vector2 Tangent;

            public ProjectionResult(Vector2 point, Vector2 tangent)
            {
                Point = point;
                Tangent = tangent;
            }
        }
    }
}
