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
    public partial class DodgeBeamToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();
        private bool syncingSelection;

        public Bindable<Colour4> BeamColour => settings.BeamColour;

        public DodgeBeamToolboxGroup()
            : base(DodgeEditorStrings.Beam)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new osuTK.Vector2(5);
            Children = new Drawable[]
            {
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.BeamWidth,
                    Current = settings.BeamWidth,
                    KeyboardStep = 1f,
                },
                createColourControl(DodgeEditorStrings.FillColour, settings.BeamColour),
                createColourControl(DodgeEditorStrings.OutlineColour, settings.BeamOutlineColour),
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.Opacity,
                    Current = settings.BeamOpacity,
                    KeyboardStep = 0.05f,
                },
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.OutlineThickness,
                    Current = settings.BeamOutlineThickness,
                    KeyboardStep = 0.25f,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            settings.BeamWidth.BindValueChanged(value => applyToSelection(beam => beam.BeamWidth = value.NewValue));
            settings.BeamColour.BindValueChanged(value => applyToSelection(beam => beam.Colour = value.NewValue));
            settings.BeamOutlineColour.BindValueChanged(value => applyToSelection(beam => beam.OutlineColour = value.NewValue));
            settings.BeamOpacity.BindValueChanged(value => applyToSelection(beam => beam.Opacity = value.NewValue));
            settings.BeamOutlineThickness.BindValueChanged(value => applyToSelection(beam => beam.OutlineThickness = value.NewValue));

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
            DodgeBeam? beam = selectedHitObjects.OfType<DodgeBeam>().FirstOrDefault();

            if (beam == null)
                return;

            syncingSelection = true;
            settings.BeamWidth.Value = beam.BeamWidth;
            settings.BeamColour.Value = beam.Colour;
            settings.BeamOutlineColour.Value = beam.OutlineColour;
            settings.BeamOpacity.Value = beam.Opacity;
            settings.BeamOutlineThickness.Value = beam.OutlineThickness;
            syncingSelection = false;
        }

        private void applyToSelection(Action<DodgeBeam> apply)
        {
            if (syncingSelection)
                return;

            var beams = selectedHitObjects.OfType<DodgeBeam>().ToArray();

            if (beams.Length == 0)
                return;

            editorBeatmap.BeginChange();

            try
            {
                foreach (var beam in beams)
                {
                    apply(beam);
                    editorBeatmap.Update(beam);
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
    }
}
