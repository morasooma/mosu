// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges;
using osu.Framework.Input.States;
using osu.Framework.Lists;
using osu.Game.Configuration;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osuTK;

namespace osu.Game.Rulesets.Osu
{
    public partial class OsuInputManager : RulesetInputManager<OsuAction>, IPositionalMissProvider
    {
        private const double virtual_cursor_delay = 200;

        public SlimReadOnlyListWrapper<OsuAction> PressedActions => KeyBindingContainer.PressedActions;

        /// <summary>
        /// Whether gameplay input buttons should be allowed.
        /// Defaults to <c>true</c>, generally used for mods like Relax which turn off main inputs.
        /// </summary>
        /// <remarks>
        /// Of note, auxiliary inputs like the "smoke" key are left usable.
        /// </remarks>
        public bool AllowGameplayInputs
        {
            get => ((OsuKeyBindingContainer)KeyBindingContainer).AllowGameplayInputs;
            set => ((OsuKeyBindingContainer)KeyBindingContainer).AllowGameplayInputs = value;
        }

        /// <summary>
        /// Whether the user's cursor movement events should be accepted.
        /// Can be used to block only movement while still accepting button input.
        /// </summary>
        public bool AllowUserCursorMovement { get; set; } = true;

        /// <summary>
        /// The most recent real user cursor position received by this input manager.
        /// This is preserved even while gameplay is being driven by a virtual cursor.
        /// </summary>
        public Vector2 OriginalUserCursorPosition { get; private set; }

        public bool HasOriginalUserCursorPosition { get; private set; }

        /// <summary>
        /// Whether gameplay is currently being driven by a virtual cursor position.
        /// </summary>
        public bool IsVirtualCursorActive => externalVirtualCursorActive || virtualCursorDelayEnabled.Value;

        public Vector2 VirtualCursorPosition { get; private set; }

        /// <summary>
        /// Whether the currently-dispatched mouse movement came from real user input while an external virtual cursor owns gameplay movement.
        /// Consumers which observe raw mouse movement should ignore it to avoid briefly mixing the two cursor paths.
        /// </summary>
        internal bool ShouldSuppressPhysicalCursorMove => externalVirtualCursorActive && syntheticCursorMoves == 0;

        public Vector2 ReplayCursorPosition { get; private set; }

        public bool HasReplayCursorPosition { get; private set; }

        public bool ReplayBotActive { get; private set; }

        private readonly BindableBool virtualCursorDelayEnabled = new BindableBool();
        private readonly List<CursorSample> originalCursorHistory = new List<CursorSample>();
        private readonly Stopwatch cursorDelayClock = Stopwatch.StartNew();

        private bool externalVirtualCursorActive;
        private int syntheticCursorMoves;

        public event Action<double>? NewPositionalMiss;

        protected override KeyBindingContainer<OsuAction> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
        {
            var container = new OsuKeyBindingContainer(ruleset, variant, unique);
            container.UnhandledGameplayPress += handleUnhandledGameplayPress;
            return container;
        }

        private void handleUnhandledGameplayPress()
        {
            Action<double>? newPositionalMiss = NewPositionalMiss;

            if (newPositionalMiss == null)
                return;

            double? closestOffset = null;

            foreach (DrawableHitCircle.HitReceptor receptor in NonPositionalInputQueue.OfType<DrawableHitCircle.HitReceptor>())
            {
                if (!receptor.CanBeHit() || receptor.GetPositionalMissTimeOffset() is not double offset)
                    continue;

                if (!closestOffset.HasValue || Math.Abs(offset) < Math.Abs(closestOffset.Value))
                    closestOffset = offset;
            }

            if (closestOffset.HasValue)
                newPositionalMiss.Invoke(closestOffset.Value);
        }

        public bool CheckScreenSpaceActionPressJudgeable(Vector2 screenSpacePosition) =>
            // This is a very naive but simple approach.
            //
            // Based on user feedback of more nuanced scenarios (where touch doesn't behave as expected),
            // this can be expanded to a more complex implementation, but I'd still want to keep it as simple as we can.
            NonPositionalInputQueue.OfType<DrawableHitCircle.HitReceptor>().Any(c => c.CanBeHit() && c.ReceivePositionalInputAt(screenSpacePosition));

        public OsuInputManager(RulesetInfo ruleset)
            : base(ruleset, 0, SimultaneousBindingMode.Unique)
        {
        }

        /// <summary>
        /// Applies a virtual cursor position to gameplay while keeping the user's real cursor position available via
        /// <see cref="OriginalUserCursorPosition"/>.
        /// </summary>
        public void MoveVirtualCursorTo(Vector2 screenSpacePosition)
        {
            externalVirtualCursorActive = true;
            applyCursorPosition(screenSpacePosition);
        }

        /// <summary>
        /// Dispatches a press at a cursor sample crossed within this frame, then restores the current position.
        /// Does not take ownership of the cursor or alter the original user input.
        /// </summary>
        internal void ApplyCursorSampleForPress(Vector2 screenSpacePosition, Action press)
        {
            Vector2 currentPosition = CurrentState.Mouse.Position;
            try
            {
                applyCursorPosition(screenSpacePosition);
                press();
            }
            finally
            {
                applyCursorPosition(currentPosition);
            }
        }

        public void HandleUserCursorMovement(Vector2 screenSpacePosition)
        {
            handleOriginalCursorMovement(screenSpacePosition);

            if (ReplayInputHandler != null)
                return;

            if (!AllowUserCursorMovement || externalVirtualCursorActive)
                return;

            if (virtualCursorDelayEnabled.Value)
            {
                updateDelayedCursorPosition();
                return;
            }

            applyCursorPosition(screenSpacePosition);
        }

        /// <summary>
        /// Stops driving gameplay from the virtual cursor.
        /// </summary>
        /// <param name="restoreOriginalPosition">
        /// Whether gameplay should snap back to the last recorded real user cursor position.
        /// </param>
        public void ResetVirtualCursor(bool restoreOriginalPosition = true)
        {
            externalVirtualCursorActive = false;

            if (!restoreOriginalPosition || virtualCursorDelayEnabled.Value)
                return;

            if (ReplayBotActive)
            {
                if (HasReplayCursorPosition)
                    applyCursorPosition(ReplayCursorPosition);

                return;
            }

            if (HasOriginalUserCursorPosition)
                applyCursorPosition(OriginalUserCursorPosition);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            OriginalUserCursorPosition = CurrentState.Mouse.Position;
            HasOriginalUserCursorPosition = true;
            VirtualCursorPosition = CurrentState.Mouse.Position;

            config.BindWith(OsuSetting.ForkVirtualCursorInputDelay, virtualCursorDelayEnabled);
            virtualCursorDelayEnabled.BindValueChanged(delay =>
            {
                originalCursorHistory.Clear();

                if (HasOriginalUserCursorPosition)
                    originalCursorHistory.Add(new CursorSample(currentDelayTime - virtual_cursor_delay, OriginalUserCursorPosition));

                if (delay.NewValue)
                {
                    if (HasOriginalUserCursorPosition)
                        applyCursorPosition(OriginalUserCursorPosition);
                }
                else if (!externalVirtualCursorActive && HasOriginalUserCursorPosition)
                    applyCursorPosition(OriginalUserCursorPosition);
            }, true);

            Add(new OsuTouchInputMapper(this) { RelativeSizeAxes = Axes.Both });
        }

        protected override bool Handle(UIEvent e)
        {
            if (syntheticCursorMoves == 0)
            {
                switch (e)
                {
                    case MouseMoveEvent mouseMove:
                        handleOriginalCursorMovement(mouseMove.ScreenSpaceMousePosition);

                        if (externalVirtualCursorActive)
                        {
                            // The framework has already applied the physical mouse position to CurrentState before dispatching
                            // this event. Restore virtual ownership immediately so the gameplay cursor cannot flicker between paths.
                            applyCursorPosition(VirtualCursorPosition);
                            return false;
                        }

                        if (virtualCursorDelayEnabled.Value || !AllowUserCursorMovement)
                        {
                            if (virtualCursorDelayEnabled.Value)
                                updateDelayedCursorPosition();

                            return false;
                        }

                        break;

                    case MouseDownEvent mouseDown:
                        updateOriginalCursorPosition(mouseDown.ScreenSpaceMouseDownPosition);
                        break;
                }
            }

            return base.Handle(e);
        }

        protected override void Update()
        {
            base.Update();

            if (virtualCursorDelayEnabled.Value && !externalVirtualCursorActive)
                updateDelayedCursorPosition();
        }

        private void handleOriginalCursorMovement(Vector2 screenSpacePosition)
        {
            updateOriginalCursorPosition(screenSpacePosition);

            if (!virtualCursorDelayEnabled.Value)
                return;

            originalCursorHistory.Add(new CursorSample(currentDelayTime, screenSpacePosition));
        }

        private void updateOriginalCursorPosition(Vector2 screenSpacePosition)
        {
            OriginalUserCursorPosition = screenSpacePosition;
            HasOriginalUserCursorPosition = true;
        }

        internal void UpdateReplayCursorPosition(Vector2 screenSpacePosition)
        {
            ReplayCursorPosition = screenSpacePosition;
            HasReplayCursorPosition = true;
        }

        internal void SetReplayBotActive(bool active)
        {
            ReplayBotActive = active;

            if (!active)
                HasReplayCursorPosition = false;
        }

        private void updateDelayedCursorPosition()
        {
            if (!HasOriginalUserCursorPosition)
                return;

            Vector2 delayedPosition = getDelayedCursorPosition();

            if (VirtualCursorPosition == delayedPosition)
                return;

            applyCursorPosition(delayedPosition);
        }

        private Vector2 getDelayedCursorPosition()
        {
            if (originalCursorHistory.Count == 0)
                return OriginalUserCursorPosition;

            double targetTime = currentDelayTime - virtual_cursor_delay;

            while (originalCursorHistory.Count > 2 && originalCursorHistory[1].Time <= targetTime)
                originalCursorHistory.RemoveAt(0);

            if (targetTime <= originalCursorHistory[0].Time)
                return originalCursorHistory[0].Position;

            if (originalCursorHistory.Count == 1 || targetTime >= originalCursorHistory[^1].Time)
                return originalCursorHistory[^1].Position;

            for (int i = 0; i < originalCursorHistory.Count - 1; i++)
            {
                CursorSample start = originalCursorHistory[i];
                CursorSample end = originalCursorHistory[i + 1];

                if (targetTime <= end.Time)
                {
                    float t = (float)((targetTime - start.Time) / (end.Time - start.Time));
                    return start.Position + (end.Position - start.Position) * t;
                }
            }

            return originalCursorHistory[^1].Position;
        }

        private void applyCursorPosition(Vector2 screenSpacePosition)
        {
            screenSpacePosition = clampCursorPositionToScreenBounds(screenSpacePosition);
            VirtualCursorPosition = screenSpacePosition;
            syntheticCursorMoves++;

            try
            {
                new MousePositionAbsoluteInput { Position = screenSpacePosition }.Apply(CurrentState, this);
            }
            finally
            {
                syntheticCursorMoves--;
            }
        }

        private Vector2 clampCursorPositionToScreenBounds(Vector2 screenSpacePosition)
        {
            var containingInputManager = GetContainingInputManager();
            var bounds = containingInputManager?.ScreenSpaceDrawQuad.AABBFloat ?? ScreenSpaceDrawQuad.AABBFloat;

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return screenSpacePosition;

            float maximumReasonableOverflowX = Math.Max(bounds.Width * 2f, 1024f);
            float maximumReasonableOverflowY = Math.Max(bounds.Height * 2f, 1024f);

            if (screenSpacePosition.X < bounds.Left - maximumReasonableOverflowX
                || screenSpacePosition.X > bounds.Right + maximumReasonableOverflowX
                || screenSpacePosition.Y < bounds.Top - maximumReasonableOverflowY
                || screenSpacePosition.Y > bounds.Bottom + maximumReasonableOverflowY)
                return screenSpacePosition;

            return Vector2.Clamp(screenSpacePosition, bounds.TopLeft, bounds.BottomRight);
        }

        private double currentDelayTime => cursorDelayClock.Elapsed.TotalMilliseconds;

        private readonly record struct CursorSample(double Time, Vector2 Position);

        public class ReplayCursorPositionInput : IInput
        {
            public Vector2 Position;

            public void Apply(InputState state, IInputStateChangeHandler handler)
            {
                if (handler is OsuInputManager osuInputManager)
                    osuInputManager.UpdateReplayCursorPosition(Position);

                new MousePositionAbsoluteInput { Position = Position }.Apply(state, handler);
            }
        }

        private partial class OsuKeyBindingContainer : RulesetKeyBindingContainer
        {
            private bool allowGameplayInputs = true;

            public event Action? UnhandledGameplayPress;

            /// <summary>
            /// Whether gameplay input buttons should be allowed.
            /// Defaults to <c>true</c>, generally used for mods like Relax which turn off main inputs.
            /// </summary>
            /// <remarks>
            /// Of note, auxiliary inputs like the "smoke" key are left usable.
            /// </remarks>
            public bool AllowGameplayInputs
            {
                get => allowGameplayInputs;
                set
                {
                    allowGameplayInputs = value;
                    ReloadMappings();
                }
            }

            public OsuKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
                : base(ruleset, variant, unique)
            {
            }

            protected override void ReloadMappings(IQueryable<RealmKeyBinding> realmKeyBindings)
            {
                base.ReloadMappings(realmKeyBindings);

                if (!AllowGameplayInputs)
                    KeyBindings = KeyBindings.Where(static b => b.GetAction<OsuAction>() == OsuAction.Smoke).ToList();
            }

            protected override Drawable? PropagatePressed(IEnumerable<Drawable> drawables, InputState state, OsuAction pressed, float scrollAmount = 0, bool isPrecise = false, bool repeat = false)
            {
                bool wasAlreadyPressed = PressedActions.Contains(pressed);
                Drawable? handledBy = base.PropagatePressed(drawables, state, pressed, scrollAmount, isPrecise, repeat);

                if (!wasAlreadyPressed && !repeat && handledBy == null && pressed is OsuAction.LeftButton or OsuAction.RightButton)
                    UnhandledGameplayPress?.Invoke();

                return handledBy;
            }
        }
    }

    public enum OsuAction
    {
        [Description("Left button")]
        LeftButton,

        [Description("Right button")]
        RightButton,

        [Description("Smoke")]
        Smoke,

        [Description("Hit circle tool")]
        EditorHitCircleTool = 10000,

        [Description("Slider tool")]
        EditorSliderTool,

        [Description("Spinner tool")]
        EditorSpinnerTool,

        [Description("Grid from points tool")]
        EditorGridFromPointsTool,

        [Description("Toggle grid snap")]
        EditorToggleGridSnap,

        [Description("Toggle distance snap")]
        EditorToggleDistanceSnap,
    }
}
