using System;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.Rooms
{
    public class DailyChallengeProposalOverview
    {
        [JsonProperty("can_submit")] public bool CanSubmit;
        [JsonProperty("can_manage")] public bool CanManage;
        [JsonProperty("submitted_today")] public int SubmittedToday;
        [JsonProperty("daily_limit")] public int DailyLimit = 1;
        [JsonProperty("resets_at")] public DateTimeOffset ResetsAt;
    }

    public class DailyChallengeProposal
    {
        [JsonProperty("id")] public long ID;
        [JsonProperty("owner_id")] public int OwnerID;
        [JsonProperty("beatmap_id")] public int BeatmapID;
        [JsonProperty("ruleset_id")] public int RulesetID;
        [JsonProperty("required_mods")] public APIMod[] RequiredMods = Array.Empty<APIMod>();
        [JsonProperty("allowed_mods")] public APIMod[] AllowedMods = Array.Empty<APIMod>();
        [JsonProperty("freestyle")] public bool Freestyle;
        [JsonProperty("status")] public string Status = string.Empty;
        [JsonProperty("revision")] public int Revision;

        public PlaylistItem ToPlaylistItem() => new PlaylistItem(new APIBeatmap { OnlineID = BeatmapID })
        {
            ID = ID, OwnerID = OwnerID, RulesetID = RulesetID,
            RequiredMods = RequiredMods, AllowedMods = AllowedMods, Freestyle = Freestyle
        };
    }

    public class DailyChallengeProposalPage
    {
        [JsonProperty("items")] public DailyChallengeProposal[] Items = Array.Empty<DailyChallengeProposal>();
        [JsonProperty("total")] public int Total;
    }

    public class DailyChallengeProposalMutation
    {
        [JsonProperty("id")] public long ID;
    }

    public class GetDailyChallengeProposalStatusRequest : APIRequest<DailyChallengeProposalOverview>
    {
        protected override string Target => "daily-challenge/proposals/status";
    }

    public class GetDailyChallengeProposalsRequest : APIRequest<DailyChallengeProposalPage>
    {
        private readonly string status;
        private readonly int offset;
        public GetDailyChallengeProposalsRequest(string status, int offset) { this.status = status; this.offset = offset; }
        protected override string Target => $"daily-challenge/proposals?status={status}&offset={offset}&limit=50";
    }

    public class MutateDailyChallengeProposalRequest : APIRequest<DailyChallengeProposalMutation>
    {
        private readonly long? id;
        private readonly object payload;

        public MutateDailyChallengeProposalRequest(PlaylistItem item, string requestKey, bool directlyApprove)
        {
            payload = new { request_key = requestKey, selection = selection(item), directly_approve = directlyApprove };
        }

        public MutateDailyChallengeProposalRequest(DailyChallengeProposal proposal, string action, PlaylistItem? item = null)
        {
            id = proposal.ID;
            payload = new { revision = proposal.Revision, action, selection = item == null ? null : selection(item) };
        }

        private static object selection(PlaylistItem item) => new
        {
            beatmap_id = item.Beatmap.OnlineID, ruleset_id = item.RulesetID,
            required_mods = item.RequiredMods, allowed_mods = item.AllowedMods, freestyle = item.Freestyle
        };

        protected override string Target => id == null ? "daily-challenge/proposals" : $"daily-challenge/proposals/{id}";
        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Post;
            request.ContentType = "application/json";
            request.AddRaw(JsonConvert.SerializeObject(payload));
            return request;
        }
    }
}
