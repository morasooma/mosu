using System;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests
{
    public sealed class DiscoveryTasteArtist
    {
        [JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonProperty("weight")] public double Weight { get; set; }
        [JsonProperty("plays")] public long Plays { get; set; }
        [JsonProperty("songs")] public int Songs { get; set; }
    }
    public sealed class DiscoveryTasteSignal
    {
        [JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonProperty("weight")] public double Weight { get; set; }
    }
    public sealed class DiscoveryTasteCandidate
    {
        [JsonProperty("set_id")] public int SetId { get; set; }
        [JsonProperty("weight")] public double Weight { get; set; }
        [JsonProperty("reason")] public string Reason { get; set; } = string.Empty;
    }
    public sealed class DiscoveryTasteProfile
    {
        [JsonProperty("artists")] public DiscoveryTasteArtist[] Artists { get; set; } = Array.Empty<DiscoveryTasteArtist>();
        [JsonProperty("related_artists")] public DiscoveryTasteSignal[] RelatedArtists { get; set; } = Array.Empty<DiscoveryTasteSignal>();
        [JsonProperty("genres")] public DiscoveryTasteSignal[] Genres { get; set; } = Array.Empty<DiscoveryTasteSignal>();
        [JsonProperty("tags")] public DiscoveryTasteSignal[] Tags { get; set; } = Array.Empty<DiscoveryTasteSignal>();
        [JsonProperty("candidates")] public DiscoveryTasteCandidate[] Candidates { get; set; } = Array.Empty<DiscoveryTasteCandidate>();
        [JsonProperty("played_sets")] public int[] PlayedSets { get; set; } = Array.Empty<int>();
    }
    public sealed class DiscoveryTasteRequest : APIRequest<DiscoveryTasteProfile>
    {
        protected override string Target => "discovery/taste";
    }
}
