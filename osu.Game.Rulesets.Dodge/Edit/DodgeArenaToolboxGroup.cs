// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.ArenaRotation,
                    Current = settings.ArenaRotation,
                    KeyboardStep = 1f,
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

            settings.ArenaRotation.BindValueChanged(_ => applyToSelection());
            settings.ArenaKiaiShakeAngle.BindValueChanged(_ => applyToSelection());
            settings.ArenaBackgroundColour.BindValueChanged(_ => applyToSelection());
            settings.ArenaBackgroundOpacity.BindValueChanged(_ => applyToSelection());
            settings.ArenaBorderColour.BindValueChanged(_ => applyToSelection());
            settings.ArenaBorderOpacity.BindValueChanged(_ => applyToSelection());
            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => readFromSelection());
        }

        private void readFromSelection()
        {
            DodgeArenaChange? change = selectedHitObjects.OfType<DodgeArenaChange>().FirstOrDefault();

            if (change == null)
                return;

            syncingSelection = true;
            settings.ArenaRotation.Value = change.TargetRotation;
            settings.ArenaKiaiShakeAngle.Value = change.KiaiShakeAngle;
            settings.ArenaBackgroundColour.Value = change.Colour;
            settings.ArenaBackgroundOpacity.Value = change.Opacity;
            settings.ArenaBorderColour.Value = change.OutlineColour;
            settings.ArenaBorderOpacity.Value = change.BorderOpacity;
            syncingSelection = false;
        }

        private void applyToSelection()
        {
            if (syncingSelection)
                return;

            DodgeArenaChange[] changes = selectedHitObjects.OfType<DodgeArenaChange>().ToArray();

            if (changes.Length == 0)
                return;

            editorBeatmap.BeginChange();

            foreach (DodgeArenaChange change in changes)
            {
                applyAppearance(change);
                editorBeatmap.Update(change);
            }

            editorBeatmap.EndChange();
        }

        private void applyAppearance(DodgeArenaChange change)
        {
            change.TargetRotation = settings.ArenaRotation.Value;
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
