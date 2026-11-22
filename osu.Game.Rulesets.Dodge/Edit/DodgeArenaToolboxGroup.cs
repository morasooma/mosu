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
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeArenaToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();
        private bool syncingSelection;

        public DodgeArenaToolboxGroup()
            : base(DodgeEditorStrings.Arena)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                new SettingsCheckbox { LabelText = DodgeEditorStrings.ArenaWorldSpace, Current = settings.ArenaWorldSpace },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.ArenaWidth, Current = settings.ArenaWidth, KeyboardStep = 16 },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.ArenaHeight, Current = settings.ArenaHeight, KeyboardStep = 16 },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.ArenaCentreX, Current = settings.ArenaCentreX, KeyboardStep = 16 },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.ArenaCentreY, Current = settings.ArenaCentreY, KeyboardStep = 16 },
                new ExpandableButton
                {
                    RelativeSizeAxes = Axes.X,
                    ContractedLabelText = DodgeEditorStrings.FitSelectedArena,
                    ExpandedLabelText = DodgeEditorStrings.FitSelectedArena,
                    Action = () =>
                    {
                        var change = selectedHitObjects.OfType<DodgeArenaChange>().FirstOrDefault();
                        if (change != null)
                            settings.FitEditorView(change.TargetPosition + change.TargetSize / 2, change.TargetSize, change.TargetRotation);
                    },
                },
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.ArenaRotation,
                    Current = settings.ArenaRotation,
                    KeyboardStep = 1f,
                },
                new SettingsEnumDropdown<DodgeCameraEasing>
                {
                    LabelText = DodgeEditorStrings.ArenaEasing,
                    Current = settings.ArenaEasing,
                    TooltipText = DodgeEditorStrings.ArenaEasingHint,
                },
                new SettingsSlider<float>
                {
                    LabelText = DodgeEditorStrings.ArenaKiaiShakeAngle,
                    Current = settings.ArenaKiaiShakeAngle,
                    KeyboardStep = 0.5f,
                },
                createColourControl(DodgeEditorStrings.ArenaBackgroundColour, settings.ArenaBackgroundColour),
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.ArenaBackgroundOpacity,
                    Current = settings.ArenaBackgroundOpacity,
                    KeyboardStep = 0.05f,
                },
                createColourControl(DodgeEditorStrings.ArenaBorderColour, settings.ArenaBorderColour),
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.ArenaBorderOpacity,
                    Current = settings.ArenaBorderOpacity,
                    KeyboardStep = 0.05f,
                },
                new ExpandableButton
                {
                    RelativeSizeAxes = Axes.X,
                    ContractedLabelText = DodgeEditorStrings.Reset,
                    ExpandedLabelText = DodgeEditorStrings.ResetArena,
                    Action = ResetArena,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            settings.ArenaWorldSpace.BindValueChanged(value => applyToSelection(change =>
            {
                change.WorldSpace = value.NewValue;
                change.ClampToBaseBounds();
            }));
            settings.ArenaWidth.BindValueChanged(value => applyToSelection(change => change.ResizeFromCentre(new Vector2(value.NewValue, change.TargetSize.Y))));
            settings.ArenaHeight.BindValueChanged(value => applyToSelection(change => change.ResizeFromCentre(new Vector2(change.TargetSize.X, value.NewValue))));
            settings.ArenaCentreX.BindValueChanged(value => applyToSelection(change =>
            {
                change.TargetPosition = new Vector2(value.NewValue - change.TargetSize.X / 2, change.TargetPosition.Y);
                change.ClampToBaseBounds();
            }));
            settings.ArenaCentreY.BindValueChanged(value => applyToSelection(change =>
            {
                change.TargetPosition = new Vector2(change.TargetPosition.X, value.NewValue - change.TargetSize.Y / 2);
                change.ClampToBaseBounds();
            }));
            settings.ArenaRotation.BindValueChanged(value => applyToSelection(change => change.TargetRotation = value.NewValue));
            settings.ArenaEasing.BindValueChanged(value => applyToSelection(change => change.Easing = value.NewValue));
            settings.ArenaKiaiShakeAngle.BindValueChanged(value => applyToSelection(change => change.KiaiShakeAngle = value.NewValue));
            settings.ArenaBackgroundColour.BindValueChanged(value => applyToSelection(change => change.Colour = value.NewValue));
            settings.ArenaBackgroundOpacity.BindValueChanged(value => applyToSelection(change => change.Opacity = value.NewValue));
            settings.ArenaBorderColour.BindValueChanged(value => applyToSelection(change => change.OutlineColour = value.NewValue));
            settings.ArenaBorderOpacity.BindValueChanged(value => applyToSelection(change => change.BorderOpacity = value.NewValue));
            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => readFromSelection(), true);
            editorBeatmap.HitObjectUpdated += onHitObjectUpdated;
        }

        private void onHitObjectUpdated(HitObject hitObject)
        {
            if (selectedHitObjects.Contains(hitObject))
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
            DodgeArenaChange? change = selectedHitObjects.OfType<DodgeArenaChange>().FirstOrDefault();

            if (change == null)
                return;

            syncingSelection = true;
            settings.ArenaWorldSpace.Value = change.WorldSpace;
            settings.ArenaWidth.Value = change.TargetSize.X;
            settings.ArenaHeight.Value = change.TargetSize.Y;
            settings.ArenaCentreX.Value = change.TargetPosition.X + change.TargetSize.X / 2;
            settings.ArenaCentreY.Value = change.TargetPosition.Y + change.TargetSize.Y / 2;
            settings.ArenaRotation.Value = change.TargetRotation;
            settings.ArenaEasing.Value = change.Easing;
            settings.ArenaKiaiShakeAngle.Value = change.KiaiShakeAngle;
            settings.ArenaBackgroundColour.Value = change.Colour;
            settings.ArenaBackgroundOpacity.Value = change.Opacity;
            settings.ArenaBorderColour.Value = change.OutlineColour;
            settings.ArenaBorderOpacity.Value = change.BorderOpacity;
            syncingSelection = false;
        }

        private void applyToSelection(Action<DodgeArenaChange> apply)
        {
            if (syncingSelection)
                return;

            DodgeArenaChange[] changes = selectedHitObjects.OfType<DodgeArenaChange>().ToArray();

            if (changes.Length == 0)
                return;

            editorBeatmap.BeginChange();

            try
            {
                foreach (DodgeArenaChange change in changes)
                {
                    apply(change);
                    editorBeatmap.Update(change);
                }
            }
            finally
            {
                editorBeatmap.EndChange();
            }
        }

        private void applyAppearance(DodgeArenaChange change)
        {
            change.TargetRotation = settings.ArenaRotation.Value;
            change.Easing = settings.ArenaEasing.Value;
            change.KiaiShakeAngle = settings.ArenaKiaiShakeAngle.Value;
            change.Colour = settings.ArenaBackgroundColour.Value;
            change.Opacity = settings.ArenaBackgroundOpacity.Value;
            change.OutlineColour = settings.ArenaBorderColour.Value;
            change.BorderOpacity = settings.ArenaBorderOpacity.Value;
        }

        public void ResetArena()
        {
            bool hasArenaChanges = editorBeatmap.HitObjects.OfType<DodgeArenaChange>().Any();
            double startTime = hasArenaChanges ? System.Math.Min(beatSnapProvider.SnapTime(editorClock.CurrentTime), editorClock.TrackLength) : 0;

            if (hasArenaChanges && startTime >= editorClock.TrackLength)
                return;

            double duration = beatSnapProvider.GetBeatLengthAtTime(startTime) * 4;

            editorBeatmap.BeginChange();

            foreach (var existing in editorBeatmap.HitObjects
                                                      .OfType<DodgeArenaChange>()
                                                      .Where(change => System.Math.Abs(change.StartTime - startTime) <= 2)
                                                      .ToArray())
            {
                editorBeatmap.Remove(existing);
            }

            var arenaChange = new DodgeArenaChange
            {
                StartTime = startTime,
                Duration = duration,
                TargetPosition = Vector2.Zero,
                TargetSize = DodgePlayfield.BASE_SIZE,
                TargetRotation = 0,
            };

            applyAppearance(arenaChange);

            DodgeEditorTrackBounds.Constrain(arenaChange, editorClock.TrackLength, true);
            editorBeatmap.Add(arenaChange);

            editorBeatmap.EndChange();
        }

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
    }
}
