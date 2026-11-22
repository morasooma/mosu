using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class BeatmapCatalogueSearchTest
    {
        private static CatalogueQuery query => new CatalogueQuery("Camellia", 0, SearchCategory.Ranked, SortCriteria.Updated,
            SortDirection.Descending, SearchGenre.Any, SearchLanguage.Any, SearchExplicit.Hide, [], [], false);

        private const string set = """
            {"id":123456,"artist":"Camellia","title":"Test","creator":"mapper","status":"ranked","nsfw":false,
             "last_updated":"2026-10-01T00:00:00Z","play_count":null,"favourite_count":null,"beatmaps":[{"id":123457,"beatmapset_id":123456,"mode":"osu",
             "checksum":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","countCircles":10,"lastUpdated":"2026-10-01T00:00:00Z"}]}
            """;

        [Test]
        public void UrlsUseSourceSpecificFiltersAndPagination()
        {
            string nekoha = BeatmapCatalogueSearch.BuildUrl(query with { Page = 2 }, BeatmapCatalogueSource.Nekoha);
            Assert.That(nekoha, Does.Contain("page=3").And.Contain("mode=osu").And.Contain("status=ranked%2Capproved"));
            string mirria = BeatmapCatalogueSearch.BuildUrl(query with { Page = 2 }, BeatmapCatalogueSource.Mirria);
            Assert.That(mirria, Does.Contain("offset=100").And.Contain("limit=50").And.Contain("statuses%5B0%5D=ranked"));
        }

        [TestCase("star>6")]
        [TestCase("stars>=6")]
        [TestCase("sr>6")]
        public void NumericQueriesUseMirrorParametersNotLiteralText(string text)
        {
            string url = BeatmapCatalogueSearch.BuildUrl(query with { Text = text }, BeatmapCatalogueSource.Nekoha);
            Assert.That(url, Does.Contain("q=&").And.Contain("stars_min=6"));
            string mirria = BeatmapCatalogueSearch.BuildUrl(query with { Text = text }, BeatmapCatalogueSource.Mirria);
            Assert.That(mirria, Does.Contain("query=&"));
            var filters = new BeatmapCatalogueFilters(text);
            Assert.That(filters.Matches(JObject.Parse("{\"beatmaps\":[{\"difficulty_rating\":6.1,\"mode_int\":0}]}"), 0), Is.True);
            Assert.That(filters.Matches(JObject.Parse("{\"beatmaps\":[{\"difficulty_rating\":5.9,\"mode_int\":0}]}"), 0), Is.False);
        }

        [Test]
        public void AllNumericConditionsMustMatchTheSameDifficultyAndStrictBoundsArePreserved()
        {
            var filters = new BeatmapCatalogueFilters("camellia star>6 ar>=9 length<180");
            Assert.That(filters.Text, Is.EqualTo("camellia"));
            Assert.That(filters.Matches(JObject.Parse("{\"beatmaps\":[{\"difficulty_rating\":7,\"ar\":8,\"total_length\":100},{\"difficulty_rating\":5,\"ar\":10,\"total_length\":100}]}"), -1), Is.False);
            Assert.That(filters.Matches(JObject.Parse("{\"beatmaps\":[{\"difficulty_rating\":7,\"ar\":9,\"total_length\":179}]}"), -1), Is.True);
            Assert.That(new BeatmapCatalogueFilters("star>6").Matches(JObject.Parse("{\"beatmaps\":[{\"difficulty_rating\":6}]}"), -1), Is.False);
            Assert.Throws<NotSupportedException>(() => new BeatmapCatalogueFilters("pp>200"));
        }

        [Test]
        public void AutoPreservesOriginalTransferFailureWhenOneReserveLacksSelectedSort()
        {
            var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            var client = new BeatmapCatalogueSearch(handler);
            Assert.ThrowsAsync<HttpRequestException>(async () => await client.SearchAsync(query with { Sort = SortCriteria.Ranked }, BeatmapCatalogueSource.Auto, CancellationToken.None));
            Assert.That(handler.Urls.Count, Is.EqualTo(3));
            Assert.That(handler.Urls.Any(u => u.Contains("lisek", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public async Task CancelledSearchDoesNotPutMirrorIntoCooldown()
        {
            int calls = 0;
            var handler = new Handler(async (_, token) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    await Task.Delay(10000, token);
                return json("{\"beatmapsets\":[]}");
            });
            var client = new BeatmapCatalogueSearch(handler);
            using var cancel = new CancellationTokenSource();
            var pending = client.SearchAsync(query, BeatmapCatalogueSource.Nekoha, cancel.Token);
            cancel.Cancel();
            Assert.That(async () => await pending, Throws.InstanceOf<OperationCanceledException>());
            await client.SearchAsync(query, BeatmapCatalogueSource.Nekoha, CancellationToken.None);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void UnsupportedFiltersAreNeverSilentlyDropped()
        {
            foreach (var unsupported in new[]
                     {
                         query with { Personal = true }, query with { Category = SearchCategory.Favourites },
                         query with { General = [SearchGeneral.Follows] }, query with { Genre = SearchGenre.Anime },
                         query with { Extra = [SearchExtra.Storyboard] }, query with { Sort = SortCriteria.Relevance },
                     })
                Assert.Throws<NotSupportedException>(() => BeatmapCatalogueSearch.BuildUrl(unsupported, BeatmapCatalogueSource.Nekoha));
            Assert.Throws<NotSupportedException>(() => BeatmapCatalogueSearch.BuildUrl(query with { Sort = SortCriteria.Ranked }, BeatmapCatalogueSource.Mirria));
        }

        [TestCase(BeatmapCatalogueSource.OsuDirect)]
        [TestCase(BeatmapCatalogueSource.Hinamizawa)]
        public void AdditionalCataloguesTranslateConditionsAndPagination(BeatmapCatalogueSource source)
        {
            string url = Uri.UnescapeDataString(BeatmapCatalogueSearch.BuildUrl(query with { Text = "kikuo star>7 ar>=9 length<180", Page = 2 }, source));
            Assert.That(url, Does.Not.Contain("star>7"));
            if (source == BeatmapCatalogueSource.OsuDirect)
            {
                var bounds = new Dictionary<string, string>();
                new BeatmapCatalogueFilters("kikuo star>7 ar>=9 length<180").AddNekohaBounds(bounds);
                Assert.That(url, Does.Contain($"q=[beatmaps.difficulty_rating >= 7 AND beatmaps.ar >= {bounds["ar_min"]} AND beatmaps.total_length <= {bounds["length_max"]}] kikuo")
                                     .And.Contain("offset=100").And.Contain("amount=50").And.Contain("status=1,2").And.Contain("sort=last_updated:desc"));
            }
            else
                Assert.That(url, Does.Contain("query=kikuo").And.Contain("min_stars=7").And.Contain("max_length=180")
                                     .And.Contain("page=2").And.Contain("limit=50").And.Contain("status=ranked"));
            Assert.That(BeatmapCatalogueSearch.SupportsSort(source, SortCriteria.Plays), Is.True);
            Assert.That(BeatmapCatalogueSearch.SupportsSort(source, SortCriteria.Relevance), Is.False);
        }

        [Test]
        public void HinamizawaDiscoveryFiltersUseValidNonnegativeBoundsAndIntegerSeconds()
        {
            var filters = new BeatmapCatalogueFilters("star>=6.75 star<=8.5 bpm>=0 bpm<=500 length<=900");
            var bounds = new Dictionary<string, string>();
            filters.AddHinamizawaBounds(bounds);
            Assert.That(bounds["min_bpm"], Is.EqualTo("0"));
            Assert.That(bounds["max_length"], Is.EqualTo("901"));
            Assert.That(int.TryParse(bounds["max_length"], out _), Is.True);
            Assert.That(filters.Matches(JObject.Parse("""{"beatmaps":[{"mode_int":0,"difficulty_rating":7,"bpm":200,"total_length":900}]}"""), 0), Is.True);
            Assert.That(filters.Matches(JObject.Parse("""{"beatmaps":[{"mode_int":0,"difficulty_rating":7,"bpm":200,"total_length":901}]}"""), 0), Is.False);
        }

        [Test]
        public void CompositeHinamizawaStatusesAreCheckedLocallyRatherThanSentAsIgnoredCsv()
        {
            string url = BeatmapCatalogueSearch.BuildUrl(query with { Category = SearchCategory.Leaderboard }, BeatmapCatalogueSource.Hinamizawa);
            Assert.That(url, Does.Not.Contain("status="));
            Assert.That(BeatmapCatalogueSearch.MatchesCategory(JObject.Parse("{\"status\":\"graveyard\"}"), SearchCategory.Leaderboard), Is.False);
            Assert.That(BeatmapCatalogueSearch.MatchesCategory(JObject.Parse("{\"status\":\"approved\"}"), SearchCategory.Ranked), Is.True);
            Assert.That(new BeatmapCatalogueFilters("").Matches(JObject.Parse("{\"beatmaps\":[{\"mode\":\"mania\"}]}"), 0), Is.False);
        }

        [TestCase(BeatmapCatalogueSource.OsuDirect)]
        [TestCase(BeatmapCatalogueSource.Hinamizawa)]
        public async Task AdditionalCataloguesDeserializeWithoutCredentialsAndPreserveRawPagination(BeatmapCatalogueSource source)
        {
            var handler = new Handler((request, _) =>
            {
                Assert.That(request.Headers.Authorization, Is.Null);
                Assert.That(request.Headers.Contains("Cookie"), Is.False);
                string rows = "[" + string.Join(',', Enumerable.Repeat(set, source == BeatmapCatalogueSource.OsuDirect ? 50 : 1)) + "]";
                return Task.FromResult(json(source == BeatmapCatalogueSource.OsuDirect ? rows : "{\"source\":\"osudirect\",\"beatmapsets\":" + rows + "}"));
            });
            var request = new SearchBeatmapSetsRequest("camellia", new RulesetInfo { OnlineID = 0 }, sortCriteria: SortCriteria.Updated)
            {
                CatalogueSource = source, MirrorSearch = new BeatmapCatalogueSearch(handler),
            };
            await perform(request);
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
            Assert.That(request.Response!.BeatmapSets.Any(), Is.True);
            Assert.That(request.Response.Cursor, Is.Not.Null);
            Assert.That(request.ActualCatalogueSource, Is.EqualTo(source));
            BeatmapCatalogueMetadataCache.Invalidate(123456);
        }

        [TestCase(0, 1, false)]
        [TestCase(0, 2, true)]
        [TestCase(1, 2, false)]
        public async Task HinamizawaLocalPaginationUsesReportedTotals(int page, int pages, bool hasMore)
        {
            var handler = new Handler((_, _) => Task.FromResult(json($"{{\"source\":\"local\",\"page\":{page},\"total_pages\":{pages},\"beatmapsets\":[{set}]}}")));
            var result = await new BeatmapCatalogueSearch(handler).SearchAsync(query with { Page = page }, BeatmapCatalogueSource.Hinamizawa, CancellationToken.None);
            Assert.That(result.HasMore, Is.EqualTo(hasMore));
        }

        [Test]
        public async Task AutoReachesNewReserveWithoutLeakingCredentialsOrUsingOfficialEndpoint()
        {
            var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "osu.direct"
                ? json("[]") : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            var page = await new BeatmapCatalogueSearch(handler).SearchAsync(query, BeatmapCatalogueSource.Auto, CancellationToken.None);
            Assert.That(page.Source, Is.EqualTo(BeatmapCatalogueSource.OsuDirect));
            Assert.That(handler.Urls.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task CacheHasTtlAndDoesNotShareMutableJson()
        {
            var clock = DateTimeOffset.UtcNow;
            var handler = new Handler((_, _) => Task.FromResult(json("{\"beatmapsets\":[" + set + "]}")));
            var client = new BeatmapCatalogueSearch(handler, () => clock);
            var first = await client.SearchAsync(query, BeatmapCatalogueSource.Nekoha, CancellationToken.None);
            first.Sets.Clear();
            Assert.That((await client.SearchAsync(query, BeatmapCatalogueSource.Nekoha, CancellationToken.None)).Sets.Count, Is.EqualTo(1));
            Assert.That(handler.Urls.Count, Is.EqualTo(1));
            clock = clock.AddMinutes(6);
            await client.SearchAsync(query, BeatmapCatalogueSource.Nekoha, CancellationToken.None);
            Assert.That(handler.Urls.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task ExpiredPagesFromOtherQueriesAreReleasedOnNextSearch()
        {
            var clock = DateTimeOffset.UtcNow;
            var handler = new Handler((_, _) => Task.FromResult(json("{\"beatmapsets\":[" + set + "]}")));
            var client = new BeatmapCatalogueSearch(handler, () => clock);
            for (int i = 0; i < 4; i++)
                await client.SearchAsync(query with { Text = "old-" + i }, BeatmapCatalogueSource.Nekoha, CancellationToken.None);
            var cache = (System.Collections.IDictionary)typeof(BeatmapCatalogueSearch)
                .GetField("cache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(client)!;
            Assert.That(cache.Count, Is.EqualTo(4));
            clock = clock.AddMinutes(6);
            await client.SearchAsync(query with { Text = "new" }, BeatmapCatalogueSource.Nekoha, CancellationToken.None);
            Assert.That(cache.Count, Is.EqualTo(1));
        }

        [Test]
        public void MetadataCachePhysicallyReleasesExpiredEntriesOnReadAndStore()
        {
            const int oldId = 1900000001;
            const int newId = 1900000002;
            var metadata = new BeatmapMirrorMetadata("test", DateTimeOffset.UtcNow, []);
            var entries = (Dictionary<int, (BeatmapMirrorMetadata Metadata, DateTimeOffset Expires)>)typeof(BeatmapCatalogueMetadataCache)
                .GetField("entries", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
            var sync = typeof(BeatmapCatalogueMetadataCache)
                .GetField("sync", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
            try
            {
                lock (sync) entries[oldId] = (metadata, DateTimeOffset.UtcNow.AddMinutes(-1));
                Assert.That(BeatmapCatalogueMetadataCache.Get(oldId), Is.Null);
                lock (sync) Assert.That(entries.ContainsKey(oldId), Is.False);
                lock (sync) entries[oldId] = (metadata, DateTimeOffset.UtcNow.AddMinutes(-1));
                BeatmapCatalogueMetadataCache.Store(newId, metadata);
                lock (sync) Assert.That(entries.ContainsKey(oldId), Is.False);
                Assert.That(BeatmapCatalogueMetadataCache.Get(newId), Is.SameAs(metadata));
            }
            finally
            {
                BeatmapCatalogueMetadataCache.Invalidate(oldId);
                BeatmapCatalogueMetadataCache.Invalidate(newId);
            }
        }

        [Test]
        public async Task ConcurrentIdenticalRequestsShareOneTransferAndOneCancellationDoesNotCancelOtherWaiter()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new Handler(async (_, token) => { await gate.Task.WaitAsync(token); return json("{\"beatmapsets\":[]}"); });
            var client = new BeatmapCatalogueSearch(handler);
            using var cancellation = new CancellationTokenSource();
            var a = client.SearchAsync(query, BeatmapCatalogueSource.Nekoha, cancellation.Token);
            var b = client.SearchAsync(query, BeatmapCatalogueSource.Nekoha, CancellationToken.None);
            cancellation.Cancel();
            Assert.That(async () => await a, Throws.InstanceOf<OperationCanceledException>());
            gate.SetResult();
            await b;
            Assert.That(handler.Urls.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task LastWaiterCancellationAbortsUnderlyingTransfer()
        {
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new Handler(async (_, token) =>
            {
                try { await Task.Delay(10000, token); }
                finally { stopped.SetResult(); }
                return json("{}");
            });
            using var cancellation = new CancellationTokenSource();
            var task = new BeatmapCatalogueSearch(handler).SearchAsync(query, BeatmapCatalogueSource.Nekoha, cancellation.Token);
            cancellation.Cancel();
            Assert.That(async () => await task, Throws.InstanceOf<OperationCanceledException>());
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        [Test]
        public async Task AutoFallsBackSequentiallyOnFailureButNotEmptyResults()
        {
            var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Host.Contains("nekoha", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : json("[]")));
            var page = await new BeatmapCatalogueSearch(handler).SearchAsync(query, BeatmapCatalogueSource.Auto, CancellationToken.None);
            Assert.That(page.Source, Is.EqualTo(BeatmapCatalogueSource.Mirria));
            Assert.That(handler.Urls.Count, Is.EqualTo(2));
            Assert.That(handler.Urls.All(url => !url.Contains("ppy.sh", StringComparison.Ordinal) && !url.Contains("morasooma", StringComparison.Ordinal)), Is.True);
            handler = new Handler((_, _) => Task.FromResult(json("{\"beatmapsets\":[]}")));
            await new BeatmapCatalogueSearch(handler).SearchAsync(query, BeatmapCatalogueSource.Auto, CancellationToken.None);
            Assert.That(handler.Urls.Count, Is.EqualTo(1));
        }

        [Test]
        public void LaterPageCannotSwitchSourceAndManualSelectionCannotFallback()
        {
            var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            Assert.ThrowsAsync<System.IO.IOException>(async () => await new BeatmapCatalogueSearch(handler).SearchAsync(query with { Page = 1 }, BeatmapCatalogueSource.Auto, CancellationToken.None));
            Assert.That(handler.Urls.Count, Is.EqualTo(1));
            handler.Urls.Clear();
            Assert.ThrowsAsync<HttpRequestException>(async () => await new BeatmapCatalogueSearch(handler).SearchAsync(query, BeatmapCatalogueSource.Nekoha, CancellationToken.None));
            Assert.That(handler.Urls.Count, Is.EqualTo(1));
        }

        [TestCase(BeatmapCatalogueSource.OsuDirect)]
        [TestCase(BeatmapCatalogueSource.Hinamizawa)]
        public void AdditionalCatalogueFailuresNeverSwitchManualSource(BeatmapCatalogueSource source)
        {
            var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            Assert.ThrowsAsync<HttpRequestException>(async () => await new BeatmapCatalogueSearch(handler).SearchAsync(query, source, CancellationToken.None));
            Assert.That(handler.Urls.Count, Is.EqualTo(1));
        }

        [Test]
        public void CatalogueEnumKeepsExistingSavedValues()
        {
            Assert.That((int)BeatmapCatalogueSource.Auto, Is.EqualTo(0));
            Assert.That((int)BeatmapCatalogueSource.Nekoha, Is.EqualTo(1));
            Assert.That((int)BeatmapCatalogueSource.Mirria, Is.EqualTo(2));
            Assert.That((int)BeatmapCatalogueSource.Official, Is.EqualTo(3));
        }

        [Test]
        public async Task RetryAfterPreventsRepeatedOriginRequests()
        {
            var handler = new Handler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
                return Task.FromResult(response);
            });
            var client = new BeatmapCatalogueSearch(handler);
            for (int i = 0; i < 2; i++)
                Assert.ThrowsAsync<HttpRequestException>(async () => await client.SearchAsync(query, BeatmapCatalogueSource.Nekoha, CancellationToken.None));
            Assert.That(handler.Urls.Count, Is.EqualTo(1));
            await Task.CompletedTask;
        }

        [Test]
        public async Task RequestMapsDifficultiesFiltersExplicitAndCachesRevisionWithoutCredentials()
        {
            BeatmapCatalogueMetadataCache.Invalidate(123456);
            var handler = new Handler((request, _) =>
            {
                Assert.That(request.Headers.Authorization, Is.Null);
                return Task.FromResult(json("{\"beatmapsets\":[" + set + "," + set.Replace("123456", "123458").Replace("\"nsfw\":false", "\"nsfw\":true") + "]}"));
            });
            var request = makeRequest(new BeatmapCatalogueSearch(handler));
            await perform(request);
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
            var mapset = request.Response!.BeatmapSets.Single();
            Assert.That(mapset.Beatmaps[0].BeatmapSet, Is.SameAs(mapset));
            Assert.That(mapset.Beatmaps[0].TotalObjectCount, Is.EqualTo(10));
            Assert.That(BeatmapCatalogueMetadataCache.Get(123456), Is.Not.Null);
            Assert.That(BeatmapCatalogueMetadataCache.Get(123456)!.Complete, Is.False, "Mode-filtered pages may omit other modes.");
            BeatmapCatalogueMetadataCache.Invalidate(123456);
            BeatmapCatalogueMetadataCache.Invalidate(123458);
        }

        [Test]
        public async Task FullyFilteredPageKeepsCursorForRawResults()
        {
            string rows = string.Join(',', Enumerable.Repeat(set.Replace("\"nsfw\":false", "\"nsfw\":true"), 100));
            var request = makeRequest(new BeatmapCatalogueSearch(new Handler((_, _) => Task.FromResult(json("{\"beatmapsets\":[" + rows + "]}")))));
            await perform(request);
            Assert.That(request.Response!.BeatmapSets, Is.Empty);
            Assert.That(request.Response.Cursor, Is.Not.Null);
            BeatmapCatalogueMetadataCache.Invalidate(123456);
        }

        [Test]
        public void MirrorSearchRunsOutsidePrimaryQueueAndExclusiveSearchStaysOnMosu()
        {
            var primary = new Mock<IAPIProvider>();
            primary.Setup(a => a.PerformAsync(It.IsAny<APIRequest>())).Returns(Task.CompletedTask);
            var router = new BeatmapApiProvider(primary.Object, null);
            var mirror = makeRequest(new BeatmapCatalogueSearch(new Handler((_, _) => Task.FromResult(json("{}")))));
            router.Queue(mirror);
            primary.Verify(a => a.PerformAsync(mirror), Times.Once);
            primary.Verify(a => a.Queue(It.IsAny<APIRequest>()), Times.Never);
            var exclusive = new SearchBeatmapSetsRequest("", new RulesetInfo { OnlineID = 10 }) { CatalogueSource = BeatmapCatalogueSource.Mirria };
            router.Queue(exclusive);
            primary.Verify(a => a.Queue(exclusive), Times.Once);
        }

        [Test]
        public async Task OfficialSelectionNeverFallsThroughToMosuEndpoint()
        {
            var request = new SearchBeatmapSetsRequest("", new RulesetInfo { OnlineID = 0 });
            await perform(request);
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));
        }

        [Test]
        public void CatalogueChoicePersists()
        {
            using var storage = new TemporaryNativeStorage("catalogue-choice");
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<BeatmapCatalogueSource>(OsuSetting.BeatmapCatalogueSource), Is.EqualTo(BeatmapCatalogueSource.Auto));
                config.SetValue(OsuSetting.BeatmapCatalogueSource, BeatmapCatalogueSource.Mirria);
                config.Save();
            }
            using var restored = new OsuConfigManager(storage);
            Assert.That(restored.Get<BeatmapCatalogueSource>(OsuSetting.BeatmapCatalogueSource), Is.EqualTo(BeatmapCatalogueSource.Mirria));
        }

        [TestCase(""), TestCase("star>6"), Explicit("Bounded live catalogue requests, no archive download.")]
        public async Task LiveMirrorSearch(string text)
        {
            foreach (var source in new[] { BeatmapCatalogueSource.Nekoha, BeatmapCatalogueSource.Mirria })
            {
                var request = new SearchBeatmapSetsRequest(text, new RulesetInfo { OnlineID = 0 }, sortCriteria: SortCriteria.Updated)
                {
                    CatalogueSource = source,
                };
                await perform(request);
                Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
                Assert.That(request.Response!.BeatmapSets.Any(), Is.True);
                if (text == "star>6")
                    Assert.That(request.Response.BeatmapSets.All(s => s.Beatmaps.Any(b => b.StarRating > 6)), Is.True);
                Assert.That(request.Response.BeatmapSets.All(s => s.Beatmaps.All(b => ReferenceEquals(b.BeatmapSet, s))), Is.True);
                TestContext.Out.WriteLine($"{source}: {request.Response.BeatmapSets.Count()} sets, {request.Response.BeatmapSets.Sum(s => s.Beatmaps.Length)} difficulties");
            }
        }

        [TestCase(BeatmapCatalogueSource.OsuDirect, "", SearchCategory.Any)]
        [TestCase(BeatmapCatalogueSource.Hinamizawa, "", SearchCategory.Any)]
        [TestCase(BeatmapCatalogueSource.OsuDirect, "kikuo star>7", SearchCategory.Any)]
        [TestCase(BeatmapCatalogueSource.Hinamizawa, "kikuo star>7", SearchCategory.Any)]
        [TestCase(BeatmapCatalogueSource.OsuDirect, "kikuo", SearchCategory.Ranked)]
        [TestCase(BeatmapCatalogueSource.Hinamizawa, "kikuo", SearchCategory.Ranked)]
        [Explicit("Bounded two-page live search of each additional provider through the client request adapter.")]
        public async Task LiveAdditionalCatalogue(BeatmapCatalogueSource source, string text, SearchCategory category)
        {
            Cursor? cursor = null;
            var seen = new HashSet<int>();
            for (int page = 0; page < 2; page++)
            {
                var request = new SearchBeatmapSetsRequest(text, new RulesetInfo { OnlineID = 0 }, cursor,
                    searchCategory: category, sortCriteria: SortCriteria.Updated)
                {
                    CatalogueSource = source,
                };
                await perform(request);
                Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
                var sets = request.Response!.BeatmapSets.ToArray();
                if (page == 0) Assert.That(sets.Length, Is.GreaterThan(0));
                Assert.That(sets.All(s => s.Beatmaps.All(b => ReferenceEquals(b.BeatmapSet, s))), Is.True);
                Assert.That(sets.All(s => s.Beatmaps.Any(b => b.RulesetID == 0 && (!text.Contains("star>", StringComparison.Ordinal) || b.StarRating > 7))), Is.True);
                Assert.That(sets.All(s => BeatmapCatalogueSearch.MatchesCategory(new JObject { ["status"] = s.Status.ToString().ToLowerInvariant() }, category)), Is.True);
                Assert.That(sets.All(s => !s.HasExplicitContent), Is.True);
                if (sets.Length > 0)
                    Assert.That(sets.Any(s => !seen.Contains(s.OnlineID)), Is.True, "Pagination must advance, not repeat page zero.");
                seen.UnionWith(sets.Select(s => s.OnlineID));
                TestContext.Out.WriteLine($"{source} query={text} category={category} page={page}: {sets.Length} sets; hasMore={request.Response.Cursor != null}");
                cursor = request.Response.Cursor;
                if (cursor == null) break;
            }
        }

        private static SearchBeatmapSetsRequest makeRequest(BeatmapCatalogueSearch client) =>
            new SearchBeatmapSetsRequest("camellia", new RulesetInfo { OnlineID = 0 }, sortCriteria: SortCriteria.Updated)
            {
                CatalogueSource = BeatmapCatalogueSource.Nekoha,
                MirrorSearch = client,
            };

        private static async Task perform(SearchBeatmapSetsRequest request)
        {
            var api = new Mock<IAPIProvider>();
            api.SetupGet(a => a.AccessToken).Returns("secret-token-must-never-be-sent");
            api.Setup(a => a.Schedule(It.IsAny<Action>())).Callback<Action>(action => action());
            request.AttachAPI(api.Object);
            await Task.Factory.StartNew(request.Perform, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private static HttpResponseMessage json(string body) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

        private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
        {
            internal readonly List<string> Urls = new List<string>();
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.RequestUri!.ToString());
                return send(request, cancellationToken);
            }
        }
    }
}
