using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Discovery;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class DiscoveryMediaTest
    {
        [TestCase(5980826, 2647761, true)]
        [TestCase(4519018, 2145523, false)]
        [Explicit("Bounded live requests to the exact reported public map/clip sources.")]
        public async Task ReportedPublicMedia(int mapId, int setId, bool hasRaw)
        {
            using var media = new DiscoveryMedia();
            var result = await media.LoadAsync(new APIBeatmap { OnlineID = mapId, OnlineBeatmapSetID = setId, RulesetID = 0 }, CancellationToken.None);
            Assert.That(result.Audio.Length, Is.InRange(1000, 2 * 1024 * 1024));
            Assert.That(result.Map.HitObjects.Count > 0, Is.EqualTo(hasRaw));
            Assert.That(result.Map.Metadata.PreviewTime >= 0, Is.EqualTo(hasRaw));
        }

        [Test]
        public async Task MissingRawFileDoesNotDiscardAvailableAudio()
        {
            var handler = new PreviewHandler();
            using var http = new HttpClient(handler);
            using var media = new DiscoveryMedia(http);
            var result = await media.LoadAsync(selected(), CancellationToken.None);
            Assert.That(result.Audio, Is.EqualTo(handler.Audio));
            Assert.That(result.Map.Metadata.PreviewTime, Is.EqualTo(-1));
            Assert.That(result.Map.HitObjects, Is.Empty);
            Assert.That(result.Map.BeatmapInfo.OnlineID, Is.EqualTo(4519018));
            Assert.That(handler.Urls, Has.Count.EqualTo(4));
            foreach (string source in DiscoveryMedia.RawSources(4519018))
                Assert.That(handler.Urls, Does.Contain(source));
            Assert.That(handler.Urls, Has.None.Contains("download"));
        }

        [Test]
        public async Task Audio404UsesBoundedPreviewMirror()
        {
            var handler = new PreviewHandler { FailPrimaryAudio = true, ValidRaw = true };
            using var http = new HttpClient(handler);
            using var media = new DiscoveryMedia(http);
            var result = await media.LoadAsync(selected(), CancellationToken.None);
            Assert.That(result.Audio, Is.EqualTo(handler.Audio));
            Assert.That(result.Map.Metadata.PreviewTime, Is.EqualTo(60000));
            Assert.That(handler.Urls, Does.Contain("https://osu.direct/api/media/preview/set/2145523"));
            Assert.That(handler.Urls, Has.Count.EqualTo(3));
        }

        [TestCase("catboy.best")]
        [TestCase("osu.direct")]
        public async Task InvalidRawRevisionDoesNotBlockOtherMirrors(string validHost)
        {
            var handler = new PreviewHandler { ValidRaw = true, ValidRawHost = validHost };
            using var http = new HttpClient(handler);
            using var media = new DiscoveryMedia(http);
            var result = await media.LoadAsync(selected(), CancellationToken.None);
            Assert.That(result.Map.Metadata.PreviewTime, Is.EqualTo(60000));
            Assert.That(handler.Urls, Does.Contain(validHost == "catboy.best" ? "https://catboy.best/osu/4519018" : "https://osu.direct/api/osu/4519018/raw"));
            Assert.That(handler.Urls, Has.None.Contains("hinamizawa"));
        }

        [Test]
        public void CancellationDoesNotStartFallbackTransfers()
        {
            var handler = new PreviewHandler();
            using var http = new HttpClient(handler);
            using var media = new DiscoveryMedia(http);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.ThrowsAsync<OperationCanceledException>(async () => await media.LoadAsync(selected(), cancellation.Token));
            Assert.That(handler.Urls, Is.Empty);
        }

        private static APIBeatmap selected() => new APIBeatmap { OnlineID = 4519018, OnlineBeatmapSetID = 2145523, RulesetID = 0 };

        private sealed class PreviewHandler : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly byte[] Audio = { 0x49, 0x44, 0x33, 1, 2, 3 };
            public bool FailPrimaryAudio;
            public bool ValidRaw;
            public string? ValidRawHost;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri!.ToString();
                Urls.Add(url);
                Assert.That(request.Headers.UserAgent.ToString(), Does.Contain("Morasooma"));
                bool audio = url.Contains("/preview/", StringComparison.Ordinal);
                bool fail = audio ? FailPrimaryAudio && request.RequestUri.Host == "b.ppy.sh" : !ValidRaw;
                byte[] bytes = audio ? Audio : Encoding.UTF8.GetBytes("osu file format v14\n[General]\nPreviewTime:60000\nMode:0\n[Metadata]\nBeatmapID:4519018\nBeatmapSetID:2145523\n");
                if (!audio && ValidRawHost != null && request.RequestUri.Host != ValidRawHost)
                    bytes = Encoding.UTF8.GetBytes("osu file format v14\n[General]\nPreviewTime:60000\nMode:0\n[Metadata]\nBeatmapID:1\nBeatmapSetID:2145523\n");
                return Task.FromResult(new HttpResponseMessage(fail ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            }
        }
    }
}
