using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;

namespace osu.Game.Graphics.Backgrounds.AI
{
    public enum BackgroundSafetyCombination
    {
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.AnyModel))]
        Any,
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.AllModels))]
        All,
    }

    // Nullable results distinguish disabled/not-yet-checked models from a confident safe prediction.
    internal sealed record BackgroundSafetyScores(BackgroundClassification? General, BackgroundClassification? Anime);

    internal sealed record BackgroundSafetyPolicy(bool GeneralEnabled, bool AnimeEnabled, double GeneralThreshold, double AnimeThreshold,
                                                  BackgroundFilterMode Categories, BackgroundSafetyCombination Combination, double BlurStrength)
    {
        public static BackgroundSafetyPolicy FromConfig(OsuConfigManager config) => new BackgroundSafetyPolicy(
            config.Get<bool>(OsuSetting.ForkAiGeneralModel), config.Get<bool>(OsuSetting.ForkAiAnimeModel),
            config.Get<double>(OsuSetting.ForkAiGeneralThreshold), config.Get<double>(OsuSetting.ForkAiAnimeThreshold),
            config.Get<BackgroundFilterMode>(OsuSetting.ForkAiBackgroundFilterMode),
            config.Get<BackgroundSafetyCombination>(OsuSetting.ForkAiCombination), config.Get<double>(OsuSetting.ForkAiBlurStrength));

        public bool NeedsGeneral => GeneralEnabled && Categories != BackgroundFilterMode.Hentai;
        public bool NeedsAnime => AnimeEnabled;

        public bool ShouldBlur(BackgroundSafetyScores scores)
        {
            // No active classifiers, missing data or failed inference must never silently reveal an image.
            if (!NeedsGeneral && !NeedsAnime)
                return true;
            if (NeedsGeneral && scores.General == null || NeedsAnime && scores.Anime == null)
                return true;
            // General output is [NSFL, NSFW, SFW]; anime output is [safe, r15, r18].
            bool general = NeedsGeneral && scores.General!.Value.Hentai >= GeneralThreshold;
            bool anime = NeedsAnime && (Categories == BackgroundFilterMode.Suggestive
                ? scores.Anime!.Value.Hentai + scores.Anime.Value.Neutral
                : scores.Anime!.Value.Neutral) >= AnimeThreshold;
            return Combination == BackgroundSafetyCombination.All
                ? (!NeedsGeneral || general) && (!NeedsAnime || anime)
                : general || anime;
        }
    }
}
