// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.Timing
{
    internal partial class GroupSection : CompositeDrawable
    {
        private FormTextBox textBox = null!;
        private FormDiscreteAdjustmentControl<double> offsetControl = null!;
        private bool changingGroup;

        private OsuButton button = null!;

        [Resolved]
        protected Bindable<ControlPointGroup?> SelectedGroup { get; private set; } = null!;

        [Resolved]
        private ControlPointSelection selection { get; set; } = null!;

        [Resolved]
        protected EditorBeatmap Beatmap { get; private set; } = null!;

        [Resolved]
        private OsuConfigManager configManager { get; set; } = null!;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Padding = new MarginPadding(10) { Bottom = 0 };

            InternalChildren = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Spacing = new Vector2(10),
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        textBox = new FormTextBox
                        {
                            Caption = global::osu.Game.Localisation.ClientTranslationStrings.J4332,
                            SelectAllOnFocus = true,
                        },
                        offsetControl = new FormDiscreteAdjustmentControl<double>(1)
                        {
                            Caption = global::osu.Game.Localisation.ClientTranslationStrings.J4333,
                            Current = new BindableDouble { Precision = 1 },
                        },
                        button = new RoundedButton
                        {
                            Text = global::osu.Game.Localisation.ClientTranslationStrings.J4334,
                            RelativeSizeAxes = Axes.X,
                            Action = () => changeSelectedGroupTime(clock.CurrentTime)
                        }
                    }
                },
            };

            textBox.OnCommit += (sender, isNew) =>
            {
                if (!isNew)
                    return;

                if (double.TryParse(sender.Text, out double newTime))
                {
                    changeSelectedGroupTime(newTime);
                }
                else
                {
                    SelectedGroup.TriggerChange();
                }
            };

            SelectedGroup.BindValueChanged(group =>
            {
                if (group.NewValue == null)
                {
                    textBox.Current.Value = string.Empty;

                    // cannot use textBox.Current.Disabled due to https://github.com/ppy/osu-framework/issues/3919
                    textBox.ReadOnly = true;
                    offsetControl.Current.Disabled = true;
                    button.Enabled.Value = false;
                    return;
                }

                textBox.ReadOnly = false;
                button.Enabled.Value = true;
                offsetControl.Current.Disabled = false;

                changingGroup = true;
                try
                {
                    textBox.Current.Value = $"{group.NewValue.Time:n0}";
                    offsetControl.Current.Value = group.NewValue.Time;
                }
                finally
                {
                    changingGroup = false;
                }
            }, true);

            offsetControl.Current.BindValueChanged(offset =>
            {
                if (changingGroup || SelectedGroup.Value == null)
                    return;

                bool wasAtStart = clock.CurrentTimeAccurate == SelectedGroup.Value.Time;
                changeSelectedGroupTime(offset.NewValue);
                if (!clock.IsRunning && wasAtStart)
                    clock.Seek(SelectedGroup.Value.Time);
            });
        }

        private void changeSelectedGroupTime(in double time)
        {
            if (SelectedGroup.Value == null || time == SelectedGroup.Value.Time)
                return;

            selection.ChangeTime(Beatmap, time, configManager.Get<bool>(OsuSetting.EditorAdjustExistingObjectsOnTimingChanges));
        }
    }
}
