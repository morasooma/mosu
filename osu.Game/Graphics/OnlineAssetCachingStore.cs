// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Online;

namespace osu.Game.Graphics
{
    /// <summary>
    /// <para>
    /// Store for retrieval and caching of assets (background, avatars, covers) retrieved from the web to disk.
    /// </para>
    /// <para>
    /// This store assumes relies on the uniqueness of the URL of retrieved assets to determine identity.
    /// Therefore, this store <b>MUST</b> only be used with URLs that are content-addressed in some way
    /// (by containing a content-based hash in the filename, or a cache-busting query string based on time of last update).
    /// </para>
    /// <para>
    /// This store <b>MUST NOT</b> be used with URLs containing naive cache-busting strings (e.g. <c>test.jpg?TIMESTAMP</c>)
    /// as it both makes the caching ineffective <b>AND</b> trashes the cache with entries that will never be used again.
    /// </para>
    /// </summary>
    public class OnlineAssetCachingStore : IDisposable
    {
        // ReSharper disable NotAccessedField.Local
        private readonly RealmAccess realmAccess;
        private readonly OnlineStore onlineStore;
        private readonly RealmFileStore fileStore;
        private readonly LargeTextureStore largeTextureStore;
        private readonly SeasonalBackgroundResourceStore seasonalBackgroundStore = new SeasonalBackgroundResourceStore();

        public OnlineAssetCachingStore(GameHost host, RealmAccess realmAccess)
        {
            this.realmAccess = realmAccess;
            onlineStore = new TrustedDomainOnlineStore();
            fileStore = new RealmFileStore(realmAccess, host.Storage);
            // largeTextureStore = new LargeTextureStore(host.Renderer, host.CreateTextureLoaderStore(new StorageBackedResourceStore(fileStore.Storage)));
            largeTextureStore = new LargeTextureStore(host.Renderer, host.CreateTextureLoaderStore(onlineStore));
            largeTextureStore.AddTextureSource(host.CreateTextureLoaderStore(seasonalBackgroundStore));
        }

        public Texture? Get(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            return largeTextureStore.Get(url);

            // TODO: logic below temporarily disabled as it causes unacceptable performance on devices with slow I/O due to realm abuse
            // see https://github.com/ppy/osu/issues/38651#issuecomment-5356443643 for details

            // string? path = realmAccess.Write(r =>
            // {
            //     var a = r.All<RealmOnlineAsset>().Filter($@"{nameof(RealmOnlineAsset.File)}.{nameof(RealmNamedFileUsage.Filename)} == $0", url).FirstOrDefault();
            //     if (a != null)
            //         a.LastAccessed = DateTimeOffset.Now;
            //     return a?.File.File.GetStoragePath();
            // });

            // if (path == null)
            // {
            //     var onlineStream = onlineStore.GetStream(url);

            //     if (onlineStream == null)
            //         return null;

            //     path = realmAccess.Write(r =>
            //     {
            //         var file = fileStore.Add(onlineStream, r);
            //         r.Add(new RealmOnlineAsset(file, url));
            //         return file.GetStoragePath();
            //     });
            // }
            // else
            // {
            //     Logger.Log($"Online asset {url} retrieved from {nameof(OnlineAssetCachingStore)}.", LoggingTarget.Network);
            // }

            // return largeTextureStore.Get(path);
        }

        /// <summary>
        /// Retrieves a content-addressed seasonal background, persisting its encoded bytes in fork.realm.
        /// </summary>
        public Texture? GetSeasonalBackground(string url, string hash)
        {
            if (string.IsNullOrWhiteSpace(url) || !isSha256(hash))
                return null;

            string normalisedHash = hash.ToLowerInvariant();
            string lookupKey = $"seasonal-background:{normalisedHash}";
            byte[]? data = ForkDataStore.Instance?.GetSeasonalBackground(normalisedHash);

            if (data == null || !hashMatches(data, normalisedHash))
            {
                data = onlineStore.Get(url);

                if (data == null || !hashMatches(data, normalisedHash))
                    return null;

                ForkDataStore.Instance?.SetSeasonalBackground(normalisedHash, data);
            }

            seasonalBackgroundStore.Add(lookupKey, data);

            try
            {
                return largeTextureStore.Get(lookupKey);
            }
            finally
            {
                seasonalBackgroundStore.Remove(lookupKey);
            }
        }

        private static bool isSha256(string hash)
            => hash.Length == 64 && hash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

        private static bool hashMatches(byte[] data, string expectedHash)
            => string.Equals(Convert.ToHexStringLower(SHA256.HashData(data)), expectedHash, StringComparison.Ordinal);

        public void Dispose()
        {
            onlineStore.Dispose();
            largeTextureStore.Dispose();
        }

        private sealed class SeasonalBackgroundResourceStore : IResourceStore<byte[]>
        {
            private readonly ConcurrentDictionary<string, byte[]> resources = new ConcurrentDictionary<string, byte[]>();

            public void Add(string name, byte[] data) => resources[name] = data;

            public void Remove(string name) => resources.TryRemove(name, out _);

            public byte[] Get(string name) => resources.GetValueOrDefault(name)!;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));

            public Stream GetStream(string name) => Get(name) is byte[] data ? new MemoryStream(data, writable: false) : null!;

            public IEnumerable<string> GetAvailableResources() => resources.Keys;

            public void Dispose() => resources.Clear();
        }
    }
}
