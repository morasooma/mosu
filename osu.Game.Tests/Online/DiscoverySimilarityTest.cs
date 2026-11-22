using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Overlays.Discovery;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class DiscoverySimilarityTest
    {
        [Test]
        public async Task TestAnonymousBoundedSearchAndMissingSeed()
        {
            using var client = new HttpClient(new Handler(request =>
            {
                Assert.That(request.RequestUri!.Host, Is.EqualTo("api.ayreth.site"));
                Assert.That(request.Headers.Authorization, Is.Null);
                Assert.That(request.RequestUri.Query, Does.Contain("beatmap_id=123"));
                Assert.That(request.Headers.UserAgent.ToString(), Does.Contain("Morasooma"));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"matches\":[{\"beatmap_id\":456,\"beatmapset_id\":45,\"similarity\":0.8},{\"beatmap_id\":-1,\"beatmapset_id\":45,\"similarity\":0.8}]}") };
            }));
            var result = await new DiscoverySimilarity(client).SearchAsync(123, default);
            Assert.That(result.Single(), Is.EqualTo(new DiscoveryNeighbour(456, 45, 0.8, 123)));
            using var missing = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
            Assert.That(await new DiscoverySimilarity(missing).SearchAsync(123, default), Is.Empty);
            using var huge = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 262145)) }));
            Assert.ThrowsAsync<System.IO.InvalidDataException>(async () => await new DiscoverySimilarity(huge).SearchAsync(123, default));
        }

        [Test]
        public void TestQueuedFeedbackCannotCrossAccountBoundary()
        {
            var api = new osu.Game.Online.API.DummyAPIAccess();
            api.LocalUser.Value = new osu.Game.Online.API.Requests.Responses.APIUser { Id = 43 };
            var request = new osu.Game.Online.API.Requests.DiscoveryFeedbackRequest(new osu.Game.Online.API.Requests.DiscoveryFeedbackEvent(), 42, api.Endpoints.APIUrl);
            request.AttachAPI(api);
            request.Perform();
            Assert.That(request.CompletionState, Is.EqualTo(osu.Game.Online.API.APIRequestCompletionState.Failed));
        }

        [Test]
        public void TestNeighboursMergeMultipleStylesWithoutCentroidAndDeduplicateSeeds()
        {
            var result = DiscoverySimilarity.Merge(new[] { new DiscoveryNeighbour(1, 10, .8, 100), new DiscoveryNeighbour(1, 10, .7, 200),
                new DiscoveryNeighbour(1, 10, .8, 100), new DiscoveryNeighbour(2, 20, .82, 300) });
            Assert.That(result.Select(n => n.MapId), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(result[0].Similarity, Is.EqualTo(.83).Within(.0001));
        }

        [Test]
        [Explicit("Live public MapMatcher corpus check; not a recommendation-quality benchmark.")]
        public async Task TestLivePublicSeedAndAbsentRidley()
        {
            var provider = new DiscoverySimilarity();
            var found = await provider.SearchAsync(131891, default);
            Assert.That(found.Count, Is.GreaterThan(0));
            Assert.That(found.All(n => n.Similarity is >= 0 and <= 1 && n.MapId > 0), Is.True);
            Assert.That(await provider.SearchAsync(5980826, default), Is.Empty);
        }

        private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
        }
    }
}
