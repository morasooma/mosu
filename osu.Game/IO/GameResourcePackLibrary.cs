using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Graphics.Fonts;
using SixLabors.ImageSharp;

namespace osu.Game.IO
{
    /// <summary>
    /// Portable, bounded, data-only .morars archives. The catalogue comes from this
    /// client, never the archive. Shaders and executable/configuration code are excluded.
    /// Activation happens on next startup so cached textures/fonts cannot mix packs.
    /// </summary>
    public sealed class GameResourcePackLibrary
    {
        public const string Extension = ".morars";
        public const string DefaultPack = "default";
        public const int MaximumArchiveBytes = 512 * 1024 * 1024;
        public const int MaximumFileBytes = 32 * 1024 * 1024;
        private const long maximum_expanded_bytes = 768L * 1024 * 1024;
        private const int maximum_entries = 16000;
        private const int maximum_manifest_bytes = 4 * 1024 * 1024;
        private readonly Storage storage;
        private readonly IResourceStore<byte[]> catalogue;
        private readonly HashSet<string> allowed;

        public GameResourcePackLibrary(Storage storage, IResourceStore<byte[]> catalogue)
        {
            this.storage = storage.GetStorageForDirectory("ResourcePacks");
            this.catalogue = catalogue;
            allowed = catalogue.GetAvailableResources().Where(IsResourcePath).ToHashSet(StringComparer.Ordinal);
        }

        public sealed class Manifest
        {
            public string Format { get; set; } = "Morasooma.Resources";
            public int Version { get; set; } = 1;
            public string Name { get; set; } = "Morasooma";
            public Dictionary<string, string> Files { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public sealed record InstalledPack(string Id, string Name);
        public sealed record ValidatedPack(Manifest Manifest, string Id, FontPackLibrary.Validated? Fonts);

        // Exact roots and extensions, plus a trusted-key allowlist in Validate().
        // In particular there is NO Shaders/, ShaderOverrides/, dll, resx or raw font path.
        public static bool IsResourcePath(string? name)
        {
            if (!SafePath(name)) return false;
            return name!.StartsWith("Textures/", StringComparison.Ordinal) && Path.GetExtension(name) is ".png" or ".jpg" or ".jpeg"
                   || (name.StartsWith("Samples/", StringComparison.Ordinal) || name.StartsWith("Tracks/", StringComparison.Ordinal)
                       || name.StartsWith("Rulesets/Taiko/Samples/", StringComparison.Ordinal)) && Path.GetExtension(name) is ".wav" or ".mp3"
                   || name.StartsWith("Fonts/", StringComparison.Ordinal) && Path.GetExtension(name) is ".png" or ".bin"
                   || name.StartsWith("Skins/Morasooma/", StringComparison.Ordinal) && (name.EndsWith(".png", StringComparison.Ordinal) || name == "Skins/Morasooma/skin.ini")
                   || name.StartsWith("Skins/Legacy/", StringComparison.Ordinal) && Path.GetExtension(name) is ".wav" or ".mp3";
        }

        public static bool SafePath(string? value) => value != null && value.Length is > 0 and <= 240
            && !value.Contains('\\') && !value.Contains(':') && !value.Any(char.IsControl)
            && value.Split('/').All(p => p.Length > 0 && p is not ("." or "..") && !p.EndsWith(' ') && !p.EndsWith('.'));

        private static bool isNotice(string path) => path.StartsWith("Notices/", StringComparison.Ordinal)
            && Path.GetExtension(path) is ".txt" or ".md" or ".json";

        public ValidatedPack Validate(Stream input, Action<float>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!input.CanSeek || input.Length is < 1 or > MaximumArchiveBytes)
                throw new InvalidDataException("Invalid resource archive size.");
            input.Position = 0;
            string id = Convert.ToHexStringLower(SHA256.HashData(input));
            input.Position = 0;
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count is < 3 or > maximum_entries) throw new InvalidDataException("Invalid resource entry count.");
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            var insensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = entry.FullName;
                int limit = path == "pack.json" ? maximum_manifest_bytes : path == "fonts.morafn" ? MaximumArchiveBytes / 2 : MaximumFileBytes;
                if (!SafePath(path) || !insensitive.Add(path) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000
                    || entry.Length < 1 || entry.Length > limit || (total += entry.Length) > maximum_expanded_bytes
                    || !(path == "pack.json" || path == "fonts.morafn" || allowed.Contains(path) || isNotice(path)))
                    throw new InvalidDataException("Unsafe, unknown or oversized resource entry: " + path);
                entries.Add(path, entry);
            }
            if (!entries.TryGetValue("pack.json", out var manifestEntry)) throw new InvalidDataException("Missing resource manifest.");
            using var manifestStream = manifestEntry.Open();
            var manifest = readManifest(ReadBounded(manifestStream, maximum_manifest_bytes));
            if (manifest.Format != "Morasooma.Resources" || manifest.Version != 1 || manifest.Name == null
                || manifest.Name.Length is < 1 or > 64 || manifest.Name.Any(char.IsControl) || manifest.Name != manifest.Name.Trim()
                || manifest.Files == null || manifest.Files.Count != entries.Count - 1 || manifest.Files.Keys.Any(k => k == "pack.json" || !entries.ContainsKey(k))
                || !manifest.Files.ContainsKey("Notices/Morasooma/LICENSE.txt") || !manifest.Files.Keys.Any(allowed.Contains))
                throw new InvalidDataException("Unsupported or incomplete resource manifest.");
            long pixels = 0;
            FontPackLibrary.Validated? fonts = null;
            int index = 0;
            foreach (var (path, digest) in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (digest == null || digest.Length != 64 || digest.Any(c => !char.IsAsciiHexDigit(c))) throw new InvalidDataException("Invalid resource hash.");
                using var stream = entries[path].Open();
                byte[] bytes = ReadBounded(stream, path == "fonts.morafn" ? MaximumArchiveBytes / 2 : MaximumFileBytes);
                if (bytes.LongLength != entries[path].Length || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), digest, StringComparison.Ordinal))
                    throw new InvalidDataException("Resource hash mismatch: " + path);
                if (path == "fonts.morafn")
                {
                    using var fontStream = new MemoryStream(bytes, false);
                    fonts = FontPackLibrary.Validate(fontStream);
                }
                else if (isNotice(path))
                {
                    if (bytes.Length > 4 * 1024 * 1024) throw new InvalidDataException("Resource notice is too large.");
                    _ = new UTF8Encoding(false, true).GetString(bytes);
                }
                else if (path.EndsWith(".bin", StringComparison.Ordinal) || path.EndsWith(".ini", StringComparison.Ordinal))
                {
                    // Raw font descriptors and built-in skin configuration are not an
                    // unvalidated secondary file loader. Custom fonts use the existing
                    // fully validated, self-contained .morafn payload instead.
                    byte[] trusted = catalogue.Get(path) ?? throw new InvalidDataException("Missing trusted resource.");
                    if (!bytes.AsSpan().SequenceEqual(trusted)) throw new InvalidDataException("Built-in descriptor/configuration cannot be changed: " + path);
                }
                else if (Path.GetExtension(path) is ".png" or ".jpg" or ".jpeg")
                    validateImage(bytes, path.EndsWith(".png", StringComparison.Ordinal), ref pixels);
                else
                    validateAudio(bytes, path);
                progress?.Invoke(++index / (float)manifest.Files.Count);
            }
            return new ValidatedPack(manifest, id, fonts);
        }

        public ValidatedPack Install(Stream input, Action<float>? progress = null, CancellationToken cancellationToken = default)
        {
            string staging = ".install-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var output = storage.GetStream(staging, FileAccess.ReadWrite, FileMode.CreateNew))
                {
                    CopyBounded(input, output, MaximumArchiveBytes, cancellationToken);
                    var validated = Validate(output, progress, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    output.Close();
                    string destination = storage.GetFullPath(validated.Id + Extension);
                    if (!File.Exists(destination)) File.Move(storage.GetFullPath(staging), destination);
                    else
                    {
                        using var existing = File.OpenRead(destination);
                        if (Validate(existing, cancellationToken: cancellationToken).Id != validated.Id)
                            throw new InvalidDataException("Installed resource pack identity mismatch.");
                    }
                    return validated;
                }
            }
            finally { if (storage.Exists(staging)) storage.Delete(staging); }
        }

        public GameResourcePackStore Open(string id)
        {
            if (id == DefaultPack) return new GameResourcePackStore();
            if (!isId(id)) throw new InvalidDataException("Invalid installed resource ID.");
            string path = storage.GetFullPath(id + Extension);
            using (var input = File.OpenRead(path))
                if (Validate(input).Id != id) throw new InvalidDataException("Installed resource pack changed.");
            return new GameResourcePackStore(path);
        }

        public FontPackLibrary.Validated? ReadFonts(string id)
        {
            if (id == DefaultPack) return null;
            using var input = File.OpenRead(storage.GetFullPath(id + Extension));
            using var zip = new ZipArchive(input, ZipArchiveMode.Read);
            var entry = zip.GetEntry("fonts.morafn");
            if (entry == null) return null;
            using var data = entry.Open();
            return FontPackLibrary.Validate(data);
        }

        public IEnumerable<InstalledPack> Installed()
        {
            foreach (string file in storage.GetFiles(string.Empty, "*" + Extension).Take(256))
            {
                if (!isId(Path.GetFileNameWithoutExtension(file))) continue;
                Manifest? manifest = null;
                try
                {
                    using var stream = storage.GetStream(file);
                    using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                    using var entry = zip.GetEntry("pack.json")?.Open();
                    if (entry != null) manifest = readManifest(ReadBounded(entry, maximum_manifest_bytes));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException) { }
                if (manifest?.Name != null && manifest.Name.Length is > 0 and <= 64 && !manifest.Name.Any(char.IsControl))
                    yield return new InstalledPack(Path.GetFileNameWithoutExtension(file), manifest.Name);
            }
        }

        public void CopyNotices(string id, Dictionary<string, byte[]> destination)
        {
            if (id == DefaultPack) return;
            if (!isId(id)) throw new InvalidDataException("Invalid resource ID.");
            using var stream = storage.GetStream(id + Extension);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries.Where(e => isNotice(e.FullName)))
            {
                using var input = entry.Open();
                byte[] bytes = ReadBounded(input, 4 * 1024 * 1024);
                // Content-addressed history survives re-export without nested path growth.
                destination["Notices/Imported/" + Convert.ToHexStringLower(SHA256.HashData(bytes)) + Path.GetExtension(entry.FullName)] = bytes;
            }
        }

        public void Export(Stream output, string name, IResourceStore<byte[]> effective,
                           IReadOnlyDictionary<string, byte[]> additional, Action<float>? progress = null, CancellationToken cancellationToken = default)
        {
            var manifest = new Manifest { Name = name };
            var paths = allowed.OrderBy(p => p, StringComparer.Ordinal).Concat(additional.Keys).ToArray();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                int index = 0;
                foreach (string path in paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] bytes = (additional.TryGetValue(path, out var data) ? data : effective.Get(path))
                        ?? throw new InvalidDataException("Missing resource: " + path);
                    if (!(allowed.Contains(path) || path == "fonts.morafn" || isNotice(path))) throw new InvalidDataException("Disallowed export resource.");
                    manifest.Files.Add(path, Convert.ToHexStringLower(SHA256.HashData(bytes)));
                    using (var stream = archive.CreateEntry(path, CompressionLevel.Fastest).Open()) stream.Write(bytes);
                    progress?.Invoke(++index / (float)paths.Length);
                }
                using var entry = archive.CreateEntry("pack.json", CompressionLevel.Fastest).Open();
                entry.Write(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented)));
            }
        }

        private static Manifest readManifest(byte[] data)
        {
            using var text = new StringReader(new UTF8Encoding(false, true).GetString(data));
            using var reader = new JsonTextReader(text) { MaxDepth = 8 };
            var json = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Trailing resource manifest data.");
            return json.ToObject<Manifest>(JsonSerializer.Create(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error,
            })) ?? throw new InvalidDataException("Missing resource manifest.");
        }

        private static void validateImage(byte[] bytes, bool png, ref long totalPixels)
        {
            if (png) validateStaticPng(bytes);
            else if (bytes.Length < 3 || bytes[0] != 0xff || bytes[1] != 0xd8 || bytes[2] != 0xff)
                throw new InvalidDataException("Expected JPEG image.");
            using var input = new MemoryStream(bytes, false);
            var options = new SixLabors.ImageSharp.Formats.DecoderOptions { SkipMetadata = true, MaxFrames = 1 };
            var info = Image.Identify(options, input);
            if (info.Width is < 1 or > 8192 || info.Height is < 1 or > 8192 || (long)info.Width * info.Height > 32 * 1024 * 1024
                || (totalPixels += (long)info.Width * info.Height) > 768L * 1024 * 1024)
                throw new InvalidDataException("Resource images exceed decoded limits.");
            input.Position = 0;
            using var decoded = Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(options, input);
        }

        private static void validateStaticPng(byte[] bytes)
        {
            if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException("Expected static PNG.");
            int offset = 8;
            bool ended = false;
            while (offset < bytes.Length)
            {
                if (bytes.Length - offset < 12) throw new InvalidDataException("Truncated PNG.");
                uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
                if (length > bytes.Length - offset - 12) throw new InvalidDataException("Invalid PNG chunk.");
                string type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
                if (type is "acTL" or "fcTL" or "fdAT") throw new InvalidDataException("Animated PNG is not supported.");
                offset += checked((int)length + 12);
                if (type == "IEND") { ended = true; break; }
            }
            if (!ended || offset != bytes.Length) throw new InvalidDataException("Invalid PNG ending.");
        }

        private static void validateAudio(byte[] bytes, string path)
        {
            if (Audio.InterfaceSoundLibrary.IsIntentionalSilence(path, bytes)) return;
            using var stream = new MemoryStream(bytes, false);
            string decodedName = bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WAVE"
                ? "resource.wav" : "resource.mp3";
            using var file = Utils.TagLibUtils.GetTagLibFile(decodedName, stream);
            var p = file.Properties;
            if (p.AudioChannels is < 1 or > 2 || p.AudioSampleRate is < 8000 or > 192000 || p.Duration.TotalSeconds <= 0
                || p.Duration.TotalSeconds > (path.StartsWith("Tracks/", StringComparison.Ordinal) ? 300 : 30)
                || p.Duration.TotalSeconds * p.AudioSampleRate * p.AudioChannels * 4 > 256 * 1024 * 1024)
                throw new InvalidDataException("Invalid resource audio.");
        }

        private static bool isId(string id) => id.Length == 64 && id.All(char.IsAsciiHexDigit);
        internal static byte[] ReadBounded(Stream input, int limit)
        {
            using var output = new MemoryStream();
            CopyBounded(input, output, limit);
            return output.ToArray();
        }
        private static void CopyBounded(Stream input, Stream output, int limit, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, limit - total + 1))) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                total += read;
                if (total > limit) throw new InvalidDataException("Resource data exceeds size limit.");
                output.Write(buffer, 0, read);
            }
        }
    }
}
