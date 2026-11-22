// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API
{
    internal static class ServerExclusiveBeatmapResources
    {
        public static void ResolveResponse(object response, EndpointConfiguration endpoints)
        {
            switch (response)
            {
                case APIBeatmapSet set:
                    if (set.OnlineID >= BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD)
                        Resolve(set, endpoints);
                    break;

                case APIBeatmap { BeatmapSet: { } set }:
                    ResolveResponse(set, endpoints);
                    break;

                case SearchBeatmapSetsResponse search:
                    ResolveResponse(search.BeatmapSets, endpoints);
                    break;

                case GetBeatmapsResponse batch:
                    foreach (var beatmap in batch.Beatmaps)
                        ResolveResponse(beatmap, endpoints);
                    break;

                case IEnumerable<APIBeatmapSet> sets:
                    foreach (var set in sets)
                        ResolveResponse(set, endpoints);
                    break;

                case IEnumerable<APIUserMostPlayedBeatmap> mostPlayed:
                    foreach (var item in mostPlayed)
                        ResolveResponse(item.BeatmapSet, endpoints);
                    break;
            }
        }

        public static void Resolve(APIBeatmapSet beatmapSet, EndpointConfiguration endpoints)
        {
            // Legacy records can lack covers even though the archive contains a background.
            string? fallback = beatmapSet.Beatmaps.Length > 0
                ? ResolveUrl($"/file/beatmaps/server/background/{beatmapSet.Beatmaps[0].OnlineID}", endpoints)
                : null;
            beatmapSet.Covers = new BeatmapSetOnlineCovers
            {
                CoverLowRes = ResolveUrl(beatmapSet.Covers.CoverLowRes, endpoints),
                Cover = ResolveUrl(beatmapSet.Covers.Cover, endpoints) is { Length: > 0 } cover ? cover : fallback ?? string.Empty,
                CardLowRes = ResolveUrl(beatmapSet.Covers.CardLowRes, endpoints),
                Card = ResolveUrl(beatmapSet.Covers.Card, endpoints) is { Length: > 0 } card ? card : fallback ?? string.Empty,
                ListLowRes = ResolveUrl(beatmapSet.Covers.ListLowRes, endpoints),
                List = ResolveUrl(beatmapSet.Covers.List, endpoints) is { Length: > 0 } list ? list : fallback ?? string.Empty,
            };

            // Server-exclusive previews are proxied by Mosu. This also avoids sending
            // custom set IDs to b.ppy.sh, where no matching preview can exist.
            beatmapSet.Preview =
                $"{endpoints.APIUrl.TrimEnd('/')}/api/private/audio/beatmapset/{beatmapSet.OnlineID}";
        }

        internal static string ResolveUrl(string? value, EndpointConfiguration endpoints)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            value = value.Trim();

            if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)
                && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
                return value;

            if (value.StartsWith("//", StringComparison.Ordinal))
            {
                var apiUri = new Uri(endpoints.APIUrl, UriKind.Absolute);
                return $"{apiUri.Scheme}:{value}";
            }

            string relative = value.StartsWith("beatmaps/", StringComparison.OrdinalIgnoreCase)
                ? $"/file/{value}"
                : value;

            var root = new Uri($"{endpoints.APIUrl.TrimEnd('/')}/", UriKind.Absolute);
            return new Uri(root, relative).AbsoluteUri;
        }
    }
}
