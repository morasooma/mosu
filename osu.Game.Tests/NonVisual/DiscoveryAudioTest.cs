using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays.Discovery;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public partial class DiscoveryAudioTest
    {
        private sealed class ActiveGameplay : osu.Game.Screens.Play.ILocalUserPlayInfo
        {
            public osu.Framework.Bindables.IBindable<osu.Game.Screens.Play.LocalUserPlayingState> PlayingState { get; } =
                new osu.Framework.Bindables.Bindable<osu.Game.Screens.Play.LocalUserPlayingState>(osu.Game.Screens.Play.LocalUserPlayingState.Playing);
        }

        private static APIBeatmap Map(int id) => new APIBeatmap { OnlineID = id * 10, OnlineBeatmapSetID = id,
            BeatmapSet = new APIBeatmapSet { OnlineID = id }, UserPlayCount = 1 };
        private static float[] Vector(int index)
        {
            var result = new float[512]; result[index] = 1; return result;
        }

        [Test]
        public async Task TestLocalRankingChangesOrderAndReusesContentCache()
        {
            using var storage = new TemporaryNativeStorage("discovery-audio-" + Guid.NewGuid().ToString("N"));
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkAiAudioRecommendations, true);
            using var service = new DiscoveryAudioService();
            service.Initialise(config, storage);
            int active = 0, maximum = 0, inference = 0;
            service.AudioForTest = async (id, token) =>
            {
                int count = Interlocked.Increment(ref active);
                maximum = Math.Max(maximum, count);
                await Task.Delay(10, token);
                Interlocked.Decrement(ref active);
                return new[] { (byte)id };
            };
            service.EmbedForTest = (bytes, _) => { inference++; return Task.FromResult(Vector(bytes[0] == 3 ? 1 : 0)); };
            var result = await service.RankAsync(new[] { Map(1) }, new[] { Map(3), Map(2) }, DiscoverySeedSource.Manual, CancellationToken.None);
            Assert.That(result[2], Is.GreaterThan(result[3]));
            Assert.That(inference, Is.EqualTo(3));
            Assert.That(maximum, Is.InRange(1, 2));
            await service.RankAsync(new[] { Map(1) }, new[] { Map(2), Map(3) }, DiscoverySeedSource.Manual, CancellationToken.None);
            Assert.That(inference, Is.EqualTo(3), "cached content must not reload model or run inference");
            Assert.That(Directory.Exists(storage.GetFullPath("ai-models")), Is.False);
        }

        [Test]
        public async Task TestMissingCandidateIsNotTreatedAsARealAudioMatch()
        {
            using var storage = new TemporaryNativeStorage("discovery-audio-missing-" + Guid.NewGuid().ToString("N"));
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkAiAudioRecommendations, true);
            using var service = new DiscoveryAudioService(); service.Initialise(config, storage);
            service.AudioForTest = (id, _) => id == 3 ? Task.FromException<byte[]>(new IOException("missing")) : Task.FromResult(new[] { (byte)id });
            service.EmbedForTest = (_, _) => Task.FromResult(Vector(0));
            var result = await service.RankAsync(new[] { Map(1) }, new[] { Map(3), Map(2) }, DiscoverySeedSource.Manual, CancellationToken.None);
            Assert.That(result.Keys, Is.EquivalentTo(new[] { 2 }));
        }

        [Test]
        public void TestDisabledNeverDownloadsAndCancellationStopsWork()
        {
            using var storage = new TemporaryNativeStorage("discovery-audio-cancel-" + Guid.NewGuid().ToString("N"));
            using var config = new OsuConfigManager(storage);
            using var service = new DiscoveryAudioService(); service.Initialise(config, storage);
            int calls = 0;
            service.AudioForTest = async (_, token) => { calls++; await Task.Delay(Timeout.Infinite, token); return Array.Empty<byte>(); };
            Assert.ThrowsAsync<OperationCanceledException>(async () => await service.RankAsync(new[] { Map(1) }, new[] { Map(2) }, DiscoverySeedSource.Manual, CancellationToken.None));
            Assert.That(calls, Is.Zero);
            config.SetValue(OsuSetting.ForkAiAudioRecommendations, true);
            using var cancel = new CancellationTokenSource(50);
            Assert.ThrowsAsync<TaskCanceledException>(async () => await service.RankAsync(new[] { Map(1) }, new[] { Map(2) }, DiscoverySeedSource.Manual, cancel.Token));
            Assert.That(calls, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void TestPreprocessingAndMalformedVectors()
        {
            var wave = Enumerable.Range(0, 48000 * 12).Select(i => (float)Math.Sin(i * .01)).ToArray();
            var windows = DiscoveryAudioDecoder.Windows(wave, CancellationToken.None);
            Assert.That(windows, Has.Length.EqualTo(3));
            Assert.That(windows.All(w => w.Length == 480000 && w.All(float.IsFinite)), Is.True);
            Assert.Throws<InvalidDataException>(() => DiscoveryAudioDecoder.Windows(new float[480000], CancellationToken.None));
            Assert.Throws<InvalidDataException>(() => DiscoveryAudioService.Cosine(new float[512], Vector(0)));
            Assert.Throws<InvalidDataException>(() => DiscoveryAudioService.Cosine(new[] { float.NaN }, Vector(0)));
            Assert.That(DiscoveryAudioService.Cosine(Vector(0), Vector(0)), Is.EqualTo(1));
            Assert.That(DiscoveryAudioService.Cosine(Vector(0), Vector(1)), Is.EqualTo(0));
        }

        [Test]
        public void TestNormalisationDoesNotReuseMertLayersOrCentre()
        {
            var embedding = Enumerable.Range(0, 512).Select(i => (float)Math.Sin(i)).ToArray();
            var vector = DiscoveryAudioModel.Transform(embedding);
            Assert.That(vector.Sum(v => (double)v * v), Is.EqualTo(1).Within(1e-5));
            Assert.Throws<InvalidDataException>(() => DiscoveryAudioModel.Transform(new float[13 * 768]));
            Assert.That(DiscoveryAudioModel.Version, Does.StartWith("clap-music-"));
            Assert.That(typeof(DiscoveryAudioService).Assembly.GetManifestResourceNames()
                .Where(n => n.Contains("mert", StringComparison.OrdinalIgnoreCase)), Is.Empty,
                "No non-commercial MERT graph or derived centre may remain embedded in the client");
            Assert.That(DiscoveryAudioModel.Features, Is.EqualTo(512));
            Assert.That(DiscoveryAudioModel.SampleRate, Is.EqualTo(48000));
        }

        [Test]
        public async Task TestRealPreviewDecodeInferenceAndRankingWhenFixtureProvided()
        {
            string? fixture = Environment.GetEnvironmentVariable("MOSU_CLAP_TEST_FIXTURE");
            if (fixture == null) Assert.Ignore("Explicit local CLAP fixture required.");
            string directory = fixture!;
            if (!ManagedBass.Bass.Init(0) && ManagedBass.Bass.LastError != ManagedBass.Errors.Already)
                throw new InvalidOperationException("Cannot initialise silent decoding device.");
            using var storage = new TemporaryNativeStorage("discovery-audio-native-" + Guid.NewGuid().ToString("N"));
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkAiAudioRecommendations, true);
            config.SetValue(OsuSetting.ForkAiAudioDevice, DiscoveryAudioDevice.Auto);
            string models = storage.GetFullPath("ai-models/discovery/" + DiscoveryAudioModel.Version);
            Directory.CreateDirectory(models);
            // Local verified fixtures only. Hard links avoid copying 278 MB for a test.
            Link(Path.Combine(directory, "clap-music-audio.onnx"), Path.Combine(models, "clap-music-audio.onnx"));
            string runtime = Path.Combine(models, "runtime/1.23.2/libonnxruntime.so.1.23.2");
            Directory.CreateDirectory(Path.GetDirectoryName(runtime)!);
            Link(Environment.GetEnvironmentVariable("MOSU_CLAP_TEST_NATIVE")!, runtime);
            using var service = new DiscoveryAudioService(); service.Initialise(config, storage, new ActiveGameplay());
            service.AudioForTest = (id, _) => Task.FromResult(File.ReadAllBytes(Path.Combine(directory, id + ".mp3")));
            var timer = System.Diagnostics.Stopwatch.StartNew();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var streamed = new Dictionary<int, double>();
            var result = await service.RankAsync(new[] { Map(1605076) }, new[] { Map(2091950) }, DiscoverySeedSource.Manual, deadline.Token,
                (id, score) => streamed.Add(id, score));
            Assert.That(streamed, Is.EqualTo(result), "completed scores are published during active gameplay, without waiting for a break");
            Assert.That(result.Keys, Is.EquivalentTo(new[] { 2091950 }));
            Assert.That(result[2091950], Is.InRange(-1, 1));
            var files = Directory.GetFiles(storage.GetFullPath("ai-audio-cache"), "*.f32", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.EqualTo(2));
            foreach (int id in new[] { 1605076, 2091950 })
            {
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, id + ".mp3"))));
                byte[] bytes = File.ReadAllBytes(files.Single(f => Path.GetFileNameWithoutExtension(f) == hash));
                var actual = new float[512]; Buffer.BlockCopy(bytes, 0, actual, 0, bytes.Length);
                bytes = File.ReadAllBytes(Path.Combine(directory, id + "-expected.f32"));
                var expected = new float[512]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
                double cosine = DiscoveryAudioService.Cosine(actual, expected);
                TestContext.Out.WriteLine($"Native MP3 decoder + CLAP vs Python/ffmpeg set={id}: cosine={cosine:R}");
                Assert.That(cosine, Is.GreaterThan(.99), "decoder/resampler must preserve the audio feature space");
            }
            TestContext.Out.WriteLine($"Native service, two real previews: elapsed={timer.Elapsed.TotalSeconds:F2}s; cosine={result[2091950]:R}");

            static void Link(string source, string destination)
            {
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ln")
                { ArgumentList = { source, destination }, UseShellExecute = false });
                process!.WaitForExit();
                Assert.That(process.ExitCode, Is.Zero);
            }
        }

        // Deliberately opt-in: ordinary tests must never download a 278 MB model or touch a user profile.
        [Test]
        public void TestRealClapCSharpParityWhenFixtureProvided()
        {
            string? fixture = Environment.GetEnvironmentVariable("MOSU_CLAP_TEST_FIXTURE");
            if (fixture == null) Assert.Ignore("Explicit local CLAP fixture required.");
            string directory = fixture!;
            string runtimeRoot = Path.Combine(directory, "runtime");
            string runtimeFile = Path.Combine(runtimeRoot, "1.23.2/libonnxruntime.so.1.23.2");
            Directory.CreateDirectory(Path.GetDirectoryName(runtimeFile)!);
            if (!File.Exists(runtimeFile)) File.Copy(Environment.GetEnvironmentVariable("MOSU_CLAP_TEST_NATIVE")!, runtimeFile);
            osu.Game.Graphics.Backgrounds.AI.BackgroundAiRuntime.Ensure(runtimeRoot, CancellationToken.None).GetAwaiter().GetResult();
            {
                using var model = new DiscoveryAudioModel(Path.Combine(directory, "clap-music-audio.onnx"), DiscoveryAudioDevice.Cpu);
                byte[] bytes = File.ReadAllBytes(Path.Combine(directory, "parity-input.f32"));
                var input = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, input, 0, bytes.Length);
                var mel = DiscoveryAudioFeatures.Extract(input, CancellationToken.None);
                bytes = File.ReadAllBytes(Path.Combine(directory, "parity-mel.f32"));
                var expectedMel = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, expectedMel, 0, bytes.Length);
                double melDelta = mel.Zip(expectedMel, (a, b) => Math.Abs(a - b)).Max();
                TestContext.Out.WriteLine($"CLAP log-mel vs Transformers max delta: {melDelta:R}");
                Assert.That(melDelta, Is.LessThan(2e-4));
                var actual = model.InferFeatures(mel, CancellationToken.None);
                bytes = File.ReadAllBytes(Path.Combine(directory, "parity-embedding.f32"));
                var expected = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
                Assert.That(actual.Zip(expected, (a, b) => Math.Abs(a - b)).Max(), Is.LessThan(2e-5));
                Assert.That(model.Provider, Is.EqualTo("CPU"));
                TestContext.Out.WriteLine("Real native C# CLAP: embedding agrees with Python ONNX within 2e-5.");
            }
        }
    }
}
