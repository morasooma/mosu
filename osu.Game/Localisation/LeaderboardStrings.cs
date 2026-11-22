// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class LeaderboardStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Leaderboard";

        /// <summary>
        /// "Couldn't fetch scores!"
        /// </summary>
        public static LocalisableString CouldntFetchScores => new TranslatableString(getKey(@"couldnt_fetch_scores"), @"Couldn't fetch scores!");

        /// <summary>
        /// "Please select a beatmap!"
        /// </summary>
        public static LocalisableString PleaseSelectABeatmap => new TranslatableString(getKey(@"please_select_a_beatmap"), @"Please select a beatmap!");

        /// <summary>
        /// "Leaderboards are not available for this ruleset!"
        /// </summary>
        public static LocalisableString LeaderboardsAreNotAvailableForThisRuleset => new TranslatableString(getKey(@"leaderboards_are_not_available_for_this_ruleset"), @"Leaderboards are not available for this ruleset!");

        /// <summary>
        /// "Leaderboards are not available for this beatmap!"
        /// </summary>
        public static LocalisableString LeaderboardsAreNotAvailableForThisBeatmap => new TranslatableString(getKey(@"leaderboards_are_not_available_for_this_beatmap"), @"Leaderboards are not available for this beatmap!");

        /// <summary>
        /// "Refresh beatmap data"
        /// </summary>
        public static LocalisableString ReloadBeatmapData => new TranslatableString(getKey(@"reload_beatmap_data"), @"Refresh beatmap data");

        /// <summary>
        /// "Couldn't refresh beatmap data."
        /// </summary>
        public static LocalisableString CouldntReloadBeatmapData => new TranslatableString(getKey(@"couldnt_reload_beatmap_data"), @"Couldn't refresh beatmap data.");

        /// <summary>
        /// "This beatmap set still exists online. Custom difficulties cannot be uploaded to the server."
        /// </summary>
        public static LocalisableString CannotUploadCustomDifficultyToServer => new TranslatableString(getKey(@"cannot_upload_custom_difficulty_to_server"), @"This beatmap set still exists online. Custom difficulties cannot be uploaded to the server.");

        /// <summary>
        /// "Upload to server"
        /// </summary>
        public static LocalisableString UploadBeatmapToServer => new TranslatableString(getKey(@"upload_beatmap_to_server"), @"Upload to server");

        /// <summary>
        /// "Help"
        /// </summary>
        public static LocalisableString BeatmapUploadHelp => new TranslatableString(getKey(@"beatmap_upload_help"), @"Help");

        /// <summary>
        /// "What should I do?"
        /// </summary>
        public static LocalisableString BeatmapUploadHelpHeader => new TranslatableString(getKey(@"beatmap_upload_help_header"), @"What should I do?");

        /// <summary>
        /// "If this beatmap already exists on the server, refresh its data using the refresh button. If it isn't on the server yet, use the upload button to help the community."
        /// </summary>
        public static LocalisableString BeatmapUploadHelpBody => new TranslatableString(getKey(@"beatmap_upload_help_body"), @"If this beatmap already exists on the server, refresh its data using the refresh button. If it isn't on the server yet, use the upload button to help the community.");

        /// <summary>
        /// "Preparing beatmap package..."
        /// </summary>
        public static LocalisableString PreparingBeatmapUpload => new TranslatableString(getKey(@"preparing_beatmap_upload"), @"Preparing beatmap package...");

        /// <summary>
        /// "Uploading beatmap to server..."
        /// </summary>
        public static LocalisableString UploadingBeatmapToServer => new TranslatableString(getKey(@"uploading_beatmap_to_server"), @"Uploading beatmap to server...");

        /// <summary>
        /// "Beatmap uploaded to server."
        /// </summary>
        public static LocalisableString BeatmapUploadedToServer => new TranslatableString(getKey(@"beatmap_uploaded_to_server"), @"Beatmap uploaded to server.");

        /// <summary>
        /// "Couldn't upload beatmap to server."
        /// </summary>
        public static LocalisableString CouldntUploadBeatmapToServer => new TranslatableString(getKey(@"couldnt_upload_beatmap_to_server"), @"Couldn't upload beatmap to server.");

        /// <summary>
        /// "No records yet!"
        /// </summary>
        public static LocalisableString NoRecordsYet => new TranslatableString(getKey(@"no_records_yet"), @"No records yet!");

        /// <summary>
        /// "Please sign in to view online leaderboards!"
        /// </summary>
        public static LocalisableString PleaseSignInToViewOnlineLeaderboards => new TranslatableString(getKey(@"please_sign_in_to_view_online_leaderboards"), @"Please sign in to view online leaderboards!");

        /// <summary>
        /// "Please invest in a Morasooma Supporter tag to view this leaderboard!"
        /// </summary>
        public static LocalisableString PleaseInvestInAnOsuSupporterTagToViewThisLeaderboard => new TranslatableString(getKey(@"please_invest_in_an_osu_supporter_tag_to_view_this_leaderboard"), @"Please invest in a Morasooma Supporter tag to view this leaderboard!");

        /// <summary>
        /// "You are not on a team. Maybe you should join one!"
        /// </summary>
        public static LocalisableString NoTeam => new TranslatableString(getKey(@"no_team"), @"You are not on a team. Maybe you should join one!");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
