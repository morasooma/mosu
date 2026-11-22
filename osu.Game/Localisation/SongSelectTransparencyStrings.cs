using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SongSelectTransparencyStrings
    {
        private const string prefix = "osu.Game.Localisation.SongSelectTransparency";

        /// <summary>"Transparency in the beatmap carousel"</summary>
        public static LocalisableString Caption => new TranslatableString($"{prefix}:caption", "Transparency in the beatmap carousel");
        /// <summary>"Makes toolbar, difficulty backgrounds and the bottom panel translucent during song selection. Difficulty colours, text and buttons are preserved."</summary>
        public static LocalisableString Hint => new TranslatableString($"{prefix}:hint", "Makes toolbar, difficulty backgrounds and the bottom panel translucent during song selection. Difficulty colours, text and buttons are preserved.");
    }
}
