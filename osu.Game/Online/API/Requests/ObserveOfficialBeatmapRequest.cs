// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Net.Http;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Sends a metadata-only freshness hint to Mosu after the official osu! API
    /// reports that a locally known beatmap changed.
    /// </summary>
    internal class ObserveOfficialBeatmapRequest : APIRequest
    {
        private readonly APIBeatmap beatmap;

        public ObserveOfficialBeatmapRequest(APIBeatmap beatmap)
        {
            this.beatmap = beatmap;
        }

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Post;

            if (beatmap.BeatmapSet != null)
            {
                request.AddParameter("beatmapset_status", status(beatmap.BeatmapSet.Status), RequestParameterType.Query);

                if (beatmap.BeatmapSet.LastUpdated is { } setLastUpdated)
                    request.AddParameter("beatmapset_last_updated", setLastUpdated.ToString("O", CultureInfo.InvariantCulture), RequestParameterType.Query);
            }

            request.AddParameter("beatmap_id", beatmap.OnlineID.ToString(CultureInfo.InvariantCulture), RequestParameterType.Query);
            request.AddParameter("beatmap_status", status(beatmap.Status), RequestParameterType.Query);
            request.AddParameter("beatmap_last_updated", beatmap.LastUpdated.ToString("O", CultureInfo.InvariantCulture), RequestParameterType.Query);

            if (!string.IsNullOrEmpty(beatmap.Checksum))
                request.AddParameter("beatmap_checksum", beatmap.Checksum, RequestParameterType.Query);

            return request;
        }

        protected override string Uri =>
            $@"{API!.Endpoints.APIUrl}/api/private/beatmapsets/{beatmap.OnlineBeatmapSetID}/official-observation";

        protected override string Target => string.Empty;

        private static string status(BeatmapOnlineStatus value) => value.ToString().ToLowerInvariant();
    }
}
