using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Backgrounds.AI;
using osu.Game.Localisation;
using Realms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class BackgroundSafetyTest
    {
        [Test]
        public void TestPoliciesAndInvalidProbabilities()
        {
            var general = new BackgroundClassification(0.05f, 0.4f, 0.55f, 0, 0);
            var anime = new BackgroundClassification(0.55f, 0.4f, 0.05f, 0, 0);
            var scores = new BackgroundSafetyScores(general, anime);
            var policy = new BackgroundSafetyPolicy(true, true, .3, .3, BackgroundFilterMode.Suggestive, BackgroundSafetyCombination.Any, 16);
            Assert.That(policy.ShouldBlur(scores), Is.True);
            Assert.That((policy with { Categories = BackgroundFilterMode.Explicit }).ShouldBlur(scores), Is.True);
            Assert.That((policy with { Categories = BackgroundFilterMode.Explicit, Combination = BackgroundSafetyCombination.All }).ShouldBlur(scores), Is.False);
            Assert.That((policy with { Categories = BackgroundFilterMode.Hentai }).ShouldBlur(scores), Is.False);
            Assert.That((policy with { GeneralEnabled = false }).ShouldBlur(scores), Is.True);
            Assert.That((policy with { GeneralEnabled = false, AnimeEnabled = false }).ShouldBlur(scores), Is.True);
            Assert.That((policy with { GeneralThreshold = .5, AnimeThreshold = .5 }).ShouldBlur(scores), Is.False);
            Assert.That(policy.ShouldBlur(new BackgroundSafetyScores(null, anime)), Is.True, "Unknown must remain covered.");
            Assert.That((policy with { AnimeEnabled = false, Combination = BackgroundSafetyCombination.All }).ShouldBlur(new BackgroundSafetyScores(general, null)), Is.True);
            Assert.That(new BackgroundClassification(float.NaN, 0, 1, 0, 0).IsValid, Is.False);
            Assert.That(default(BackgroundClassification).IsValid, Is.False);
        }

        [TestCase(8, 0.9, 5)]
        [TestCase(8, 0.1, 0)]
        [TestCase(2, 1, 1)]
        [TestCase(1, 1, 1)]
        public void TestCpuAdmission(int processors, double idle, int expected)
            => Assert.That(BackgroundCpuBudget.Calculate(processors, idle), Is.EqualTo(expected));

        [Test]
        public void TestForkRealmMigrationBackupAndVersionedCache()
        {
            using var storage = new TemporaryNativeStorage($"ai-migration-{Guid.NewGuid():N}");
            using (var realm = Realm.GetInstance(new RealmConfiguration(storage.GetFullPath("fork.realm", true))
                   {
                       SchemaVersion = 13,
                       Schema = new[] { typeof(ForkBeatmapData), typeof(ForkScoreData), typeof(ForkSeasonalBackgroundData) },
                   }))
                realm.Write(() => realm.Add(new ForkSeasonalBackgroundData { Hash = "old", Data = new byte[] { 1, 2, 3 } }));
            string hash = new string('a', 64);
            var value = new BackgroundClassification(0.1f, 0.6f, 0.3f, 0, 0);
            using (var store = new ForkDataStore(storage))
            {
                store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version, value);
                Assert.That(store.GetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version), Is.EqualTo(value));
                Assert.That(store.GetBackgroundClassification(hash, EnsembleBackgroundClassifier.Anime.Version), Is.Null);
                Assert.Throws<ArgumentException>(() => store.SetBackgroundClassification(hash, "model-1", default));
            }
            Assert.That(File.Exists(storage.GetFullPath("fork.before-ai-v14.realm")), Is.True);
            using (var old = Realm.GetInstance(new RealmConfiguration(storage.GetFullPath("fork.before-ai-v14.realm"))
                   {
                       SchemaVersion = 13,
                       Schema = new[] { typeof(ForkBeatmapData), typeof(ForkScoreData), typeof(ForkSeasonalBackgroundData) },
                   }))
                Assert.That(old.Find<ForkSeasonalBackgroundData>("old")!.Data, Is.EqualTo(new byte[] { 1, 2, 3 }));
            using var reopened = new ForkDataStore(storage);
            Assert.That(reopened.GetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version), Is.EqualTo(value));
        }

        [TestCase("ru", "ИИ функции")]
        [TestCase("en", "AI functions")]
        [TestCase("es", "Funciones de IA")]
        [TestCase("ko", "AI 기능")]
        [TestCase("zh", "AI 功能")]
        public void TestRealLocalisationResolver(string language, string expected)
        {
            using var config = new TestConfig();
            using var manager = new LocalisationManager(config);
            manager.AddLanguage(language, new ResourceManagerLocalisationStore(language));
            config.SetValue(FrameworkSetting.Locale, language);
            Assert.That(manager.GetLocalisedString(AiFunctionsStrings.Title), Is.EqualTo(expected));
            using var resources = new ResourceManagerLocalisationStore(language);
            foreach (var property in typeof(AiFunctionsStrings).GetProperties())
            {
                var text = (LocalisableString)property.GetValue(null)!;
                Assert.That(manager.GetLocalisedString(text), Is.Not.Empty);
                if (language != "en")
                    Assert.That(resources.Get("osu.Game.Localisation.AiFunctions:" + property.Name), Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public async Task TestLazyWorkerCancellationInferenceAndPersistentCache()
        {
            string generalModel = Environment.GetEnvironmentVariable("MOSU_AI_TEST_GENERAL_MODEL") ?? string.Empty;
            string animeModel = Environment.GetEnvironmentVariable("MOSU_AI_TEST_ANIME_MODEL") ?? string.Empty;
            Assert.That(File.Exists(generalModel) && File.Exists(animeModel), Is.True, "Supply verified test models through MOSU_AI_TEST_GENERAL_MODEL and MOSU_AI_TEST_ANIME_MODEL.");
            using var storage = new TemporaryNativeStorage($"ai-worker-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            using var store = new ForkDataStore(storage);
            var service = new BackgroundSafetyService();
            service.Initialise(config, storage, store);
            Assert.That(config.Get<bool>(OsuSetting.ForkAiBackgroundBlur), Is.False);
            Assert.That(config.Get<BackgroundFilterMode>(OsuSetting.ForkAiBackgroundFilterMode), Is.EqualTo(BackgroundFilterMode.Suggestive));
            Assert.That(service.WorkerAlive, Is.False);
            Assert.That(Directory.Exists(storage.GetFullPath("ai-models")), Is.False);
            int sources = 0;
            Assert.That(service.RequestBackground(() => { sources++; return (null, null); }, CancellationToken.None).IsCanceled, Is.True);
            Assert.That(sources, Is.Zero);
            Directory.CreateDirectory(storage.GetFullPath("ai-models"));
            File.Copy(generalModel, storage.GetFullPath("ai-models/" + EnsembleBackgroundClassifier.General.FileName));
            File.Copy(animeModel, storage.GetFullPath("ai-models/" + EnsembleBackgroundClassifier.Anime.FileName));
            using var image = new Image<Rgb24>(320, 180, new Rgb24(20, 60, 100));
            using var bytes = new MemoryStream();
            image.SaveAsPng(bytes);
            byte[] png = bytes.ToArray();
            string hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
            try
            {
                service.SetEnabledForTest(true);
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                Assert.That(service.RequestBackground(() => { sources++; return (hash, new MemoryStream(png)); }, cancelled.Token).IsCanceled, Is.True);
                var watch = Stopwatch.StartNew();
                var request = service.RequestBackground(() =>
                {
                    Assert.That(Thread.CurrentThread.Priority, Is.EqualTo(ThreadPriority.Lowest));
                    sources++;
                    return (hash, new MemoryStream(png));
                }, CancellationToken.None);
                Assert.That(watch.ElapsedMilliseconds, Is.LessThan(50));
                var first = await request.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.That(first!.Scores.General!.Value.IsValid, Is.True);
                Assert.That(first.Scores.Anime!.Value.IsValid, Is.True);
                Assert.That(service.InferenceCount, Is.EqualTo(2));
                Assert.That(first.Scores.General.Value.Hentai, Is.EqualTo(.033762876).Within(.001), "Compare RGB preprocessing/output order with the Python ONNX reference.");
                Assert.That(first.Scores.Anime.Value.Neutral, Is.EqualTo(.047250398).Within(.001));
                var second = await service.RequestBackground(() => (hash, new MemoryStream(png)), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.That(second!.Scores, Is.EqualTo(first.Scores));
                Assert.That(service.InferenceCount, Is.EqualTo(2));
                Assert.That(service.CacheHits, Is.EqualTo(1));
                var immediate = service.RequestBackground(new osu.Game.Tests.Beatmaps.TestWorkingBeatmap(new osu.Game.Beatmaps.Beatmap()),
                    CancellationToken.None, hash, priority: true);
                Assert.That(immediate.IsCompletedSuccessfully, Is.True);
                using var reloaded = new ForkDataStore(storage);
                Assert.That(reloaded.GetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version), Is.EqualTo(first.Scores.General));
                Assert.That(reloaded.GetBackgroundClassification(hash, EnsembleBackgroundClassifier.Anime.Version), Is.EqualTo(first.Scores.Anime));
                config.SetValue(OsuSetting.ForkAiGeneralThreshold, .01);
                config.SetValue(OsuSetting.ForkAiBackgroundFilterMode, BackgroundFilterMode.Explicit);
                Assert.That(service.IsCurrent(first), Is.True, "Thresholds and categories must reuse cached scores.");
                config.SetValue(OsuSetting.ForkAiBlurStrength, 24.0);
                Assert.That(service.IsCurrent(first), Is.False);
                var stronger = await service.RequestBackground(() => (hash, new MemoryStream(png)), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.That(stronger!.BlurStrength, Is.EqualTo(24));
                Assert.That(service.InferenceCount, Is.EqualTo(2), "Blur strength must not cause inference.");
                using var queued = new CancellationTokenSource();
                for (int i = 0; i < 200; i++)
                    _ = service.RequestBackground(() => (hash, new MemoryStream(png)), queued.Token);
                Assert.That(service.PendingCount, Is.LessThanOrEqualTo(64));
                queued.Cancel();
                service.SetEnabledForTest(false);
                Assert.That(service.PendingCount, Is.Zero);
            }
            finally
            {
                service.Dispose();
                Assert.That(SpinWait.SpinUntil(() => !service.WorkerAlive, TimeSpan.FromSeconds(10)), Is.True);
            }
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public Task TestPersistedClassificationInFreshWorkerDoesNotInfer(bool generalEnabled, bool animeEnabled) => Task.Run(async () =>
        {
            using var storage = new TemporaryNativeStorage($"ai-restart-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkAiGeneralModel, generalEnabled);
            config.SetValue(OsuSetting.ForkAiAnimeModel, animeEnabled);
            byte[] png;
            using (var image = new Image<Rgb24>(800, 400, new Rgb24(40, 80, 120)))
            using (var output = new MemoryStream())
            {
                image.SaveAsPng(output);
                png = output.ToArray();
            }
            string hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
            var classification = new BackgroundClassification(0.05f, 0.85f, 0.1f, 0, 0);
            using (var writer = new ForkDataStore(storage))
            {
                writer.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version, classification);
                writer.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.Anime.Version, classification);
            }
            using var reader = new ForkDataStore(storage);
            var service = new BackgroundSafetyService();
            service.Initialise(config, storage, reader);
            try
            {
                service.SetEnabledForTest(true);
                var result = await service.RequestBackground(() => (hash, new MemoryStream(png)), CancellationToken.None, priority: true)
                    .WaitAsync(TimeSpan.FromSeconds(20));
                Assert.That(result!.Scores.General, generalEnabled ? Is.EqualTo(classification) : Is.Null);
                Assert.That(result.Scores.Anime, animeEnabled ? Is.EqualTo(classification) : Is.Null);
                Assert.That(result.Width, Is.EqualTo(320));
                Assert.That(result.Height, Is.EqualTo(160));
                Assert.That(service.InferenceCount, Is.Zero);
                Assert.That(service.CacheHits, Is.EqualTo((generalEnabled ? 1 : 0) + (animeEnabled ? 1 : 0)));
                Assert.That(Directory.GetFiles(storage.GetFullPath("ai-background-cache/safety"), "*.rgb"), Has.Length.EqualTo(1));
                Assert.That(Directory.Exists(storage.GetFullPath("ai-models")), Is.False, "Persisted scores need no weights/network.");
            }
            finally
            {
                service.Dispose();
                Assert.That(SpinWait.SpinUntil(() => !service.WorkerAlive, TimeSpan.FromSeconds(10)), Is.True);
            }
        });

        [TestCase(false)]
        [TestCase(true)]
        public Task TestPreviewAndFullBackgroundShareCompletedArtwork(bool mutableFile) => Task.Run(async () =>
        {
            using var storage = new TemporaryNativeStorage($"ai-shared-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            using var store = new ForkDataStore(storage);
            byte[] png;
            using (var image = new Image<Rgb24>(640, 360, new Rgb24(40, 80, 120)))
            using (var output = new MemoryStream())
            {
                image.SaveAsPng(output);
                png = output.ToArray();
            }
            string hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
            string key = mutableFile ? "stable:shared-file:revision" : hash;
            var classification = new BackgroundClassification(0.05f, 0.85f, 0.1f, 0, 0);
            store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version, classification);
            store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.Anime.Version, classification);
            var service = new BackgroundSafetyService();
            service.Initialise(config, storage, store);
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var abandoned = new CancellationTokenSource();
            int reads = 0;
            try
            {
                service.SetEnabledForTest(true);
                var blocker = service.RequestBackground(() =>
                {
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Test did not release the worker.");
                    return (null, null);
                }, CancellationToken.None, priority: true);
                Assert.That(entered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                var cancelledPreview = service.RequestBackground(() => throw new AssertionException("Cancelled preview opened artwork."),
                    abandoned.Token, cacheKey: key);
                var preview = service.RequestBackground(() => throw new AssertionException("Preview reopened already completed artwork."),
                    CancellationToken.None, cacheKey: key);
                var background = service.RequestBackground(() =>
                {
                    Interlocked.Increment(ref reads);
                    return (mutableFile ? null : hash, new MemoryStream(png));
                }, CancellationToken.None, priority: true, cacheKey: key);
                abandoned.Cancel();
                release.Set();
                await blocker.WaitAsync(TimeSpan.FromSeconds(15));
                var fullResult = await background.WaitAsync(TimeSpan.FromSeconds(15));
                Assert.That(service.TryGetCompleted(key, out var immediate), Is.True);
                Assert.That(immediate, Is.SameAs(fullResult), "Both surfaces see the same prepared blur immediately.");
                Assert.That(await preview.WaitAsync(TimeSpan.FromSeconds(15)), Is.SameAs(fullResult));
                Assert.That(SpinWait.SpinUntil(() => cancelledPreview.IsCanceled, TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(reads, Is.EqualTo(1));
                Assert.That(service.InferenceCount, Is.Zero);
            }
            finally
            {
                release.Set();
                service.Dispose();
                Assert.That(SpinWait.SpinUntil(() => !service.WorkerAlive, TimeSpan.FromSeconds(10)), Is.True);
            }
        });

        [Test]
        public async Task TestPreparedBlurSurvivesRestartAndStrengthInvalidatesOnlyArtwork()
        {
            using var storage = new TemporaryNativeStorage("ai-blur-restart-" + Guid.NewGuid().ToString("N"));
            using var config = new OsuConfigManager(storage);
            using var store = new ForkDataStore(storage);
            byte[] png;
            using (var image = new Image<Rgb24>(640, 360, new Rgb24(50, 80, 100)))
            using (var output = new MemoryStream())
            {
                image.SaveAsPng(output);
                png = output.ToArray();
            }
            string hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
            var classification = new BackgroundClassification(.1f, .8f, .1f, 0, 0);
            store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version, classification);
            store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.Anime.Version, classification);
            for (int restart = 0; restart < 2; restart++)
            {
                var service = new BackgroundSafetyService();
                service.Initialise(config, storage, store);
                try
                {
                    service.SetEnabledForTest(true);
                    var result = await service.RequestBackground(() => (hash, new MemoryStream(png)), CancellationToken.None, priority: true).WaitAsync(TimeSpan.FromSeconds(20));
                    Assert.That(result!.BlurredRgb, Is.Not.Null);
                    Assert.That(service.CachedArtworkLoads, Is.EqualTo(restart));
                    Assert.That(service.InferenceCount, Is.Zero);
                    if (restart == 1)
                    {
                        config.SetValue(OsuSetting.ForkAiBlurStrength, 24.0);
                        var stronger = await service.RequestBackground(() => (hash, new MemoryStream(png)), CancellationToken.None, priority: true).WaitAsync(TimeSpan.FromSeconds(20));
                        Assert.That(stronger!.BlurStrength, Is.EqualTo(24));
                        Assert.That(service.InferenceCount, Is.Zero);
                        Assert.That(service.CachedArtworkLoads, Is.EqualTo(1));
                    }
                }
                finally { service.Dispose(); Assert.That(SpinWait.SpinUntil(() => !service.WorkerAlive, 10000), Is.True); }
            }
        }

        private sealed class TestConfig : FrameworkConfigManager
        {
            protected override string Filename => null!;
            public TestConfig() : base(null!) { }
        }
    }
}
