using System;
using System.IO;
using System.Threading;
using System.Net.Http;
using System.Net;
using System.IO.Compression;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using System.Security.Cryptography;
using osu.Game.Graphics.Backgrounds.AI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class DirectBackgroundPackageTest
    {
        [Test]
        public void TestPinnedSourcesAreOriginalAndNotMosuPublication()
        {
            foreach (var source in DirectBackgroundPackage.Models)
            {
                Assert.That(new Uri(source.Url).Host, Is.EqualTo("huggingface.co"));
                Assert.That(source.Sha256, Has.Length.EqualTo(64));
                Assert.That(source.Url, Does.Not.Contain("/main/"));
            }
            Assert.That(new Uri(DirectBackgroundPackage.Runtime.Url).Host, Is.EqualTo("github.com"));
        }

        [Test]
        public void TestSpaceCheckUsesContainingFilesystem()
        {
            // MOSU_BACKGROUND_SPACE_TEST_PATH can point at a real low-space mounted volume.
            // This test only queries space and never writes to that volume.
            string path = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_SPACE_TEST_PATH") ?? Path.GetTempPath();
            long available = new DriveInfo(path).AvailableFreeSpace;
            if (available < DirectBackgroundPackage.REQUIRED_PREPARATION_BYTES)
            {
                var error = Assert.Throws<DirectBackgroundPackage.InsufficientSpaceException>(() => DirectBackgroundPackage.CheckPreparationSpace(path));
                Assert.That(error!.Message, Does.Contain(Path.GetFullPath(path)).And.Contain(available.ToString()));
            }
            else
                Assert.DoesNotThrow(() => DirectBackgroundPackage.CheckPreparationSpace(path));
        }

        [Test]
        public async Task TestCorruptedDownloadAndInstallAreRejected()
        {
            using var storage = new TemporaryNativeStorage("background-source-checks-" + Guid.NewGuid().ToString("N"));
            byte[] data = { 1, 2, 3, 4 };
            using var handler = new PayloadHandler(data);
            using var client = new HttpClient(handler);
            var source = new DirectBackgroundPackage.Source("weights", "https://original.example/weights", data.Length, new string('0', 64));
            string path = storage.GetFullPath("weights");
            Assert.ThrowsAsync<InvalidDataException>(async () => await DirectBackgroundPackage.Download(client, source, path, CancellationToken.None));
            source = source with { Sha256 = Convert.ToHexString(SHA256.HashData(data)) };
            await DirectBackgroundPackage.Download(client, source, path, CancellationToken.None);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(data));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await DirectBackgroundPackage.Download(client, source, path, cancelled.Token));
            string installed = storage.GetFullPath("installed"); Directory.CreateDirectory(installed);
            Assert.That(await DirectBackgroundPackage.VerifyInstall(installed, CancellationToken.None), Is.False);
            string[] names = { DirectBackgroundPackage.Executable, "unet.safetensors", "clip.gguf", "tae.safetensors" };
            foreach (string name in names) File.WriteAllBytes(Path.Combine(installed, name), data);
            var assets = names.Select(n => new StillLifePackage.Asset(n, data.Length, source.Sha256)).ToArray();
            File.WriteAllText(Path.Combine(installed, "direct-manifest.json"), JsonSerializer.Serialize(assets));
            Assert.That(await DirectBackgroundPackage.VerifyInstall(installed, CancellationToken.None), Is.True);
            File.WriteAllBytes(Path.Combine(installed, "unet.safetensors"), new byte[] { 9 });
            Assert.That(await DirectBackgroundPackage.VerifyInstall(installed, CancellationToken.None), Is.False);
        }

        [Test]
        public async Task TestDownloadRetriesReportSourceAndKeepInnerError()
        {
            using var storage = new TemporaryNativeStorage("background-network-error-" + Guid.NewGuid().ToString("N"));
            using var handler = new FailingHandler();
            using var client = new HttpClient(handler);
            var source = new DirectBackgroundPackage.Source("runtime", "https://github.com/original/runtime.zip", 1, new string('0', 64));
            var error = Assert.ThrowsAsync<DirectBackgroundPackage.DownloadException>(async () =>
                await DirectBackgroundPackage.Download(client, source, storage.GetFullPath("runtime"), CancellationToken.None));
            Assert.That(handler.Requests, Is.EqualTo(3));
            Assert.That(error!.Host, Is.EqualTo("github.com"));
            Assert.That(error.InnerException, Is.TypeOf<HttpRequestException>());
            await Task.CompletedTask;
        }

        private sealed class FailingHandler : HttpMessageHandler
        {
            public int Requests;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests++;
                throw new HttpRequestException("Simulated connection failure");
            }
        }

        [Test]
        public void TestRuntimeExtractionRejectsTraversal()
        {
            using var storage = new TemporaryNativeStorage("background-runtime-path-" + Guid.NewGuid().ToString("N"));
            using var data = new MemoryStream();
            using (var create = new ZipArchive(data, ZipArchiveMode.Create, leaveOpen: true))
            {
                using var entry = create.CreateEntry("../evil.dll").Open(); entry.WriteByte(1);
            }
            data.Position = 0;
            using var archive = new ZipArchive(data, ZipArchiveMode.Read);
            Assert.ThrowsAsync<InvalidDataException>(async () => await DirectBackgroundPackage.ExtractRuntime(archive, storage.GetFullPath(string.Empty), CancellationToken.None));
        }

        private sealed class PayloadHandler(byte[] data) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });
        }

        [Test]
        [Explicit("Downloads original model files and prepares them locally; MOSU_BACKGROUND_INSTALL required.")]
        public async Task TestInstallDirectFromPublishersThenOffline()
        {
            string path = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_INSTALL") ?? throw new InvalidOperationException("MOSU_BACKGROUND_INSTALL required");
            var package = new DirectBackgroundPackage(path);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
            string installed = await package.Ensure(timeout.Token);
            Assert.That(await DirectBackgroundPackage.VerifyInstall(installed, timeout.Token), Is.True);
            using var offline = new OfflineHandler();
            Assert.That(await new DirectBackgroundPackage(path, offline).Ensure(timeout.Token), Is.EqualTo(installed));
            Assert.That(new DirectoryInfo(installed).GetFiles().Sum(f => f.Length), Is.LessThan(500_000_000));
            Assert.That(Directory.GetDirectories(path, ".prepare-*"), Is.Empty);
            TestContext.Out.WriteLine("Prepared installation: " + installed);
        }

        [Test]
        [Explicit("Real installed runtime and client service integration; MOSU_BACKGROUND_INSTALL required.")]
        public async Task TestRealServiceAndPersistedCache()
        {
            string root = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_INSTALL") ?? throw new InvalidOperationException("MOSU_BACKGROUND_INSTALL required");
            string installed = await new DirectBackgroundPackage(root).Ensure(CancellationToken.None);
            using var storage = new TemporaryNativeStorage("background-distortion-service-" + Guid.NewGuid().ToString("N"));
            string destination = storage.GetFullPath(Path.Combine("ai-models/background-distortion", DirectBackgroundPackage.VERSION, StillLifePackage.Platform));
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(installed)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            using var config = new OsuConfigManager(storage);
            Assert.That(config.Get<bool>(OsuSetting.ForkAiStillLife), Is.False);
            config.SetValue(OsuSetting.ForkAiStillLife, true);
            config.SetValue(OsuSetting.ForkAiStillLifeSize, StillLifeResolution.Low);
            config.SetValue(OsuSetting.ForkAiStillLifePasses, 1);
            byte[] png;
            using (var image = new Image<Rgb24>(256, 160, new Rgb24(30, 80, 120)))
            using (var output = new MemoryStream()) { image.SaveAsPng(output); png = output.ToArray(); }
            string hash = Convert.ToHexString(SHA256.HashData(png));
            byte[]? expected = null;
            for (int restart = 0; restart < 2; restart++)
            {
                var service = new StillLifeService();
                service.Initialise(config, storage);
                if (restart == 1) service.GenerateForTest = (_, _, _) => throw new AssertionException("Persisted cache hit ran inference.");
                try
                {
                    var result = await service.RequestBackground(() => (hash, new MemoryStream(png)), "alias", CancellationToken.None, true)
                        .WaitAsync(TimeSpan.FromMinutes(2));
                    Assert.That(result, Is.Not.Null);
                    Assert.That(result!.Width, Is.EqualTo(256));
                    Assert.That(result.Height, Is.EqualTo(160));
                    if (restart == 0) { expected = result.Rgb; Assert.That(service.GeneratedCount, Is.EqualTo(1)); }
                    else { Assert.That(result.Rgb, Is.EqualTo(expected)); Assert.That(service.DiskCacheHits, Is.EqualTo(1)); }
                }
                finally { service.Dispose(); Assert.That(SpinWait.SpinUntil(() => service.WorkerStopped, 10000), Is.True); }
            }
        }

        private sealed class OfflineHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new AssertionException("Installed package attempted a network request.");
        }

        [Test]
        [Explicit("Cold and warm real img2img timings; MOSU_BACKGROUND_INSTALL and MOSU_BACKGROUND_IMAGE required.")]
        public async Task TestColdGpuImageToImage()
        {
            string root = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_INSTALL") ?? throw new InvalidOperationException("MOSU_BACKGROUND_INSTALL required");
            string source = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_IMAGE") ?? throw new InvalidOperationException("MOSU_BACKGROUND_IMAGE required");
            string directory = await new DirectBackgroundPackage(root).Ensure(CancellationToken.None);
            var options = new StillLifeOptions(true, StillLifeBackend.Gpu, StillLifeResolution.Medium, 3, 1, .17, 12345, 4, true, 64);
            using var token = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var image = Image.Load<Rgb24>(source);
            var timer = Stopwatch.StartNew();
            using var engine = new StillLifeEngine(directory, options, token.Token);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var result = engine.Generate(image, options, token.Token, () => false);
                TestContext.Out.WriteLine($"GPU 384 three passes, {(repeat == 0 ? "cold including engine startup" : "warm")}: {timer.Elapsed.TotalSeconds:F2}s");
                using var output = Image.LoadPixelData<Rgb24>(result.Rgb, result.Width, result.Height);
                output.SaveAsPng(Path.Combine(root, $"verified-coldwarm-{repeat}.png"));
                timer.Restart();
            }
        }

        [Test]
        [Explicit("Real process cancellation; MOSU_BACKGROUND_INSTALL required.")]
        public async Task TestNativeCancellationStopsPersistentWorker()
        {
            string root = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_INSTALL") ?? throw new InvalidOperationException("MOSU_BACKGROUND_INSTALL required");
            string directory = await new DirectBackgroundPackage(root).Ensure(CancellationToken.None);
            var options = new StillLifeOptions(true, StillLifeBackend.Cpu, StillLifeResolution.Medium, 3, 1, .17, 12345, 4, true, 64);
            using (var engine = new StillLifeEngine(directory, options, CancellationToken.None))
            using (var image = new Image<Rgb24>(384, 384, new Rgb24(220, 20, 20)))
            using (var token = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
            {
                Assert.Catch<OperationCanceledException>(() => engine.Generate(image, options, token.Token, () => false));
            }
            Assert.That(Directory.GetDirectories(directory, ".worker-*"), Is.Empty);
        }

        [TestCase(StillLifeBackend.Cpu)]
        [TestCase(StillLifeBackend.Gpu)]
        [Explicit("Real directly installed runtime; MOSU_BACKGROUND_INSTALL required.")]
        public async Task TestDirectNativeEngine(StillLifeBackend backend)
        {
            string root = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_INSTALL") ?? throw new InvalidOperationException("MOSU_BACKGROUND_INSTALL required");
            string directory = await new DirectBackgroundPackage(root).Ensure(CancellationToken.None);
            var options = new StillLifeOptions(true, backend, StillLifeResolution.Low, 2, 1, .45, 12345, 4, true, 64);
            using var token = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var engine = new StillLifeEngine(directory, options, token.Token);
            string? source = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_IMAGE");
            using var image = source == null ? new Image<Rgb24>(256, 160, new Rgb24(30, 80, 120)) : Image.Load<Rgb24>(source);
            var result = engine.Generate(image, options, token.Token, () => false);
            Assert.That(result.Width, Is.EqualTo(256));
            Assert.That(result.Height, Is.GreaterThanOrEqualTo(32).And.LessThanOrEqualTo(256));
            Assert.That(result.Rgb, Has.Length.EqualTo(result.Width * result.Height * 3));
            using var output = Image.LoadPixelData<Rgb24>(result.Rgb, result.Width, result.Height);
            output.SaveAsPng(Path.Combine(root, "test-" + backend + ".png"));
            var faithful = options with { Passes = 1, Strength = .10 };
            using var red = new Image<Rgb24>(256, 160, new Rgb24(220, 20, 20));
            using var blue = new Image<Rgb24>(256, 160, new Rgb24(20, 20, 220));
            var first = engine.Generate(red, faithful, token.Token, () => false);
            var second = engine.Generate(blue, faithful, token.Token, () => false);
            Assert.That(first.Rgb, Is.Not.EqualTo(second.Rgb), "Initial image must affect output for identical prompt, seed and sigmas.");
            double RedMinusBlue(byte[] pixels) => Enumerable.Range(0, pixels.Length / 3).Average(i => pixels[i * 3] - pixels[i * 3 + 2]);
            Assert.That(RedMinusBlue(first.Rgb), Is.GreaterThan(80), "Low-strength img2img must preserve the red source.");
            Assert.That(RedMinusBlue(second.Rgb), Is.LessThan(-80), "Low-strength img2img must preserve the blue source.");
            var benchmark = options with { Resolution = StillLifeResolution.Medium, Passes = 3, Strength = .17 };
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var timer = Stopwatch.StartNew();
                var measured = engine.Generate(image, benchmark, token.Token, () => false);
                TestContext.Out.WriteLine($"{backend} three passes repeat {repeat}: {timer.Elapsed.TotalSeconds:F2}s");
                using var measuredImage = Image.LoadPixelData<Rgb24>(measured.Rgb, measured.Width, measured.Height);
                measuredImage.SaveAsPng(Path.Combine(root, $"img2img-{backend}-{repeat}.png"));
            }
        }
    }
}
