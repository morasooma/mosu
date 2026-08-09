// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace osu.Game.Users
{
    /// <summary>
    /// A ranking entry returned by the server-side weekly comparison endpoint.
    /// </summary>
    public class WeeklyRankingEntry : UserStatistics
    {
        [JsonProperty("rank_change")]
        public WeeklyRankChange RankChange;
    }

    public class WeeklyRankChange
    {
        [JsonProperty("status")]
        [JsonConverter(typeof(StringEnumConverter))]
        public WeeklyRankChangeStatus Status;

        [JsonProperty("delta")]
        public int? Delta;

        [JsonProperty("previous_rank")]
        public int? PreviousRank;
    }

    public enum WeeklyRankChangeStatus
    {
        Up,
        Down,
        Same,
        New,
        Unavailable,
    }
}
