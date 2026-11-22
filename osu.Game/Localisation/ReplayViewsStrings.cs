using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ReplayViewsStrings
    {
        private const string prefix = "osu.Game.Localisation.ReplayViews";

        public static LocalisableString Title => new TranslatableString($"{prefix}:title", "Replay views");
        public static LocalisableString MostViewed => new TranslatableString($"{prefix}:most_viewed", "Most viewed replays");
        public static LocalisableString Views => new TranslatableString($"{prefix}:views", "Views");
    }
}
