using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Performance;
using osu.Game.Screens.Play;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>One optional, cancellable worker. Cache hits never load the neural models.</summary>
    public partial class StillLifeService : Component
    {
        private readonly object sync = new object();
        private readonly LinkedList<Request> queue = new LinkedList<Request>();
        private readonly SemaphoreSlim wake = new SemaphoreSlim(0, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private CancellationTokenSource generation = new CancellationTokenSource();
        private readonly List<Action> unbind = new List<Action>();
        private readonly ConcurrentDictionary<string, BackgroundImageCache.Artwork> memory = new ConcurrentDictionary<string, BackgroundImageCache.Artwork>();
        private readonly Queue<string> order = new Queue<string>();
        private StillLifeOptions options = null!;
        internal StillLifeOptions Options => Volatile.Read(ref options);
        private BackgroundImageCache cache = null!;
        private DirectBackgroundPackage package = null!;
        private Task? workerTask;
        private readonly List<CancellationTokenSource> retiredGeneration = new List<CancellationTokenSource>();
        private bool stopping;
        private int clearRequested;
        private int revision;
        internal int Revision => Volatile.Read(ref revision);
        public readonly Bindable<LocalisableString> Status = new Bindable<LocalisableString>(AiFunctionsStrings.StillLifeDisabled);
        internal int GeneratedCount;
        internal int DiskCacheHits;
        internal Func<Stream, StillLifeOptions, CancellationToken, BackgroundImageCache.Artwork>? GenerateForTest;
        internal bool WorkerStopped => workerTask?.IsCompleted != false;

        [Resolved(CanBeNull = true)]
        private ILocalUserPlayInfo? playInfo { get; set; }
        [Resolved(CanBeNull = true)]
        private IHighPerformanceSessionManager? performanceSession { get; set; }
        private bool gameplayActive => playInfo?.PlayingState.Value is { } state && state != LocalUserPlayingState.NotPlaying
                                       || performanceSession?.IsSessionActive == true;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Storage storage) => Initialise(config, storage);

        internal void Initialise(OsuConfigManager config, Storage storage)
        {
            options = StillLifeOptions.FromConfig(config);
            cache = new BackgroundImageCache(storage.GetFullPath("ai-background-cache/still-life"));
            package = new DirectBackgroundPackage(storage.GetFullPath("ai-models/background-distortion"));
            Bind<bool>(OsuSetting.ForkAiStillLife);
            Bind<StillLifeBackend>(OsuSetting.ForkAiStillLifeBackend);
            Bind<StillLifeResolution>(OsuSetting.ForkAiStillLifeSize);
            Bind<int>(OsuSetting.ForkAiStillLifePasses);
            Bind<int>(OsuSetting.ForkAiStillLifeSteps);
            Bind<double>(OsuSetting.ForkAiStillLifeStrength);
            Bind<int>(OsuSetting.ForkAiStillLifeSeed);
            Bind<int>(OsuSetting.ForkAiStillLifeThreads);
            Bind<bool>(OsuSetting.ForkAiCacheEnabled);
            Bind<int>(OsuSetting.ForkAiCacheMegabytes);
            void Bind<T>(OsuSetting key)
            {
                var value = config.GetBindable<T>(key);
                value.BindValueChanged(_ =>
                {
                    Volatile.Write(ref options, StillLifeOptions.FromConfig(config));
                    lock (sync)
                    {
                        generation.Cancel();
                        retiredGeneration.Add(generation);
                        generation = new CancellationTokenSource();
                        foreach (var request in queue)
                            request.Completion.TrySetCanceled();
                        queue.Clear();
                        Signal();
                    }
                    Interlocked.Increment(ref revision);
                    if (!Options.Enabled)
                        Publish(AiFunctionsStrings.StillLifeDisabled);
                });
                unbind.Add(value.UnbindAll);
            }
        }

        internal bool TryGet(string? key, out BackgroundImageCache.Artwork? artwork)
        {
            artwork = null;
            return Options.Enabled && key != null && memory.TryGetValue(Options.CacheIdentity + ":" + key, out artwork);
        }

        internal Task<BackgroundImageCache.Artwork?> RequestBackground(WorkingBeatmap beatmap, string? hash, string? cacheKey, CancellationToken token, bool generate)
            => RequestBackground(() => (hash, beatmap.OpenBackgroundStream()), cacheKey, token, generate);

        internal Task<BackgroundImageCache.Artwork?> RequestBackground(Func<(string? Hash, Stream? Stream)> source, string? key, CancellationToken token, bool generate)
        {
            if (TryGet(key, out var ready))
                return Task.FromResult(ready);
            lock (sync)
            {
                if (stopping || !Options.Enabled || token.IsCancellationRequested)
                    return Task.FromCanceled<BackgroundImageCache.Artwork?>(new CancellationToken(true));
                while (queue.Count >= 32)
                {
                    queue.First!.Value.Completion.TrySetCanceled();
                    queue.RemoveFirst();
                }
                var request = new Request(source, key, token, generate);
                queue.AddLast(request);
                workerTask ??= Task.Run(Run);
                Signal();
                return request.Completion.Task;
            }
        }

        public void ClearCache()
        {
            lock (sync)
            {
                memory.Clear();
                order.Clear();
                generation.Cancel();
                retiredGeneration.Add(generation);
                generation = new CancellationTokenSource();
                Interlocked.Exchange(ref clearRequested, 1);
                Interlocked.Increment(ref revision);
                workerTask ??= Task.Run(Run);
                Signal();
            }
        }

        private void Signal()
        {
            if (wake.CurrentCount == 0)
                wake.Release();
        }

        private async Task Run()
        {
            StillLifeEngine? engine = null;
            StillLifeOptions? engineOptions = null;
            DateTime retryAt = DateTime.MinValue;
            string? failedOptions = null;
            LocalisableString failureStatus = AiFunctionsStrings.StillLifeFailed;
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    if (!await wake.WaitAsync(TimeSpan.FromSeconds(60), lifetime.Token).ConfigureAwait(false))
                    {
                        engine?.Dispose();
                        engine = null;
                        continue;
                    }
                    if (Interlocked.Exchange(ref clearRequested, 0) != 0)
                        cache.Clear();
                    Request? request;
                    StillLifeOptions current;
                    CancellationToken generationToken;
                    bool changed;
                    lock (sync)
                    {
                        changed = retiredGeneration.Count > 0;
                        foreach (var retired in retiredGeneration)
                            retired.Dispose();
                        retiredGeneration.Clear();
                        current = Options;
                        request = queue.Last?.Value;
                        if (request != null)
                            queue.RemoveLast();
                        if (queue.Count > 0)
                            Signal();
                        generationToken = generation.Token;
                    }
                    if (changed)
                    {
                        engine?.Dispose();
                        engine = null;
                    }
                    if (!current.Enabled)
                    {
                        engine?.Dispose();
                        engine = null;
                        request?.Completion.TrySetCanceled();
                        continue;
                    }
                    if (request == null)
                        continue;
                    using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, generationToken, request.Token);
                    var token = tokenSource.Token;
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        if (TryGet(request.Key, out var ready))
                        {
                            request.Completion.TrySetResult(ready);
                            continue;
                        }
                        // Only the full selected background generates. Cards consume shared/disk results.
                        if (request.Generate)
                            await Task.Delay(350, token).ConfigureAwait(false);
                        var source = request.Source();
                        using var stream = source.Stream;
                        if (stream == null)
                        {
                            request.Completion.TrySetResult(null);
                            continue;
                        }
                        if (!stream.CanSeek || stream.Length > 32 * 1024 * 1024)
                            throw new InvalidDataException("Still Life image exceeds 32 MB.");
                        string hash = source.Hash ?? Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                        string identity = current.CacheIdentity + ":" + hash;
                        var result = current.CacheEnabled ? cache.Read(identity) : null;
                        if (result != null)
                            Interlocked.Increment(ref DiskCacheHits);
                        if (result == null && request.Generate)
                        {
                            if (failedOptions == current.CacheIdentity && DateTime.UtcNow < retryAt)
                            {
                                // A retry delay is normal state, not another failure for every selected background.
                                request.Completion.TrySetResult(null);
                                Publish(failureStatus);
                                continue;
                            }
                            while (gameplayActive)
                            {
                                Publish(AiFunctionsStrings.Waiting);
                                await Task.Delay(250, token).ConfigureAwait(false);
                            }
                            Publish(AiFunctionsStrings.StillLifeProcessing);
                            stream.Position = 0;
                            if (GenerateForTest != null)
                                result = GenerateForTest(stream, current, token);
                            else
                            {
                                if (engine == null || engineOptions == null || engineOptions.Backend != current.Backend || engineOptions.Threads != current.Threads)
                                {
                                    engine?.Dispose();
                                    engine = null;
                                    Publish(AiFunctionsStrings.StillLifeLoading);
                                    string directory = await package.Ensure(token).ConfigureAwait(false);
                                    engine = new StillLifeEngine(directory, current, token);
                                    engineOptions = current;
                                }
                                Publish(AiFunctionsStrings.StillLifeProcessing);
                                var decoderOptions = new DecoderOptions { TargetSize = new Size(512, 512), MaxFrames = 1, SkipMetadata = true };
                                var info = Image.Identify(decoderOptions, stream);
                                if ((long)info.Width * info.Height > 40_000_000)
                                    throw new InvalidDataException("Still Life image dimensions exceed the limit.");
                                stream.Position = 0;
                                using var image = Image.Load<Rgb24>(decoderOptions, stream);
                                result = engine.Generate(image, current, token, () => gameplayActive);
                            }
                            Interlocked.Increment(ref GeneratedCount);
                            token.ThrowIfCancellationRequested();
                            failedOptions = null;
                            if (current.CacheEnabled)
                                cache.Write(identity, result, current.CacheMegabytes * 1024L * 1024 * 3 / 4);
                        }
                        if (result != null)
                        {
                            Remember(identity, result);
                            if (request.Key != null)
                                Remember(current.CacheIdentity + ":" + request.Key, result);
                        }
                        token.ThrowIfCancellationRequested();
                        request.Completion.TrySetResult(result);
                        Publish(result != null || failedOptions != current.CacheIdentity ? AiFunctionsStrings.StillLifeReady : failureStatus);
                    }
                    catch (OperationCanceledException)
                    {
                        request.Completion.TrySetCanceled();
                        engine?.Dispose();
                        engine = null;
                    }
                    catch (Exception e)
                    {
                        request.Completion.TrySetResult(null);
                        engine?.Dispose();
                        engine = null;
                        failedOptions = current.CacheIdentity;
                        retryAt = DateTime.UtcNow.AddSeconds(30);
                        failureStatus = e switch
                        {
                            DirectBackgroundPackage.InsufficientSpaceException => AiFunctionsStrings.StillLifeInsufficientSpace,
                            DirectBackgroundPackage.DownloadException download => AiFunctionsStrings.StillLifeDownloadFailed(download.Host),
                            _ => AiFunctionsStrings.StillLifeFailed,
                        };
                        Logger.Error(e, "Background distortion unavailable; original/safety-filtered background retained.");
                        Publish(failureStatus);
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                engine?.Dispose();
                generation.Dispose();
                foreach (var retired in retiredGeneration)
                    retired.Dispose();
                retiredGeneration.Clear();
                lifetime.Dispose();
                wake.Dispose();
            }
        }

        private void Remember(string key, BackgroundImageCache.Artwork artwork)
        {
            lock (sync)
            {
                if (!memory.ContainsKey(key))
                {
                    if (order.Count >= 16)
                        memory.TryRemove(order.Dequeue(), out _);
                    order.Enqueue(key);
                }
                memory[key] = artwork;
            }
        }

        private void Publish(LocalisableString value) => Schedule(() => { if (!IsDisposed) Status.Value = value; });

        protected override void Dispose(bool isDisposing)
        {
            lock (sync)
            {
                stopping = true;
                lifetime.Cancel();
                generation.Cancel();
                foreach (var request in queue)
                    request.Completion.TrySetCanceled();
                queue.Clear();
                if (workerTask == null)
                {
                    generation.Dispose();
                    foreach (var retired in retiredGeneration)
                        retired.Dispose();
                    retiredGeneration.Clear();
                    lifetime.Dispose();
                    wake.Dispose();
                }
            }
            foreach (var action in unbind)
                action();
            base.Dispose(isDisposing);
        }

        private sealed record Request(Func<(string? Hash, Stream? Stream)> Source, string? Key, CancellationToken Token, bool Generate)
        {
            public TaskCompletionSource<BackgroundImageCache.Artwork?> Completion { get; } = new TaskCompletionSource<BackgroundImageCache.Artwork?>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
