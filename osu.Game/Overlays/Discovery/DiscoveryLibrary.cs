using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;
using Realms;

namespace osu.Game.Overlays.Discovery
{
    /// <summary>Metadata-only song index. No local osu files or audio are opened.</summary>
    internal sealed class DiscoveryLibrary : IDisposable
    {
        private readonly object sync = new object();
        private readonly Dictionary<Guid, string[]> maps = new Dictionary<Guid, string[]>();
        private readonly Dictionary<string, int> songs = new Dictionary<string, int>();
        private readonly HashSet<string> stableSongs = new HashSet<string>();
        private readonly IDisposable subscription;

        internal DiscoveryLibrary(RealmAccess realm)
        {
            realm.Run(r =>
            {
                foreach (var map in r.All<BeatmapInfo>().Filter("BeatmapSet.DeletePending == false"))
                    add(map);
            });
            subscription = realm.RegisterForNotifications(r => r.All<BeatmapInfo>().Filter("BeatmapSet.DeletePending == false"), (sender, changes) =>
            {
                lock (sync)
                {
                    if (changes == null || changes.DeletedIndices.Length > 0 || changes.Moves.Length > 0)
                    {
                        maps.Clear(); songs.Clear();
                        foreach (var map in sender) add(map);
                    }
                    else
                    {
                        foreach (int index in changes.InsertedIndices.Concat(changes.NewModifiedIndices).Distinct())
                        {
                            var map = sender[index];
                            remove(map.ID);
                            add(map);
                        }
                    }
                }
            });
            StablePathManager.AvailabilityChanged += refreshStable;
            refreshStable();
        }

        private void refreshStable()
        {
            var keys = StablePathManager.GetAvailableSongMetadata()
                .SelectMany(m => Keys(m.Artist, m.Title, m.ArtistUnicode, m.TitleUnicode)).ToArray();
            lock (sync)
            {
                stableSongs.Clear();
                stableSongs.UnionWith(keys);
            }
        }

        internal static IEnumerable<string> Keys(string artist, string title, string artistUnicode = "", string titleUnicode = "") =>
            new[] { DiscoveryHistory.SongKey(artist, title), DiscoveryHistory.SongKey(artistUnicode, titleUnicode),
                DiscoveryHistory.SongKey(artist, titleUnicode), DiscoveryHistory.SongKey(artistUnicode, title) }.Where(k => k.Length > 0).Distinct();

        internal bool ContainsSong(osu.Game.Online.API.Requests.Responses.APIBeatmapSet set)
        {
            lock (sync)
                return Keys(set.Artist, set.Title, set.ArtistUnicode, set.TitleUnicode).Any(k => songs.ContainsKey(k) || stableSongs.Contains(k));
        }

        private void add(BeatmapInfo map)
        {
            var m = map.Metadata;
            var keys = Keys(m.Artist, m.Title, m.ArtistUnicode, m.TitleUnicode).ToArray();
            maps[map.ID] = keys;
            foreach (string key in keys) songs[key] = songs.GetValueOrDefault(key) + 1;
        }

        private void remove(Guid id)
        {
            if (!maps.Remove(id, out var keys)) return;
            foreach (string key in keys)
                if (--songs[key] == 0) songs.Remove(key);
        }

        public void Dispose()
        {
            subscription.Dispose();
            StablePathManager.AvailabilityChanged -= refreshStable;
        }
    }
}
