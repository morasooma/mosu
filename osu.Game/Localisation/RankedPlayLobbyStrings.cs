using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class RankedPlayLobbyStrings
    {
        private static LocalisableString text(string key, string fallback, params object[] args)
            => new TranslatableString($"osu.Game.Localisation.RankedPlayLobby:{key}", fallback, args);

        public static LocalisableString PoolSort => text("pool_sort", "Sort by");
        public static LocalisableString PoolStarsAscending => text("pool_stars_ascending", "Stars: low to high");
        public static LocalisableString PoolStarsDescending => text("pool_stars_descending", "Stars: high to low");
        public static LocalisableString PoolNewestFirst => text("pool_newest_first", "Newest additions first");
        public static LocalisableString PoolOldestFirst => text("pool_oldest_first", "Oldest additions first");
        public static LocalisableString PoolTitle => text("pool_title", "Title");
        public static LocalisableString PoolArtist => text("pool_artist", "Artist");
        public static LocalisableString PoolOnlyMine => text("pool_only_mine", "Added by me");
        public static LocalisableString PoolOnlyMineHint => text("pool_only_mine_hint", "Only maps you added to this shared pool, not maps you created.");
        public static LocalisableString PoolShowing(int shown, int total) => text("pool_showing", "Showing {0} of {1} beatmaps", shown, total);

        public static LocalisableString ActiveMatches => text("active_matches", "Ongoing matches");
        public static LocalisableString NoActiveMatches => text("no_active_matches", "No matches in progress");
        public static LocalisableString Spectate => text("spectate", "Spectate");
        public static LocalisableString Round(int round) => text("round", "Round {0}", round);
    }
}
