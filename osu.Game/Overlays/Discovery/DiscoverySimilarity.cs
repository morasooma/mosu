using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Discovery
{
    public record DiscoveryNeighbour(int MapId, int SetId, double Similarity, int SeedId);

    /// <summary>Anonymous public map IDs only. Never sends accounts, scores, tokens or local files.</summary>
    internal sealed class DiscoverySimilarity
    {
        private static readonly HttpClient sharedClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        private readonly HttpClient client;
        internal DiscoverySimilarity(HttpClient? client = null) => this.client = client ?? sharedClient;

        internal async Task<IReadOnlyList<DiscoveryNeighbour>> SearchAsync(int seedId, CancellationToken token)
        {
            if (seedId <= 0 || seedId >= 2_000_000_000) return Array.Empty<DiscoveryNeighbour>();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(8));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.ayreth.site/api/v1/similar?beatmap_id={seedId}&k=24&star_range=1&allow_unranked=true");
            request.Headers.UserAgent.ParseAdd("Morasooma/1.0 (+https://github.com/GooGuTeam/osu)");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) return Array.Empty<DiscoveryNeighbour>();
            response.EnsureSuccessStatusCode();
            const int limit = 256 * 1024;
            if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Similarity response exceeds limit.");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) > 0)
            {
                if (bytes.Length + read > limit) throw new InvalidDataException("Similarity response exceeds limit.");
                bytes.Write(buffer, 0, read);
            }
            var json = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes.ToArray()));
            return (json["matches"] as JArray ?? new JArray()).Take(24).Select(m => new DiscoveryNeighbour(
                (int?)m["beatmap_id"] ?? 0, (int?)m["beatmapset_id"] ?? 0, (double?)m["similarity"] ?? 0, seedId))
                .Where(m => m.MapId > 0 && m.MapId < 2_000_000_000 && m.SetId > 0 && m.SetId < 2_000_000_000
                            && double.IsFinite(m.Similarity) && m.Similarity is >= 0 and <= 1).ToArray();
        }

        // Round-robin distinct seeds preserves different playstyles instead of averaging them into one centroid.
        internal static APIBeatmap[] Seeds(IEnumerable<APIBeatmap> top, IEnumerable<APIBeatmapSet> likes) =>
            likes.Reverse().SelectMany(s => s.Beatmaps.Take(1)).Take(4).Concat(top)
                .Where(m => m.RulesetID == 0 && m.OnlineID > 0 && m.OnlineID < 2_000_000_000)
                .DistinctBy(m => m.OnlineID).Take(24).ToArray();

        internal static IReadOnlyList<DiscoveryNeighbour> Merge(IEnumerable<DiscoveryNeighbour> neighbours) => neighbours
            .GroupBy(n => n.MapId).Select(g => g.OrderByDescending(n => n.Similarity).First() with
            {
                Similarity = g.Max(n => n.Similarity) + Math.Min(0.15, (g.Select(n => n.SeedId).Distinct().Count() - 1) * 0.03),
            }).OrderByDescending(n => n.Similarity).ToArray();
    }
}
