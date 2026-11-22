using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class DailyChallengeProposalStrings
    {
        private const string prefix = "osu.Game.Localisation.DailyChallengeProposals";
        private static LocalisableString text(string key, string fallback, params object[] args) => new TranslatableString($"{prefix}:{key}", fallback, args);
        /// <summary>"#{0} · Required: {1} · Optional: {2}"</summary>
        public static LocalisableString ModSummary(long id, string required, string allowed) => text("mod_summary", "#{0} · Required: {1} · Optional: {2}", id, required, allowed);
        /// <summary>"Propose a beatmap"</summary>
        public static LocalisableString Propose => text("propose", "Propose a beatmap");
        /// <summary>"Submit for review"</summary>
        public static LocalisableString Submit => text("submit", "Submit for review");
        /// <summary>"Proposals"</summary>
        public static LocalisableString Requests => text("requests", "Proposals");
        /// <summary>"Upcoming daily challenges"</summary>
        public static LocalisableString Queue => text("queue", "Upcoming daily challenges");
        /// <summary>"Awaiting review"</summary>
        public static LocalisableString Pending => text("pending", "Awaiting review");
        /// <summary>"Add a beatmap"</summary>
        public static LocalisableString Add => text("add", "Add a beatmap");
        /// <summary>"Add to queue"</summary>
        public static LocalisableString AddToQueue => text("add_to_queue", "Add to queue");
        /// <summary>"Edit settings"</summary>
        public static LocalisableString Edit => text("edit", "Edit settings");
        /// <summary>"Save settings"</summary>
        public static LocalisableString Save => text("save", "Save settings");
        /// <summary>"Reject / remove"</summary>
        public static LocalisableString Reject => text("reject", "Reject / remove");
        /// <summary>"Move up"</summary>
        public static LocalisableString Up => text("up", "Move up");
        /// <summary>"Move down"</summary>
        public static LocalisableString Down => text("down", "Move down");
        /// <summary>"Next page"</summary>
        public static LocalisableString More => text("more", "Next page");
        /// <summary>"Previous page"</summary>
        public static LocalisableString Previous => text("previous", "Previous page");
        /// <summary>"Refresh"</summary>
        public static LocalisableString Refresh => text("refresh", "Refresh");
        /// <summary>"Saving…"</summary>
        public static LocalisableString Saving => text("saving", "Saving…");
        /// <summary>"Loading…"</summary>
        public static LocalisableString Loading => text("loading", "Loading…");
        /// <summary>"Staff submissions have no daily limit."</summary>
        public static LocalisableString StaffQuota => text("staff_quota", "Staff submissions have no daily limit.");
        /// <summary>"Daily limit reached. Resets at 00:00 UTC (1 proposal, or 3 with supporter)."</summary>
        public static LocalisableString Limit => text("limit", "Daily limit reached. Resets at 00:00 UTC (1 proposal, or 3 with supporter).");
        /// <summary>"Submitted today: {0}/{1} · resets at 00:00 UTC"</summary>
        public static LocalisableString Quota(int count, int limit) => text("quota", "Submitted today: {0}/{1} · resets at 00:00 UTC", count, limit);
        /// <summary>"Daily challenge proposals: {0}"</summary>
        public static LocalisableString ProfileProposals(int count) => text("profile_proposals", "Daily challenge proposals: {0}", count);
        /// <summary>"Became daily maps: {0}"</summary>
        public static LocalisableString ProfileMaps(int count) => text("profile_maps", "Became daily maps: {0}", count);
        /// <summary>"This beatmap is unavailable on the server. Select another beatmap."</summary>
        public static LocalisableString Unavailable => text("unavailable", "This beatmap is unavailable on the server. Select another beatmap.");
        /// <summary>"Request failed. Please try again."</summary>
        public static LocalisableString Failed => text("failed", "Request failed. Please try again.");
        /// <summary>"This proposal has changed. Refresh the list before trying again."</summary>
        public static LocalisableString Conflict => text("conflict", "This proposal has changed. Refresh the list before trying again.");
        /// <summary>"No beatmaps here."</summary>
        public static LocalisableString Empty => text("empty", "No beatmaps here.");
        /// <summary>"First in queue plays tomorrow. Remaining beatmaps follow on subsequent days."</summary>
        public static LocalisableString QueueHint => text("queue_hint", "First in queue plays tomorrow. Remaining beatmaps follow on subsequent days.");
        /// <summary>"Select one difficulty. Required mods and all their settings will be saved."</summary>
        public static LocalisableString SelectionHint => text("selection_hint", "Select one difficulty. Required mods and all their settings will be saved.");
        /// <summary>"Page {0} · {1} proposals"</summary>
        public static LocalisableString Page(int page, int total) => text("page", "Page {0} · {1} proposals", page, total);
        /// <summary>"Download this beatmap using the download button before editing its settings."</summary>
        public static LocalisableString DownloadToEdit => text("download_to_edit", "Download this beatmap using the download button before editing its settings.");
        /// <summary>"Select a proposal to review its settings."</summary>
        public static LocalisableString Choose => text("choose", "Select a proposal to review its settings.");
    }
}
