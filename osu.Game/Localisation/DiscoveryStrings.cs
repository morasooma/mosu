using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class DiscoveryStrings
    {
        public static LocalisableString AudioTitle => text("AudioTitle", "Audio recommendations — CLAP Music");
        public static LocalisableString AudioHint => text("AudioHint", "Optional local audio analysis. Downloads 278 MB of CLAP Music weights and the runtime only when Discovery needs them. Preview downloads are anonymous; audio features stay local. LAION CLAP Music and its ONNX conversion: Apache-2.0.");
        public static LocalisableString AudioAuto => text("AudioAuto", "Automatic GPU if available, otherwise CPU");
        public static LocalisableString AudioBackgroundSearch => text("AudioBackgroundSearch", "Keep finding maps in the background");
        public static LocalisableString AudioBackgroundSearchHint => text("AudioBackgroundSearchHint", "Keep searching and analysing audio after closing Discovery, including while you play. Turn this off to only run the search with Discovery open.");
        public static LocalisableString AudioCandidates => text("AudioCandidates", "Audio candidates per batch (up to 100 ready recommendations)");
        public static LocalisableString AudioQueueEmpty => text("AudioQueueEmpty", "No more audio-checked maps in this search. Change filters or retry.");
        public static LocalisableString AudioClearCache => text("AudioClearCache", "Clear audio feature cache");
        public static LocalisableString AudioDisabled => text("AudioDisabled", "Audio analysis disabled — model not loaded");
        public static LocalisableString AudioIdle => text("AudioIdle", "Audio analysis enabled — waiting for Discovery");
        public static LocalisableString AudioLoading => text("AudioLoading", "Downloading / loading CLAP Music audio model…");
        public static LocalisableString AudioAnalysing => text("AudioAnalysing", "Comparing candidate audio with your examples…");
        public static LocalisableString AudioProcessing(string device) => text("AudioProcessing", "Analysing audio locally — {0}", device);
        public static LocalisableString AudioReady(string device) => text("AudioReady", "Audio model ready — {0}; cached features reused", device);
        public static LocalisableString AudioFailed => text("AudioFailed", "Audio analysis unavailable — check AI settings, network and free space. Unanalysed maps are not passed off as audio matches.");
        public static LocalisableString AudioSimilar => text("AudioSimilar", "Similar audio — local CLAP Music analysis");
        private const string prefix = "osu.Game.Localisation.Discovery";
        /// <summary>
        /// "Map recommendations"
        /// </summary>
        public static LocalisableString Title => text("title", "Map recommendations");
        /// <summary>
        /// "New maps from your Mosu top plays"
        /// </summary>
        public static LocalisableString Description => text("description", "New maps from your examples and play history");
        /// <summary>
        /// "Finding maps…"
        /// </summary>
        public static LocalisableString Loading => text("loading", "Finding maps…");
        /// <summary>
        /// "Sign in to Mosu to use your top plays."
        /// </summary>
        public static LocalisableString Login => text("login", "Sign in to Mosu to use your examples and play history.");
        /// <summary>
        /// "No top plays in this mode yet. Try another mode or play a few maps."
        /// </summary>
        public static LocalisableString NoScores => text("no_scores", "No top plays in this mode yet. Try another mode or play a few maps.");
        /// <summary>
        /// "No new maps found in the checked catalogue pages. Change the filters or retry."
        /// </summary>
        public static LocalisableString Empty => text("empty", "No new maps found in the checked catalogue pages. Change the filters or retry.");
        /// <summary>
        /// "Could not load recommendations. Try again."
        /// </summary>
        public static LocalisableString Failed => text("failed", "Could not load recommendations. Try again.");
        /// <summary>
        /// "Gameplay preview unavailable. You can still save or download this map."
        /// </summary>
        public static LocalisableString MediaFailed => text("media_failed", "Gameplay preview unavailable. You can still save or download this map.");
        /// <summary>
        /// "Loading gameplay preview…"
        /// </summary>
        public static LocalisableString PreviewLoading => text("preview_loading", "Loading gameplay preview…");
        /// <summary>
        /// "Audio preview only. Download the map to watch synchronized gameplay."
        /// </summary>
        public static LocalisableString NoAnchor => text("no_anchor", "Audio preview only. Download the map to watch synchronized gameplay.");
        /// <summary>
        /// "Skip"
        /// </summary>
        public static LocalisableString Next => text("next", "Skip");
        /// <summary>
        /// "Save for later"
        /// </summary>
        public static LocalisableString Save => text("save", "Save for later");
        /// <summary>
        /// "Download mapset"
        /// </summary>
        public static LocalisableString Download => text("download", "Download mapset");
        /// <summary>
        /// "Load more"
        /// </summary>
        public static LocalisableString Refresh => text("refresh", "Load more");
        /// <summary>
        /// "Saved"
        /// </summary>
        public static LocalisableString Saved => text("saved", "Saved");
        /// <summary>
        /// "Recommendations"
        /// </summary>
        public static LocalisableString ForYou => text("for_you", "Recommendations");
        /// <summary>
        /// "Pause preview"
        /// </summary>
        public static LocalisableString Pause => text("pause", "Pause preview");
        /// <summary>
        /// "Play preview"
        /// </summary>
        public static LocalisableString Play => text("play", "Play preview");
        /// <summary>
        /// "Close"
        /// </summary>
        public static LocalisableString Close => text("close", "Close");
        /// <summary>
        /// "Artist matches your top plays or likes"
        /// </summary>
        public static LocalisableString Artist => text("artist", "Artist from your played or saved music");
        public static LocalisableString Listeners => text("listeners", "Played by listeners with similar musical taste");
        public static LocalisableString Music => text("music", "Matches your music genres or tags");
        /// <summary>
        /// "Mapper matches your top plays or likes"
        /// </summary>
        public static LocalisableString Mapper => text("mapper", "Mapper matches your top plays or likes");
        /// <summary>
        /// "Near the difficulty and tempo of your top plays"
        /// </summary>
        public static LocalisableString Similar => text("similar", "More maps from the catalogue");
        /// <summary>
        /// "Top plays and likes guide the order. All map statuses included; installed maps hidden."
        /// </summary>
        public static LocalisableString LocalOnly => text("local_only", "Choose top plays, frequently played maps or your own examples. All statuses included; installed songs hidden.");
        /// <summary>
        /// "Could not save your choice on this device."
        /// </summary>
        public static LocalisableString SaveFailed => text("save_failed", "Could not save your choice on this device.");
        /// <summary>
        /// "Discovery currently supports osu!, taiko, catch and mania."
        /// </summary>
        public static LocalisableString Unsupported => text("unsupported", "Discovery currently supports Standard, Taiko, Catch and Mania.");
        /// <summary>
        /// "Save a map to return to it here."
        /// </summary>
        public static LocalisableString EmptySaved => text("empty_saved", "Save a map to return to it here.");
        /// <summary>
        /// "{0} maps left · {1} saved"
        /// </summary>
        public static LocalisableString Progress(int remaining, int saved) => new TranslatableString($"{prefix}:progress", "{0} maps left · {1} saved", remaining, saved);
        /// <summary>
        /// "Minimum stars"
        /// </summary>
        public static LocalisableString MinStars => text("min_stars", "Minimum stars");
        /// <summary>
        /// "Maximum stars"
        /// </summary>
        public static LocalisableString MaxStars => text("max_stars", "Maximum stars");
        /// <summary>
        /// "Minimum BPM"
        /// </summary>
        public static LocalisableString MinBpm => text("min_bpm", "Minimum BPM");
        /// <summary>
        /// "Maximum BPM"
        /// </summary>
        public static LocalisableString MaxBpm => text("max_bpm", "Maximum BPM");
        /// <summary>
        /// "Maximum length (min)"
        /// </summary>
        public static LocalisableString MaxLength => text("max_length", "Maximum length (min)");
        /// <summary>
        /// "Apply filters"
        /// </summary>
        public static LocalisableString Apply => text("apply", "Apply filters");
        /// <summary>
        /// "Replay clip"
        /// </summary>
        public static LocalisableString Repeat => text("repeat", "Replay clip");
        /// <summary>
        /// "Download &amp; watch full map"
        /// </summary>
        public static LocalisableString FullPreview => text("full_preview", "Download & watch full map");
        /// <summary>
        /// "Waiting for map download…"
        /// </summary>
        public static LocalisableString FullLoading => text("full_loading", "Waiting for map download…");
        /// <summary>
        /// "Map status"
        /// </summary>
        public static LocalisableString MapStatus => text("map_status", "Map status");
        public static LocalisableString GameplayOsuOnly => text("gameplay_osu_only", "Gameplay similarity currently supports Standard only");
        public static LocalisableString GameplaySimilar => text("gameplay_similar", "Gameplay similar to your selected examples");
        public static LocalisableString CommunityStyle => text("community_style", "Play style: confirmed user tags");
        public static LocalisableString EstimatedStyle => text("estimated_style", "Play style: difficulty-calculator estimate");
        public static LocalisableString TasteMode => text("taste_mode", "By taste");
        public static LocalisableString GameplayMode => text("gameplay_mode", "By gameplay");
        public static LocalisableString GameplayType => text("gameplay_type", "Play style");
        public static LocalisableString Streams => text("streams", "Streams");
        public static LocalisableString Jumps => text("jumps", "Jumps");
        public static LocalisableString Rhythm => text("rhythm", "Rhythm changes");
        public static LocalisableString Sliders => text("sliders", "Sliders");
        public static LocalisableString Stamina => text("stamina", "Stamina");
        public static LocalisableString Catalogue => text("catalogue", "Map catalogue");
        public static LocalisableString OfficialUnavailable => text("official_unavailable", "Connect the official account or choose Auto / a mirror in Filters.");
        public static LocalisableString Filters => text("filters", "Filters");
        public static LocalisableString MoreFilters => text("more_filters", "More filters");
        public static LocalisableString Retry => text("retry", "Retry search");
        public static LocalisableString SeedSource => text("seed_source", "Recommendation examples");
        public static LocalisableString MixedSeeds => text("mixed_seeds", "Top and frequently played");
        public static LocalisableString TopSeeds => text("top_seeds", "Top plays");
        public static LocalisableString FrequentSeeds => text("frequent_seeds", "Frequently played");
        public static LocalisableString ManualSeeds => text("manual_seeds", "My examples");
        public static LocalisableString ExampleMaps => text("example_maps", "Example maps");
        public static LocalisableString ExampleMapsHint => text("example_maps_hint", "Up to 8 map links or difficulty IDs");
        public static LocalisableString InvalidExamples => text("invalid_examples", "Add up to 8 Morasooma / osu! map links or difficulty IDs.");
        public static LocalisableString ExamplesUnavailable => text("examples_unavailable", "Could not load the examples in this mode. Check the links and retry.");
        private static LocalisableString text(string key, string fallback) => new TranslatableString($"{prefix}:{key}", fallback);
        private static LocalisableString text(string key, string fallback, string device) => new TranslatableString($"{prefix}:{key}", fallback, device);
    }
}
