using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class NamedThemeDropdown : FormDropdown<string>
    {
        private readonly CustomThemeLibrary library;
        private readonly Bindable<ThemeMode> mode;
        private readonly IBindable<int> libraryChanged;
        private readonly bool includeBuiltInThemes;
        private bool refreshing;

        public NamedThemeDropdown(OsuConfigManager config, bool includeBuiltInThemes = false)
        {
            library = config.CustomThemes;
            mode = config.GetBindable<ThemeMode>(OsuSetting.ForkThemeMode);
            libraryChanged = library.Changed.GetBoundCopy();
            this.includeBuiltInThemes = includeBuiltInThemes;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Current.BindValueChanged(e =>
            {
                if (refreshing)
                    return;

                if (e.NewValue.StartsWith("builtin:", StringComparison.Ordinal))
                    mode.Value = Enum.Parse<ThemeMode>(e.NewValue.Substring("builtin:".Length));
                else
                {
                    library.Select(e.NewValue);
                    mode.Value = ThemeMode.Custom;
                }
            });
            libraryChanged.BindValueChanged(_ => refresh());
            mode.BindValueChanged(_ => refresh());
            refresh();
        }

        private void refresh()
        {
            refreshing = true;
            try
            {
                // Recreate menu items as their labels may have changed after a rename.
                Items = Array.Empty<string>();
                Items = (includeBuiltInThemes
                    ? new[] { "builtin:Default", "builtin:Light", "builtin:Dark" }
                    : Array.Empty<string>()).Concat(library.Entries.Select(e => e.Id));
                Current.Value = includeBuiltInThemes && mode.Value != ThemeMode.Custom
                    ? $"builtin:{mode.Value}"
                    : library.ActiveId;
            }
            finally
            {
                refreshing = false;
            }
        }

        protected override LocalisableString GenerateItemText(string item) => item switch
        {
            "builtin:Default" => ForkSettingsStrings.ThemeModeDefault,
            "builtin:Light" => ForkSettingsStrings.ThemeModeLight,
            "builtin:Dark" => ForkSettingsStrings.ThemeModeDark,
            _ => string.IsNullOrEmpty(library.Entries.FirstOrDefault(e => e.Id == item)?.Name)
                ? ForkSettingsStrings.ThemeModeCustom
                : library.Entries.First(e => e.Id == item).Name,
        };
    }
}
