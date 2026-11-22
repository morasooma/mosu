using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using SharpCompress.Archives.Zip;
using SharpCompress.Compressors.Deflate;

namespace osu.Game.Online.API.Requests
{
    internal static class BeatmapMirrorArchive
    {
        internal const long MaximumArchiveBytes = 512L * 1024 * 1024;
        private const long maximum_extracted_bytes = 1024L * 1024 * 1024;
        private const long maximum_difficulty_bytes = 16L * 1024 * 1024;

        /// <summary>Read every entry, check CRC/size, and hash raw .osu bytes without re-encoding.</summary>
        internal static string[] Inspect(string filename, CancellationToken cancellationToken, int? expectedSetId = null)
        {
            if (new FileInfo(filename).Length > MaximumArchiveBytes)
                throw new InvalidDataException("Beatmap archive exceeds the download size limit.");

            using var archive = ZipArchive.OpenArchive(filename);
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            byte[] buffer = new byte[64 * 1024];
            long total = 0;
            int entries = 0;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++entries > 10000)
                    throw new InvalidDataException("Too many files in the beatmap archive.");
                if (entry.IsDirectory)
                    continue;

                string path = (entry.Key ?? string.Empty).Replace('\\', '/');
                if (path.Length == 0 || path.StartsWith('/') || path.Contains(':') || path.Split('/').Any(p => p == "..") || !paths.Add(path))
                    throw new InvalidDataException("Unsafe or duplicate filename in the beatmap archive.");
                if (entry.IsEncrypted || entry.Size < 0 || entry.Size > maximum_extracted_bytes - total)
                    throw new InvalidDataException("Invalid or oversized entry in the beatmap archive.");

                bool isDifficulty = path.EndsWith(".osu", StringComparison.OrdinalIgnoreCase);
                if (isDifficulty && entry.Size > maximum_difficulty_bytes)
                    throw new InvalidDataException("Beatmap difficulty exceeds the size limit.");

                using var hash = isDifficulty ? IncrementalHash.CreateHash(HashAlgorithmName.MD5) : null;
                using var difficultyBytes = isDifficulty ? new MemoryStream() : null;
                var crc = new CRC32();
                using var stream = entry.OpenEntryStream();
                long size = 0;
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    size += read;
                    total += read;
                    if (total > maximum_extracted_bytes || size > entry.Size)
                        throw new InvalidDataException("Beatmap archive expanded beyond its declared size.");
                    crc.SlurpBlock(buffer, 0, read);
                    hash?.AppendData(buffer, 0, read);
                    difficultyBytes?.Write(buffer, 0, read);
                }

                if (size != entry.Size || unchecked((uint)crc.Crc32Result) != entry.Crc)
                    throw new InvalidDataException($"Corrupt beatmap archive entry: {path}");
                if (hash != null)
                {
                    difficultyBytes!.Position = 0;
                    using var reader = new StreamReader(difficultyBytes, Encoding.UTF8, true, leaveOpen: true);
                    if (reader.ReadLine()?.StartsWith("osu file format v", StringComparison.Ordinal) != true)
                        throw new InvalidDataException("The archive contains an invalid .osu file.");
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("BeatmapSetID:", StringComparison.Ordinal) && int.TryParse(line.AsSpan("BeatmapSetID:".Length), out int setId)
                            && expectedSetId > 0 && setId > 0 && setId != expectedSetId)
                            throw new InvalidDataException("The mirror returned an archive for a different beatmap set.");
                    }
                    hashes.Add(Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
                }
            }

            if (hashes.Count == 0)
                throw new InvalidDataException("The mirror returned no beatmap difficulties.");
            return hashes.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
