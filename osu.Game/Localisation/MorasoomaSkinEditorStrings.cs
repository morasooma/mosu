using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class MorasoomaSkinEditorStrings
    {
        private const string prefix = "osu.Game.Localisation.MorasoomaSkinEditor";

        /// <summary>
        /// "Morasooma components"
        /// </summary>
        public static LocalisableString Components => new TranslatableString($"{prefix}:components", "Morasooma components");

        public static LocalisableString LeaderboardStyle => text("leaderboard_style", "Leaderboard style");
        public static LocalisableString LeaderboardClassic => text("leaderboard_classic", "Classic");
        public static LocalisableString LeaderboardMorasooma => text("leaderboard_morasooma", "Morasooma (interface theme)");
        public static LocalisableString LeaderboardPanels => text("leaderboard_panels", "Score panels");
        public static LocalisableString LeaderboardTransparent => text("leaderboard_transparent", "Transparent");
        public static LocalisableString LeaderboardSolid => text("leaderboard_solid", "Solid");
        public static LocalisableString LeaderboardMisses => text("leaderboard_misses", "Show misses");
        public static LocalisableString LeaderboardPp => text("leaderboard_pp", "Show performance points");
        public static LocalisableString LeaderboardMods => text("leaderboard_mods", "Show mods");
        public static LocalisableString LeaderboardAccuracy => text("leaderboard_accuracy", "Show accuracy");
        public static LocalisableString LeaderboardCombo => text("leaderboard_combo", "Show combo");
        public static LocalisableString BlurStrength => text("blur_strength", "Blur strength");
        public static LocalisableString BackgroundTint => text("background_tint", "Theme tint opacity");
        public static LocalisableString CustomTextSize => text("custom_text_size", "Custom size");
        public static LocalisableString BeatmapDifficultyDisplay => text("beatmap_difficulty_display", "Beatmap difficulty");
        public static LocalisableString KeyScrollSpeed => text("key_scroll_speed", "Key scroll speed (px/s)");
        public static LocalisableString ScrollOverflow => text("scroll_overflow", "Scroll overflowing text");
        private static LocalisableString text(string key, string fallback) => new TranslatableString($"{prefix}:{key}", fallback);

        /// <summary>"Blur background"</summary>
        public static LocalisableString BackgroundBlur => new TranslatableString($"{prefix}:background_blur", "Blur background");

        /// <summary>"Background dim"</summary>
        public static LocalisableString BackgroundDim => new TranslatableString($"{prefix}:background_dim", "Background dim");

        /// <summary>"Background opacity"</summary>
        public static LocalisableString BackgroundOpacity => new TranslatableString($"{prefix}:background_opacity", "Background opacity");

        /// <summary>"Fit background to component"</summary>
        public static LocalisableString BackgroundFullImage => new TranslatableString($"{prefix}:background_full_image", "Fit background to component");
    }
}
