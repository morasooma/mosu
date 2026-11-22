using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Online.API.Requests
{
    internal static class BeatmapCatalogueMetadataCache
    {
        private static readonly object sync = new object();
        private static readonly Dictionary<int, (BeatmapMirrorMetadata Metadata, DateTimeOffset Expires)> entries = new Dictionary<int, (BeatmapMirrorMetadata, DateTimeOffset)>();

        internal static void Store(int id, BeatmapMirrorMetadata metadata)
        {
            lock (sync)
            {
                var now = DateTimeOffset.UtcNow;
                foreach (var entry in entries)
                {
                    if (entry.Value.Expires <= now)
                        entries.Remove(entry.Key);
                }
                entries[id] = (metadata, now.AddMinutes(5));
                while (entries.Count > 1024)
                    entries.Remove(entries.MinBy(e => e.Value.Expires).Key);
            }
        }

        internal static BeatmapMirrorMetadata? Get(int id)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(id, out var entry))
                    return null;
                if (entry.Expires > DateTimeOffset.UtcNow)
                    return entry.Metadata;
                entries.Remove(id);
                return null;
            }
        }

        internal static void Invalidate(int id)
        {
            lock (sync) entries.Remove(id);
        }
    }
}
