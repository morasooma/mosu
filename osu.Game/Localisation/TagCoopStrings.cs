// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class TagCoopStrings
    {
        private const string prefix = @"osu.Game.Localisation.TagCoop";

        /// <summary>
        /// "All players"
        /// </summary>
        public static LocalisableString AllPlayers => new TranslatableString(getKey(@"all_players"), @"All players");
        /// <summary>
        /// "Spin now"
        /// </summary>
        public static LocalisableString SpinNow => new TranslatableString(getKey(@"spin_now"), @"Spin now");
        /// <summary>
        /// "Your turn"
        /// </summary>
        public static LocalisableString YourTurn => new TranslatableString(getKey(@"your_turn"), @"Your turn");
        /// <summary>
        /// "Hit this combo"
        /// </summary>
        public static LocalisableString HitThisCombo => new TranslatableString(getKey(@"hit_this_combo"), @"Hit this combo");
        /// <summary>
        /// "All get ready"
        /// </summary>
        public static LocalisableString AllGetReady => new TranslatableString(getKey(@"all_get_ready"), @"All get ready");
        /// <summary>
        /// "Get ready"
        /// </summary>
        public static LocalisableString GetReady => new TranslatableString(getKey(@"get_ready"), @"Get ready");
        /// <summary>
        /// "Playing now"
        /// </summary>
        public static LocalisableString PlayingNow => new TranslatableString(getKey(@"playing_now"), @"Playing now");
        /// <summary>
        /// "Waiting for turn"
        /// </summary>
        public static LocalisableString WaitingForTurn => new TranslatableString(getKey(@"waiting_for_turn"), @"Waiting for turn");
        /// <summary>
        /// "Tag Co-op"
        /// </summary>
        public static LocalisableString TagCoop => new TranslatableString(getKey(@"tag_coop"), @"Tag Co-op");
        /// <summary>
        /// "Tag Co-op score"
        /// </summary>
        public static LocalisableString TagCoopScore => new TranslatableString(getKey(@"tag_coop_score"), @"Tag Co-op score");

        /// <summary>
        /// "Spinner in {0:0.0} s"
        /// </summary>
        public static LocalisableString SpinnerIn(double seconds) => new TranslatableString(getKey(@"spinner_in"), @"Spinner in {0:0.0} s", seconds);
        /// <summary>
        /// "Your turn in {0:0.0} s"
        /// </summary>
        public static LocalisableString YourTurnIn(double seconds) => new TranslatableString(getKey(@"your_turn_in"), @"Your turn in {0:0.0} s", seconds);
        /// <summary>
        /// "{0} · {1} ms"
        /// </summary>
        public static LocalisableString WithPing(LocalisableString status, int ping) => new TranslatableString(getKey(@"status_with_ping"), @"{0} · {1} ms", status, ping);
        /// <summary>
        /// "{0}: miss"
        /// </summary>
        public static LocalisableString RemoteMiss(string username) => new TranslatableString(getKey(@"remote_miss"), @"{0}: miss", username);
        /// <summary>
        /// "Players: {0}"
        /// </summary>
        public static LocalisableString Players(string usernames) => new TranslatableString(getKey(@"players"), @"Players: {0}", usernames);

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
