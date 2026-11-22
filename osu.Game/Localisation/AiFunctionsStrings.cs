using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class AiFunctionsStrings
    {
        private const string prefix = "osu.Game.Localisation.AiFunctions";

        public static LocalisableString Nsfw => new TranslatableString($"{prefix}:Nsfw", "NSFW filter");
        public static LocalisableString Distortion => new TranslatableString($"{prefix}:Distortion", "Background distortion");
        public static LocalisableString Configure => new TranslatableString($"{prefix}:Configure", "Configure");
        public static LocalisableString SettingsDescription => new TranslatableString($"{prefix}:SettingsDescription", "Select an AI function to configure.");
        /// <summary>
        /// "AI functions"
        /// </summary>
        public static LocalisableString Title => new TranslatableString($"{prefix}:Title", "AI functions");
        /// <summary>
        /// "Blur unsafe backgrounds (experimental)"
        /// </summary>
        public static LocalisableString Blur => new TranslatableString($"{prefix}:Blur", "Blur unsafe backgrounds (experimental)");
        /// <summary>
        /// "Local SwiftFormer XS + Anime Rating. Downloads up to 30 MB only when enabled and needed. Unchecked or failed backgrounds stay hidden. Can make mistakes; video/storyboards are not filtered."
        /// </summary>
        public static LocalisableString Description => new TranslatableString($"{prefix}:Description", "Local SwiftFormer XS + Anime Rating. Downloads up to 30 MB only when enabled and needed. Unchecked or failed backgrounds stay hidden. Can make mistakes; video/storyboards are not filtered.");
        /// <summary>
        /// "Content to filter"
        /// </summary>
        public static LocalisableString Categories => new TranslatableString($"{prefix}:Categories", "Content to filter");
        /// <summary>
        /// "NSFW + explicit anime (R18)"
        /// </summary>
        public static LocalisableString Explicit => new TranslatableString($"{prefix}:Explicit", "NSFW + explicit anime (R18)");
        /// <summary>
        /// "NSFW + suggestive anime (R15 + R18)"
        /// </summary>
        public static LocalisableString Suggestive => new TranslatableString($"{prefix}:Suggestive", "NSFW + suggestive anime (R15 + R18)");
        /// <summary>
        /// "Only explicit anime (Anime Rating R18)"
        /// </summary>
        public static LocalisableString Hentai => new TranslatableString($"{prefix}:Hentai", "Only explicit anime (Anime Rating R18)");
        /// <summary>
        /// "SwiftFormer XS — general NSFW classifier (13 MB)"
        /// </summary>
        public static LocalisableString GeneralModel => new TranslatableString($"{prefix}:GeneralModel", "SwiftFormer XS — general NSFW classifier (13 MB)");
        /// <summary>
        /// "Anime Rating — anime classifier (17 MB)"
        /// </summary>
        public static LocalisableString AnimeModel => new TranslatableString($"{prefix}:AnimeModel", "Anime Rating — anime classifier (17 MB)");
        /// <summary>
        /// "SwiftFormer threshold"
        /// </summary>
        public static LocalisableString GeneralThreshold => new TranslatableString($"{prefix}:GeneralThreshold", "SwiftFormer threshold");
        /// <summary>
        /// "Anime Rating threshold"
        /// </summary>
        public static LocalisableString AnimeThreshold => new TranslatableString($"{prefix}:AnimeThreshold", "Anime Rating threshold");
        /// <summary>
        /// "Lower = more censorship, fewer misses, more false positives. Scores are reused from fork.realm; changing the threshold needs no new inference."
        /// </summary>
        public static LocalisableString ThresholdHint => new TranslatableString($"{prefix}:ThresholdHint", "Lower = more censorship, fewer misses, more false positives. Scores are reused from fork.realm; changing the threshold needs no new inference.");
        /// <summary>
        /// "Combine active classifiers"
        /// </summary>
        public static LocalisableString Combination => new TranslatableString($"{prefix}:Combination", "Combine active classifiers");
        /// <summary>
        /// "Any is safer against misses. All can miss content noticed by only one model. Anime-only mode does not use SwiftFormer. With no active models, backgrounds remain hidden."
        /// </summary>
        public static LocalisableString CombinationHint => new TranslatableString($"{prefix}:CombinationHint", "Any is safer against misses. All can miss content noticed by only one model. Anime-only mode does not use SwiftFormer. With no active models, backgrounds remain hidden.");
        /// <summary>
        /// "Any model flags it (recommended)"
        /// </summary>
        public static LocalisableString AnyModel => new TranslatableString($"{prefix}:AnyModel", "Any model flags it (recommended)");
        /// <summary>
        /// "All active models flag it (less censorship)"
        /// </summary>
        public static LocalisableString AllModels => new TranslatableString($"{prefix}:AllModels", "All active models flag it (less censorship)");
        /// <summary>
        /// "Safety blur strength"
        /// </summary>
        public static LocalisableString BlurStrength => new TranslatableString($"{prefix}:BlurStrength", "Safety blur strength");
        /// <summary>
        /// "Restore strict defaults (both models, 30%, any)"
        /// </summary>
        public static LocalisableString Reset => new TranslatableString($"{prefix}:Reset", "Restore strict defaults (both models, 30%, any)");
        /// <summary>
        /// "Disabled — models not loaded"
        /// </summary>
        public static LocalisableString Disabled => new TranslatableString($"{prefix}:Disabled", "Disabled — models not loaded");
        /// <summary>
        /// "Downloading / loading safety classifier…"
        /// </summary>
        public static LocalisableString Loading => new TranslatableString($"{prefix}:Loading", "Downloading / loading safety classifier…");
        /// <summary>
        /// "Ready — 1 background worker, automatic CPU budget"
        /// </summary>
        public static LocalisableString Ready => new TranslatableString($"{prefix}:Ready", "Ready — 1 background worker, automatic CPU budget");
        /// <summary>
        /// "Waiting for spare CPU / gameplay to finish"
        /// </summary>
        public static LocalisableString Waiting => new TranslatableString($"{prefix}:Waiting", "Waiting for spare CPU / gameplay to finish");
        /// <summary>
        /// "Analysis unavailable — background hidden; change settings or disable the filter to reveal"
        /// </summary>
        public static LocalisableString Failed => new TranslatableString($"{prefix}:Failed", "Analysis unavailable — background hidden; change settings or disable the filter to reveal");
        /// <summary>
        /// "No active classifiers — backgrounds stay hidden"
        /// </summary>
        public static LocalisableString NoModels => new TranslatableString($"{prefix}:NoModels", "No active classifiers — backgrounds stay hidden");
        /// <summary>
        /// Neural background distortion — Tiny-SD + LCM
        /// </summary>
        public static LocalisableString StillLife => new TranslatableString($"{prefix}:StillLife", "Neural background distortion — Tiny-SD + LCM");
        /// <summary>
        /// Downloads original model weights from Hugging Face and the runtime from GitHub automatically. Prepares locally without Python; about 1.06 GB download, 2.5 GB temporary free space, under 500 MB installed. Runs outside gameplay and caches results.
        /// </summary>
        public static LocalisableString StillLifeHint => new TranslatableString($"{prefix}:StillLifeHint", "Downloads original model weights from Hugging Face and the runtime from GitHub automatically. Prepares locally without Python; about 1.06 GB download, 2.5 GB temporary free space, under 500 MB installed. Runs outside gameplay and caches results.");
        /// <summary>
        /// Processing device
        /// </summary>
        public static LocalisableString Backend => new TranslatableString($"{prefix}:Backend", "Processing device");
        /// <summary>
        /// CPU
        /// </summary>
        public static LocalisableString Cpu => new TranslatableString($"{prefix}:Cpu", "CPU");
        /// <summary>
        /// GPU — Vulkan
        /// </summary>
        public static LocalisableString Gpu => new TranslatableString($"{prefix}:Gpu", "GPU — Vulkan");
        /// <summary>
        /// Processing resolution
        /// </summary>
        public static LocalisableString Resolution => new TranslatableString($"{prefix}:Resolution", "Processing resolution");
        /// <summary>
        /// 256 px — fastest, lower quality
        /// </summary>
        public static LocalisableString Resolution256 => new TranslatableString($"{prefix}:Resolution256", "256 px — fastest, lower quality");
        /// <summary>
        /// 384 px — balanced
        /// </summary>
        public static LocalisableString Resolution384 => new TranslatableString($"{prefix}:Resolution384", "384 px — balanced");
        /// <summary>
        /// 512 px — highest quality, slower
        /// </summary>
        public static LocalisableString Resolution512 => new TranslatableString($"{prefix}:Resolution512", "512 px — highest quality, slower");
        /// <summary>
        /// Successive redraws
        /// </summary>
        public static LocalisableString Passes => new TranslatableString($"{prefix}:Passes", "Successive redraws");
        /// <summary>
        /// LCM steps per redraw
        /// </summary>
        public static LocalisableString Steps => new TranslatableString($"{prefix}:Steps", "LCM steps per redraw");
        /// <summary>
        /// Distortion strength
        /// </summary>
        public static LocalisableString Strength => new TranslatableString($"{prefix}:Strength", "Distortion strength");
        /// <summary>
        /// Seed (same settings reuse the same result)
        /// </summary>
        public static LocalisableString Seed => new TranslatableString($"{prefix}:Seed", "Seed (same settings reuse the same result)");
        /// <summary>
        /// CPU threads
        /// </summary>
        public static LocalisableString Threads => new TranslatableString($"{prefix}:Threads", "CPU threads");
        /// <summary>
        /// Save prepared backgrounds to disk
        /// </summary>
        public static LocalisableString CacheEnabled => new TranslatableString($"{prefix}:CacheEnabled", "Save prepared backgrounds to disk");
        /// <summary>
        /// Background cache limit (MiB; models excluded)
        /// </summary>
        public static LocalisableString CacheLimit => new TranslatableString($"{prefix}:CacheLimit", "Background cache limit (MiB; models excluded)");
        /// <summary>
        /// Clear prepared background cache
        /// </summary>
        public static LocalisableString ClearCache => new TranslatableString($"{prefix}:ClearCache", "Clear prepared background cache");
        /// <summary>
        /// Still Life package manifest URL
        /// </summary>
        public static LocalisableString PackageUrl => new TranslatableString($"{prefix}:PackageUrl", "Still Life package manifest URL");
        /// <summary>
        /// Advanced: trusted HTTPS manifest URL; PLATFORM becomes linux-x64 or win-x64. Empty uses an already installed package. No public package is configured yet.
        /// </summary>
        public static LocalisableString PackageHint => new TranslatableString($"{prefix}:PackageHint", "Advanced: trusted HTTPS manifest URL; PLATFORM becomes linux-x64 or win-x64. Empty uses an already installed package. No public package is configured yet.");
        /// <summary>
        /// Background distortion disabled
        /// </summary>
        public static LocalisableString StillLifeDisabled => new TranslatableString($"{prefix}:StillLifeDisabled", "Background distortion disabled");
        /// <summary>
        /// Background distortion: downloading originals / preparing locally…
        /// </summary>
        public static LocalisableString StillLifeLoading => new TranslatableString($"{prefix}:StillLifeLoading", "Background distortion: downloading originals / preparing locally…");
        /// <summary>
        /// Background distortion: processing selected background…
        /// </summary>
        public static LocalisableString StillLifeProcessing => new TranslatableString($"{prefix}:StillLifeProcessing", "Background distortion: processing selected background…");
        /// <summary>
        /// Background distortion ready — cached results are reused
        /// </summary>
        public static LocalisableString StillLifeReady => new TranslatableString($"{prefix}:StillLifeReady", "Background distortion ready — cached results are reused");
        /// <summary>
        /// Background distortion: download failed from the original source.
        /// </summary>
        public static LocalisableString StillLifeDownloadFailed(string host) => new TranslatableString($"{prefix}:StillLifeDownloadFailed", "Background distortion: download failed from {0}. A new background request will retry after 30 seconds.", host);

        /// <summary>
        /// Background distortion: free 2.5 GB on the drive containing the game data, then try again.
        /// </summary>
        public static LocalisableString StillLifeInsufficientSpace => new TranslatableString($"{prefix}:StillLifeInsufficientSpace", "Background distortion: free 2.5 GB on the drive containing the game data, then try again.");

        /// <summary>
        /// Background distortion unavailable — check connection, free space or selected device
        /// </summary>
        public static LocalisableString StillLifeFailed => new TranslatableString($"{prefix}:StillLifeFailed", "Background distortion unavailable — check connection, free space or selected device");
    }
}
