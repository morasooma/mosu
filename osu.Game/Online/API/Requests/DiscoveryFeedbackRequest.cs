using System;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    public sealed class DiscoveryFeedbackEvent
    {
        [JsonProperty("event_id")] public Guid EventId { get; set; } = Guid.NewGuid();
        [JsonProperty("impression_id")] public Guid ImpressionId { get; set; }
        [JsonProperty("beatmap_id")] public int BeatmapId { get; set; }
        [JsonProperty("beatmapset_id")] public int BeatmapSetId { get; set; }
        [JsonProperty("ruleset")] public int Ruleset { get; set; }
        [JsonProperty("action")] public string Action { get; set; } = string.Empty;
        [JsonProperty("work_mode")] public string WorkMode { get; set; } = string.Empty;
        [JsonProperty("source")] public string Source { get; set; } = string.Empty;
        [JsonProperty("algorithm_version")] public string AlgorithmVersion { get; set; } = "hybrid-v1";
        [JsonProperty("seed_ids")] public int[] SeedIds { get; set; } = Array.Empty<int>();
        [JsonProperty("elapsed_ms")] public long ElapsedMs { get; set; }
        [JsonProperty("occurred_at")] public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class DiscoveryFeedbackRequest : APIRequest
    {
        public DiscoveryFeedbackEvent Event { get; }
        private readonly int ownerId;
        private readonly string endpoint;
        public DiscoveryFeedbackRequest(DiscoveryFeedbackEvent feedback, int ownerId, string endpoint)
        {
            Event = feedback;
            this.ownerId = ownerId;
            this.endpoint = endpoint;
        }
        public override void Perform()
        {
            // The API queue may outlive an account/server switch. Never attribute an old action to a new user.
            if (API?.LocalUser.Value.Id != ownerId || API.Endpoints.APIUrl != endpoint) { Cancel(); return; }
            base.Perform();
        }
        protected override string Target => "discovery/feedback";
        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Post;
            request.ContentType = "application/json";
            request.AddRaw(JsonConvert.SerializeObject(Event));
            return request;
        }
    }
}
