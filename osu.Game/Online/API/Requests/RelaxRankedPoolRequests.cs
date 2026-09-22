// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    public class GetRelaxRankedPoolRequest : APIRequest<RelaxRankedPoolResponse>
    {
        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/v2/matchmaking/relax-pool";
        protected override string Target => throw new NotSupportedException();
    }

    public class ReplaceRelaxRankedPoolRequest : APIRequest<ReplaceRelaxRankedPoolResponse>
    {
        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/v2/matchmaking/relax-pool";
        protected override string Target => throw new NotSupportedException();

        [JsonProperty("beatmap_ids")]
        public int[] BeatmapIds { get; init; } = [];

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Put;
            request.ContentType = "application/json";
            request.AddRaw(JsonConvert.SerializeObject(this));
            return request;
        }
    }

    public class RelaxRankedPoolResponse
    {
        [JsonProperty("can_manage")]
        public bool CanManage { get; set; }

        [JsonProperty("owner")]
        public RelaxRankedPoolOwner? Owner { get; set; }

        [JsonProperty("selected_owner")]
        public RelaxRankedPoolOwner? SelectedOwner { get; set; }

        [JsonProperty("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("beatmaps")]
        public List<RelaxRankedPoolBeatmap> Beatmaps { get; set; } = [];
    }

    public class RelaxRankedPoolOwner
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;
    }

    public class RelaxRankedPoolBeatmap
    {
        [JsonProperty("beatmap_id")]
        public int BeatmapId { get; set; }

        [JsonProperty("beatmapset_id")]
        public int BeatmapsetId { get; set; }

        [JsonProperty("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("star_rating")]
        public double StarRating { get; set; }

        [JsonProperty("total_length")]
        public int TotalLength { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class ReplaceRelaxRankedPoolResponse
    {
        [JsonProperty("accepted")]
        public int Accepted { get; set; }

        [JsonProperty("rejected")]
        public int Rejected { get; set; }

        [JsonProperty("rejected_beatmap_ids")]
        public int[] RejectedBeatmapIds { get; set; } = [];
    }
}
