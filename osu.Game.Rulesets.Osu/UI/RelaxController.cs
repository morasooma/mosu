// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class RelaxController : Drawable
    {
        private const double plan_cleanup_padding = 160;
        private const double derived_stream_timing_window = 110;
        private const int timing_feedback_sample_limit = 18;
        private const int player_timing_sample_limit = 16;
        private const double derived_short_spinner_ignore_duration = 320;
        private const double drift_cycle_beats = 16;
        private const double min_drift_period = 3500;
        private const double max_drift_period = 9000;
        private const double minimum_uniform = 1e-6;
        private const double derived_hold_variance_ratio = 0.6;
        private const double derived_stack_variance_multiplier = 1.35;
        private const double derived_primary_reset_multiplier = 2.2;
        private const double derived_primary_reset_padding = 32;
        private const double derived_same_action_repress_ratio = 0.7;
        private const double derived_minimum_repress_gap = 18;
        private const double derived_maximum_repress_gap = 40;
        private const double derived_minimum_cursor_reaction_offset = -8;
        private const double derived_maximum_cursor_reaction_offset = 12;
        private const double derived_minimum_cursor_early_shift = 14;
        private const double derived_maximum_cursor_early_shift = 28;
        private const double derived_assist_cursor_coupling_ratio = 0.42;
        private const double derived_minimum_assist_cursor_coupling = 0.18;
        private const double derived_maximum_assist_cursor_coupling = 0.34;
        private const double derived_assist_cursor_early_shift_ratio = 0.45;
        private const double derived_minimum_assist_cursor_early_shift = 6;
        private const double derived_maximum_assist_cursor_early_shift = 14;
        private const double derived_stream_spacing_ratio_threshold = 1.85;
        private const double derived_jump_spacing_ratio_threshold = 1.45;
        private const double derived_jump_spacing_ratio_range = 1.55;
        private const double derived_minimum_jump_gameplay_lead = 6;
        private const double derived_maximum_jump_gameplay_lead = 18;
        private const double derived_minimum_slider_tail_padding = 22;
        private const double derived_maximum_slider_tail_padding = 52;
        private const float derived_sync_inner_minimum_padding = 4f;
        private const float derived_sync_inner_maximum_padding = 12f;
        private const float derived_sync_inner_minimum_radius_ratio = 0.58f;
        private const float derived_cursor_release_radius_multiplier = 0.55f;
        private const float spinner_orbit_radius_multiplier = 0.58f;
        private const float minimum_spinner_orbit_radius = 56f;
        private readonly int gameSeed = Guid.NewGuid().GetHashCode();

        private Bindable<bool> enabled = null!;
        private Bindable<double> baseOffset = null!;
        private Bindable<double> timingVariance = null!;
        private Bindable<double> dynamicDrift = null!;
        private Bindable<double> holdTime = null!;
        private Bindable<double> sliderTailOffset = null!;
        private Bindable<float> syncRadius = null!;
        private Bindable<double> maxSyncDelay = null!;
        private Bindable<double> alternateThreshold = null!;
        private Bindable<double> stableBpm = null!;
        private Bindable<double> misaltProbability = null!;
        private Bindable<bool> streamBlindMode = null!;
        private Bindable<bool> blindTapEnabled = null!;
        private Bindable<double> aimCenterBias = null!;

        private readonly Dictionary<OsuHitObject, HitPlan> plans = new Dictionary<OsuHitObject, HitPlan>();
        private readonly List<TargetDescriptor> activeTargets = new List<TargetDescriptor>();
        private readonly List<OsuPatternNode> patternNodeBuffer = new List<OsuPatternNode>(32);
        private readonly List<OsuHitObject> plansToRemove = new List<OsuHitObject>();
        private readonly List<HitPlan> sameActionPlans = new List<HitPlan>();
        private readonly List<RelaxInputEvent> inputEvents = new List<RelaxInputEvent>();
        private readonly Queue<TimingFeedbackSample> timingFeedbackSamples = new Queue<TimingFeedbackSample>();
        private readonly Queue<PlayerTimingSample> playerTimingSamples = new Queue<PlayerTimingSample>();
        private IBeatmap? beatmap;
        private AimAssistController? aimAssistController;
        private OsuInputManager? inputManager;
        private OsuPlayfield? playfield;
        private DrawableRuleset? drawableRuleset;

        private bool gameplayInputsBlocked;
        private bool alternatingBurstActive;
        private bool hasPlannedAction;
        private bool leftPressed;
        private bool rightPressed;
        private bool leftApplied;
        private bool rightApplied;
        private int leftHeldPlanCount;
        private int rightHeldPlanCount;
        private double leftNextPressAllowedTime;
        private double rightNextPressAllowedTime;
        private Vector2 lastIdleCursorPosition;
        private double lastCursorMovementTime = double.NaN;
        private double tapStamina = 1;
        private double lastStaminaSampleTime = double.NaN;
        private double liveTapStamina = 1;
        private double lastLiveStaminaSampleTime = double.NaN;
        private double smoothedLiveStreamTapWindow = double.NaN;
        private double nextStreamPressAllowedTime;
        private double adaptiveTimingCorrection;
        private double lastTimingNoise;
        private double lastStreamMissTime = double.NaN;
        private double streamMissRecoveryPenalty;
        private int consecutiveStreamMisses;
        private double playerTimingCorrection;
        private double lastTimingFeedbackSampleTime = double.NaN;
        private double lastPlayerTimingSampleTime = double.NaN;
        private double lastPlannedActionTargetTime = double.NaN;
        private int planSequence;
        private int streamBurstNoteCount;
        private OsuAction lastPlannedAction;
        private OsuAction primaryAction = OsuAction.LeftButton;

        public bool IsEnabled => getConfiguredEnabled();

        public bool IsDeclaredMod => mosuRelaxMod != null;

        public long GeneratedPressCount => inputEvents.Count(input => input.IsPress);

        public long GeneratedReleaseCount => inputEvents.Count(input => !input.IsPress);

        public string CurrentModeName { get; private set; } = @"idle";

        public double? CurrentPlannedPressTime { get; private set; }

        public double? CurrentPlannedReleaseTime { get; private set; }

        public double CurrentAppliedTimingOffset { get; private set; }

        public double CurrentPendingSyncDelay { get; private set; }

        public double CurrentTimingVarianceScale { get; private set; } = 1;

        public OsuAction? LastPressAction { get; private set; }

        public double LastPressTime { get; private set; } = double.NaN;

        public double LastReleaseTime { get; private set; } = double.NaN;

        public double CurrentTapStamina { get; private set; } = 1;

        public double CurrentAdaptiveTimingCorrection { get; private set; }

        public double CurrentPlayerTimingCorrection { get; private set; }

        public double CurrentStreamFatigueOffset { get; private set; }

        public Vector2? CurrentImpactPointPosition { get; private set; }

        public IReadOnlyList<RelaxInputEvent> InputEvents => inputEvents;

        public bool IsTargetAwaitingPress(OsuHitObject hitObject)
            => getConfiguredEnabled()
               && plans.TryGetValue(hitObject, out HitPlan? plan)
               && !plan.Pressed
               && !plan.Released;

        public bool TryGetTargetScheduledPressTime(OsuHitObject hitObject, out double pressTime)
        {
            if (getConfiguredEnabled()
                && plans.TryGetValue(hitObject, out HitPlan? plan)
                && !plan.Pressed
                && !plan.Released)
            {
                pressTime = plan.GetScheduledPressTime();
                return true;
            }

            pressTime = 0;
            return false;
        }

        public bool TryGetTargetLinkedPressTime(OsuHitObject hitObject, out double pressTime)
        {
            if (getConfiguredEnabled()
                && plans.TryGetValue(hitObject, out HitPlan? plan)
                && !plan.Pressed
                && !plan.Released)
            {
                pressTime = getLinkedPressTime(plan);
                return true;
            }

            pressTime = 0;
            return false;
        }


        private osu.Game.Rulesets.Osu.Mods.OsuModMosuRelax? mosuRelaxMod;

        private void cacheMosuRelaxMod()
        {
            if (drawableRuleset == null)
                return;

            mosuRelaxMod ??= drawableRuleset.Mods.OfType<osu.Game.Rulesets.Osu.Mods.OsuModMosuRelax>().FirstOrDefault();
        }

        private bool getConfiguredEnabled() => mosuRelaxMod != null || enabled.Value;
        private double getConfiguredBaseOffset() => mosuRelaxMod?.BaseOffset.Value ?? baseOffset.Value;
        private double getConfiguredTimingVariance() => mosuRelaxMod?.TimingVariance.Value ?? timingVariance.Value;
        private double getConfiguredDynamicDrift() => mosuRelaxMod?.DynamicDrift.Value ?? dynamicDrift.Value;
        private double getConfiguredHoldTime() => mosuRelaxMod?.HoldTime.Value ?? holdTime.Value;
        private double getConfiguredSliderTailOffset() => mosuRelaxMod?.SliderTailOffset.Value ?? sliderTailOffset.Value;
        private float getConfiguredSyncRadius() => mosuRelaxMod?.SyncRadius.Value ?? syncRadius.Value;
        private double getConfiguredMaxSyncDelay() => mosuRelaxMod?.MaxSyncDelay.Value ?? maxSyncDelay.Value;
        private double getConfiguredAlternateThreshold() => mosuRelaxMod?.AlternateThreshold.Value ?? alternateThreshold.Value;
        private double getConfiguredStableBpm() => mosuRelaxMod?.StableBpm.Value ?? stableBpm.Value;
        private double getConfiguredMisaltProbability() => mosuRelaxMod?.MisaltProbability.Value ?? misaltProbability.Value;
        private bool getConfiguredStreamBlindMode() => mosuRelaxMod?.StreamBlindMode.Value ?? streamBlindMode.Value;
        private bool getConfiguredBlindTapEnabled() => mosuRelaxMod?.BlindTapEnabled.Value ?? blindTapEnabled.Value;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IBeatmap? beatmap)
        {
            this.beatmap = beatmap;
            enabled = config.GetBindable<bool>(OsuSetting.ForkRelaxEnabled);
            baseOffset = config.GetBindable<double>(OsuSetting.ForkRelaxBaseOffset);
            timingVariance = config.GetBindable<double>(OsuSetting.ForkRelaxTimingVariance);
            dynamicDrift = config.GetBindable<double>(OsuSetting.ForkRelaxDynamicDrift);
            holdTime = config.GetBindable<double>(OsuSetting.ForkRelaxHoldTime);
            sliderTailOffset = config.GetBindable<double>(OsuSetting.ForkRelaxSliderTailOffset);
            syncRadius = config.GetBindable<float>(OsuSetting.ForkRelaxSyncRadius);
            maxSyncDelay = config.GetBindable<double>(OsuSetting.ForkRelaxMaxSyncDelay);
            alternateThreshold = config.GetBindable<double>(OsuSetting.ForkRelaxAlternateThreshold);
            stableBpm = config.GetBindable<double>(OsuSetting.ForkRelaxStableBpm);
            misaltProbability = config.GetBindable<double>(OsuSetting.ForkRelaxMisaltProbability);
            streamBlindMode = config.GetBindable<bool>(OsuSetting.ForkRelaxStreamBlindMode);
            blindTapEnabled = config.GetBindable<bool>(OsuSetting.ForkRelaxBlindTapEnabled);
            aimCenterBias = config.GetBindable<double>(OsuSetting.ForkAimAssistCenterBias);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager() as OsuInputManager;
            playfield = this.FindClosestParent<OsuPlayfield>();
            aimAssistController = playfield?.AimAssistController;
            drawableRuleset = this.FindClosestParent<DrawableRuleset>();
        }

        protected override void Update()
        {
            base.Update();

            inputManager ??= GetContainingInputManager() as OsuInputManager;
            playfield ??= this.FindClosestParent<OsuPlayfield>();
            aimAssistController ??= playfield?.AimAssistController;
            drawableRuleset ??= this.FindClosestParent<DrawableRuleset>();

            cacheMosuRelaxMod();

            if (inputManager == null || playfield == null)
                return;

            Vector2 rawCursorPosition = getRawCursorPosition();

            if (double.IsNaN(lastCursorMovementTime) || (rawCursorPosition - lastIdleCursorPosition).LengthSquared > 4f)
            {
                lastIdleCursorPosition = rawCursorPosition;
                lastCursorMovementTime = Time.Current;
            }

            if (!shouldRun())
            {
                clearState();
                return;
            }

            ensureGameplayInputsBlocked();
            updateDynamicFatigueState();
            decayTimingFeedbackAfterBreak();
            collectTargets();
            syncPlansWithTargets();
            cleanupPlans();
            processDueReleases();
            processDuePresses();
            updateCursorAutomation();
            updateDiagnostics();
        }

        public void ClearInputEvents()
        {
            inputEvents.Clear();
            LastPressAction = null;
            LastPressTime = double.NaN;
            LastReleaseTime = double.NaN;
        }

        private bool shouldRun()
        {
            if (!getConfiguredEnabled())
                return false;

            bool replayBotActive = inputManager?.ReplayBotActive == true;

            if (inputManager?.ReplayInputHandler != null)
                return false;

            if (!replayBotActive && !double.IsNaN(lastCursorMovementTime) && Time.Current - lastCursorMovementTime > 1500)
                return false;

            if (drawableRuleset == null)
                return true;

            foreach (Mod mod in drawableRuleset.Mods)
            {
                if (mod is ModRelax || mod is ModAutoplay)
                    return false;
            }

            return true;
        }

        private void ensureGameplayInputsBlocked()
        {
            if (gameplayInputsBlocked)
                return;

            inputManager!.AllowGameplayInputs = false;
            gameplayInputsBlocked = true;
        }

        private void clearState()
        {
            plans.Clear();
            activeTargets.Clear();
            plansToRemove.Clear();
            sameActionPlans.Clear();
            CurrentModeName = @"idle";
            CurrentPlannedPressTime = null;
            CurrentPlannedReleaseTime = null;
            CurrentAppliedTimingOffset = 0;
            CurrentPendingSyncDelay = 0;
            CurrentTimingVarianceScale = 1;
            CurrentImpactPointPosition = null;
            alternatingBurstActive = false;
            hasPlannedAction = false;
            planSequence = 0;
            primaryAction = OsuAction.LeftButton;
            leftHeldPlanCount = 0;
            rightHeldPlanCount = 0;
            leftNextPressAllowedTime = 0;
            rightNextPressAllowedTime = 0;
            tapStamina = 1;
            lastStaminaSampleTime = double.NaN;
            liveTapStamina = 1;
            lastLiveStaminaSampleTime = double.NaN;
            smoothedLiveStreamTapWindow = double.NaN;
            nextStreamPressAllowedTime = 0;
            streamBurstNoteCount = 0;
            lastStreamMissTime = double.NaN;
            streamMissRecoveryPenalty = 0;
            consecutiveStreamMisses = 0;
            lastPlannedActionTargetTime = double.NaN;
            CurrentTapStamina = 1;
            CurrentStreamFatigueOffset = 0;
            lastTimingNoise = 0;
            clearTimingFeedbackState();
            clearPlayerTimingState();
            releaseAllActions();
            leftApplied = false;
            rightApplied = false;

            if (!gameplayInputsBlocked)
                return;

            inputManager!.AllowGameplayInputs = true;
            gameplayInputsBlocked = false;
        }

        private void clearTimingFeedbackState()
        {
            timingFeedbackSamples.Clear();
            adaptiveTimingCorrection = 0;
            lastTimingFeedbackSampleTime = double.NaN;
            CurrentAdaptiveTimingCorrection = 0;
        }

        private void clearPlayerTimingState()
        {
            playerTimingSamples.Clear();
            playerTimingCorrection = 0;
            lastPlayerTimingSampleTime = double.NaN;
            CurrentPlayerTimingCorrection = 0;
        }

        private void updateDynamicFatigueState()
        {
            recoverLiveTapStamina(Time.Current);

            if (double.IsNaN(smoothedLiveStreamTapWindow))
                smoothedLiveStreamTapWindow = getLiveStreamTapWindow();
            else
            {
                double targetWindow = getLiveStreamTapWindow();
                double smoothing = 0.08 + (1 - liveTapStamina) * 0.1;

                smoothedLiveStreamTapWindow += (targetWindow - smoothedLiveStreamTapWindow) * smoothing;
            }

            applyTimingFeedbackOffsetToPendingPlans();
        }

        private void decayTimingFeedbackAfterBreak()
        {
            bool reset = false;
            double resetGap = getDerivedTimingFeedbackResetGap();

            if (timingFeedbackSamples.Count > 0
                && !double.IsNaN(lastTimingFeedbackSampleTime)
                && Time.Current - lastTimingFeedbackSampleTime > resetGap)
            {
                clearTimingFeedbackState();
                reset = true;
            }

            if (playerTimingSamples.Count > 0
                && !double.IsNaN(lastPlayerTimingSampleTime)
                && Time.Current - lastPlayerTimingSampleTime > resetGap)
            {
                clearPlayerTimingState();
                reset = true;
            }

            if (reset)
                applyTimingFeedbackOffsetToPendingPlans();
        }

        private void collectTargets()
        {
            activeTargets.Clear();
            TargetDescriptor previous = default;
            bool hasPrevious = false;
            bool requiresSort = false;

            foreach (Drawable drawable in playfield!.HitObjectContainer.Objects)
            {
                if (drawable is not DrawableOsuHitObject osuDrawable)
                    continue;

                if (osuDrawable is DrawableSliderHead || osuDrawable is DrawableSliderTail || osuDrawable is DrawableSliderTick || osuDrawable is DrawableSliderRepeat)
                    continue;

                if (osuDrawable.AllJudged)
                    continue;

                TargetDescriptor target = createTargetDescriptor(osuDrawable);

                if (hasPrevious && compareTargets(previous, target) > 0)
                    requiresSort = true;

                activeTargets.Add(target);
                previous = target;
                hasPrevious = true;
            }

            if (requiresSort && activeTargets.Count > 1)
                activeTargets.Sort(compareTargets);
        }

        private static int compareTargets(TargetDescriptor first, TargetDescriptor second)
        {
            int result = first.StartTime.CompareTo(second.StartTime);

            if (result != 0)
                return result;

            result = first.EndTime.CompareTo(second.EndTime);

            if (result != 0)
                return result;

            result = first.BaseLocalPosition.X.CompareTo(second.BaseLocalPosition.X);

            if (result != 0)
                return result;

            return first.BaseLocalPosition.Y.CompareTo(second.BaseLocalPosition.Y);
        }

        private TargetDescriptor createTargetDescriptor(DrawableOsuHitObject drawable)
        {
            OsuHitObject hitObject = drawable.HitObject;
            double endTime = hitObject is IHasDuration hasDuration ? hasDuration.EndTime : hitObject.StartTime;

            return new TargetDescriptor(drawable, hitObject, hitObject.StartTime, endTime, hitObject.Position, getTargetScreenSpacePosition(drawable), getDrawableTargetRadius(drawable, hitObject));
        }

        private void syncPlansWithTargets()
        {
            foreach (HitPlan plan in plans.Values)
                plan.BeginFrame();

            OsuPatternState[] patternStates = analyzePatternStates(activeTargets);

            for (int i = 0; i < activeTargets.Count; i++)
            {
                TargetDescriptor target = activeTargets[i];

                if (!plans.TryGetValue(target.HitObject, out HitPlan? plan))
                {
                    TargetDescriptor? previous = i > 0 ? activeTargets[i - 1] : null;
                    TargetDescriptor? next = i + 1 < activeTargets.Count ? activeTargets[i + 1] : null;
                    plan = createPlan(target, previous, next, activeTargets, patternStates, i);
                    plans.Add(target.HitObject, plan);
                }

                plan.UpdateTarget(target);
            }

            enforcePendingCadenceAlternation(patternStates);
        }

        private void enforcePendingCadenceAlternation(IReadOnlyList<OsuPatternState> patternStates)
        {
            if (activeTargets.Count < 2)
                return;

            OsuAction? previousAction = null;
            double previousTargetTime = double.NaN;

            for (int i = 0; i < activeTargets.Count; i++)
            {
                TargetDescriptor target = activeTargets[i];

                if (!plans.TryGetValue(target.HitObject, out HitPlan? plan))
                    continue;

                TargetDescriptor? previous = i > 0 ? activeTargets[i - 1] : null;
                TargetDescriptor? next = i + 1 < activeTargets.Count ? activeTargets[i + 1] : null;

                if (!plan.Released && previousAction.HasValue && previous?.HitObject != null)
                {
                    OsuPatternState patternState = i < patternStates.Count ? patternStates[i] : default;
                    OsuPatternState nextPatternState = next?.HitObject != null && i + 1 < patternStates.Count
                        ? patternStates[i + 1]
                        : default;

                    double gap = target.StartTime - previous.Value.StartTime;
                    double effectiveGap = getEffectiveTappingGap(previous.Value, target);
                    double nextEffectiveGap = next?.HitObject != null ? getEffectiveTappingGap(target, next.Value) : double.PositiveInfinity;
                    bool currentIsStream = patternState.IsCommittedStream;
                    double currentJumpSeverity = getJumpTappingSeverity(previous.Value, target);
                    bool singletapFriendlyJump = !currentIsStream && isSingletapFriendlyJump(gap, currentJumpSeverity);
                    bool currentIsStackBurst = isFastStackBurst(target, previous, next, gap);
                    bool nextIsStackBurst = next?.HitObject != null
                                            && isFastStackBurst(next.Value, target, null, next.Value.StartTime - target.StartTime);
                    bool currentSupportsThresholdAlternation = patternState.IsCommittedStream
                                                               || isBurstAlternationPattern(patternState)
                                                               || currentIsStackBurst
                                                               || supportsThresholdAlternation(patternState.PatternInfo);
                    bool nextSupportsThresholdAlternation = nextPatternState.IsCommittedStream
                                                            || isBurstAlternationPattern(nextPatternState)
                                                            || nextIsStackBurst
                                                            || supportsThresholdAlternation(nextPatternState.PatternInfo);

                    if (!plan.Pressed
                        && plan.Action == previousAction.Value
                        && ShouldForceAlternationBySameFingerCadenceCap(
                            defaultWouldRepeatSameAction: true,
                            currentSupportsThresholdAlternation,
                            nextSupportsThresholdAlternation,
                            singletapFriendlyJump,
                            target.StartTime - previousTargetTime,
                            effectiveGap,
                            nextEffectiveGap,
                            getSameFingerCadenceWindow()))
                    {
                        plan.Action = getOppositeAction(previousAction.Value);
                    }
                }

                if (!plan.Released)
                {
                    previousAction = plan.Action;
                    previousTargetTime = target.StartTime;
                }
            }
        }

        private void cleanupPlans()
        {
            plansToRemove.Clear();
            double currentTime = Time.Current;
            double syncDelayLimit = Math.Max(0, getConfiguredMaxSyncDelay());

            foreach ((OsuHitObject hitObject, HitPlan plan) in plans)
            {
                if (plan.Released)
                {
                    if (!plan.SeenThisFrame && currentTime > plan.GetReleaseTime() + plan_cleanup_padding)
                        plansToRemove.Add(hitObject);

                    continue;
                }

                if (plan.Pressed || plan.SeenThisFrame)
                    continue;

                if (currentTime > plan.GetPressDeadline(syncDelayLimit) + plan_cleanup_padding)
                    plansToRemove.Add(hitObject);
            }

            for (int i = 0; i < plansToRemove.Count; i++)
                plans.Remove(plansToRemove[i]);
        }

        private HitPlan createPlan(TargetDescriptor target, TargetDescriptor? previous, TargetDescriptor? next, IReadOnlyList<TargetDescriptor> allTargets,
                                   IReadOnlyList<OsuPatternState> patternStates, int currentIndex)
        {
            OsuPatternState patternState = currentIndex >= 0 && currentIndex < patternStates.Count
                ? patternStates[currentIndex]
                : default;
            OsuPatternState nextPatternState = next?.HitObject != null && currentIndex + 1 < patternStates.Count
                ? patternStates[currentIndex + 1]
                : default;
            OsuPatternInfo patternInfo = patternState.PatternInfo;
            int seed = HashCode.Combine(
                gameSeed,
                target.HitObject.GetHashCode(),
                (int)Math.Round(target.StartTime),
                (int)Math.Round(target.BaseLocalPosition.X),
                (int)Math.Round(target.BaseLocalPosition.Y));
            bool isStacked = isStackedTarget(target, previous, next);
            bool isSlider = target.Drawable is DrawableSlider;
            bool isSpinner = target.Drawable is DrawableSpinner;
            bool tapOnlySlider = isSlider && overlapsAnySlider(target, previous, next);
            bool suppressSpinner = isSpinner && shouldSuppressSpinner(target, previous, next);
            bool keepHeldSpinner = isSpinner && next?.Drawable is DrawableSpinner;
            bool isStream = patternState.IsCommittedStream;
            double jumpSeverity = getJumpSeverity(target, previous, next, isStream, isSpinner);
            bool hadPreviousAction = hasPlannedAction;
            OsuAction previousAction = lastPlannedAction;
            OsuAction action = chooseAction(target, seed, previous, next, patternState, nextPatternState);
            bool isSingleTapJump = !isStream
                                   && jumpSeverity > 0.12
                                   && previous?.HitObject != null
                                   && hadPreviousAction
                                   && action == previousAction;

            double varianceScale = 1;

            if (isStacked)
                varianceScale *= derived_stack_variance_multiplier;

            if (isSingleTapJump)
                varianceScale *= getDerivedJumpSingleTapVarianceMultiplier(jumpSeverity);

            double variance = getConfiguredTimingVariance() * varianceScale;

            double rawNoise = nextGaussian(seed, 17) * variance;
            double noise = rawNoise * 0.45 + lastTimingNoise * 0.55;
            lastTimingNoise = noise;

            double appliedOffset = getConfiguredBaseOffset()
                                   + getDynamicDriftOffset(target.StartTime)
                                   + noise;

            double plannedPressTime = target.StartTime + appliedOffset;
            double cursorReactionOffset = getDerivedCursorReactionOffset(seed) + nextGaussian(seed, 42) * 6.0;

            double baseHold = getConfiguredHoldTime() + nextGaussian(seed, 31) * getDerivedHoldVariance();
            double gapToNext = next?.HitObject != null ? next.Value.StartTime - target.StartTime : 500;
            if (gapToNext < baseHold + 15)
                baseHold = Math.Min(baseHold, Math.Max(12, gapToNext * 0.7));
            if (jumpSeverity > 0.05)
                baseHold *= Math.Clamp(1.0 - jumpSeverity * 0.4, 0.4, 1.0);

            double holdDuration = Math.Max(0, scaleRealTimeWindow(baseHold));
            
            double sliderTailVariance = variance * Math.Clamp(1.5 + jumpSeverity * 2.5, 1.5, 4.5);
            
            // Human-like slider tail dropping:
            // When the gap between a slider's end and the next note is short, and the next note is a severe jump,
            // players naturally "rush" and release the slider early to aim the jump, occasionally dropping the tail.
            double tailRushPenalty = 0;
            if (isSlider && next?.HitObject != null)
            {
                double sliderGapToNext = next.Value.StartTime - target.EndTime;
                if (sliderGapToNext < 180)
                {
                    double rushProgress = 1.0 - Math.Clamp(sliderGapToNext / 180.0, 0, 1);
                    // Rushing causes up to 60-80ms early release depending on jump severity
                    tailRushPenalty = rushProgress * (30.0 + jumpSeverity * 50.0);
                }
            }

            double fixedReleaseTime = isSlider
                ? tapOnlySlider
                    ? double.NaN
                    : target.EndTime + getEffectiveSliderTailOffset() - tailRushPenalty + nextGaussian(seed, 67) * sliderTailVariance
                : isSpinner
                    ? keepHeldSpinner
                        ? next!.Value.EndTime
                        : target.EndTime + nextGaussian(seed, 88) * variance
                    : double.NaN;

            return new HitPlan(
                planSequence++,
                target,
                previous,
                next,
                action,
                appliedOffset,
                plannedPressTime,
                cursorReactionOffset,
                holdDuration,
                fixedReleaseTime,
                isSlider,
                isSpinner,
                isSingleTapJump,
                tapOnlySlider,
                suppressSpinner,
                keepHeldSpinner,
                isStacked,
                isStream,
                jumpSeverity,
                tapOnlySlider
                    ? @"slider-tap"
                    : isSingleTapJump
                        ? @"jump-singletap"
                        : isSlider
                            ? @"slider"
                            : isSpinner
                                ? @"spinner"
                                : getModeName(patternState),
                seed,
                varianceScale,
                getAdaptiveTimingFeedbackOffset(isSlider, isStream, jumpSeverity),
                patternInfo,
                patternState);
        }

        private bool overlapsAnySlider(TargetDescriptor target, TargetDescriptor? previous, TargetDescriptor? next)
            => overlapsSlider(target, previous) || overlapsSlider(target, next);

        private bool overlapsSlider(TargetDescriptor target, TargetDescriptor? other)
            => other?.HitObject != null
               && other.Value.Drawable is DrawableSlider
               && targetsOverlap(target, other.Value);

        private bool shouldSuppressSpinner(TargetDescriptor target, TargetDescriptor? previous, TargetDescriptor? next)
        {
            if (target.Drawable is not DrawableSpinner)
                return false;

            double realDuration = toRealTimeWindow(target.EndTime - target.StartTime);

            if (realDuration > derived_short_spinner_ignore_duration)
                return false;

            return overlapsSlider(target, previous) || overlapsSlider(target, next);
        }

        private static bool targetsOverlap(TargetDescriptor first, TargetDescriptor second)
            => first.StartTime < second.EndTime && second.StartTime < first.EndTime;

        private double getTimingFeedbackOffset(HitPlan plan)
            => getAdaptiveTimingFeedbackOffset(plan.IsSlider && !plan.IsTapOnlySlider, plan.IsStream, plan.JumpSeverity)
               + getPlayerTimingOffset(plan)
               + getStreamFatigueTimingOffset(plan);

        private double getAdaptiveTimingFeedbackOffset(bool isSlider, bool isStream, double jumpSeverity)
        {
            double scale = 1;

            if (isStream)
                scale *= 0.72;
            else if (isSlider)
                scale *= 0.88;

            if (jumpSeverity > 0.01)
                scale *= 0.92;

            return adaptiveTimingCorrection * scale;
        }

        private double getPlayerTimingOffset(HitPlan plan)
        {
            if (playerTimingSamples.Count < 2 || Math.Abs(playerTimingCorrection) < 0.05 || plan.IsSpinner)
                return 0;

            double scale;

            if (plan.IsSlider && !plan.IsTapOnlySlider)
                scale = 0.52;
            else if (plan.IsSingleTapJump)
                scale = 1.12;
            else if (plan.IsJump)
                scale = 0.94 + plan.JumpSeverity * 0.22;
            else if (plan.IsStream)
                scale = 0.72;
            else
                scale = 0.82;

            return playerTimingCorrection * scale;
        }

        private double getStreamFatigueTimingOffset(HitPlan plan)
        {
            if (!plan.IsStream || plan.Previous?.HitObject == null)
            {
                CurrentStreamFatigueOffset = 0;
                return 0;
            }

            double effectiveGap = getEffectiveTappingGap(plan.Previous.Value, plan);
            double cadenceWindow = double.IsNaN(smoothedLiveStreamTapWindow)
                ? getLiveStreamTapWindow()
                : smoothedLiveStreamTapWindow;

            if (cadenceWindow <= effectiveGap)
            {
                CurrentStreamFatigueOffset = 0;
                return 0;
            }

            double overslow = Math.Clamp((cadenceWindow - effectiveGap) / Math.Max(1, effectiveGap * 0.5), 0, 1);
            double depletion = Math.Clamp((0.54 - liveTapStamina) / 0.54, 0, 1);
            double strain = Math.Clamp((getStableTapWindow() - effectiveGap) / Math.Max(1, getStableTapWindow() * 0.55), 0, 1);
            double cap = scaleRealTimeWindow(Math.Clamp(4 + strain * 9 + getConfiguredTimingVariance() * 0.14, 4, 13));
            double result = -cap * overslow * Math.Pow(depletion, 0.78);

            CurrentStreamFatigueOffset = result;
            return result;
        }

        private void registerTimingFeedbackSample(HitPlan plan)
        {
            if (!plan.ActualPressTime.HasValue || plan.IsSpinner)
                return;

            double sampleTime = plan.ActualPressTime.Value;

            if (!double.IsNaN(lastTimingFeedbackSampleTime)
                && sampleTime - lastTimingFeedbackSampleTime > getDerivedTimingFeedbackResetGap())
            {
                timingFeedbackSamples.Clear();
                adaptiveTimingCorrection *= 0.35;
            }

            lastTimingFeedbackSampleTime = sampleTime;

            timingFeedbackSamples.Enqueue(new TimingFeedbackSample(sampleTime, plan.ActualPressTime.Value - plan.GetFeedbackReferencePressTime()));
            pruneTimingFeedbackSamples(sampleTime);
            recalculateTimingFeedbackCorrection(sampleTime);
            applyTimingFeedbackOffsetToPendingPlans();
        }

        private void registerPlayerTimingSample(HitPlan plan, double sampleTime)
        {
            if (plan.IsSpinner || plan.PlayerTimingSampleRegistered)
                return;

            double earlyCap = getDerivedPlayerTimingEarlySampleCap(plan);
            double lateCap = getDerivedPlayerTimingLateSampleCap(plan);
            double residual = Math.Clamp(sampleTime - plan.GetPlayerTimingReferenceTime(), -earlyCap, lateCap);
            double weightScale = getPlayerTimingSampleWeight(plan);

            plan.PlayerTimingSampleRegistered = true;

            if (weightScale <= 0)
                return;

            if (!double.IsNaN(lastPlayerTimingSampleTime)
                && sampleTime - lastPlayerTimingSampleTime > getDerivedTimingFeedbackResetGap())
            {
                playerTimingSamples.Clear();
                playerTimingCorrection *= 0.35;
            }

            if (playerTimingCorrection != 0)
            {
                if (playerTimingCorrection < 0 && residual > playerTimingCorrection
                    || playerTimingCorrection > 0 && residual < playerTimingCorrection)
                {
                    playerTimingCorrection += (residual - playerTimingCorrection) * 0.5;
                }

                bool oppositeDirection = Math.Sign(residual) != 0 && Math.Sign(residual) != Math.Sign(playerTimingCorrection);
                bool softerInput = Math.Abs(residual) < Math.Abs(playerTimingCorrection) * 0.75;

                if (oppositeDirection)
                    playerTimingCorrection *= 0.52;
                else if (softerInput)
                    playerTimingCorrection *= 0.72;
            }

            lastPlayerTimingSampleTime = sampleTime;
            playerTimingSamples.Enqueue(new PlayerTimingSample(sampleTime, residual, weightScale));
            prunePlayerTimingSamples(sampleTime);
            recalculatePlayerTimingCorrection(sampleTime);
            applyTimingFeedbackOffsetToPendingPlans();
        }

        private double getPlayerTimingSampleWeight(HitPlan plan)
        {
            if (plan.IsSpinner)
                return 0;

            if (plan.IsSingleTapJump)
                return 1.3 + plan.JumpSeverity * 0.35;

            if (plan.IsJump)
                return 1.08 + plan.JumpSeverity * 0.32;

            if (plan.IsStream)
            {
                double weight = 0.88;

                if (plan.PatternInfo.StreamShape == OsuStreamShapeKind.ZigZag)
                    weight *= 0.9;

                if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Spaced)
                    weight *= 0.88;
                else if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Variable)
                    weight *= 0.92;

                return weight;
            }

            if (plan.IsSlider && !plan.IsTapOnlySlider)
                return 0.58;

            return 0.76;
        }

        private void prunePlayerTimingSamples(double currentTime)
        {
            double feedbackWindow = getDerivedPlayerTimingWindow();

            while (playerTimingSamples.Count > 0)
            {
                if (playerTimingSamples.Count <= player_timing_sample_limit
                    && currentTime - playerTimingSamples.Peek().Time <= feedbackWindow)
                    break;

                playerTimingSamples.Dequeue();
            }
        }

        private void recalculatePlayerTimingCorrection(double currentTime)
        {
            if (playerTimingSamples.Count == 0)
            {
                playerTimingCorrection = 0;
                CurrentPlayerTimingCorrection = 0;
                return;
            }

            double feedbackWindow = getDerivedPlayerTimingWindow();
            double tailThreshold = getDerivedPlayerTimingTailThreshold();
            double weightedResidualSum = 0;
            double residualWeight = 0;
            double weightedLateTailSum = 0;
            double lateTailWeight = 0;
            double weightedEarlyTailSum = 0;
            double earlyTailWeight = 0;

            foreach (PlayerTimingSample sample in playerTimingSamples)
            {
                double age = Math.Max(0, currentTime - sample.Time);
                double ageWeight = Math.Clamp(1 - age / feedbackWindow, 0.35, 1);
                double weight = ageWeight * sample.WeightScale;
                double residual = sample.Residual;

                weightedResidualSum += residual * weight;
                residualWeight += weight;

                double lateTail = residual - tailThreshold;

                if (lateTail > 0)
                {
                    weightedLateTailSum += lateTail * weight;
                    lateTailWeight += weight;
                }

                double earlyTail = -residual - tailThreshold;

                if (earlyTail > 0)
                {
                    weightedEarlyTailSum += earlyTail * weight;
                    earlyTailWeight += weight;
                }
            }

            double meanResidual = residualWeight > 0 ? weightedResidualSum / residualWeight : 0;
            double lateTailBias = lateTailWeight > 0 ? weightedLateTailSum / lateTailWeight : 0;
            double earlyTailBias = earlyTailWeight > 0 ? weightedEarlyTailSum / earlyTailWeight : 0;
            double sampleStrength = Math.Clamp(0.42 + (playerTimingSamples.Count - 1) / 3.0, 0.42, 1);
            double targetCorrection = (meanResidual * 0.98 + (lateTailBias - earlyTailBias) * 0.42) * sampleStrength;
            double correctionCap = getDerivedPlayerTimingCorrectionCap();
            double smoothing = 0.34 + 0.24 * sampleStrength;

            targetCorrection = Math.Clamp(targetCorrection, -correctionCap, correctionCap);
            playerTimingCorrection += (targetCorrection - playerTimingCorrection) * smoothing;

            if (Math.Abs(playerTimingCorrection) < 0.05)
                playerTimingCorrection = 0;

            CurrentPlayerTimingCorrection = playerTimingCorrection;
        }

        private void pruneTimingFeedbackSamples(double currentTime)
        {
            double feedbackWindow = getDerivedTimingFeedbackWindow();

            while (timingFeedbackSamples.Count > 0)
            {
                if (timingFeedbackSamples.Count <= timing_feedback_sample_limit
                    && currentTime - timingFeedbackSamples.Peek().Time <= feedbackWindow)
                    break;

                timingFeedbackSamples.Dequeue();
            }
        }

        private void recalculateTimingFeedbackCorrection(double currentTime)
        {
            if (timingFeedbackSamples.Count == 0)
            {
                adaptiveTimingCorrection = 0;
                CurrentAdaptiveTimingCorrection = 0;
                return;
            }

            double feedbackWindow = getDerivedTimingFeedbackWindow();
            double tailThreshold = getDerivedTimingFeedbackTailThreshold();
            double weightedResidualSum = 0;
            double residualWeight = 0;
            double weightedLateTailSum = 0;
            double lateTailWeight = 0;
            double weightedEarlyTailSum = 0;
            double earlyTailWeight = 0;

            foreach (TimingFeedbackSample sample in timingFeedbackSamples)
            {
                double age = Math.Max(0, currentTime - sample.Time);
                double weight = Math.Clamp(1 - age / feedbackWindow, 0.25, 1);
                double residual = sample.Residual;

                weightedResidualSum += residual * weight;
                residualWeight += weight;

                double lateTail = residual - tailThreshold;

                if (lateTail > 0)
                {
                    weightedLateTailSum += lateTail * weight;
                    lateTailWeight += weight;
                }

                double earlyTail = -residual - tailThreshold;

                if (earlyTail > 0)
                {
                    weightedEarlyTailSum += earlyTail * weight;
                    earlyTailWeight += weight;
                }
            }

            double meanResidual = residualWeight > 0 ? weightedResidualSum / residualWeight : 0;
            double lateTailBias = lateTailWeight > 0 ? weightedLateTailSum / lateTailWeight : 0;
            double earlyTailBias = earlyTailWeight > 0 ? weightedEarlyTailSum / earlyTailWeight : 0;
            double sampleStrength = Math.Clamp((timingFeedbackSamples.Count - 1) / 4.0, 0, 1);
            double targetCorrection = -(meanResidual * 0.72 + (lateTailBias - earlyTailBias) * 0.48) * sampleStrength;
            double correctionCap = getDerivedTimingFeedbackCorrectionCap();
            double smoothing = 0.22 + 0.24 * sampleStrength;

            targetCorrection = Math.Clamp(targetCorrection, -correctionCap, correctionCap);
            adaptiveTimingCorrection += (targetCorrection - adaptiveTimingCorrection) * smoothing;

            if (Math.Abs(adaptiveTimingCorrection) < 0.05)
                adaptiveTimingCorrection = 0;

            CurrentAdaptiveTimingCorrection = adaptiveTimingCorrection;
        }

        private void applyTimingFeedbackOffsetToPendingPlans()
        {
            foreach (HitPlan plan in plans.Values)
            {
                if (plan.Pressed || plan.Released)
                    continue;

                plan.TimingFeedbackOffset = getTimingFeedbackOffset(plan);
            }
        }

        private OsuAction chooseAction(TargetDescriptor target, int seed, TargetDescriptor? previous, TargetDescriptor? next, OsuPatternState patternState, OsuPatternState nextPatternState)
        {
            OsuPatternInfo patternInfo = patternState.PatternInfo;
            OsuPatternInfo nextPatternInfo = nextPatternState.PatternInfo;
            recoverTapStamina(target.StartTime);
            double gap = previous?.HitObject != null ? target.StartTime - previous.Value.StartTime : double.PositiveInfinity;
            bool continuesStream = previous?.HitObject != null && isStreamConnection(previous.Value, target);
            bool currentIsStackBurst = isFastStackBurst(target, previous, next, gap);
            bool nextIsStackBurst = next?.HitObject != null
                                    && isFastStackBurst(next.Value, target, null, next.Value.StartTime - target.StartTime);
            bool currentIsFlowBurst = isBurstAlternationPattern(patternState);
            bool nextIsFlowBurst = isBurstAlternationPattern(nextPatternState);
            bool currentIsBurstLike = patternState.IsCommittedStream || currentIsFlowBurst || currentIsStackBurst;
            bool nextIsBurstLike = nextPatternState.IsCommittedStream || nextIsFlowBurst || nextIsStackBurst;
            bool continuesBurstLike = continuesStream
                                      || previous?.HitObject != null && positionsMatch(previous.Value.BaseLocalPosition, target.BaseLocalPosition);
            bool currentIsStream = patternState.IsCommittedStream;
            bool nextIsStream = next?.HitObject != null && nextPatternState.IsCommittedStream;
            double currentJumpSeverity = previous?.HitObject != null ? getJumpTappingSeverity(previous.Value, target) : 0;
            bool currentIsSpinner = target.Drawable is DrawableSpinner;
            bool previousIsSpinner = previous?.Drawable is DrawableSpinner;
            bool overlapsHeldSlider = previous?.Drawable is DrawableSlider && targetsOverlap(previous.Value, target);

            if (currentIsSpinner && previousIsSpinner && hasPlannedAction)
            {
                alternatingBurstActive = false;
                streamBurstNoteCount = 0;
                rememberPlannedAction(lastPlannedAction, target.StartTime);
                return lastPlannedAction;
            }

            if (!hasPlannedAction || gap > getDerivedPrimaryFingerReset())
            {
                alternatingBurstActive = false;
                streamBurstNoteCount = currentIsBurstLike ? 1 : 0;
                tapStamina = Math.Min(1, tapStamina + 0.12);
                rememberPlannedAction(primaryAction, target.StartTime);
                return lastPlannedAction;
            }

            if (overlapsHeldSlider && hasPlannedAction)
            {
                OsuAction protectedSliderAction = lastPlannedAction;
                OsuAction nextAction = getOppositeAction(protectedSliderAction);

                if (currentIsBurstLike)
                    applyTapStaminaResult(previous?.HitObject != null ? getEffectiveTappingGap(previous.Value, target) : double.PositiveInfinity, true);

                alternatingBurstActive = currentIsBurstLike;
                rememberPlannedAction(nextAction, target.StartTime);
                return nextAction;
            }

            streamBurstNoteCount = currentIsBurstLike
                ? continuesBurstLike ? streamBurstNoteCount + 1 : 1
                : 0;

            double effectiveGap = previous?.HitObject != null ? getEffectiveTappingGap(previous.Value, target) : double.PositiveInfinity;
            double nextEffectiveGap = next?.HitObject != null ? getEffectiveTappingGap(target, next.Value) : double.PositiveInfinity;
            bool singletapFriendlyJump = !currentIsStream && isSingletapFriendlyJump(gap, currentJumpSeverity);
            bool currentSupportsThresholdAlternation = currentIsBurstLike || supportsThresholdAlternation(patternInfo);
            bool nextSupportsThresholdAlternation = nextIsBurstLike || supportsThresholdAlternation(nextPatternInfo);
            bool forceAlternationByThreshold = previous?.HitObject != null
                                               && shouldForceAlternationByThreshold(
                                                   effectiveGap,
                                                   nextEffectiveGap,
                                                   currentSupportsThresholdAlternation,
                                                   nextSupportsThresholdAlternation,
                                                   singletapFriendlyJump);
            bool forceAlternationByCadenceCap = previous?.HitObject != null
                                                && shouldForceAlternationBySameFingerCadenceCap(
                                                    target.StartTime,
                                                    effectiveGap,
                                                    nextEffectiveGap,
                                                    currentSupportsThresholdAlternation,
                                                    nextSupportsThresholdAlternation,
                                                    singletapFriendlyJump);

            double currentEquivalentBpm = (currentIsStream ? 15000.0 : 30000.0) / (effectiveGap * getGameplayRate());
            bool forceAlternationByBpm = getConfiguredAlternateThreshold() > 0 && currentEquivalentBpm >= getConfiguredAlternateThreshold();

            if (forceAlternationByBpm || forceAlternationByThreshold || forceAlternationByCadenceCap)
            {
                OsuAction nextAction = getOppositeAction(lastPlannedAction);

                if (currentIsBurstLike || nextSupportsThresholdAlternation)
                    applyTapStaminaResult(effectiveGap, true);

                alternatingBurstActive = currentSupportsThresholdAlternation || nextSupportsThresholdAlternation;
                rememberPlannedAction(nextAction, target.StartTime);
                return nextAction;
            }

            bool earlyStreamAlternation = currentIsStream && shouldAlternateStreamFromSecondNote(effectiveGap);
            bool alternateByStreamPattern = currentIsBurstLike && (
                earlyStreamAlternation
                    ? streamBurstNoteCount >= 2
                    : streamBurstNoteCount > getDerivedStreamAnchorCount(effectiveGap));
            bool alternateForWideJump = !currentIsStream
                                        && currentJumpSeverity > 0.12
                                        && !singletapFriendlyJump
                                        && effectiveGap <= getDerivedJumpAlternationWindow(currentJumpSeverity);
            bool alternateWithPrevious = alternateByStreamPattern
                                         || alternateForWideJump
                                         || shouldForceAlternation(effectiveGap, currentIsBurstLike)
                                         || currentIsBurstLike && alternatingBurstActive && shouldMaintainAlternatingBurst(effectiveGap, nextEffectiveGap, nextIsBurstLike);
            bool alternateIntoNext = next?.HitObject != null && shouldPrepareAlternation(target, next.Value, effectiveGap, nextIsBurstLike);

            if (alternateWithPrevious || alternateIntoNext)
            {
                OsuAction nextAction = getOppositeAction(lastPlannedAction);

                double dynamicMisalt = getConfiguredMisaltProbability();
                if (currentIsBurstLike)
                {
                    double depletion = Math.Clamp(1.0 - tapStamina, 0, 1.0);
                    // Когда стамина падает, шанс "споткнуться" пальцами сильно растет
                    dynamicMisalt += depletion * 0.08; 
                }

                if (nextUniform(seed, 53) < dynamicMisalt)
                    nextAction = lastPlannedAction;

                if (currentIsBurstLike)
                    applyTapStaminaResult(effectiveGap, nextAction != lastPlannedAction);

                alternatingBurstActive = currentIsBurstLike && shouldMaintainAlternatingBurst(effectiveGap, nextEffectiveGap, nextIsBurstLike);
                rememberPlannedAction(nextAction, target.StartTime);
                return nextAction;
            }

            if (currentIsBurstLike)
                applyTapStaminaResult(effectiveGap, false);

            alternatingBurstActive = false;
            rememberPlannedAction(primaryAction, target.StartTime);
            return lastPlannedAction;
        }

        private bool isFastStackBurst(TargetDescriptor target, TargetDescriptor? previous, TargetDescriptor? next, double gap)
        {
            if (!isStackedTarget(target, previous, next))
                return false;

            double stackWindow = Math.Clamp(getStableTapWindow() + 28, 92, 182);
            return gap <= stackWindow
                   || next?.HitObject != null && next.Value.StartTime - target.StartTime <= stackWindow;
        }

        private static bool isBurstAlternationPattern(OsuPatternState patternState)
        {
            OsuPatternInfo patternInfo = patternState.PatternInfo;
            return patternState.Candidate == OsuPatternSegmentKind.BurstFlow
               && patternInfo.Kind == OsuPatternKind.Burst
               && patternInfo.ChainLength >= 3
               && patternInfo.ContinuityWeight >= 0.64f
               && patternInfo.AverageSpacingRatio <= 3.25f;
        }

        private OsuPatternState[] analyzePatternStates(IReadOnlyList<TargetDescriptor> targets)
        {
            if (targets.Count == 0)
                return Array.Empty<OsuPatternState>();

            patternNodeBuffer.Clear();

            for (int i = 0; i < targets.Count; i++)
            {
                TargetDescriptor target = targets[i];
                patternNodeBuffer.Add(new OsuPatternNode(
                    target.ScreenSpacePosition,
                    target.StartTime,
                    target.Radius,
                    target.Drawable is DrawableHitCircle));
            }

            return OsuPatternSegmentAnalyzer.Analyze(patternNodeBuffer, index => getBeatLengthAt(targets[index].StartTime));
        }

        private static OsuAction getOppositeAction(OsuAction action)
            => action == OsuAction.LeftButton ? OsuAction.RightButton : OsuAction.LeftButton;

        private static bool isStackedTarget(TargetDescriptor target, TargetDescriptor? previous, TargetDescriptor? next)
        {
            if (previous?.HitObject != null && positionsMatch(target.BaseLocalPosition, previous.Value.BaseLocalPosition))
                return true;

            return next?.HitObject != null && positionsMatch(target.BaseLocalPosition, next.Value.BaseLocalPosition);
        }

        private OsuPatternInfo classifyPattern(IReadOnlyList<TargetDescriptor> targets, int currentIndex)
        {
            patternNodeBuffer.Clear();

            for (int i = 0; i < targets.Count; i++)
            {
                TargetDescriptor target = targets[i];
                patternNodeBuffer.Add(new OsuPatternNode(
                    target.ScreenSpacePosition,
                    target.StartTime,
                    target.Radius,
                    target.Drawable is DrawableHitCircle));
            }

            return OsuPatternClassifier.Classify(patternNodeBuffer, currentIndex, getBeatLengthAt(targets[currentIndex].StartTime));
        }

        private bool isStreamConnection(TargetDescriptor previous, TargetDescriptor current)
        {
            return OsuPatternClassifier.IsFlowConnection(
                new OsuPatternNode(previous.ScreenSpacePosition, previous.StartTime, previous.Radius, previous.Drawable is DrawableHitCircle),
                new OsuPatternNode(current.ScreenSpacePosition, current.StartTime, current.Radius, current.Drawable is DrawableHitCircle),
                getBeatLengthAt(previous.StartTime));
        }

        private string getModeName(OsuPatternState patternState)
        {
            OsuPatternInfo patternInfo = patternState.PatternInfo;

            if (!patternState.IsCommittedStream)
                return @"circle";

            return patternInfo.StreamShape switch
            {
                OsuStreamShapeKind.ZigZag => @"stream-zigzag",
                OsuStreamShapeKind.Arc => @"stream-arc",
                _ => patternInfo.StreamSpacing switch
                {
                    OsuStreamSpacingKind.Spaced => @"stream-spaced",
                    OsuStreamSpacingKind.Variable => @"stream-variable",
                    _ => @"stream"
                }
            };
        }

        private double getJumpSeverity(TargetDescriptor target, TargetDescriptor? previous, TargetDescriptor? next, bool isStream, bool isSpinner)
        {
            if (isStream || isSpinner)
                return 0;

            double maxSpacingRatio = 0;

            if (previous?.HitObject != null)
                maxSpacingRatio = Math.Max(maxSpacingRatio, getSpacingRatio(previous.Value, target));

            if (next?.HitObject != null)
                maxSpacingRatio = Math.Max(maxSpacingRatio, getSpacingRatio(target, next.Value));

            return Math.Clamp((maxSpacingRatio - derived_jump_spacing_ratio_threshold) / derived_jump_spacing_ratio_range, 0, 1);
        }

        private static double getSpacingRatio(TargetDescriptor previous, TargetDescriptor current)
        {
            return getSpacingRatio(previous.BaseLocalPosition, previous.HitObject.Radius, current.BaseLocalPosition, current.HitObject.Radius);
        }

        private static double getSpacingRatio(Vector2 previousPosition, double previousRadius, Vector2 currentPosition, double currentRadius)
        {
            double spacing = (currentPosition - previousPosition).Length;
            double radius = Math.Max(1, Math.Min(previousRadius, currentRadius));
            return spacing / (radius * 2);
        }

        private void recoverTapStamina(double currentTime)
        {
            if (double.IsNaN(lastStaminaSampleTime))
            {
                lastStaminaSampleTime = currentTime;
                return;
            }

            double elapsed = Math.Max(0, currentTime - lastStaminaSampleTime);
            tapStamina = Math.Min(1, tapStamina + elapsed / getDerivedPeakRecoveryTime());
            lastStaminaSampleTime = currentTime;
        }

        internal static double ConvertStableBpmToGameplayTapWindow(double bpm, double gameplayRate)
            => 15000.0 / Math.Max(1, bpm) * Math.Max(0.05, Math.Abs(gameplayRate));

        private double getGameplayRate()
        {
            double rate = (Clock as IGameplayClock)?.GetTrueGameplayRate() ?? Clock.Rate;
            return Math.Max(0.05, Math.Abs(rate));
        }

        private double scaleRealTimeWindow(double milliseconds)
            => milliseconds * getGameplayRate();

        private double toRealTimeWindow(double gameplayMilliseconds)
            => gameplayMilliseconds / getGameplayRate();

        private double getStableTapWindow()
            => ConvertStableBpmToGameplayTapWindow(getConfiguredStableBpm(), getGameplayRate());

        internal static double GetDerivedPeakBpm(double stableBpm)
        {
            double lowerBound = stableBpm + 18;
            double upperBound = Math.Max(420, lowerBound);
            double preferred = stableBpm * 1.28 + 12;
            return Math.Clamp(preferred, lowerBound, upperBound);
        }

        private double getPeakTapWindow()
        {
            double peakBpm = GetDerivedPeakBpm(getConfiguredStableBpm());
            return ConvertStableBpmToGameplayTapWindow(peakBpm, getGameplayRate());
        }

        internal static double GetDerivedLivePeakBpm(double stableBpm)
        {
            double lowerBound = stableBpm + 36;
            double upperBound = Math.Max(360, lowerBound);
            double preferred = stableBpm * 2.05 + 52;
            return Math.Clamp(preferred, lowerBound, upperBound);
        }

        private double getLivePeakTapWindow()
        {
            double peakBpm = GetDerivedLivePeakBpm(getConfiguredStableBpm());
            return ConvertStableBpmToGameplayTapWindow(peakBpm, getGameplayRate());
        }

        private double getExhaustedAlternationWindow()
        {
            double stableWindow = getStableTapWindow();
            return stableWindow + Math.Clamp(stableWindow * 0.28, 20, 48);
        }

        private double getAlternationThresholdTapWindow()
        {
            if (getConfiguredAlternateThreshold() <= 0)
                return double.NegativeInfinity;

            return ConvertStableBpmToGameplayTapWindow(getConfiguredAlternateThreshold(), getGameplayRate());
        }

        private bool shouldForceAlternationByThreshold(double effectiveGap, double nextEffectiveGap, bool currentSupportsThresholdAlternation, bool nextSupportsThresholdAlternation,
                                                       bool singletapFriendlyJump)
            => ShouldForceAlternationByThreshold(
                currentSupportsThresholdAlternation,
                nextSupportsThresholdAlternation,
                singletapFriendlyJump,
                effectiveGap,
                nextEffectiveGap,
                getAlternationThresholdTapWindow());

        internal static bool ShouldForceAlternationByThreshold(bool currentSupportsThresholdAlternation, bool nextSupportsThresholdAlternation, bool singletapFriendlyJump,
                                                               double effectiveGap, double nextEffectiveGap, double alternationThresholdTapWindow)
        {
            if (double.IsNegativeInfinity(alternationThresholdTapWindow) || effectiveGap > alternationThresholdTapWindow)
                return false;

            if (currentSupportsThresholdAlternation)
                return !singletapFriendlyJump;

            return nextSupportsThresholdAlternation
                   && nextEffectiveGap <= alternationThresholdTapWindow;
        }

        private bool shouldForceAlternationBySameFingerCadenceCap(double targetStartTime, double effectiveGap, double nextEffectiveGap, bool currentSupportsThresholdAlternation,
                                                                  bool nextSupportsThresholdAlternation, bool singletapFriendlyJump)
            => ShouldForceAlternationBySameFingerCadenceCap(
                hasPlannedAction && primaryAction == lastPlannedAction,
                currentSupportsThresholdAlternation,
                nextSupportsThresholdAlternation,
                singletapFriendlyJump,
                targetStartTime - lastPlannedActionTargetTime,
                effectiveGap,
                nextEffectiveGap,
                getSameFingerCadenceWindow());

        internal static bool ShouldForceAlternationBySameFingerCadenceCap(bool defaultWouldRepeatSameAction, bool currentSupportsThresholdAlternation,
                                                                          bool nextSupportsThresholdAlternation, bool singletapFriendlyJump, double elapsedSinceLastPlannedAction,
                                                                          double effectiveGap, double nextEffectiveGap, double cadenceWindow)
        {
            if (!defaultWouldRepeatSameAction || !double.IsFinite(elapsedSinceLastPlannedAction) || cadenceWindow <= 0 || elapsedSinceLastPlannedAction > cadenceWindow)
                return false;

            bool currentCadence = currentSupportsThresholdAlternation
                                  && !singletapFriendlyJump
                                  && effectiveGap <= cadenceWindow;
            bool enteringCadence = nextSupportsThresholdAlternation
                                   && effectiveGap <= cadenceWindow
                                   && nextEffectiveGap <= cadenceWindow;

            return currentCadence || enteringCadence;
        }

        private static bool supportsThresholdAlternation(OsuPatternInfo patternInfo)
            => patternInfo.Kind == OsuPatternKind.Stream
               || patternInfo.Kind == OsuPatternKind.Burst && patternInfo.ChainLength >= 3;

        private double getSameFingerCadenceWindow()
        {
            double cadenceWindow = getDerivedEarlyStreamAlternationWindow();
            double thresholdWindow = getAlternationThresholdTapWindow();

            if (!double.IsNegativeInfinity(thresholdWindow))
                cadenceWindow = Math.Max(cadenceWindow, thresholdWindow);

            return cadenceWindow;
        }

        private void rememberPlannedAction(OsuAction action, double targetStartTime)
        {
            lastPlannedAction = action;
            lastPlannedActionTargetTime = targetStartTime;
            hasPlannedAction = true;
        }

        private double getDerivedPeakRecoveryTime()
            => scaleRealTimeWindow(Math.Clamp(2100 - (getConfiguredStableBpm() - 150) * 5.5, 1200, 2300));

        private double getEffectiveTappingGap(TargetDescriptor previous, TargetDescriptor current)
        {
            double gap = current.StartTime - previous.StartTime;
            double spacingPenalty = getJumpTappingPenalty(previous, current);
            return Math.Max(1, gap - spacingPenalty);
        }

        private double getEffectiveTappingGap(TargetDescriptor previous, HitPlan current)
        {
            double gap = current.TargetStartTime - previous.StartTime;
            double spacingPenalty = getJumpTappingPenalty(previous.BaseLocalPosition, previous.HitObject.Radius, current.BaseLocalPosition, current.HitObject.Radius);
            return Math.Max(1, gap - spacingPenalty);
        }

        private double getJumpTappingPenalty(TargetDescriptor previous, TargetDescriptor current)
        {
            return getJumpTappingPenalty(previous.BaseLocalPosition, previous.HitObject.Radius, current.BaseLocalPosition, current.HitObject.Radius);
        }

        private double getJumpTappingSeverity(TargetDescriptor previous, TargetDescriptor current)
        {
            double spacingRatio = getSpacingRatio(previous, current);
            return Math.Clamp((spacingRatio - derived_jump_spacing_ratio_threshold) / derived_jump_spacing_ratio_range, 0, 1);
        }

        private double getJumpTappingPenalty(Vector2 previousPosition, double previousRadius, Vector2 currentPosition, double currentRadius)
        {
            double spacing = (currentPosition - previousPosition).Length;
            double radius = Math.Max(1, Math.Min(previousRadius, currentRadius));
            double spacingRatio = spacing / (radius * 2);
            return Math.Clamp((spacingRatio - 1.1) * 16, 0, 42);
        }

        private double getTapStrain(double effectiveGap)
        {
            double stableWindow = getStableTapWindow();

            if (effectiveGap >= stableWindow)
                return 0;

            double peakWindow = getPeakTapWindow();
            return Math.Clamp((stableWindow - effectiveGap) / Math.Max(1, stableWindow - peakWindow), 0, 1);
        }

        private double getRequiredTapStamina(double effectiveGap)
        {
            double strain = getTapStrain(effectiveGap);
            return Math.Clamp(0.18 + 0.72 * strain * strain, 0.18, 0.94);
        }

        private int getDerivedStreamAnchorCount(double effectiveGap)
        {
            if (effectiveGap <= getStableTapWindow() * 0.78)
                return 1;

            return 2;
        }

        private double getDerivedEarlyStreamAlternationWindow()
            => Math.Clamp(getStableTapWindow() + 26, 82, 102);

        private bool shouldAlternateStreamFromSecondNote(double effectiveGap)
            => effectiveGap <= getDerivedEarlyStreamAlternationWindow();

        private double getDerivedJumpAlternationWindow(double jumpSeverity)
            => Math.Clamp(Math.Max(getStableTapWindow() + 8, 112) + jumpSeverity * 42, 112, 176);

        internal static double GetDerivedJumpSingleTapWindow(double stableTapWindow, double jumpSeverity)
        {
            double upperBound = Math.Max(1, stableTapWindow + 60);
            double lowerBound = Math.Min(118, upperBound);
            double preferred = Math.Max(lowerBound, stableTapWindow + 54 - jumpSeverity * 20);

            return Math.Clamp(preferred, lowerBound, upperBound);
        }

        private double getDerivedJumpSingleTapWindow(double jumpSeverity)
            => GetDerivedJumpSingleTapWindow(getStableTapWindow(), jumpSeverity);

        private bool isSingletapFriendlyJump(double rawGap, double jumpSeverity)
            => jumpSeverity > 0.12 && rawGap >= getDerivedJumpSingleTapWindow(jumpSeverity);

        private double getDerivedJumpSingleTapVarianceMultiplier(double jumpSeverity)
            => Math.Clamp(0.5 - jumpSeverity * 0.2, 0.24, 0.5);

        private double getTapStaminaDelta(double effectiveGap, bool alternated)
        {
            double strain = getTapStrain(effectiveGap);

            if (strain <= 0)
                return 0;

            return alternated
                ? 0.015 + 0.055 * strain
                : -(0.07 + 0.27 * strain);
        }

        private double getProjectedTapStamina(double currentStamina, double effectiveGap, bool alternated)
            => Math.Clamp(currentStamina + getTapStaminaDelta(effectiveGap, alternated), 0, 1);

        private double getProjectedTapStamina(double effectiveGap, bool alternated)
            => getProjectedTapStamina(tapStamina, effectiveGap, alternated);

        private bool shouldForceAlternation(double effectiveGap, bool streamControlled)
        {
            if (effectiveGap <= getPeakTapWindow())
                return true;

            if (!streamControlled || effectiveGap > getExhaustedAlternationWindow())
                return false;

            return tapStamina < getRequiredTapStamina(effectiveGap);
        }

        private bool shouldMaintainAlternatingBurst(double effectiveGap, double nextEffectiveGap, bool nextIsStream)
        {
            double continuationWindow = getExhaustedAlternationWindow();

            if (nextIsStream && nextEffectiveGap <= continuationWindow)
                return true;

            return effectiveGap <= continuationWindow && tapStamina < 0.84;
        }

        private bool shouldPrepareAlternation(TargetDescriptor current, TargetDescriptor next, double currentGap, bool nextIsStream)
        {
            double nextRawGap = next.StartTime - current.StartTime;
            double nextGap = getEffectiveTappingGap(current, next);
            double nextJumpSeverity = getJumpTappingSeverity(current, next);

            if (!nextIsStream && isSingletapFriendlyJump(nextRawGap, nextJumpSeverity))
                return false;

            if (nextGap <= getPeakTapWindow())
                return true;

            if (!nextIsStream || nextGap > getExhaustedAlternationWindow())
                return false;

            double projectedAfterCurrent = getProjectedTapStamina(currentGap, false);
            double projectedAfterCurrentAndNext = getProjectedTapStamina(projectedAfterCurrent, nextGap, false);
            double preparationBuffer = 0.04 + getJumpTappingSeverity(current, next) * 0.34;

            return projectedAfterCurrentAndNext < Math.Min(0.97, getRequiredTapStamina(nextGap) + preparationBuffer);
        }

        private void applyTapStaminaResult(double effectiveGap, bool alternated)
            => tapStamina = getProjectedTapStamina(effectiveGap, alternated);
        
        private void recoverLiveTapStamina(double currentTime)
        {
            if (double.IsNaN(lastLiveStaminaSampleTime))
            {
                lastLiveStaminaSampleTime = currentTime;
                CurrentTapStamina = liveTapStamina;
                return;
            }

            double elapsed = Math.Max(0, currentTime - lastLiveStaminaSampleTime);
            liveTapStamina = Math.Min(1, liveTapStamina + elapsed / getDerivedPeakRecoveryTime());
            lastLiveStaminaSampleTime = currentTime;
            CurrentTapStamina = liveTapStamina;
        }

        private double getLiveStreamTapWindow()
        {
            double stableWindow = getStableTapWindow();
            double peakWindow = getLivePeakTapWindow();
            double exhaustedWindow = stableWindow + Math.Clamp(stableWindow * 0.52, 24, 104);

            if (liveTapStamina >= 0.35)
            {
                double burstProgress = Math.Pow((liveTapStamina - 0.35) / 0.65, 0.25);
                return stableWindow + (peakWindow - stableWindow) * burstProgress;
            }

            double exhaustionProgress = Math.Pow(liveTapStamina / 0.35, 0.75);
            return exhaustedWindow + (stableWindow - exhaustedWindow) * exhaustionProgress;
        }

        private double getLiveTapStaminaDelta(double effectiveGap, bool alternated)
        {
            double stableWindow = getStableTapWindow();
            double peakWindow = getLivePeakTapWindow();
            double exhaustedWindow = stableWindow + Math.Clamp(stableWindow * 0.52, 24, 104);

            if (effectiveGap >= exhaustedWindow)
                return -Math.Clamp(0.18 + (effectiveGap - exhaustedWindow) / Math.Max(1, stableWindow) * 0.24, 0.18, 0.45);

            if (effectiveGap >= stableWindow)
                return -Math.Clamp(0.07 + (effectiveGap - stableWindow) / Math.Max(1, exhaustedWindow - stableWindow) * 0.12, 0.07, 0.18);

            double strain = Math.Clamp((stableWindow - effectiveGap) / Math.Max(1, stableWindow - peakWindow), 0, 1);
            double overspeed = effectiveGap < peakWindow
                ? Math.Clamp((peakWindow - effectiveGap) / Math.Max(1, peakWindow * 0.45), 0, 1)
                : 0;

            double drain = 0.06 + 0.22 * strain + 0.22 * overspeed;

            if (alternated)
                drain *= 0.86;
            else
                drain *= 1.08;

            return drain;
        }

        private void applyLiveTapStaminaResult(HitPlan plan)
        {
            recoverLiveTapStamina(Time.Current);

            if (!plan.IsStream)
                return;

            double effectiveGap = plan.Previous?.HitObject != null ? getEffectiveTappingGap(plan.Previous.Value, plan) : double.PositiveInfinity;
            bool alternated = LastPressAction.HasValue && plan.Action != LastPressAction.Value;

            liveTapStamina = Math.Clamp(liveTapStamina - getLiveTapStaminaDelta(effectiveGap, alternated), 0, 1);
            CurrentTapStamina = liveTapStamina;

            double targetWindow = getLiveStreamTapWindow();

            if (double.IsNaN(smoothedLiveStreamTapWindow))
                smoothedLiveStreamTapWindow = targetWindow;
            else
            {
                double slowdownProgress = Math.Clamp((0.82 - liveTapStamina) / 0.82, 0, 1);
                double smoothing = 0.18 + slowdownProgress * 0.26;
                smoothedLiveStreamTapWindow += (targetWindow - smoothedLiveStreamTapWindow) * smoothing;
            }

            double cadenceGap = getStreamCadenceGateGap(plan);
            double missPenalty = getStreamMissRecoveryDelay(plan);
            nextStreamPressAllowedTime = Time.Current + cadenceGap + missPenalty;
            applyTimingFeedbackOffsetToPendingPlans();
        }

        private double getStreamCadenceGateGap(HitPlan plan)
        {
            if (!plan.IsStream || plan.Previous?.HitObject == null)
                return 0;

            double effectiveGap = getEffectiveTappingGap(plan.Previous.Value, plan);
            double cadenceWindow = double.IsNaN(smoothedLiveStreamTapWindow)
                ? getLiveStreamTapWindow()
                : smoothedLiveStreamTapWindow;

            if (cadenceWindow <= effectiveGap)
                return cadenceWindow;

            bool alternated = LastPressAction.HasValue && plan.Action != LastPressAction.Value;
            double overslow = Math.Clamp((cadenceWindow - effectiveGap) / Math.Max(1, effectiveGap * 0.55), 0, 1);
            double depletion = Math.Clamp((0.82 - liveTapStamina) / 0.82, 0, 1);
            double gateStrength = 0.22 + depletion * 0.28 + overslow * 0.16;

            if (alternated)
                gateStrength *= 0.88;

            if (plan.PatternInfo.StreamShape == OsuStreamShapeKind.ZigZag)
                gateStrength *= 0.86;

            if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Spaced)
                gateStrength *= 0.78;
            else if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Variable)
                gateStrength *= 0.84;

            double softenedGap = effectiveGap + (cadenceWindow - effectiveGap) * gateStrength;
            return Math.Clamp(softenedGap, 0, cadenceWindow);
        }

        private void evaluateStreamMiss(HitPlan plan)
        {
            Vector2 cursorPosition = getSyncCursorPosition();
            Vector2 rawCursorPosition = getRawCursorPosition();
            Vector2 targetPosition = getTargetScreenSpacePosition(plan);
            float radius = getTargetRadius(plan);
            float distance = (cursorPosition - targetPosition).Length;
            float rawDistance = (rawCursorPosition - targetPosition).Length;
            float hitRadius = getEffectiveSyncHitRadius(plan, radius);

            // Check if cursor was actually on the note at press time
            bool cursorOnNote = distance <= hitRadius || rawDistance <= hitRadius;

            if (cursorOnNote)
            {
                // Successful hit — decay miss state
                if (consecutiveStreamMisses > 0)
                {
                    consecutiveStreamMisses = Math.Max(0, consecutiveStreamMisses - 1);
                    streamMissRecoveryPenalty *= 0.45;
                }

                return;
            }

            // Stream miss detected — apply rhythm disruption
            consecutiveStreamMisses++;
            lastStreamMissTime = Time.Current;

            double effectiveGap = plan.Previous?.HitObject != null
                ? getEffectiveTappingGap(plan.Previous.Value, plan)
                : 60;

            // Base penalty: 15-45% of the gap between notes
            double basePenalty = effectiveGap * Math.Clamp(0.15 + consecutiveStreamMisses * 0.08, 0.15, 0.45);

            // Consecutive misses make recovery harder
            double consecutiveMultiplier = Math.Clamp(1 + (consecutiveStreamMisses - 1) * 0.35, 1, 2.2);

            // Stamina depletion from misses
            double depletionBonus = Math.Clamp((0.7 - liveTapStamina) / 0.7, 0, 1) * effectiveGap * 0.12;

            streamMissRecoveryPenalty = Math.Clamp(
                basePenalty * consecutiveMultiplier + depletionBonus,
                scaleRealTimeWindow(4),
                scaleRealTimeWindow(38));

            // Also nudge the adaptive timing correction to simulate rhythm disruption
            double timingDisruption = scaleRealTimeWindow(Math.Clamp(2 + consecutiveStreamMisses * 1.5, 2, 7));
            adaptiveTimingCorrection += timingDisruption * 0.3;
        }

        private double getStreamMissRecoveryDelay(HitPlan plan)
        {
            if (streamMissRecoveryPenalty <= 0 || double.IsNaN(lastStreamMissTime))
                return 0;

            // Decay the penalty based on time since last miss
            double elapsed = Math.Max(0, Time.Current - lastStreamMissTime);
            double decayTime = scaleRealTimeWindow(Math.Clamp(180 + consecutiveStreamMisses * 60, 180, 480));
            double decayFactor = Math.Clamp(1 - elapsed / decayTime, 0, 1);

            double penalty = streamMissRecoveryPenalty * decayFactor;

            if (penalty < 0.5)
            {
                streamMissRecoveryPenalty = 0;
                return 0;
            }

            return penalty;
        }

        private void processDuePresses()
        {
            while (true)
            {
                HitPlan? nextPlan = getNextPendingPlan();

                if (nextPlan == null)
                    return;

                double nextPressTime = getPressEvaluationTime(nextPlan);

                if (Time.Current < nextPressTime)
                    return;

                if (nextPlan.SuppressPress)
                {
                    skipPlan(nextPlan);
                    continue;
                }

                double nextPressAllowedTime = getNextPressAllowedTime(nextPlan);

                if (Time.Current < nextPressAllowedTime)
                {
                    nextPlan.DeferPressUntil(nextPressAllowedTime);
                    nextPlan.PendingSyncDelay = Math.Max(0, nextPressAllowedTime - nextPlan.GetCorrectedPlannedPressTime());
                    return;
                }

                if (shouldDelayForSync(nextPlan))
                    return;

                pressPlan(nextPlan);
            }
        }

        private HitPlan? getNextPendingPlan()
        {
            HitPlan? nextPlan = null;

            foreach (HitPlan plan in plans.Values)
            {
                if (plan.Pressed || plan.Released)
                    continue;

                if (nextPlan == null || plan.CompareQueueOrder(nextPlan) < 0)
                    nextPlan = plan;
            }

            return nextPlan;
        }

        private double getPressEvaluationTime(HitPlan plan)
        {
            if (plan.IsSpinner)
                return getLinkedPressTime(plan);

            Vector2 cursorPosition = getSyncCursorPosition();
            Vector2 rawCursorPosition = getRawCursorPosition();
            Vector2 targetPosition = getTargetScreenSpacePosition(plan);
            float radius = getTargetRadius(plan);
            float distance = (cursorPosition - targetPosition).Length;
            float acquisitionRadius = Math.Max(radius, getScreenSpaceRadius(plan.BaseLocalPosition, (float)plan.HitObject.Radius)) * getAdaptiveSyncRadiusScale(plan);
            
            if (plan.LastDistanceTime > 0 && Time.Current > plan.LastDistanceTime)
            {
                float velocityTowardsTarget = (plan.LastDistance - distance) / (float)(Time.Current - plan.LastDistanceTime);
                if (plan.IsJump && velocityTowardsTarget < -0.8f && distance > radius * 0.85f && distance <= acquisitionRadius)
                {
                    if (!plan.EmergencyPressTime.HasValue)
                        plan.EmergencyPressTime = Time.Current;
                }
            }
            
            plan.LastDistance = distance;
            plan.LastDistanceTime = Time.Current;

            float rawDistance = (rawCursorPosition - targetPosition).Length;
            float trustedRawRadius = getPlayerTimingTrustedRadius(plan, radius);
            double scheduledPressTime = plan.GetScheduledPressTime();
            bool gameplayAcquired = distance <= acquisitionRadius;
            bool rawAcquired = rawDistance <= acquisitionRadius;
            bool rawTrusted = rawDistance <= trustedRawRadius;
            float trackedDistance = Math.Min(distance, rawDistance);
            bool allowGameplayAcquisition = shouldAllowGameplayAcquisition(plan, rawAcquired, rawDistance, acquisitionRadius, scheduledPressTime);

            if (gameplayAcquired && allowGameplayAcquisition)
                plan.RegisterCursorAcquisition(Time.Current, false);

            if (rawAcquired)
            {
                plan.RegisterCursorAcquisition(Time.Current, true);
                plan.UpdateRawCursorTrust(rawTrusted, Time.Current);
            }
            else
            {
                bool hadTrustedRawAim = plan.RawCursorTrustedTime.HasValue;
                plan.UpdateRawCursorTrust(false, Time.Current);

                if (plan.IsJump
                    && plan.RawCursorAcquiredTime.HasValue
                    && !hadTrustedRawAim
                    && rawDistance > getJumpRawLatchReleaseRadius(plan, radius, acquisitionRadius))
                    plan.ClearRawCursorAcquisition();
            }

            if (!gameplayAcquired && !rawAcquired)
            {
                float releaseRadius = plan.IsJump
                    ? getJumpAcquisitionReleaseRadius(plan, radius, acquisitionRadius)
                    : acquisitionRadius + Math.Max(getConfiguredSyncRadius(), acquisitionRadius * derived_cursor_release_radius_multiplier);

                if (trackedDistance > releaseRadius)
                    plan.ClearCursorAcquisition();
            }

            return getLinkedPressTime(plan);
        }

        private bool shouldRegisterPlayerTimingSampleNow(HitPlan plan, double currentTime)
        {
            if (plan.PlayerTimingSampleRegistered || !plan.RawCursorAcquiredTime.HasValue)
                return false;

            if (currentTime < plan.GetPlayerTimingReferenceTime() - getDerivedPlayerTimingObservationLead(plan))
                return false;

            return hasCommittedEarlyRawAim(plan, currentTime);
        }

        private double getPlayerTimingSampleTime(HitPlan plan, double currentTime)
        {
            if (!plan.RawCursorAcquiredTime.HasValue)
                return currentTime;

            double sampleAnchorTime = plan.RawCursorTrustedTime ?? plan.RawCursorAcquiredTime.Value;
            double earliestTrustedTime = plan.GetPlayerTimingReferenceTime() - getDerivedPlayerTimingEarlySampleCap(plan);
            return Math.Max(sampleAnchorTime, earliestTrustedTime);
        }

        private bool shouldDelayForSync(HitPlan plan)
        {
            if (plan.IsSpinner || getConfiguredSyncRadius() <= 0 || getConfiguredMaxSyncDelay() <= 0)
            {
                plan.PendingSyncDelay = 0;
                plan.SyncDelayStarted = false;
                return false;
            }

            double pendingDelay = Time.Current - plan.GetCorrectedPlannedPressTime();
            double effectiveMaxSyncDelay = getEffectiveMaxSyncDelay(plan);
            double pressDeadline = plan.GetPressDeadline(effectiveMaxSyncDelay);

            if (plan.CurrentDrawable == null && plan.SyncDelayStarted)
            {
                plan.PendingSyncDelay = Math.Max(0, pendingDelay);
                return Time.Current < pressDeadline;
            }

            Vector2 cursorPosition = getSyncCursorPosition();
            Vector2 rawCursorPosition = getRawCursorPosition();
            Vector2 targetPosition = getTargetScreenSpacePosition(plan);
            float radius = getTargetRadius(plan);
            float distance = (cursorPosition - targetPosition).Length;
            
            float velocityTowardsTarget = 0;
            if (plan.LastDistanceTime > 0 && Time.Current > plan.LastDistanceTime)
            {
                velocityTowardsTarget = (plan.LastDistance - distance) / (float)(Time.Current - plan.LastDistanceTime);
            }
            plan.LastDistance = distance;
            plan.LastDistanceTime = Time.Current;

            float rawDistance = (rawCursorPosition - targetPosition).Length;
            bool isInsideHitbox = isCursorInsideHitbox(plan, distance, radius);
            bool isRawInsideHitbox = rawDistance <= getEffectiveSyncHitRadius(plan, radius);
            float blindTapThreshold = getEffectiveBlindTapThreshold(plan, radius);
            float effectiveSyncRange = getEffectiveSyncRange(plan, radius);
            float rawSupportedSyncRange = getRawSupportedSyncRange(plan, radius, effectiveSyncRange);
            bool committedRawSyncHold = hasCommittedRawSyncHold(plan, rawDistance, radius, rawSupportedSyncRange);
            bool assistedOnlySync = rawDistance > rawSupportedSyncRange && distance <= effectiveSyncRange;

            if (assistedOnlySync)
                effectiveMaxSyncDelay = Math.Min(effectiveMaxSyncDelay, getAssistOnlyMaxSyncDelay(plan));

            if (getConfiguredBlindTapEnabled() && blindTapThreshold > 0 && distance > blindTapThreshold)
            {
                plan.PendingSyncDelay = 0;
                plan.SyncDelayStarted = false;
                return false;
            }

            bool rawOffStreamTrajectory = plan.IsStream
                                          && rawDistance > Math.Max(getEffectiveSyncHitRadius(plan, radius) * 0.82f, radius * 0.64f)
                                          && isCursorOffStreamTrajectory(plan, rawCursorPosition);

            if (getConfiguredStreamBlindMode() && plan.IsStream && (isCursorOffStreamTrajectory(plan, cursorPosition) || rawOffStreamTrajectory))
            {
                plan.PendingSyncDelay = 0;
                plan.SyncDelayStarted = false;
                return false;
            }

            if (committedRawSyncHold)
            {
                plan.PendingSyncDelay = 0;
                plan.SyncDelayStarted = false;
                return false;
            }

            if (plan.IsJump && !isRawInsideHitbox)
            {
                if (pendingDelay >= effectiveMaxSyncDelay)
                {
                    plan.PendingSyncDelay = Math.Max(0, pendingDelay);
                    plan.SyncDelayStarted = false;
                    return false;
                }

                plan.PendingSyncDelay = Math.Max(0, pendingDelay);
                plan.SyncDelayStarted = true;
                return true;
            }

            if (isInsideHitbox || isRawInsideHitbox)
            {
                plan.PendingSyncDelay = 0;
                plan.SyncDelayStarted = false;
                return false;
            }

            if (distance > effectiveSyncRange)
            {
                plan.PendingSyncDelay = 0;
                plan.SyncDelayStarted = false;
                return false;
            }

            if (pendingDelay >= effectiveMaxSyncDelay)
            {
                plan.PendingSyncDelay = Math.Max(0, pendingDelay);
                plan.SyncDelayStarted = false;
                return false;
            }

            plan.PendingSyncDelay = Math.Max(0, pendingDelay);
            plan.SyncDelayStarted = true;
            return true;
        }

        private bool isCursorInsideHitbox(HitPlan plan, float distance, float radius)
        {
            float effectiveRadius = getEffectiveSyncHitRadius(plan, radius);
            DrawableOsuHitObject? drawable = plan.CurrentDrawable;

            if (drawable == null)
                return distance <= effectiveRadius;

            switch (drawable)
            {
                case DrawableSlider slider when slider.HeadCircle.IsLoaded:
                    return distance <= effectiveRadius;

                case DrawableHitCircle hitCircle when hitCircle.IsLoaded:
                    return distance <= effectiveRadius;

                default:
                    return distance <= effectiveRadius;
            }
        }

        private bool isCursorOffStreamTrajectory(HitPlan plan, Vector2 cursorPosition)
        {
            Vector2 first = Vector2.Zero;
            Vector2 second = Vector2.Zero;
            Vector2 third = Vector2.Zero;
            int pointCount = 0;

            if (plan.Previous?.HitObject != null)
                assignPoint(plan.Previous.Value.ScreenSpacePosition);

            assignPoint(plan.LastKnownScreenSpacePosition);

            if (plan.Next?.HitObject != null)
                assignPoint(plan.Next.Value.ScreenSpacePosition);

            if (pointCount < 2)
                return false;

            float bestDistanceSquared = float.MaxValue;

            evaluateSegment(first, second);

            if (pointCount == 3)
                evaluateSegment(second, third);

            float radius = getTargetRadius(plan);
            float allowedDistance = Math.Max(18f, radius + Math.Min(Math.Max(getConfiguredSyncRadius() * 0.4f, 8f), radius * 0.75f));

            if (plan.PatternInfo.StreamShape == OsuStreamShapeKind.ZigZag)
                allowedDistance = Math.Max(allowedDistance, radius * 1.65f);
            else if (plan.PatternInfo.StreamShape == OsuStreamShapeKind.Arc)
                allowedDistance = Math.Max(allowedDistance, radius * 1.4f);

            if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Spaced)
                allowedDistance = Math.Max(allowedDistance, radius * 1.85f);
            else if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Variable)
                allowedDistance = Math.Max(allowedDistance, radius * 1.55f);

            return bestDistanceSquared > allowedDistance * allowedDistance;

            void assignPoint(Vector2 point)
            {
                switch (pointCount)
                {
                    case 0:
                        first = point;
                        break;

                    case 1:
                        second = point;
                        break;

                    default:
                        third = point;
                        break;
                }

                pointCount++;
            }

            void evaluateSegment(Vector2 start, Vector2 end)
            {
                Vector2 segment = end - start;
                float lengthSquared = segment.LengthSquared;

                if (lengthSquared <= 0.0001f)
                    return;

                float t = Math.Clamp(Vector2.Dot(cursorPosition - start, segment) / lengthSquared, 0, 1);
                Vector2 projection = start + segment * t;
                bestDistanceSquared = Math.Min(bestDistanceSquared, (cursorPosition - projection).LengthSquared);
            }
        }

        private void pressPlan(HitPlan plan)
        {
            collectHeldPlans(plan.Action);
            bool reuseHeldSpinnerChain = canReuseHeldSpinnerChain(plan);

            if (!reuseHeldSpinnerChain && sameActionPlans.Count > 0)
            {
                for (int i = 0; i < sameActionPlans.Count; i++)
                    releasePlan(sameActionPlans[i]);

                plan.DeferPressUntil(Time.Current + 16);
                return;
            }

            double nextPressAllowedTime = getNextPressAllowedTime(plan);

            if (Time.Current < nextPressAllowedTime)
            {
                plan.DeferPressUntil(nextPressAllowedTime);
                plan.PendingSyncDelay = Math.Max(0, nextPressAllowedTime - plan.GetCorrectedPlannedPressTime());
                return;
            }

            plan.Pressed = true;
            plan.ActualPressTime = Time.Current;
            plan.PendingSyncDelay = Math.Max(0, Time.Current - plan.GetCorrectedPlannedPressTime());
            plan.SyncDelayStarted = false;
            incrementHeldPlanCount(plan.Action);

            if (!reuseHeldSpinnerChain)
            {
                setActionPressed(plan.Action, true);
                flushInputState();
            }

            // Stream miss recovery: detect if press happened while cursor is off the note
            if (plan.IsStream && !plan.IsSpinner)
                evaluateStreamMiss(plan);

            if (shouldRegisterPlayerTimingSampleNow(plan, Time.Current))
                registerPlayerTimingSample(plan, getPlayerTimingSampleTime(plan, Time.Current));

            registerTimingFeedbackSample(plan);
            applyLiveTapStaminaResult(plan);
            LastPressAction = plan.Action;
            LastPressTime = Time.Current;

            if (!reuseHeldSpinnerChain)
                inputEvents.Add(new RelaxInputEvent(true, plan.Action, Time.Current, plan.TargetStartTime));
        }

        private bool canReuseHeldSpinnerChain(HitPlan plan)
        {
            if (!plan.IsSpinner || sameActionPlans.Count == 0)
                return false;

            for (int i = 0; i < sameActionPlans.Count; i++)
            {
                if (!sameActionPlans[i].IsSpinner)
                    return false;
            }

            return true;
        }

        private void skipPlan(HitPlan plan)
        {
            plan.Released = true;
            plan.SyncDelayStarted = false;
            plan.PendingSyncDelay = 0;
        }

        private void collectHeldPlans(OsuAction action)
        {
            sameActionPlans.Clear();

            foreach (HitPlan plan in plans.Values)
            {
                if (!plan.Pressed || plan.Released || plan.Action != action)
                    continue;

                sameActionPlans.Add(plan);
            }
        }

        private void processDueReleases()
        {
            while (true)
            {
                HitPlan? nextRelease = null;
                double nextReleaseTime = double.PositiveInfinity;
                double currentTime = Time.Current;

                foreach (HitPlan plan in plans.Values)
                {
                    if (!plan.Pressed || plan.Released)
                        continue;

                    double releaseTime = plan.GetReleaseTime();

                    if (currentTime < releaseTime || releaseTime >= nextReleaseTime)
                        continue;

                    nextRelease = plan;
                    nextReleaseTime = releaseTime;
                }

                if (nextRelease == null)
                    return;

                releasePlan(nextRelease);
            }
        }

        private void releasePlan(HitPlan plan)
        {
            if (plan.Released)
                return;

            bool wasHeld = plan.Pressed;
            plan.Released = true;
            plan.SyncDelayStarted = false;
            bool releasedInput = false;

            if (wasHeld)
                decrementHeldPlanCount(plan.Action);

            if (!hasOtherHeldPlan(plan))
            {
                setActionPressed(plan.Action, false);
                flushInputState();
                setNextPressAllowedTime(plan.Action, Time.Current + getDerivedSameActionRepressGap());
                releasedInput = true;
            }

            if (releasedInput)
            {
                LastReleaseTime = Time.Current;
                inputEvents.Add(new RelaxInputEvent(false, plan.Action, Time.Current, plan.TargetStartTime));
            }
        }

        private bool hasOtherHeldPlan(HitPlan releasedPlan)
            => getHeldPlanCount(releasedPlan.Action) > 0;

        private void updateDiagnostics()
        {
            HitPlan? currentPlan = null;

            foreach (HitPlan plan in plans.Values)
            {
                if (plan.Released)
                    continue;

                if (currentPlan == null || plan.CompareDiagnosticPriority(currentPlan) < 0)
                    currentPlan = plan;
            }

            if (currentPlan == null)
            {
                CurrentModeName = @"idle";
                CurrentPlannedPressTime = null;
                CurrentPlannedReleaseTime = null;
                CurrentAppliedTimingOffset = 0;
                CurrentPendingSyncDelay = 0;
                CurrentTimingVarianceScale = 1;
                CurrentAdaptiveTimingCorrection = adaptiveTimingCorrection;
                CurrentPlayerTimingCorrection = playerTimingCorrection;
                CurrentStreamFatigueOffset = 0;
                CurrentImpactPointPosition = null;
                return;
            }

            CurrentModeName = currentPlan.ModeName;
            CurrentPlannedPressTime = getLinkedPressTime(currentPlan);
            CurrentPlannedReleaseTime = currentPlan.GetReleaseTime();
            CurrentAppliedTimingOffset = currentPlan.GetCorrectedPlannedPressTime() - currentPlan.TargetStartTime;
            CurrentPendingSyncDelay = currentPlan.PendingSyncDelay;
            CurrentAdaptiveTimingCorrection = adaptiveTimingCorrection;
            CurrentPlayerTimingCorrection = playerTimingCorrection;
            CurrentTimingVarianceScale = currentPlan.TimingVarianceScale;
            CurrentImpactPointPosition = getTargetImpactPointPosition(currentPlan, getRawCursorPosition());
        }

        private void updateCursorAutomation()
        {
        }

        private void releaseAllActions()
        {
            leftPressed = false;
            rightPressed = false;

            if (leftApplied || rightApplied)
                flushInputState();
        }

        private void setActionPressed(OsuAction action, bool isPressed)
        {
            switch (action)
            {
                case OsuAction.LeftButton:
                    leftPressed = isPressed;
                    break;

                case OsuAction.RightButton:
                    rightPressed = isPressed;
                    break;
            }
        }

        private void flushInputState()
        {
            if (inputManager == null)
                return;

            updateActionState(OsuAction.LeftButton, leftPressed, ref leftApplied);
            updateActionState(OsuAction.RightButton, rightPressed, ref rightApplied);
        }

        private void updateActionState(OsuAction action, bool shouldBePressed, ref bool isApplied)
        {
            if (shouldBePressed == isApplied)
                return;

            if (shouldBePressed)
                inputManager!.KeyBindingContainer.TriggerPressed(action);
            else
                inputManager!.KeyBindingContainer.TriggerReleased(action);

            isApplied = shouldBePressed;
        }

        private double getNextActionPressAllowedTime(OsuAction action)
        {
            switch (action)
            {
                case OsuAction.LeftButton:
                    return leftNextPressAllowedTime;

                case OsuAction.RightButton:
                    return rightNextPressAllowedTime;

                default:
                    return 0;
            }
        }

        private double getNextPressAllowedTime(HitPlan plan)
        {
            recoverLiveTapStamina(Time.Current);

            double actionAllowedTime = getNextActionPressAllowedTime(plan.Action);

            if (!plan.IsStream)
                return actionAllowedTime;

            return Math.Max(actionAllowedTime, nextStreamPressAllowedTime);
        }

        private void setNextPressAllowedTime(OsuAction action, double time)
        {
            switch (action)
            {
                case OsuAction.LeftButton:
                    leftNextPressAllowedTime = Math.Max(leftNextPressAllowedTime, time);
                    break;

                case OsuAction.RightButton:
                    rightNextPressAllowedTime = Math.Max(rightNextPressAllowedTime, time);
                    break;
            }
        }

        private double getDerivedHoldVariance()
            => getConfiguredTimingVariance() * derived_hold_variance_ratio;

        private double getDerivedPrimaryFingerReset()
        {
            double stableWindow = getStableTapWindow();
            return Math.Max(stableWindow * derived_primary_reset_multiplier, stableWindow + derived_primary_reset_padding);
        }

        private double getDerivedSameActionRepressGap()
            => scaleRealTimeWindow(Math.Clamp(getConfiguredHoldTime() * derived_same_action_repress_ratio + getConfiguredTimingVariance() * 0.35, derived_minimum_repress_gap, derived_maximum_repress_gap));

        private double getDerivedTimingFeedbackWindow()
            => scaleRealTimeWindow(Math.Clamp(2400 + getConfiguredMaxSyncDelay() * 38 + getConfiguredTimingVariance() * 42 + getConfiguredSyncRadius() * 14, 2400, 5600));

        private double getDerivedTimingFeedbackResetGap()
            => Math.Max(getDerivedPrimaryFingerReset() * 2.6, 1100);

        private double getDerivedTimingFeedbackTailThreshold()
            => Math.Clamp(4 + getConfiguredTimingVariance() * 0.35, 4, 10);

        private double getDerivedTimingFeedbackCorrectionCap()
            => Math.Clamp(6 + getConfiguredMaxSyncDelay() * 0.55 + getConfiguredTimingVariance() * 0.3 + getConfiguredSyncRadius() * 0.12, 6, 16);

        private double getDerivedPlayerTimingWindow()
            => scaleRealTimeWindow(Math.Clamp(1800 + getConfiguredSyncRadius() * 28 + getConfiguredTimingVariance() * 24, 1800, 4200));

        private double getDerivedPlayerTimingTailThreshold()
            => scaleRealTimeWindow(Math.Clamp(5 + getConfiguredTimingVariance() * 0.25, 5, 12));

        private double getDerivedPlayerTimingCorrectionCap()
            => scaleRealTimeWindow(Math.Clamp(8 + getConfiguredSyncRadius() * 0.35 + getConfiguredTimingVariance() * 0.3 + getConfiguredMaxSyncDelay() * 0.42, 8, 28));

        private double getDerivedPlayerTimingObservationLead(HitPlan plan)
        {
            double lead = scaleRealTimeWindow(Math.Clamp(12 + getConfiguredTimingVariance() * 0.2 + getConfiguredSyncRadius() * 0.12, 12, 22));

            if (plan.IsStream)
                lead += scaleRealTimeWindow(3);
            else if (plan.IsJump)
                lead *= 0.92;

            return lead;
        }

        private double getDerivedPlayerTimingEarlySampleCap(HitPlan plan)
        {
            double baseCap = scaleRealTimeWindow(Math.Clamp(18 + getConfiguredSyncRadius() * 0.28 + getConfiguredTimingVariance() * 0.35, 18, 40));

            if (!plan.IsJump)
                return baseCap;

            return baseCap + scaleRealTimeWindow(Math.Clamp(6 + plan.JumpSeverity * 12, 6, 16));
        }

        private double getDerivedPlayerTimingLateSampleCap(HitPlan plan)
        {
            double baseCap = scaleRealTimeWindow(Math.Clamp(18 + getConfiguredMaxSyncDelay() * 0.6 + getConfiguredTimingVariance() * 0.4, 18, 44));

            if (!plan.IsJump)
                return baseCap;

            return baseCap + scaleRealTimeWindow(Math.Clamp(4 + plan.JumpSeverity * 8, 4, 12));
        }

        private double getDerivedPlayerTimingEarlyCommitThreshold(HitPlan plan)
        {
            double threshold = scaleRealTimeWindow(Math.Clamp(4 + getConfiguredTimingVariance() * 0.12 + getConfiguredSyncRadius() * 0.05, 4, 9));

            if (plan.IsJump)
                threshold *= 1.08;
            else if (plan.IsStream)
                threshold *= 0.8;

            return threshold;
        }

        private double getDerivedPlayerTimingCommitHold(HitPlan plan)
        {
            double hold = scaleRealTimeWindow(Math.Clamp(8 + getConfiguredTimingVariance() * 0.15 + getConfiguredSyncRadius() * 0.08, 8, 18));

            if (plan.IsStream)
                hold *= 0.75;
            else if (plan.IsJump)
                hold *= 0.9;
            else if (plan.IsSlider && !plan.IsTapOnlySlider)
                hold *= 1.12;

            return hold;
        }

        private float getPlayerTimingTrustedRadius(HitPlan plan, float radius)
        {
            float trustedRadius = getEffectiveSyncHitRadius(plan, radius);
            float radialFloor = radius * (plan.IsJump ? 0.72f : plan.IsStream ? 0.6f : 0.64f);

            trustedRadius = Math.Max(trustedRadius, radialFloor);

            if (plan.IsSingleTapJump)
                trustedRadius = Math.Min(radius, trustedRadius + radius * 0.06f);

            float minimumTrustedRadius = Math.Min(radius, Math.Max(6f, radius * 0.55f));
            return Math.Clamp(trustedRadius, minimumTrustedRadius, radius);
        }

        private bool hasCommittedEarlyRawAim(HitPlan plan, double currentTime)
        {
            if (!plan.RawCursorAcquiredTime.HasValue)
                return false;

            double referenceTime = plan.GetPlayerTimingReferenceTime();
            double earlyCommitThreshold = getDerivedPlayerTimingEarlyCommitThreshold(plan);

            if (plan.RawCursorAcquiredTime.Value >= referenceTime - earlyCommitThreshold)
                return true;

            return plan.RawCursorTrustedTime.HasValue
                   && currentTime - plan.RawCursorTrustedTime.Value >= getDerivedPlayerTimingCommitHold(plan);
        }

        private bool hasCommittedRawSyncHold(HitPlan plan, float rawDistance, float radius, float rawSupportedSyncRange)
        {
            if (!plan.RawCursorAcquiredTime.HasValue)
                return false;

            float committedRadius = getCommittedRawSyncHoldRadius(plan, radius, rawSupportedSyncRange);

            if (rawDistance > committedRadius)
                return false;

            double holdAnchorTime = plan.RawCursorTrustedTime ?? plan.RawCursorAcquiredTime.Value;
            return Time.Current - holdAnchorTime >= getDerivedSyncCommitHold(plan);
        }

        private double getDerivedSyncCommitHold(HitPlan plan)
        {
            double hold = getDerivedPlayerTimingCommitHold(plan) * 0.7;

            if (plan.IsJump)
                hold *= 0.92;
            else if (plan.IsStream)
                hold *= 0.88;

            return Math.Max(scaleRealTimeWindow(6), hold);
        }

        private float getCommittedRawSyncHoldRadius(HitPlan plan, float radius, float rawSupportedSyncRange)
        {
            float trustedRadius = getPlayerTimingTrustedRadius(plan, radius);
            float radialFloor = radius * (plan.IsJump ? 0.74f : plan.IsStream ? 0.78f : 0.8f);
            float padding = Math.Min(
                Math.Max(4f, getConfiguredSyncRadius() * (plan.IsJump ? 0.18f : 0.22f)),
                radius * (plan.IsJump ? 0.08f : 0.12f));
            float committedRadius = Math.Max(radialFloor, trustedRadius + padding);
            return Math.Min(Math.Min(radius, rawSupportedSyncRange), committedRadius);
        }

        private double getDerivedCursorReactionOffset(int seed)
        {
            double variance = Math.Clamp(1.5 + getConfiguredTimingVariance() * 0.18, 1.5, 5.5);
            double bias = Math.Clamp(0.6 + getConfiguredHoldTime() * 0.028 + getConfiguredTimingVariance() * 0.05, 0.6, 4.2);
            return scaleRealTimeWindow(Math.Clamp(bias + nextGaussian(seed, 71) * variance, derived_minimum_cursor_reaction_offset, derived_maximum_cursor_reaction_offset));
        }

        private double getDerivedCursorTimingCoupling()
            => Math.Clamp(0.55 + getConfiguredTimingVariance() / 120.0 + getConfiguredSyncRadius() / 400.0, 0.55, 0.72);

        private double getDerivedAssistCursorTimingCoupling()
            => Math.Clamp(getDerivedCursorTimingCoupling() * derived_assist_cursor_coupling_ratio,
                derived_minimum_assist_cursor_coupling, derived_maximum_assist_cursor_coupling);

        private double getDerivedCursorEarlyShiftCap()
            => scaleRealTimeWindow(Math.Clamp(14 + getConfiguredTimingVariance() * 0.7, derived_minimum_cursor_early_shift, derived_maximum_cursor_early_shift));

        private double getDerivedAssistCursorEarlyShiftCap()
            => scaleRealTimeWindow(Math.Clamp(toRealTimeWindow(getDerivedCursorEarlyShiftCap()) * derived_assist_cursor_early_shift_ratio,
                derived_minimum_assist_cursor_early_shift, derived_maximum_assist_cursor_early_shift));

        private double getDerivedSliderTailPadding()
            // Default tail release should linger a bit after the slider ends instead of snapping off at the tail.
            => scaleRealTimeWindow(Math.Clamp(Math.Max(getConfiguredHoldTime() * 0.85, 22 + getConfiguredTimingVariance() * 0.65),
                derived_minimum_slider_tail_padding, derived_maximum_slider_tail_padding));

        private double getEffectiveSliderTailOffset()
            => getConfiguredSliderTailOffset() == 0
                ? getDerivedSliderTailPadding()
                : scaleRealTimeWindow(getConfiguredSliderTailOffset());

        private float getDerivedBlindTapThreshold(float radius)
        {
            if (getConfiguredSyncRadius() <= 0)
                return 0;

            return Math.Max(radius + getConfiguredSyncRadius() * 1.35f, getConfiguredSyncRadius() * 3f);
        }

        private float getAdaptiveSyncRadiusScale(HitPlan plan)
        {
            if (plan.Next?.HitObject == null || plan.IsSpinner)
                return 1;

            if (plan.Next.Value.Drawable is DrawableSpinner)
                return 1;

            double spacingRatio = getSpacingRatio(plan.BaseLocalPosition, plan.HitObject.Radius, plan.Next.Value.BaseLocalPosition, plan.Next.Value.HitObject.Radius);
            double realGap = toRealTimeWindow(Math.Max(0, plan.Next.Value.StartTime - plan.TargetStartTime));
            float pressure = (float)Math.Clamp(1 - realGap / 240.0, 0, 1);
            float closeCompression = (float)Math.Clamp((1.35 - spacingRatio) / 0.65, 0, 1);
            float wideExpansion = (float)Math.Clamp((spacingRatio - 1.95) / 1.9, 0, 1);
            float scale = 1;

            scale -= closeCompression * (float)Interpolation.Lerp(0.03f, 0.11f, pressure);
            scale += wideExpansion * (float)Interpolation.Lerp(0.03f, 0.12f, pressure);

            if (plan.IsJump)
                scale += (float)plan.JumpSeverity * 0.04f;

            return Math.Clamp(scale, 0.9f, 1.16f);
        }

        private float getEffectiveBlindTapThreshold(HitPlan plan, float radius)
        {
            float threshold = getDerivedBlindTapThreshold(radius) * getAdaptiveSyncRadiusScale(plan);

            if (!plan.IsJump || threshold <= 0)
                return threshold;

            float bonus = Math.Min(Math.Max(radius * 0.8f, getConfiguredSyncRadius() * 1.25f), radius * 1.85f);
            return threshold + bonus * (float)plan.JumpSeverity;
        }

        private float getEffectiveSyncRange(HitPlan plan, float radius)
        {
            float range = (radius + getConfiguredSyncRadius()) * getAdaptiveSyncRadiusScale(plan);

            if (!plan.IsJump)
                return range;

            float bonus = Math.Min(Math.Max(10f, getConfiguredSyncRadius() + radius * 0.35f), radius * 0.95f);
            return range + bonus * (float)plan.JumpSeverity;
        }

        private float getRawSupportedSyncRange(HitPlan plan, float radius, float effectiveSyncRange)
        {
            float trustedRadius = getPlayerTimingTrustedRadius(plan, radius);
            float supportPadding = Math.Min(
                Math.Max(8f, getConfiguredSyncRadius() * (plan.IsJump ? 0.28f : 0.45f)),
                radius * (plan.IsJump ? 0.35f : 0.62f));

            float supportRange = trustedRadius + supportPadding;

            if (!plan.IsJump)
                supportRange += Math.Min(getConfiguredSyncRadius() * 0.3f, radius * 0.28f);

            if (plan.IsStream)
            {
                if (plan.PatternInfo.StreamShape == OsuStreamShapeKind.ZigZag)
                    supportRange += Math.Min(getConfiguredSyncRadius() * 0.28f, radius * 0.2f);

                if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Spaced)
                    supportRange += Math.Min(getConfiguredSyncRadius() * 0.38f, radius * 0.3f);
                else if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Variable)
                    supportRange += Math.Min(getConfiguredSyncRadius() * 0.24f, radius * 0.18f);
            }

            return Math.Clamp(supportRange, trustedRadius, effectiveSyncRange);
        }

        private double getEffectiveMaxSyncDelay(HitPlan plan)
        {
            double delay = scaleRealTimeWindow(Math.Max(0, getConfiguredMaxSyncDelay()));

            if (!plan.IsJump || delay <= 0)
                return delay;

            double bonus = scaleRealTimeWindow(Math.Clamp(2 + getConfiguredSyncRadius() * 0.14 + getConfiguredTimingVariance() * 0.12, 2, 8));
            return delay + bonus * plan.JumpSeverity;
        }

        private double getAssistOnlyMaxSyncDelay(HitPlan plan)
        {
            double baseCap = scaleRealTimeWindow(Math.Clamp(6 + getConfiguredSyncRadius() * 0.18 + getConfiguredTimingVariance() * 0.18, 6, 16));

            if (plan.IsJump)
                baseCap *= 0.78;
            else if (plan.IsStream)
                baseCap *= 0.9;

            if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Spaced)
                baseCap *= 1.12;
            else if (plan.PatternInfo.StreamSpacing == OsuStreamSpacingKind.Variable)
                baseCap *= 1.06;

            return Math.Min(getEffectiveMaxSyncDelay(plan), baseCap);
        }

        private float getEffectiveSyncHitRadius(HitPlan plan, float radius)
        {
            if (getConfiguredSyncRadius() <= 0)
                return radius;

            float padding = Math.Clamp(getConfiguredSyncRadius() * 0.3f + radius * 0.1f,
                derived_sync_inner_minimum_padding,
                Math.Min(derived_sync_inner_maximum_padding, radius * 0.38f));

            if (plan.IsJump)
                return radius;

            return Math.Max(radius * derived_sync_inner_minimum_radius_ratio, (radius - padding) * getAdaptiveSyncRadiusScale(plan));
        }

        private bool shouldAllowGameplayAcquisition(HitPlan plan, bool rawAcquired, float rawDistance, float acquisitionRadius, double scheduledPressTime)
        {
            if (!plan.IsJump || rawAcquired)
                return true;

            float rawApproachRadius = acquisitionRadius + Math.Min(Math.Max(10f, getConfiguredSyncRadius() * 0.85f), acquisitionRadius * 0.75f) * (float)plan.JumpSeverity;

            if (rawDistance <= rawApproachRadius)
                return true;

            return Time.Current >= scheduledPressTime - getJumpGameplayAcquisitionLead(plan);
        }

        private double getJumpGameplayAcquisitionLead(HitPlan plan)
        {
            if (!plan.IsJump)
                return double.PositiveInfinity;

            return scaleRealTimeWindow(Math.Clamp(6 + getConfiguredSyncRadius() * 0.3 + plan.JumpSeverity * 10, derived_minimum_jump_gameplay_lead, derived_maximum_jump_gameplay_lead));
        }

        private float getJumpRawLatchReleaseRadius(HitPlan plan, float radius, float acquisitionRadius)
        {
            float trustedRadius = getPlayerTimingTrustedRadius(plan, radius);
            float releasePadding = Math.Min(Math.Max(4f, getConfiguredSyncRadius() * 0.3f), radius * 0.24f);
            return Math.Max(acquisitionRadius * 0.9f, trustedRadius + releasePadding);
        }

        private float getJumpAcquisitionReleaseRadius(HitPlan plan, float radius, float acquisitionRadius)
            => getJumpRawLatchReleaseRadius(plan, radius, acquisitionRadius);

        private double getBeatLengthAt(double time)
        {
            double beatLength = beatmap?.ControlPointInfo.TimingPointAt(time).BeatLength ?? 500;
            return beatLength > 0 ? beatLength : 500;
        }

        private double getDynamicDriftOffset(double time)
        {
            if (getConfiguredDynamicDrift() <= 0)
                return 0;

            double beatLength = getBeatLengthAt(time);
            double period = Math.Clamp(beatLength * drift_cycle_beats, min_drift_period, max_drift_period);
            return Math.Sin(time / period * Math.PI * 2) * getConfiguredDynamicDrift();
        }

        private double getSpinnerAngularSpeed(Spinner spinner)
        {
            double seconds = Math.Max(0.25, spinner.Duration / 1000.0);
            double minimumRps = Math.Max(2.4, spinner.SpinsRequired / seconds);
            double assistedRps = Math.Clamp(minimumRps * 1.3, 3.2, 6.2);
            return assistedRps * Math.PI * 2;
        }

        private double nextUniform(int seed, int salt)
        {
            uint value = (uint)HashCode.Combine(seed, salt);
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;

            return Math.Clamp((value + 1d) / (uint.MaxValue + 2d), minimum_uniform, 1 - minimum_uniform);
        }

        private double nextGaussian(int seed, int salt)
        {
            double u1 = nextUniform(seed, salt);
            double u2 = nextUniform(seed, salt + 1);
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(Math.PI * 2 * u2);
        }

        private Vector2 getTargetScreenSpacePosition(HitPlan plan)
        {
            if (plan.IsSpinner)
            {
                if (plan.CurrentDrawable != null)
                    return getTargetScreenSpacePosition(plan.CurrentDrawable);

                return plan.LastKnownScreenSpacePosition;
            }

            Vector2 targetPosition = plan.CurrentDrawable != null
                ? getTargetScreenSpacePosition(plan.CurrentDrawable)
                : plan.LastKnownScreenSpacePosition;
            return targetPosition;
        }

        private Vector2 getTargetImpactPointPosition(HitPlan plan, Vector2 rawCursorPosition)
        {
            if (plan.IsSpinner)
                return getTargetScreenSpacePosition(plan);

            Vector2 targetPosition = getTargetScreenSpacePosition(plan);
            Vector2? previousPosition = plan.Previous?.HitObject != null ? plan.Previous.Value.ScreenSpacePosition : null;
            Vector2? nextPosition = plan.Next?.HitObject != null ? plan.Next.Value.ScreenSpacePosition : null;
            float radius = getTargetRadius(plan);
            float strengthMultiplier = plan.IsSlider && !plan.IsTapOnlySlider ? 0.72f : 1f;

            return ImpactPointHelper.GetImpactPoint(targetPosition, radius, rawCursorPosition, previousPosition, nextPosition, aimCenterBias.Value, strengthMultiplier);
        }

        private Vector2 getTargetScreenSpacePosition(DrawableOsuHitObject drawable)
        {
            switch (drawable)
            {
                case DrawableSpinner spinner when spinner.Body.IsLoaded:
                    return spinner.Body.ScreenSpaceDrawQuad.Centre;

                case DrawableSlider slider when slider.HeadCircle.IsLoaded:
                    return slider.HeadCircle.ScreenSpaceDrawQuad.Centre;

                case DrawableHitCircle hitCircle when hitCircle.IsLoaded:
                    return hitCircle.ScreenSpaceDrawQuad.Centre;

                default:
                    return toScreenSpace(drawable.HitObject.StackedPosition);
            }
        }

        private Vector2 getSyncCursorPosition()
        {
            if (inputManager == null)
                return Vector2.Zero;

            return inputManager.CurrentState.Mouse.Position;
        }

        private Vector2 getRawCursorPosition()
        {
            if (inputManager == null)
                return Vector2.Zero;

            if (inputManager.ReplayBotActive && inputManager.HasReplayCursorPosition)
                return inputManager.ReplayCursorPosition;

            return getSyncCursorPosition();
        }

        private int getHeldPlanCount(OsuAction action)
        {
            switch (action)
            {
                case OsuAction.LeftButton:
                    return leftHeldPlanCount;

                case OsuAction.RightButton:
                    return rightHeldPlanCount;

                default:
                    return 0;
            }
        }

        private void incrementHeldPlanCount(OsuAction action)
        {
            switch (action)
            {
                case OsuAction.LeftButton:
                    leftHeldPlanCount++;
                    break;

                case OsuAction.RightButton:
                    rightHeldPlanCount++;
                    break;
            }
        }

        private void decrementHeldPlanCount(OsuAction action)
        {
            switch (action)
            {
                case OsuAction.LeftButton:
                    leftHeldPlanCount = Math.Max(0, leftHeldPlanCount - 1);
                    break;

                case OsuAction.RightButton:
                    rightHeldPlanCount = Math.Max(0, rightHeldPlanCount - 1);
                    break;
            }
        }

        private double getLinkedPressTime(HitPlan plan)
            => plan.GetLinkedPressTime(
                getDerivedCursorTimingCoupling(),
                plan.IsJump ? 0 : getDerivedAssistCursorTimingCoupling(),
                getDerivedCursorEarlyShiftCap(),
                plan.IsJump ? 0 : getDerivedAssistCursorEarlyShiftCap(),
                allowEarlyPull: !plan.IsSpinner && !plan.IsJump);

        private float getTargetRadius(HitPlan plan)
        {
            if (plan.CurrentDrawable != null)
                return getDrawableTargetRadius(plan.CurrentDrawable, plan.HitObject);

            if (plan.LastKnownRadius > 0)
                return plan.LastKnownRadius;

            return getScreenSpaceRadius(plan.BaseLocalPosition, (float)plan.HitObject.Radius);
        }

        private float getDrawableTargetRadius(DrawableOsuHitObject drawable, OsuHitObject hitObject)
        {
            switch (drawable)
            {
                case DrawableSpinner spinner when spinner.Body.IsLoaded:
                    return getDrawableScreenRadius(spinner.Body);

                case DrawableSlider slider when slider.IsLoaded:
                    return getHitCircleScreenRadius(slider.HeadCircle);

                case DrawableHitCircle hitCircle when hitCircle.IsLoaded:
                    return getHitCircleScreenRadius(hitCircle);

                default:
                    return getScreenSpaceRadius(hitObject.StackedPosition, (float)hitObject.Radius);
            }
        }

        private static float getHitCircleScreenRadius(DrawableHitCircle hitCircle)
            => getDrawableScreenRadius(hitCircle.HitArea);

        private static float getDrawableScreenRadius(Drawable drawable)
        {
            float width = (drawable.ScreenSpaceDrawQuad.TopRight - drawable.ScreenSpaceDrawQuad.TopLeft).Length;
            float height = (drawable.ScreenSpaceDrawQuad.BottomLeft - drawable.ScreenSpaceDrawQuad.TopLeft).Length;
            return Math.Min(width, height) * 0.5f;
        }

        private float getScreenSpaceRadius(Vector2 localPosition, float localRadius)
        {
            Vector2 centre = toScreenSpace(localPosition);
            Vector2 edge = toScreenSpace(localPosition + new Vector2(localRadius, 0));
            return (edge - centre).Length;
        }

        private Vector2 toScreenSpace(Vector2 localPosition) => playfield!.GamefieldToScreenSpace(localPosition);

        private static bool positionsMatch(Vector2 first, Vector2 second)
            => (first - second).LengthSquared <= 1f;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
        }

        public readonly struct RelaxInputEvent
        {
            public readonly bool IsPress;
            public readonly OsuAction Action;
            public readonly double Time;
            public readonly double TargetStartTime;

            public RelaxInputEvent(bool isPress, OsuAction action, double time, double targetStartTime)
            {
                IsPress = isPress;
                Action = action;
                Time = time;
                TargetStartTime = targetStartTime;
            }
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

        private readonly struct TimingFeedbackSample
        {
            public readonly double Time;
            public readonly double Residual;

            public TimingFeedbackSample(double time, double residual)
            {
                Time = time;
                Residual = residual;
            }
        }

        private readonly struct PlayerTimingSample
        {
            public readonly double Time;
            public readonly double Residual;
            public readonly double WeightScale;

            public PlayerTimingSample(double time, double residual, double weightScale)
            {
                Time = time;
                Residual = residual;
                WeightScale = weightScale;
            }
        }

        private sealed class HitPlan
        {
            public readonly int Sequence;
            public readonly OsuHitObject HitObject;
            public readonly double TargetStartTime;
            public readonly double TargetEndTime;
            public readonly Vector2 BaseLocalPosition;
            public readonly TargetDescriptor? Previous;
            public readonly TargetDescriptor? Next;
            public OsuAction Action;
            public readonly double AppliedOffset;
            public readonly double PlannedPressTime;
            public readonly double CursorReactionOffset;
            public readonly double HoldDuration;
            public readonly double FixedReleaseTime;
            public readonly bool IsSlider;
            public readonly bool IsSpinner;
            public readonly bool IsSingleTapJump;
            public readonly bool IsTapOnlySlider;
            public readonly bool SuppressPress;
            public readonly bool KeepHeldSpinnerChain;
            public readonly bool IsStacked;
            public readonly bool IsStream;
            public readonly double JumpSeverity;
            public readonly double TimingVarianceScale;
            public bool IsJump => JumpSeverity > 0.01;
            public readonly string ModeName;
            public readonly OsuPatternInfo PatternInfo;
            public readonly OsuPatternState PatternState;

            public DrawableOsuHitObject? CurrentDrawable;
            public float LastKnownRadius;
            public Vector2 LastKnownScreenSpacePosition;
            public bool SeenThisFrame;
            public bool Pressed;
            public bool Released;
            public bool SyncDelayStarted;
            public double? ActualPressTime;
            public double? GameplayCursorAcquiredTime;
            public double? RawCursorAcquiredTime;
            public double? RawCursorTrustedTime;
            public float LastDistance = float.MaxValue;
            public double LastDistanceTime;
            public double PendingSyncDelay;
            public double DeferredPressTime;
            public double? EmergencyPressTime;
            public double TimingFeedbackOffset;
            public double SpinnerAngle;
            public double SpinnerLastUpdateTime;
            public int SpinnerDirection;
            public bool SpinnerCursorInitialised;
            public bool PlayerTimingSampleRegistered;
            public bool HasRawCursorAcquisition => RawCursorAcquiredTime.HasValue;

            public HitPlan(int sequence, TargetDescriptor target, TargetDescriptor? previous, TargetDescriptor? next, OsuAction action, double appliedOffset, double plannedPressTime,
                           double cursorReactionOffset,
                           double holdDuration, double fixedReleaseTime, bool isSlider, bool isSpinner, bool isSingleTapJump, bool isTapOnlySlider, bool suppressPress, bool keepHeldSpinnerChain,
                           bool isStacked, bool isStream, double jumpSeverity, string modeName, int seed, double timingVarianceScale,
                           double timingFeedbackOffset, OsuPatternInfo patternInfo = default, OsuPatternState patternState = default)
            {
                Sequence = sequence;
                HitObject = target.HitObject;
                TargetStartTime = target.StartTime;
                TargetEndTime = target.EndTime;
                BaseLocalPosition = target.BaseLocalPosition;
                Previous = previous;
                Next = next;
                Action = action;
                AppliedOffset = appliedOffset;
                PlannedPressTime = plannedPressTime;
                CursorReactionOffset = cursorReactionOffset;
                HoldDuration = holdDuration;
                FixedReleaseTime = fixedReleaseTime;
                IsSlider = isSlider;
                IsSpinner = isSpinner;
                IsSingleTapJump = isSingleTapJump;
                IsTapOnlySlider = isTapOnlySlider;
                SuppressPress = suppressPress;
                KeepHeldSpinnerChain = keepHeldSpinnerChain;
                IsStacked = isStacked;
                IsStream = isStream;
                JumpSeverity = jumpSeverity;
                TimingVarianceScale = timingVarianceScale;
                ModeName = modeName;
                PatternInfo = patternInfo;
                PatternState = patternState;
                CurrentDrawable = target.Drawable;
                LastKnownRadius = target.Radius;
                LastKnownScreenSpacePosition = target.ScreenSpacePosition;
                GameplayCursorAcquiredTime = null;
                RawCursorAcquiredTime = null;
                RawCursorTrustedTime = null;
                DeferredPressTime = double.NegativeInfinity;
                TimingFeedbackOffset = timingFeedbackOffset;
                SpinnerAngle = 0;
                SpinnerLastUpdateTime = double.NaN;
                SpinnerDirection = (seed & 1) == 0 ? 1 : -1;
                SpinnerCursorInitialised = false;
                PlayerTimingSampleRegistered = false;
            }

            public void BeginFrame()
            {
                SeenThisFrame = false;
                CurrentDrawable = null;
            }

            public void UpdateTarget(TargetDescriptor target)
            {
                SeenThisFrame = true;
                CurrentDrawable = target.Drawable;
                LastKnownRadius = target.Radius;
                LastKnownScreenSpacePosition = target.ScreenSpacePosition;
            }

            public int CompareQueueOrder(HitPlan other)
            {
                int result = TargetStartTime.CompareTo(other.TargetStartTime);

                if (result != 0)
                    return result;

                result = GetScheduledPressTime().CompareTo(other.GetScheduledPressTime());

                if (result != 0)
                    return result;

                return Sequence.CompareTo(other.Sequence);
            }

            public double GetFeedbackReferencePressTime()
                => TargetStartTime + AppliedOffset;

            public double GetPlayerTimingReferenceTime()
                => GetFeedbackReferencePressTime();

            public double GetCorrectedPlannedPressTime()
                => PlannedPressTime + TimingFeedbackOffset;

            public double GetScheduledPressTime()
            {
                double scheduled = Math.Max(GetCorrectedPlannedPressTime(), DeferredPressTime);
                if (EmergencyPressTime.HasValue)
                    return Math.Min(scheduled, EmergencyPressTime.Value);
                return scheduled;
            }

            public double GetLinkedPressTime(double rawCoupling, double gameplayCoupling, double rawEarlyShiftCap, double gameplayEarlyShiftCap, bool allowEarlyPull)
            {
                double scheduledPressTime = GetScheduledPressTime();
                double? cursorAcquiredTime = RawCursorAcquiredTime ?? GameplayCursorAcquiredTime;

                if (!cursorAcquiredTime.HasValue)
                    return scheduledPressTime;

                bool rawCursorTiming = RawCursorAcquiredTime.HasValue;
                double coupling = rawCursorTiming ? rawCoupling : gameplayCoupling;
                double earlyShiftCap = rawCursorTiming ? rawEarlyShiftCap : gameplayEarlyShiftCap;
                double cursorDrivenPressTime = cursorAcquiredTime.Value + CursorReactionOffset;

                if (cursorDrivenPressTime <= scheduledPressTime)
                {
                    if (!allowEarlyPull)
                        return scheduledPressTime;

                    return scheduledPressTime + Math.Max(-earlyShiftCap, (cursorDrivenPressTime - scheduledPressTime) * coupling);
                }

                return cursorDrivenPressTime;
            }

            public void DeferPressUntil(double time)
                => DeferredPressTime = Math.Max(DeferredPressTime, time);

            public bool RegisterCursorAcquisition(double currentTime, bool isRawCursor)
            {
                if (isRawCursor)
                {
                    if (RawCursorAcquiredTime.HasValue)
                        return false;

                    RawCursorAcquiredTime = currentTime;
                    return true;
                }

                if (GameplayCursorAcquiredTime.HasValue)
                    return false;

                GameplayCursorAcquiredTime = currentTime;
                return true;
            }

            public void UpdateRawCursorTrust(bool isTrusted, double currentTime)
            {
                if (isTrusted)
                    RawCursorTrustedTime ??= currentTime;
                else
                    RawCursorTrustedTime = null;
            }

            public void ClearCursorAcquisition()
            {
                GameplayCursorAcquiredTime = null;
                RawCursorAcquiredTime = null;
                RawCursorTrustedTime = null;
            }

            public void ClearRawCursorAcquisition()
            {
                RawCursorAcquiredTime = null;
                RawCursorTrustedTime = null;
            }

            public void InitialiseSpinnerCursor(Vector2 radial, double currentTime)
            {
                SpinnerAngle = Math.Atan2(radial.Y, radial.X);
                SpinnerLastUpdateTime = currentTime;
                SpinnerCursorInitialised = true;
            }

            public void AdvanceSpinnerCursor(double angularSpeed, double currentTime)
            {
                if (!SpinnerCursorInitialised)
                    return;

                double elapsed = Math.Max(0, currentTime - SpinnerLastUpdateTime);
                SpinnerAngle += SpinnerDirection * angularSpeed * elapsed / 1000.0;
                SpinnerLastUpdateTime = currentTime;
            }

            public int CompareDiagnosticPriority(HitPlan other)
            {
                int result = getDiagnosticBucket().CompareTo(other.getDiagnosticBucket());

                if (result != 0)
                    return result;

                return CompareQueueOrder(other);
            }

            public double GetReleaseTime()
            {
                double pressTime = ActualPressTime ?? GetCorrectedPlannedPressTime();

                if (IsSlider && !IsTapOnlySlider)
                    return Math.Max(pressTime + HoldDuration, FixedReleaseTime);

                if (IsSpinner)
                    return Math.Max(pressTime, FixedReleaseTime);

                return pressTime + HoldDuration;
            }

            public double GetPressDeadline(double syncDelayLimit)
                => GetCorrectedPlannedPressTime() + syncDelayLimit;

            private int getDiagnosticBucket()
                => Pressed ? 1 : 0;
        }
    }
}
