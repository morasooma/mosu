using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace osu.Game.IO
{
    /// <summary>Read-only installed data. Never mounts code, shaders or archive-supplied paths.</summary>
    public sealed class GameResourcePackStore : IResourceStore<byte[]>
    {
        private readonly object sync = new object();
        private readonly ZipArchive? archive;
        private readonly Dictionary<string, ZipArchiveEntry> entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);

        public GameResourcePackStore(string? path = null)
        {
            if (path == null) return;
            archive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
                if (GameResourcePackLibrary.IsResourcePath(entry.FullName)) entries.Add(entry.FullName, entry);
        }

        public byte[] Get(string name)
        {
            // A second, independent boundary: even a tampered local archive cannot
            // make any shader lookup reach a user-supplied file.
            if (!GameResourcePackLibrary.IsResourcePath(name)) return null!;
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry)) return null!;
                using var input = entry.Open();
                return GameResourcePackLibrary.ReadBounded(input, GameResourcePackLibrary.MaximumFileBytes);
            }
        }

        public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Get(name));
        }

        public Stream GetStream(string name) => Get(name) is byte[] bytes ? new MemoryStream(bytes, false) : null!;
        public IEnumerable<string> GetAvailableResources() => entries.Keys.ToArray();
        public void Dispose() { lock (sync) archive?.Dispose(); }

        /// <summary>Borrow a game-wide store without transferring its lifetime to a skin or ruleset.</summary>
        public static IResourceStore<byte[]> Borrow(IResourceStore<byte[]> source) => new Borrowed(source);

        private sealed class Borrowed : IResourceStore<byte[]>
        {
            private readonly IResourceStore<byte[]> source;
            public Borrowed(IResourceStore<byte[]> source) => this.source = source;
            public byte[] Get(string name) => source.Get(name);
            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => source.GetAsync(name, cancellationToken);
            public Stream GetStream(string name) => source.GetStream(name);
            public IEnumerable<string> GetAvailableResources() => source.GetAvailableResources();
            public void Dispose() { }
        }
    }
}
