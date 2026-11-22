using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;

namespace osu.Game.Graphics.Fonts
{
    internal sealed class FontPackResourceStore : IResourceStore<byte[]>
    {
        private readonly IReadOnlyDictionary<string, byte[]> files;
        public FontPackResourceStore(IReadOnlyDictionary<string, byte[]> files) => this.files = files;
        public byte[] Get(string name) => files.TryGetValue(name, out var value) ? value : null!;
        public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));
        public System.IO.Stream GetStream(string name) => files.TryGetValue(name, out var data) ? new System.IO.MemoryStream(data, false) : null!;
        public IEnumerable<string> GetAvailableResources() => files.Keys;
        public void Dispose() { }
    }

    internal sealed class FontPackGlyphStore : RawCachingGlyphStore, IGlyphStore, IResourceStore<TextureUpload>
    {
        private readonly string name;
        public FontPackGlyphStore(ResourceStore<byte[]> resources, string asset, string name, IResourceStore<TextureUpload> loader)
            : base(resources, asset, loader) => this.name = name;
        string IGlyphStore.FontName => name;
        private string map(string key) => key.StartsWith(name + "/", StringComparison.Ordinal) ? FontName + key[name.Length..] : key;
        TextureUpload IResourceStore<TextureUpload>.Get(string key) => base.Get(map(key));
        Task<TextureUpload> IResourceStore<TextureUpload>.GetAsync(string key, CancellationToken token) => base.GetAsync(map(key), token);
        IEnumerable<string> IResourceStore<TextureUpload>.GetAvailableResources() => base.GetAvailableResources()
            .Select(key => key.StartsWith(FontName + "/", StringComparison.Ordinal) ? name + key[FontName.Length..] : key);
    }
}
