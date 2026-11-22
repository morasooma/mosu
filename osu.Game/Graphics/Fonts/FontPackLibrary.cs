using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.Platform;
using SharpFNT;
using SixLabors.ImageSharp;

namespace osu.Game.Graphics.Fonts
{
    /// <summary>Data-only .morafn ZIPs. Never extracts paths supplied by the archive.</summary>
    public sealed class FontPackLibrary
    {
        public const string EXTENSION = ".morafn";
        private const int max_archive = 256 * 1024 * 1024;
        private const int max_file = 16 * 1024 * 1024;
        private const int max_total = 384 * 1024 * 1024;
        private readonly Storage storage;

        public FontPackLibrary(Storage storage) => this.storage = storage.GetStorageForDirectory("FontPacks");

        public sealed class Pack
        {
            public string Format { get; set; } = "Morasooma.Fonts";
            public int Version { get; set; } = 1;
            public FontConfiguration Settings { get; set; } = new FontConfiguration();
            public List<Family> Families { get; set; } = new List<Family>();
        }

        public sealed class Family
        {
            public string Name { get; set; } = string.Empty;
            public string MetricsFamily { get; set; } = string.Empty;
            public string? LatinHeadingFamily { get; set; }
            public List<Face> Faces { get; set; } = new List<Face>();
            public List<string> Notices { get; set; } = new List<string>();
        }

        public sealed class Face
        {
            public string Weight { get; set; } = "Regular";
            public string Asset { get; set; } = string.Empty;
        }

        public sealed class Validated
        {
            public Pack Manifest { get; }
            public IReadOnlyDictionary<string, byte[]> Files { get; }
            public string Id { get; }
            internal byte[] Archive { get; }
            internal Validated(Pack manifest, Dictionary<string, byte[]> files, string id, byte[] archive)
            {
                Manifest = manifest;
                Files = files;
                Id = id;
                Archive = archive;
            }
            public string InstalledFamily(string name) => $"morafn-{Id}-{Manifest.Families.FindIndex(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))}";
            public FontConfiguration InstalledSettings()
            {
                var settings = JsonConvert.DeserializeObject<FontConfiguration>(JsonConvert.SerializeObject(Manifest.Settings))!;
                settings.Common = map(settings.Common);
                settings.Main = map(settings.Main);
                settings.Alternate = map(settings.Alternate);
                settings.Numeric = map(settings.Numeric);
                settings.Inter = map(settings.Inter);
                return settings;
            }
            private string map(string name) => name == "Default" ? name : InstalledFamily(name);
        }

        public static Validated Validate(Stream input)
        {
            using var bounded = new MemoryStream();
            copyBounded(input, bounded, max_archive);
            string id = Convert.ToHexStringLower(SHA256.HashData(bounded.ToArray()));
            bounded.Position = 0;
            using var zip = new ZipArchive(bounded, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count is < 1 or > 1024) throw new InvalidDataException("Invalid archive entry count.");
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                string name = entry.FullName;
                if (!safePath(name) || files.ContainsKey(name) || entry.Length > max_file
                    || (total += entry.Length) > max_total || (entry.ExternalAttributes >> 16 & 0xf000) == 0xa000)
                    throw new InvalidDataException("Unsafe font archive entry.");
                using var source = entry.Open();
                using var output = new MemoryStream();
                copyBounded(source, output, name == "pack.json" ? 128 * 1024 : max_file);
                if (output.Length != entry.Length) throw new InvalidDataException("Invalid entry length.");
                files.Add(name, output.ToArray());
            }
            if (!files.TryGetValue("pack.json", out var json)) throw new InvalidDataException("Missing pack.json.");
            var manifest = JsonConvert.DeserializeObject<Pack>(new UTF8Encoding(false, true).GetString(json), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None, MaxDepth = 16, MissingMemberHandling = MissingMemberHandling.Error,
            }) ?? throw new InvalidDataException("Invalid font manifest.");
            if (manifest.Format != "Morasooma.Fonts" || manifest.Version != 1 || manifest.Settings?.IsValid() != true
                || manifest.Families == null || manifest.Families.Count is < 1 or > 32)
                throw new InvalidDataException("Unsupported font package.");
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pack.json" };
            var familyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int faceCount = 0;
            long decodedPixels = 0;
            int glyphCount = 0;
            var checkedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var decodeOptions = new SixLabors.ImageSharp.Formats.DecoderOptions { SkipMetadata = true, MaxFrames = 1 };
            foreach (var family in manifest.Families)
            {
                if (family == null || !Overlays.CustomTheme.ThemeInterfaceSettings.IsSafeFontName(family.Name)
                    || family.Name == "Default" || !familyNames.Add(family.Name)
                    || !validMetrics(family.MetricsFamily) || family.Faces == null || family.Faces.Count is < 1 or > 20
                    || (faceCount += family.Faces.Count) > 128 || family.Notices == null || family.Notices.Count > 16)
                    throw new InvalidDataException("Invalid font family.");
                var weights = new HashSet<string>(StringComparer.Ordinal);
                foreach (var face in family.Faces)
                {
                    if (face == null || !(validWeight(face.Weight) || family.MetricsFamily == "Noto" && new[] { "Basic", "Bopomofo", "CJK-Basic", "CJK-Compatibility", "Hangul", "Thai" }.Contains(face.Weight)) || !weights.Add(face.Weight)
                        || !safePath(face.Asset) || !face.Asset.EndsWith(".bin", StringComparison.Ordinal)
                        || !used.Add(face.Asset) || !files.TryGetValue(face.Asset, out var descriptor) || descriptor.Length > 4 * 1024 * 1024)
                        throw new InvalidDataException("Invalid font face.");
                    using var source = new MemoryStream(descriptor);
                    validateBinary(descriptor);
                    var font = BitmapFont.FromStream(source, FormatHint.Binary, false);
                    if (font.Pages.Count is < 1 or > 192 || font.Characters.Count is < 1 or > 65536 || (glyphCount += font.Characters.Count) > 1000000
                        || font.Common.ScaleWidth is < 1 or > 4096 || font.Common.ScaleHeight is < 1 or > 4096)
                        throw new InvalidDataException("Invalid font dimensions.");
                    var dimensions = new Dictionary<int, (int w, int h)>();
                    for (int page = 0; page < font.Pages.Count; page++)
                    {
                        string path = PageName(face.Asset, page, font.Pages.Count);
                        if (!files.TryGetValue(path, out var png)) throw new InvalidDataException("Missing font page.");
                        used.Add(path);
                        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                            throw new InvalidDataException("Expected a PNG font atlas.");
                        validateStaticPng(png);
                        using var imageStream = new MemoryStream(png);
                        var info = Image.Identify(decodeOptions, imageStream);
                        if (info.Width is < 1 or > 4096 || info.Height is < 1 or > 4096
                            || (long)info.Width * info.Height > 4 * 1024 * 1024)
                            throw new InvalidDataException("Atlas exceeds decoded limits.");
                        if (checkedPages.Add(path))
                        {
                            decodedPixels += (long)info.Width * info.Height;
                            if (decodedPixels > 256L * 1024 * 1024) throw new InvalidDataException("Font package exceeds decoded pixel limit.");
                            imageStream.Position = 0;
                            using var decoded = Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(decodeOptions, imageStream);
                        }
                        dimensions[page] = (info.Width, info.Height);
                    }
                    foreach (var c in font.Characters)
                    {
                        var glyph = c.Value;
                        if (c.Key is < 0 or > 65535 || !dimensions.TryGetValue(glyph.Page, out var size)
                            || glyph.Width < 1 || glyph.Height < 1 || glyph.X < 0 || glyph.Y < 0
                            || (long)glyph.X + glyph.Width > size.w || (long)glyph.Y + glyph.Height > size.h)
                            throw new InvalidDataException("Glyph outside atlas.");
                    }
                    if (font.KerningPairs?.Any(p => !font.Characters.ContainsKey(p.Key.First) || !font.Characters.ContainsKey(p.Key.Second)) == true)
                        throw new InvalidDataException("Invalid kerning references.");
                }
                foreach (string notice in family.Notices)
                {
                    if (!safePath(notice) || !notice.EndsWith(".txt", StringComparison.Ordinal) || !files.ContainsKey(notice))
                        throw new InvalidDataException("Missing font notice.");
                    used.Add(notice);
                }
            }
            if (manifest.Families.Any(f => f.LatinHeadingFamily != null && !familyNames.Contains(f.LatinHeadingFamily))
                || manifest.Settings.Families.Any(f => f != "Default" && !familyNames.Contains(f)) || used.Count != files.Count)
                throw new InvalidDataException("Missing font reference or unexpected archive content.");
            return new Validated(manifest, files, id, bounded.ToArray());
        }

        public string Install(Validated pack)
        {
            string name = pack.Id + EXTENSION;
            if (storage.Exists(name))
            {
                using var existing = storage.GetStream(name);
                if (Validate(existing).Id != pack.Id) throw new InvalidDataException("Installed font archive was modified.");
                return storage.GetFullPath(name);
            }
            string staging = ".install-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var output = storage.GetStream(staging, FileAccess.Write, FileMode.CreateNew))
                    output.Write(pack.Archive);
                File.Move(storage.GetFullPath(staging), storage.GetFullPath(name));
                return storage.GetFullPath(name);
            }
            finally { if (storage.Exists(staging)) storage.Delete(staging); }
        }

        public IEnumerable<Validated> Installed()
        {
            foreach (string file in storage.GetFiles(string.Empty, "*" + EXTENSION))
            {
                Validated? pack = null;
                try
                {
                    using var stream = storage.GetStream(file);
                    var validated = Validate(stream);
                    if (file != validated.Id + EXTENSION) throw new InvalidDataException("Font package identity mismatch.");
                    pack = validated;
                }
                catch (Exception ex) { osu.Framework.Logging.Logger.Error(ex, "Could not load font package."); }
                if (pack != null) yield return pack;
            }
        }

        // Runtime texture loaders are not restricted to a single decoded frame.
        // Reject APNG rather than validating frame one and later loading all frames.
        private static void validateStaticPng(byte[] bytes)
        {
            int offset = 8;
            bool ended = false;
            while (offset < bytes.Length)
            {
                if (bytes.Length - offset < 12) throw new InvalidDataException("Truncated PNG chunk.");
                uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
                if (length > bytes.Length - offset - 12) throw new InvalidDataException("Invalid PNG chunk size.");
                string type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
                if (type is "acTL" or "fcTL" or "fdAT") throw new InvalidDataException("Animated font atlases are not supported.");
                offset += checked((int)length + 12);
                if (type == "IEND") { ended = true; break; }
            }
            if (!ended || offset != bytes.Length) throw new InvalidDataException("Invalid PNG ending.");
        }

        // Check block lengths before handing untrusted counts to the BMFont parser.
        private static void validateBinary(byte[] bytes)
        {
            using var input = new BinaryReader(new MemoryStream(bytes));
            if (bytes.Length < 4 || input.ReadByte() != 'B' || input.ReadByte() != 'M' || input.ReadByte() != 'F' || input.ReadByte() != 3)
                throw new InvalidDataException("Expected BMFont binary version 3.");
            var blocks = new HashSet<byte>();
            while (input.BaseStream.Position < bytes.Length)
            {
                if (bytes.Length - input.BaseStream.Position < 5) throw new InvalidDataException("Truncated BMFont block.");
                byte kind = input.ReadByte();
                uint length = input.ReadUInt32();
                if (kind is < 1 or > 5 || !blocks.Add(kind) || length > bytes.Length - input.BaseStream.Position
                    || kind == 1 && length < 15 || kind == 2 && length != 15
                    || kind == 3 && length < 1 || kind == 4 && (length == 0 || length % 20 != 0 || length / 20 > 65536)
                    || kind == 5 && length % 10 != 0)
                    throw new InvalidDataException("Invalid BMFont block.");
                input.BaseStream.Seek(length, SeekOrigin.Current);
            }
            if (!new byte[] { 1, 2, 3, 4 }.All(blocks.Contains)) throw new InvalidDataException("Incomplete BMFont descriptor.");
        }

        public static string PageName(string asset, int page, int count) => asset[..^4] + "_" + page.ToString().PadLeft((count - 1).ToString().Length, '0') + ".png";
        public static bool IsWeight(string value) => validWeight(value);
        private static bool validWeight(string value) => new[] { "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" }
            .Any(w => value == w || value == w + "Italic");
        private static bool validMetrics(string value) => new[] { "Torus", "Torus-Alternate", "Venera", "Nunito", "MosuRoundedAlt", "Syncopate", "Michroma", "Inter", "Noto", "Custom" }.Contains(value);
        private static bool safePath(string? value) => value != null && value.Length is > 0 and <= 180
            && !value.Contains('\\') && !value.Contains(':') && !value.StartsWith('/')
            && value.Split('/').All(s => s.Length > 0 && s != "." && s != ".." && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'));
        private static void copyBounded(Stream input, Stream output, int limit)
        {
            byte[] buffer = new byte[81920];
            int total = 0, read;
            while ((read = input.Read(buffer, 0, Math.Min(buffer.Length, limit - total + 1))) > 0)
            {
                total = checked(total + read);
                if (total > limit) throw new InvalidDataException("Font data exceeds size limit.");
                output.Write(buffer, 0, read);
            }
        }
    }
}
