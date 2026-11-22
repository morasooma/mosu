// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using osu.Framework.Extensions;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Game.Extensions;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Online.API.Requests
{
    public class SearchBeatmapSetsRequest : APIRequest<SearchBeatmapSetsResponse>
    {
        [CanBeNull]
        public IReadOnlyCollection<SearchGeneral> General { get; }

        public SearchCategory SearchCategory { get; }

        public SortCriteria SortCriteria { get; }

        public SortDirection SortDirection { get; }

        public SearchGenre Genre { get; }

        public SearchLanguage Language { get; }

        [CanBeNull]
        public IReadOnlyCollection<SearchExtra> Extra { get; }

        public SearchPlayed Played { get; }

        public SearchExplicit ExplicitContent { get; }

        public bool ServerExclusiveOnly =>
            ruleset.OnlineID == 10 || Extra?.Contains(SearchExtra.ServerExclusive) == true;

        [CanBeNull]
        public IReadOnlyCollection<ScoreRank> Ranks { get; }

        public BeatmapCatalogueSource CatalogueSource { get; init; } = BeatmapCatalogueSource.Official;
        public BeatmapCatalogueSource? ActualCatalogueSource { get; private set; }
        internal BeatmapCatalogueSearch MirrorSearch { get; init; } = BeatmapCatalogueSearch.Shared;

        internal CatalogueQuery MirrorQuery => new CatalogueQuery(query ?? string.Empty, ruleset.OnlineID, SearchCategory, SortCriteria, SortDirection,
            Genre, Language, ExplicitContent, Extra?.ToArray() ?? [], General?.ToArray() ?? [],
            Played != SearchPlayed.Any || Ranks?.Count > 0, cursor?.Properties.TryGetValue("mirror_page", out var page) == true ? page.ToObject<int>() : 0);

        public override void Perform()
        {
            if (CatalogueSource == BeatmapCatalogueSource.Official && !ServerExclusiveOnly && API is not OfficialOsuBeatmapApi)
            {
                Fail(new InvalidOperationException("Connect the official osu! account, or select a mirror in the beatmap library."));
                return;
            }
            if (ServerExclusiveOnly || CatalogueSource == BeatmapCatalogueSource.Official)
            {
                base.Perform();
                return;
            }
            performMirrorSearch().WaitSafely();
        }

        private async Task performMirrorSearch()
        {
            using var cancellation = new CancellationTokenSource();
            using var monitor = new Timer(_ =>
            {
                if (CompletionState != APIRequestCompletionState.Waiting)
                    cancellation.Cancel();
            }, null, 0, 50);
            try
            {
                var criteria = MirrorQuery;
                var filters = new BeatmapCatalogueFilters(criteria.Text);
                var page = await MirrorSearch.SearchAsync(criteria, CatalogueSource, cancellation.Token).ConfigureAwait(false);
                ActualCatalogueSource = page.Source;
                var sets = new List<Responses.APIBeatmapSet>();
                var serializer = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
                {
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                });
                foreach (var row in page.Sets.OfType<JObject>())
                {
                    int id = (int?)row["id"] ?? 0;
                    if (id <= 0 || id >= BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD)
                        continue;
                    if (row["user"] is JObject user && user["id"] == null)
                        row.Remove("user");
                    // Mirria uses camelCase for some difficulty fields.
                    foreach (var beatmap in row["beatmaps"]?.OfType<JObject>() ?? [])
                    {
                        foreach (string key in new[] { "countCircles", "countSliders", "countSpinners", "isScoreable", "lastUpdated" })
                        {
                            string snake = string.Concat(key.Select(c => char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : c.ToString()));
                            if (beatmap[key] != null && beatmap[snake] == null) beatmap[snake] = beatmap[key]!.DeepClone();
                        }
                    }
                    var set = row.ToObject<Responses.APIBeatmapSet>(serializer)!;
                    foreach (var beatmap in set.Beatmaps) beatmap.BeatmapSet = set;
                    if (BeatmapMirrorMetadata.Parse(page.Source.ToString(), id, row) is { } metadata)
                        BeatmapCatalogueMetadataCache.Store(id, metadata with { Complete = criteria.Mode < 0 && metadata.Complete });
                    if (ExplicitContent == SearchExplicit.Hide && set.HasExplicitContent
                        || !BeatmapCatalogueSearch.MatchesCategory(row, criteria.Category) || !filters.Matches(row, criteria.Mode))
                        continue;
                    sets.Add(set);
                }
                Logger.Log($"Beatmap catalogue source={page.Source} page={criteria.Page} raw={page.Sets.Count} shown={sets.Count}", LoggingTarget.Network);
                TriggerSuccess(new SearchBeatmapSetsResponse
                {
                    BeatmapSets = sets,
                    Cursor = page.HasMore ? new Cursor { Properties = new Dictionary<string, JToken> { ["mirror_page"] = criteria.Page + 1 } } : null,
                    Total = sets.Count,
                });
            }
            catch (Exception e)
            {
                Fail(e);
            }
            finally
            {
                await monitor.DisposeAsync().ConfigureAwait(false);
            }
        }

        private readonly string query;
        private readonly RulesetInfo ruleset;
        private readonly Cursor cursor;

        private string directionString => SortDirection == SortDirection.Descending ? @"desc" : @"asc";

        public SearchBeatmapSetsRequest(
            string query,
            RulesetInfo ruleset,
            Cursor cursor = null,
            IReadOnlyCollection<SearchGeneral> general = null,
            SearchCategory searchCategory = SearchCategory.Any,
            SortCriteria sortCriteria = SortCriteria.Ranked,
            SortDirection sortDirection = SortDirection.Descending,
            SearchGenre genre = SearchGenre.Any,
            SearchLanguage language = SearchLanguage.Any,
            IReadOnlyCollection<SearchExtra> extra = null,
            IReadOnlyCollection<ScoreRank> ranks = null,
            SearchPlayed played = SearchPlayed.Any,
            SearchExplicit explicitContent = SearchExplicit.Hide)
        {
            this.query = query;
            this.ruleset = ruleset;
            this.cursor = cursor;

            General = general;
            SearchCategory = searchCategory;
            SortCriteria = sortCriteria;
            SortDirection = sortDirection;
            Genre = genre;
            Language = language;
            Extra = extra;
            Ranks = ranks;
            Played = played;
            ExplicitContent = explicitContent;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            bool serverExclusiveOnly = ServerExclusiveOnly;
            IEnumerable<SearchExtra> forwardedExtra = serverExclusiveOnly
                ? Extra?.Where(e => e != SearchExtra.ServerExclusive)
                : Extra;

            if (query != null)
                req.AddParameter("q", query);

            if (General != null && General.Any())
                req.AddParameter("c", string.Join('.', General.Select(e => e.ToString().ToSnakeCase())));

            if (ruleset.OnlineID >= 0)
                req.AddParameter("m", ruleset.OnlineID.ToString());

            // For server-exclusive searches, always use "any" status so all server
            // beatmaps are shown regardless of their rank status in the database.
            req.AddParameter("s", serverExclusiveOnly ? "any" : SearchCategory.ToString().ToLowerInvariant());

            if (Genre != SearchGenre.Any)
                req.AddParameter("g", ((int)Genre).ToString());

            if (Language != SearchLanguage.Any)
                req.AddParameter("l", ((int)Language).ToString());

            req.AddParameter("sort", $"{SortCriteria.ToString().ToLowerInvariant()}_{directionString}");

            if (forwardedExtra != null && forwardedExtra.Any())
                req.AddParameter("e", string.Join('.', forwardedExtra.Select(e => e.ToString().ToLowerInvariant())));

            if (Ranks != null && Ranks.Any())
                req.AddParameter("r", string.Join('.', Ranks.Select(r => r.ToString())));

            if (Played != SearchPlayed.Any)
                req.AddParameter("played", Played.ToString().ToLowerInvariant());

            req.AddParameter("nsfw", ExplicitContent == SearchExplicit.Show ? "true" : "false");

            if (cursor != null)
            {
                if (serverExclusiveOnly && cursor.Properties.TryGetValue("offset", out var offsetValue))
                    req.AddParameter("offset", offsetValue.ToString());
                else
                    req.AddCursor(cursor);
            }

            return req;
        }

        protected override string Uri => ServerExclusiveOnly
            ? $@"{API!.Endpoints.APIUrl}/api/private/beatmapsets/server-search"
            : base.Uri;

        protected override string Target => @"beatmapsets/search";

        protected override void PostProcess()
        {
            base.PostProcess();

            if (Response != null && Extra?.Contains(SearchExtra.ServerExclusive) == true)
            {
                int currentOffset = 0;
                if (cursor != null && cursor.Properties.TryGetValue("offset", out var offsetToken))
                    currentOffset = offsetToken.ToObject<int>();

                int returnedCount = Response.BeatmapSets?.Count() ?? 0;

                if (returnedCount > 0)
                {
                    Response.Cursor = new Cursor
                    {
                        Properties = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                        {
                            { "offset", Newtonsoft.Json.Linq.JToken.FromObject(currentOffset + returnedCount) }
                        }
                    };
                }
            }
        }
    }
}
