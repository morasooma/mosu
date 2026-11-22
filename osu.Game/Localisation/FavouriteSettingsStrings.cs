using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class FavouriteSettingsStrings
    {
        private const string prefix = "osu.Game.Localisation.FavouriteSettings";
        public static LocalisableString SelectionTitle => new TranslatableString($"{prefix}:SelectionTitle", "Select favourite settings");
        public static LocalisableString Title => new TranslatableString($"{prefix}:Title", "Favourite settings");
        public static LocalisableString Description => new TranslatableString($"{prefix}:Description", "Your frequently used settings in one place.");
        public static LocalisableString Add => new TranslatableString($"{prefix}:Add", "Add");
        public static LocalisableString Remove => new TranslatableString($"{prefix}:Remove", "Remove");
        public static LocalisableString FinishRemoving => new TranslatableString($"{prefix}:FinishRemoving", "Done");
        public static LocalisableString Done => new TranslatableString($"{prefix}:Done", "Done — return to favourites");
        public static LocalisableString SelectDescription => new TranslatableString($"{prefix}:SelectDescription", "Select settings with a star. Their values will not change. Escape returns to favourites.");
        public static LocalisableString Empty => new TranslatableString($"{prefix}:Empty", "No available favourite settings. Use Add settings to select them. Unavailable settings are kept for other platforms.");
        /// <summary>"Shortcut: {0}"</summary>
        public static LocalisableString Shortcut(LocalisableString keys) => new TranslatableString($"{prefix}:Shortcut", "Shortcut: {0}", keys);
        public static LocalisableString ToggleAction => new TranslatableString($"{prefix}:ToggleAction", "Toggle favourite settings");
    }
}
