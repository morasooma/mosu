using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SmokeTagStrings
    {
        private const string prefix = "osu.Game.Localisation.SmokeTag";

        /// <summary>
        /// "Draws your tag with cursor smoke after the final judgement. The movement and smoke input are saved in the replay."
        /// </summary>
        public static LocalisableString Hint => new TranslatableString($"{prefix}:hint", "Draws your tag with cursor smoke after the final judgement. The movement and smoke input are saved in the replay.");

        /// <summary>
        /// "Smoke tag text"
        /// </summary>
        public static LocalisableString TextCaption => new TranslatableString($"{prefix}:text_caption", "Smoke tag text");

        /// <summary>
        /// "Up to 16 characters: Latin/Cyrillic letters, digits and basic punctuation. Drawn in uppercase; unsupported characters are skipped. Empty text uses MORASOOMA."
        /// </summary>
        public static LocalisableString TextHint => new TranslatableString($"{prefix}:text_hint", "Up to 16 characters: Latin/Cyrillic letters, digits and basic punctuation. Drawn in uppercase; unsupported characters are skipped. Empty text uses MORASOOMA.");
    }
}
