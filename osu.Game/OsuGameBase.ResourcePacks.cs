using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Fonts;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game
{
    public partial class OsuGameBase
    {
        public GameResourcePackLibrary ResourcePacks { get; private set; } = null!;
        public BindableBool ResourcePackBusy { get; } = new BindableBool();
        public BindableInt ResourcePacksChanged { get; } = new BindableInt();
        public string LoadedResourcePack { get; private set; } = GameResourcePackLibrary.DefaultPack;
        public string SelectedResourcePack => LocalConfig.Get<string>(OsuSetting.GameResourcePack);
        private ResourceStore<byte[]> resourcePackCatalogue = null!;
        private GameResourcePackStore resourcePackStore = null!;
        private int resourcePackOperation;

        private void initialiseResourcePacks()
        {
            resourcePackCatalogue = new ResourceStore<byte[]>();
            // Independent immutable default chain: never point this catalogue back
            // at Resources, which will subsequently contain the imported store.
            foreach (string prefix in new[] { "TextureOverrides", "CompatibilityOverrides", "CounterOverrides", "MedalOverrides", "IconOverrides", "SoundOverrides" })
                resourcePackCatalogue.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), prefix));
            resourcePackCatalogue.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));
            resourcePackCatalogue.AddStore(new DllResourceStore(osu.Game.Resources.OsuResources.ResourceAssembly));
            resourcePackCatalogue.AddStore(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly));
            ResourcePacks = new GameResourcePackLibrary(Storage, resourcePackCatalogue);
            string selected = LocalConfig.Get<string>(OsuSetting.GameResourcePack);
            try
            {
                resourcePackStore = ResourcePacks.Open(selected);
                LoadedResourcePack = selected;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not load resource pack; using bundled resources.");
                resourcePackStore = new GameResourcePackStore();
                Schedule(() => BeatmapManager?.PostNotification?.Invoke(new SimpleErrorNotification { Text = GameResourceStrings.LoadFailed }));
            }
            // Called before the bundled game stores and all texture/audio/font consumers.
            // Framework-only resources keep their existing priority.
            Resources.AddStore(resourcePackStore);
            RegisterImportHandler(new ResourcePackImporter(this));
        }

        private void loadResourcePackSoundSelection()
        {
            string previous = LocalConfig.Get<string>(OsuSetting.AppliedGameResourceSounds);
            if (previous == LoadedResourcePack) return;
            if (previous.Length > 0)
                LocalConfig.SetValue(OsuSetting.InterfaceSoundPack, LocalConfig.Get<string>(OsuSetting.GameResourceSoundBaseline));
            if (LoadedResourcePack != GameResourcePackLibrary.DefaultPack)
            {
                LocalConfig.SetValue(OsuSetting.GameResourceSoundBaseline, LocalConfig.Get<string>(OsuSetting.InterfaceSoundPack));
                LocalConfig.SetValue(OsuSetting.InterfaceSoundPack, osu.Game.Audio.InterfaceSoundLibrary.DefaultPack);
                LocalConfig.SetValue(OsuSetting.AppliedGameResourceSounds, LoadedResourcePack);
            }
            else LocalConfig.SetValue(OsuSetting.AppliedGameResourceSounds, "");
        }

        private void loadResourcePackFonts()
        {
            string previous = LocalConfig.Get<string>(OsuSetting.AppliedGameResourceFonts);
            if (previous != LoadedResourcePack && previous.Length > 0)
            {
                var baseline = Newtonsoft.Json.JsonConvert.DeserializeObject<FontConfiguration>(LocalConfig.Get<string>(OsuSetting.GameResourceFontBaseline));
                if (baseline?.IsValid() == true) baseline.Apply(LocalConfig);
                LocalConfig.SetValue(OsuSetting.AppliedGameResourceFonts, "");
            }
            var pack = ResourcePacks.ReadFonts(LoadedResourcePack);
            if (pack == null) return;
            fontPackLibrary.Install(pack);
            registerFontPack(pack);
            if (previous != LoadedResourcePack)
            {
                LocalConfig.SetValue(OsuSetting.GameResourceFontBaseline, Newtonsoft.Json.JsonConvert.SerializeObject(FontConfiguration.Capture(LocalConfig)));
                pack.InstalledSettings().Apply(LocalConfig);
                LocalConfig.SetValue(OsuSetting.AppliedGameResourceFonts, LoadedResourcePack);
            }
        }

        public void SelectResourcePack(string id)
        {
            if (ResourcePackBusy.Value) return;
            if (id != GameResourcePackLibrary.DefaultPack && !ResourcePacks.Installed().Any(p => p.Id == id))
                throw new InvalidDataException("Unknown resource pack.");
            LocalConfig.SetValue(OsuSetting.GameResourcePack, id);
            ResourcePacksChanged.Value++;
            BeatmapManager?.PostNotification?.Invoke(new SimpleNotification
            {
                Text = id == LoadedResourcePack ? GameResourceStrings.Selected : GameResourceStrings.RestartRequired,
            });
        }

        public Task<string?> ExportResourcePack() => resourcePackOperationAsync(null, null);
        public Task<string?> ImportResourcePack(Stream input, string name) => resourcePackOperationAsync(input, name);

        private async Task<string?> resourcePackOperationAsync(Stream? input, string? name)
        {
            if (Interlocked.CompareExchange(ref resourcePackOperation, 1, 0) != 0)
            {
                Schedule(() => BeatmapManager?.PostNotification?.Invoke(new SimpleErrorNotification { Text = GameResourceStrings.Busy }));
                return null;
            }
            bool importing = input != null;
            var notification = new ProgressNotification
            {
                Text = importing ? GameResourceStrings.Importing : GameResourceStrings.Exporting,
                State = ProgressNotificationState.Active,
            };
            // Capture mutable UI settings on Update, never enumerate live bindables in a worker.
            var settings = FontConfiguration.Capture(LocalConfig);
            var fonts = fontPacks.ToArray();
            string selectedSoundPack = interfaceSounds?.SelectedId ?? osu.Game.Audio.InterfaceSoundLibrary.DefaultPack;
            Schedule(() =>
            {
                ResourcePackBusy.Value = true;
                BeatmapManager?.PostNotification?.Invoke(notification);
            });
            try
            {
                var token = notification.CancellationToken;
                if (importing)
                {
                    var pack = await Task.Run(() => ResourcePacks.Install(input!, p => notification.Progress = p * 0.98f, token)).ConfigureAwait(false);
                    var selected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    Schedule(() =>
                    {
                        try
                        {
                            LocalConfig.SetValue(OsuSetting.GameResourcePack, pack.Id);
                            ResourcePacksChanged.Value++;
                            notification.Progress = 1;
                            notification.CompletionText = GameResourceStrings.Imported;
                            notification.State = ProgressNotificationState.Completed;
                            selected.SetResult();
                        }
                        catch (Exception ex) { selected.SetException(ex); }
                    });
                    await selected.Task.ConfigureAwait(false);
                    return pack.Id;
                }
                string filename = "resources-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6] + GameResourcePackLibrary.Extension;
                var exports = Storage.GetStorageForDirectory("exports");
                await Task.Run(() =>
                {
                    try
                    {
                        var extra = resourcePackNotices();
                        using (var fontArchive = new MemoryStream())
                        {
                            FontPackExporter.Export(fontArchive, settings, Storage, fonts, GameResourcePackStore.Borrow(resourcePackStore));
                            extra.Add("fonts.morafn", fontArchive.ToArray());
                        }
                        using var effective = new ResourceStore<byte[]>();
                        if (interfaceSounds != null)
                            effective.AddStore(new EffectiveInterfaceSounds(interfaceSounds, selectedSoundPack));
                        effective.AddStore(GameResourcePackStore.Borrow(resourcePackStore));
                        effective.AddStore(GameResourcePackStore.Borrow(resourcePackCatalogue));
                        using var output = exports.GetStream(filename, FileAccess.ReadWrite, FileMode.CreateNew);
                        ResourcePacks.Export(output, "Morasooma", effective, extra, p => notification.Progress = p * 0.65f, token);
                        ResourcePacks.Validate(output, p => notification.Progress = 0.65f + p * 0.34f, token);
                        token.ThrowIfCancellationRequested();
                    }
                    catch
                    {
                        if (exports.Exists(filename)) exports.Delete(filename);
                        throw;
                    }
                }).ConfigureAwait(false);
                Schedule(() =>
                {
                    notification.Progress = 1;
                    notification.CompletionText = GameResourceStrings.Exported;
                    notification.CompletionClickAction = () => exports.PresentFileExternally(filename);
                    notification.State = ProgressNotificationState.Completed;
                });
                return exports.GetFullPath(filename);
            }
            catch (OperationCanceledException)
            {
                Schedule(() => notification.State = ProgressNotificationState.Cancelled);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Resource pack operation failed.");
                Schedule(() =>
                {
                    notification.CompleteSilently();
                    BeatmapManager?.PostNotification?.Invoke(new SimpleErrorNotification
                    {
                        Text = importing ? GameResourceStrings.ImportFailed : GameResourceStrings.ExportFailed,
                    });
                });
                return null;
            }
            finally
            {
                Schedule(() =>
                {
                    ResourcePackBusy.Value = false;
                    Interlocked.Exchange(ref resourcePackOperation, 0);
                });
            }
        }

        private Dictionary<string, byte[]> resourcePackNotices()
        {
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var assembly = typeof(Morasooma.Resources.ResourceAssembly).Assembly;
            using var own = new DllResourceStore(assembly);
            // DllResourceStore's generic name enumeration treats dots inside a
            // filename (CC0-1.0.txt) as directories. Read each exact manifest name
            // rather than round-tripping that lossy name through Get().
            var manifestNames = assembly.GetManifestResourceNames();
            var paths = own.GetAvailableResources().ToArray();
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                byte[] readNotice()
                {
                    using var input = assembly.GetManifestResourceStream(manifestNames[i])!;
                    return GameResourcePackLibrary.ReadBounded(input, 4 * 1024 * 1024);
                }
                if (path == "LICENSE") result["Notices/Morasooma/LICENSE.txt"] = readNotice();
                else if (path == "THIRD-PARTY-NOTICES.md" || path.StartsWith("Licenses/", StringComparison.Ordinal) && !path.StartsWith("Licenses/Shaders/", StringComparison.Ordinal)
                         || path.EndsWith("-manifest.json", StringComparison.Ordinal) && path != "shader-manifest.json"
                         || path.StartsWith("Fonts/", StringComparison.Ordinal) && Path.GetExtension(path) is ".txt" or ".md")
                    result["Notices/Morasooma/" + path.Replace("CC0-1/0.txt", "CC0-1.0.txt", StringComparison.Ordinal)] = readNotice();
            }
            // Retained upstream flags/fork artwork are NOT relicensed by the wrapper.
            result["Notices/Retained/osu-resources-LICENCE.md"] = own.Get("Licenses/ResourcePacks/osu-resources-LICENCE.md");
            result["Notices/Retained/Twemoji-NOTICE.txt"] = own.Get("Licenses/ResourcePacks/Twemoji-NOTICE.txt");
            result["Notices/Retained/NOTICE.txt"] = System.Text.Encoding.UTF8.GetBytes(
                "This export is a snapshot of the installed client resources, not a whole-client commercial-clearance claim. " +
                "Retained flags, fork/owner artwork and user-imported resources retain their own terms. " +
                "No blanket Morasooma ownership or permission to redistribute third-party/user material is granted. " +
                "Shaders, code, online caches, beatmaps, replays and personal skin libraries are excluded.\n");
            ResourcePacks.CopyNotices(LoadedResourcePack, result);
            interfaceSounds?.CopyExportNotices(result);
            return result;
        }

        private sealed class EffectiveInterfaceSounds : IResourceStore<byte[]>
        {
            private readonly Audio.InterfaceSoundManager sounds;
            private readonly Audio.InterfaceSoundPack? pack;
            public EffectiveInterfaceSounds(Audio.InterfaceSoundManager sounds, string selected)
            {
                this.sounds = sounds;
                pack = sounds.Library.Find(selected);
            }
            public byte[] Get(string name)
            {
                if (!name.StartsWith("Samples/", StringComparison.Ordinal) || Path.GetExtension(name) is not (".wav" or ".mp3")) return null!;
                string key = name[8..^Path.GetExtension(name).Length];
                if (pack?.Sounds.TryGetValue(key, out string? blob) != true || blob == null) return null!;
                try { return sounds.Library.ReadSound(blob); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { return null!; }
            }
            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));
            public Stream GetStream(string name) => Get(name) is byte[] bytes ? new MemoryStream(bytes, false) : null!;
            public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();
            public void Dispose() { }
        }

        private sealed class ResourcePackImporter : ICanAcceptFiles
        {
            private readonly OsuGameBase game;
            public ResourcePackImporter(OsuGameBase game) => this.game = game;
            public IEnumerable<string> HandledExtensions => new[] { GameResourcePackLibrary.Extension };
            public Task Import(params string[] paths) => Import(paths.Select(p => new ImportTask(p)).ToArray());
            public async Task Import(ImportTask[] tasks, ImportParameters parameters = default)
            {
                foreach (var task in tasks)
                {
                    try
                    {
                        using var stream = task.Stream ?? File.OpenRead(task.Path);
                        // Existing file import dispatch runs on a worker; enter the game
                        // scheduler before reading settings or creating notifications.
                        var done = new TaskCompletionSource<Task<string?>>(TaskCreationOptions.RunContinuationsAsynchronously);
                        game.Schedule(() =>
                        {
                            try { done.SetResult(game.ImportResourcePack(stream, Path.GetFileNameWithoutExtension(task.Path))); }
                            catch (Exception ex) { done.SetException(ex); }
                        });
                        await (await done.Task.ConfigureAwait(false)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Could not open resource package.");
                        game.Schedule(() => game.BeatmapManager?.PostNotification?.Invoke(new SimpleErrorNotification { Text = GameResourceStrings.ImportFailed }));
                    }
                }
            }
        }
    }
}
