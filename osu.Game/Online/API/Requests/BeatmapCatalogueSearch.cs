using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Online.API.Requests
{
    internal sealed record CatalogueQuery(string Text, int Mode, SearchCategory Category, SortCriteria Sort, SortDirection Direction,
                                          SearchGenre Genre, SearchLanguage Language, SearchExplicit Explicit, SearchExtra[] Extra, SearchGeneral[] General,
                                          bool Personal, int Page = 0);

    internal sealed record CataloguePage(JArray Sets, BeatmapCatalogueSource Source, bool HasMore);

    /// <summary>Anonymous client-side search. No Mosu or official credentials, no implicit official fallback.</summary>
    internal sealed class BeatmapCatalogueSearch
    {
        internal static readonly BeatmapCatalogueSearch Shared = new BeatmapCatalogueSearch();
        private readonly HttpClient http;
        private readonly Func<DateTimeOffset> now;
        private readonly object sync = new object();
        private readonly Dictionary<string, CachedPage> cache = new Dictionary<string, CachedPage>();
        private readonly Dictionary<string, PendingPage> pending = new Dictionary<string, PendingPage>();
        private readonly Dictionary<BeatmapCatalogueSource, DateTimeOffset> cooldowns = new Dictionary<BeatmapCatalogueSource, DateTimeOffset>();

        internal BeatmapCatalogueSearch(HttpMessageHandler? handler = null, Func<DateTimeOffset>? clock = null)
        {
            http = handler == null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(8);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Morasooma/1.0 (+https://github.com/GooGuTeam/osu)");
            this.now = clock ?? (() => DateTimeOffset.UtcNow);
        }

        internal async Task<CataloguePage> SearchAsync(CatalogueQuery query, BeatmapCatalogueSource source, CancellationToken token)
        {
            if (source == BeatmapCatalogueSource.Official)
                throw new ArgumentException("Official search is not mirror traffic.", nameof(source));
            var candidates = source == BeatmapCatalogueSource.Auto
                ? new[] { BeatmapCatalogueSource.Nekoha, BeatmapCatalogueSource.Mirria, BeatmapCatalogueSource.OsuDirect, BeatmapCatalogueSource.Hinamizawa }
                : new[] { source };
            Exception? last = null;
            Exception? transferError = null;
            foreach (var candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                string url;
                try { url = BuildUrl(query, candidate); }
                catch (NotSupportedException e) { last = e; continue; }
                try
                {
                    return await getPage(url, candidate, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException or Newtonsoft.Json.JsonException)
                {
                    lock (sync)
                    {
                        if (!cooldowns.TryGetValue(candidate, out var until) || until <= now())
                            cooldowns[candidate] = now().AddSeconds(30);
                    }
                    Logger.Log($"Beatmap catalogue failure source={candidate} page={query.Page} url={url} error={e}", LoggingTarget.Network);
                    transferError = last = e;
                    // A cursor belongs to one source. Never append another catalogue's page N.
                    if (query.Page > 0)
                        throw new IOException("The catalogue is unavailable. Select another mirror to restart the search.", e);
                }
            }
            throw transferError ?? last ?? new NotSupportedException("No mirror supports these filters.");
        }

        internal static string BuildUrl(CatalogueQuery query, BeatmapCatalogueSource source)
        {
            if (query.Personal || query.Category is SearchCategory.Mine or SearchCategory.Favourites || query.General.Length != 0)
                throw new NotSupportedException("Personal and general filters require the official catalogue. Select osu! (official) or clear these filters.");
            if (query.Mode is < -1 or > 3)
                throw new NotSupportedException("Public mirrors support only osu!, taiko, catch and mania.");
            if (query.Genre != SearchGenre.Any || query.Language != SearchLanguage.Any)
                throw new NotSupportedException("These mirrors do not support genre/language filters. Select osu! (official) or clear the filters.");
            string status = query.Category switch
            {
                SearchCategory.Any => "ranked,approved,loved,qualified,pending,wip,graveyard",
                SearchCategory.Leaderboard => "ranked,approved,loved,qualified",
                SearchCategory.Ranked => "ranked,approved",
                SearchCategory.Pending => "pending,wip",
                _ => query.Category.ToString().ToLowerInvariant(),
            };
            var filters = new BeatmapCatalogueFilters(query.Text);
            var values = new Dictionary<string, string>();
            string host;
            if (source == BeatmapCatalogueSource.Nekoha)
            {
                if (query.Extra.Any(e => e != SearchExtra.Video))
                    throw new NotSupportedException("Nekoha does not support the storyboard filter. Clear it or select osu! (official).");
                string sort = query.Sort switch
                {
                    SortCriteria.Title => "title", SortCriteria.Artist => "artist", SortCriteria.Difficulty => "difficulty",
                    SortCriteria.Updated => "updated", SortCriteria.Ranked => "ranked", SortCriteria.Favourites => "favourites",
                    _ => throw new NotSupportedException("Nekoha does not support this sorting. Choose title, artist, difficulty, updated, ranked or favourites."),
                };
                host = "https://mirror.nekoha.moe/api/search";
                values["q"] = filters.Text;
                filters.AddNekohaBounds(values);
                values["page"] = (query.Page + 1).ToString(CultureInfo.InvariantCulture);
                values["sort"] = sort;
                values["order"] = query.Direction == SortDirection.Ascending ? "asc" : "desc";
                values["status"] = status;
                if (query.Mode >= 0) values["mode"] = new[] { "osu", "taiko", "fruits", "mania" }[query.Mode];
                if (query.Extra.Contains(SearchExtra.Video)) values["video"] = "true";
            }
            else if (source == BeatmapCatalogueSource.Mirria)
            {
                if (query.Extra.Length > 0)
                    throw new NotSupportedException("Mirria does not support video/storyboard filters. Clear them or choose another catalogue.");
                if (query.Sort != SortCriteria.Updated && !(query.Sort == SortCriteria.Plays && query.Direction == SortDirection.Ascending))
                    throw new NotSupportedException("Mirria supports sorting by update date or ascending plays. Select one of these or choose another catalogue.");
                host = "https://mirror.lisek.cc/api/v1/search";
                values["query"] = filters.Text;
                values["limit"] = "50";
                values["offset"] = (query.Page * 50).ToString(CultureInfo.InvariantCulture);
                values["sort"] = query.Sort == SortCriteria.Plays ? "playcount" : query.Direction == SortDirection.Ascending ? "updated_asc" : "updated_desc";
                string[] statuses = status.Split(',');
                for (int i = 0; i < statuses.Length; i++) values[$"statuses[{i}]"] = statuses[i];
                if (query.Mode >= 0) values["modes[0]"] = new[] { "osu", "taiko", "fruits", "mania" }[query.Mode];
            }
            else if (source == BeatmapCatalogueSource.OsuDirect)
            {
                if (query.Extra.Length > 0)
                    throw new NotSupportedException("osu.direct does not support these extra-content filters. Clear them or choose another catalogue.");
                string field = query.Sort switch
                {
                    SortCriteria.Title => "title", SortCriteria.Artist => "artist", SortCriteria.Difficulty => "beatmaps.difficulty_rating",
                    SortCriteria.Updated => "last_updated", SortCriteria.Ranked => "ranked_date",
                    SortCriteria.Plays => "play_count", SortCriteria.Favourites => "favourite_count",
                    _ => throw new NotSupportedException("osu.direct does not support this sorting."),
                };
                host = "https://osu.direct/api/v2/search";
                values["q"] = filters.DirectQuery();
                values["amount"] = "50";
                values["offset"] = (query.Page * 50).ToString(CultureInfo.InvariantCulture);
                values["sort"] = field + (query.Direction == SortDirection.Ascending ? ":asc" : ":desc");
                values["status"] = string.Join(',', status.Split(',').Select(s => s switch
                {
                    "graveyard" => "-2", "wip" => "-1", "pending" => "0", "ranked" => "1", "approved" => "2", "qualified" => "3", "loved" => "4",
                    _ => throw new NotSupportedException("Unsupported osu.direct status."),
                }));
                if (query.Mode >= 0) values["mode"] = query.Mode.ToString(CultureInfo.InvariantCulture);
            }
            else if (source == BeatmapCatalogueSource.Hinamizawa)
            {
                if (query.Extra.Length > 0)
                    throw new NotSupportedException("Hinamizawa extra-content filters are not supported by this adapter. Clear them or choose another catalogue.");
                string field = query.Sort switch
                {
                    SortCriteria.Title => "title", SortCriteria.Artist => "artist", SortCriteria.Difficulty => "difficulty",
                    SortCriteria.Updated => "updated", SortCriteria.Ranked => "ranked", SortCriteria.Plays => "plays", SortCriteria.Favourites => "favourites",
                    _ => throw new NotSupportedException("Hinamizawa does not support this sorting."),
                };
                host = "https://mirror.hinamizawa.ai/v3/osu/beatmaps/search/v2";
                values["query"] = filters.Text;
                filters.AddHinamizawaBounds(values);
                values["limit"] = "50";
                values["page"] = query.Page.ToString(CultureInfo.InvariantCulture);
                values["sort"] = field + (query.Direction == SortDirection.Ascending ? "_asc" : "_desc");
                // Composite categories are searched without a status then checked locally.
                // This endpoint silently ignores CSV status values; do not send one.
                if (query.Category is not (SearchCategory.Any or SearchCategory.Leaderboard))
                    values["status"] = query.Category == SearchCategory.Wip ? "pending" : query.Category.ToString().ToLowerInvariant();
                if (query.Mode >= 0) values["mode"] = query.Mode.ToString(CultureInfo.InvariantCulture);
                values["explicit"] = query.Explicit == SearchExplicit.Hide ? "hide" : "show";
            }
            else
                throw new ArgumentOutOfRangeException(nameof(source));
            // Explicit-content filtering is local, against the unfiltered page. Pagination uses raw rows.
            return host + "?" + string.Join('&', values.Select(v => Uri.EscapeDataString(v.Key) + "=" + Uri.EscapeDataString(v.Value)));
        }

        internal static bool MatchesCategory(JObject set, SearchCategory category)
        {
            string? status = (string?)set["status"];
            return category switch
            {
                SearchCategory.Any => true,
                SearchCategory.Leaderboard => status is "ranked" or "approved" or "loved" or "qualified",
                SearchCategory.Ranked => status is "ranked" or "approved",
                SearchCategory.Pending => status is "pending" or "wip",
                _ => string.Equals(status, category.ToString(), StringComparison.OrdinalIgnoreCase),
            };
        }

        internal static bool SupportsSort(BeatmapCatalogueSource source, SortCriteria sort) => source switch
        {
            BeatmapCatalogueSource.Official => true,
            BeatmapCatalogueSource.Mirria => sort == SortCriteria.Updated,
            BeatmapCatalogueSource.OsuDirect or BeatmapCatalogueSource.Hinamizawa => sort is SortCriteria.Title or SortCriteria.Artist
                or SortCriteria.Difficulty or SortCriteria.Updated or SortCriteria.Ranked or SortCriteria.Favourites or SortCriteria.Plays,
            _ => sort is SortCriteria.Title or SortCriteria.Artist or SortCriteria.Difficulty or SortCriteria.Updated or SortCriteria.Ranked or SortCriteria.Favourites,
        };

        private async Task<CataloguePage> getPage(string url, BeatmapCatalogueSource source, CancellationToken token)
        {
            PendingPage job;
            lock (sync)
            {
                // TTL is also a lifetime bound, not just a ban on cache hits.
                // Drop expired pages from previous queries before retaining more.
                var timestamp = now();
                foreach (var page in cache)
                {
                    if (page.Value.Expires <= timestamp)
                        cache.Remove(page.Key);
                }
                if (cache.TryGetValue(url, out var cached) && cached.Expires > timestamp)
                    return cached.Page with { Sets = (JArray)cached.Page.Sets.DeepClone() };
                if (cooldowns.TryGetValue(source, out var until) && until > now())
                    throw new HttpRequestException($"{source}: повтор через {Math.Ceiling((until - now()).TotalSeconds)} с после ошибки. Исходная ошибка записана в network.log.");
                if (!pending.TryGetValue(url, out job!))
                {
                    job = new PendingPage();
                    pending.Add(url, job);
                    job.Task = fetch(url, source, job.Cancellation.Token);
                }
                job.Waiters++;
            }
            try
            {
                var page = await job.Task.WaitAsync(token).ConfigureAwait(false);
                lock (sync)
                {
                    // Bound cached payload as well as entry count (at most 16 MiB of JSON).
                    cache[url] = new CachedPage(page, now().AddMinutes(5), page.Sets.ToString(Newtonsoft.Json.Formatting.None).Length * 2);
                    while (cache.Count > 64 || cache.Values.Sum(c => (long)c.Bytes) > 16 * 1024 * 1024)
                        cache.Remove(cache.MinBy(c => c.Value.Expires).Key);
                }
                return page with { Sets = (JArray)page.Sets.DeepClone() };
            }
            finally
            {
                lock (sync)
                {
                    if (--job.Waiters == 0)
                    {
                        pending.Remove(url);
                        job.Cancellation.Cancel();
                        job.Cancellation.Dispose();
                    }
                }
            }
        }

        private async Task<CataloguePage> fetch(string url, BeatmapCatalogueSource source, CancellationToken token)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(8));
            var transferToken = deadline.Token;
            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, transferToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    double seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds
                                     ?? (response.Headers.RetryAfter?.Date - now())?.TotalSeconds ?? 30;
                    lock (sync) cooldowns[source] = now().AddSeconds(Math.Clamp(seconds, 1, 3600));
                    response.EnsureSuccessStatusCode();
                }
                using var input = await response.Content.ReadAsStreamAsync(transferToken).ConfigureAwait(false);
                using var output = new MemoryStream();
                byte[] buffer = new byte[16384];
                int read;
                while ((read = await input.ReadAsync(buffer, transferToken).ConfigureAwait(false)) != 0)
                {
                    if (output.Length + read > 8 * 1024 * 1024)
                        throw new InvalidDataException("Catalogue response exceeds 8 MiB.");
                    output.Write(buffer, 0, read);
                }
                var json = JToken.Parse(System.Text.Encoding.UTF8.GetString(output.ToArray()));
                var rows = (source is BeatmapCatalogueSource.Mirria or BeatmapCatalogueSource.OsuDirect ? json : json["beatmapsets"]) as JArray
                           ?? throw new InvalidDataException("Invalid catalogue response (missing beatmapsets).");
                // Hinamizawa upstream shapes omit totals; even a short page need not be
                // terminal. Its local shape has reliable total_pages/page instead.
                bool hasMore = source == BeatmapCatalogueSource.Hinamizawa
                    ? json["source"]?.Value<string>() == "local" && json["total_pages"] != null && json["page"] != null
                        ? json["page"]!.Value<int>() + 1 < json["total_pages"]!.Value<int>()
                        : rows.Count > 0
                    : rows.Count >= (source == BeatmapCatalogueSource.Nekoha ? 100 : 50);
                Logger.Log($"Beatmap catalogue response source={source} status={(int)response.StatusCode} bytes={output.Length} rows={rows.Count} upstream={(json is JObject ? (string?)json["source"] : null)} url={url}", LoggingTarget.Network);
                return new CataloguePage(rows, source, hasMore);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new HttpRequestException("Catalogue request timed out.");
            }
        }

        private sealed record CachedPage(CataloguePage Page, DateTimeOffset Expires, int Bytes);
        private sealed class PendingPage
        {
            internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            internal Task<CataloguePage> Task = null!;
            internal int Waiters;
        }
    }
}
