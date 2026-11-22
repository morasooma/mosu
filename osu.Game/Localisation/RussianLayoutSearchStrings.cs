using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class RussianLayoutSearchStrings
    {
        private const string prefix = "osu.Game.Localisation.RussianLayoutSearch";

        /// <summary>"Account for Russian keyboard layout when searching"</summary>
        public static LocalisableString Caption => new TranslatableString($"{prefix}:caption", "Account for Russian keyboard layout when searching");
        /// <summary>"Also search using English keys: фшь ыдщз → aim slop. Russian titles still match. Applies immediately to local beatmap search."</summary>
        public static LocalisableString Hint => new TranslatableString($"{prefix}:hint", "Also search using English keys: фшь ыдщз → aim slop. Russian titles still match. Applies immediately to local beatmap search.");
    }
}
