using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Beatmaps;

namespace osu.Game.Online.Hinamizawa
{
    public sealed record HinamizawaSearch(string Query = "", int Mode = -1, string Status = "ranked", string Sort = "ranked_desc",
                                         string Creator = "", string Genre = "", string Language = "", string Explicit = "hide", string Extra = "",
                                         double? MinStars = null, double? MaxStars = null, double? MinBpm = null, double? MaxBpm = null,
                                         double? MinLength = null, double? MaxLength = null, double? MinPP = null, double? MaxPP = null);

    public sealed record HinamizawaPage(APIBeatmapSet[] Sets, bool HasMore, int? TotalCount = null, string Source = "unknown", int? NextOffset = null, string? PaginationWarning = null);

    public enum HinamizawaArchiveProvider
    {
        Hinamizawa,
        NeriNyan,
    }

    public enum BeatmapCatalogueProvider
    {
        Sayobot,
        Hinamizawa,
    }

    public sealed record HinamizawaDownloadProgress(long BytesReceived, long? TotalBytes, double BytesPerSecond)
    {
        public double? Fraction => TotalBytes > 0 ? Math.Clamp((double)BytesReceived / TotalBytes.Value, 0, 1) : null;
        public double? SecondsRemaining => TotalBytes > 0 && BytesPerSecond > 0 ? Math.Max(0, TotalBytes.Value - BytesReceived) / BytesPerSecond : null;
    }

    /// <summary>Anonymous mirror traffic. Never attaches Mosu/official credentials or changes their API routing.</summary>
    public sealed class HinamizawaClient : IDisposable
    {
        public const string HOST = "https://mirror.hinamizawa.ai";
        public const int PAGE_SIZE = 50;
        private const long max_archive_bytes = 512L * 1024 * 1024;
        private const long max_extracted_bytes = 1024L * 1024 * 1024;
        private readonly HttpClient http;
        private readonly SemaphoreSlim archiveLock = new SemaphoreSlim(1, 1);
        private readonly string cacheRoot;

        public HinamizawaClient(string cacheRoot, HttpMessageHandler? handler = null)
        {
            this.cacheRoot = cacheRoot;
            http = handler == null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = TimeSpan.FromMinutes(3);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Morasooma/1.0 (+https://github.com/GooGuTeam/osu)");
        }

        public static string BuildSearchUrl(HinamizawaSearch search, int page)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(page);
            var values = new Dictionary<string, string>
            {
                ["query"] = search.Query, ["limit"] = PAGE_SIZE.ToString(CultureInfo.InvariantCulture),
                ["page"] = page.ToString(CultureInfo.InvariantCulture), ["sort"] = search.Sort,
                ["explicit"] = search.Explicit,
            };
            if (search.Mode >= 0) values["mode"] = search.Mode.ToString(CultureInfo.InvariantCulture);
            add("status", search.Status);
            add("creator", search.Creator);
            add("genre", search.Genre);
            add("language", search.Language);
            add("extra", search.Extra);
            number("min_stars", search.MinStars); number("max_stars", search.MaxStars);
            number("min_bpm", search.MinBpm); number("max_bpm", search.MaxBpm);
            number("min_length", search.MinLength); number("max_length", search.MaxLength);
            number("min_pp", search.MinPP); number("max_pp", search.MaxPP);
            return HOST + "/v3/osu/beatmaps/search/v2?" + string.Join("&", values.Select(v => Uri.EscapeDataString(v.Key) + "=" + Uri.EscapeDataString(v.Value)));

            void add(string key, string value) { if (!string.IsNullOrWhiteSpace(value)) values[key] = value; }
            void number(string key, double? value)
            {
                if (!value.HasValue) return;
                if (!double.IsFinite(value.Value) || value < 0) throw new ArgumentOutOfRangeException(key);
                values[key] = value.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static string BuildSayobotSearchBody(string query, int mode, int page, int pageSize = 25)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(page);
            if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
            bool hasQuery = !string.IsNullOrWhiteSpace(query);
            var body = new JObject { ["cmd"] = "beatmaplist", ["limit"] = pageSize, ["offset"] = page, ["type"] = hasQuery ? "search" : "new" };
            if (hasQuery)
            {
                body["keyword"] = query;
                body["class"] = 31;
                if (mode is >= 0 and <= 3) body["mode"] = 1 << mode;
            }
            return body.ToString(Newtonsoft.Json.Formatting.None);
        }

        public static BeatmapOnlineStatus SayobotStatus(int approved) => approved switch
        {
            -2 => BeatmapOnlineStatus.Graveyard,
            -1 => BeatmapOnlineStatus.WIP,
            0 => BeatmapOnlineStatus.Pending,
            1 => BeatmapOnlineStatus.Ranked,
            2 => BeatmapOnlineStatus.Approved,
            3 => BeatmapOnlineStatus.Qualified,
            4 => BeatmapOnlineStatus.Loved,
            _ => BeatmapOnlineStatus.None,
        };

        public async Task<HinamizawaPage> SearchSayobotAsync(string query, int mode, int page, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sayobot.cn/?post")
            {
                Content = new StringContent(BuildSayobotSearchBody(query, mode, page), Encoding.UTF8, "application/json"),
            };
            using var response = await http.SendAsync(request, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = JObject.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            if ((int?)json["status"] is int status && status != 0) throw new HttpRequestException($"Sayobot search failed with status {status}.");
            var rows = json["data"] as JArray ?? new JArray();
            var sets = new List<APIBeatmapSet>();
            foreach (JObject row in rows)
            {
                token.ThrowIfCancellationRequested();
                int setId = (int?)row["sid"] ?? 0;
                if (setId <= 0) continue;
                using var detailResponse = await http.GetAsync($"https://api.sayobot.cn/v2/beatmapinfo?0={setId}", token).ConfigureAwait(false);
                detailResponse.EnsureSuccessStatusCode();
                var detailJson = JObject.Parse(await detailResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false));
                if ((int?)detailJson["status"] is int detailStatus && detailStatus != 0) continue;
                var detail = detailJson["data"] as JObject;
                if (detail == null) continue;
                var set = mapSayobotDetail(detail);
                if (set != null) sets.Add(set);
            }
            int? total = (int?)json["results"];
            int? nextOffset = (int?)json["endid"];
            bool validCursor = nextOffset is > 0 && nextOffset.Value > page;
            bool more = rows.Count > 0 && validCursor;
            string? warning = rows.Count > 0 && !validCursor
                ? "Sayobot returned no advancing endid cursor; further results are unavailable."
                : null;
            return new HinamizawaPage(sets.ToArray(), more, total, "Sayobot", nextOffset, warning);
        }

        private static APIBeatmapSet? mapSayobotDetail(JObject detail)
        {
            int setId = (int?)detail["sid"] ?? 0;
            if (setId <= 0) return null;
            int approved = (int?)detail["approved"] ?? -3;
            var set = new APIBeatmapSet
            {
                OnlineID = setId, Status = SayobotStatus(approved), Title = (string?)detail["title"] ?? "",
                TitleUnicode = (string?)detail["titleU"] ?? "", Artist = (string?)detail["artist"] ?? "",
                ArtistUnicode = (string?)detail["artistU"] ?? "", AuthorID = (int?)detail["creator_id"] ?? 0,
                AuthorString = (string?)detail["creator"] ?? "", Source = (string?)detail["source"] ?? "",
                Tags = (string?)detail["tags"] ?? "", BPM = (double?)detail["bpm"] ?? 0,
                LastUpdated = unixTime((long?)detail["last_update"]),
                HasVideo = (int?)detail["video"] == 1, HasStoryboard = (int?)detail["storyboard"] == 1,
                FavouriteCount = (int?)detail["favourite_count"] ?? 0,
                Submitted = unixTime((long?)detail["last_update"]) ?? DateTimeOffset.MinValue, Ranked = approved is >= 1 and <= 3 ? unixTime((long?)detail["approved_date"]) : null,
            };
            foreach (JObject map in detail["bid_data"] as JArray ?? new JArray())
            {
                int mode = (int?)map["mode"] ?? -1;
                string? length = (string?)map["length"] ?? map["length"]?.ToString();
                double seconds = double.TryParse(length, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0;
                var beatmap = new APIBeatmap
                {
                    OnlineID = (int?)map["bid"] ?? 0, OnlineBeatmapSetID = setId, RulesetID = mode,
                    Status = set.Status, Checksum = string.Empty, DifficultyName = (string?)map["version"] ?? "",
                    ApproachRate = (float?)map["AR"] ?? 0, OverallDifficulty = (float?)map["OD"] ?? 0,
                    CircleSize = (float?)map["CS"] ?? 0, DrainRate = (float?)map["HP"] ?? 0,
                    StarRating = (double?)map["star"] ?? 0, Length = seconds * 1000,
                    MaxCombo = (int?)map["maxcombo"], BPM = set.BPM, LastUpdated = set.LastUpdated ?? DateTimeOffset.MinValue,
                };
                if (beatmap.OnlineID > 0 && mode is >= 0 and <= 3) { beatmap.BeatmapSet = set; set.Beatmaps = set.Beatmaps.Append(beatmap).ToArray(); }
            }
            return set;
        }

        private static DateTimeOffset? unixTime(long? seconds)
        {
            if (seconds is not > 0) return null;
            try { return DateTimeOffset.FromUnixTimeSeconds(seconds.Value); }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        public async Task<HinamizawaPage> SearchAsync(HinamizawaSearch search, int page, CancellationToken token)
        {
            using var response = await http.GetAsync(BuildSearchUrl(search, page), token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = JObject.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            if (json["error"] != null && json["beatmapsets"] == null)
                throw new HttpRequestException("Hinamizawa search sources are unavailable: " + (string?)json["error"]);
            // An empty user object on local-index results would overwrite creator/user_id in APIBeatmapSet's setter.
            foreach (var set in json["beatmapsets"] ?? new JArray())
            {
                if (set["user"] is JObject user && user["id"] == null)
                    ((JObject)set).Remove("user");
            }
            var sets = json["beatmapsets"]?.ToObject<APIBeatmapSet[]>() ?? Array.Empty<APIBeatmapSet>();
            foreach (var set in sets)
            {
                set.Preview = $"{HOST}/v3/osu/music/preview/{set.OnlineID}";
                foreach (var beatmap in set.Beatmaps) beatmap.BeatmapSet = set;
            }
            // Upstream facet post-filtering can produce short pages: only an EMPTY page is terminal there.
            bool local = (string?)json["source"] == "local";
            bool more = local && json["total_pages"] != null
                ? page + 1 < (int)json["total_pages"]!
                : sets.Length > 0;
            int? totalCount = local && json["total_count"] != null ? (int)json["total_count"]! : null;
            return new HinamizawaPage(sets, more, totalCount, (string?)json["source"] ?? "unknown");
        }

        public static string BuildArchiveUrl(int setId, HinamizawaArchiveProvider provider)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setId);
            return provider switch
            {
                HinamizawaArchiveProvider.Hinamizawa => $"{HOST}/api/v1/hinai/d/{setId}?noVideo=1",
                HinamizawaArchiveProvider.NeriNyan => $"https://api.nerinyan.moe/d/{setId}?noVideo=true",
                _ => throw new ArgumentOutOfRangeException(nameof(provider)),
            };
        }

        public async Task<string> GetArchiveAsync(int setId, IProgress<float>? progress, CancellationToken token, string revision = "", IProgress<HinamizawaDownloadProgress>? downloadProgress = null, HinamizawaArchiveProvider provider = HinamizawaArchiveProvider.Hinamizawa)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setId);
            await archiveLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(cacheRoot);
                string revisionKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(revision)))[..16];
                string providerKey = provider == HinamizawaArchiveProvider.NeriNyan ? "nerinyan" : "hinamizawa";
                string path = Path.Combine(cacheRoot, providerKey + "-" + setId.ToString(CultureInfo.InvariantCulture) + "-" + revisionKey + ".osz");
                if (File.Exists(path))
                {
                    File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
                    long cachedSize = new FileInfo(path).Length;
                    downloadProgress?.Report(new HinamizawaDownloadProgress(cachedSize, cachedSize, 0));
                    progress?.Report(1);
                    return path;
                }
                string temporary = path + ".partial";
                try
                {
                    using var response = await http.GetAsync(BuildArchiveUrl(setId, provider), HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    long? length = response.Content.Headers.ContentLength;
                    if (length > max_archive_bytes) throw new InvalidDataException("The map archive exceeds 512 MiB.");
                    await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                    await using (var output = File.Create(temporary))
                    {
                        byte[] buffer = new byte[81920];
                        long total = 0;
                        var clock = Stopwatch.StartNew();
                        double lastReport = 0;
                        downloadProgress?.Report(new HinamizawaDownloadProgress(0, length, 0));
                        int count;
                        while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                        {
                            total += count;
                            if (total > max_archive_bytes) throw new InvalidDataException("The map archive exceeds 512 MiB.");
                            await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                            if (length > 0) progress?.Report((float)total / length.Value);
                            if (clock.Elapsed.TotalSeconds - lastReport >= 0.15)
                            {
                                lastReport = clock.Elapsed.TotalSeconds;
                                downloadProgress?.Report(new HinamizawaDownloadProgress(total, length, total / Math.Max(0.001, lastReport)));
                            }
                        }
                        downloadProgress?.Report(new HinamizawaDownloadProgress(total, length, total / Math.Max(0.001, clock.Elapsed.TotalSeconds)));
                    }
                    using (var archive = ZipFile.OpenRead(temporary))
                    {
                        if (!archive.Entries.Any(e => e.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)))
                            throw new InvalidDataException("The mirror returned no beatmaps in the archive.");
                        validateEntries(archive, Path.Combine(cacheRoot, setId.ToString(CultureInfo.InvariantCulture)));
                    }
                    File.Move(temporary, path, true);
                    return path;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            finally { archiveLock.Release(); }
        }

        public static string ExtractArchive(string archivePath, CancellationToken token)
        {
            string destination = Path.ChangeExtension(archivePath, null)!;
            if (File.Exists(Path.Combine(destination, ".complete"))) return destination;
            string staging = destination + ".extracting";
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            try
            {
                using var zip = ZipFile.OpenRead(archivePath);
                validateEntries(zip, staging);
                Directory.CreateDirectory(staging);
                foreach (var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    string target = ResolveArchivePath(staging, entry.FullName);
                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, true);
                }
                File.WriteAllText(Path.Combine(staging, ".complete"), "1");
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                Directory.Move(staging, destination);
                return destination;
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }

        public static string ResolveArchivePath(string root, string name)
        {
            string normalized = name.Replace('\\', '/');
            if (normalized.StartsWith('/') || normalized.Contains(':')) throw new InvalidDataException("Unsafe archive path.");
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(root, normalized));
            if (!result.StartsWith(fullRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new InvalidDataException("Archive path escapes the cache.");
            return result;
        }

        private static void validateEntries(ZipArchive zip, string root)
        {
            if (zip.Entries.Count > 20000) throw new InvalidDataException("Too many archive entries.");
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                ResolveArchivePath(root, entry.FullName);
                // ZIP Unix symlinks must not be materialised in the cache.
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Archive symlinks are not allowed.");
                total = checked(total + entry.Length);
                if (total > max_extracted_bytes) throw new InvalidDataException("Extracted map exceeds 1 GiB.");
            }
        }

        public void TrimCache()
        {
            if (!Directory.Exists(cacheRoot)) return;
            // Session owns all working beatmaps; trimming occurs only between gameplay sessions.
            var archives = new DirectoryInfo(cacheRoot).GetFiles("*.osz").OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
            long total = 0;
            foreach (var archive in archives)
            {
                string extracted = Path.ChangeExtension(archive.FullName, null)!;
                total += archive.Length;
                if (Directory.Exists(extracted)) total += Directory.EnumerateFiles(extracted, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
                if (total <= 2L * 1024 * 1024 * 1024) continue;
                if (Directory.Exists(extracted)) Directory.Delete(extracted, true);
                archive.Delete();
            }
        }

        // Do not dispose the semaphore while a cancelled request is still unwinding its finally block.
        public void Dispose() => http.Dispose();
    }
}
