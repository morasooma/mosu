using System;
using System.Collections.Generic;

namespace osu.Game.Database
{
    /// <summary>
    /// Small read-through cache for optional replay JSON. Large replays remain owned by
    /// the score currently using them, rather than by a process-lifetime global cache.
    /// </summary>
    internal sealed class TagCoopReplayCache
    {
        private const int maximum_bytes = 4 * 1024 * 1024;
        private const int maximum_entries = 8;
        private readonly object sync = new object();
        private readonly Dictionary<Guid, LinkedListNode<(Guid Id, string Json)>> entries = new();
        private readonly LinkedList<(Guid Id, string Json)> order = new();
        private long cachedBytes;

        internal int Count
        {
            get { lock (sync) return entries.Count; }
        }

        internal long CachedBytes
        {
            get { lock (sync) return cachedBytes; }
        }

        public string GetOrLoad(Guid id, Func<string> load)
        {
            lock (sync)
            {
                if (entries.TryGetValue(id, out var existing))
                {
                    order.Remove(existing);
                    order.AddLast(existing);
                    return existing.Value.Json;
                }

                string json = load();
                set(id, json);
                return json;
            }
        }

        public void Set(Guid id, string json)
        {
            lock (sync) set(id, json);
        }

        private void set(Guid id, string json)
        {
            if (entries.Remove(id, out var old))
            {
                cachedBytes -= old.Value.Json.Length * sizeof(char);
                order.Remove(old);
            }

            long bytes = (long)json.Length * sizeof(char);
            if (bytes == 0 || bytes > maximum_bytes)
                return;

            while (entries.Count >= maximum_entries || cachedBytes + bytes > maximum_bytes)
            {
                var oldest = order.First!;
                cachedBytes -= oldest.Value.Json.Length * sizeof(char);
                entries.Remove(oldest.Value.Id);
                order.RemoveFirst();
            }

            entries.Add(id, order.AddLast((id, json)));
            cachedBytes += bytes;
        }
    }
}
