// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API
{
    internal static class ServerExclusiveBeatmapResources
    {
        public static void Resolve(APIBeatmapSet beatmapSet, EndpointConfiguration endpoints)
        {
            beatmapSet.Covers = new BeatmapSetOnlineCovers
            {
                CoverLowRes = ResolveUrl(beatmapSet.Covers.CoverLowRes, endpoints),
                Cover = ResolveUrl(beatmapSet.Covers.Cover, endpoints),
                CardLowRes = ResolveUrl(beatmapSet.Covers.CardLowRes, endpoints),
                Card = ResolveUrl(beatmapSet.Covers.Card, endpoints),
                ListLowRes = ResolveUrl(beatmapSet.Covers.ListLowRes, endpoints),
                List = ResolveUrl(beatmapSet.Covers.List, endpoints),
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

            if (Uri.TryCreate(value, UriKind.Absolute, out _))
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
