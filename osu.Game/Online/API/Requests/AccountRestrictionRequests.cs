// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests
{
    public class GetAccountRestrictionStatusRequest : APIRequest<AccountRestrictionStatusResponse>
    {
        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/account/restriction-status";
        protected override string Target => throw new NotSupportedException();
    }

    public class AccountRestrictionStatusResponse
    {
        [JsonProperty("is_restricted")]
        public bool IsRestricted { get; set; }

        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("discord_username")]
        public string? DiscordUsername { get; set; }

        [JsonProperty("discord_url")]
        public string? DiscordUrl { get; set; }
    }
}
