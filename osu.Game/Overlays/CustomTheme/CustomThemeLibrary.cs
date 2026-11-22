using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Game.Configuration;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>
    /// Named local themes. The existing working-theme setting remains the source for rendering
    /// and live editors; every edit is also saved to the selected library entry.
    /// </summary>
    public class CustomThemeLibrary
    {
        // Match the portable theme format so export/import preserves the complete name.
        public const int MAX_NAME_LENGTH = 64;

        private readonly OsuConfigManager config;
        private readonly Bindable<string> workingTheme;
        private readonly List<Entry> entries;
        private bool switching;
        private readonly List<object> interfaceBindings = new List<object>();
        private readonly Bindable<ThemeMode> mode;

        public IReadOnlyList<Entry> Entries => entries;
        public string ActiveId => config.Get<string>(OsuSetting.ForkActiveCustomThemeId);
        public Entry Active => entries.First(e => e.Id == ActiveId);
        public readonly Bindable<int> Changed = new Bindable<int>();

        public CustomThemeLibrary(OsuConfigManager config)
        {
            this.config = config;
            workingTheme = config.GetBindable<string>(OsuSetting.ForkCustomThemeJson);
            mode = config.GetBindable<ThemeMode>(OsuSetting.ForkThemeMode);

            try
            {
                entries = JsonConvert.DeserializeObject<List<Entry>>(config.Get<string>(OsuSetting.ForkCustomThemesJson)) ?? new List<Entry>();
                entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.Id) || e.Theme == null);
                entries = entries.DistinctBy(e => e.Id).ToList();
            }
            catch (JsonException)
            {
                entries = new List<Entry>();
            }

            // Migrate the single custom theme without changing its palette or the selected mode.
            if (entries.Count == 0)
                entries.Add(new Entry { Theme = CustomThemeDefinition.FromJson(workingTheme.Value) });

            var active = entries.FirstOrDefault(e => e.Id == ActiveId);
            if (active == null)
            {
                active = entries[0];
                // A missing selection must not discard a previously edited working theme.
                active.Theme = CustomThemeDefinition.FromJson(workingTheme.Value);
                config.SetValue(OsuSetting.ForkActiveCustomThemeId, active.Id);
            }

            // Keep the original empty working-theme default for untouched profiles.
            if (!string.IsNullOrEmpty(workingTheme.Value) || !active.Theme.Equals(new CustomThemeDefinition()))
                workingTheme.Value = active.Theme.ToJson();
            persist();
            workingTheme.BindValueChanged(e =>
            {
                if (switching)
                    return;

                Active.Theme = CustomThemeDefinition.FromJson(e.NewValue);
                applyInterface();
                persist();
            });

            observe<bool>(OsuSetting.ForkDisableInterfaceShear, (s, v) => s.DisableShear = v);
            observe<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle, (s, v) => s.SongSelectStyle = v);
            observe<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews, (s, v) => s.OldCarouselPreviews = v);
            observe<InterfaceAnimationMode>(OsuSetting.ForkInterfaceAnimations, (s, v) => s.Animations = v);
            observe<bool>(OsuSetting.ForkEnhancedAnimations, (s, v) => s.EnhancedAnimations = v);
            observe<bool>(OsuSetting.ForkOverlayTransparency, (s, v) => s.OverlayTransparency = v);
            observe<bool>(OsuSetting.ForkSongSelectTransparency, (s, v) => s.SongSelectTransparency = v);
            observe<double>(OsuSetting.ForkOverlayBlurStrength, (s, v) => s.BlurStrength = v);
            observe<double>(OsuSetting.ForkOverlayDimAmount, (s, v) => s.DimAmount = v);
            observe<string>(OsuSetting.ForkCustomUIFont, (s, v) =>
            {
                s.Font = ThemeInterfaceSettings.IsSafeFontName(v) ? v : "Default";
                s.Fonts = Graphics.Fonts.FontConfiguration.Capture(config);
            });
            observe<bool>(OsuSetting.ForkUseSeparateFonts, (s, v) => s.Fonts = Graphics.Fonts.FontConfiguration.Capture(config));
            foreach (var setting in new[] { OsuSetting.ForkMainFont, OsuSetting.ForkAlternateFont, OsuSetting.ForkNumericFont, OsuSetting.ForkInterFont })
                observe<string>(setting, (s, v) => s.Fonts = Graphics.Fonts.FontConfiguration.Capture(config));
            mode.BindValueChanged(_ => applyInterface(), true);
        }

        public void Select(string id)
        {
            var entry = entries.FirstOrDefault(e => e.Id == id);
            if (entry == null || id == ActiveId)
                return;

            switching = true;
            try
            {
                config.SetValue(OsuSetting.ForkActiveCustomThemeId, entry.Id);
                workingTheme.Value = entry.Theme.ToJson();
                if (mode.Value == ThemeMode.Custom) entry.Theme.Interface?.Apply(config);
            }
            finally
            {
                switching = false;
            }

            Changed.Value++;
        }

        public bool Create(string name, CustomThemeDefinition? theme = null, bool select = true)
        {
            if (!isValidName(name))
                return false;

            var entry = new Entry
            {
                Name = name.Trim(),
                Theme = (theme ?? CustomThemeDefinition.FromJson(workingTheme.Value)).DeepClone(),
            };
            if (theme == null)
                entry.Theme.Interface = ThemeInterfaceSettings.Capture(config);
            entries.Add(entry);
            persist();
            if (select)
                Select(entry.Id);
            else
                Changed.Value++;
            return true;
        }

        public bool Rename(string name)
        {
            if (!isValidName(name))
                return false;

            Active.Name = name.Trim();
            persist();
            Changed.Value++;
            return true;
        }

        public void DeleteActive()
        {
            // Always retain one editable theme, including its unsaved/live palette.
            if (entries.Count <= 1)
                return;

            var previous = Active;
            string nextId = entries.First(e => e.Id != previous.Id).Id;
            entries.Remove(previous);
            Select(nextId);
            persist();
            Changed.Value++;
        }

        /// <summary>Sharing the selected local theme includes its current fork appearance settings.</summary>
        public CustomThemeDefinition GetActiveThemeForSharing()
        {
            var theme = Active.Theme.DeepClone();
            var appearance = ThemeInterfaceSettings.Capture(config);
            if (theme.Interface?.Font is string requested && appearance.Font == "Default" && !config.IsThemeFontAvailable(requested))
                appearance.Font = requested;
            theme.Interface = appearance;
            return theme;
        }

        private void observe<T>(OsuSetting setting, Action<ThemeInterfaceSettings, T> change)
        {
            var binding = config.GetBindable<T>(setting);
            interfaceBindings.Add(binding);
            binding.BindValueChanged(e =>
            {
                if (switching) return;
                var theme = Active.Theme.DeepClone();
                theme.Interface ??= new ThemeInterfaceSettings();
                change(theme.Interface, e.NewValue);
                switching = true;
                try
                {
                    Active.Theme = theme;
                    workingTheme.Value = theme.ToJson();
                    persist();
                }
                finally { switching = false; }
            });
        }

        private void applyInterface()
        {
            if (switching || mode.Value != ThemeMode.Custom) return;
            switching = true;
            try { Active.Theme.Interface?.Apply(config); }
            finally { switching = false; }
        }

        private static bool isValidName(string name) => !string.IsNullOrWhiteSpace(name)
                                                      && name.Trim().Length <= MAX_NAME_LENGTH
                                                      && !name.Any(char.IsControl);

        private void persist() => config.SetValue(OsuSetting.ForkCustomThemesJson, JsonConvert.SerializeObject(entries));

        public class Entry
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N");
            public string Name { get; set; } = string.Empty;
            public CustomThemeDefinition Theme { get; set; } = new CustomThemeDefinition();
        }
    }
}
