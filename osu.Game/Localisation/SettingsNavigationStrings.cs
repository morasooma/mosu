using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SettingsNavigationStrings
    {
        private const string prefix = "osu.Game.Localisation.SettingsNavigation";

        /// <summary>"Expand"</summary>
        public static LocalisableString Expand => new TranslatableString($"{prefix}:expand", "Expand");
        /// <summary>"Collapse"</summary>
        public static LocalisableString Collapse => new TranslatableString($"{prefix}:collapse", "Collapse");
    }
}
