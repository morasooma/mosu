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
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeBulletToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();
        private bool syncingSelection;
        private FillFlowContainer appearanceControls = null!;
        private ExpandableButton appearanceButton = null!;
        private FillFlowContainer waveControls = null!;

        public BindableBool ContinueUntilExit => settings.BulletContinueUntilExit;

        public BindableBool LockFlight => settings.BulletLockFlight;

        public Bindable<Colour4> ProjectileColour => settings.BulletColour;
        public bool AppearanceControlsVisible => appearanceControls.Alpha > 0;
        public bool ShowAppearance
        {
            get => AppearanceControlsVisible;
            set => setAppearanceVisibility(value);
        }

        public DodgeBulletToolboxGroup()
            : base(DodgeEditorStrings.Bullet)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new osuTK.Vector2(5);
            Children = new Drawable[]
            {
                new OsuCheckbox
                {
                    RelativeSizeAxes = Axes.X,
                    LabelText = DodgeEditorStrings.ContinueUntilOutside,
                    Current = settings.BulletContinueUntilExit,
                },
                new OsuCheckbox
                {
                    RelativeSizeAxes = Axes.X,
                    LabelText = DodgeEditorStrings.LockFlight,
                    Current = settings.BulletLockFlight,
                },
                createEnumControl(DodgeEditorStrings.TrajectoryGuide, settings.BulletTrajectoryGuideStyle),
                createEnumControl(DodgeEditorStrings.MovementType, settings.BulletMovementType),
                createEnumControl(DodgeEditorStrings.MovementEasing, settings.BulletMovementEasing),
                waveControls = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(5),
                    Children = new Drawable[]
                    {
                        new ExpandableSlider<float>
                        {
                            ExpandedLabelText = DodgeEditorStrings.WaveAmplitude,
                            Current = settings.BulletWaveAmplitude,
                            KeyboardStep = 1,
                        },
                        new ExpandableSlider<int>
                        {
                            ExpandedLabelText = DodgeEditorStrings.WaveCycles,
                            Current = settings.BulletWaveCycles,
                            KeyboardStep = 1,
                        },
                        new ExpandableSlider<float>
                        {
                            ExpandedLabelText = DodgeEditorStrings.WavePhase,
                            Current = settings.BulletWavePhase,
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
            Spacing = new osuTK.Vector2(5),
            Alpha = 0,
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(3),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = DodgeEditorStrings.ProjectileShape },
                        new OsuEnumDropdown<DodgeBulletShape>
                        {
                            RelativeSizeAxes = Axes.X,
                            Current = settings.BulletShape,
                        },
                    },
                },
                createColourControl(DodgeEditorStrings.FillColour, settings.BulletColour),
                createColourControl(DodgeEditorStrings.OutlineColour, settings.BulletOutlineColour),
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.Opacity,
                    Current = settings.BulletOpacity,
                    KeyboardStep = 0.05f,
                },
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.OutlineThickness,
                    Current = settings.BulletOutlineThickness,
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

            settings.BulletContinueUntilExit.BindValueChanged(value => applyToSelection(bullet => bullet.ContinueUntilExit = value.NewValue));
            settings.BulletTrajectoryGuideStyle.BindValueChanged(value => applyToSelection(bullet => bullet.TrajectoryGuideStyle = value.NewValue));
            settings.BulletMovementType.BindValueChanged(type =>
            {
                waveControls.Alpha = type.NewValue != DodgeMovementType.Linear ? 1 : 0;
                waveControls.Children[0].Alpha = type.NewValue == DodgeMovementType.Sine ? 1 : 0;
                ((ExpandableSlider<int>)waveControls.Children[1]).ExpandedLabelText = type.NewValue.IsLooping()
                    ? DodgeEditorStrings.LoopRepetitions : DodgeEditorStrings.WaveCycles;
                applyToSelection(bullet => bullet.MovementType = type.NewValue);
            }, true);
            settings.BulletMovementEasing.BindValueChanged(value => applyToSelection(bullet => bullet.MovementEasing = value.NewValue));
            settings.BulletWaveAmplitude.BindValueChanged(value => applyToSelection(bullet => bullet.WaveAmplitude = value.NewValue));
            settings.BulletWaveCycles.BindValueChanged(value => applyToSelection(bullet => bullet.WaveCycles = value.NewValue));
            settings.BulletWavePhase.BindValueChanged(value => applyToSelection(bullet => bullet.WavePhase = value.NewValue));
            settings.BulletShape.BindValueChanged(value => applyToSelection(bullet => bullet.Shape = value.NewValue));
            settings.BulletColour.BindValueChanged(value => applyToSelection(bullet => bullet.Colour = value.NewValue));
            settings.BulletOutlineColour.BindValueChanged(value => applyToSelection(bullet => bullet.OutlineColour = value.NewValue));
            settings.BulletOpacity.BindValueChanged(value => applyToSelection(bullet => bullet.Opacity = value.NewValue));
            settings.BulletOutlineThickness.BindValueChanged(value => applyToSelection(bullet => bullet.OutlineThickness = value.NewValue));
            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => readFromSelection(), true);
            editorBeatmap.HitObjectUpdated += onHitObjectUpdated;
        }

        private void onHitObjectUpdated(HitObject item)
        {
            if (selectedHitObjects.Contains(item))
                Scheduler.AddOnce(readFromSelection);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (editorBeatmap != null)
                editorBeatmap.HitObjectUpdated -= onHitObjectUpdated;
            base.Dispose(isDisposing);
        }

        private void readFromSelection()
        {
            DodgeBullet? bullet = selectedHitObjects.OfType<DodgeBullet>().FirstOrDefault();

            if (bullet == null)
                return;

            syncingSelection = true;
            settings.BulletContinueUntilExit.Value = bullet.ContinueUntilExit;
            settings.BulletTrajectoryGuideStyle.Value = bullet.TrajectoryGuideStyle;
            settings.BulletMovementType.Value = bullet.MovementType;
            settings.BulletMovementEasing.Value = bullet.MovementEasing;
            settings.BulletWaveAmplitude.Value = bullet.WaveAmplitude;
            settings.BulletWaveCycles.Value = Math.Max(1, bullet.WaveCycles);
            settings.BulletWavePhase.Value = bullet.WavePhase;
            settings.BulletShape.Value = bullet.Shape;
            settings.BulletColour.Value = bullet.Colour;
            settings.BulletOutlineColour.Value = bullet.OutlineColour;
            settings.BulletOpacity.Value = bullet.Opacity;
            settings.BulletOutlineThickness.Value = bullet.OutlineThickness;
            syncingSelection = false;
        }

        private void applyToSelection(Action<DodgeBullet> apply)
        {
            if (syncingSelection)
                return;

            var bullets = selectedHitObjects.OfType<DodgeBullet>().ToArray();

            if (bullets.Length == 0)
                return;

            editorBeatmap.BeginChange();

            try
            {
                foreach (var bullet in bullets)
                {
                    apply(bullet);
                    editorBeatmap.Update(bullet);
                }
            }
            finally
            {
                editorBeatmap.EndChange();
            }
        }

        private static Drawable createColourControl(LocalisableString label, Bindable<Colour4> colour) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new osuTK.Vector2(3),
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
            where T : struct, System.Enum
            => new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new osuTK.Vector2(3),
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
