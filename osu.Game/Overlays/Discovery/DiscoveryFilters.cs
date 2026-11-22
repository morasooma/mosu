using System;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Overlays.Discovery
{
    internal sealed record DiscoveryFilters(double MinimumStars, double MaximumStars, SearchCategory Category = SearchCategory.Any,
                                             double MinimumBpm = 0, double MaximumBpm = 1000, double MaximumLength = double.MaxValue)
    {
        internal bool Matches(APIBeatmap map) => double.IsFinite(map.StarRating) && map.StarRating >= MinimumStars && map.StarRating <= MaximumStars
                                                && MatchesStatus(map.Status)
                                                && map.BPM >= MinimumBpm && map.BPM <= MaximumBpm && map.Length <= MaximumLength;
        private bool MatchesStatus(BeatmapOnlineStatus status) => Category switch
        {
            SearchCategory.Ranked => status is BeatmapOnlineStatus.Ranked or BeatmapOnlineStatus.Approved,
            SearchCategory.Leaderboard => status is BeatmapOnlineStatus.Ranked or BeatmapOnlineStatus.Approved or BeatmapOnlineStatus.Loved or BeatmapOnlineStatus.Qualified,
            SearchCategory.Qualified => status == BeatmapOnlineStatus.Qualified,
            SearchCategory.Loved => status == BeatmapOnlineStatus.Loved,
            SearchCategory.Pending => status is BeatmapOnlineStatus.Pending or BeatmapOnlineStatus.WIP,
            SearchCategory.Graveyard => status == BeatmapOnlineStatus.Graveyard,
            _ => true,
        };
        internal string Query => FormattableString.Invariant($"star>={MinimumStars:0.##} star<={MaximumStars:0.##} bpm>={MinimumBpm:0.##} bpm<={MaximumBpm:0.##}")
                                 + (MaximumLength < double.MaxValue ? FormattableString.Invariant($" length<={MaximumLength / 1000:0}") : string.Empty);
    }
}
