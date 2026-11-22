using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>
    /// Confirmation dialog shown before importing a theme file into Morasooma.
    /// Guarantees that zero settings mutation occurs until the user explicitly confirms.
    /// </summary>
    public partial class ThemeImportConfirmationDialog : PopupDialog
    {
        public ThemeImportConfirmationDialog(string themeName, ThemeMode baseTheme, int overridesCount, Action onConfirm, Action? onCancel = null)
        {
            LocalisableString baseThemeString = baseTheme switch
            {
                ThemeMode.Light => ForkSettingsStrings.ThemeModeLight,
                ThemeMode.Dark => ForkSettingsStrings.ThemeModeDark,
                ThemeMode.Custom => ForkSettingsStrings.ThemeModeCustom,
                _ => ForkSettingsStrings.ThemeModeDefault,
            };

            HeaderText = ForkSettingsStrings.CustomThemeImportDialogHeader;
            BodyText = ForkSettingsStrings.CustomThemeImportDialogBody(
                themeName,
                baseThemeString,
                overridesCount.ToString());

            Icon = FontAwesome.Solid.Palette;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = ForkSettingsStrings.CustomThemeImportDialogApply,
                    Action = onConfirm,
                },
                new PopupDialogCancelButton
                {
                    Text = ForkSettingsStrings.CustomThemeImportDialogCancel,
                    Action = onCancel,
                },
            };
        }
    }
}
