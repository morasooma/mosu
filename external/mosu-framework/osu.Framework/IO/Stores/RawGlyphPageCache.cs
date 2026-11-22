using System;
using System.Collections.Generic;

namespace osu.Framework.IO.Stores
{
    /// <summary>
    /// Bounded alpha-only font pages, shared across glyph stores with identical content keys.
    /// Loading takes place outside this cache's lock, so a cold page does not block unrelated fonts.
    /// </summary>
    internal sealed class RawGlyphPageCache
    {
        internal static readonly RawGlyphPageCache Shared = new RawGlyphPageCache(32 * 1024 * 1024);

        private readonly int maximumBytes;
        private readonly object sync = new object();
        private readonly Dictionary<string, LinkedListNode<(string Key, byte[] Pixels)>> pages = new Dictionary<string, LinkedListNode<(string, byte[])>>();
        private readonly LinkedList<(string Key, byte[] Pixels)> order = new LinkedList<(string, byte[])>();
        private int cachedBytes;

        internal int CachedBytes
        {
            get { lock (sync) return cachedBytes; }
        }

        internal RawGlyphPageCache(int maximumBytes) => this.maximumBytes = maximumBytes;

        internal bool CanCache(int length) => length > 0 && length <= maximumBytes;

        internal byte[]? Get(string key)
        {
            lock (sync)
            {
                if (!pages.TryGetValue(key, out var node))
                    return null;
                order.Remove(node);
                order.AddLast(node);
                return node.Value.Pixels;
            }
        }

        internal byte[] Add(string key, byte[] pixels)
        {
            if (!CanCache(pixels.Length))
                throw new ArgumentOutOfRangeException(nameof(pixels));

            lock (sync)
            {
                // Another store may have loaded the same immutable page while we were reading.
                if (pages.TryGetValue(key, out var existing))
                {
                    order.Remove(existing);
                    order.AddLast(existing);
                    return existing.Value.Pixels;
                }

                while (cachedBytes > maximumBytes - pixels.Length)
                {
                    var oldest = order.First!;
                    cachedBytes -= oldest.Value.Pixels.Length;
                    pages.Remove(oldest.Value.Key);
                    order.RemoveFirst();
                }

                pages.Add(key, order.AddLast((key, pixels)));
                cachedBytes += pixels.Length;
                return pixels;
            }
        }
    }
}
