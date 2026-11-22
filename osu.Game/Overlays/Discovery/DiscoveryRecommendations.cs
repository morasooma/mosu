using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Discovery
{
    internal enum DiscoveryReason { Artist, Mapper, Similar, Gameplay, Listeners, Music, Audio }

    internal record DiscoveryCandidate(APIBeatmap Map, DiscoveryReason Reason, double Weight);

    /// <summary>Metadata candidate retrieval; optional local CLAP Music re-ranking is owned by DiscoveryAudioService.</summary>
    internal sealed class DiscoveryRecommendations
    {
        private readonly APIBeatmap[] seeds;
        internal IReadOnlyList<APIBeatmap> Seeds => seeds;
        internal DiscoverySeedSource SeedSource { get; }
        internal readonly HashSet<int> KnownSets;
        private readonly HashSet<string> knownSongs;
        internal double TargetStars { get; }
        internal double TargetBpm { get; }
        internal int Mode { get; }
        internal DiscoveryTasteRanking Taste { get; } = new DiscoveryTasteRanking();
        internal void SetTaste(osu.Game.Online.API.Requests.DiscoveryTasteProfile value)
        {
            Taste.SetProfile(value);
            KnownSets.UnionWith(value.PlayedSets);
        }

        internal DiscoveryRecommendations(IEnumerable<SoloScoreInfo> scores, int mode,
                                          IEnumerable<APIBeatmap>? frequent = null, IEnumerable<APIBeatmap>? manual = null,
                                          DiscoverySeedSource source = DiscoverySeedSource.Top)
        {
            Mode = mode;
            SeedSource = source;
            seeds = DiscoverySeedSelection.Select(scores.Where(s => s.Beatmap != null).Select(s => s.Beatmap!),
                frequent ?? Array.Empty<APIBeatmap>(), manual ?? Array.Empty<APIBeatmap>(), source, mode);
            if (source == DiscoverySeedSource.Manual) Taste.SetExamples(seeds);
            KnownSets = seeds.Select(m => m.OnlineBeatmapSetID).ToHashSet();
            knownSongs = seeds.Select(m => DiscoveryHistory.SongKey(m.BeatmapSet!)).Where(s => s.Length > 0).ToHashSet();
            // Use unmodded metadata consistently on both sides; do not compare modded score stars to base maps.
            TargetStars = weightedMedian(seeds.Where(m => double.IsFinite(m.StarRating) && m.StarRating > 0)
                .Select(m => (m.StarRating, DiscoverySeedSelection.Weight(m, source))), 2);
            TargetBpm = weightedMedian(seeds.Where(m => double.IsFinite(m.BPM) && m.BPM > 0)
                .Select(m => (m.BPM, DiscoverySeedSelection.Weight(m, source))), 160);
        }

        internal void UpdateFeedback(IEnumerable<APIBeatmapSet> likedSets)
        {
            var likes = likedSets.Where(s => s.Beatmaps.Any(m => m.RulesetID == Mode)).DistinctBy(s => s.OnlineID).TakeLast(100).Reverse().ToArray();
            Taste.SetLikes(likes);
        }

        internal string[] Queries(int batch, DiscoveryFilters? filters = null, DiscoveryWorkMode workMode = DiscoveryWorkMode.Taste)
        {
            string range = (filters ?? DefaultFilters).Query;
            if (workMode == DiscoveryWorkMode.Gameplay) return new[] { range };
            return Taste.Queries(batch, range);
        }

        internal DiscoveryFilters DefaultFilters => new DiscoveryFilters(Math.Max(0, TargetStars - 1), TargetStars + 0.75);

        internal IReadOnlyList<DiscoveryCandidate> Rank(IEnumerable<APIBeatmapSet> sets, IReadOnlySet<int> excluded, DiscoveryFilters? filters = null, DiscoveryWorkMode workMode = DiscoveryWorkMode.Taste, IReadOnlyDictionary<int, double>? gameplayWeights = null)
        {
            return sets.Where(s => s.OnlineID > 0 && !s.HasExplicitContent && !KnownSets.Contains(s.OnlineID) && !excluded.Contains(s.OnlineID)
                           && !knownSongs.Contains(DiscoveryHistory.SongKey(s)))
                       .DistinctBy(s => s.OnlineID)
                       .Select(s =>
                       {
                           foreach (var map in s.Beatmaps) map.BeatmapSet = s;
                           var nearest = s.Beatmaps.Where(m => m.RulesetID == Mode && (filters ?? DefaultFilters).Matches(m))
                                          .OrderByDescending(m => workMode == DiscoveryWorkMode.Gameplay ? gameplayWeights?.GetValueOrDefault(m.OnlineID) ?? 0 : 0)
                                          .ThenBy(m => Math.Abs(m.StarRating - TargetStars)).FirstOrDefault();
                           if (nearest == null) return null;
                           if (workMode == DiscoveryWorkMode.Taste)
                           {
                               var music = Taste.Rank(s);
                               return new DiscoveryCandidate(nearest, music.Reason, music.Weight);
                           }
                           double gameplay = gameplayWeights?.GetValueOrDefault(nearest.OnlineID) ?? 0;
                           double weight = gameplay * 10 - Math.Abs(nearest.StarRating - TargetStars)
                                           - Math.Min(1, Math.Abs(nearest.BPM - TargetBpm) / 150);
                           return new DiscoveryCandidate(nearest, gameplay > 0 ? DiscoveryReason.Gameplay : DiscoveryReason.Similar, weight);
                       })
                       .OfType<DiscoveryCandidate>().OrderByDescending(c => c.Weight).Take(240).ToArray();
        }

        /// <summary>Hard quotas: a narrow artist query must not become an artist-only recommendation feed.</summary>
        internal static IReadOnlyList<DiscoveryCandidate> Diversify(IEnumerable<DiscoveryCandidate> ranked, int limit = 24,
                                                                    string? previousArtist = null, string? previousMapper = null)
        {
            var pool = ranked.DistinctBy(c => c.Map.OnlineBeatmapSetID).ToList();
            var result = new List<DiscoveryCandidate>();
            var artists = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var mappers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var songs = new HashSet<string>();
            while (result.Count < limit)
            {
                var next = pool.FirstOrDefault(c =>
                {
                    var set = c.Map.BeatmapSet!;
                    return (DiscoveryHistory.SongKey(set) is not { Length: > 0 } song || !songs.Contains(song))
                           && !string.Equals(set.Artist, previousArtist, StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(set.AuthorString, previousMapper, StringComparison.OrdinalIgnoreCase)
                           && artists.GetValueOrDefault(set.Artist) < 2 && mappers.GetValueOrDefault(set.AuthorString) < 3;
                });
                if (next == null) break; // Return a smaller honest batch rather than fill with one artist.
                result.Add(next); pool.Remove(next);
                var selected = next.Map.BeatmapSet!;
                songs.Add(DiscoveryHistory.SongKey(selected));
                previousArtist = selected.Artist;
                previousMapper = selected.AuthorString;
                artists[selected.Artist] = artists.GetValueOrDefault(selected.Artist) + 1;
                mappers[selected.AuthorString] = mappers.GetValueOrDefault(selected.AuthorString) + 1;
            }
            return result;
        }

        private static double weightedMedian(IEnumerable<(double Value, double Weight)> values, double fallback)
        {
            var sorted = values.OrderBy(v => v.Value).ToArray();
            if (sorted.Length == 0) return fallback;
            double halfway = sorted.Sum(v => v.Weight) / 2;
            double total = 0;
            foreach (var item in sorted)
            {
                total += item.Weight;
                if (total > halfway) return item.Value;
            }
            return sorted[^1].Value;
        }

    }
}
