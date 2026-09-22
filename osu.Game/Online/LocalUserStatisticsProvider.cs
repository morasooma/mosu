// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Legacy;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Users;

namespace osu.Game.Online
{
    /// <summary>
    /// A component that keeps track of the latest statistics for the local user.
    /// </summary>
    public partial class LocalUserStatisticsProvider : Component
    {
        /// <summary>
        /// Invoked whenever a change occured to the statistics of any ruleset,
        /// either due to change in local user (log out and log in) or as a result of score submission.
        /// </summary>
        /// <remarks>
        /// This does not guarantee the presence of the old statistics,
        /// specifically in the case of initial population or change in local user.
        /// </remarks>
        public event Action<UserStatisticsUpdate>? StatisticsUpdated;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private StableBanchoSession? stableBanchoSession { get; set; }

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        private readonly Dictionary<string, UserStatistics> statisticsCache = new Dictionary<string, UserStatistics>();

        /// <summary>
        /// Returns the <see cref="UserStatistics"/> currently available for the given ruleset.
        /// This may return null if the requested statistics has not been fetched before yet.
        /// </summary>
        /// <param name="ruleset">The ruleset to return the corresponding <see cref="UserStatistics"/> for.</param>
        public UserStatistics? GetStatisticsFor(RulesetInfo ruleset) => statisticsCache.GetValueOrDefault(ruleset.ShortName);

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (stableBanchoSession != null)
                stableBanchoSession.UserStatisticsUpdated += onStableUserStatisticsUpdated;

            localUser.BindTo(api.LocalUser);
            localUser.BindValueChanged(_ =>
            {
                // queuing up requests directly on user change is unsafe, as the API status may have not been updated yet.
                // schedule a frame to allow the API to be in its correct state sending requests.
                Schedule(initialiseStatistics);
            }, true);
        }

        private void initialiseStatistics()
        {
            statisticsCache.Clear();

            if (api.LocalUser.Value == null || api.LocalUser.Value.Id <= 1)
                return;

            if (MosuServerEnvironment.UsesStableProtocol)
            {
                if (stableBanchoSession == null)
                    return;

                foreach (var ruleset in rulesets.AvailableRulesets.Where(r => r.IsLegacyRuleset()))
                {
                    StableBanchoUserStatistics? statistics = stableBanchoSession.GetUserStatistics(api.LocalUser.Value.Id, ruleset.OnlineID);
                    if (statistics != null)
                        UpdateStatistics(toUserStatistics(statistics), ruleset);
                }

                return;
            }

            foreach (var ruleset in rulesets.AvailableRulesets.Where(r => r.IsLegacyRuleset() || MosuServerEnvironment.IsThirdPartyServer))
            {
                RefetchStatistics(ruleset);

                if (MosuServerEnvironment.SupportsSpecialRulesets)
                {
                    if (MosuServerEnvironment.OnlyOsuRelax)
                    {
                        if (ruleset.ShortName == RulesetInfo.OSU_MODE_SHORTNAME)
                            RefetchStatistics(ruleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));
                    }
                    else
                    {
                        switch (ruleset.ShortName)
                        {
                            case RulesetInfo.OSU_MODE_SHORTNAME:
                                RefetchStatistics(ruleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));
                                RefetchStatistics(ruleset.CreateSpecialRuleset(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_ONLINE_ID));
                                break;

                            case RulesetInfo.TAIKO_MODE_SHORTNAME:
                                RefetchStatistics(ruleset.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID));
                                break;

                            case RulesetInfo.CATCH_MODE_SHORTNAME:
                                RefetchStatistics(ruleset.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID));
                                break;
                        }
                    }
                }
            }
        }

        public void RefetchStatistics(RulesetInfo ruleset, Action<UserStatisticsUpdate>? callback = null)
        {
            if (!ruleset.IsLegacyRuleset() && !MosuServerEnvironment.IsThirdPartyServer && !MosuServerEnvironment.SupportsSpecialRulesets)
                throw new InvalidOperationException($@"Retrieving statistics is not supported for ruleset {ruleset.ShortName}");

            var request = new GetUserRequest(api.LocalUser.Value.Id, ruleset);
            request.Success += u => UpdateStatistics(u.Statistics, ruleset, callback);
            request.Failure += _ =>
            {
                var cached = statisticsCache.GetValueOrDefault(ruleset.ShortName);
                callback?.Invoke(new UserStatisticsUpdate(ruleset, cached, cached ?? new UserStatistics()));
            };
            api.Queue(request);
        }

        public void RefetchStatistics(ScoreInfo score, Action<UserStatisticsUpdate>? callback = null)
        {
            var specialRuleset = score.Ruleset.CreateSpecialRulesetByScore(score);
            RefetchStatistics(specialRuleset ?? score.Ruleset, callback);
        }

        protected void UpdateStatistics(UserStatistics newStatistics, RulesetInfo ruleset, Action<UserStatisticsUpdate>? callback = null)
        {
            var oldStatistics = statisticsCache.GetValueOrDefault(ruleset.ShortName);
            statisticsCache[ruleset.ShortName] = newStatistics;

            var update = new UserStatisticsUpdate(ruleset, oldStatistics, newStatistics);
            callback?.Invoke(update);
            StatisticsUpdated?.Invoke(update);
        }

        private void onStableUserStatisticsUpdated(StableBanchoUserStatistics statistics)
        {
            if (statistics.UserId != api.LocalUser.Value.Id)
                return;

            RulesetInfo? ruleset = rulesets.AvailableRulesets.FirstOrDefault(r => r.OnlineID == statistics.RulesetId);
            if (ruleset != null)
                UpdateStatistics(toUserStatistics(statistics), ruleset);
        }

        private UserStatistics toUserStatistics(StableBanchoUserStatistics statistics) => new UserStatistics
        {
            User = api.LocalUser.Value,
            IsRanked = statistics.GlobalRank > 0,
            GlobalRank = statistics.GlobalRank > 0 ? statistics.GlobalRank : null,
            PP = statistics.PerformancePoints >= 0 ? statistics.PerformancePoints : null,
            RankedScore = statistics.RankedScore,
            Accuracy = statistics.Accuracy <= 1 ? statistics.Accuracy * 100 : statistics.Accuracy,
            PlayCount = statistics.PlayCount,
            TotalScore = statistics.TotalScore,
        };

        protected override void Dispose(bool isDisposing)
        {
            if (stableBanchoSession != null)
                stableBanchoSession.UserStatisticsUpdated -= onStableUserStatisticsUpdated;

            base.Dispose(isDisposing);
        }
    }

    public record UserStatisticsUpdate(RulesetInfo Ruleset, UserStatistics? OldStatistics, UserStatistics NewStatistics);
}
