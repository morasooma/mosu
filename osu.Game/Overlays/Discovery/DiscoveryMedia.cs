using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Online.API.Requests.Responses;
using osu.Framework.Logging;

namespace osu.Game.Overlays.Discovery
{
    /// <summary>Bounded, anonymous, active-card transfers only. Never fetches an archive.</summary>
    internal sealed class DiscoveryMedia : IDisposable
    {
        private static readonly HttpClient sharedClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        private readonly HttpClient client;

        internal DiscoveryMedia(HttpClient? client = null) => this.client = client ?? sharedClient;
        private readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();
        private readonly Queue<string> order = new Queue<string>();
        private readonly object cacheLock = new object();
        private int cachedBytes;
        private bool disposed;

        internal async Task<(Beatmap Map, byte[] Audio)> LoadAsync(APIBeatmap selected, CancellationToken token)
        {
            if (selected.OnlineID <= 0 || selected.OnlineID >= 2_000_000_000 || selected.OnlineBeatmapSetID <= 0 || selected.OnlineBeatmapSetID >= 2_000_000_000)
                throw new InvalidDataException("No public preview source for this map.");
            var mapTask = loadMap(selected, token);
            var audioTask = audioBytes(selected.OnlineBeatmapSetID, token);
            await Task.WhenAll(mapTask, audioTask).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return (await mapTask.ConfigureAwait(false), await audioTask.ConfigureAwait(false));
        }

        internal Task<Beatmap> LoadMapAsync(APIBeatmap selected, CancellationToken token) => loadMap(selected, token);

        private async Task<Beatmap> loadMap(APIBeatmap selected, CancellationToken token)
        {
            try
            {
                // Validate each candidate before accepting it. A stale revision from one mirror
                // must not prevent trying another, and a different difficulty is never accepted.
                Exception? last = null;
                foreach (string url in RawSources(selected.OnlineID))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var bytes = await get(url, 2 * 1024 * 1024, token).ConfigureAwait(false);
                        return await Task.Run(() => Decode(bytes, selected), token).ConfigureAwait(false);
                    }
                    catch (Exception e) when (!token.IsCancellationRequested && e is HttpRequestException or InvalidDataException or TaskCanceledException)
                    {
                        Logger.Log($"Discovery raw source={url}: {e.Message}", LoggingTarget.Network);
                        last = e;
                    }
                }
                throw last ?? new InvalidDataException("No raw preview source available.");
            }
            catch (Exception e) when (!token.IsCancellationRequested && e is HttpRequestException or InvalidDataException or TaskCanceledException)
            {
                // A missing/revised raw file must not prevent listening to an available song clip.
                // No synthetic objects or guessed timestamp: full gameplay remains an explicit download.
                Logger.Log($"Discovery raw map={selected.OnlineID} set={selected.OnlineBeatmapSetID}: {e.Message}", LoggingTarget.Network);
                var map = new Beatmap();
                map.BeatmapInfo.OnlineID = selected.OnlineID;
                map.BeatmapInfo.BeatmapSet = new BeatmapSetInfo { OnlineID = selected.OnlineBeatmapSetID };
                map.Metadata.PreviewTime = -1;
                return map;
            }
        }

        internal static string[] RawSources(int mapId) => new[]
        {
            $"https://osu.ppy.sh/osu/{mapId}",
            $"https://catboy.best/osu/{mapId}",
            $"https://osu.direct/api/osu/{mapId}/raw",
        };

        internal Task<byte[]> LoadAudioAsync(int setId, CancellationToken token)
        {
            if (setId <= 0 || setId >= 2_000_000_000) throw new InvalidDataException("No public audio source.");
            return audioBytes(setId, token);
        }

        private async Task<byte[]> audioBytes(int setId, CancellationToken token)
        {
            try { return await get($"https://b.ppy.sh/preview/{setId}.mp3", 2 * 1024 * 1024, token).ConfigureAwait(false); }
            catch (Exception e) when (!token.IsCancellationRequested && e is HttpRequestException or InvalidDataException or TaskCanceledException)
            {
                Logger.Log($"Discovery audio set={setId}: {e.Message}; trying mirror", LoggingTarget.Network);
                return await get($"https://osu.direct/api/media/preview/set/{setId}", 2 * 1024 * 1024, token).ConfigureAwait(false);
            }
        }

        private async Task<byte[]> get(string url, int limit, CancellationToken token)
        {
            try { return await getCore(url, limit, token).ConfigureAwait(false); }
            catch (OperationCanceledException e) when (!token.IsCancellationRequested)
            {
                throw new HttpRequestException($"Preview request timed out: {url}", e);
            }
        }

        private async Task<byte[]> getCore(string url, int limit, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (cacheLock)
                if (cache.TryGetValue(url, out var bytes)) return bytes;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(6));
            token = deadline.Token;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Morasooma/1.0 (+https://github.com/GooGuTeam/osu)");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} from {url}", null, response.StatusCode);
            if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Preview exceeds the transfer limit.");
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var output = new MemoryStream();
            byte[] buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                if (output.Length + read > limit) throw new InvalidDataException("Preview exceeds the transfer limit.");
                output.Write(buffer, 0, read);
            }
            byte[] result = output.ToArray();
            if (result.Length == 0) throw new InvalidDataException("Empty preview response.");
            // Official raw-file misses may be HTTP 200 with HTML/empty content.
            if (new Uri(url).AbsolutePath.StartsWith("/osu/", StringComparison.Ordinal)
                || new Uri(url).AbsolutePath.StartsWith("/api/osu/", StringComparison.Ordinal)) ValidateHeader(result);
            lock (cacheLock)
            {
                if (!disposed && !cache.ContainsKey(url))
                {
                    cache[url] = result;
                    order.Enqueue(url);
                    cachedBytes += result.Length;
                    while (order.Count > 12 || cachedBytes > 8 * 1024 * 1024)
                    {
                        string oldest = order.Dequeue();
                        cachedBytes -= cache[oldest].Length;
                        cache.Remove(oldest);
                    }
                }
            }
            return result;
        }

        internal static void ValidateHeader(byte[] bytes)
        {
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true);
            if (reader.ReadLine()?.Trim().StartsWith("osu file format v", StringComparison.Ordinal) != true)
                throw new InvalidDataException("The source did not return an osu beatmap.");
        }

        internal static Beatmap Decode(byte[] bytes, APIBeatmap selected)
        {
            ValidateHeader(bytes);
            string md5 = Convert.ToHexString(MD5.HashData(bytes));
            if (!string.IsNullOrEmpty(selected.Checksum) && !md5.Equals(selected.Checksum, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Preview revision differs from the catalogue.");
            using var stream = new MemoryStream(bytes);
            using var reader = new LineBufferedReader(stream);
            var map = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            if ((map.BeatmapInfo.OnlineID > 0 && map.BeatmapInfo.OnlineID != selected.OnlineID) || map.BeatmapInfo.Ruleset.OnlineID != selected.RulesetID
                || (map.BeatmapInfo.BeatmapSet?.OnlineID > 0 && map.BeatmapInfo.BeatmapSet.OnlineID != selected.OnlineBeatmapSetID))
                throw new InvalidDataException("Preview does not match the selected difficulty.");
            // Legacy public files predate embedded online IDs. Their HTTPS endpoint/checksum
            // identifies the map; hydrate missing IDs rather than rejecting all old maps.
            map.BeatmapInfo.OnlineID = selected.OnlineID;
            map.BeatmapInfo.BeatmapSet ??= new BeatmapSetInfo();
            map.BeatmapInfo.BeatmapSet.OnlineID = selected.OnlineBeatmapSetID;
            if (map.HitObjects.Count > 25000) throw new InvalidDataException("This map is too large for a lightweight preview.");
            map.BeatmapInfo.MD5Hash = md5;
            return map;
        }

        public void Dispose()
        {
            lock (cacheLock) { disposed = true; cache.Clear(); order.Clear(); cachedBytes = 0; }
        }
    }
}
