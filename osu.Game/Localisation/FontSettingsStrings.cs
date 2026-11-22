using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class FontSettingsStrings
    {
        private static LocalisableString text(string key, string fallback) => new TranslatableString($"osu.Game.Localisation.FontSettings:{key}", fallback);
        public static LocalisableString Separate => text("separate", "Use different fonts");
        public static LocalisableString SeparateHint => text("separate_hint", "Choose fonts separately for each interface role. Turn off to use one font for the interface. Skin image digits and icons are not changed.");
        public static LocalisableString Common => text("common", "Single interface font");
        public static LocalisableString Main => text("main", "Main text");
        public static LocalisableString Alternate => text("alternate", "Headings and accents");
        public static LocalisableString Numeric => text("numeric", "Numbers");
        public static LocalisableString Inter => text("inter", "Additional text");
        public static LocalisableString RoleHint => text("role_hint", "Changes the font family for this group. Elements keep their own size and weight.");
        public static LocalisableString NumericHint => text("numeric_hint", "Changes the font family for numeric text. Does not change skin image digits. Elements keep their own size and weight.");
        public static LocalisableString Compatibility(string name) => new TranslatableString("osu.Game.Localisation.FontSettings:compatibility", "{0} (compatibility)", name);
        public static LocalisableString Default => text("default", "Default for this family");
        public static LocalisableString BuiltIn(string name) => new TranslatableString("osu.Game.Localisation.FontSettings:built_in", "{0} (built-in)", name);
        public static LocalisableString Pack(string name) => new TranslatableString("osu.Game.Localisation.FontSettings:pack", "{0} (imported)", name);
        public static LocalisableString Import => text("import", "Import fonts (.morafn)");
        public static LocalisableString Export => text("export", "Export settings and atlases (.morafn)");
        public static LocalisableString Hint => text("hint", "BMFont families only; Default keeps the original role. Import applies settings immediately. Exports include selected atlases and bundled font licences. Share custom fonts only if their licence permits it.");
        public static LocalisableString Imported => text("imported", "Font settings and atlases imported.");
        public static LocalisableString Associate => text("associate", "Open .morafn files with this client");
        public static LocalisableString Associated => text("associated", "File association registered.");
        public static LocalisableString Failed => text("failed", "Could not import or export fonts. Check the archive, font atlases and size limits.");
    }
}
