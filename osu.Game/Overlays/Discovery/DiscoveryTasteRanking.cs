using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Overlays.Discovery
{
    internal sealed class DiscoveryTasteRanking
    {
        private DiscoveryTasteProfile profile = new DiscoveryTasteProfile();
        private APIBeatmapSet[] likes = Array.Empty<APIBeatmapSet>();
        private APIBeatmapSet[] examples = Array.Empty<APIBeatmapSet>();
        private bool manual;
        internal IEnumerable<DiscoveryTasteCandidate> Candidates => manual ? Array.Empty<DiscoveryTasteCandidate>() : profile.Candidates;
        internal void SetExamples(IEnumerable<APIBeatmap> maps)
        {
            manual = true;
            examples = maps.Where(m => m.BeatmapSet != null).Select(m => m.BeatmapSet!).DistinctBy(s => s.OnlineID).ToArray();
        }
        internal void SetProfile(DiscoveryTasteProfile value) => profile = value;
        internal void SetLikes(IEnumerable<APIBeatmapSet> value) => likes = value.TakeLast(100).ToArray();
        internal static string Key(string text) => string.Join(" ", new string((text ?? string.Empty).Normalize(NormalizationForm.FormKC).ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        private static bool matches(string text, string signal) => (" " + Key(text) + " ").Contains(" " + Key(signal) + " ", StringComparison.Ordinal);

        internal SearchGenre Genre(int batch)
        {
            if (manual) return SearchGenre.Any;
            var names = profile.Genres.Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
            if (names.Length == 0) return SearchGenre.Any;
            string name = Key(names[batch % names.Length]).Replace(" ", string.Empty, StringComparison.Ordinal);
            return Enum.GetValues<SearchGenre>().FirstOrDefault(g => string.Equals(g.ToString(), name, StringComparison.OrdinalIgnoreCase));
        }

        internal string[] Queries(int batch, string range)
        {
            var queries = new List<string>();
            // Saved music is explicit positive feedback, not evidence of mapper preference.
            var artists = (manual ? examples.Select(s => s.Artist)
                : profile.RelatedArtists.Select(a => a.Name).Concat(likes.AsEnumerable().Reverse().Select(s => s.Artist)).Concat(profile.Artists.Select(a => a.Name)))
                .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (artists.Length > 0) queries.Add(Key(artists[batch % artists.Length]) + " " + range);
            var signals = manual ? Array.Empty<string>() : profile.Tags.Select(t => t.Name).Distinct().ToArray();
            if (signals.Length > 0) queries.Add(Key(signals[batch % signals.Length]) + " " + range);
            queries.Add(range);
            return queries.Distinct().ToArray();
        }

        internal (double Weight, DiscoveryReason Reason) Rank(APIBeatmapSet set)
        {
            string artist = Key(set.Artist);
            if (manual)
            {
                // Metadata-only candidate retrieval until the optional audio model is verified.
                // Do not mix account-wide peer/artist preferences into a manually chosen direction.
                double exampleWeight = examples.Any(s => Key(s.Artist) == artist) ? 2 : 0;
                return (exampleWeight, exampleWeight > 0 ? DiscoveryReason.Artist : DiscoveryReason.Similar);
            }
            var own = profile.Artists.FirstOrDefault(a => Key(a.Name) == artist);
            double total = profile.Artists.Select(a => a.Weight).DefaultIfEmpty(1).Max();
            double knownArtist = own == null ? 0 : own.Weight / Math.Max(1, total);
            double liked = Math.Min(1, likes.Count(s => Key(s.Artist) == artist) / 3d);
            double genre = profile.Genres.Where(g => Key(set.Genre.Name) == Key(g.Name)).Select(g => g.Weight).DefaultIfEmpty(0).Max()
                           / Math.Max(1, profile.Genres.Select(g => g.Weight).DefaultIfEmpty(1).Max());
            double tag = profile.Tags.Where(t => matches(set.Tags, t.Name)).Select(t => t.Weight).DefaultIfEmpty(0).Max()
                         / Math.Max(1, profile.Tags.Select(t => t.Weight).DefaultIfEmpty(1).Max());
            var peer = profile.Candidates.FirstOrDefault(c => c.SetId == set.OnlineID);
            double peerWeight = peer == null ? 0 : peer.Weight / Math.Max(.000001, profile.Candidates.Select(c => c.Weight).DefaultIfEmpty(1).Max());
            var related = profile.RelatedArtists.FirstOrDefault(a => Key(a.Name) == artist);
            double relatedWeight = related == null ? 0 : related.Weight / Math.Max(.000001, profile.RelatedArtists.Select(a => a.Weight).DefaultIfEmpty(1).Max());
            double weight = peerWeight * 6 + relatedWeight * 3 + knownArtist * 2 + liked * 2 + genre + tag * 2;
            return (weight, peerWeight > 0 || relatedWeight > 0 ? DiscoveryReason.Listeners : knownArtist > 0 || liked > 0 ? DiscoveryReason.Artist
                : genre > 0 || tag > 0 ? DiscoveryReason.Music : DiscoveryReason.Similar);
        }
    }
}
