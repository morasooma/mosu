// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.IO.Network;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Users;

namespace osu.Game.Online.API.Requests
{
    public class GetBestScorePpRankingsRequest : APIRequest<GetTopUsersResponse>
    {
        private readonly RulesetInfo ruleset;
        private readonly int page;
        private readonly CountryCode countryCode;
        private readonly string? variant;

        public GetBestScorePpRankingsRequest(RulesetInfo ruleset, int page = 1, CountryCode countryCode = CountryCode.Unknown, string? variant = null)
        {
            this.ruleset = ruleset;
            this.page = page;
            this.countryCode = countryCode;
            this.variant = variant;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            req.AddParameter("page", page.ToString());

            if (countryCode != CountryCode.Unknown)
                req.AddParameter("country", countryCode.ToString());

            if (variant != null)
                req.AddParameter("variant", variant);

            return req;
        }

        protected override string Target => string.Empty;

        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/rankings/{ruleset.ShortName}/best-score-pp";
    }
}
