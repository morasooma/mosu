// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests
{
    public class GetServerBeatmapSetUploadStateRequest : APIRequest<ServerBeatmapSetUploadState>
    {
        private readonly int beatmapSetId;

        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/beatmapsets/{beatmapSetId}/upload-state";

        protected override string Target => throw new NotSupportedException();

        public GetServerBeatmapSetUploadStateRequest(int beatmapSetId)
        {
            this.beatmapSetId = beatmapSetId;
        }
    }

    public class ServerBeatmapSetUploadState
    {
        [JsonProperty(@"beatmapset_id")]
        public int BeatmapSetID { get; set; }

        [JsonProperty(@"revision")]
        public int Revision { get; set; }

        [JsonProperty(@"status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty(@"updates_remaining")]
        public int UpdatesRemaining { get; set; }

        [JsonProperty(@"rate_limit_reset_at")]
        public DateTimeOffset? RateLimitResetAt { get; set; }

        [JsonProperty(@"source_beatmapset_id")]
        public int? SourceBeatmapSetID { get; set; }

        [JsonProperty(@"source_beatmap_id")]
        public int? SourceBeatmapID { get; set; }
    }
}
