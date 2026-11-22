using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds.AI;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Performance;
using osu.Game.Screens.Play;

namespace osu.Game.Overlays.Discovery
{
    /// <summary>Optional local CLAP music worker, shared by discovery and AI settings. No executable downloads or Python.</summary>
    public partial class DiscoveryAudioService : Component
    {
        private readonly SemaphoreSlim owner = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private CancellationTokenSource settingsGeneration = new CancellationTokenSource();
        private readonly List<CancellationTokenSource> retired = new List<CancellationTokenSource>();
        private IBindable<bool> enabled = null!;
        private IBindable<DiscoveryAudioDevice> device = null!;
        private IBindable<int> batch = null!;
        private string models = null!;
        private string cache = null!;
        private bool clearRequested;
        private bool stopping;
        private readonly object sync = new object();
        public readonly Bindable<LocalisableString> Status = new Bindable<LocalisableString>(DiscoveryStrings.AudioDisabled);
        internal bool Enabled => enabled?.Value == true;
        internal int CandidateLimit => batch.Value;
        internal event Action? SettingsChanged;
        internal Func<int, CancellationToken, Task<byte[]>>? AudioForTest;
        internal Func<byte[], CancellationToken, Task<float[]>>? EmbedForTest;
        [Resolved(CanBeNull = true)] private ILocalUserPlayInfo? playInfo { get; set; }
        [Resolved(CanBeNull = true)] private IHighPerformanceSessionManager? performance { get; set; }
        private bool gameplayActive => playInfo?.PlayingState.Value is { } state && state != LocalUserPlayingState.NotPlaying || performance?.IsSessionActive == true;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Storage storage) => Initialise(config, storage);

        internal void Initialise(OsuConfigManager config, Storage storage, ILocalUserPlayInfo? gameplayState = null)
        {
            playInfo ??= gameplayState;
            models = storage.GetFullPath("ai-models/discovery/" + DiscoveryAudioModel.Version);
            cache = storage.GetFullPath("ai-audio-cache/" + DiscoveryAudioModel.Version);
            enabled = config.GetBindable<bool>(OsuSetting.ForkAiAudioRecommendations);
            device = config.GetBindable<DiscoveryAudioDevice>(OsuSetting.ForkAiAudioDevice);
            batch = config.GetBindable<int>(OsuSetting.ForkAiAudioCandidates);
            enabled.BindValueChanged(_ => Changed());
            device.BindValueChanged(_ => Changed());
            batch.BindValueChanged(_ => Changed());
            void Changed()
            {
                lock (sync)
                {
                    settingsGeneration.Cancel();
                    retired.Add(settingsGeneration);
                    settingsGeneration = new CancellationTokenSource();
                }
                Publish(Enabled ? DiscoveryStrings.AudioIdle : DiscoveryStrings.AudioDisabled);
                SettingsChanged?.Invoke();
            }
            Publish(Enabled ? DiscoveryStrings.AudioIdle : DiscoveryStrings.AudioDisabled);
        }

        internal async Task<IReadOnlyDictionary<int, double>> RankAsync(IReadOnlyList<APIBeatmap> seeds, IReadOnlyList<APIBeatmap> candidates,
                                                                        DiscoverySeedSource source, CancellationToken token, Action<int, double>? scoreReady = null)
        {
            CancellationToken settingToken;
            lock (sync)
            {
                if (stopping || !Enabled) throw new OperationCanceledException();
                settingToken = settingsGeneration.Token;
            }
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token, settingToken);
            token = cancel.Token;
            await owner.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!Enabled || stopping) throw new OperationCanceledException(token);
                Publish(DiscoveryStrings.AudioAnalysing);
                using var media = new DiscoveryMedia();
                var references = seeds.DistinctBy(s => s.OnlineBeatmapSetID).Take(8).ToArray();
                var maps = candidates.DistinctBy(s => s.OnlineBeatmapSetID).Take(CandidateLimit).ToArray();
                if (references.Length == 0) throw new InvalidDataException("Audio matching needs at least one reference.");
                var vectors = new Dictionary<int, float[]>();
                var scores = new Dictionary<int, double>();
                var referenceIds = references.Select(r => r.OnlineBeatmapSetID).ToHashSet();
                var mapIds = maps.Select(m => m.OnlineBeatmapSetID).ToHashSet();
                DiscoveryAudioModel? model = null;
                string? modelPath = null;
                // Dedicated owner so the priority can't leak to an unrelated thread-pool job.
                using var worker = new LowPriorityWorker();
                try
                {
                    // At most two anonymous preview downloads/decoders in parallel. One session owns inference.
                    foreach (var group in references.Concat(maps).DistinctBy(m => m.OnlineBeatmapSetID).Chunk(2))
                    {
                        token.ThrowIfCancellationRequested();
                        var prepared = await Task.WhenAll(group.Select(Prepare)).ConfigureAwait(false);
                        foreach (var item in prepared)
                        {
                            if (item.Error != null)
                            {
                                if (references.Any(s => s.OnlineBeatmapSetID == item.Id)) throw new InvalidDataException("Reference audio unavailable.", item.Error);
                                continue; // A failed candidate is unrated, not assigned a fake low embedding.
                            }
                            var vector = item.Vector;
                            if (vector == null)
                            {
                                if (EmbedForTest != null)
                                    vector = await EmbedForTest(item.Audio!, token).ConfigureAwait(false);
                                else
                                {
                                    if (modelPath == null)
                                    {
                                        // Avoid first-time bulk model traffic during an active map. The normal
                                        // path with installed weights keeps preparing recommendations throughout play.
                                        while (gameplayActive && !File.Exists(Path.Combine(models, DiscoveryAudioModel.FileName)))
                                        {
                                            Publish(AiFunctionsStrings.Waiting);
                                            await Task.Delay(1000, token).ConfigureAwait(false);
                                        }
                                        Publish(DiscoveryStrings.AudioLoading);
                                        modelPath = await DiscoveryAudioModel.Install(models, token).ConfigureAwait(false);
                                        await BackgroundAiRuntime.Ensure(Path.Combine(models, "runtime"), token, !gameplayActive && device.Value == DiscoveryAudioDevice.Auto).ConfigureAwait(false);
                                    }
                                    var sum = new float[DiscoveryAudioModel.Features];
                                    foreach (var window in item.Windows!)
                                    {
                                        token.ThrowIfCancellationRequested();
                                        // Do not compete with the game's renderer. One low-priority CPU thread
                                        // handles CLAP during play; switch at the next window boundary.
                                        var embedding = await worker.Run(() =>
                                        {
                                            token.ThrowIfCancellationRequested();
                                            var requested = gameplayActive ? DiscoveryAudioDevice.Cpu : device.Value;
                                            if (model == null || requested == DiscoveryAudioDevice.Cpu && model.Provider != "CPU")
                                            {
                                                model?.Dispose();
                                                model = null;
                                                try { model = new DiscoveryAudioModel(modelPath, requested); }
                                                catch (OnnxRuntimeException) when (requested == DiscoveryAudioDevice.Auto)
                                                {
                                                    model = new DiscoveryAudioModel(modelPath, DiscoveryAudioDevice.Cpu);
                                                }
                                            }
                                            Publish(DiscoveryStrings.AudioProcessing(model.Provider));
                                            return model.Infer(window, token);
                                        }).ConfigureAwait(false);
                                        for (int i = 0; i < sum.Length; i++) sum[i] += embedding[i] / item.Windows!.Length;
                                    }
                                    vector = DiscoveryAudioModel.Transform(sum);
                                }
                                ValidateVector(vector);
                                WriteCache(item.Hash!, vector);
                            }
                            vectors[item.Id] = vector;
                            if (referenceIds.All(vectors.ContainsKey))
                            {
                                // Normally the just-completed map; also handles a candidate that shares a reference set.
                                foreach (int id in mapIds.Where(id => !scores.ContainsKey(id) && vectors.ContainsKey(id)))
                                {
                                    token.ThrowIfCancellationRequested();
                                    var similarities = references.Select(r => (Similarity: Cosine(vectors[id], vectors[r.OnlineBeatmapSetID]),
                                        Weight: DiscoverySeedSelection.Weight(r, source))).OrderByDescending(r => r.Similarity).Take(2).ToArray();
                                    double score = .7 * similarities[0].Similarity + .3 * similarities.Sum(s => s.Similarity * s.Weight) / similarities.Sum(s => s.Weight);
                                    scores.Add(id, score);
                                    scoreReady?.Invoke(id, score);
                                }
                            }
                        }
                    }
                    Publish(DiscoveryStrings.AudioReady(model?.Provider ?? "cache"));
                    return scores;
                }
                finally { model?.Dispose(); }

                async Task<Prepared> Prepare(APIBeatmap map)
                {
                    try
                    {
                        byte[] audio = AudioForTest != null ? await AudioForTest(map.OnlineBeatmapSetID, token).ConfigureAwait(false)
                            : await media.LoadAudioAsync(map.OnlineBeatmapSetID, token).ConfigureAwait(false);
                        string hash = Convert.ToHexString(SHA256.HashData(audio));
                        float[]? cached = ReadCache(hash);
                        if (cached != null) return new Prepared(map.OnlineBeatmapSetID, hash, null, null, cached, null);
                        var windows = EmbedForTest == null ? await worker.Run(() => DiscoveryAudioDecoder.Decode(audio, token)).ConfigureAwait(false) : null;
                        return new Prepared(map.OnlineBeatmapSetID, hash, audio, windows, null, null);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Logger.Log($"Discovery audio preview unavailable: set={map.OnlineBeatmapSetID}, {e.GetType().Name}", LoggingTarget.Network);
                        return new Prepared(map.OnlineBeatmapSetID, null, null, null, null, e);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Publish(Enabled ? DiscoveryStrings.AudioIdle : DiscoveryStrings.AudioDisabled);
                throw;
            }
            catch
            {
                Publish(DiscoveryStrings.AudioFailed);
                throw;
            }
            finally
            {
                try
                {
                    lock (sync)
                    {
                        if (clearRequested)
                        {
                            if (Directory.Exists(cache)) Directory.Delete(cache, true);
                            clearRequested = false;
                        }
                        foreach (var old in retired) old.Dispose();
                        retired.Clear();
                    }
                }
                catch (IOException e) { Logger.Error(e, "Could not clear audio vector cache."); }
                finally { owner.Release(); }
            }
        }

        private sealed class LowPriorityWorker : IDisposable
        {
            private readonly System.Collections.Concurrent.BlockingCollection<Action> jobs = new System.Collections.Concurrent.BlockingCollection<Action>();
            private Thread? thread;

            internal Task<T> Run<T>(Func<T> action)
            {
                lock (jobs)
                {
                    if (thread == null)
                    {
                        thread = new Thread(() =>
                        {
                            // Linux priority is per thread; .NET Thread.Priority alone may not lower
                            // normal SCHED_OTHER threads. Only change this dedicated worker's nice.
                            if (OperatingSystem.IsLinux())
                            {
                                try { setpriority(0, 0, 10); }
                                catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
                            }
                            foreach (var job in jobs.GetConsumingEnumerable()) job();
                        }) { Name = "Mosu discovery audio", IsBackground = true, Priority = ThreadPriority.BelowNormal };
                        thread.Start();
                    }
                    var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                    jobs.Add(() =>
                    {
                        try { result.SetResult(action()); }
                        catch (Exception e) { result.SetException(e); }
                    });
                    return result.Task;
                }
            }

            public void Dispose()
            {
                jobs.CompleteAdding();
                thread?.Join();
                jobs.Dispose();
            }

            [System.Runtime.InteropServices.DllImport("libc")]
            private static extern int setpriority(int which, int who, int priority);
        }

        internal static double Cosine(float[] a, float[] b)
        {
            ValidateVector(a); ValidateVector(b);
            double dot = 0, aa = 0, bb = 0;
            for (int i = 0; i < a.Length; i++) { dot += a[i] * (double)b[i]; aa += a[i] * (double)a[i]; bb += b[i] * (double)b[i]; }
            return Math.Clamp(dot / Math.Sqrt(aa * bb), -1, 1);
        }

        private static void ValidateVector(float[] vector)
        {
            if (vector.Length != DiscoveryAudioModel.Features || vector.Any(v => !float.IsFinite(v)) || vector.All(v => v == 0))
                throw new InvalidDataException("Invalid cached audio vector.");
        }

        private float[]? ReadCache(string hash)
        {
            try
            {
                string path = Path.Combine(cache, hash + ".f32");
                if (!File.Exists(path) || new FileInfo(path).Length != DiscoveryAudioModel.Features * sizeof(float)) return null;
                byte[] bytes = File.ReadAllBytes(path);
                var result = new float[DiscoveryAudioModel.Features];
                Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
                ValidateVector(result);
                return result;
            }
            catch (Exception e) when (e is IOException or InvalidDataException) { return null; }
        }

        private void WriteCache(string hash, float[] vector)
        {
            Directory.CreateDirectory(cache);
            string target = Path.Combine(cache, hash + ".f32");
            byte[] bytes = new byte[vector.Length * sizeof(float)];
            Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(target + ".partial", bytes);
            File.Move(target + ".partial", target, true);
            // Small vector cache: <=2048 songs / ~6 MiB, never delete model assets or user's maps.
            foreach (var old in new DirectoryInfo(cache).GetFiles("*.f32").OrderByDescending(f => f.LastWriteTimeUtc).Skip(2048)) old.Delete();
        }

        public void ClearCache()
        {
            lock (sync)
            {
                settingsGeneration.Cancel(); retired.Add(settingsGeneration); settingsGeneration = new CancellationTokenSource();
                clearRequested = true;
            }
            _ = ClearWhenIdle();
            async Task ClearWhenIdle()
            {
                await owner.WaitAsync().ConfigureAwait(false);
                try
                {
                    lock (sync)
                    {
                        if (clearRequested && Directory.Exists(cache)) Directory.Delete(cache, true);
                        clearRequested = false;
                    }
                }
                catch (IOException e) { Logger.Error(e, "Could not clear audio vector cache."); }
                finally { owner.Release(); }
            }
            SettingsChanged?.Invoke();
        }

        private void Publish(LocalisableString text) => Schedule(() => { if (!IsDisposed) Status.Value = text; });

        protected override void Dispose(bool isDisposing)
        {
            lock (sync) { stopping = true; lifetime.Cancel(); settingsGeneration.Cancel(); }
            enabled?.UnbindAll(); device?.UnbindAll(); batch?.UnbindAll();
            base.Dispose(isDisposing);
        }

        private sealed record Prepared(int Id, string? Hash, byte[]? Audio, float[][]? Windows, float[]? Vector, Exception? Error);
    }
}
