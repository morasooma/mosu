// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    public class APIUserAchievement
    {
        [JsonProperty("achievement_id")]
        public int ID;

        [JsonProperty("achieved_at")]
        public DateTimeOffset AchievedAt;

        [JsonProperty("name")]
        public string? Name;

        [JsonProperty("slug")]
        public string? Slug;

        [JsonProperty("description")]
        public string? Description;

        [JsonProperty("icon_url")]
        public string? IconUrl;

        [JsonProperty("icon_url_2x")]
        public string? IconUrl2x;
    }
}
