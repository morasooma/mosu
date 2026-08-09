// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.Spectator;
using osu.Game.Scoring;
using osu.Game.Users;

namespace osu.Game.Online
{
    /// <summary>
    /// A persistent component that binds to the spectator server and API in order to deliver updates about the logged in user's gameplay statistics.
    /// </summary>
    public partial class UserStatisticsWatcher : Component
    {
        private readonly LocalUserStatisticsProvider statisticsProvider;

        public IBindable<ScoreBasedUserStatisticsUpdate?> LatestUpdate => latestUpdate;
        private readonly Bindable<ScoreBasedUserStatisticsUpdate?> latestUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();

        [Resolved]
        private SpectatorClient spectatorClient { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private readonly Dictionary<long, ScoreInfo> watchedScores = new Dictionary<long, ScoreInfo>();
        private readonly LinkedList<long> earlyProcessedScores = new LinkedList<long>();

        private const int max_early_processed_scores = 64;

        public UserStatisticsWatcher(LocalUserStatisticsProvider statisticsProvider)
        {
            this.statisticsProvider = statisticsProvider;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            spectatorClient.OnUserScoreProcessed += userScoreProcessed;
        }

        /// <summary>
        /// Registers for a user statistics update after the given <paramref name="score"/> has been processed server-side.
        /// </summary>
        /// <param name="score">The score to listen for the statistics update for.</param>
        public void RegisterForStatisticsUpdateAfter(ScoreInfo score)
        {
            Schedule(() =>
            {
                if (!api.IsLoggedIn)
                    return;

                if ((!score.Ruleset.IsLegacyRuleset() && !MosuServerEnvironment.IsThirdPartyServer && !MosuServerEnvironment.SupportsSpecialRulesets) || score.OnlineID <= 0)
                    return;

                LinkedListNode<long>? earlyEvent = earlyProcessedScores.Find(score.OnlineID);

                if (earlyEvent != null)
                {
                    earlyProcessedScores.Remove(earlyEvent);
                    fetchStatisticsUpdate(score);
                    return;
                }

                watchedScores.Add(score.OnlineID, score);

                // The spectator server may not send UserScoreProcessed events (e.g. third-party servers,
                // or non-ranked beatmaps on official server). Use Task.Delay so the fallback fires even if
                // the component's scheduler is not pumping at this moment.
                long onlineId = score.OnlineID;

                Task.Run(async () =>
                {
                    await Task.Delay(5000).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        if (watchedScores.Remove(onlineId, out var fallbackScoreInfo))
                            fetchStatisticsUpdate(fallbackScoreInfo);
                    });
                });
            });
        }

        private void userScoreProcessed(int userId, long scoreId)
        {
            if (userId != api.LocalUser.Value?.OnlineID)
                return;

            if (!watchedScores.Remove(scoreId, out var scoreInfo))
            {
                LinkedListNode<long>? existing = earlyProcessedScores.Find(scoreId);

                if (existing != null)
                    earlyProcessedScores.Remove(existing);

                earlyProcessedScores.AddLast(scoreId);

                if (earlyProcessedScores.Count > max_early_processed_scores)
                    earlyProcessedScores.RemoveFirst();

                return;
            }

            fetchStatisticsUpdate(scoreInfo);
        }

        private void fetchStatisticsUpdate(ScoreInfo scoreInfo)
        {
            statisticsProvider.RefetchStatistics(scoreInfo, u => Schedule(() =>
            {
                var before = u.OldStatistics ?? new UserStatistics();
                latestUpdate.Value = new ScoreBasedUserStatisticsUpdate(scoreInfo, before, u.NewStatistics);
            }));
        }

        protected override void Dispose(bool isDisposing)
        {
            if (spectatorClient.IsNotNull())
                spectatorClient.OnUserScoreProcessed -= userScoreProcessed;

            base.Dispose(isDisposing);
        }
    }
}
