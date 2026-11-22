using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class RankedPlayVisualModsStrings
    {
        private const string prefix = "osu.Game.Localisation.RankedPlayVisualMods";

        public static LocalisableString Caption => new TranslatableString($"{prefix}:caption", "Local visual mod");
        public static LocalisableString Hint => new TranslatableString($"{prefix}:hint", "Selected before queueing and locked for the match. Only mods supported by the selected queue are available. Not sent to the server; no score bonus. Disabled for rounds with conflicting required mods.");
        public static LocalisableString None => new TranslatableString($"{prefix}:none", "None");
    }
}
