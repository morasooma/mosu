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

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();
        private bool syncingSelection;

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
                },
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.TriggerStrength,
                    Current = settings.TriggerStrength,
                    KeyboardStep = 0.05f,
                },
                new ExpandableSlider<float>
                {
                    ExpandedLabelText = DodgeEditorStrings.TriggerDuration,
                    Current = settings.TriggerDuration,
                    KeyboardStep = 50,
                },
                createColourControl(DodgeEditorStrings.TriggerColour, settings.TriggerColour),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            settings.TriggerAction.BindValueChanged(_ => applyToSelection());
            settings.TriggerStrength.BindValueChanged(_ => applyToSelection());
            settings.TriggerDuration.BindValueChanged(_ => applyToSelection());
            settings.TriggerColour.BindValueChanged(_ => applyToSelection());
            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => readFromSelection());
            editorBeatmap.HitObjectUpdated += onHitObjectUpdated;
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
            settings.TriggerAction.Value = trigger.Action;
            settings.TriggerStrength.Value = trigger.Strength;
            settings.TriggerColour.Value = trigger.Colour;

            if (trigger.IsTimedEffect && trigger.Duration > 0)
                settings.TriggerDuration.Value = (float)trigger.Duration;

            syncingSelection = false;
        }

        private void applyToSelection()
        {
            if (syncingSelection)
                return;

            DodgeTrigger[] triggers = selectedHitObjects.OfType<DodgeTrigger>().ToArray();

            if (triggers.Length == 0)
                return;

            editorBeatmap.BeginChange();

            foreach (DodgeTrigger trigger in triggers)
            {
                trigger.Action = settings.TriggerAction.Value;
                trigger.Strength = settings.TriggerStrength.Value;
                trigger.Colour = settings.TriggerColour.Value;
                trigger.Duration = trigger.IsTimedEffect ? settings.TriggerDuration.Value : 0;
                editorBeatmap.Update(trigger);
            }

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

        protected override void Dispose(bool isDisposing)
        {
            if (editorBeatmap != null)
                editorBeatmap.HitObjectUpdated -= onHitObjectUpdated;

            base.Dispose(isDisposing);
        }
    }
}
