using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class CollectionMemoryStrings
    {
        private const string prefix = "osu.Game.Localisation.CollectionMemory";

        public static LocalisableString Caption => new TranslatableString($"{prefix}:caption", "Remember selected collection");

        public static LocalisableString Hint => new TranslatableString($"{prefix}:hint", "Restores the selected collection when opening song select. When disabled, new song select screens start with all beatmaps; the current selection is not changed.");
    }
}
