// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online
{
    /// <summary>
    /// Official osu! API endpoints used by the capability-limited beatmap client.
    /// Realtime endpoints are intentionally omitted so this configuration cannot publish presence.
    /// </summary>
    public sealed class OfficialOsuEndpointConfiguration : EndpointConfiguration
    {
        public OfficialOsuEndpointConfiguration()
        {
            WebsiteUrl = APIUrl = @"https://osu.ppy.sh";

            APIClientID = string.Empty;
            APIClientSecret = string.Empty;

            SpectatorUrl = string.Empty;
            MultiplayerUrl = string.Empty;
            MetadataUrl = string.Empty;
            BeatmapSubmissionServiceUrl = null;
            LivenessProbeUrl = null;
        }
    }
}
