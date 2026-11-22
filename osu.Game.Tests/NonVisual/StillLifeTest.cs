using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Logging;
using osu.Game.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds.AI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public partial class StillLifeTest
    {
        [Test]
        public void TestDiskCacheRestartCorruptionAndLimit()
        {
            using var storage = new TemporaryNativeStorage("prepared-artwork-" + Guid.NewGuid().ToString("N"));
            string path = storage.GetFullPath("artwork");
            var cache = new BackgroundImageCache(path);
            var rgb = new byte[64 * 32 * 3];
            new Random(42).NextBytes(rgb);
            cache.Write("hash:policy", new BackgroundImageCache.Artwork(rgb, 64, 32), 100000);
            var reopened = new BackgroundImageCache(path);
            Assert.That(reopened.Read("hash:policy")!.Rgb, Is.EqualTo(rgb));
            Assert.That(reopened.Read("hash:other-policy"), Is.Null);
            File.WriteAllBytes(Directory.GetFiles(path).Single(), new byte[] { 1, 2, 3 });
            Assert.That(reopened.Read("hash:policy"), Is.Null);
            Assert.That(Directory.GetFiles(path), Is.Empty);
            for (int i = 0; i < 4; i++)
                cache.Write("image" + i, new BackgroundImageCache.Artwork(rgb, 64, 32), 15000);
            Assert.That(new DirectoryInfo(path).GetFiles().Sum(f => f.Length), Is.LessThanOrEqualTo(15000));
            reopened.Clear();
            Assert.That(Directory.GetFiles(path), Is.Empty);
        }

        [Test]
        public async Task TestPackageManifestAndBoundedDownloads()
        {
            var manifest = new StillLifePackage.Manifest(StillLifePackage.VERSION, StillLifePackage.Platform,
                new[] { StillLifePackage.WorkerFile, "unet.safetensors", "encoder.onnx", "decoder.onnx", "embeddings.bin" }
                    .Select(name => new StillLifePackage.Asset(name, 1, new string('a', 64))).ToArray());
            Assert.DoesNotThrow(() => StillLifePackage.Validate(manifest));
            Assert.Throws<InvalidDataException>(() => StillLifePackage.Validate(manifest with { Files = manifest.Files.Append(new StillLifePackage.Asset("../escape", 1, new string('a', 64))).ToArray() }));
            Assert.Throws<InvalidDataException>(() => StillLifePackage.Validate(manifest with { Files = manifest.Files.Append(manifest.Files[0]).ToArray() }));
            Assert.Throws<InvalidDataException>(() => StillLifePackage.Validate(manifest with { Files = manifest.Files.Select(f => f with { Bytes = 100_000_000 }).ToArray() }));
            using var source = new MemoryStream(new byte[4096]);
            using var output = new MemoryStream();
            Assert.ThrowsAsync<InvalidDataException>(async () => await StillLifePackage.CopyBounded(source, output, 1000, CancellationToken.None));
            using var storage = new TemporaryNativeStorage("missing-still-life-" + Guid.NewGuid().ToString("N"));
            var package = new StillLifePackage(storage.GetFullPath("models"));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await package.Ensure(string.Empty, CancellationToken.None));
            await Task.CompletedTask;
        }

        [Test]
        public async Task TestAutomaticInstallVerificationRepairAndNoRedownload()
        {
            using var storage = new TemporaryNativeStorage("still-life-install-" + Guid.NewGuid().ToString("N"));
            byte[] contents = { 1, 2, 3, 4 };
            var assets = new[] { StillLifePackage.WorkerFile, "unet.safetensors", "encoder.onnx", "decoder.onnx", "embeddings.bin" }
                .Select(name => new StillLifePackage.Asset(name, contents.Length, Convert.ToHexString(SHA256.HashData(contents)))).ToArray();
            var manifest = new StillLifePackage.Manifest(StillLifePackage.VERSION, StillLifePackage.Platform, assets);
            using var handler = new PackageHandler();
            handler.Files["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest);
            foreach (var asset in assets) handler.Files[asset.Path] = contents;
            string root = storage.GetFullPath("models");
            var package = new StillLifePackage(root, handler);
            string installed = await package.Ensure("https://packages.example/PLATFORM/manifest.json", CancellationToken.None);
            Assert.That(handler.Requests, Is.EqualTo(6));
            Assert.That(File.ReadAllBytes(Path.Combine(installed, "unet.safetensors")), Is.EqualTo(contents));
            Assert.That(await package.Ensure(string.Empty, CancellationToken.None), Is.EqualTo(installed));
            Assert.That(handler.Requests, Is.EqualTo(6), "An installed package must work offline without HTTP calls.");
            File.WriteAllBytes(Path.Combine(installed, "unet.safetensors"), new byte[] { 9 });
            await package.Ensure("https://packages.example/PLATFORM/manifest.json", CancellationToken.None);
            Assert.That(handler.Requests, Is.EqualTo(12));
            File.Delete(Path.Combine(installed, "encoder.onnx"));
            handler.Files["unet.safetensors"] = new byte[] { 9, 9, 9, 9 };
            Assert.ThrowsAsync<InvalidDataException>(async () => await package.Ensure("https://packages.example/PLATFORM/manifest.json", CancellationToken.None));
            Assert.That(Directory.GetDirectories(root, ".install-*"), Is.Empty, "Failed installs must clean up staging files.");
            Assert.That(File.ReadAllBytes(Path.Combine(installed, "unet.safetensors")), Is.EqualTo(contents), "A failed replacement must retain the previous installation.");
        }

        private sealed class PackageHandler : HttpMessageHandler
        {
            public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
            public int Requests;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests++;
                Assert.That(request.RequestUri!.Scheme, Is.EqualTo("https"));
                string name = Path.GetFileName(request.RequestUri.AbsolutePath);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Files[name]) });
            }
        }

        [Test]
        public async Task TestCacheSurvivesWorkerRestartWithoutModelsAndPolicyInvalidation()
        {
            using var storage = new TemporaryNativeStorage("still-life-cache-" + Guid.NewGuid().ToString("N"));
            using var config = new OsuConfigManager(storage);
            Assert.That(config.Get<bool>(OsuSetting.ForkAiStillLife), Is.False);
            config.SetValue(OsuSetting.ForkAiStillLife, true);
            byte[] png;
            using (var image = new Image<Rgb24>(64, 32, new Rgb24(20, 30, 40)))
            using (var bytes = new MemoryStream())
            {
                image.SaveAsPng(bytes);
                png = bytes.ToArray();
            }
            string hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
            var artwork = new BackgroundImageCache.Artwork(new byte[64 * 32 * 3], 64, 32);
            int generated = 0;
            StillLifeService Create()
            {
                var service = new StillLifeService();
                service.Initialise(config, storage);
                service.GenerateForTest = (_, _, token) => { token.ThrowIfCancellationRequested(); Interlocked.Increment(ref generated); return artwork; };
                return service;
            }
            var first = Create();
            try
            {
                // Cards must never launch generation by themselves.
                var card = await first.RequestBackground(() => (hash, new MemoryStream(png)), "alias", CancellationToken.None, false).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.That(card, Is.Null);
                Assert.That(generated, Is.Zero);
                var result = await first.RequestBackground(() => (hash, new MemoryStream(png)), "alias", CancellationToken.None, true).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.That(result, Is.SameAs(artwork));
                Assert.That(first.TryGet("alias", out _), Is.True);
                Assert.That(generated, Is.EqualTo(1));
            }
            finally { first.Dispose(); Assert.That(SpinWait.SpinUntil(() => first.WorkerStopped, 10000), Is.True); }
            var second = Create();
            second.GenerateForTest = (_, _, _) => throw new AssertionException("A persisted cache hit attempted inference.");
            try
            {
                var result = await second.RequestBackground(() => (hash, new MemoryStream(png)), "alias", CancellationToken.None, true).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.That(result!.Rgb, Is.EqualTo(artwork.Rgb));
                Assert.That(second.DiskCacheHits, Is.EqualTo(1));
                config.SetValue(OsuSetting.ForkAiStillLifeBackend, StillLifeBackend.Gpu);
                Assert.That(second.TryGet("alias", out _), Is.False);
                Assert.That(Directory.Exists(storage.GetFullPath("ai-models")), Is.False);
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                Assert.That(second.RequestBackground(() => throw new AssertionException("Cancelled request opened source."), "new", cancelled.Token, true).IsCanceled, Is.True);
                second.ClearCache();
            }
            finally { second.Dispose(); Assert.That(SpinWait.SpinUntil(() => second.WorkerStopped, 10000), Is.True); }
        }

        [Test]
        public async Task TestRetryDelayDoesNotLogAgainOrReplaceFailureStatus()
        {
            using var storage = new TemporaryNativeStorage("still-life-retry-" + Guid.NewGuid().ToString("N"));
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkAiStillLife, true);
            var service = new StatusTestService();
            service.Initialise(config, storage);
            int attempts = 0;
            int errors = 0;
            service.GenerateForTest = (_, _, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new DirectBackgroundPackage.InsufficientSpaceException(storage.GetFullPath(string.Empty), 100);
            };
            void capture(LogEntry entry)
            {
                if (entry.Message.Contains("Background distortion unavailable", StringComparison.Ordinal))
                    Interlocked.Increment(ref errors);
            }
            Logger.Flush();
            Logger.NewEntry += capture;
            try
            {
                for (int i = 0; i < 3; i++)
                    Assert.That(await service.RequestBackground(() => ("image", new MemoryStream(new byte[] { 1 })), "alias", CancellationToken.None, true)
                        .WaitAsync(TimeSpan.FromSeconds(10)), Is.Null);
                // A card-only cache miss must not overwrite the selected background's failure either.
                Assert.That(await service.RequestBackground(() => ("other", new MemoryStream(new byte[] { 1 })), "other", CancellationToken.None, false)
                    .WaitAsync(TimeSpan.FromSeconds(10)), Is.Null);
                Assert.That(SpinWait.SpinUntil(() =>
                {
                    service.UpdateStatus();
                    return service.Status.Value.Equals(AiFunctionsStrings.StillLifeInsufficientSpace);
                }, 10000), Is.True);
                Logger.Flush();
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(errors, Is.EqualTo(1), "Retry cooldown must not emit new errors.");
            }
            finally
            {
                Logger.NewEntry -= capture;
                service.Dispose();
                Assert.That(SpinWait.SpinUntil(() => service.WorkerStopped, 10000), Is.True);
            }
        }

        private partial class StatusTestService : StillLifeService
        {
            public void UpdateStatus() => Scheduler.Update();
        }

        [Test]
        public async Task TestChangingBackendCancelsActiveGenerationAndClearWorksWhileDisabled()
        {
            using var storage = new TemporaryNativeStorage("still-life-cancel-" + Guid.NewGuid().ToString("N"));
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkAiStillLife, true);
            using var entered = new ManualResetEventSlim();
            var service = new StillLifeService();
            service.Initialise(config, storage);
            service.GenerateForTest = (_, _, token) =>
            {
                entered.Set();
                if (!token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)))
                    throw new AssertionException("Active generation was not cancelled.");
                token.ThrowIfCancellationRequested();
                throw new AssertionException("Cancelled generation continued.");
            };
            try
            {
                var pending = service.RequestBackground(() => ("image", new MemoryStream(new byte[] { 1 })), "alias", CancellationToken.None, true);
                Assert.That(entered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                config.SetValue(OsuSetting.ForkAiStillLifeBackend, StillLifeBackend.Gpu);
                Assert.CatchAsync<OperationCanceledException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
                service.GenerateForTest = (_, options, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    Assert.That(options.Backend, Is.EqualTo(StillLifeBackend.Gpu));
                    return new BackgroundImageCache.Artwork(new byte[3], 1, 1);
                };
                Assert.That(await service.RequestBackground(() => ("image", new MemoryStream(new byte[] { 1 })), "alias", CancellationToken.None, true)
                    .WaitAsync(TimeSpan.FromSeconds(10)), Is.Not.Null);
                config.SetValue(OsuSetting.ForkAiStillLife, false);
                service.ClearCache();
                string directory = storage.GetFullPath("ai-background-cache/still-life");
                Assert.That(SpinWait.SpinUntil(() => Directory.GetFiles(directory, "*.rgb").Length == 0, 10000), Is.True);
                Assert.That(service.TryGet("alias", out _), Is.False);
            }
            finally
            {
                service.Dispose();
                Assert.That(SpinWait.SpinUntil(() => service.WorkerStopped, 10000), Is.True);
            }
        }

        [Test]
        [Explicit("Real local package validation; supply MOSU_STILL_LIFE_TEST_PACKAGE.")]
        public async Task TestRealPackageCanBeLoadedWithoutAnyDownload()
        {
            string source = Environment.GetEnvironmentVariable("MOSU_STILL_LIFE_TEST_PACKAGE") ?? string.Empty;
            Assert.That(Directory.Exists(source), Is.True);
            using var storage = new TemporaryNativeStorage("still-life-real-install-" + Guid.NewGuid().ToString("N"));
            string root = storage.GetFullPath("ai-models");
            string target = Path.Combine(root, StillLifePackage.VERSION, StillLifePackage.Platform);
            Directory.CreateDirectory(target);
            foreach (string path in Directory.GetFiles(source))
                File.Copy(path, Path.Combine(target, Path.GetFileName(path)));
            Assert.That(await new StillLifePackage(root).Ensure(string.Empty, CancellationToken.None), Is.EqualTo(target));
            Assert.That(new DirectoryInfo(target).GetFiles().Sum(f => f.Length), Is.LessThanOrEqualTo(StillLifePackage.MAXIMUM_BYTES));
        }

        [TestCase(StillLifeBackend.Cpu)]
        [TestCase(StillLifeBackend.Gpu)]
        [Explicit("Real native integration; supply MOSU_STILL_LIFE_TEST_PACKAGE and run offscreen without active gameplay.")]
        public void TestRealNativeEngine(StillLifeBackend backend)
        {
            string directory = Environment.GetEnvironmentVariable("MOSU_STILL_LIFE_TEST_PACKAGE") ?? string.Empty;
            Assert.That(Directory.Exists(directory), Is.True);
            var options = new StillLifeOptions(true, backend, StillLifeResolution.Low, 1, 1, .45, 12345, 4, true, 64);
            using var token = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            using var engine = new StillLifeEngine(directory, options, token.Token);
            using var image = new Image<Rgb24>(256, 160, new Rgb24(30, 80, 120));
            var result = engine.Generate(image, options, token.Token, () => false);
            Assert.That(result.Width, Is.EqualTo(256));
            Assert.That(result.Height, Is.EqualTo(160));
            Assert.That(result.Rgb.Length, Is.EqualTo(256 * 160 * 3));
            Assert.That(result.Rgb.Distinct().Count(), Is.GreaterThan(8));
            var repeat = engine.Generate(image, options, token.Token, () => false);
            Assert.That(repeat.Rgb, Is.EqualTo(result.Rgb));
        }
    }
}
