// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();
        private bool syncingSelection;

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
                new SettingsCheckbox
                {
                    LabelText = DodgeEditorStrings.CameraContinuousScroll,
                    Current = settings.CameraContinuousScroll,
                    TooltipText = DodgeEditorStrings.CameraContinuousScrollHint,
                },
                new SettingsEnumDropdown<DodgeCameraEasing>
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

            settings.CameraContinuousScroll.BindValueChanged(_ => applyToSelection());
            settings.CameraEasing.BindValueChanged(_ => applyToSelection());
            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => readFromSelection());
        }

        private void readFromSelection()
        {
            DodgeCameraChange? change = selectedHitObjects.OfType<DodgeCameraChange>().FirstOrDefault();

            if (change == null)
                return;

            syncingSelection = true;
            settings.CameraContinuousScroll.Value = change.Continuous;
            settings.CameraEasing.Value = change.Easing;
            syncingSelection = false;
        }

        private void applyToSelection()
        {
            if (syncingSelection)
                return;

            DodgeCameraChange[] changes = selectedHitObjects.OfType<DodgeCameraChange>().ToArray();

            if (changes.Length == 0)
                return;

            editorBeatmap.BeginChange();

            foreach (DodgeCameraChange change in changes)
            {
                change.Continuous = settings.CameraContinuousScroll.Value;
                change.Easing = settings.CameraEasing.Value;
                editorBeatmap.Update(change);
            }

            editorBeatmap.EndChange();
        }
    }
}
