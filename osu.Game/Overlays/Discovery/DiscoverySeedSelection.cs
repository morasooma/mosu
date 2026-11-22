using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Discovery
{
    internal enum DiscoverySeedSource
    {
        [LocalisableDescription(typeof(DiscoveryStrings), nameof(DiscoveryStrings.MixedSeeds))]
        Mixed,
        [LocalisableDescription(typeof(DiscoveryStrings), nameof(DiscoveryStrings.TopSeeds))]
        Top,
        [LocalisableDescription(typeof(DiscoveryStrings), nameof(DiscoveryStrings.FrequentSeeds))]
        Frequent,
        [LocalisableDescription(typeof(DiscoveryStrings), nameof(DiscoveryStrings.ManualSeeds))]
        Manual,
    }

    internal readonly record struct DiscoveryReference(int Id, bool IsSet, int? DifficultyId = null);

    /// <summary>Personal playcounts, never global popularity. Explicit examples take priority over inferred taste.</summary>
    internal static class DiscoverySeedSelection
    {
        internal const int MAXIMUM_REFERENCES = 8;

        internal static bool TryParse(string input, out DiscoveryReference[] references)
        {
            var result = new List<DiscoveryReference>();
            references = Array.Empty<DiscoveryReference>();
            if (input.Length > 2048) return false;
            foreach (string part in input.Split(new[] { ' ', '\r', '\n', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                DiscoveryReference reference;
                if (int.TryParse(part, out int numeric) && valid(numeric))
                    reference = new DiscoveryReference(numeric, false);
                else
                {
                    if (!Uri.TryCreate(part, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                        || !string.IsNullOrEmpty(uri.UserInfo) || uri.Host is not ("morasooma.net" or "osu.ppy.sh")) return false;
                    var match = Regex.Match(uri.AbsolutePath, @"^/(beatmapsets|beatmaps|b)/(\d+)/?$");
                    if (!match.Success || !int.TryParse(match.Groups[2].Value, out int id) || !valid(id)) return false;
                    bool isSet = match.Groups[1].Value == "beatmapsets";
                    int? difficulty = null;
                    if (isSet && uri.Fragment.Length > 0)
                    {
                        var fragment = Regex.Match(uri.Fragment, @"^#(?:osu|taiko|fruits|mania)(?:/(\d+))?$");
                        if (!fragment.Success) return false;
                        if (fragment.Groups[1].Success)
                        {
                            if (!int.TryParse(fragment.Groups[1].Value, out int bid) || !valid(bid)) return false;
                            difficulty = bid;
                        }
                    }
                    reference = new DiscoveryReference(id, isSet, difficulty);
                }
                if (!result.Contains(reference)) result.Add(reference);
                if (result.Count > MAXIMUM_REFERENCES) return false;
            }
            references = result.ToArray();
            return references.Length > 0;
        }

        internal static APIBeatmap[] Select(IEnumerable<APIBeatmap> top, IEnumerable<APIBeatmap> frequent,
                                           IEnumerable<APIBeatmap> manual, DiscoverySeedSource source, int mode)
        {
            bool usable(APIBeatmap m) => m.BeatmapSet != null && m.RulesetID == mode && valid(m.OnlineID);
            var best = top.Where(usable).DistinctBy(m => m.OnlineID).Take(50).ToArray();
            var repeated = frequent.Where(usable).Where(m => m.UserPlayCount > 0)
                .OrderByDescending(m => m.UserPlayCount).DistinctBy(m => m.OnlineID).Take(50).ToArray();
            if (source == DiscoverySeedSource.Manual) return manual.Where(usable).DistinctBy(m => m.OnlineID).Take(MAXIMUM_REFERENCES).ToArray();
            if (source == DiscoverySeedSource.Top) return best;
            if (source == DiscoverySeedSource.Frequent) return repeated;
            // Round-robin: 10 PP-farm maps cannot crowd out the frequently played branch.
            return Enumerable.Range(0, Math.Max(best.Length, repeated.Length))
                .SelectMany(i => new[] { i < repeated.Length ? repeated[i] : null, i < best.Length ? best[i] : null })
                .OfType<APIBeatmap>().DistinctBy(m => m.OnlineID).Take(50).ToArray();
        }

        internal static double Weight(APIBeatmap map, DiscoverySeedSource source) =>
            source == DiscoverySeedSource.Manual ? 1 : 1 + Math.Min(4, Math.Log(1 + Math.Max(0, map.UserPlayCount)));

        internal static APIBeatmap? Resolve(DiscoveryReference reference, APIBeatmapSet set, int mode, double targetStars)
        {
            if (set.OnlineID != reference.Id || set.Beatmaps == null) return null;
            foreach (var map in set.Beatmaps) map.BeatmapSet = set;
            return set.Beatmaps.Where(m => m.RulesetID == mode && (!reference.DifficultyId.HasValue || m.OnlineID == reference.DifficultyId))
                .OrderBy(m => Math.Abs(m.StarRating - targetStars)).ThenBy(m => m.StarRating).FirstOrDefault();
        }

        private static bool valid(int id) => id > 0 && id < 2_000_000_000;
    }
}
