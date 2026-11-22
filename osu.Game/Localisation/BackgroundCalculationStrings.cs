using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BackgroundCalculationStrings
    {
        private const string prefix = "osu.Game.Localisation.BackgroundCalculation";

        /// <summary>
        /// "Sort and filter maps using selected mods (experimental)"
        /// </summary>
        public static LocalisableString ModdedCarouselRatings => new TranslatableString($"{prefix}:ModdedCarouselRatings", "Sort and filter maps using selected mods (experimental)");
        /// <summary>
        /// "Estimates SR/PP for the list, then calculates visible maps in the background. The order may change as results arrive. Off uses the previous saved-rating logic."
        /// </summary>
        public static LocalisableString ModdedCarouselRatingsHint => new TranslatableString($"{prefix}:ModdedCarouselRatingsHint", "Estimates SR/PP for the list, then calculates visible maps in the background. The order may change as results arrive. Off uses the previous saved-rating logic.");

        /// <summary>
        /// "Calculating difficulty and PP for new or updated maps…"
        /// </summary>
        public static LocalisableString CalculatingNewMaps => new TranslatableString($"{prefix}:CalculatingNewMaps", "Calculating difficulty and PP for new or updated maps…");
        /// <summary>
        /// "Background calculations"
        /// </summary>
        public static LocalisableString Title => new TranslatableString($"{prefix}:Title", "Background calculations");
        /// <summary>
        /// "Library preparation only. Gameplay and server PP are not affected. Existing caches are kept."
        /// </summary>
        public static LocalisableString Description => new TranslatableString($"{prefix}:Description", "Library preparation only. Gameplay and server PP are not affected. Existing caches are kept.");
        /// <summary>
        /// "Startup and CPU budget"
        /// </summary>
        public static LocalisableString General => new TranslatableString($"{prefix}:General", "Startup and CPU budget");
        /// <summary>
        /// "Calculate automatically at startup"
        /// </summary>
        public static LocalisableString Startup => new TranslatableString($"{prefix}:Startup", "Calculate automatically at startup");
        /// <summary>
        /// "Startup delay (seconds)"
        /// </summary>
        public static LocalisableString Delay => new TranslatableString($"{prefix}:Delay", "Startup delay (seconds)");
        /// <summary>
        /// "Maximum simultaneous calculations"
        /// </summary>
        public static LocalisableString Threads => new TranslatableString($"{prefix}:Threads", "Maximum simultaneous calculations");
        /// <summary>
        /// "Shared budget across modes. Import worker count changes after restart."
        /// </summary>
        public static LocalisableString ThreadsHint => new TranslatableString($"{prefix}:ThreadsHint", "Shared budget across modes. Import worker count changes after restart.");
        /// <summary>
        /// "Calculation duty cycle"
        /// </summary>
        public static LocalisableString Pace => new TranslatableString($"{prefix}:Pace", "Calculation duty cycle");
        /// <summary>
        /// "Cooperative work/rest ratio, not an exact CPU usage cap. Background startup work pauses during gameplay."
        /// </summary>
        public static LocalisableString PaceHint => new TranslatableString($"{prefix}:PaceHint", "Cooperative work/rest ratio, not an exact CPU usage cap. Background startup work pauses during gameplay.");
        /// <summary>
        /// "Enable this mode"
        /// </summary>
        public static LocalisableString Enabled => new TranslatableString($"{prefix}:Enabled", "Enable this mode");
        /// <summary>
        /// "Standard difficulty (stars)"
        /// </summary>
        public static LocalisableString SR => new TranslatableString($"{prefix}:SR", "Standard difficulty (stars)");
        /// <summary>
        /// "Potential PP for a perfect score"
        /// </summary>
        public static LocalisableString PP => new TranslatableString($"{prefix}:PP", "Potential PP for a perfect score");
        /// <summary>
        /// "PP require difficulty attributes even when the separate star-rating pass is disabled."
        /// </summary>
        public static LocalisableString PPHint => new TranslatableString($"{prefix}:PPHint", "PP require difficulty attributes even when the separate star-rating pass is disabled.");
        /// <summary>
        /// "Relax difficulty (stars)"
        /// </summary>
        public static LocalisableString RXSR => new TranslatableString($"{prefix}:RXSR", "Relax difficulty (stars)");
        /// <summary>
        /// "Potential Relax PP"
        /// </summary>
        public static LocalisableString RXPP => new TranslatableString($"{prefix}:RXPP", "Potential Relax PP");
        /// <summary>
        /// "Recalculate outdated star ratings"
        /// </summary>
        public static LocalisableString Refresh => new TranslatableString($"{prefix}:Refresh", "Recalculate outdated star ratings");
        /// <summary>
        /// "Calculate on import / map update"
        /// </summary>
        public static LocalisableString Import => new TranslatableString($"{prefix}:Import", "Calculate on import / map update");
        /// <summary>
        /// "Mode concurrency limit (0 = shared budget)"
        /// </summary>
        public static LocalisableString LocalThreads => new TranslatableString($"{prefix}:LocalThreads", "Mode concurrency limit (0 = shared budget)");
        /// <summary>
        /// "Library maintenance"
        /// </summary>
        public static LocalisableString Maintenance => new TranslatableString($"{prefix}:Maintenance", "Library maintenance");
        /// <summary>
        /// "Populate map object counts"
        /// </summary>
        public static LocalisableString Objects => new TranslatableString($"{prefix}:Objects", "Populate map object counts");
        /// <summary>
        /// "Populate missing local score statistics"
        /// </summary>
        public static LocalisableString Scores => new TranslatableString($"{prefix}:Scores", "Populate missing local score statistics");
        /// <summary>
        /// "Update online map metadata at startup"
        /// </summary>
        public static LocalisableString Online => new TranslatableString($"{prefix}:Online", "Update online map metadata at startup");
        /// <summary>
        /// "Populate user tags from local cache"
        /// </summary>
        public static LocalisableString Tags => new TranslatableString($"{prefix}:Tags", "Populate user tags from local cache");
        /// <summary>
        /// "Queue controls"
        /// </summary>
        public static LocalisableString Queue => new TranslatableString($"{prefix}:Queue", "Queue controls");
        /// <summary>
        /// "Calculate missing values"
        /// </summary>
        public static LocalisableString Run => new TranslatableString($"{prefix}:Run", "Calculate missing values");
        /// <summary>
        /// "Pause"
        /// </summary>
        public static LocalisableString Pause => new TranslatableString($"{prefix}:Pause", "Pause");
        /// <summary>
        /// "Resume"
        /// </summary>
        public static LocalisableString Resume => new TranslatableString($"{prefix}:Resume", "Resume");
        /// <summary>
        /// "Maps that failed background processing are skipped until their files change. Gameplay is not blocked."
        /// </summary>
        public static LocalisableString FailedMapsSkipped => new TranslatableString($"{prefix}:FailedMapsSkipped", "Maps that failed background processing are skipped until their files change. Gameplay is not blocked.");
        /// <summary>
        /// "Stop after the current map"
        /// </summary>
        public static LocalisableString Stop => new TranslatableString($"{prefix}:Stop", "Stop after the current map");
    }
}
