// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    public class APIMedal
    {
        [JsonProperty("achievement_id")]
        public int ID;

        [JsonProperty("name")]
        public string Name = string.Empty;

        [JsonProperty("slug")]
        public string Slug = string.Empty;

        [JsonProperty("description")]
        public string Description = string.Empty;

        [JsonProperty("grouping")]
        public string Grouping = string.Empty;

        [JsonProperty("ordering")]
        public int Ordering;

        [JsonProperty("mode")]
        public string? Mode;

        [JsonProperty("icon_url")]
        public string IconUrl = string.Empty;

        [JsonProperty("icon_url_2x")]
        public string IconUrl2x = string.Empty;

        [JsonProperty("achieved")]
        public bool Achieved;

        [JsonProperty("achieved_at")]
        public DateTimeOffset? AchievedAt;
    }
}
