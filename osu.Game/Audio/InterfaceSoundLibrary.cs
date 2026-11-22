using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Platform;
using osu.Game.Utils;

namespace osu.Game.Audio
{
    /// <summary>
    /// Local, data-only sound packs. Archive names are never extraction paths.
    /// Portable .morafx exports contain the effective sounds, not a dependency on
    /// another user's built-in bank. Existing local packs and bare ZIPs remain readable.
    /// </summary>
    public sealed class InterfaceSoundLibrary
    {
        public const string DefaultPack = "default";
        public const string Extension = ".morafx";
        public const string Format = "Morasooma.Sounds";
        public const int MaximumFileBytes = 8 * 1024 * 1024;
        public const int MaximumPackBytes = 64 * 1024 * 1024;
        public const int MaximumEntries = 512;
        public const double MaximumDurationSeconds = 30;
        private const int maximum_manifest_bytes = 128 * 1024;
        private const int maximum_notice_bytes = 128 * 1024;
        private const int maximum_notices = 16;
        private const string retired_pack = "lazer-2017";

        private readonly Storage storage;
        private readonly HashSet<string> events;

        public InterfaceSoundLibrary(Storage storage, IEnumerable<string> events)
        {
            this.storage = storage.GetStorageForDirectory("interface-sounds");
            this.events = new HashSet<string>(events, StringComparer.Ordinal);
        }

        public void PresentExternally() => storage.PresentExternally();

        public IReadOnlyList<InterfaceSoundPack> GetPacks()
        {
            var result = new List<InterfaceSoundPack>();
            foreach (string path in storage.GetFiles("", "*.json").Take(MaximumEntries))
            {
                try
                {
                    using var stream = storage.GetStream(path);
                    var pack = readManifest(stream);
                    if (isId(pack.Id) && Path.GetFileName(path) == pack.Id + ".json")
                        result.Add(pack);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or JsonException or ArgumentException)
                {
                    // An incomplete or externally edited pack must not prevent startup.
                }
            }
            return result.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public InterfaceSoundPack? Find(string id) => GetPacks().FirstOrDefault(p => p.Id == id);

        public InterfaceSoundPack Import(Stream stream, string suggestedName)
        {
            var (manifest, files) = readArchive(stream, suggestedName);
            // Never overwrite another installed pack, even if a portable archive
            // contains an ID copied from its sender's local manifest.
            var pack = new InterfaceSoundPack { Name = manifest.Name };
            foreach (var (key, name) in manifest.Sounds)
                pack.Sounds.Add(key, saveBlob(name, files[name]));
            foreach (string name in manifest.Notices)
                pack.Notices.Add(saveBlob(name, files[name]));
            save(pack);
            return pack;
        }

        public void Export(Stream output, string selectedId, Func<string, (string Filename, byte[] Data)> builtIn,
                           IEnumerable<byte[]>? notices = null)
        {
            var selected = Find(selectedId);
            var manifest = new InterfaceSoundPack { Name = selected?.Name ?? "Morasooma" };
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string key in events.OrderBy(e => e, StringComparer.Ordinal))
            {
                (string Filename, byte[] Data)? custom = null;
                if (selected?.Sounds.TryGetValue(key, out string? blob) == true)
                {
                    try { custom = (blob, ReadSound(blob)); }
                    catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
                    {
                        // Match runtime fallback for a deleted/corrupt personal file.
                    }
                }
                var sound = custom ?? builtIn(key);
                validateAudio(sound.Filename, sound.Data);
                string name = "sounds/" + digest(sound.Data) + Path.GetExtension(sound.Filename).ToLowerInvariant();
                files.TryAdd(name, sound.Data);
                manifest.Sounds.Add(key, name);
            }

            void addNotice(byte[] data)
            {
                if (data.Length is 0 or > maximum_notice_bytes)
                    throw new InvalidDataException("Invalid sound notice.");
                string name = "notices/" + digest(data) + ".txt";
                if (files.TryAdd(name, data)) manifest.Notices.Add(name);
            }

            foreach (byte[] data in notices ?? Array.Empty<byte[]>()) addNotice(data);
            foreach (string name in selected?.Notices ?? Enumerable.Empty<string>()) addNotice(ReadNotice(name));
            files.Add("pack.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented)));
            using var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var (name, data) in files)
                {
                    using var file = zip.CreateEntry(name, CompressionLevel.Fastest).Open();
                    file.Write(data);
                }
            archive.Position = 0;
            // The producer must obey the same bounded validation as the importer,
            // without installing a temporary pack or changing the current selection.
            _ = readArchive(archive, manifest.Name);
            archive.Position = 0;
            archive.CopyTo(output);
        }

        private (InterfaceSoundPack Manifest, Dictionary<string, byte[]> Files) readArchive(Stream stream, string suggestedName)
        {
            using var bounded = new MemoryStream(readBounded(stream, MaximumPackBytes), false);
            using var archive = new ZipArchive(bounded, ZipArchiveMode.Read);
            if (archive.Entries.Count > MaximumEntries)
                throw new InvalidDataException("Too many archive entries.");
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                string path = entry.FullName;
                bool directory = path.EndsWith('/');
                string canonical = directory ? path[..^1] : path;
                if (!safePath(canonical) || !names.Add(canonical) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
                    throw new InvalidDataException("Unsafe or duplicate archive entry.");
                if (directory)
                {
                    if (entry.Length != 0) throw new InvalidDataException("Directory entry has data.");
                    continue;
                }
                int maximum = path == "pack.json" ? maximum_manifest_bytes
                    : path.StartsWith("notices/", StringComparison.Ordinal) && path.EndsWith(".txt", StringComparison.Ordinal) ? maximum_notice_bytes
                    : MaximumFileBytes;
                if (entry.Length > maximum || (total += entry.Length) > MaximumPackBytes)
                    throw new InvalidDataException("Sound pack is too large.");
                using var input = entry.Open();
                byte[] data = readBounded(input, maximum);
                if (data.LongLength != entry.Length) throw new InvalidDataException("Incorrect archive entry length.");
                files.Add(path, data);
            }
            InterfaceSoundPack manifest;
            if (files.Remove("pack.json", out byte[]? bytes))
            {
                using var input = new MemoryStream(bytes, false);
                manifest = readManifest(input);
            }
            else
            {
                manifest = new InterfaceSoundPack { Name = cleanName(suggestedName) };
                foreach (string path in files.Keys)
                {
                    string key = path[..^Path.GetExtension(path).Length];
                    if (!manifest.Sounds.TryAdd(key, path)) throw new InvalidDataException("Duplicate sound event.");
                }
            }
            var used = new HashSet<string>(StringComparer.Ordinal);
            double decodedTotal = 0;
            foreach (var (key, name) in manifest.Sounds)
            {
                if (!events.Contains(key) || !safePath(name) || !files.TryGetValue(name, out byte[]? data))
                    throw new InvalidDataException("Unknown event or missing sound file.");
                if (used.Add(name)) decodedTotal += validateAudio(name, data);
                if (decodedTotal > MaximumPackBytes) throw new InvalidDataException("Decoded sound pack is too large.");
            }
            foreach (string name in manifest.Notices)
            {
                if (!safePath(name) || !name.StartsWith("notices/", StringComparison.Ordinal) || !name.EndsWith(".txt", StringComparison.Ordinal)
                    || !files.TryGetValue(name, out byte[]? data) || data.Length is 0 or > maximum_notice_bytes || !used.Add(name))
                    throw new InvalidDataException("Invalid or missing sound notice.");
            }
            if (manifest.Sounds.Count == 0 || files.Keys.Except(used).Any())
                throw new InvalidDataException("Empty sound pack or unreferenced files.");
            return (manifest, files);
        }

        public InterfaceSoundPack Replace(string selectedId, string key, string filename, Stream stream)
        {
            if (!events.Contains(key)) throw new InvalidDataException("Unknown interface sound event.");
            byte[] bytes = readBounded(stream, MaximumFileBytes);
            validateAudio(filename, bytes);
            var pack = Find(selectedId) ?? new InterfaceSoundPack();
            pack.Sounds[key] = saveBlob(filename, bytes);
            if (pack.Sounds.Values.Distinct(StringComparer.Ordinal).Sum(blob => validateAudio(blob, ReadSound(blob))) > MaximumPackBytes)
                throw new InvalidDataException("Decoded sound pack is too large.");
            save(pack);
            return pack;
        }

        public void Reset(string id, string key)
        {
            var pack = Find(id);
            if (pack != null && pack.Sounds.Remove(key)) save(pack);
        }

        public void ResetAll(string id)
        {
            var pack = Find(id);
            if (pack == null) return;
            pack.Sounds.Clear();
            save(pack);
        }

        public byte[] ReadSound(string blob)
        {
            if (!isBlob(blob) || Path.GetExtension(blob) is not (".wav" or ".mp3"))
                throw new InvalidDataException("Invalid sound filename.");
            using var stream = storage.GetStream("files/" + blob);
            byte[] data = readBounded(stream, MaximumFileBytes);
            if (digest(data) != blob[..64]) throw new InvalidDataException("Sound file was modified.");
            validateAudio(blob, data);
            return data;
        }

        public byte[] ReadNotice(string blob)
        {
            if (!isBlob(blob) || !blob.EndsWith(".txt", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid notice filename.");
            using var stream = storage.GetStream("files/" + blob);
            byte[] data = readBounded(stream, maximum_notice_bytes);
            if (digest(data) != blob[..64]) throw new InvalidDataException("Sound notice was modified.");
            return data;
        }

        private string saveBlob(string filename, byte[] data)
        {
            string name = digest(data) + Path.GetExtension(filename).ToLowerInvariant();
            if (!storage.Exists("files/" + name))
            {
                using var output = storage.GetStream("files/" + name, FileAccess.Write, FileMode.CreateNew);
                output.Write(data);
            }
            return name;
        }

        private void save(InterfaceSoundPack pack)
        {
            validateManifest(pack);
            string json = JsonConvert.SerializeObject(pack, Formatting.Indented);
            if (Encoding.UTF8.GetByteCount(json) > maximum_manifest_bytes) throw new InvalidDataException("Sound manifest is too large.");
            string path = storage.GetFullPath(pack.Id + ".json", createIfNotExisting: true);
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, json);
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static InterfaceSoundPack readManifest(Stream stream)
        {
            using var memory = new MemoryStream(readBounded(stream, maximum_manifest_bytes), false);
            using var text = new StreamReader(memory, Encoding.UTF8);
            using var reader = new JsonTextReader(text) { MaxDepth = 8 };
            var json = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Trailing manifest data.");
            var pack = json.ToObject<InterfaceSoundPack>(JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }))
                       ?? throw new InvalidDataException("Missing sound pack manifest.");
            validateManifest(pack);
            return pack;
        }

        private static void validateManifest(InterfaceSoundPack pack)
        {
            if (pack.Format != Format || pack.Version != 1 || pack.Base is not (DefaultPack or retired_pack)
                || pack.Sounds == null || pack.Sounds.Count > MaximumEntries || pack.Sounds.Any(s => string.IsNullOrEmpty(s.Key) || string.IsNullOrEmpty(s.Value))
                || pack.Notices == null || pack.Notices.Count > maximum_notices)
                throw new InvalidDataException("Unsupported sound pack manifest.");
            // Preserve the user's actual recordings from old custom packs, but
            // retire their dependency on the removed built-in Lazer2017 bank.
            pack.Base = DefaultPack;
            pack.Name = cleanName(pack.Name);
            if (!isId(pack.Id)) throw new InvalidDataException("Invalid sound pack ID.");
        }

        private static string cleanName(string name)
        {
            name = name?.Trim() ?? "";
            if (name.Length is < 1 or > 64 || name.Any(char.IsControl)) throw new InvalidDataException("Invalid sound pack name.");
            return name;
        }

        private static bool safePath(string path) => !string.IsNullOrEmpty(path) && path.Length <= 240
            && !path.Contains('\\') && !path.Contains(':') && !path.Any(char.IsControl)
            && path.Split('/').All(p => p.Length > 0 && p is not ("." or ".."));
        private static string digest(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        private static bool isId(string id) => Guid.TryParseExact(id, "N", out _);
        private static bool isBlob(string name) => name != null && name.Length == 68 && name[..64].All(char.IsAsciiHexDigit);

        public static double ValidateAudio(string filename, byte[] data) => validateAudio(filename, data);

        private static double validateAudio(string filename, byte[] data)
        {
            if (data.Length is 0 or > MaximumFileBytes || Path.GetExtension(filename).ToLowerInvariant() is not (".wav" or ".mp3"))
                throw new InvalidDataException("Only WAV and MP3 sounds are supported.");
            // A canonical empty PCM WAV is deliberate silence, not a missing file.
            if (IsIntentionalSilence(filename, data)) return 0;
            try
            {
                using var memory = new MemoryStream(data, false);
                using var file = TagLibUtils.GetTagLibFile(filename, memory);
                var properties = file.Properties;
                if (properties.AudioChannels is < 1 or > 2 || properties.AudioSampleRate is < 8000 or > 192000
                    || properties.Duration.TotalSeconds is <= 0 or > MaximumDurationSeconds
                    || properties.Duration.TotalSeconds * properties.AudioSampleRate * properties.AudioChannels * 4 > MaximumFileBytes)
                    throw new InvalidDataException("Invalid, too long or too large decoded sound.");
                return properties.Duration.TotalSeconds * properties.AudioSampleRate * properties.AudioChannels * 4;
            }
            catch (Exception e) when (e is not InvalidDataException) { throw new InvalidDataException("Cannot decode audio file.", e); }
        }

        internal static bool IsIntentionalSilence(string filename, byte[] data) =>
            Path.GetExtension(filename).Equals(".wav", StringComparison.OrdinalIgnoreCase) && isEmptyWave(data);

        private static bool isEmptyWave(byte[] data)
        {
            if (data.Length != 44 || Encoding.ASCII.GetString(data, 0, 4) != "RIFF" || BitConverter.ToUInt32(data, 4) != 36
                || Encoding.ASCII.GetString(data, 8, 8) != "WAVEfmt " || BitConverter.ToUInt32(data, 16) != 16
                || BitConverter.ToUInt16(data, 20) != 1 || Encoding.ASCII.GetString(data, 36, 4) != "data" || BitConverter.ToUInt32(data, 40) != 0) return false;
            int channels = BitConverter.ToUInt16(data, 22), bits = BitConverter.ToUInt16(data, 34);
            uint rate = BitConverter.ToUInt32(data, 24);
            return channels is 1 or 2 && bits is 16 or 24 or 32 && rate is >= 8000 and <= 192000
                && BitConverter.ToUInt16(data, 32) == channels * bits / 8 && BitConverter.ToUInt32(data, 28) == rate * channels * bits / 8;
        }

        private static byte[] readBounded(Stream input, int maximum)
        {
            if (input == null) throw new FileNotFoundException("Sound pack file is missing.");
            using var output = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = input.Read(buffer, 0, Math.Min(buffer.Length, maximum + 1 - (int)output.Length))) > 0)
            {
                output.Write(buffer, 0, read);
                if (output.Length > maximum) throw new InvalidDataException("File is too large.");
            }
            return output.ToArray();
        }
    }

    public sealed class InterfaceSoundPack
    {
        public string Format { get; set; } = InterfaceSoundLibrary.Format;
        public int Version { get; set; } = 1;
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Custom";
        public string Base { get; set; } = InterfaceSoundLibrary.DefaultPack;
        public Dictionary<string, string> Sounds { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<string> Notices { get; set; } = new List<string>();
    }
}
