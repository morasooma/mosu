// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;

namespace osu.Game.Online.API.Requests
{
    public class GetUserRequest : APIRequest<APIUser>
    {
        public readonly string Lookup;
        public readonly IRulesetInfo? Ruleset;
        public readonly string? Variant;
        private readonly LookupType lookupType;

        /// <summary>
        /// Gets a user from their ID.
        /// </summary>
        /// <param name="userId">The user to get.</param>
        /// <param name="ruleset">The ruleset to get the user's info for.</param>
        /// <param name="variant">An optional ruleset variant.</param>
        public GetUserRequest(long? userId = null, IRulesetInfo? ruleset = null, string? variant = null)
        {
            Lookup = userId.ToString()!;
            lookupType = LookupType.Id;
            Ruleset = ruleset;
            Variant = variant;
        }

        /// <summary>
        /// Gets a user from their username.
        /// </summary>
        /// <param name="username">The user to get.</param>
        /// <param name="ruleset">The ruleset to get the user's info for.</param>
        /// <param name="variant">An optional ruleset variant.</param>
        public GetUserRequest(string username, IRulesetInfo? ruleset = null, string? variant = null)
        {
            Lookup = username;
            lookupType = LookupType.Username;
            Ruleset = ruleset;
            Variant = variant;
        }

        protected override string Target => $@"users/{Lookup}/{Ruleset?.ShortName}?key={lookupType.ToString().ToLowerInvariant()}{(Variant == null ? string.Empty : $"&variant={Variant}")}";

        private enum LookupType
        {
            Id,
            Username
        }
    }
}
