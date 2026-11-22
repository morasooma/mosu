using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.Platform;
using SharpFNT;

namespace osu.Game.Graphics.Fonts
{
    internal static class FontPackExporter
    {
        public static void Export(Stream output, FontConfiguration settings, Storage storage, IEnumerable<FontPackLibrary.Validated> installed,
                                  osu.Framework.IO.Stores.IResourceStore<byte[]>? resourceOverrides = null)
        {
            if (!settings.IsValid()) throw new InvalidDataException("Invalid font settings.");
            settings = JsonConvert.DeserializeObject<FontConfiguration>(JsonConvert.SerializeObject(settings))!;
            var pack = new FontPackLibrary.Pack { Settings = settings };
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            using var bundled = new MosuFontResources(resourceOverrides);
            var installedArray = installed.ToArray();
            var families = new HashSet<string>(settings.Families.Where(f => f != "Default"), StringComparer.Ordinal);
            // Default assignments must be portable too, not just references to whichever fonts the receiver has.
            if (settings.Separate)
            {
                if (settings.Main == "Default") families.Add("Torus");
                if (settings.Alternate == "Default") families.Add("Torus-Alternate");
                if (settings.Numeric == "Default") families.Add("Venera");
                if (settings.Inter == "Default") families.Add("Inter");
            }
            else if (settings.Common == "Default")
                families.UnionWith(new[] { "Torus", "Torus-Alternate", "Venera", "Inter" });

            if (families.Contains("Torus-Alternate")) families.Add("Nunito");
            // Preserve imported composite heading dependencies too.
            foreach (var imported in installedArray)
                foreach (var family in imported.Manifest.Families)
                    if (families.Contains(imported.InstalledFamily(family.Name)) && family.LatinHeadingFamily != null)
                        families.Add(imported.InstalledFamily(family.LatinHeadingFamily));

            // Resolve defaults in the portable copy, otherwise the archive would
            // carry atlases that the receiver never selects.
            if (!settings.Separate && settings.Common == "Default") settings.Separate = true;
            if (settings.Separate)
            {
                if (settings.Main == "Default") settings.Main = "Torus";
                if (settings.Alternate == "Default") settings.Alternate = "Torus-Alternate";
                if (settings.Numeric == "Default") settings.Numeric = "Venera";
                if (settings.Inter == "Default") settings.Inter = "Inter";
            }

            foreach (string name in families)
            {
                var family = new FontPackLibrary.Family { Name = name, MetricsFamily = "Custom" };
                string folder = "fonts/f" + pack.Families.Count;
                var imported = installedArray.FirstOrDefault(p => p.Manifest.Families.Any(f => p.InstalledFamily(f.Name) == name));
                if (imported != null)
                {
                    var original = imported.Manifest.Families.First(f => imported.InstalledFamily(f.Name) == name);
                    family.MetricsFamily = original.MetricsFamily;
                    family.LatinHeadingFamily = original.LatinHeadingFamily == null ? null : imported.InstalledFamily(original.LatinHeadingFamily);
                    foreach (var face in original.Faces)
                        addFace(family, face.Weight, imported.Files[face.Asset],
                            page => imported.Files[FontPackLibrary.PageName(face.Asset, page, pageCount(imported.Files[face.Asset]))], folder, files);
                    foreach (var notice in original.Notices)
                        addNotice(family, imported.Files[notice], folder, files);
                }
                else if (OsuGameBase.BuiltInCustomUIFonts.Contains(name) && name != "Default")
                {
                    family.MetricsFamily = name;
                    if (name == "Torus-Alternate") family.LatinHeadingFamily = "Nunito";
                    string realFamily = name switch { "Torus" => "Nunito", "Torus-Alternate" => "MosuRoundedAlt", "Venera" => "Michroma", _ => name };
                    var assets = name is "Torus" or "Torus-Alternate" or "Venera"
                        ? MosuFontResources.LegacyFaces.Keys.Where(a => a.StartsWith("Fonts/" + name + "/", StringComparison.Ordinal))
                        : bundled.GetFontAssets().Where(a => a.StartsWith("Fonts/" + name + "/", StringComparison.Ordinal));
                    foreach (string asset in assets)
                    {
                        string weight = asset[(asset.LastIndexOf('/') + 1 + name.Length + 1)..];
                        addFace(family, weight, bundled.Get(asset + ".bin"),
                            page => bundled.Get(FontPackLibrary.PageName(asset + ".bin", page, pageCount(bundled.Get(asset + ".bin")))), folder, files);
                    }
                    foreach (string asset in bundled.GetAvailableResources().Where(a => a.StartsWith("Fonts/" + realFamily + "/", StringComparison.Ordinal)
                                                                                      && !a.EndsWith(".bin", StringComparison.Ordinal) && !a.EndsWith(".png", StringComparison.Ordinal)))
                        addNotice(family, bundled.Get(asset), folder, files);
                    if (name == "Venera")
                    {
                        foreach (string asset in bundled.GetAvailableResources().Where(a => a.StartsWith("Fonts/Syncopate/", StringComparison.Ordinal)
                                                                                          && !a.EndsWith(".bin", StringComparison.Ordinal) && !a.EndsWith(".png", StringComparison.Ordinal)))
                            addNotice(family, bundled.Get(asset), folder, files);
                    }
                }
                else if (name.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase))
                {
                    var fonts = storage.GetStorageForDirectory("Fonts");
                    using var input = fonts.GetStream(name) ?? throw new InvalidDataException("Missing custom font.");
                    var font = BitmapFont.FromStream(input, false);
                    using var binary = new MemoryStream();
                    using (var writer = new BinaryWriter(binary, Encoding.UTF8, true)) font.WriteBinary(writer);
                    addFace(family, "Regular", binary.ToArray(), page =>
                    {
                        string suffix = "_" + page.ToString().PadLeft((font.Pages.Count - 1).ToString().Length, '0') + ".png";
                        using var stream = fonts.GetStream(name + suffix) ?? throw new InvalidDataException("Missing custom atlas.");
                        return readBounded(stream);
                    }, folder, files);
                    foreach (string notice in fonts.GetFiles(string.Empty, "*.txt").Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(Path.GetFileNameWithoutExtension(name), StringComparison.OrdinalIgnoreCase)))
                    {
                        using var stream = fonts.GetStream(notice);
                        addNotice(family, readBounded(stream), folder, files);
                    }
                }
                else throw new InvalidDataException("Font cannot be exported: " + name);
                if (family.Faces.Count == 0) throw new InvalidDataException("Font has no exportable faces: " + name);
                pack.Families.Add(family);
            }
            files["pack.json"] = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pack, Formatting.Indented));
            using var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true))
                foreach (var file in files)
                {
                    using var stream = zip.CreateEntry(file.Key, CompressionLevel.Fastest).Open();
                    stream.Write(file.Value);
                }
            archive.Position = 0;
            // The producer must obey the exact same limits as the consumer.
            _ = FontPackLibrary.Validate(archive);
            archive.Position = 0;
            archive.CopyTo(output);
        }

        private static void addFace(FontPackLibrary.Family family, string weight, byte[] descriptor, Func<int, byte[]> page, string folder, Dictionary<string, byte[]> files)
        {
            string asset = folder + "/" + weight + ".bin";
            // Convert empty legacy whitespace rectangles into transparent texels for GlyphStore.
            using var input = new MemoryStream(descriptor);
            var font = BitmapFont.FromStream(input, FormatHint.Binary, false);
            var pages = Enumerable.Range(0, font.Pages.Count).Select(page).ToArray();
            for (int i = 0; i < pages.Length; i++)
                files[FontPackLibrary.PageName(asset, i, pages.Length)] = pages[i];
            foreach (var glyph in font.Characters.Values)
            {
                if (glyph.Width != 0 && glyph.Height != 0) continue;
                // Existing source PNGs have a transparent border; verify it before reuse.
                using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(pages[0]);
                bool found = false;
                for (int y = 0; y < image.Height && !found; y++)
                    for (int x = 0; x < image.Width && !found; x++)
                        if (image[x, y].A == 0)
                        {
                            glyph.X = x; glyph.Y = y; glyph.Page = 0; glyph.Width = glyph.Height = 1; found = true;
                        }
                if (!found) throw new InvalidDataException("No empty texel for font whitespace.");
            }
            using var binary = new MemoryStream();
            using (var writer = new BinaryWriter(binary, Encoding.UTF8, true)) font.WriteBinary(writer);
            files[asset] = binary.ToArray();
            family.Faces.Add(new FontPackLibrary.Face { Weight = weight, Asset = asset });
        }

        private static void addNotice(FontPackLibrary.Family family, byte[] bytes, string folder, Dictionary<string, byte[]> files)
        {
            string path = folder + "/notice-" + family.Notices.Count + ".txt";
            files[path] = bytes;
            family.Notices.Add(path);
        }
        private static int pageCount(byte[] descriptor)
        {
            using var input = new MemoryStream(descriptor);
            return BitmapFont.FromStream(input, FormatHint.Binary, false).Pages.Count;
        }
        private static byte[] readBounded(Stream input)
        {
            if (input.Length > 16 * 1024 * 1024) throw new InvalidDataException("Font file exceeds limit.");
            using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray();
        }
    }
}
