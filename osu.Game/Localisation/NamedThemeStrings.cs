using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class NamedThemeStrings
    {
        private const string prefix = "osu.Game.Localisation.NamedTheme";

        /// <summary>
        /// "Saved themes"
        /// </summary>
        public static LocalisableString SavedThemes => new TranslatableString($"{prefix}:saved_themes", "Saved themes");
        /// <summary>
        /// "Theme name"
        /// </summary>
        public static LocalisableString Name => new TranslatableString($"{prefix}:name", "Theme name");
        /// <summary>
        /// "Save as a new theme"
        /// </summary>
        public static LocalisableString Create => new TranslatableString($"{prefix}:create", "Save as a new theme");
        /// <summary>
        /// "Rename theme"
        /// </summary>
        public static LocalisableString Rename => new TranslatableString($"{prefix}:rename", "Rename theme");
        /// <summary>
        /// "Delete theme"
        /// </summary>
        public static LocalisableString Delete => new TranslatableString($"{prefix}:delete", "Delete theme");
        /// <summary>
        /// "Colour and geometry changes are saved automatically to the selected theme. Enter a name to save a separate copy or rename it."
        /// </summary>
        public static LocalisableString AutoSaveHint => new TranslatableString($"{prefix}:auto_save_hint", "Colour and geometry changes are saved automatically to the selected theme. Enter a name to save a separate copy or rename it.");
        /// <summary>
        /// "Enter a theme name (1–64 characters, no line breaks)."
        /// </summary>
        public static LocalisableString InvalidName => new TranslatableString($"{prefix}:invalid_name", "Enter a theme name (1–64 characters, no line breaks).");
        /// <summary>
        /// "Delete the saved theme ‘{0}’? This cannot be undone."
        /// </summary>
        public static LocalisableString DeleteConfirmation(LocalisableString name) => new TranslatableString($"{prefix}:delete_confirmation", "Delete the saved theme ‘{0}’? This cannot be undone.", name);
    }
}
