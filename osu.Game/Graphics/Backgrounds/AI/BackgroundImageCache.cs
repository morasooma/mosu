using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>Worker-only, bounded cache of prepared RGB artwork, independent of the classification database.</summary>
    internal sealed class BackgroundImageCache
    {
        private readonly string directory;
        private const int maximum_pixels = 512 * 512;
        private const int magic = 0x3142474d;
        public BackgroundImageCache(string directory) => this.directory = directory;

        internal sealed record Artwork(byte[] Rgb, int Width, int Height);

        public Artwork? Read(string key)
        {
            string path = pathFor(key);
            if (!File.Exists(path))
                return null;
            try
            {
                using var input = File.OpenRead(path);
                if (input.Length > maximum_pixels * 4L)
                    throw new InvalidDataException("Oversized artwork cache entry.");
                using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
                if (reader.ReadInt32() != magic)
                    throw new InvalidDataException("Invalid artwork cache version.");
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                if (width < 1 || height < 1 || width > 512 || height > 512 || (long)width * height > maximum_pixels)
                    throw new InvalidDataException("Invalid artwork dimensions.");
                byte[] expected = reader.ReadBytes(32);
                byte[] rgb = new byte[width * height * 3];
                using var compressed = new BrotliStream(input, CompressionMode.Decompress);
                compressed.ReadExactly(rgb);
                if (compressed.ReadByte() != -1 || !SHA256.HashData(rgb).AsSpan().SequenceEqual(expected))
                    throw new InvalidDataException("Damaged artwork cache entry.");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
                return new Artwork(rgb, width, height);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                try { File.Delete(path); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                return null;
            }
        }

        public void Write(string key, Artwork artwork, long maximumBytes)
        {
            if (artwork.Width is < 1 or > 512 || artwork.Height is < 1 or > 512 || artwork.Rgb.Length != artwork.Width * artwork.Height * 3)
                throw new ArgumentException("Invalid cached artwork.");
            Directory.CreateDirectory(directory);
            string path = pathFor(key);
            string temporary = path + ".partial";
            try
            {
                using (var output = File.Create(temporary))
                {
                    using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
                    writer.Write(magic);
                    writer.Write(artwork.Width);
                    writer.Write(artwork.Height);
                    writer.Write(SHA256.HashData(artwork.Rgb));
                    using var compressed = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true);
                    compressed.Write(artwork.Rgb);
                }
                File.Move(temporary, path, true);
                Trim(maximumBytes);
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        public void Trim(long maximumBytes)
        {
            if (!Directory.Exists(directory))
                return;
            var files = new DirectoryInfo(directory).GetFiles("*.rgb").OrderBy(f => f.LastWriteTimeUtc).ToArray();
            long bytes = files.Sum(f => f.Length);
            foreach (var file in files)
            {
                if (bytes <= maximumBytes)
                    break;
                bytes -= file.Length;
                file.Delete();
            }
        }

        public void Clear()
        {
            if (!Directory.Exists(directory))
                return;
            foreach (string file in Directory.EnumerateFiles(directory, "*.rgb"))
                File.Delete(file);
        }

        private string pathFor(string key) => Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant() + ".rgb");
    }
}
