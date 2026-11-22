using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using FileWebRequest = osu.Framework.IO.Network.FileWebRequest;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class BeatmapMirrorDownloadTest
    {
        private static readonly byte[] current = Encoding.UTF8.GetBytes("osu file format v14\n[Metadata]\nTitle:Current\nBeatmapID:1\nBeatmapSetID:123\n");
        private static readonly byte[] stale = Encoding.UTF8.GetBytes("osu file format v14\n[Metadata]\nTitle:Old\nBeatmapID:1\nBeatmapSetID:123\n");
        private readonly ConcurrentBag<string> files = new ConcurrentBag<string>();

        [TearDown]
        public void Cleanup()
        {
            foreach (string file in files)
                File.Delete(file);
        }

        [Test]
        public void HashesRawUtf16AndChecksAllEntries()
        {
            byte[] utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("osu file format v14\n[Metadata]\nTitle:Test")).ToArray();
            string path = save(archive(utf16));
            Assert.That(BeatmapMirrorArchive.Inspect(path, CancellationToken.None), Is.EqualTo(new[] { md5(utf16) }));
        }

        [Test]
        public void RejectsEmptyAndUnsafeArchives()
        {
            Assert.Throws<InvalidDataException>(() => BeatmapMirrorArchive.Inspect(save(archive(current, "../map.osu")), CancellationToken.None));
            Assert.Throws<InvalidDataException>(() => BeatmapMirrorArchive.Inspect(save(archive(current, "audio.mp3")), CancellationToken.None));
            Assert.Throws<InvalidDataException>(() => BeatmapMirrorArchive.Inspect(save(archive(current, "C:/map.osu")), CancellationToken.None));
        }

        [Test]
        public void RejectsWrongSetAndFakeDifficultyWhenCatalogueIsUnavailable()
        {
            Assert.Throws<InvalidDataException>(() => BeatmapMirrorArchive.Inspect(save(archive(current)), CancellationToken.None, 999));
            Assert.Throws<InvalidDataException>(() => BeatmapMirrorArchive.Inspect(save(archive(Encoding.UTF8.GetBytes("<html>not a map</html>"))), CancellationToken.None));
        }

        [Test]
        public void RejectsCrcCorruptionOutsideTheDifficulty()
        {
            byte[] bytes = archive(current, extra: "audio-check-marker");
            int offset = bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes("audio-check-marker"));
            Assert.That(offset, Is.GreaterThan(0));
            bytes[offset] ^= 1;
            Assert.Throws<InvalidDataException>(() => BeatmapMirrorArchive.Inspect(save(bytes), CancellationToken.None));
        }

        [Test]
        public void CancelledArchiveInspectionStops()
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => BeatmapMirrorArchive.Inspect(save(archive(current)), cancelled.Token));
        }

        [Test]
        public void ReferenceUsesNewestCatalogueAndIgnoresLocalEdits()
        {
            var old = metadata("old", "2026-01-01T00:00:00Z", stale);
            var latest = metadata("new", "2026-10-01T00:00:00Z", current);
            Assert.That(BeatmapMirrorMetadata.SelectReference(new[] { old, latest }), Is.EqualTo(latest));
            Assert.That(latest.Matches(new[] { md5(stale) }), Is.False);
            Assert.That(latest.Matches(new[] { md5(current) }), Is.True);
            var local = new BeatmapSetInfo { Beatmaps = { new BeatmapInfo { MD5Hash = md5(stale) } } };
            Assert.That(BeatmapMirrorMetadata.FromModel(local), Is.Null);
        }

        [Test]
        public void MetadataValidatesSetIdentityAndMissingHashes()
        {
            Assert.That(BeatmapMirrorMetadata.Parse("mirror", 123, null), Is.Null);
            Assert.That(BeatmapMirrorMetadata.Parse("mirror", 123, JObject.Parse("{\"id\":124,\"beatmaps\":[]}")), Is.Null);
            Assert.That(BeatmapMirrorMetadata.Parse("mirror", 123, JObject.Parse("{\"id\":123,\"beatmaps\":[{\"checksum\":null}]}")), Is.Null);
        }

        [Test]
        public void HistoryRanksFreshnessThenMeasuredTransferAndHonoursBackoff()
        {
            var history = new BeatmapMirrorHistory();
            var fresh = new BeatmapMirrorSource("fresh", "https://fresh.test");
            var fast = new BeatmapMirrorSource("fast", "https://fast.test");
            var sources = new[] { fast, fresh };
            var reference = metadata("fresh", "2026-10-01T00:00:00Z", current);
            history.Record("fast", 4 * 1024 * 1024, 1, 0.1);
            history.Record("fresh", 4 * 1024 * 1024, 3, 0.2);
            Assert.That(history.Order(sources, new[] { reference }, reference, DateTimeOffset.UtcNow)[0], Is.EqualTo(fresh));
            Assert.That(history.Order(sources, [], null, DateTimeOffset.UtcNow)[0], Is.EqualTo(fast));
            history.BackOff("fast", DateTimeOffset.UtcNow.AddMinutes(1));
            Assert.That(history.Order(sources, [], null, DateTimeOffset.UtcNow)[0], Is.EqualTo(fresh));
        }

        [Test]
        public async Task StartsFourBodiesTogetherAndChoosesFullTransferNotFirstByte()
        {
            using var server = new LocalMirrorServer(archive(current));
            using var gate = new CountdownEvent(4);
            server.BeforeBody = async path =>
            {
                gate.Signal();
                await Task.Run(() => gate.Wait(TimeSpan.FromSeconds(3)));
                if (path != "/fast-body")
                    await Task.Delay(1200);
            };
            var request = createRequest(server, ["slow-header", "slow-body", "fast-body", "slow-fourth"]);
            await run(request);
            Assert.That(server.Paths.Distinct().Count(), Is.EqualTo(4));
            Assert.That(server.FirstArrivalSpread.TotalMilliseconds, Is.LessThan(1000), "Four initial downloads must not be staggered by hedge_delay.");
            Assert.That(request.Winner, Does.EndWith("/fast-body"));
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
            Assert.That(request.DownloadFiles.Count(File.Exists), Is.EqualTo(1));
            Assert.That(server.AuthorizationHeaders, Is.Empty, "Never send the Mosu token to a public mirror.");
        }

        [Test]
        public async Task FastFirstByteWithSlowBodyLosesToFastCompleteArchive()
        {
            using var server = new LocalMirrorServer(archive(current));
            server.SlowBodies.Add("/instant-headers-slow-body");
            var request = createRequest(server, ["instant-headers-slow-body", "fast-complete"], expected: current);
            await run(request);
            Assert.That(request.Winner, Does.EndWith("/fast-complete"));
        }

        [Test]
        public async Task StaleFastArchiveCannotBeatSlowerCurrentArchive()
        {
            using var server = new LocalMirrorServer(archive(current));
            server.Bodies["/stale"] = archive(stale);
            server.BeforeBody = path => Task.Delay(path == "/current" ? 300 : 0);
            var request = createRequest(server, ["stale", "current"], expected: current);
            await run(request);
            Assert.That(request.Winner, Does.EndWith("/current"));
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
            Assert.That(request.DownloadFiles.Count(File.Exists), Is.EqualTo(1));
        }

        [Test]
        public async Task AllStaleArchivesFailRatherThanSilentlyImportingOldMap()
        {
            using var server = new LocalMirrorServer(archive(stale));
            var request = createRequest(server, ["stale-one", "stale-two"], expected: current);
            await run(request);
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));
            Assert.That(request.DownloadFiles.Any(File.Exists), Is.False);
        }

        [Test]
        public async Task NotFoundAndBadZipFallThroughToReserve()
        {
            using var server = new LocalMirrorServer(archive(current));
            foreach (string source in new[] { "one", "two", "three", "four" })
                server.StatusCodes["/" + source] = 404;
            server.Bodies["/bad"] = Encoding.UTF8.GetBytes("not a zip");
            var request = createRequest(server, ["one", "two", "three", "four", "bad", "good"], expected: current);
            await run(request);
            Assert.That(request.Winner, Does.EndWith("/good"));
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
            Assert.That(request.DownloadFiles.Count(File.Exists), Is.EqualTo(1));
        }

        [Test]
        public async Task AbortedTransferDoesNotValidateDeletedTemporaryFile()
        {
            using var server = new LocalMirrorServer(archive(current));
            var request = createRequest(server, ["aborted"], expected: current);
            request.AbortOnProgress = true;
            Exception? failure = null;
            request.Failure += error => failure = error;

            await run(request);

            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));
            Assert.That(failure, Is.TypeOf<IOException>().With.Message.Contains("transfer was aborted"));
            Assert.That(request.DownloadFiles.Any(File.Exists), Is.False);
        }

        [Test]
        public async Task CancellationAbortsAllBodiesAndRemovesEveryTemporaryFile()
        {
            using var server = new LocalMirrorServer(archive(current));
            using var gate = new CountdownEvent(4);
            server.BeforeBody = async _ => { gate.Signal(); await Task.Delay(2000); };
            var request = createRequest(server, ["cancel-one", "cancel-two", "cancel-three", "cancel-four"]);
            var task = Task.Run(request.Perform);
            Assert.That(await Task.Run(() => gate.Wait(TimeSpan.FromSeconds(5))), Is.True);
            request.Cancel();
            await task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));
            Assert.That(request.DownloadFiles.Any(File.Exists), Is.False);
        }

        [Test]
        public async Task HistoricalDifficultiesInCachedCatalogueAreReconciledWithoutRedownloadingArchive()
        {
            using var server = new LocalMirrorServer(archive(current));
            var cached = metadata("Nekoha", "2026-10-01T00:00:00Z", current) with { Checksums = new[] { md5(current), md5(stale) }, Complete = false };
            BeatmapCatalogueMetadataCache.Store(123, cached);
            try
            {
                var sources = new[] { "Nekoha", "Mirria", "OsuDirect" }
                    .Select(name => new BeatmapMirrorSource(name, server.Url + name, "https://metadata.test/" + name)).ToArray();
                var request = new TestDownloadRequest(new APIBeatmapSet { OnlineID = 123 }, sources);
                request.Metadata["Nekoha"] = cached;
                request.Metadata["Mirria"] = metadata("Mirria", "2026-10-01T00:00:00Z", current);
                request.Metadata["OsuDirect"] = metadata("OsuDirect", "2026-10-01T00:00:00Z", current);
                attach(request);
                await run(request);
                Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
                Assert.That(request.MetadataRequests, Is.EqualTo(3));
                Assert.That(server.Paths.Count, Is.EqualTo(3), "Only one initial archive transfer per source.");
                Assert.That(request.DownloadFiles.Count(File.Exists), Is.EqualTo(1));
            }
            finally
            {
                BeatmapCatalogueMetadataCache.Invalidate(123);
            }
        }

        [Test]
        public void MetadataReconciliationNeedsIndependentAgreementAndNeverDowngrades()
        {
            var latest = metadata("catalogue", "2026-10-01T00:00:00Z", current);
            var old = metadata("Mirria", "2026-01-01T00:00:00Z", stale);
            Assert.That(BeatmapMirrorMetadata.Reconcile(latest, [old, old with { Source = "OsuDirect" }]), Is.Null);
            Assert.That(BeatmapMirrorMetadata.Reconcile(latest, [latest with { Source = "Mirria" }]), Is.Null);
            var archiveVotes = new[] { latest with { Source = "Nekoha" }, latest with { Source = "Mino" } };
            Assert.That(BeatmapMirrorMetadata.Reconcile(latest, [latest with { Source = "Mirria" }], archiveVotes), Is.Not.Null);
            Assert.That(BeatmapMirrorMetadata.Reconcile(latest, [], archiveVotes), Is.Null);
            Assert.That(BeatmapMirrorMetadata.Reconcile(latest, [old], [old with { Source = "Nekoha" }, old with { Source = "Mino" }]), Is.Null);
            var conflict = latest with { Checksums = new[] { md5(current), md5(stale) } };
            Assert.That(BeatmapMirrorMetadata.Reconcile(conflict, [latest with { Source = "Mirria" }, latest with { Source = "OsuDirect" }, conflict]), Is.EqualTo(latest with { Source = "Mirria" }));
        }

        [Test]
        public async Task FreshSearchMetadataAvoidsRedundantPreflight()
        {
            using var server = new LocalMirrorServer(archive(current));
            const int setId = 123;
            BeatmapCatalogueMetadataCache.Store(setId, metadata("Nekoha", "2026-10-01T00:00:00Z", current));
            try
            {
                var request = new TestDownloadRequest(new APIBeatmapSet { OnlineID = setId },
                    [new BeatmapMirrorSource("cached", server.Url + "cached", "https://metadata.test/cached")]);
                attach(request);
                await run(request);
                Assert.That(request.MetadataRequests, Is.Zero);
                Assert.That(request.Winner, Does.EndWith("/cached"));
            }
            finally
            {
                BeatmapCatalogueMetadataCache.Invalidate(setId);
            }
        }

        [Test]
        public async Task NewerMetadataSortsInitialWaveAndRejectsArchiveFromOlderCatalogue()
        {
            using var server = new LocalMirrorServer(archive(stale));
            server.Bodies["/fresh"] = archive(current);
            var model = new APIBeatmapSet
            {
                OnlineID = 123,
                LastUpdated = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                Beatmaps = [new APIBeatmap { Checksum = md5(stale) }],
            };
            var sources = new[] { "old-one", "old-two", "old-three", "old-four", "fresh" }
                          .Select(p => new BeatmapMirrorSource(p, server.Url + p, "https://metadata.test/" + p)).ToArray();
            var request = new TestDownloadRequest(model, sources);
            request.Metadata["fresh"] = metadata("fresh", "2026-10-01T00:00:00Z", current);
            attach(request);
            await run(request);
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
            Assert.That(request.Winner, Does.EndWith("/fresh"));
            Assert.That(server.Paths, Does.Contain("/fresh"));
            Assert.That(request.DownloadFiles.Count(File.Exists), Is.EqualTo(1));
        }

        [Test]
        public async Task SlowInitialOriginsDoNotPreventReserveFromWinning()
        {
            using var server = new LocalMirrorServer(archive(current));
            server.BeforeBody = path => Task.Delay(path == "/reserve" ? 0 : 3500);
            var request = createRequest(server, ["slow-a", "slow-b", "slow-c", "slow-d", "reserve"], expected: current);
            await run(request);
            Assert.That(request.Winner, Does.EndWith("/reserve"));
            Assert.That(request.DownloadFiles.Count(File.Exists), Is.EqualTo(1));
        }

        [Test]
        public async Task ManualMirrorDoesNotStartOtherSources()
        {
            using var server = new LocalMirrorServer(archive(current));
            var request = createRequest(server, ["manual"], expected: current);
            request.Mirror = DownloadMirror.Mirria;
            await run(request);
            Assert.That(request.Winner, Does.EndWith("/manual"));
            Assert.That(server.Paths.Count, Is.EqualTo(1));
        }

        [Test]
        public void CompleteReferenceRejectsExtraOutdatedDifficulties()
        {
            var reference = metadata("mirror", "2026-10-01T00:00:00Z", current);
            Assert.That(reference.Matches(new[] { md5(current), md5(stale) }), Is.False);
            Assert.That((reference with { Complete = false }).Matches(new[] { md5(current), md5(stale) }), Is.True);
        }

        [Test]
        public void AutoSourcesIncludeNewMirrorsAndNoDeadChimuOrDuplicateCatboy()
        {
            var request = new DownloadBeatmapSetRequest(new APIBeatmapSet { OnlineID = 123 }, true);
            var api = new Mock<IAPIProvider>();
            api.SetupGet(a => a.Endpoints).Returns(new EndpointConfiguration { APIUrl = "https://mosu.test" });
            request.AttachAPI(api.Object);
            var sources = request.GetMirrors();
            Assert.That(sources.Select(s => s.Name), Does.Contain("Nekoha").And.Contain("Mirria").And.Not.Contain("Chimu"));
            Assert.That(sources.Count(s => s.Url.Contains("catboy.best", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(sources.Single(s => s.Name == "OsuDirect").Url, Is.EqualTo("https://osu.direct/api/d/123?noVideo=1"));
            Assert.That(sources.Single(s => s.Name == "Mirria").Url, Does.EndWith("?video=false"));
        }

        [Test, Explicit("Live mirror smoke test; performs a single bounded set download over the internet.")]
        public async Task LiveAutoDownloadMatchesKnownRawDifficultyHashes()
        {
            string[] expected =
            [
                "614b67433809741b2df426428ffd2ecd", "66bbd45c8337b199dc03008053fcfd8f", "95575ff0982208675b2a2900cf520f47",
                "d890dec1f84c8183350fcb6a40660a53", "fcb320b8ad91659dbe38bce53c208589",
            ];
            var model = new APIBeatmapSet
            {
                OnlineID = 992499,
                LastUpdated = DateTimeOffset.Parse("2026-01-31T15:44:27Z"),
                Beatmaps = expected.Select(hash => new APIBeatmap { Checksum = hash }).ToArray(),
            };
            var request = new DownloadBeatmapSetRequest(model, true);
            var api = new Mock<IAPIProvider>();
            api.SetupGet(a => a.Endpoints).Returns(new EndpointConfiguration { APIUrl = "https://morasooma.net" });
            api.Setup(a => a.Schedule(It.IsAny<Action>())).Callback<Action>(action => action());
            request.AttachAPI(api.Object);
            string? downloaded = null;
            request.Success += file => { downloaded = file; files.Add(file); };
            var clock = Stopwatch.StartNew();
            await Task.Run(request.Perform).WaitAsync(TimeSpan.FromSeconds(40));
            Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
            Assert.That(downloaded, Is.Not.Null);
            Assert.That(BeatmapMirrorArchive.Inspect(downloaded!, CancellationToken.None), Is.EquivalentTo(expected));
            TestContext.Out.WriteLine($"Live Auto download and full archive validation: {clock.ElapsedMilliseconds} ms, {new FileInfo(downloaded!).Length} bytes");
        }

        [Test, Explicit("Reproduce the reported set with Nekoha's historical difficulty catalogue, then validate a real archive.")]
        public async Task LiveReportedSetReconcilesHistoricalCatalogue()
        {
            const int id = 2578022;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var metadata = await BeatmapMirrorMetadata.FetchAsync(new BeatmapMirrorSource("Nekoha", "", $"https://mirror.nekoha.moe/api/beatmapset/{id}"), id, timeout.Token);
            Assert.That(metadata, Is.Not.Null);
            BeatmapCatalogueMetadataCache.Store(id, metadata! with { Complete = false });
            try
            {
                var request = new DownloadBeatmapSetRequest(new APIBeatmapSet { OnlineID = id }, true);
                var api = new Mock<IAPIProvider>();
                api.SetupGet(a => a.Endpoints).Returns(new EndpointConfiguration { APIUrl = "https://morasooma.net" });
                api.Setup(a => a.Schedule(It.IsAny<Action>())).Callback<Action>(action => action());
                request.AttachAPI(api.Object);
                string? downloaded = null;
                request.Success += file => { downloaded = file; files.Add(file); };
                await Task.Run(request.Perform).WaitAsync(TimeSpan.FromSeconds(60));
                Assert.That(request.CompletionState, Is.EqualTo(APIRequestCompletionState.Completed));
                Assert.That(downloaded, Is.Not.Null);
                Assert.That(BeatmapMirrorArchive.Inspect(downloaded!, CancellationToken.None, id), Is.EqualTo(new[] { "60f0a1ba4eb8a0c2fb3594c973fbcb5e" }));
                TestContext.Out.WriteLine($"Reported set validated: {new FileInfo(downloaded!).Length} bytes, historical catalogue hashes={metadata!.Checksums.Count}");
            }
            finally
            {
                BeatmapCatalogueMetadataCache.Invalidate(id);
            }
        }

        private TestDownloadRequest createRequest(LocalMirrorServer server, string[] paths, byte[]? expected = null)
        {
            var model = new APIBeatmapSet { OnlineID = 123 };
            if (expected != null)
            {
                model.LastUpdated = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
                model.Beatmaps = [new APIBeatmap { Checksum = md5(expected) }];
            }
            var request = new TestDownloadRequest(model, paths.Select(p => new BeatmapMirrorSource(p, server.Url + p)).ToArray());
            attach(request);
            return request;
        }

        private void attach(TestDownloadRequest request)
        {
            var api = new Mock<IAPIProvider>();
            api.Setup(a => a.Schedule(It.IsAny<Action>())).Callback<Action>(action => action());
            request.AttachAPI(api.Object);
            request.Success += file => files.Add(file);
        }

        private static async Task run(TestDownloadRequest request) => await Task.Run(request.Perform).WaitAsync(TimeSpan.FromSeconds(10));

        private string save(byte[] bytes)
        {
            string file = Path.Combine(Path.GetTempPath(), $"mosu-mirror-test-{Guid.NewGuid():N}.osz");
            File.WriteAllBytes(file, bytes);
            files.Add(file);
            return file;
        }

        private static string md5(byte[] bytes) => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
        private static BeatmapMirrorMetadata metadata(string source, string updated, byte[] bytes) => new BeatmapMirrorMetadata(source, DateTimeOffset.Parse(updated), [md5(bytes)]);

        private static byte[] archive(byte[] difficulty, string name = "map.osu", string? extra = null)
        {
            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                using (var entry = zip.CreateEntry(name, CompressionLevel.NoCompression).Open())
                    entry.Write(difficulty);
                if (extra != null)
                {
                    using var entry = zip.CreateEntry("audio.mp3", CompressionLevel.NoCompression).Open();
                    entry.Write(Encoding.UTF8.GetBytes(extra));
                }
            }
            return output.ToArray();
        }

        private sealed class TestDownloadRequest(APIBeatmapSet model, BeatmapMirrorSource[] sources) : DownloadBeatmapSetRequest(model, true)
        {
            internal readonly ConcurrentBag<string> DownloadFiles = new ConcurrentBag<string>();
            internal string? Winner;
            internal bool AbortOnProgress;
            internal readonly Dictionary<string, BeatmapMirrorMetadata> Metadata = new Dictionary<string, BeatmapMirrorMetadata>();
            internal override IReadOnlyList<BeatmapMirrorSource> GetMirrors() => sources;
            internal int MetadataRequests;
            internal override Task<BeatmapMirrorMetadata?> FetchMirrorMetadataAsync(BeatmapMirrorSource source, CancellationToken cancellationToken)
            {
                MetadataRequests++;
                return Task.FromResult(Metadata.GetValueOrDefault(source.Name));
            }

            internal override FileWebRequest CreateMirrorRequest(string filename, BeatmapMirrorSource source)
            {
                DownloadFiles.Add(filename);
                var request = new MirrorFileWebRequest(filename, source.Url) { AllowInsecureRequests = true };
                if (AbortOnProgress)
                    request.DownloadProgress += (_, _) => request.Abort();
                return request;
            }

            // Observe the successful network request, not a fixture-specific selection algorithm.
            public override void Perform()
            {
                Success += _ => Winner = WebRequest?.Url;
                base.Perform();
            }
        }

        private sealed class LocalMirrorServer : IDisposable
        {
            private readonly HttpListener listener = new HttpListener();
            private readonly Task acceptTask;
            private readonly ConcurrentBag<Task> handlers = new ConcurrentBag<Task>();
            private readonly byte[] body;
            private readonly ConcurrentBag<long> arrivals = new ConcurrentBag<long>();
            internal readonly ConcurrentBag<string> Paths = new ConcurrentBag<string>();
            internal readonly ConcurrentBag<string> AuthorizationHeaders = new ConcurrentBag<string>();
            internal readonly ConcurrentDictionary<string, byte[]> Bodies = new ConcurrentDictionary<string, byte[]>();
            internal readonly ConcurrentDictionary<string, int> StatusCodes = new ConcurrentDictionary<string, int>();
            internal readonly HashSet<string> SlowBodies = new HashSet<string>();
            internal Func<string, Task>? BeforeBody;
            internal string Url { get; }
            internal TimeSpan FirstArrivalSpread => TimeSpan.FromSeconds((arrivals.Max() - arrivals.Min()) / (double)Stopwatch.Frequency);

            internal LocalMirrorServer(byte[] body)
            {
                this.body = body;
                var port = new TcpListener(IPAddress.Loopback, 0);
                port.Start();
                int number = ((IPEndPoint)port.LocalEndpoint).Port;
                port.Stop();
                Url = $"http://127.0.0.1:{number}/";
                listener.Prefixes.Add(Url);
                listener.Start();
                acceptTask = accept();
            }

            private async Task accept()
            {
                try
                {
                    while (listener.IsListening)
                        handlers.Add(handle(await listener.GetContextAsync()));
                }
                catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { }
            }

            private async Task handle(HttpListenerContext context)
            {
                try
                {
                    string path = context.Request.Url!.AbsolutePath;
                    arrivals.Add(Stopwatch.GetTimestamp());
                    Paths.Add(path);
                    if (context.Request.Headers["Authorization"] is { } auth)
                        AuthorizationHeaders.Add(auth);
                    context.Response.StatusCode = StatusCodes.GetValueOrDefault(path, 200);
                    byte[] bytes = Bodies.GetValueOrDefault(path, body);
                    context.Response.ContentLength64 = bytes.Length;
                    // Flush headers before delaying the body: this models a misleadingly fast HTTP response.
                    await context.Response.OutputStream.FlushAsync();
                    if (BeforeBody != null)
                        await BeforeBody(path);
                    if (SlowBodies.Contains(path))
                    {
                        await context.Response.OutputStream.WriteAsync(bytes.AsMemory(0, 4));
                        await context.Response.OutputStream.FlushAsync();
                        await Task.Delay(1500);
                        await context.Response.OutputStream.WriteAsync(bytes.AsMemory(4));
                    }
                    else
                        await context.Response.OutputStream.WriteAsync(bytes);
                }
                catch (Exception e) when (e is IOException or HttpListenerException or ObjectDisposedException) { }
                finally { context.Response.Close(); }
            }

            public void Dispose()
            {
                listener.Close();
                acceptTask.GetAwaiter().GetResult();
                Task.WhenAll(handlers).GetAwaiter().GetResult();
            }
        }
    }
}
