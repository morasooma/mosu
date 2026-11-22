// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
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
    public partial class DodgeCameraToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();
        private bool syncingSelection;
        private Drawable scrollControl = null!;
        private Drawable easingControl = null!;

        public DodgeCameraToolboxGroup()
            : base(DodgeEditorStrings.Camera)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                new SettingsEnumDropdown<DodgeCameraMode> { LabelText = DodgeEditorStrings.CameraMode, Current = settings.CameraMode },
                scrollControl = new SettingsCheckbox
                {
                    LabelText = DodgeEditorStrings.CameraContinuousScroll,
                    Current = settings.CameraContinuousScroll,
                    TooltipText = DodgeEditorStrings.CameraContinuousScrollHint,
                },
                easingControl = new SettingsEnumDropdown<DodgeCameraEasing>
                {
                    LabelText = DodgeEditorStrings.CameraEasing,
                    Current = settings.CameraEasing,
                    TooltipText = DodgeEditorStrings.CameraEasingHint,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            settings.CameraMode.BindValueChanged(value =>
            {
                applyToSelection(change =>
                {
                    bool wasInstant = change.Mode != DodgeCameraMode.Scroll;
                    change.Mode = value.NewValue;
                    if (wasInstant && value.NewValue == DodgeCameraMode.Scroll)
                        change.Duration = beatSnapProvider.GetBeatLengthAtTime(change.StartTime) * 4;
                });
                scrollControl.Alpha = easingControl.Alpha = value.NewValue == DodgeCameraMode.Scroll ? 1 : 0;
            }, true);
            settings.CameraContinuousScroll.BindValueChanged(value => applyToSelection(change => change.Continuous = value.NewValue));
            settings.CameraEasing.BindValueChanged(value => applyToSelection(change => change.Easing = value.NewValue));
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
            DodgeCameraChange? change = selectedHitObjects.OfType<DodgeCameraChange>().FirstOrDefault();

            if (change == null)
                return;

            syncingSelection = true;
            settings.CameraMode.Value = change.Mode;
            settings.CameraContinuousScroll.Value = change.Continuous;
            settings.CameraEasing.Value = change.Easing;
            syncingSelection = false;
        }

        private void applyToSelection(Action<DodgeCameraChange> apply)
        {
            if (syncingSelection)
                return;

            DodgeCameraChange[] changes = selectedHitObjects.OfType<DodgeCameraChange>().ToArray();

            if (changes.Length == 0)
                return;

            editorBeatmap.BeginChange();

            try
            {
                foreach (DodgeCameraChange change in changes)
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
    }
}
