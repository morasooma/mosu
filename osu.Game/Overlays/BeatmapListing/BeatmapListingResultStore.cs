using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Beatmaps.Drawables.Cards;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>
    /// A search's metadata is backed by a delete-on-close temporary file. Layout
    /// entries keep offsets/IDs, not every page's deserialised difficulty graph.
    /// Restoring a visited entry is local and does not replay network cursors.
    /// </summary>
    internal sealed class BeatmapListingResultStore : IDisposable
    {
        private const int capacity = 96;
        private const long max_cached_serialized_bytes = 8 * 1024 * 1024;
        private long cachedBytes;
        private readonly object sync = new object();
        private readonly FileStream file = new FileStream(Path.Combine(Path.GetTempPath(), "mosu-listing-" + Guid.NewGuid().ToString("N")),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose | FileOptions.RandomAccess);
        private readonly Dictionary<long, (APIBeatmapSet Set, LinkedListNode<long> Node, int Bytes)> cache = new();
        private readonly LinkedList<long> order = new();
        private readonly JsonSerializerSettings settings = new JsonSerializerSettings
        {
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            TypeNameHandling = TypeNameHandling.None
        };
        private int owners = 1;
        private bool disposed;

        internal void AddOwner() { lock (sync) { ObjectDisposedException.ThrowIf(disposed, this); owners++; } }

        internal Handle Add(APIBeatmapSet set)
        {
            byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new StoredSet { Set = set, Covers = set.Covers }, settings));
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                long offset = file.Length;
                file.Position = offset;
                file.Write(data);
                return new Handle(this, offset, data.Length, set.OnlineID, new(set.HasFavourited, set.FavouriteCount));
            }
        }

        private APIBeatmapSet read(long offset, int length)
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (cache.TryGetValue(offset, out var hit))
                {
                    order.Remove(hit.Node);
                    order.AddLast(hit.Node);
                    return hit.Set;
                }
                byte[] data = new byte[length];
                file.Position = offset;
                file.ReadExactly(data);
                var stored = JsonConvert.DeserializeObject<StoredSet>(Encoding.UTF8.GetString(data), settings)!;
                var set = stored.Set;
                set.Covers = stored.Covers;
                if (length > max_cached_serialized_bytes)
                    return set;
                cache.Add(offset, (set, order.AddLast(offset), length));
                cachedBytes += length;
                while (cache.Count > capacity || cachedBytes > max_cached_serialized_bytes)
                {
                    cachedBytes -= cache[order.First!.Value].Bytes;
                    cache.Remove(order.First.Value);
                    order.RemoveFirst();
                }
                return set;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed || --owners > 0) return;
                disposed = true;
                cache.Clear();
                cachedBytes = 0;
                order.Clear();
                file.Dispose();
            }
        }

        private sealed class StoredSet
        {
            public APIBeatmapSet Set { get; set; } = null!;
            public osu.Game.Beatmaps.BeatmapSetOnlineCovers Covers { get; set; }
        }

        internal sealed class Handle
        {
            private readonly BeatmapListingResultStore store;
            private readonly long offset;
            private readonly int length;
            internal int OnlineID { get; }
            internal BeatmapSetFavouriteState Favourites { get; }
            internal Handle(BeatmapListingResultStore store, long offset, int length, int id, BeatmapSetFavouriteState favourites)
            {
                this.store = store; this.offset = offset; this.length = length; OnlineID = id; Favourites = favourites;
            }
            internal APIBeatmapSet Get() => store.read(offset, length);
            internal void AddOwner() => store.AddOwner();
            internal void ReleaseOwner() => store.Dispose();
            internal Task GetAsync()
            {
                store.AddOwner();
                return Task.Run(() =>
                {
                    try { Get(); }
                    finally { store.Dispose(); }
                });
            }
        }
    }
}
