// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeTriggerToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();
        private bool syncingSelection;
        private FillFlowContainer effectControls = null!;
        private Drawable colourControl = null!;
        private ExpandableSlider<int> durationSlider = null!;
        private ExpandableSlider<float> strengthSlider = null!;
        private Drawable sourceControls = null!;
        private Drawable moveSourceControl = null!;
        private Drawable targetControls = null!;
        private Drawable sizeControls = null!;
        private Drawable easingControl = null!;

        public DodgeTriggerToolboxGroup()
            : base(DodgeEditorStrings.Trigger)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                new SettingsEnumDropdown<DodgeTriggerAction>
                {
                    LabelText = DodgeEditorStrings.TriggerAction,
                    Current = settings.TriggerAction,
                    // Legacy v21 actions remain readable, but arena/camera authoring
                    // belongs to their existing tools rather than duplicate trigger controls.
                    Items = Enum.GetValues<DodgeTriggerAction>().Where(a => a is not (DodgeTriggerAction.ResizeArena or DodgeTriggerAction.CameraFollow or DodgeTriggerAction.CameraFixed)),
                },
                effectControls = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                    Children = new Drawable[]
                    {
                        strengthSlider = new ExpandableSlider<float>
                        {
                            ExpandedLabelText = DodgeEditorStrings.TriggerStrength,
                            Current = settings.TriggerStrength,
                            KeyboardStep = 0.05f,
                        },
                        durationSlider = new ExpandableSlider<int>
                        {
                            ExpandedLabelText = DodgeEditorStrings.DurationSnapUnits,
                            Current = settings.TriggerDurationSnapUnits,
                            KeyboardStep = 1,
                        },
                        colourControl = createColourControl(DodgeEditorStrings.TriggerColour, settings.TriggerColour),
                    },
                },
                moveSourceControl = new SettingsCheckbox
                {
                    LabelText = DodgeEditorStrings.MoveFromCurrentPosition,
                    Current = settings.TriggerMoveFromCurrent,
                },
                sourceControls = createCoordinates(DodgeEditorStrings.TriggerSourceX, settings.TriggerSourceX,
                    DodgeEditorStrings.TriggerSourceY, settings.TriggerSourceY),
                targetControls = createCoordinates(DodgeEditorStrings.TriggerTargetX, settings.TriggerTargetX,
                    DodgeEditorStrings.TriggerTargetY, settings.TriggerTargetY),
                sizeControls = createCoordinates(DodgeEditorStrings.TriggerWidth, settings.TriggerWidth,
                    DodgeEditorStrings.TriggerHeight, settings.TriggerHeight),
                easingControl = new SettingsEnumDropdown<DodgeCameraEasing>
                {
                    LabelText = DodgeEditorStrings.CameraEasing,
                    Current = settings.TriggerEasing,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            settings.TriggerAction.BindValueChanged(value =>
            {
                if (!syncingSelection && value.NewValue == DodgeTriggerAction.ResizeArena)
                {
                    syncingSelection = true;
                    try
                    {
                        settings.TriggerTargetX.Value = 0;
                        settings.TriggerTargetY.Value = 0;
                    }
                    finally
                    {
                        syncingSelection = false;
                    }
                }
                applyToSelection(trigger =>
                {
                    bool wasTimedEffect = trigger.IsTimedEffect;
                    bool enteringResize = trigger.Action != DodgeTriggerAction.ResizeArena && value.NewValue == DodgeTriggerAction.ResizeArena;
                    trigger.Action = value.NewValue;
                    if (enteringResize)
                    {
                        trigger.TargetPosition = Vector2.Zero;
                        trigger.TargetSize = new Vector2(settings.TriggerWidth.Value, settings.TriggerHeight.Value);
                    }
                    if (!trigger.IsTimedEffect)
                        trigger.Duration = 0;
                    else if (!wasTimedEffect)
                        trigger.Duration = durationAt(trigger.StartTime);
                });
                updateControlVisibility();
            }, true);
            settings.TriggerStrength.BindValueChanged(value =>
            {
                applyToSelection(trigger => trigger.Strength = value.NewValue, trigger => trigger.Action is DodgeTriggerAction.ScreenShake or DodgeTriggerAction.FlashEffect);
                strengthSlider.ContractedLabelText = LocalisableString.Interpolate($"{DodgeEditorStrings.TriggerStrength}: {value.NewValue:P0}");
            }, true);
            settings.TriggerDurationSnapUnits.BindValueChanged(_ =>
                applyToSelection(trigger => trigger.Duration = durationAt(trigger.StartTime), trigger => trigger.IsTimedEffect));
            settings.TriggerColour.BindValueChanged(value =>
                applyToSelection(trigger => trigger.Colour = value.NewValue, trigger => trigger.Action == DodgeTriggerAction.FlashEffect));
            settings.TriggerMoveFromCurrent.BindValueChanged(v =>
            {
                applyToSelection(t => t.MoveFromCurrentPosition = v.NewValue, isMove);
                updateControlVisibility();
            });
            settings.TriggerSourceX.BindValueChanged(v => applyToSelection(t => t.SourcePosition = new Vector2(v.NewValue, t.SourcePosition.Y), isMove));
            settings.TriggerSourceY.BindValueChanged(v => applyToSelection(t => t.SourcePosition = new Vector2(t.SourcePosition.X, v.NewValue), isMove));
            settings.TriggerTargetX.BindValueChanged(v => applyToSelection(t => t.TargetPosition = new Vector2(v.NewValue, t.TargetPosition.Y), isSpatial));
            settings.TriggerTargetY.BindValueChanged(v => applyToSelection(t => t.TargetPosition = new Vector2(t.TargetPosition.X, v.NewValue), isSpatial));
            settings.TriggerWidth.BindValueChanged(v => applyToSelection(t => t.TargetSize = new Vector2(v.NewValue, t.TargetSize.Y), isResize));
            settings.TriggerHeight.BindValueChanged(v => applyToSelection(t => t.TargetSize = new Vector2(t.TargetSize.X, v.NewValue), isResize));
            settings.TriggerEasing.BindValueChanged(v => applyToSelection(t => t.Easing = v.NewValue, isSpatial));
            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => readFromSelection(), true);
            editorBeatmap.HitObjectUpdated += onHitObjectUpdated;
        }

        private double durationAt(double time)
            => beatSnapProvider.GetBeatLengthAtTime(time) * settings.TriggerDurationSnapUnits.Value;

        protected override void Update()
        {
            base.Update();
            double time = selectedHitObjects.OfType<DodgeTrigger>().FirstOrDefault()?.StartTime ?? editorClock.CurrentTime;
            durationSlider.ContractedLabelText = DodgeEditorStrings.DurationValue(settings.TriggerDurationSnapUnits.Value, durationAt(time));
        }

        private void updateControlVisibility()
        {
            DodgeTriggerAction action = settings.TriggerAction.Value;
            bool spatial = action is DodgeTriggerAction.ResizeArena or DodgeTriggerAction.MovePlayer;
            effectControls.Alpha = spatial || action is DodgeTriggerAction.ScreenShake or DodgeTriggerAction.FlashEffect ? 1 : 0;
            strengthSlider.Alpha = action is DodgeTriggerAction.ScreenShake or DodgeTriggerAction.FlashEffect ? 1 : 0;
            colourControl.Alpha = action == DodgeTriggerAction.FlashEffect ? 1 : 0;
            moveSourceControl.Alpha = action == DodgeTriggerAction.MovePlayer ? 1 : 0;
            sourceControls.Alpha = action == DodgeTriggerAction.MovePlayer && !settings.TriggerMoveFromCurrent.Value ? 1 : 0;
            targetControls.Alpha = easingControl.Alpha = spatial ? 1 : 0;
            sizeControls.Alpha = action == DodgeTriggerAction.ResizeArena ? 1 : 0;
        }

        private void onHitObjectUpdated(HitObject hitObject)
        {
            if (selectedHitObjects.Contains(hitObject))
                Scheduler.AddOnce(readFromSelection);
        }

        private void readFromSelection()
        {
            DodgeTrigger? trigger = selectedHitObjects.OfType<DodgeTrigger>().FirstOrDefault();
            if (trigger == null)
                return;

            syncingSelection = true;
            try
            {
                settings.TriggerAction.Value = trigger.Action;
                settings.TriggerStrength.Value = trigger.Strength;
                settings.TriggerColour.Value = trigger.Colour;
                settings.TriggerMoveFromCurrent.Value = trigger.MoveFromCurrentPosition;
                settings.TriggerSourceX.Value = trigger.SourcePosition.X;
                settings.TriggerSourceY.Value = trigger.SourcePosition.Y;
                settings.TriggerTargetX.Value = trigger.TargetPosition.X;
                settings.TriggerTargetY.Value = trigger.TargetPosition.Y;
                settings.TriggerWidth.Value = trigger.TargetSize.X;
                settings.TriggerHeight.Value = trigger.TargetSize.Y;
                settings.TriggerEasing.Value = trigger.Easing;
                double snapLength = beatSnapProvider.GetBeatLengthAtTime(trigger.StartTime);
                if (trigger.IsTimedEffect && snapLength > 0)
                {
                    settings.TriggerDurationSnapUnits.Value = Math.Clamp((int)Math.Round(trigger.Duration / snapLength),
                        settings.TriggerDurationSnapUnits.MinValue, settings.TriggerDurationSnapUnits.MaxValue);
                }
            }
            finally
            {
                syncingSelection = false;
            }
            updateControlVisibility();
        }

        private void applyToSelection(Action<DodgeTrigger> apply, Func<DodgeTrigger, bool>? predicate = null)
        {
            if (syncingSelection)
                return;

            DodgeTrigger[] triggers = selectedHitObjects.OfType<DodgeTrigger>().Where(trigger => predicate?.Invoke(trigger) != false).ToArray();
            if (triggers.Length == 0)
                return;

            editorBeatmap.BeginChange();
            try
            {
                foreach (DodgeTrigger trigger in triggers)
                {
                    apply(trigger);
                    DodgeEditorTrackBounds.Constrain(trigger, editorClock.TrackLength, true);
                    editorBeatmap.Update(trigger);
                }
            }
            finally
            {
                editorBeatmap.EndChange();
            }
        }

        private static bool isMove(DodgeTrigger t) => t.Action == DodgeTriggerAction.MovePlayer;
        private static bool isResize(DodgeTrigger t) => t.Action == DodgeTriggerAction.ResizeArena;
        private static bool isSpatial(DodgeTrigger t) => isMove(t) || isResize(t);

        private static Drawable createCoordinates(LocalisableString xLabel, BindableFloat x, LocalisableString yLabel, BindableFloat y)
            => new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new ExpandableSlider<float> { ExpandedLabelText = xLabel, Current = x, KeyboardStep = 1 },
                    new ExpandableSlider<float> { ExpandedLabelText = yLabel, Current = y, KeyboardStep = 1 },
                },
            };

        private static Drawable createColourControl(LocalisableString label, Bindable<Colour4> colour) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(3),
            Children = new Drawable[]
            {
                new OsuSpriteText { Text = label },
                new SettingsColour.ColourControl
                {
                    Height = 32,
                    Current = colour,
                },
            },
        };

        protected override void Dispose(bool isDisposing)
        {
            if (editorBeatmap != null)
                editorBeatmap.HitObjectUpdated -= onHitObjectUpdated;
            base.Dispose(isDisposing);
        }
    }
}
