// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.UI
{
    [Cached]
    public partial class ObservedHitObjectGraph : CompositeDrawable, IKeyBindingHandler<OsuAction>
    {
        // Temporarily disabled while we rework the anti-cheat surfaces and UX.
        /// <summary>
        /// Master switch for the observed-hit-object anti-cheat. While this is <c>false</c> the component is not
        /// added to the playfield at all, so it costs neither scene nodes nor an update-thread callback.
        /// </summary>
        public const bool FEATURE_ENABLED = false;

        private const bool feature_enabled = FEATURE_ENABLED;
        private const int max_debug_targets = 32;
        private const float distance_margin = 1.5f;

        private readonly BindableBool graphEnabled = new BindableBool();
        private readonly BindableBool debugVisible = new BindableBool();
        private readonly BindableFloat shiftPixels = new BindableFloat();
        private readonly BindableDouble shiftIntervalMs = new BindableDouble();
        private readonly BindableBool onlineRecordSendingDisabled = new BindableBool();

        private readonly Dictionary<OsuHitObject, int> sequenceIndexByHitObject = new Dictionary<OsuHitObject, int>();
        private readonly Dictionary<DrawableOsuHitObject, ObservedHitObjectState> statesByDrawable = new Dictionary<DrawableOsuHitObject, ObservedHitObjectState>();
        private readonly List<ObservedHitObjectState> observedTargets = new List<ObservedHitObjectState>();
        private readonly List<CircularContainer> debugMarkers = new List<CircularContainer>();
        private readonly HashSet<int> shiftedSequenceIndicesSeen = new HashSet<int>();

        private Container markerContainer = null!;
        private Container infoContainer = null!;
        private OsuSpriteText infoText = null!;
        private IBeatmap? beatmap;
        private OsuPlayfield? playfield;
        private OsuInputManager? inputManager;
        private bool divergenceLogged;
        private double sessionMonitoringTime;
        private double sessionSuspiciousTime;
        private double sessionDivergentTime;
        private double sessionConsistencyIntegral;
        private double sessionPeakConsistencyScore;
        private int sessionPressEvidenceCount;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        public IReadOnlyList<ObservedHitObjectState> ObservedTargets => observedTargets;

        public ObservedHitObjectState? CurrentTarget { get; private set; }

        public ObservedHitObjectState? NextTarget { get; private set; }

        public ObservedTargetConsistencyState ConsistencyState { get; private set; } = ObservedTargetConsistencyState.Inactive;

        public double ConsistencyScore { get; private set; }

        public bool IsMonitoringActive => feature_enabled
                                          && graphEnabled.Value
                                          && api.IsLoggedIn
                                          && !onlineRecordSendingDisabled.Value
                                          && inputManager != null
                                          && inputManager.ReplayInputHandler == null
                                          && !inputManager.ReplayBotActive;

        public ObservedHitObjectGraph()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IBeatmap? beatmap)
        {
            this.beatmap = beatmap;
            config.BindWith(OsuSetting.ForkObservedHitObjectGraphEnabled, graphEnabled);
            config.BindWith(OsuSetting.ForkObservedHitObjectGraphDebugVisible, debugVisible);
            config.BindWith(OsuSetting.ForkObservedHitObjectGraphShiftPixels, shiftPixels);
            config.BindWith(OsuSetting.ForkObservedHitObjectGraphShiftIntervalMs, shiftIntervalMs);
            config.BindWith(OsuSetting.ForkDisableOnlineRecordSending, onlineRecordSendingDisabled);
            rebuildSequenceIndexMap();

            InternalChildren = new Drawable[]
            {
                markerContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Name = "ObservedHitObjectContainer",
                },
                infoContainer = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Margin = new MarginPadding(12),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(10, 12, 16, 220),
                        },
                        infoText = new OsuSpriteText
                        {
                            Padding = new MarginPadding(8),
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                            Colour = Color4.White,
                        }
                    }
                }
            };
        }

        protected override void Update()
        {
            base.Update();

            playfield ??= this.FindClosestParent<OsuPlayfield>();
            inputManager ??= GetContainingInputManager() as OsuInputManager;

            if (playfield == null)
                return;

            if (!IsMonitoringActive)
            {
                clearState();
                updateSnapshot();
                return;
            }

            updateObservedTargets();
            updateConsistencyState();
            updateDebugOverlay();
            updateSnapshot();
        }

        public bool OnPressed(KeyBindingPressEvent<OsuAction> e)
        {
            if (!IsMonitoringActive)
                return false;

            switch (e.Action)
            {
                case OsuAction.LeftButton:
                case OsuAction.RightButton:
                    addPressEvidence();
                    break;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<OsuAction> e)
        {
        }

        private void updateObservedTargets()
        {
            var aliveDrawables = new HashSet<DrawableOsuHitObject>();

            observedTargets.Clear();

            foreach (DrawableOsuHitObject drawable in playfield!.HitObjectContainer.AliveObjects
                         .OfType<DrawableOsuHitObject>()
                         .Where(shouldObserveDrawable)
                         .OrderBy(getSequenceIndex)
                         .ThenBy(d => d.HitObject.StartTime))
            {
                aliveDrawables.Add(drawable);

                if (!statesByDrawable.TryGetValue(drawable, out ObservedHitObjectState? state))
                {
                    state = new ObservedHitObjectState();
                    statesByDrawable.Add(drawable, state);
                }

                Vector2 realScreenSpacePosition = getDisplayCentre(drawable);
                float radius = getDrawableTargetRadius(drawable);
                int sequenceIndex = getSequenceIndex(drawable);
                Vector2 mirrorOffset = computeMirrorOffset(drawable, sequenceIndex, radius);

                state.Update(
                    drawable,
                    sequenceIndex,
                    realScreenSpacePosition,
                    realScreenSpacePosition + mirrorOffset,
                    playfield.ScreenSpaceToGamefield(realScreenSpacePosition),
                    playfield.ScreenSpaceToGamefield(realScreenSpacePosition + mirrorOffset),
                    mirrorOffset,
                    radius);

                observedTargets.Add(state);

                if (state.MirrorOffset.LengthSquared > 0.001f)
                    shiftedSequenceIndicesSeen.Add(sequenceIndex);
            }

            foreach (DrawableOsuHitObject staleDrawable in statesByDrawable.Keys.Where(key => !aliveDrawables.Contains(key)).ToArray())
                statesByDrawable.Remove(staleDrawable);

            CurrentTarget = observedTargets.FirstOrDefault(state => Time.Current <= state.EndTime + 120)
                            ?? observedTargets.FirstOrDefault();

            if (CurrentTarget != null)
            {
                int currentIndex = observedTargets.IndexOf(CurrentTarget);
                NextTarget = currentIndex >= 0 && currentIndex + 1 < observedTargets.Count
                    ? observedTargets[currentIndex + 1]
                    : null;
            }
            else
                NextTarget = null;
        }

        private void updateConsistencyState()
        {
            double elapsedSeconds = Math.Abs(Clock.ElapsedFrameTime) / 1000.0;

            if (CurrentTarget == null || inputManager == null)
            {
                ConsistencyScore = Math.Max(0, ConsistencyScore - elapsedSeconds);
                ConsistencyState = ObservedTargetConsistencyState.Monitoring;
                return;
            }

            Vector2 cursorPosition = inputManager.CurrentState.Mouse.Position;
            float authoritativeDistance = (cursorPosition - CurrentTarget.AuthoritativeScreenSpacePosition).Length;
            float observedDistance = (cursorPosition - CurrentTarget.ObservedScreenSpacePosition).Length;
            bool inEvidenceWindow = Time.Current >= CurrentTarget.StartTime - 450 && Time.Current <= CurrentTarget.EndTime + 150;

            if (inEvidenceWindow && observedDistance + distance_margin < authoritativeDistance && observedDistance <= CurrentTarget.Radius * 1.1f)
            {
                float margin = authoritativeDistance - observedDistance;
                ConsistencyScore = Math.Min(16, ConsistencyScore + Math.Min(12, margin) * elapsedSeconds * 2.5);
            }
            else
                ConsistencyScore = Math.Max(0, ConsistencyScore - elapsedSeconds * 1.4);

            ConsistencyState = ConsistencyScore >= 4
                ? ObservedTargetConsistencyState.Divergent
                : ConsistencyScore >= 1
                    ? ObservedTargetConsistencyState.Suspicious
                    : ObservedTargetConsistencyState.Monitoring;

            sessionMonitoringTime += elapsedSeconds;
            sessionConsistencyIntegral += ConsistencyScore * elapsedSeconds;
            sessionPeakConsistencyScore = Math.Max(sessionPeakConsistencyScore, ConsistencyScore);

            switch (ConsistencyState)
            {
                case ObservedTargetConsistencyState.Suspicious:
                    sessionSuspiciousTime += elapsedSeconds;
                    break;

                case ObservedTargetConsistencyState.Divergent:
                    sessionSuspiciousTime += elapsedSeconds;
                    sessionDivergentTime += elapsedSeconds;
                    break;
            }

            if (ConsistencyState == ObservedTargetConsistencyState.Divergent && !divergenceLogged)
            {
                Logger.Log("Observed hit-object graph marked the current session as divergent.", LoggingTarget.Runtime, LogLevel.Important);
                divergenceLogged = true;
            }
        }

        private void addPressEvidence()
        {
            if (CurrentTarget == null || inputManager == null)
                return;

            Vector2 cursorPosition = inputManager.CurrentState.Mouse.Position;
            float authoritativeDistance = (cursorPosition - CurrentTarget.AuthoritativeScreenSpacePosition).Length;
            float observedDistance = (cursorPosition - CurrentTarget.ObservedScreenSpacePosition).Length;

            if (observedDistance + distance_margin < authoritativeDistance && observedDistance <= CurrentTarget.Radius * 1.15f)
            {
                ConsistencyScore = Math.Min(16, ConsistencyScore + 1.25);
                sessionPressEvidenceCount++;
            }
        }

        private void updateDebugOverlay()
        {
            bool visible = debugVisible.Value && IsMonitoringActive;

            if (!visible)
            {
                infoContainer.Hide();

                foreach (CircularContainer marker in debugMarkers)
                    marker.Hide();

                return;
            }

            List<ObservedHitObjectState> shiftedTargets = observedTargets
                .Where(target => target.MirrorOffset.LengthSquared > 0.001f)
                .Take(max_debug_targets)
                .ToList();

            ensureDebugMarkers(shiftedTargets.Count);

            for (int i = 0; i < debugMarkers.Count; i++)
            {
                if (i >= shiftedTargets.Count)
                {
                    debugMarkers[i].Hide();
                    continue;
                }

                ObservedHitObjectState state = shiftedTargets[i];
                CircularContainer marker = debugMarkers[i];

                marker.Show();
                marker.Position = ToLocalSpace(state.ObservedScreenSpacePosition);

                float radius = toLocalRadius(state.ObservedScreenSpacePosition, state.Radius);
                marker.Size = new Vector2(radius * 2);
                marker.BorderColour = getMarkerColour(state);
                marker.Colour = getMarkerFillColour(state);
            }

            infoContainer.Show();
            infoText.Text = buildDebugText();
        }

        private LocalisableString buildDebugText()
            => $"observed: {observedTargets.Count}\n"
             + $"moved: {observedTargets.Count(target => target.MirrorOffset.LengthSquared > 0.001f)}\n"
             + $"edge gap: {shiftPixels.Value:0.0}px\n"
             + $"state: {ConsistencyState}\n"
             + $"score: {ConsistencyScore:0.00}";

        private void ensureDebugMarkers(int requiredCount)
        {
            while (debugMarkers.Count < requiredCount)
            {
                var marker = createMarker();
                debugMarkers.Add(marker);
                markerContainer.Add(marker);
            }
        }

        private CircularContainer createMarker() => new CircularContainer
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.Centre,
            Masking = true,
            Alpha = 0.9f,
            BorderThickness = 2,
            Size = new Vector2(20),
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
            }
        };

        private Color4 getMarkerColour(ObservedHitObjectState state)
        {
            if (CurrentTarget != null && ReferenceEquals(CurrentTarget, state))
                return Color4.OrangeRed;

            if (NextTarget != null && ReferenceEquals(NextTarget, state))
                return Color4.Gold;

            return new Color4(120, 220, 255, 255);
        }

        private Color4 getMarkerFillColour(ObservedHitObjectState state)
        {
            if (CurrentTarget != null && ReferenceEquals(CurrentTarget, state))
                return new Color4(255, 90, 60, 60);

            if (NextTarget != null && ReferenceEquals(NextTarget, state))
                return new Color4(255, 210, 50, 50);

            return new Color4(120, 220, 255, 40);
        }

        private void clearState()
        {
            observedTargets.Clear();
            CurrentTarget = null;
            NextTarget = null;
            ConsistencyScore = 0;
            ConsistencyState = ObservedTargetConsistencyState.Inactive;
            divergenceLogged = false;

            foreach (CircularContainer marker in debugMarkers)
                marker.Hide();

            infoContainer.Hide();
        }

        private void updateSnapshot()
        {
            GameplayPerformanceSnapshot.ObservedHitObjectGraphActive = IsMonitoringActive;
            GameplayPerformanceSnapshot.ObservedHitObjectCount = observedTargets.Count;
            GameplayPerformanceSnapshot.ObservedHitObjectShiftPixels = IsMonitoringActive ? shiftPixels.Value : 0;
            GameplayPerformanceSnapshot.ObservedHitObjectState = ConsistencyState.ToString();
            GameplayPerformanceSnapshot.ObservedHitObjectScore = ConsistencyScore;
            GameplayPerformanceSnapshot.ObservedHitObjectMonitoringTime = sessionMonitoringTime;
            GameplayPerformanceSnapshot.ObservedHitObjectSuspiciousTime = sessionSuspiciousTime;
            GameplayPerformanceSnapshot.ObservedHitObjectDivergentTime = sessionDivergentTime;
            GameplayPerformanceSnapshot.ObservedHitObjectAverageScore = sessionMonitoringTime > 0
                ? sessionConsistencyIntegral / sessionMonitoringTime
                : 0;
            GameplayPerformanceSnapshot.ObservedHitObjectPeakScore = sessionPeakConsistencyScore;
            GameplayPerformanceSnapshot.ObservedHitObjectPressEvidenceCount = sessionPressEvidenceCount;
            GameplayPerformanceSnapshot.ObservedHitObjectShiftedTargetCount = shiftedSequenceIndicesSeen.Count;
        }

        private bool shouldObserveDrawable(DrawableOsuHitObject drawable)
            => drawable.IsLoaded
               && drawable is not DrawableSpinner
               && drawable is not DrawableSpinnerTick
               && drawable is not DrawableSpinnerBonusTick
               && drawable is not DrawableSliderHead
               && drawable is not DrawableSliderTail
               && drawable is not DrawableSliderTick
               && drawable is not DrawableSliderRepeat;

        private Vector2 computeMirrorOffset(DrawableOsuHitObject drawable, int sequenceIndex, float radius)
        {
            if (sequenceIndex < 0 || sequenceIndex == int.MaxValue)
                return Vector2.Zero;

            if ((sequenceIndex + 1) % 5 != 0)
                return Vector2.Zero;

            float outerGap = Math.Clamp(shiftPixels.Value, 0, 1.0f);
            float magnitude = radius * 2 + outerGap;

            if (magnitude <= 0)
                return Vector2.Zero;

            double interval = Math.Max(100, shiftIntervalMs.Value);
            int phase = (int)Math.Floor(drawable.HitObject.StartTime / interval);
            int stableSeed = getStableSeed(drawable, sequenceIndex);

            Vector2 direction = getMirrorDirection(stableSeed, phase);

            return direction * magnitude;
        }

        private static Vector2 getMirrorDirection(int stableSeed, int phase)
        {
            int directionIndex = Math.Abs(stableSeed + phase) % 8;

            return directionIndex switch
            {
                0 => new Vector2(1, 0),
                1 => new Vector2(-1, 0),
                2 => new Vector2(0, 1),
                3 => new Vector2(0, -1),
                4 => Vector2.Normalize(new Vector2(1, 1)),
                5 => Vector2.Normalize(new Vector2(-1, 1)),
                6 => Vector2.Normalize(new Vector2(1, -1)),
                _ => Vector2.Normalize(new Vector2(-1, -1)),
            };
        }

        private static int getStableSeed(DrawableOsuHitObject drawable, int index)
        {
            var hitObject = drawable.HitObject;

            unchecked
            {
                int seed = index * 397;
                seed = (seed * 397) ^ (int)Math.Round(hitObject.StartTime);
                seed = (seed * 397) ^ (int)Math.Round(hitObject.StackedPosition.X * 10);
                seed = (seed * 397) ^ (int)Math.Round(hitObject.StackedPosition.Y * 10);
                return seed;
            }
        }

        private Vector2 getDisplayCentre(DrawableOsuHitObject drawable)
        {
            switch (drawable)
            {
                case DrawableSlider slider when slider.HeadCircle.IsLoaded:
                    return slider.HeadCircle.ScreenSpaceDrawQuad.Centre;

                case DrawableHitCircle hitCircle when hitCircle.IsLoaded:
                    return hitCircle.ScreenSpaceDrawQuad.Centre;

                default:
                    return playfield!.GamefieldToScreenSpace(drawable.HitObject.StackedPosition);
            }
        }

        private float getDrawableTargetRadius(DrawableOsuHitObject drawable)
        {
            switch (drawable)
            {
                case DrawableSlider slider when slider.HeadCircle.IsLoaded:
                    return getDrawableScreenRadius(slider.HeadCircle.HitArea);

                case DrawableHitCircle hitCircle when hitCircle.IsLoaded:
                    return getDrawableScreenRadius(hitCircle.HitArea);

                default:
                    Vector2 centre = playfield!.GamefieldToScreenSpace(drawable.HitObject.StackedPosition);
                    Vector2 edge = playfield.GamefieldToScreenSpace(drawable.HitObject.StackedPosition + new Vector2((float)drawable.HitObject.Radius, 0));
                    return (edge - centre).Length;
            }
        }

        private static float getDrawableScreenRadius(Drawable drawable)
        {
            float width = (drawable.ScreenSpaceDrawQuad.TopRight - drawable.ScreenSpaceDrawQuad.TopLeft).Length;
            float height = (drawable.ScreenSpaceDrawQuad.BottomLeft - drawable.ScreenSpaceDrawQuad.TopLeft).Length;
            return Math.Min(width, height) * 0.5f;
        }

        private float toLocalRadius(Vector2 screenSpaceCentre, float screenSpaceRadius)
        {
            Vector2 localCentre = ToLocalSpace(screenSpaceCentre);
            Vector2 localEdge = ToLocalSpace(screenSpaceCentre + new Vector2(screenSpaceRadius, 0));
            return (localEdge - localCentre).Length;
        }

        private void rebuildSequenceIndexMap()
        {
            sequenceIndexByHitObject.Clear();

            if (beatmap == null)
                return;

            int index = 0;

            foreach (OsuHitObject hitObject in beatmap.HitObjects.OfType<OsuHitObject>())
            {
                if (!shouldTrackSequenceHitObject(hitObject))
                    continue;

                sequenceIndexByHitObject[hitObject] = index++;
            }
        }

        private int getSequenceIndex(DrawableOsuHitObject drawable)
        {
            if (sequenceIndexByHitObject.Count == 0)
                rebuildSequenceIndexMap();

            return sequenceIndexByHitObject.TryGetValue(drawable.HitObject, out int sequenceIndex)
                ? sequenceIndex
                : int.MaxValue;
        }

        private static bool shouldTrackSequenceHitObject(OsuHitObject hitObject)
            => hitObject is not Spinner;
    }

    public sealed class ObservedHitObjectState
    {
        public DrawableOsuHitObject SourceDrawable { get; private set; } = null!;

        public OsuHitObject SourceHitObject { get; private set; } = null!;

        public int SequenceIndex { get; private set; }

        public string Kind { get; private set; } = string.Empty;

        public double StartTime { get; private set; }

        public double EndTime { get; private set; }

        public Vector2 AuthoritativeScreenSpacePosition { get; private set; }

        public Vector2 ObservedScreenSpacePosition { get; private set; }

        public Vector2 AuthoritativeGamefieldPosition { get; private set; }

        public Vector2 ObservedGamefieldPosition { get; private set; }

        public Vector2 MirrorOffset { get; private set; }

        public float Radius { get; private set; }

        public void Update(
            DrawableOsuHitObject drawable,
            int sequenceIndex,
            Vector2 authoritativeScreenSpacePosition,
            Vector2 observedScreenSpacePosition,
            Vector2 authoritativeGamefieldPosition,
            Vector2 observedGamefieldPosition,
            Vector2 mirrorOffset,
            float radius)
        {
            SourceDrawable = drawable;
            SourceHitObject = drawable.HitObject;
            SequenceIndex = sequenceIndex;
            Kind = drawable switch
            {
                DrawableSlider => "slider",
                DrawableHitCircle => "circle",
                _ => drawable.GetType().Name,
            };
            StartTime = drawable.HitObject.StartTime;
            EndTime = drawable.HitObject is IHasDuration hasDuration ? hasDuration.EndTime : drawable.HitObject.StartTime;
            AuthoritativeScreenSpacePosition = authoritativeScreenSpacePosition;
            ObservedScreenSpacePosition = observedScreenSpacePosition;
            AuthoritativeGamefieldPosition = authoritativeGamefieldPosition;
            ObservedGamefieldPosition = observedGamefieldPosition;
            MirrorOffset = mirrorOffset;
            Radius = radius;
        }
    }

    public enum ObservedTargetConsistencyState
    {
        Inactive,
        Monitoring,
        Suspicious,
        Divergent,
    }
}
