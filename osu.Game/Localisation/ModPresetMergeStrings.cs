using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ModPresetMergeStrings
    {
        private const string prefix = "osu.Game.Localisation.ModPresetMerge";

        /// <summary>"Merge mod presets with Shift"</summary>
        public static LocalisableString Caption => new TranslatableString($"{prefix}:caption", "Merge mod presets with Shift");

        /// <summary>"Hold Shift when clicking a preset or pressing its number key to add its mods to the current selection. Conflicting mods are skipped; existing mods and their settings take priority."</summary>
        public static LocalisableString Hint => new TranslatableString($"{prefix}:hint", "Hold Shift when clicking a preset or pressing its number key to add its mods to the current selection. Conflicting mods are skipped; existing mods and their settings take priority.");
    }
}
