using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class InterfaceSoundStrings
    {
        private const string prefix = "osu.Game.Localisation.InterfaceSound";

        /// <summary>
        /// "Interface sounds"
        /// </summary>
        public static LocalisableString Header => new TranslatableString($"{prefix}:header", "Interface sounds");
        /// <summary>
        /// "Sound pack"
        /// </summary>
        public static LocalisableString Pack => new TranslatableString($"{prefix}:pack", "Sound pack");
        /// <summary>
        /// "Default"
        /// </summary>
        public static LocalisableString Default => new TranslatableString($"{prefix}:default", "Default");
        /// <summary>
        /// "Changes interface sounds only, not hitsounds or music. Applies without restarting."
        /// </summary>
        public static LocalisableString Hint => text("hint", "Changes interface sounds only, not hitsounds or music. Applies without restarting.");
        /// <summary>
        /// "Import sounds (.morafx)"
        /// </summary>
        public static LocalisableString Import => text("import", "Import sounds (.morafx)");
        public static LocalisableString Export => text("export", "Export sound pack (.morafx)");
        public static LocalisableString Folder => text("folder", "Open sound packs folder");
        public static LocalisableString Imported => text("imported", "Sound pack imported.");
        /// <summary>
        /// "Portable .morafx package with interface sounds and notices. Maximum 64 MiB. Share recordings only if their licence permits it."
        /// </summary>
        public static LocalisableString ImportHint => text("import_hint", "Portable .morafx package with interface sounds and notices. Maximum 64 MiB. Share recordings only if their licence permits it.");
        /// <summary>
        /// "Choose a .morafx file"
        /// </summary>
        public static LocalisableString ChooseArchive => text("choose_archive", "Choose a .morafx file");
        /// <summary>
        /// "Interface event"
        /// </summary>
        public static LocalisableString Event => new TranslatableString($"{prefix}:event", "Interface event");
        /// <summary>
        /// "Preview sound"
        /// </summary>
        public static LocalisableString Preview => new TranslatableString($"{prefix}:preview", "Preview sound");
        /// <summary>
        /// "Replace sound (WAV / MP3)"
        /// </summary>
        public static LocalisableString Replace => new TranslatableString($"{prefix}:replace", "Replace sound (WAV / MP3)");
        /// <summary>
        /// "Maximum 8 MiB and 30 seconds. Built-in packs are never modified: a local custom copy is created."
        /// </summary>
        public static LocalisableString ReplaceHint => new TranslatableString($"{prefix}:replace_hint", "Maximum 8 MiB and 30 seconds. Built-in packs are never modified: a local custom copy is created.");
        /// <summary>
        /// "Choose an audio file"
        /// </summary>
        public static LocalisableString ChooseSound => new TranslatableString($"{prefix}:choose_sound", "Choose an audio file");
        /// <summary>
        /// "Reset this event to the base pack"
        /// </summary>
        public static LocalisableString Reset => new TranslatableString($"{prefix}:reset", "Reset this event to the base pack");
        /// <summary>
        /// "Reset all overrides to the base pack"
        /// </summary>
        public static LocalisableString ResetAll => new TranslatableString($"{prefix}:reset_all", "Reset all overrides to the base pack");
        /// <summary>
        /// "Could not load sound pack. Check file format, event names, size and duration."
        /// </summary>
        public static LocalisableString ImportFailed => new TranslatableString($"{prefix}:import_failed", "Could not load sound pack. Check file format, event names, size and duration.");

        public static LocalisableString Edit => text("edit", "Edit sounds");
        public static LocalisableString ScopeHint => text("scope_hint", "Interface only. Does not change hitsounds or music.");
        public static LocalisableString ImportShort => text("import_short", "Import .morafx");
        public static LocalisableString ResetAllShort => text("reset_all_short", "Reset all");
        public static LocalisableString CustomSound => text("custom_sound", "Custom sound");
        public static LocalisableString BaseSound => text("base_sound", "From base pack");

        private static LocalisableString text(string key, string fallback) => new TranslatableString($"{prefix}:{key}", fallback);

        public static LocalisableString Category(string category) => category switch
        {
            "UI" => text("category_ui", "Interface"),
            "Menu" => text("category_menu", "Main menu"),
            "SongSelect" => text("category_song_select", "Song selection"),
            "SongSelectClassic" => text("category_song_select_classic", "Classic song selection"),
            "Keyboard" => text("category_keyboard", "Text input"),
            "Intro" => text("category_intro", "Welcome and goodbye"),
            "Results" => text("category_results", "Results"),
            "Multiplayer" => text("category_multiplayer", "Multiplayer"),
            "DailyChallenge" => text("category_daily", "Daily challenge"),
            "MedalSplash" => text("category_medals", "Medals"),
            _ => category,
        };

        public static LocalisableString EventLabel(string key) => key switch
        {
            "UI/default-select" => text("label_click", "Click"),
            "UI/default-hover" => text("label_hover", "Hover"),
            "UI/check-on" => text("label_check_on", "Checkbox on"),
            "UI/check-off" => text("label_check_off", "Checkbox off"),
            "UI/screen-back" => text("label_back", "Back"),
            "UI/notch-tick" => text("label_slider", "Slider tick"),
            "UI/dropdown-open" => text("label_dropdown_open", "Dropdown open"),
            "UI/dropdown-close" => text("label_dropdown_close", "Dropdown close"),
            _ => key[(key.IndexOf('/') + 1)..].Replace('/', ' ').Replace('-', ' '),
        };

        public static LocalisableString EventName(string key) => key switch
        {
            "UI/default-hover" => new TranslatableString($"{prefix}:event_default_hover", "Hover (UI/default-hover)"),
            "UI/default-select" => new TranslatableString($"{prefix}:event_default_select", "Click (UI/default-select)"),
            "UI/check-on" => new TranslatableString($"{prefix}:event_check_on", "Checkbox on (UI/check-on)"),
            "UI/check-off" => new TranslatableString($"{prefix}:event_check_off", "Checkbox off (UI/check-off)"),
            "UI/screen-back" => new TranslatableString($"{prefix}:event_screen_back", "Back (UI/screen-back)"),
            "UI/notch-tick" => new TranslatableString($"{prefix}:event_notch_tick", "Slider tick (UI/notch-tick)"),
            "UI/dropdown-open" => new TranslatableString($"{prefix}:event_dropdown_open", "Dropdown open (UI/dropdown-open)"),
            "UI/dropdown-close" => new TranslatableString($"{prefix}:event_dropdown_close", "Dropdown close (UI/dropdown-close)"),
            _ => key,
        };
    }
}
