using osu.Framework.Localisation;
using osu.Game.Users;

namespace osu.Game.Localisation
{
    public static class PresenceStrings
    {
        private const string prefix = "osu.Game.Localisation.Presence";

        public static LocalisableString CurrentlyOnline => text("currently_online", "Currently online");
        private static LocalisableString text(string key, string fallback, params object[] args)
            => new TranslatableString($"{prefix}:{key}", fallback, args);

        public static LocalisableString Client(UserPresence presence)
        {
            LocalisableString kind = presence.BuildKind switch
            {
                "Release" => text("release", "Release"),
                "Dev" => text("dev", "Dev"),
                "Custom" => text("custom", "Custom build"),
                _ => string.Empty
            };
            if (string.IsNullOrEmpty(presence.Platform)) return kind;
            if (string.IsNullOrEmpty(presence.BuildKind)) return presence.Platform;
            return text("client", "{0} · {1}", presence.Platform, kind);
        }

        public static LocalisableString Activity(UserActivity activity)
        {
            if (activity is UserActivity.SpectatingMultiplayerGame)
                return text("spectating_multi", "Spectating multiplayer");
            if (activity is UserActivity.InGame game)
            {
                string mode = game.RulesetID switch
                {
                    0 or 4 or 5 => "Standard", 1 or 6 => "Taiko", 2 or 7 => "Catch",
                    3 => "Mania", 10 => "Dodge", _ => game.RulesetPlayingVerb
                };
                bool relax = System.Array.IndexOf(game.ModAcronyms ?? System.Array.Empty<string>(), "RX") >= 0 || game.RulesetID is 4 or 6 or 7;
                bool autopilot = System.Array.IndexOf(game.ModAcronyms ?? System.Array.Empty<string>(), "AP") >= 0 || game.RulesetID == 5;
                LocalisableString variant = relax ? text("relax", "{0} Relax", mode)
                    : autopilot ? text("autopilot", "{0} Autopilot", mode) : mode;
                LocalisableString status = game switch
                {
                    UserActivity.InMultiplayerGame { RankedPlayRoomID: not null } => text("playing_ranked", "Playing Ranked Play · {0}", variant),
                    UserActivity.InMultiplayerGame => text("playing_multi", "Playing multiplayer · {0}", variant),
                    UserActivity.PlayingDailyChallenge => text("playing_daily", "Playing daily challenge · {0}", variant),
                    _ => text("playing", "Playing · {0}", variant)
                };
                return game.ModAcronyms?.Length > 0
                    ? text("mods", "{0} · {1}", status, string.Join(" + ", game.ModAcronyms)) : status;
            }
            return activity switch
            {
                UserActivity.ChoosingBeatmap => text("choosing", "Choosing a beatmap"),
                UserActivity.TestingBeatmap => text("testing", "Testing a beatmap"),
                UserActivity.ModdingBeatmap => text("modding", "Modding a beatmap"),
                UserActivity.EditingBeatmap => text("editing", "Editing a beatmap"),
                UserActivity.SpectatingUser => text("spectating", "Spectating a player"),
                UserActivity.WatchingReplay => text("replay", "Watching a replay"),
                UserActivity.SearchingForLobby => text("searching", "Looking for a lobby"),
                UserActivity.InLobby { IsRankedPlay: true } => text("ranked", "Playing Ranked Play"),
                UserActivity.InLobby => text("lobby", "In a lobby"),
                UserActivity.InDailyChallengeLobby => text("daily_lobby", "In daily challenge lobby"),
                _ => activity.GetStatus()
            };
        }
    }
}
