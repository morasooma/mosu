using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Online;
using osu.Game.Beatmaps;
using osu.Game.Online.Hinamizawa;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class HinamizawaClientTest
    {
        [Test]
        public void TestSayobotRequestAndStatusMapping()
        {
            string body = HinamizawaClient.BuildSayobotSearchBody("muship", 2, 3);
            Assert.That(body, Does.Contain("\"cmd\":\"beatmaplist\""));
            Assert.That(body, Does.Contain("\"limit\":25"));
            Assert.That(body, Does.Contain("\"offset\":3"));
            Assert.That(body, Does.Contain("\"mode\":4"));
            Assert.That(body, Does.Contain("\"type\":\"search\""));
            Assert.That(HinamizawaClient.BuildSayobotSearchBody("", 0, 0), Does.Contain("\"type\":\"new\""));
            Assert.That(HinamizawaClient.BuildSayobotSearchBody("", 0, 0), Does.Not.Contain("\"keyword\""));
            Assert.That(HinamizawaClient.SayobotStatus(-2), Is.EqualTo(BeatmapOnlineStatus.Graveyard));
            Assert.That(HinamizawaClient.SayobotStatus(4), Is.EqualTo(BeatmapOnlineStatus.Loved));
            Assert.That(HinamizawaClient.SayobotStatus(99), Is.EqualTo(BeatmapOnlineStatus.None));
        }

        [Test]
        public async Task TestSayobotSearchLoadsDetailsAndMapsAllDifficulties()
        {
            using var client = new HinamizawaClient(Path.GetTempPath(), new SayobotHandler());
            var first = await client.SearchSayobotAsync("muship", 0, 0, CancellationToken.None);
            Assert.That(first.Sets, Has.Length.EqualTo(1));
            Assert.That(first.Sets[0].OnlineID, Is.EqualTo(123));
            Assert.That(first.Sets[0].Beatmaps, Has.Length.EqualTo(2));
            Assert.That(first.Sets[0].Artist, Is.EqualTo("muship"));
            Assert.That(first.Sets[0].Beatmaps[0].Checksum, Is.Empty);
            Assert.That(first.Sets[0].Beatmaps[1].OnlineID, Is.EqualTo(457));
            Assert.That(first.HasMore, Is.True);
            Assert.That(first.NextOffset, Is.EqualTo(7));
            var second = await client.SearchSayobotAsync("muship", 0, first.NextOffset!.Value, CancellationToken.None);
            Assert.That(second.HasMore, Is.False);
            Assert.That(second.PaginationWarning, Does.Contain("endid"));
        }

        [Test]
        public void TestArchiveProviderContractsAndCacheIsolation()
        {
            Assert.That(HinamizawaClient.BuildArchiveUrl(123, HinamizawaArchiveProvider.Hinamizawa), Is.EqualTo("https://mirror.hinamizawa.ai/api/v1/hinai/d/123?noVideo=1"));
            Assert.That(HinamizawaClient.BuildArchiveUrl(123, HinamizawaArchiveProvider.NeriNyan), Is.EqualTo("https://api.nerinyan.moe/d/123?noVideo=true"));
            Assert.Throws<ArgumentOutOfRangeException>(() => HinamizawaClient.BuildArchiveUrl(0, HinamizawaArchiveProvider.NeriNyan));
        }

        [Test]
        public void TestQueryEncodingAndInvariantNumbers()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                string url = HinamizawaClient.BuildSearchUrl(new HinamizawaSearch("a&b 日本", MinStars: 4.5, MaxPP: 200), 2);
                Assert.That(url, Does.Contain("query=a%26b%20"));
                Assert.That(url, Does.Contain("min_stars=4.5"));
                Assert.That(url, Does.Contain("page=2"));
                Assert.That(url, Does.Not.Contain("&mode="));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void TestDownloadProgressHandlesUnknownLengthAndCacheHit()
        {
            var unknown = new HinamizawaDownloadProgress(1024, null, 512);
            Assert.That(unknown.Fraction, Is.Null);
            Assert.That(unknown.SecondsRemaining, Is.Null);
            var cached = new HinamizawaDownloadProgress(2048, 2048, 0);
            Assert.That(cached.Fraction, Is.EqualTo(1));
            Assert.That(cached.SecondsRemaining, Is.Null);
        }

        [Test]
        public async Task TestCachedArchiveReportsCompleteProgress()
        {
            string root = Path.Combine(Path.GetTempPath(), "hinai-cache-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                string revision = "revision";
                string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(revision)))[..16];
                byte[] bytes = [1, 2, 3];
                await File.WriteAllBytesAsync(Path.Combine(root, "hinamizawa-123-" + key + ".osz"), bytes);
                using var client = new HinamizawaClient(root, new StubHandler("{}"));
                var progress = new ProgressRecorder();
                string result = await client.GetArchiveAsync(123, null, CancellationToken.None, revision, progress);
                Assert.That(File.ReadAllBytes(result), Is.EqualTo(bytes));
                Assert.That(progress.Last?.BytesReceived, Is.EqualTo(bytes.Length));
                Assert.That(progress.Last?.TotalBytes, Is.EqualTo(bytes.Length));
            }
            finally { Directory.Delete(root, true); }
        }

        [TestCase("../outside")]
        [TestCase("..\\outside")]
        [TestCase("/etc/passwd")]
        [TestCase("C:\\outside")]
        public void TestArchiveTraversalRejected(string path)
        {
            Assert.Throws<InvalidDataException>(() => HinamizawaClient.ResolveArchivePath(Path.Combine(Path.GetTempPath(), "hinai-cache"), path));
        }

        [Test]
        public void TestSafeNestedPath()
        {
            string root = Path.Combine(Path.GetTempPath(), "hinai-cache");
            Assert.That(HinamizawaClient.ResolveArchivePath(root, "samples\\hit.wav"), Is.EqualTo(Path.Combine(root, "samples", "hit.wav")));
        }

        [Test]
        public async Task TestSearchRepairsCreatorAndConnectsDifficulties()
        {
            using var client = new HinamizawaClient(Path.GetTempPath(), new StubHandler("""
                {"source":"local","total_count":51,"total_pages":2,"beatmapsets":[{"id":123,"title":"Song","artist":"Artist","creator":"Mapper","user_id":42,"user":{},"status":"ranked","beatmaps":[{"id":456,"beatmapset_id":123,"mode_int":0,"version":"Hard","checksum":"abc"}]}]}
                """));
            var page = await client.SearchAsync(new HinamizawaSearch(), 0, CancellationToken.None);
            Assert.That(page.HasMore, Is.True);
            Assert.That(page.TotalCount, Is.EqualTo(51));
            Assert.That(page.Source, Is.EqualTo("local"));
            Assert.That(page.Sets[0].Author.Username, Is.EqualTo("Mapper"));
            Assert.That(page.Sets[0].Author.Id, Is.EqualTo(42));
            Assert.That(page.Sets[0].Beatmaps[0].BeatmapSet, Is.SameAs(page.Sets[0]));
            Assert.That(page.Sets[0].Preview, Does.StartWith(HinamizawaClient.HOST));
            Assert.That((await client.SearchAsync(new HinamizawaSearch(), 1, CancellationToken.None)).HasMore, Is.False);
        }

        [Test]
        public async Task TestSearchOutageIsNotReportedAsEmptyCatalogue()
        {
            using var client = new HinamizawaClient(Path.GetTempPath(), new StubHandler("{\"error\":\"All beatmap sources unavailable\",\"sources_tried\":[]}"));
            Assert.ThrowsAsync<HttpRequestException>(async () => await client.SearchAsync(new HinamizawaSearch(), 0, CancellationToken.None));
        }

        [Test]
        public async Task TestShortUpstreamPageIsNotTerminal()
        {
            using var client = new HinamizawaClient(Path.GetTempPath(), new StubHandler("""
                {"source":"upstream","beatmapsets":[{"id":123,"creator":"Mapper","user_id":42,"beatmaps":[]}]}
                """));
            Assert.That((await client.SearchAsync(new HinamizawaSearch(), 0, CancellationToken.None)).HasMore, Is.True);
        }

        [Test]
        public async Task TestEmptyPageIsTerminal()
        {
            using var client = new HinamizawaClient(Path.GetTempPath(), new StubHandler("{\"beatmapsets\":[]}"));
            Assert.That((await client.SearchAsync(new HinamizawaSearch(), 0, CancellationToken.None)).HasMore, Is.False);
        }

        [Test]
        public void TestExtractionIsAtomicAndReusable()
        {
            string root = Path.Combine(Path.GetTempPath(), "hinai-test-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                string path = Path.Combine(root, "123.osz");
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
                {
                    using var writer = new StreamWriter(zip.CreateEntry("nested/map.osu").Open());
                    writer.Write("osu file format v14");
                }
                string extracted = HinamizawaClient.ExtractArchive(path, CancellationToken.None);
                Assert.That(File.Exists(Path.Combine(extracted, "nested", "map.osu")), Is.True);
                Assert.That(HinamizawaClient.ExtractArchive(path, CancellationToken.None), Is.EqualTo(extracted));
                Assert.That(Directory.Exists(extracted + ".extracting"), Is.False);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void TestMaliciousArchiveDoesNotExtract()
        {
            string root = Path.Combine(Path.GetTempPath(), "hinai-test-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                string path = Path.Combine(root, "123.osz");
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) zip.CreateEntry("../escaped.osu");
                Assert.Throws<InvalidDataException>(() => HinamizawaClient.ExtractArchive(path, CancellationToken.None));
                Assert.That(File.Exists(Path.Combine(root, "escaped.osu")), Is.False);
                Assert.That(Directory.Exists(Path.Combine(root, "123.extracting")), Is.False);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void TestPreviewMediaUsesDifficultyIds()
        {
            Assert.That(HinamizawaPreviewBeatmap.GetBackgroundUrl(456), Is.EqualTo(HinamizawaClient.HOST + "/v3/osu/beatmaps/b/456/bg"));
            Assert.That(HinamizawaPreviewBeatmap.GetAudioUrl(457), Is.EqualTo(HinamizawaClient.HOST + "/v3/osu/beatmaps/b/457/audio"));
        }

        [Test]
        public void TestOnlyExactHttpsMirrorHostIsTrusted()
        {
            string url = HinamizawaClient.HOST + "/v3/osu/music/preview/123";
            Assert.That(TrustedDomainOnlineStore.ResolveLookupUrl(url), Is.EqualTo(url));
            Assert.That(TrustedDomainOnlineStore.ResolveLookupUrl("https://evil.mirror.hinamizawa.ai/image"), Is.Empty);
            Assert.That(TrustedDomainOnlineStore.ResolveLookupUrl("http://mirror.hinamizawa.ai/image"), Is.Empty);
            Assert.That(TrustedDomainOnlineStore.ResolveLookupUrl("https://mirror.hinamizawa.ai:444/image"), Is.Empty);
        }

        private sealed class ProgressRecorder : IProgress<HinamizawaDownloadProgress>
        {
            public HinamizawaDownloadProgress? Last { get; private set; }
            public void Report(HinamizawaDownloadProgress value) => Last = value;
        }

        [TestCase(0, 0)]
        [TestCase(5, 5)]
        public async Task TestSayobotInvalidCursorIsTerminalWithWarning(int offset, int endId)
        {
            using var client = new HinamizawaClient(Path.GetTempPath(), new SayobotCursorHandler(endId, 1));
            var page = await client.SearchSayobotAsync("muship", 0, offset, CancellationToken.None);
            Assert.That(page.HasMore, Is.False);
            Assert.That(page.PaginationWarning, Does.Contain("endid"));
        }

        [Test]
        public async Task TestSayobotRepeatingCursorIsNotReportedAsMorePages()
        {
            using var client = new HinamizawaClient(Path.GetTempPath(), new SayobotCursorHandler(4, 4));
            var first = await client.SearchSayobotAsync("muship", 0, 0, CancellationToken.None);
            Assert.That(first.HasMore, Is.True);
            var repeated = await client.SearchSayobotAsync("muship", 0, first.NextOffset!.Value, CancellationToken.None);
            Assert.That(repeated.HasMore, Is.False);
            Assert.That(repeated.PaginationWarning, Does.Contain("endid"));
        }

        private sealed class SayobotCursorHandler(int firstEndId, int laterEndId) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.That(request.Headers.Authorization, Is.Null);
                string response = request.Method == HttpMethod.Post
                    ? request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult().Contains("\"type\":\"search\"")
                        ? $"{{\"status\":0,\"results\":100,\"data\":[{{\"sid\":123}}],\"endid\":{firstEndId}}}"
                        : $"{{\"status\":0,\"results\":100,\"data\":[{{\"sid\":124}}],\"endid\":{laterEndId}}}"
                    : "{\"status\":0,\"data\":{\"sid\":123,\"approved\":4,\"title\":\"Song\",\"artist\":\"muship\",\"creator\":\"Mapper\",\"creator_id\":9,\"bid_data\":[{\"bid\":456,\"mode\":0,\"version\":\"Easy\",\"length\":\"100\",\"AR\":5,\"OD\":5,\"CS\":4,\"HP\":5,\"star\":1.5},{\"bid\":457,\"mode\":0,\"version\":\"Hard\",\"length\":\"120\",\"star\":4.2}]}}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
            }
        }

        private sealed class SayobotHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.That(request.Headers.Authorization, Is.Null);
                string response = request.Method == HttpMethod.Post
                    ? request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult().Contains("\"offset\":0")
                        ? "{\"status\":0,\"results\":100,\"data\":[{\"sid\":123}],\"endid\":7}"
                        : "{\"status\":0,\"results\":100,\"data\":[{\"sid\":124}],\"endid\":0}"
                    : "{\"status\":0,\"data\":{\"sid\":123,\"approved\":4,\"title\":\"Song\",\"artist\":\"muship\",\"creator\":\"Mapper\",\"creator_id\":9,\"bid_data\":[{\"bid\":456,\"mode\":0,\"version\":\"Easy\",\"length\":\"100\",\"AR\":5,\"OD\":5,\"CS\":4,\"HP\":5,\"star\":1.5},{\"bid\":457,\"mode\":0,\"version\":\"Hard\",\"length\":\"120\",\"star\":4.2}]}}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
            }
        }

        private sealed class StubHandler(string json) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.That(request.Headers.Authorization, Is.Null);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            }
        }
    }
}
