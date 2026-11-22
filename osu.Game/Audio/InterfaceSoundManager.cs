using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;

namespace osu.Game.Audio
{
    /// <summary>
    /// Resolves only explicitly known interface events. Gameplay, beatmap, skin and music
    /// stores are untouched. Pack IO/preparation never happens in a Play/GetChannel call.
    /// </summary>
    public sealed class InterfaceSoundManager : IDisposable
    {
        private readonly AudioManager audio;
        private readonly IResourceStore<byte[]> resources;
        private readonly Storage storage;
        private readonly Bindable<string> selected;
        private readonly ISampleStore sources;
        private readonly SoundResourceStore soundData = new SoundResourceStore();
        private readonly SampleVirtual silence = new SampleVirtual("custom/silence");
        private readonly object preparationLock = new object();
        private readonly Func<string, Sample, Sample> transform;
        private readonly Func<string, Sample, Sample>? previousTransform;
        private IReadOnlyDictionary<string, Sample> snapshot = new Dictionary<string, Sample>();
        private HashSet<string> sourceNames = new HashSet<string>(StringComparer.Ordinal);
        private int generation;
        private int changeVersion;
        private bool disposed;

        public InterfaceSoundLibrary Library { get; }
        public IReadOnlyList<string> Events { get; }
        public BindableInt Changed { get; } = new BindableInt();
        public Task Ready { get; private set; } = Task.CompletedTask;
        public string SelectedId => selected.Value;

        public InterfaceSoundManager(Storage storage, OsuConfigManager config, AudioManager audio, IResourceStore<byte[]> resources)
        {
            this.audio = audio;
            this.resources = resources;
            this.storage = storage;
            Events = resources.GetAvailableResources()
                              .Where(p => p.StartsWith("Samples/", StringComparison.Ordinal))
                              .Select(p => p[8..])
                              .Where(p => Path.GetExtension(p) is ".wav" or ".mp3")
                              .Select(p => p[..^Path.GetExtension(p).Length])
                              .Where(IsInterfaceEvent)
                              .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Library = new InterfaceSoundLibrary(storage, Events);
            sources = audio.GetSampleStore(soundData);
            sources.PlaybackConcurrency = OsuGameBase.SAMPLE_CONCURRENCY;
            previousTransform = audio.GlobalSampleTransform;
            transform = wrap;
            audio.GlobalSampleTransform = transform;
            selected = config.GetBindable<string>(OsuSetting.InterfaceSoundPack);
            if (selected.Value == "lazer-2017") selected.Value = InterfaceSoundLibrary.DefaultPack;
            selected.BindValueChanged(_ => Reload());
            Reload();
        }

        public static bool IsInterfaceEvent(string name) => name is not
            ("Intro/welcome" or "Intro/seeya" or "Intro/Welcome/welcome" or "Intro/Welcome/seeya") && name.Split('/')[0] is
            ("UI" or "Keyboard" or "Menu" or "SongSelect" or "SongSelectClassic" or
             "Intro" or "Results" or "Multiplayer" or "MedalSplash" or "DailyChallenge");

        private Sample wrap(string name, Sample original)
        {
            if (!IsInterfaceEvent(name))
                return previousTransform?.Invoke(name, original) ?? original;
            return new InterfaceSoundSample(original, () => Volatile.Read(ref snapshot).TryGetValue(name, out Sample? replacement) && replacement.IsLoaded
                ? replacement
                : original);
        }

        public void Select(string id) => selected.Value = id == "lazer-2017" ? InterfaceSoundLibrary.DefaultPack : id;

        public async Task<string> Export()
        {
            string id = SelectedId;
            return await Task.Run(() =>
            {
                using var archive = new MemoryStream();
                // All audible built-in interface defaults are now replacements.
                // Include their actual CC0 texts/credits, not a licence for upstream
                // recordings absent from this snapshot. Historical custom-pack notices
                // (including upstream licences) still travel on re-export in Library.
                using var bundled = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
                var notices = new List<byte[]> { bundled.Get("LICENSE"), bundled.Get("THIRD-PARTY-NOTICES.md") };
                notices.AddRange(bundled.GetAvailableResources()
                                       .Where(p => p.StartsWith("Licenses/Sounds/", StringComparison.Ordinal)
                                                   && Path.GetExtension(p) is ".txt" or ".md")
                                       .OrderBy(p => p, StringComparer.Ordinal).Select(bundled.Get));
                Library.Export(archive, id, key =>
                {
                    foreach (string extension in new[] { ".wav", ".mp3" })
                    {
                        byte[]? bytes = resources.Get("Samples/" + key + extension);
                        if (bytes != null) return (key + extension, bytes);
                    }
                    throw new InvalidDataException("Missing built-in sound: " + key);
                }, notices);
                var exports = storage.GetStorageForDirectory("exports");
                string name = "sounds-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6] + InterfaceSoundLibrary.Extension;
                using (var output = exports.GetStream(name, FileAccess.Write, FileMode.CreateNew))
                {
                    archive.Position = 0;
                    archive.CopyTo(output);
                }
                return exports.GetFullPath(name);
            }).ConfigureAwait(false);
        }

        public void CopyExportNotices(Dictionary<string, byte[]> destination)
        {
            foreach (string notice in Library.Find(SelectedId)?.Notices ?? Enumerable.Empty<string>())
                destination["Notices/InterfaceSounds/" + notice] = Library.ReadNotice(notice);
        }

        public void Reload()
        {
            string id = selected.Value;
            int version = Interlocked.Increment(ref generation);
            Ready = Task.Run(() =>
            {
                try
                {
                    prepare(id, version);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
                {
                    Volatile.Write(ref snapshot, new Dictionary<string, Sample>());
                    Logger.Log("Interface sound pack could not be prepared; using built-in sounds.", level: LogLevel.Important);
                }
            });
        }

        private void prepare(string id, int version)
        {
            lock (preparationLock)
            {
                if (disposed || version != generation)
                    return;
                if (id == InterfaceSoundLibrary.DefaultPack)
                {
                    Volatile.Write(ref snapshot, new Dictionary<string, Sample>());
                    foreach (string retired in sourceNames)
                        sources.Invalidate(retired);
                    soundData.Clear();
                    sourceNames.Clear();
                    Changed.Value = Interlocked.Increment(ref changeVersion);
                    return;
                }
                var replacements = new Dictionary<string, Sample>(StringComparer.Ordinal);
                var needed = new HashSet<string>(StringComparer.Ordinal);
                var pack = Library.Find(id);
                double loadedBytes = 0;
                var loadedBlobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (string key in Events)
                {
                    byte[]? bytes = null;
                    string? source = null;
                    if (pack?.Sounds.TryGetValue(key, out string? blob) == true)
                    {
                        try
                        {
                            if (!loadedBlobs.TryGetValue(blob, out bytes))
                            {
                                bytes = Library.ReadSound(blob);
                                loadedBytes += InterfaceSoundLibrary.ValidateAudio(blob, bytes);
                                if (loadedBytes > InterfaceSoundLibrary.MaximumPackBytes)
                                    throw new InvalidDataException("Sound pack is too large.");
                                loadedBlobs.Add(blob, bytes);
                            }
                            source = "custom/" + blob;
                        }
                        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
                        {
                            Logger.Log($"Interface sound {key} could not be loaded; using the base pack.", level: LogLevel.Important);
                        }
                    }
                    if (source == null || bytes == null)
                        continue;
                    // BASS cannot load a zero-frame WAV. It is deliberate silence,
                    // not a decode failure that should play the audible default.
                    if (InterfaceSoundLibrary.IsIntentionalSilence(source, bytes))
                    {
                        replacements[key] = silence;
                        continue;
                    }
                    needed.Add(source);
                    soundData.Put(source, bytes);
                    Sample? sample = sources.Get(source);
                    if (sample != null)
                        replacements[key] = sample;
                }

                // Factories are prepared off-thread. Existing in-flight channels keep their own
                // sample/factory references; invalidating retired names does not stop those channels.
                if (disposed || version != generation)
                {
                    // A newer selection will retire these factories as well, even when this
                    // preparation was superseded before its snapshot became visible.
                    sourceNames.UnionWith(needed);
                    soundData.Clear();
                    return;
                }
                Volatile.Write(ref snapshot, replacements);
                foreach (string retired in sourceNames.Except(needed))
                    sources.Invalidate(retired);
                soundData.Clear();
                sourceNames = needed;
                Changed.Value = Interlocked.Increment(ref changeVersion);
            }
        }

        public void Dispose()
        {
            lock (preparationLock)
            {
                disposed = true;
                selected.UnbindAll();
                if (audio.GlobalSampleTransform == transform)
                    audio.GlobalSampleTransform = previousTransform;
                sources.Dispose();
                silence.Dispose();
                soundData.Dispose();
            }
        }

        private sealed class SoundResourceStore : IResourceStore<byte[]>
        {
            private readonly Dictionary<string, byte[]> data = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            public void Put(string name, byte[] bytes) => data[name] = bytes;
            public byte[] Get(string name) => data.GetValueOrDefault(name)!;
            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));
            public Stream? GetStream(string name) => Get(name) is byte[] bytes ? new MemoryStream(bytes, false) : null;
            public IEnumerable<string> GetAvailableResources() => data.Keys.ToArray();
            public void Clear() => data.Clear();
            public void Dispose() => Clear();
        }
    }
}
