// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Keeps a bounded set of recently-used carousel previews alive after their panels return to the pool.
    /// </summary>
    /// <remarks>
    /// <see cref="LargeTextureStore"/> normally purges a texture as soon as its last drawable is disposed.
    /// Carousel panels are pooled aggressively, which otherwise makes scrolling back decode and upload the
    /// same background again. Retained references are released in LRU order; references held by visible
    /// sprites keep an evicted texture valid until those sprites are disposed.
    /// </remarks>
    internal class CarouselPreviewTextureStore : LargeTextureStore
    {
        // Carousel panels only need the visible neighbourhood. A 64-entry cache retained tens of
        // megabytes of GPU textures across each of the four preview stores and provided little value
        // during one-directional scrolling.
        internal const int DEFAULT_CACHE_CAPACITY = 24;

        private readonly int cacheCapacity;
        private readonly object cacheLock = new object();
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> cachedEntries = new Dictionary<string, LinkedListNode<CacheEntry>>();
        private readonly LinkedList<CacheEntry> recency = new LinkedList<CacheEntry>();

        private bool isDisposing;

        internal int CachedCount
        {
            get
            {
                lock (cacheLock)
                    return cachedEntries.Count;
            }
        }

        public CarouselPreviewTextureStore(IRenderer renderer, IResourceStore<TextureUpload>? store = null, int cacheCapacity = DEFAULT_CACHE_CAPACITY)
            : base(renderer, store)
        {
            this.cacheCapacity = cacheCapacity;
        }

        protected override bool TryGetCached(string lookupKey, out Texture texture)
        {
            bool found = base.TryGetCached(lookupKey, out texture);

            if (found)
                retainOrTouch(lookupKey);

            return found;
        }

        protected override Texture CacheAndReturnTexture(string lookupKey, Texture texture)
        {
            Texture result = base.CacheAndReturnTexture(lookupKey, texture);

            if (result != null)
                retainOrTouch(lookupKey);

            return result!;
        }

        private void retainOrTouch(string lookupKey)
        {
            lock (cacheLock)
            {
                if (isDisposing)
                    return;

                if (cachedEntries.TryGetValue(lookupKey, out var existing))
                {
                    recency.Remove(existing);
                    recency.AddFirst(existing);
                    return;
                }
            }

            // Ask LargeTextureStore for a separate reference owned by this cache. This is deliberately
            // performed outside cacheLock because the base store has its own reference-count lock.
            if (!base.TryGetCached(lookupKey, out Texture retainedTexture))
                return;

            Texture? textureToRelease = null;

            lock (cacheLock)
            {
                if (isDisposing)
                {
                    textureToRelease = retainedTexture;
                }
                else if (cachedEntries.TryGetValue(lookupKey, out var existing))
                {
                    recency.Remove(existing);
                    recency.AddFirst(existing);
                    textureToRelease = retainedTexture;
                }
                else
                {
                    var node = recency.AddFirst(new CacheEntry(lookupKey, retainedTexture));
                    cachedEntries.Add(lookupKey, node);

                    if (cachedEntries.Count > cacheCapacity)
                    {
                        var oldest = recency.Last!;
                        recency.RemoveLast();
                        cachedEntries.Remove(oldest.Value.LookupKey);
                        textureToRelease = oldest.Value.Texture;
                    }
                }
            }

            textureToRelease?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            List<Texture> texturesToRelease = new List<Texture>();

            lock (cacheLock)
            {
                isDisposing = true;

                foreach (var entry in recency)
                    texturesToRelease.Add(entry.Texture);

                recency.Clear();
                cachedEntries.Clear();
            }

            foreach (var texture in texturesToRelease)
                texture.Dispose();

            base.Dispose(disposing);
        }

        private readonly record struct CacheEntry(string LookupKey, Texture Texture);
    }
}
