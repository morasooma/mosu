// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>
    /// Provides an exact, multi-selection-friendly alternative to dragging the
    /// right edge of every object on the timeline.
    /// </summary>
    public partial class DodgeTimingToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();

        public BindableInt DurationSnapUnits { get; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 64,
        };

        private ExpandableSlider<int> durationSlider = null!;
        private bool syncingSelection;

        public DodgeTimingToolboxGroup()
            : base(DodgeEditorStrings.Timing)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = durationSlider = new ExpandableSlider<int>
            {
                Current = DurationSnapUnits,
                KeyboardStep = 1,
                ExpandedLabelText = DodgeEditorStrings.DurationSnapUnits,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            DurationSnapUnits.BindValueChanged(value =>
            {
                updateContractedLabel(value.NewValue);
                applyToSelection();
            }, true);

            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => readFromSelection(), true);
            editorBeatmap.HitObjectUpdated += onHitObjectUpdated;
        }

        private void onHitObjectUpdated(HitObject hitObject)
        {
            if (selectedHitObjects.Contains(hitObject))
                Scheduler.AddOnce(readFromSelection);
        }

        private void readFromSelection()
        {
            DodgeHitObject? first = selectedHitObjects.OfType<DodgeHitObject>().FirstOrDefault();

            if (first is not IHasDuration duration)
                return;

            double snapLength = beatSnapProvider.GetBeatLengthAtTime(first.StartTime);

            if (snapLength <= 0)
                return;

            syncingSelection = true;
            DurationSnapUnits.Value = Math.Clamp((int)Math.Round(duration.Duration / snapLength), DurationSnapUnits.MinValue, DurationSnapUnits.MaxValue);
            updateContractedLabel(DurationSnapUnits.Value);
            syncingSelection = false;
        }

        private void applyToSelection()
        {
            if (syncingSelection)
                return;

            DodgeHitObject[] selected = selectedHitObjects.OfType<DodgeHitObject>().ToArray();

            if (selected.Length == 0)
                return;

            editorBeatmap.BeginChange();

            foreach (DodgeHitObject hitObject in selected)
            {
                if (hitObject is not IHasDuration duration)
                    continue;

                duration.Duration = beatSnapProvider.GetBeatLengthAtTime(hitObject.StartTime) * DurationSnapUnits.Value;
                DodgeEditorTrackBounds.Constrain(hitObject, editorClock.TrackLength, true);
                editorBeatmap.Update(hitObject);
            }

            editorBeatmap.EndChange();
        }

        private void updateContractedLabel(int snapUnits)
        {
            DodgeHitObject? first = selectedHitObjects.OfType<DodgeHitObject>().FirstOrDefault();
            double time = first?.StartTime ?? editorClock.CurrentTime;
            double milliseconds = beatSnapProvider.GetBeatLengthAtTime(time) * snapUnits;
            durationSlider.ContractedLabelText = DodgeEditorStrings.DurationValue(snapUnits, milliseconds);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (editorBeatmap != null)
                editorBeatmap.HitObjectUpdated -= onHitObjectUpdated;

            base.Dispose(isDisposing);
        }
    }
}
