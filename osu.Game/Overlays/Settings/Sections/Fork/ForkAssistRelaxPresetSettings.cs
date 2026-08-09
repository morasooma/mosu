// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkAssistRelaxPresetSettings : SettingsSubsection
    {
        protected override LocalisableString Header => ForkSettingsStrings.PresetHeader;

        private readonly Bindable<ForkAssistRelaxPreset> selectedPreset = new Bindable<ForkAssistRelaxPreset>();
        private readonly Bindable<SettingsNote.Data?> presetNote = new Bindable<SettingsNote.Data?>();

        private bool resettingSelection;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var presetDropdown = new FormEnumDropdown<ForkAssistRelaxPreset>
            {
                Caption = ForkSettingsStrings.PresetDropdownCaption,
                HintText = ForkSettingsStrings.PresetDropdownHint,
                Current = selectedPreset
            };

            presetNote.Value = new SettingsNote.Data(ForkAssistRelaxPreset.None.GetSummary(), SettingsNote.Type.Informational);

            selectedPreset.BindValueChanged(preset =>
            {
                if (resettingSelection || preset.NewValue == ForkAssistRelaxPreset.None)
                    return;

                preset.NewValue.Apply(config);
                presetNote.Value = new SettingsNote.Data(
                    ForkSettingsStrings.PresetNoteApplied(preset.NewValue.GetDisplayName(), preset.NewValue.GetSummary()),
                    SettingsNote.Type.Informational);

                resettingSelection = true;
                selectedPreset.Value = ForkAssistRelaxPreset.None;
                resettingSelection = false;
            });

            Children = new Drawable[]
            {
                new SettingsItemV2(presetDropdown)
                {
                    ShowRevertToDefaultButton = false,
                    Note = { BindTarget = presetNote },
                },
            };
        }
    }
}
