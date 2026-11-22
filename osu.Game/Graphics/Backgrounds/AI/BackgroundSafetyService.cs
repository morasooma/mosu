using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Localisation;
using osu.Game.Performance;
using osu.Game.Screens.Play;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>
    /// One lazy, low-priority worker. Rendering only submits bounded requests and consumes completed results.
    /// No DB, image decoding, networking, hashing or inference runs in Update or drawable load methods.
    /// </summary>
    public partial class BackgroundSafetyService : Component
    {
        private const int maximum_requests = 64;
        private readonly object sync = new object();
        private readonly LinkedList<Request> queue = new LinkedList<Request>();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Dictionary<string, BackgroundClassification> memoryCache = new Dictionary<string, BackgroundClassification>();
        private readonly Queue<string> memoryOrder = new Queue<string>();
        private readonly ConcurrentDictionary<string, SafetyResult> completed = new ConcurrentDictionary<string, SafetyResult>();
        private readonly Queue<string> completedOrder = new Queue<string>(); // <= 9.4 MB of RGB, no GPU ownership.

        internal BackgroundSafetyPolicy Policy => Volatile.Read(ref policy);
        private BackgroundSafetyPolicy policy = null!;
        private readonly List<Action> unbindPolicy = new List<Action>();

        internal bool IsCurrent(SafetyResult result) => result.BlurStrength == Policy.BlurStrength
            && (!Policy.NeedsGeneral || result.Scores.General != null) && (!Policy.NeedsAnime || result.Scores.Anime != null);

        internal bool TryGetCompleted(string? hash, out SafetyResult? result)
        {
            result = null;
            if (hash == null || !completed.TryGetValue(hash, out var ready) || !IsCurrent(ready))
                return false;
            result = ready;
            return true;
        }
        private CancellationTokenSource enabledCancellation = new CancellationTokenSource();
        private readonly List<CancellationTokenSource> retiredCancellation = new List<CancellationTokenSource>();
        private Thread? worker;
        private volatile bool enabled;
        private volatile bool stopping;
        private long lastSubmission;
        private string modelDirectory = string.Empty;
        private Bindable<bool> setting = null!;
        private ForkDataStore store = null!;
        private BackgroundImageCache artworkCache = null!;
        private volatile bool cacheEnabled = true;
        private long cacheLimit = 512 * 1024 * 1024;
        private int clearCache;
        public int CachedArtworkLoads;
        private int cacheRevision;
        internal int CacheRevision => Volatile.Read(ref cacheRevision);
        public void ClearArtworkCache()
        {
            lock (sync)
            {
                Interlocked.Exchange(ref clearCache, 1);
                enabledCancellation.Cancel();
                retiredCancellation.Add(enabledCancellation);
                enabledCancellation = new CancellationTokenSource();
                foreach (var request in queue)
                    request.Completion.TrySetCanceled();
                queue.Clear();
                completed.Clear();
                completedOrder.Clear();
                Interlocked.Increment(ref cacheRevision);
                if (worker == null)
                {
                    worker = new Thread(run) { IsBackground = true, Name = "Mosu background safety", Priority = ThreadPriority.Lowest };
                    worker.Start();
                }
                wake.Set();
            }
        }
        public readonly Bindable<LocalisableString> Status = new Bindable<LocalisableString>(AiFunctionsStrings.Disabled);
        public bool Enabled => enabled;
        internal int PendingCount { get { lock (sync) return queue.Count; } }
        internal int InferenceCount;
        internal int CacheHits;
        public int CompletedInferences => Volatile.Read(ref InferenceCount);
        public int CachedClassifications => Volatile.Read(ref CacheHits);

        [Resolved(CanBeNull = true)]
        private ILocalUserPlayInfo? playInfo { get; set; }

        [Resolved(CanBeNull = true)]
        private IHighPerformanceSessionManager? performanceSession { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Storage storage, ForkDataStore dataStore)
        {
            Initialise(config, storage, dataStore);
        }

        internal void Initialise(OsuConfigManager config, Storage storage, ForkDataStore dataStore)
        {
            store = dataStore;
            modelDirectory = storage.GetFullPath("ai-models");
            artworkCache = new BackgroundImageCache(storage.GetFullPath("ai-background-cache/safety"));
            var cache = config.GetBindable<bool>(OsuSetting.ForkAiCacheEnabled);
            cache.BindValueChanged(v => cacheEnabled = v.NewValue, true);
            unbindPolicy.Add(cache.UnbindAll);
            var limit = config.GetBindable<int>(OsuSetting.ForkAiCacheMegabytes);
            limit.BindValueChanged(v => Interlocked.Exchange(ref cacheLimit, v.NewValue * 1024L * 1024), true);
            unbindPolicy.Add(limit.UnbindAll);
            setting = config.GetBindable<bool>(OsuSetting.ForkAiBackgroundBlur);
            policy = BackgroundSafetyPolicy.FromConfig(config);
            bindPolicy<bool>(config, OsuSetting.ForkAiGeneralModel);
            bindPolicy<bool>(config, OsuSetting.ForkAiAnimeModel);
            bindPolicy<double>(config, OsuSetting.ForkAiGeneralThreshold);
            bindPolicy<double>(config, OsuSetting.ForkAiAnimeThreshold);
            bindPolicy<double>(config, OsuSetting.ForkAiBlurStrength);
            bindPolicy<BackgroundFilterMode>(config, OsuSetting.ForkAiBackgroundFilterMode);
            bindPolicy<BackgroundSafetyCombination>(config, OsuSetting.ForkAiCombination);
        }

        private void bindPolicy<T>(OsuConfigManager config, OsuSetting key)
        {
            var bound = config.GetBindable<T>(key);
            bound.BindValueChanged(_ =>
            {
                Volatile.Write(ref policy, BackgroundSafetyPolicy.FromConfig(config));
                lock (sync)
                {
                    if (!stopping)
                        wake.Set();
                }
            });
            unbindPolicy.Add(bound.UnbindAll);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            setting.BindValueChanged(v => setEnabled(v.NewValue), true);
        }

        internal void SetEnabledForTest(bool value) => setEnabled(value);
        internal bool WorkerAlive => worker?.IsAlive == true;

        private void setEnabled(bool value)
        {
            lock (sync)
            {
                enabled = value;
                enabledCancellation.Cancel();
                retiredCancellation.Add(enabledCancellation);
                enabledCancellation = new CancellationTokenSource();
                foreach (var request in queue)
                    request.Completion.TrySetCanceled();
                queue.Clear();
                if (value && worker == null)
                {
                    worker = new Thread(run) { IsBackground = true, Name = "Mosu background safety", Priority = ThreadPriority.Lowest };
                    worker.Start();
                }
                wake.Set();
            }
            publish(value ? AiFunctionsStrings.Loading : AiFunctionsStrings.Disabled);
        }

        internal Task<SafetyResult?> RequestBackground(WorkingBeatmap beatmap, CancellationToken cancellation, string? hash = null, bool priority = false, string? cacheKey = null)
        {
            cacheKey ??= hash;
            if (enabled && !cancellation.IsCancellationRequested && TryGetCompleted(cacheKey, out var ready))
            {
                Interlocked.Increment(ref CacheHits);
                return Task.FromResult(ready);
            }
            return RequestBackground(() => (hash ?? beatmap.BackgroundContentHash, beatmap.OpenBackgroundStream()), cancellation, priority, cacheKey);
        }

        // The producer is evaluated only on the worker. Tests can supply isolated image streams.
        internal Task<SafetyResult?> RequestBackground(Func<(string? Hash, Stream? Stream)> source, CancellationToken cancellation, bool priority = false, string? cacheKey = null)
        {
            lock (sync)
            {
                if (!enabled || stopping || cancellation.IsCancellationRequested)
                    return Task.FromCanceled<SafetyResult?>(new CancellationToken(true));

                // Remove dead requests first. Prefer newest visible artwork when the bound is reached.
                for (var node = queue.First; node != null;)
                {
                    var next = node.Next;
                    if (node.Value.Cancellation.IsCancellationRequested || queue.Count >= maximum_requests)
                    {
                        node.Value.Completion.TrySetCanceled();
                        queue.Remove(node);
                    }
                    node = next;
                }
                var request = new Request(source, cancellation, priority, cacheKey);
                lastSubmission = Stopwatch.GetTimestamp();
                queue.AddLast(request);
                wake.Set();
                return request.Completion.Task;
            }
        }

        private bool gameplayActive => playInfo?.PlayingState.Value is { } state && state != LocalUserPlayingState.NotPlaying
                                       || performanceSession?.IsSessionActive == true;

        private void run()
        {
            EnsembleBackgroundClassifier? general = null;
            EnsembleBackgroundClassifier? anime = null;
            var retries = new Dictionary<string, DateTime>();
            try
            {
                while (!stopping)
                {
                    lock (sync)
                    {
                        foreach (var retired in retiredCancellation)
                            retired.Dispose();
                        retiredCancellation.Clear();
                    }
                    if (Interlocked.Exchange(ref clearCache, 0) != 0)
                        artworkCache.Clear();
                    if (!enabled)
                    {
                        general?.Dispose();
                        general = null;
                        anime?.Dispose();
                        anime = null;
                        retries.Clear();
                        wake.WaitOne(250);
                        continue;
                    }
                    CancellationToken token;
                    lock (sync) token = enabledCancellation.Token;
                    Request? request = null;
                    try
                    {
                        var current = Policy;
                        if (!current.NeedsGeneral) { general?.Dispose(); general = null; }
                        if (!current.NeedsAnime) { anime?.Dispose(); anime = null; }
                        lock (sync)
                        {
                            if (queue.Last != null)
                            {
                                var selected = queue.Last;
                                for (var node = queue.Last; node != null; node = node.Previous)
                                {
                                    if (node.Value.Priority)
                                    {
                                        selected = node;
                                        break;
                                    }
                                }
                                request = selected.Value;
                                queue.Remove(selected);
                            }
                        }
                        if (request == null)
                        {
                            publish(!current.NeedsGeneral && !current.NeedsAnime ? AiFunctionsStrings.NoModels
                                : retries.Values.Any(t => t > DateTime.UtcNow) ? AiFunctionsStrings.Failed : AiFunctionsStrings.Ready);
                            wake.WaitOne(250);
                            continue;
                        }
                        if (request.Cancellation.IsCancellationRequested)
                        {
                            request.Completion.TrySetCanceled();
                            continue;
                        }
                        // Don't start new image work during active traversal. A stable background gets priority
                        // after the input/loader burst settles; the queue itself never blocks scrolling.
                        long submitted;
                        lock (sync) submitted = lastSubmission;
                        int settle = request.Priority ? 0 : 200 - (int)Stopwatch.GetElapsedTime(Math.Max(request.CreatedAt, submitted)).TotalMilliseconds;
                        if (settle > 0 && token.WaitHandle.WaitOne(settle))
                            token.ThrowIfCancellationRequested();
                        request.Cancellation.ThrowIfCancellationRequested();
                        var watch = Stopwatch.StartNew();
                        SafetyResult? result;
                        if (TryGetCompleted(request.CacheKey, out var shared))
                        {
                            Interlocked.Increment(ref CacheHits);
                            result = shared;
                        }
                        else
                        {
                            result = process(request.Source, current, ref general, ref anime, retries, token, request.Cancellation);
                            if (result != null && request.CacheKey != null)
                                cacheCompleted(request.CacheKey, result);
                        }
                        token.ThrowIfCancellationRequested();
                        request.Cancellation.ThrowIfCancellationRequested();
                        request.Completion.TrySetResult(result);
                        // At most ~25% worker duty, in addition to load-based admission and gameplay pause.
                        token.WaitHandle.WaitOne(Math.Clamp((int)watch.ElapsedMilliseconds * 3, 10, 1000));
                    }
                    catch (OperationCanceledException)
                    {
                        request?.Completion.TrySetCanceled();
                    }
                    catch (Exception exception)
                    {
                        request?.Completion.TrySetResult(null);
                        Logger.Error(exception, "AI background analysis failed; artwork remains covered until the option is disabled.");
                        publish(AiFunctionsStrings.Failed);
                        wake.WaitOne(250);
                    }
                }
            }
            finally
            {
                general?.Dispose();
                anime?.Dispose();
                lock (sync)
                {
                    enabledCancellation.Dispose();
                    foreach (var retired in retiredCancellation)
                        retired.Dispose();
                    retiredCancellation.Clear();
                }
                wake.Dispose();
            }
        }

        private SafetyResult? process(Func<(string? Hash, Stream? Stream)> source, BackgroundSafetyPolicy current,
                                      ref EnsembleBackgroundClassifier? general, ref EnsembleBackgroundClassifier? anime,
                                      Dictionary<string, DateTime> retries, CancellationToken enabledToken, CancellationToken requestToken)
        {
            var input = source();
            using var stream = input.Stream;
            if (stream == null)
                return new SafetyResult(new BackgroundSafetyScores(new BackgroundClassification(0, 0, 1, 0, 0),
                    new BackgroundClassification(1, 0, 0, 0, 0)), null, BlurStrength: current.BlurStrength);
            if (!stream.CanSeek || stream.Length > 32 * 1024 * 1024)
                throw new InvalidDataException("Background exceeds the analysis input limit.");
            enabledToken.ThrowIfCancellationRequested();
            requestToken.ThrowIfCancellationRequested();
            string hash = input.Hash ?? Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            stream.Position = 0;
            if (TryGetCompleted(hash, out var ready))
            {
                Interlocked.Increment(ref CacheHits);
                return ready;
            }
            Image<Rgb24>? image = null;
            try
            {
                BackgroundClassification? generalScores = null;
                BackgroundClassification? animeScores = null;
                if (current.NeedsGeneral)
                    generalScores = classify(hash, EnsembleBackgroundClassifier.General, ref general, ref image, stream, retries, enabledToken, requestToken);
                if (current.NeedsAnime)
                    animeScores = classify(hash, EnsembleBackgroundClassifier.Anime, ref anime, ref image, stream, retries, enabledToken, requestToken);
                // Always retain a proportional blurred thumbnail, even for currently safe scores:
                // changing a threshold/category later must not require inference or reveal an image.
                stream.Position = 0;
                string artworkKey = FormattableString.Invariant($"blur-v1:{hash}:{current.BlurStrength:R}");
                var cachedArtwork = cacheEnabled ? artworkCache.Read(artworkKey) : null;
                if (cachedArtwork != null)
                {
                    Interlocked.Increment(ref CachedArtworkLoads);
                    return cacheCompleted(hash, new SafetyResult(new BackgroundSafetyScores(generalScores, animeScores), cachedArtwork.Rgb,
                        cachedArtwork.Width, cachedArtwork.Height, current.BlurStrength));
                }
                image ??= decode(stream);
                using var blurred = image.Clone(c => c.Resize(new ResizeOptions { Size = new Size(320, 320), Mode = ResizeMode.Max })
                    .GaussianBlur((float)current.BlurStrength));
                byte[] pixels = new byte[blurred.Width * blurred.Height * 3];
                blurred.CopyPixelDataTo(pixels);
                if (cacheEnabled)
                    artworkCache.Write(artworkKey, new BackgroundImageCache.Artwork(pixels, blurred.Width, blurred.Height), Interlocked.Read(ref cacheLimit) / 4);
                return cacheCompleted(hash, new SafetyResult(new BackgroundSafetyScores(generalScores, animeScores), pixels,
                    blurred.Width, blurred.Height, current.BlurStrength));
            }
            finally
            {
                image?.Dispose();
            }
        }

        private BackgroundClassification classify(string hash, EnsembleBackgroundClassifier.Model definition, ref EnsembleBackgroundClassifier? model,
                                                  ref Image<Rgb24>? image, Stream stream, Dictionary<string, DateTime> retries,
                                                  CancellationToken enabledToken, CancellationToken requestToken)
        {
            string key = definition.Version + ":" + hash;
            if (memoryCache.TryGetValue(key, out var cachedMemory))
            {
                Interlocked.Increment(ref CacheHits);
                return cachedMemory;
            }
            var cached = store.GetBackgroundClassification(hash, definition.Version);
            BackgroundClassification classification;
            if (cached != null)
            {
                classification = cached.Value;
                Interlocked.Increment(ref CacheHits);
            }
            else
            {
                enabledToken.ThrowIfCancellationRequested();
                requestToken.ThrowIfCancellationRequested();
                var budget = new BackgroundCpuBudget();
                while (gameplayActive || !budget.CanRun())
                {
                    publish(AiFunctionsStrings.Waiting);
                    enabledToken.ThrowIfCancellationRequested();
                    requestToken.ThrowIfCancellationRequested();
                    enabledToken.WaitHandle.WaitOne(250);
                }
                if (model == null)
                {
                    if (retries.TryGetValue(definition.Version, out var retry) && DateTime.UtcNow < retry)
                        throw new InvalidOperationException("AI model download is waiting before retry.");
                    publish(AiFunctionsStrings.Loading);
                    try
                    {
                        string path = EnsembleBackgroundClassifier.Download(definition, modelDirectory, enabledToken).GetAwaiter().GetResult();
                        enabledToken.ThrowIfCancellationRequested();
                        BackgroundAiRuntime.Ensure(Path.Combine(modelDirectory, "runtime"), enabledToken).GetAwaiter().GetResult();
                        model = new EnsembleBackgroundClassifier(definition, path);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        retries[definition.Version] = DateTime.UtcNow.AddSeconds(30);
                        throw;
                    }
                }
                stream.Position = 0;
                image ??= decode(stream);
                enabledToken.ThrowIfCancellationRequested();
                requestToken.ThrowIfCancellationRequested();
                classification = model.Classify(image);
                Interlocked.Increment(ref InferenceCount);
                store.SetBackgroundClassification(hash, definition.Version, classification);
            }
            if (memoryCache.Count >= 512)
                memoryCache.Remove(memoryOrder.Dequeue());
            memoryCache.Add(key, classification);
            memoryOrder.Enqueue(key);
            return classification;
        }

        private SafetyResult cacheCompleted(string hash, SafetyResult result)
        {
            lock (sync)
            {
                if (!completed.ContainsKey(hash))
                {
                    if (completed.Count >= 32)
                        completed.TryRemove(completedOrder.Dequeue(), out _);
                    completedOrder.Enqueue(hash);
                }
                completed[hash] = result;
            }
            return result;
        }

        private static Image<Rgb24> decode(Stream stream)
        {
            var configuration = SixLabors.ImageSharp.Configuration.Default.Clone();
            configuration.MaxDegreeOfParallelism = 1;
            var options = new DecoderOptions
            {
                Configuration = configuration,
                TargetSize = new Size(512, 512),
                MaxFrames = 1,
                SkipMetadata = true,
            };
            var info = Image.Identify(options, stream);
            if ((long)info.Width * info.Height > 40_000_000)
                throw new InvalidDataException("Background dimensions exceed the analysis limit.");
            stream.Position = 0;
            return Image.Load<Rgb24>(options, stream);
        }

        private LocalisableString? lastPublished;

        private void publish(LocalisableString value)
        {
            if (lastPublished?.Equals(value) == true)
                return;
            lastPublished = value;
            Schedule(() =>
            {
                if (!IsDisposed && !Status.Value.Equals(value))
                    Status.Value = value;
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            lock (sync)
            {
                stopping = true;
                enabled = false;
                enabledCancellation.Cancel();
                foreach (var request in queue)
                    request.Completion.TrySetCanceled();
                queue.Clear();
                if (worker != null)
                    wake.Set();
                else
                {
                    wake.Dispose();
                    enabledCancellation.Dispose();
                    foreach (var retired in retiredCancellation)
                        retired.Dispose();
                    retiredCancellation.Clear();
                }
            }
            setting?.UnbindAll();
            foreach (var unbind in unbindPolicy)
                unbind();
            base.Dispose(isDisposing);
        }

        internal sealed record SafetyResult(BackgroundSafetyScores Scores, byte[]? BlurredRgb, int Width = 0, int Height = 0, double BlurStrength = 16);
        private sealed record Request(Func<(string? Hash, Stream? Stream)> Source, CancellationToken Cancellation, bool Priority, string? CacheKey)
        {
            public long CreatedAt { get; } = Stopwatch.GetTimestamp();
            public TaskCompletionSource<SafetyResult?> Completion { get; } = new TaskCompletionSource<SafetyResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
