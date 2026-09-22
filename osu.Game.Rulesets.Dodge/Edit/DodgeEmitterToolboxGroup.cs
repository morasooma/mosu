// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeEmitterToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();
        private bool syncingSelection;

        private ExpandableSlider<int> countSlider = null!;
        private ExpandableSlider<float> spreadSlider = null!;
        private FillFlowContainer repeatControls = null!;
        private FillFlowContainer appearanceControls = null!;
        private ExpandableButton appearanceButton = null!;
        private FillFlowContainer waveControls = null!;

        public BindableInt BulletCount => settings.EmitterBulletCount;
        public BindableFloat SpreadAngle => settings.EmitterSpreadAngle;
        public BindableBool ContinueUntilExit => settings.EmitterContinueUntilExit;
        public BindableBool Repeating => settings.EmitterRepeating;
        public BindableBool Moving => settings.EmitterMoving;
        public BindableInt BurstCount => settings.EmitterBurstCount;
        public Bindable<DodgeEmitterBeatDivisor> BurstBeatDivisor => settings.EmitterBurstBeatDivisor;
        public Bindable<Colour4> ProjectileColour => settings.EmitterColour;
        public bool RepeatControlsVisible => repeatControls.IsPresent;
        public bool AppearanceControlsVisible => appearanceControls.Alpha > 0;
        public bool ShowAppearance
        {
            get => AppearanceControlsVisible;
            set => setAppearanceVisibility(value);
        }

        public DodgeEmitterToolboxGroup()
            : base(DodgeEditorStrings.Emitter)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new osuTK.Vector2(5);
            Children = new Drawable[]
            {
                countSlider = new ExpandableSlider<int>
                {
                    ExpandedLabelText = DodgeEditorStrings.BulletCount,
                    Current = settings.EmitterBulletCount,
                    KeyboardStep = 1,
                },
                spreadSlider = new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.SpreadAngle,
                    Current = settings.EmitterSpreadAngle,
                    KeyboardStep = 1,
                },
                new OsuCheckbox
                {
                    RelativeSizeAxes = Axes.X,
                    LabelText = DodgeEditorStrings.RepeatEmitter,
                    Current = settings.EmitterRepeating,
                },
                repeatControls = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                    Children = new Drawable[]
                    {
                        new ExpandableSlider<int>
                        {
                            ExpandedLabelText = DodgeEditorStrings.BurstCount,
                            Current = settings.EmitterBurstCount,
                            KeyboardStep = 1,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(3),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText { Text = DodgeEditorStrings.BurstInterval },
                                new OsuEnumDropdown<DodgeEmitterBeatDivisor>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Current = settings.EmitterBurstBeatDivisor,
                                },
                            },
                        },
                        new OsuCheckbox
                        {
                            RelativeSizeAxes = Axes.X,
                            LabelText = DodgeEditorStrings.MoveEmitter,
                            Current = settings.EmitterMoving,
                        },
                        new ExpandableSlider<float>
                        {
                            ExpandedLabelText = DodgeEditorStrings.BurstRotation,
                            Current = settings.EmitterBurstRotation,
                            KeyboardStep = 1,
                        },
                    },
                },
                new OsuCheckbox
                {
                    RelativeSizeAxes = Axes.X,
                    LabelText = DodgeEditorStrings.ContinueUntilOutside,
                    Current = settings.EmitterContinueUntilExit,
                },
                createEnumControl(DodgeEditorStrings.TrajectoryGuide, settings.EmitterTrajectoryGuideStyle),
                createEnumControl(DodgeEditorStrings.MovementType, settings.EmitterMovementType),
                createEnumControl(DodgeEditorStrings.MovementEasing, settings.EmitterMovementEasing),
                waveControls = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                    Children = new Drawable[]
                    {
                        new ExpandableSlider<float>
                        {
                            ExpandedLabelText = DodgeEditorStrings.WaveAmplitude,
                            Current = settings.EmitterWaveAmplitude,
                            KeyboardStep = 1,
                        },
                        new ExpandableSlider<int>
                        {
                            ExpandedLabelText = DodgeEditorStrings.WaveCycles,
                            Current = settings.EmitterWaveCycles,
                            KeyboardStep = 1,
                        },
                        new ExpandableSlider<float>
                        {
                            ExpandedLabelText = DodgeEditorStrings.WavePhase,
                            Current = settings.EmitterWavePhase,
                            KeyboardStep = 1,
                        },
                    },
                },
                appearanceButton = createAppearanceButton(),
                appearanceControls = createAppearanceControls(),
            };
        }

        private ExpandableButton createAppearanceButton() => new ExpandableButton
        {
            RelativeSizeAxes = Axes.X,
            ContractedLabelText = DodgeEditorStrings.ShowAppearance,
            ExpandedLabelText = DodgeEditorStrings.ShowAppearance,
            Action = toggleAppearance,
        };

        private FillFlowContainer createAppearanceControls() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(5),
            Alpha = 0,
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(3),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = DodgeEditorStrings.ProjectileShape },
                        new OsuEnumDropdown<DodgeBulletShape>
                        {
                            RelativeSizeAxes = Axes.X,
                            Current = settings.EmitterShape,
                        },
                    },
                },
                createColourControl(DodgeEditorStrings.FillColour, settings.EmitterColour),
                createColourControl(DodgeEditorStrings.OutlineColour, settings.EmitterOutlineColour),
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.Opacity,
                    Current = settings.EmitterOpacity,
                    KeyboardStep = 0.05f,
                },
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.OutlineThickness,
                    Current = settings.EmitterOutlineThickness,
                    KeyboardStep = 0.25f,
                },
            },
        };

        private void toggleAppearance()
            => setAppearanceVisibility(!AppearanceControlsVisible);

        private void setAppearanceVisibility(bool show)
        {
            appearanceControls.Alpha = show ? 1 : 0;
            appearanceButton.ContractedLabelText = appearanceButton.ExpandedLabelText =
                show ? DodgeEditorStrings.HideAppearance : DodgeEditorStrings.ShowAppearance;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            settings.EmitterBulletCount.BindValueChanged(count =>
            {
                countSlider.ContractedLabelText = DodgeEditorStrings.Count(count.NewValue);
                applyToSelection();
            }, true);
            settings.EmitterSpreadAngle.BindValueChanged(spread =>
            {
                spreadSlider.ContractedLabelText = DodgeEditorStrings.Spread(spread.NewValue);
                applyToSelection();
            }, true);

            settings.EmitterContinueUntilExit.BindValueChanged(_ => applyToSelection());
            settings.EmitterTrajectoryGuideStyle.BindValueChanged(_ => applyToSelection());
            settings.EmitterMovementType.BindValueChanged(type =>
            {
                waveControls.Alpha = type.NewValue == DodgeMovementType.Sine ? 1 : 0;
                applyToSelection();
            }, true);
            settings.EmitterMovementEasing.BindValueChanged(_ => applyToSelection());
            settings.EmitterWaveAmplitude.BindValueChanged(_ => applyToSelection());
            settings.EmitterWaveCycles.BindValueChanged(_ => applyToSelection());
            settings.EmitterWavePhase.BindValueChanged(_ => applyToSelection());
            settings.EmitterShape.BindValueChanged(_ => applyToSelection());
            settings.EmitterRepeating.BindValueChanged(repeating =>
            {
                repeatControls.Alpha = repeating.NewValue ? 1 : 0;
                applyToSelection();
            }, true);
            settings.EmitterMoving.BindValueChanged(_ => applyToSelection());
            settings.EmitterBurstCount.BindValueChanged(_ => applyToSelection());
            settings.EmitterBurstBeatDivisor.BindValueChanged(_ => applyToSelection());
            settings.EmitterBurstRotation.BindValueChanged(_ => applyToSelection());
            settings.EmitterColour.BindValueChanged(_ => applyToSelection());
            settings.EmitterOutlineColour.BindValueChanged(_ => applyToSelection());
            settings.EmitterOpacity.BindValueChanged(_ => applyToSelection());
            settings.EmitterOutlineThickness.BindValueChanged(_ => applyToSelection());

            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => readFromSelection());
        }

        private void readFromSelection()
        {
            DodgeEmitter? emitter = selectedHitObjects.OfType<DodgeEmitter>().FirstOrDefault();

            if (emitter == null)
                return;

            syncingSelection = true;
            settings.EmitterBulletCount.Value = emitter.EffectiveBulletCount;
            settings.EmitterSpreadAngle.Value = emitter.EffectiveSpreadAngle;
            settings.EmitterContinueUntilExit.Value = emitter.ContinueUntilExit;
            settings.EmitterTrajectoryGuideStyle.Value = emitter.TrajectoryGuideStyle;
            settings.EmitterMovementType.Value = emitter.MovementType;
            settings.EmitterMovementEasing.Value = emitter.MovementEasing;
            settings.EmitterWaveAmplitude.Value = emitter.WaveAmplitude;
            settings.EmitterWaveCycles.Value = Math.Max(1, emitter.WaveCycles);
            settings.EmitterWavePhase.Value = emitter.WavePhase;
            settings.EmitterShape.Value = emitter.Shape;
            settings.EmitterRepeating.Value = emitter.EffectiveBurstCount > 1;
            settings.EmitterMoving.Value = emitter.MoveSource;
            settings.EmitterBurstCount.Value = Math.Max(2, emitter.EffectiveBurstCount);
            settings.EmitterBurstBeatDivisor.Value = inferBeatDivisor(emitter);
            settings.EmitterBurstRotation.Value = emitter.BurstRotation;
            settings.EmitterColour.Value = emitter.Colour;
            settings.EmitterOutlineColour.Value = emitter.OutlineColour;
            settings.EmitterOpacity.Value = emitter.Opacity;
            settings.EmitterOutlineThickness.Value = emitter.OutlineThickness;
            syncingSelection = false;
        }

        private void applyToSelection()
        {
            if (syncingSelection)
                return;

            var emitters = selectedHitObjects.OfType<DodgeEmitter>().ToArray();

            if (emitters.Length == 0)
                return;

            editorBeatmap.BeginChange();

            foreach (var emitter in emitters)
            {
                bool wasMoving = emitter.MoveSource;
                emitter.BulletCount = settings.EmitterBulletCount.Value;
                emitter.SpreadAngle = settings.EmitterSpreadAngle.Value;
                emitter.ContinueUntilExit = settings.EmitterContinueUntilExit.Value;
                emitter.TrajectoryGuideStyle = settings.EmitterTrajectoryGuideStyle.Value;
                emitter.MovementType = settings.EmitterMovementType.Value;
                emitter.MovementEasing = settings.EmitterMovementEasing.Value;
                emitter.WaveAmplitude = settings.EmitterWaveAmplitude.Value;
                emitter.WaveCycles = settings.EmitterWaveCycles.Value;
                emitter.WavePhase = settings.EmitterWavePhase.Value;
                emitter.Shape = settings.EmitterShape.Value;
                emitter.BurstCount = settings.EmitterRepeating.Value
                    ? settings.EmitterBurstCount.Value
                    : DodgeEmitter.MIN_BURST_COUNT;
                emitter.BurstBeatDivisor = (int)settings.EmitterBurstBeatDivisor.Value;
                emitter.BurstInterval = intervalAt(emitter.StartTime, emitter.BurstBeatDivisor);
                emitter.BurstRotation = settings.EmitterBurstRotation.Value;
                emitter.MoveSource = settings.EmitterRepeating.Value && settings.EmitterMoving.Value;
                emitter.Colour = settings.EmitterColour.Value;
                emitter.OutlineColour = settings.EmitterOutlineColour.Value;
                emitter.Opacity = settings.EmitterOpacity.Value;
                emitter.OutlineThickness = settings.EmitterOutlineThickness.Value;

                if (!emitter.MoveSource)
                {
                    emitter.MovementEndPosition = emitter.Position;
                }
                else if (!wasMoving || emitter.MovementEndPosition == emitter.Position)
                {
                    emitter.MovementEndPosition = Vector2.ComponentMin(
                        emitter.Position + new Vector2(96, 0),
                        DodgePlayfield.BASE_SIZE);
                }

                editorBeatmap.Update(emitter);
            }

            editorBeatmap.EndChange();
        }

        private DodgeEmitterBeatDivisor inferBeatDivisor(DodgeEmitter emitter)
        {
            double beatLength = editorBeatmap.ControlPointInfo.TimingPointAt(emitter.StartTime).BeatLength;

            if (emitter.BurstBeatDivisor > 0 && Enum.IsDefined(typeof(DodgeEmitterBeatDivisor), emitter.BurstBeatDivisor))
                return (DodgeEmitterBeatDivisor)emitter.BurstBeatDivisor;

            return Enum.GetValues<DodgeEmitterBeatDivisor>()
                       .OrderBy(divisor => Math.Abs(DodgeEmitter.IntervalForBeatLength(beatLength, (int)divisor) - emitter.EffectiveBurstInterval))
                       .First();
        }

        private double intervalAt(double startTime, int beatDivisor)
            => DodgeEmitter.IntervalForBeatLength(editorBeatmap.ControlPointInfo.TimingPointAt(startTime).BeatLength, beatDivisor);

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

        private static Drawable createEnumControl<T>(LocalisableString label, Bindable<T> current)
            where T : struct, Enum
            => new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(3),
                Children = new Drawable[]
                {
                    new OsuSpriteText { Text = label },
                    new OsuEnumDropdown<T>
                    {
                        RelativeSizeAxes = Axes.X,
                        Current = current,
                    },
                },
            };

    }
}
