using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class RankedPlayModBanStrings
    {
        private static LocalisableString text(string key, string fallback, params object[] args)
            => new TranslatableString($"osu.Game.Localisation.RankedPlayModBan:{key}", fallback, args);

        public static LocalisableString Heading => text("heading", "Random mod bans");
        public static LocalisableString Caption => text("caption", "Ban one mod from the roulette for this match, or skip.");
        public static LocalisableString Spectating => text("spectating", "Players are choosing their random mod bans.");
        public static LocalisableString Skip => text("skip", "Skip ban");
        public static LocalisableString Ban(string mod) => text("ban", "Ban {0}", mod);
        public static LocalisableString YourBan(LocalisableString mod) => text("your_ban", "Your ban: {0}", mod);
        public static LocalisableString OpponentBan(LocalisableString mod) => text("opponent_ban", "Opponent's ban: {0}", mod);
        public static LocalisableString NoBan => text("no_ban", "None");
        public static LocalisableString Choosing => text("choosing", "Choosing...");
        public static LocalisableString OpponentSelecting => text("opponent_selecting", "Opponent is selecting");
        public static LocalisableString OpponentLocked => text("opponent_locked", "Banned by opponent");
        public static LocalisableString Waiting => text("waiting", "Waiting for your opponent...");
        public static LocalisableString Retry => text("retry", "Ban could not be confirmed. Please try again.");
    }
}
