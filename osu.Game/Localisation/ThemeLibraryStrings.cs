using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ThemeLibraryStrings
    {
        private const string prefix = "osu.Game.Localisation.ThemeLibrary";

        /// <summary>
        /// "Themes: {0}"
        /// </summary>
        public static LocalisableString ThemeCount(int count) => new TranslatableString($"{prefix}:theme_count", "Themes: {0}", count);

        /// <summary>
        /// "Retry"
        /// </summary>
        public static LocalisableString Retry => new TranslatableString($"{prefix}:retry", "Retry");

        /// <summary>
        /// "Close"
        /// </summary>
        public static LocalisableString Close => new TranslatableString($"{prefix}:close", "Close");

        /// <summary>
        /// "Back to library"
        /// </summary>
        public static LocalisableString BackToLibrary => new TranslatableString($"{prefix}:back_to_library", "Back to library");

        /// <summary>
        /// "Theme library"
        /// </summary>
        public static LocalisableString Library => new TranslatableString($"{prefix}:library", "Theme library");

        /// <summary>
        /// "Search themes"
        /// </summary>
        public static LocalisableString Search => new TranslatableString($"{prefix}:search", "Search themes");

        /// <summary>
        /// "All themes"
        /// </summary>
        public static LocalisableString AllThemes => new TranslatableString($"{prefix}:all_themes", "All themes");

        /// <summary>
        /// "My publications"
        /// </summary>
        public static LocalisableString MyThemes => new TranslatableString($"{prefix}:my_themes", "My publications");

        /// <summary>
        /// "Publish selected theme"
        /// </summary>
        public static LocalisableString Publish => new TranslatableString($"{prefix}:publish", "Publish selected theme");

        /// <summary>
        /// "Install theme"
        /// </summary>
        public static LocalisableString Install => new TranslatableString($"{prefix}:install", "Install theme");

        /// <summary>
        /// "Replace with selected theme"
        /// </summary>
        public static LocalisableString Update => new TranslatableString($"{prefix}:update", "Replace with selected theme");

        /// <summary>
        /// "Delete publication"
        /// </summary>
        public static LocalisableString Delete => new TranslatableString($"{prefix}:delete", "Delete publication");

        /// <summary>
        /// "Find a theme or publish the selected local theme. Previews use a fixed layout, independent of personal settings."
        /// </summary>
        public static LocalisableString OpenHint => new TranslatableString($"{prefix}:open_hint", "Find a theme or publish the selected local theme. Previews use a fixed layout, independent of personal settings.");

        /// <summary>
        /// "Sign in to use the theme library."
        /// </summary>
        public static LocalisableString LoginRequired => new TranslatableString($"{prefix}:login_required", "Sign in to use the theme library.");

        /// <summary>
        /// "Loading…"
        /// </summary>
        public static LocalisableString Loading => new TranslatableString($"{prefix}:loading", "Loading…");

        /// <summary>
        /// "No themes found."
        /// </summary>
        public static LocalisableString Empty => new TranslatableString($"{prefix}:empty", "No themes found.");

        /// <summary>
        /// "Could not complete the request. Refresh the list and try again."
        /// </summary>
        public static LocalisableString Failed => new TranslatableString($"{prefix}:failed", "Could not complete the request. Refresh the list and try again.");

        /// <summary>
        /// "Installed in saved themes. Select it to apply."
        /// </summary>
        public static LocalisableString Installed => new TranslatableString($"{prefix}:installed", "Installed in saved themes. Select it to apply.");

        /// <summary>
        /// "Publish “{0}” for everyone?"
        /// </summary>
        public static LocalisableString PublishConfirm(string name) => new TranslatableString($"{prefix}:publish_confirm", "Publish “{0}” for everyone?", name);

        /// <summary>
        /// "Replace “{0}” with the selected local theme “{1}”?"
        /// </summary>
        public static LocalisableString UpdateConfirm(string original, string replacement) => new TranslatableString($"{prefix}:update_confirm", "Replace “{0}” with the selected local theme “{1}”?", original, replacement);

        /// <summary>
        /// "Delete the publication “{0}”? Installed copies will remain."
        /// </summary>
        public static LocalisableString DeleteConfirm(string name) => new TranslatableString($"{prefix}:delete_confirm", "Delete the publication “{0}”? Installed copies will remain.", name);

    }
}
