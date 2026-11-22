using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BreakStyleStrings
    {
        private const string prefix = "osu.Game.Localisation.BreakStyle";

        /// <summary>"Break visuals"</summary>
        public static LocalisableString Caption => new TranslatableString($"{prefix}:caption", "Break visuals");
        /// <summary>"Changes only the break presentation. Legacy uses section-pass and section-fail from your skin."</summary>
        public static LocalisableString Hint => new TranslatableString($"{prefix}:hint", "Changes only the break presentation. Legacy uses section-pass and section-fail from your skin.");
        /// <summary>"Default"</summary>
        public static LocalisableString Default => new TranslatableString($"{prefix}:default", "Default");
        /// <summary>"Legacy (from skin)"</summary>
        public static LocalisableString Legacy => new TranslatableString($"{prefix}:legacy", "Legacy (from skin)");
        /// <summary>"No visuals"</summary>
        public static LocalisableString Hidden => new TranslatableString($"{prefix}:hidden", "No visuals");
    }
}
