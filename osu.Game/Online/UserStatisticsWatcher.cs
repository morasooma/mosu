// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
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

        private readonly Dictionary<long, WatchedScore> watchedScores = new Dictionary<long, WatchedScore>();
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
            api.LocalUser.ValueChanged += onUserChanged;
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

                // Repeated registration must not start another poll or reset its deadline.
                if (watchedScores.ContainsKey(score.OnlineID))
                    return;

                var watched = new WatchedScore(score, api.LocalUser.Value.Id, Time.Current + 10 * 60 * 1000);
                watchedScores.Add(score.OnlineID, watched);

                if (earlyEvent != null)
                {
                    earlyProcessedScores.Remove(earlyEvent);
                    fetchStatisticsUpdate(score, watched);
                    return;
                }

                if (MosuServerEnvironment.UsesMosuPresence && !MosuServerEnvironment.IsThirdPartyServer && !MosuServerEnvironment.UsesStableProtocol)
                    Scheduler.AddDelayed(() => pollScore(watched), 250);
                else
                {
                    // Other servers need not expose Mosu's durable processed flag.
                    Scheduler.AddDelayed(() =>
                    {
                        if (isCurrent(watched) && watchedScores.Remove(score.OnlineID))
                            fetchStatisticsUpdate(score);
                    }, 5000);
                }
            });
        }

        private void onUserChanged(ValueChangedEvent<APIUser> change)
        {
            if (change.OldValue?.Id == change.NewValue?.Id)
                return;

            clearWatches();
            latestUpdate.Value = null;
        }

        private void clearWatches()
        {
            var requests = new List<GetScoreRequest>();
            foreach (var watched in watchedScores.Values)
            {
                if (watched.Request != null)
                    requests.Add(watched.Request);
            }
            watchedScores.Clear();
            earlyProcessedScores.Clear();
            foreach (var request in requests)
                request.Cancel();
        }

        private void userScoreProcessed(int userId, long scoreId)
        {
            if (!isCurrentUser(userId))
                return;

            if (!watchedScores.TryGetValue(scoreId, out var scoreInfo))
            {
                LinkedListNode<long>? existing = earlyProcessedScores.Find(scoreId);

                if (existing != null)
                    earlyProcessedScores.Remove(existing);

                earlyProcessedScores.AddLast(scoreId);

                if (earlyProcessedScores.Count > max_early_processed_scores)
                    earlyProcessedScores.RemoveFirst();

                return;
            }

            if (isCurrentUser(scoreInfo.UserId))
                fetchStatisticsUpdate(scoreInfo.Score, scoreInfo);
        }

        private bool isCurrentUser(int userId) => api.IsLoggedIn && api.LocalUser.Value.Id == userId;

        private bool isCurrent(WatchedScore watched) =>
            isCurrentUser(watched.UserId) && watchedScores.TryGetValue(watched.Score.OnlineID, out var current) && ReferenceEquals(current, watched);

        private void pollScore(WatchedScore watched)
        {
            if (!isCurrent(watched) || watched.FetchingStatistics)
                return;

            var request = new GetScoreRequest(watched.Score.OnlineID);
            watched.Request = request;
            request.Success += result => Schedule(() =>
            {
                if (!isCurrent(watched))
                    return;

                if (result.Processed && result.OnlineID == watched.Score.OnlineID && result.UserID == watched.UserId)
                {
                    fetchStatisticsUpdate(watched.Score, watched);
                }
                else
                    retryPoll(watched);
            });
            request.Failure += _ => Schedule(() => retryPoll(watched));
            api.Queue(request);
        }

        private void retryPoll(WatchedScore watched)
        {
            if (!isCurrent(watched) || watched.FetchingStatistics)
                return;

            // Keep recovering beyond a worker's 30/60-second retries, without hammering
            // the API for ten minutes. A timeout must never publish unprocessed stats.
            if (Time.Current >= watched.Deadline)
            {
                watchedScores.Remove(watched.Score.OnlineID);
                return;
            }

            watched.Attempts++;
            double delay = watched.Attempts >= 35 ? 10_000 : Math.Min(2000, 250 * (1 << Math.Min(watched.Attempts, 3)));
            Scheduler.AddDelayed(() => pollScore(watched), delay);
        }

        private sealed class WatchedScore
        {
            public readonly ScoreInfo Score;
            public readonly int UserId;
            public readonly double Deadline;
            public int Attempts;
            public bool FetchingStatistics;
            public GetScoreRequest? Request;

            public WatchedScore(ScoreInfo score, int userId, double deadline)
            {
                Score = score;
                UserId = userId;
                Deadline = deadline;
            }
        }

        private void fetchStatisticsUpdate(ScoreInfo scoreInfo, WatchedScore? watched = null)
        {
            if (watched?.FetchingStatistics == true)
                return;
            if (watched != null)
                watched.FetchingStatistics = true;

            int userId = api.LocalUser.Value.Id;
            statisticsProvider.RefetchStatistics(scoreInfo, u => Schedule(() =>
            {
                if (!isCurrentUser(userId) || watched != null && !isCurrent(watched))
                    return;

                if (watched != null)
                    watchedScores.Remove(scoreInfo.OnlineID);
                var before = u.OldStatistics ?? new UserStatistics();
                latestUpdate.Value = new ScoreBasedUserStatisticsUpdate(scoreInfo, before, u.NewStatistics);
            }), watched == null ? null : () => Schedule(() =>
            {
                watched.FetchingStatistics = false;
                retryPoll(watched);
            }));
        }

        protected override void Dispose(bool isDisposing)
        {
            if (spectatorClient.IsNotNull())
                spectatorClient.OnUserScoreProcessed -= userScoreProcessed;

            if (api.IsNotNull())
                api.LocalUser.ValueChanged -= onUserChanged;
            clearWatches();

            base.Dispose(isDisposing);
        }
    }
}
