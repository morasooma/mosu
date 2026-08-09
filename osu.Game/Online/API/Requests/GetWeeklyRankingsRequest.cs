// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Users;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Retrieves rankings together with their movement relative to the latest completed weekly snapshot.
    /// </summary>
    public class GetWeeklyRankingsRequest : APIRequest<GetWeeklyRankingsResponse>
    {
        private readonly RulesetInfo ruleset;
        public readonly WeeklyRankingsType Type;
        private readonly int page;
        private readonly CountryCode countryCode;
        private readonly string variant;

        public GetWeeklyRankingsRequest(RulesetInfo ruleset, WeeklyRankingsType type, int page = 1, CountryCode countryCode = CountryCode.Unknown, string variant = null)
        {
            this.ruleset = ruleset;
            Type = type;
            this.page = page;
            this.countryCode = countryCode;
            this.variant = variant;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            req.AddParameter("page", page.ToString());
            req.AddParameter("sort", getSort(Type));

            if (countryCode != CountryCode.Unknown)
                req.AddParameter("country", countryCode.ToString());

            if (variant != null)
                req.AddParameter("variant", variant);

            return req;
        }

        protected override string Target => string.Empty;

        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/rankings/{ruleset.ShortName}/weekly";

        private static string getSort(WeeklyRankingsType type) => type switch
        {
            WeeklyRankingsType.Performance => "performance",
            WeeklyRankingsType.Score => "score",
            WeeklyRankingsType.BestScorePp => "best_score_pp",
            _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    public class GetWeeklyRankingsResponse
    {
        [JsonProperty("ranking")]
        public List<WeeklyRankingEntry> Users;

        [JsonProperty("total")]
        public int Total;

        [JsonProperty("comparison_days")]
        public int ComparisonDays;

        [JsonProperty("snapshot_date")]
        public string SnapshotDate;

        [JsonProperty("history_available")]
        public bool HistoryAvailable;
    }

    public enum WeeklyRankingsType
    {
        Performance,
        Score,
        BestScorePp,
    }
}
