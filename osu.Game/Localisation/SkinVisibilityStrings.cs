using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SkinVisibilityStrings
    {
        private const string prefix = "osu.Game.Localisation.SkinVisibility";
        /// <summary>"Element visibility"</summary>
        public static LocalisableString Title => new TranslatableString($"{prefix}:title", "Element visibility");
        /// <summary>"Skin texture samples; built-in skins use illustrative previews. Artwork baked into another texture cannot be removed separately."</summary>
        public static LocalisableString PreviewHint => new TranslatableString($"{prefix}:preview_hint", "Skin texture samples; built-in skins use illustrative previews. Artwork baked into another texture cannot be removed separately.");
        /// <summary>"Notes and connections"</summary>
        public static LocalisableString Notes => new TranslatableString($"{prefix}:notes", "Notes and connections");
        /// <summary>"Sliders"</summary>
        public static LocalisableString Sliders => new TranslatableString($"{prefix}:sliders", "Sliders");
        /// <summary>"Spinners"</summary>
        public static LocalisableString Other => new TranslatableString($"{prefix}:other", "Spinners");
        /// <summary>"Combo numbers"</summary>
        public static LocalisableString Numbers => new TranslatableString($"{prefix}:numbers", "Combo numbers");
        /// <summary>"Follow points"</summary>
        public static LocalisableString FollowPoints => new TranslatableString($"{prefix}:follow_points", "Follow points");
        /// <summary>"Slider ball"</summary>
        public static LocalisableString Ball => new TranslatableString($"{prefix}:ball", "Slider ball");
        /// <summary>"Slider follow circle"</summary>
        public static LocalisableString FollowCircle => new TranslatableString($"{prefix}:follow_circle", "Slider follow circle");
        /// <summary>"Reverse arrows"</summary>
        public static LocalisableString Arrows => new TranslatableString($"{prefix}:arrows", "Reverse arrows");
        /// <summary>"Slider ticks"</summary>
        public static LocalisableString Ticks => new TranslatableString($"{prefix}:ticks", "Slider ticks");
        /// <summary>"Spinner (all elements)"</summary>
        public static LocalisableString Spinner => new TranslatableString($"{prefix}:spinner", "Spinner (all elements)");
    }
}
