// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online
{
    public class ProductionEndpointConfiguration : EndpointConfiguration
    {
        public ProductionEndpointConfiguration()
        {
            WebsiteUrl = APIUrl = MosuServerEnvironment.ServerUrl;
            UpdateUrl = MosuServerEnvironment.UpdateUrl;
            APIClientSecret = MosuClientAuthentication.OAuthClientSecret;
            APIClientID = MosuClientAuthentication.OAuthClientId;
            SpectatorUrl = $@"{APIUrl}/spectator";
            MultiplayerUrl = $@"{APIUrl}/multiplayer";
            MetadataUrl = $@"{APIUrl}/metadata";
            BeatmapSubmissionServiceUrl = $@"{APIUrl}/beatmap-submission";
        }
    }
}
