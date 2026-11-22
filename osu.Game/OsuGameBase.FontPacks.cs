using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Database;
using osu.Game.Graphics.Fonts;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game
{
    public partial class OsuGameBase
    {
        private readonly HashSet<string> loadedFontPacks = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<FontPackLibrary.Validated> fontPacks = new List<FontPackLibrary.Validated>();
        private FontPackLibrary fontPackLibrary = null!;

        private void initialiseFontPacks()
        {
            fontPackLibrary = new FontPackLibrary(Storage);
            foreach (var pack in fontPackLibrary.Installed())
                registerFontPack(pack);
            RegisterImportHandler(new FontImporter(this));
        }

        private void registerFontPack(FontPackLibrary.Validated pack)
        {
            if (!loadedFontPacks.Add(pack.Id)) return;
            var store = new ResourceStore<byte[]>(new FontPackResourceStore(pack.Files));
            var fonts = new MosuFontStore(Host.Renderer);
            // Attach first so RawCachingGlyphStore receives the parent's cache storage.
            Fonts.AddStore(fonts);
            foreach (var family in pack.Manifest.Families)
            {
                string name = pack.InstalledFamily(family.Name);
                fonts.SetMetricsFamily(name, family.MetricsFamily);
                if (family.LatinHeadingFamily != null)
                    fonts.SetLatinHeadingFamily(name, pack.InstalledFamily(family.LatinHeadingFamily));
                // Noto uses several disjoint subsets rather than style weights.
                if (family.MetricsFamily == "Noto")
                {
                    foreach (var face in family.Faces)
                        fonts.AddTextureSource(new FontPackGlyphStore(store, face.Asset[..^4], name + "-" + face.Weight,
                            Host.CreateTextureLoaderStore(store)));
                    if (!AvailableCustomUIFonts.Contains(name)) AvailableCustomUIFonts.Add(name);
                    continue;
                }
                if (family.MetricsFamily is "Torus" or "Torus-Alternate" or "Venera")
                {
                    foreach (var face in family.Faces)
                        fonts.AddTextureSource(new FontPackGlyphStore(store, face.Asset[..^4], name + "-" + face.Weight,
                            Host.CreateTextureLoaderStore(store)));
                    if (!AvailableCustomUIFonts.Contains(name)) AvailableCustomUIFonts.Add(name);
                    continue;
                }
                foreach (string weight in new[] { "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" })
                foreach (string italic in new[] { "", "Italic" })
                {
                    string requested = weight + italic;
                    string fallbackWeight = family.MetricsFamily == "Michroma" ? "Regular"
                        : family.MetricsFamily == "Syncopate" ? Array.IndexOf(new[] { "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" }, weight) >= 4 ? "Bold" : "Regular"
                        : weight == "Black" ? "Bold" : weight == "Medium" && family.MetricsFamily == "Inter" ? "Regular" : weight;
                    var face = family.Faces.FirstOrDefault(f => f.Weight == requested)
                               ?? family.Faces.FirstOrDefault(f => f.Weight == fallbackWeight)
                               ?? family.Faces.FirstOrDefault(f => f.Weight == "Regular") ?? family.Faces[0];
                    fonts.AddTextureSource(new FontPackGlyphStore(store, face.Asset[..^4], name + "-" + requested,
                        Host.CreateTextureLoaderStore(store)));
                }
                if (!AvailableCustomUIFonts.Contains(name)) AvailableCustomUIFonts.Add(name);
            }
            LocalConfig.RegisterFontPackFamilies(pack.Manifest.Families.Select(f => pack.InstalledFamily(f.Name)));
            fontPacks.Add(pack);
        }

        public string FontPackDisplayName(string installedFamily)
        {
            foreach (var pack in fontPacks)
                foreach (var family in pack.Manifest.Families)
                    if (pack.InstalledFamily(family.Name) == installedFamily)
                    {
                        string displayName = family.Name.StartsWith("morafn-", StringComparison.Ordinal) ? family.MetricsFamily : family.Name;
                        return FontConfiguration.DisplayFamilyName(displayName) + " · " + pack.Id[..6];
                    }
            return installedFamily;
        }

        public async Task ImportFontPack(Stream source)
        {
            var pack = await Task.Run(() => FontPackLibrary.Validate(source)).ConfigureAwait(false);
            await Task.Run(() => fontPackLibrary.Install(pack)).ConfigureAwait(false);
            var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Schedule(() =>
            {
                try
                {
                    registerFontPack(pack);
                    pack.InstalledSettings().Apply(LocalConfig);
                    BeatmapManager?.PostNotification?.Invoke(new SimpleNotification { Text = FontSettingsStrings.Imported });
                    applied.SetResult();
                }
                catch (Exception ex) { applied.SetException(ex); }
            });
            await applied.Task.ConfigureAwait(false);
        }

        public async Task<string> ExportFontPack()
        {
            var settings = FontConfiguration.Capture(LocalConfig);
            var installed = fontPacks.ToArray();
            return await Task.Run(() =>
            {
                var export = Storage.GetStorageForDirectory("exports");
                string name = "fonts-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6] + FontPackLibrary.EXTENSION;
                using var archive = new MemoryStream();
                FontPackExporter.Export(archive, settings, Storage, installed);
                using (var output = export.GetStream(name, FileAccess.Write, FileMode.CreateNew))
                {
                    archive.Position = 0;
                    archive.CopyTo(output);
                }
                return export.GetFullPath(name);
            }).ConfigureAwait(false);
        }

        private sealed class FontImporter : ICanAcceptFiles
        {
            private readonly OsuGameBase game;
            public FontImporter(OsuGameBase game) => this.game = game;
            public IEnumerable<string> HandledExtensions => new[] { FontPackLibrary.EXTENSION };
            public Task Import(params string[] paths) => Import(paths.Select(p => new ImportTask(p)).ToArray());
            public async Task Import(ImportTask[] tasks, ImportParameters parameters = default)
            {
                foreach (var task in tasks)
                {
                    try
                    {
                        using var stream = task.Stream ?? File.OpenRead(task.Path);
                        await game.ImportFontPack(stream).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        osu.Framework.Logging.Logger.Error(ex, "Could not import .morafn package.");
                        game.Schedule(() => game.BeatmapManager?.PostNotification?.Invoke(new SimpleErrorNotification { Text = FontSettingsStrings.Failed }));
                    }
                }
            }
        }
    }
}
