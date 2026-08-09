// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using Realms;

namespace osu.Game.Online.Leaderboards
{
    public partial class LeaderboardManager : Component
    {
        /// <summary>
        /// The latest leaderboard scores fetched by the criteria in <see cref="CurrentCriteria"/>.
        /// </summary>
        public IBindable<LeaderboardScores?> Scores => scores;

        private readonly Bindable<LeaderboardScores?> scores = new Bindable<LeaderboardScores?>();

        public LeaderboardCriteria? CurrentCriteria { get; private set; }

        private IDisposable? localScoreSubscription;
        private GetScoresRequest? inFlightOnlineRequest;
        private GetBeatmapRequest? inFlightBeatmapLookupRequest;
        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        /// <summary>
        /// Fetch leaderboard content with the new criteria specified in the background.
        /// On completion, <see cref="Scores"/> will be updated with the results from this call (unless a more recent call with a different criteria has completed).
        /// </summary>
        public void FetchWithCriteria(LeaderboardCriteria newCriteria, bool forceRefresh = false)
        {
            if (!ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException(@$"{nameof(FetchWithCriteria)} must be called from the update thread.");

            if (!forceRefresh && CurrentCriteria?.Equals(newCriteria) == true && scores.Value?.FailState == null)
                return;

            CurrentCriteria = newCriteria;
            localScoreSubscription?.Dispose();
            inFlightOnlineRequest?.Cancel();
            inFlightOnlineRequest = null;
            inFlightBeatmapLookupRequest?.Cancel();
            inFlightBeatmapLookupRequest = null;
            scores.Value = null;

            if (newCriteria.Beatmap == null || newCriteria.Ruleset == null)
            {
                scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NoneSelected);
                return;
            }

            switch (newCriteria.Scope)
            {
                case BeatmapLeaderboardScope.Local:
                {
                    // Scores are persisted against the playable (base) ruleset. Special rulesets are
                    // only an API representation used to address relax/autopilot leaderboards online.
                    // Callers may still pass one while viewing local scores (for example when RX is
                    // selected in song select), so normalise it before querying the local database.
                    var localRuleset = newCriteria.Ruleset.IsSpecialRuleset()
                        ? newCriteria.Ruleset.CreateNormalRuleset()
                        : newCriteria.Ruleset;

                    localScoreSubscription = realm.RegisterForNotifications(r =>
                    {
                        var allScores = r.All<ScoreInfo>();
                        string commonFilter = $" AND {nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.Hash)} == {nameof(ScoreInfo.BeatmapHash)}"
                                              + $" AND {nameof(ScoreInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $1"
                                              + $" AND {nameof(ScoreInfo.DeletePending)} == false";

                        if (string.IsNullOrEmpty(newCriteria.Beatmap.MD5Hash))
                            return allScores.Filter($"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.ID)} == $0" + commonFilter,
                                newCriteria.Beatmap.ID, localRuleset.ShortName);

                        return allScores.Filter($"({nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.ID)} == $0"
                                                + $" OR {nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.MD5Hash)} == $2)"
                                                + commonFilter,
                            newCriteria.Beatmap.ID, localRuleset.ShortName, newCriteria.Beatmap.MD5Hash);
                    }, localScoresChanged);
                    return;
                }

                default:
                {
                    if (!api.IsLoggedIn)
                    {
                        scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NotLoggedIn);
                        return;
                    }

                    if (!newCriteria.Ruleset.IsLegacyRuleset())
                    {
                        scores.Value = LeaderboardScores.Failure(LeaderboardFailState.RulesetUnavailable);
                        return;
                    }

                    if (newCriteria.Beatmap.OnlineID <= 0)
                    {
                        Logger.Log($@"Leaderboard unavailable for beatmap {newCriteria.Beatmap}: beatmap online id is {newCriteria.Beatmap.OnlineID}, set online id is {newCriteria.Beatmap.BeatmapSet?.OnlineID ?? -1}", LoggingTarget.Network);
                        scores.Value = LeaderboardScores.Failure(LeaderboardFailState.BeatmapUnavailable);
                        return;
                    }

                    if ((newCriteria.Scope.RequiresSupporter(newCriteria.ExactMods != null)) && !api.LocalUser.Value.IsSupporter)
                    {
                        scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NotSupporter);
                        return;
                    }

                    if (newCriteria.Scope == BeatmapLeaderboardScope.Team && api.LocalUser.Value.Team == null)
                    {
                        scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NoTeam);
                        return;
                    }

                    IReadOnlyList<Mod>? requestMods = null;

                    if (newCriteria.ExactMods != null)
                    {
                        if (!newCriteria.ExactMods.Any())
                            // add nomod for the request
                            requestMods = new Mod[] { new ModNoMod() };
                        else
                            requestMods = newCriteria.ExactMods;
                    }

                    void queueScoresRequest(int beatmapOnlineId)
                    {
                        var newRequest = new GetScoresRequest(newCriteria.Beatmap, newCriteria.Ruleset, newCriteria.Scope, requestMods, newCriteria.Sorting, beatmapOnlineId);
                        newRequest.Success += response =>
                        {
                            if (!ReferenceEquals(newRequest, inFlightOnlineRequest))
                                return;

                            var result = LeaderboardScores.Success
                            (
                                response.Scores.Select(s => s.ToScoreInfo(rulesets, newCriteria.Beatmap))
                                        .Select((s, idx) =>
                                        {
                                            s.Position = idx + 1;
                                            return s;
                                        })
                                        .ToArray(),
                                scoresRequested: newRequest.ScoresRequested,
                                totalScores: response.ScoresCount,
                                response.UserScore?.CreateScoreInfo(rulesets, newCriteria.Beatmap)
                            );
                            inFlightOnlineRequest = null;
                            scores.Value = result;
                        };
                        newRequest.Failure += ex =>
                        {
                            if (!ReferenceEquals(newRequest, inFlightOnlineRequest))
                                return;

                            inFlightOnlineRequest = null;
                            Logger.Log($@"Failed to fetch leaderboards when displaying results: {ex}", LoggingTarget.Network);
                            if (ex is not OperationCanceledException)
                                scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NetworkFailure);
                        };

                        api.Queue(inFlightOnlineRequest = newRequest);
                    }

                    if (requiresChecksumLookup(newCriteria.Beatmap))
                    {
                        var lookupRequest = new GetBeatmapRequest(md5Hash: newCriteria.Beatmap.MD5Hash);
                        lookupRequest.Success += resolvedBeatmap =>
                        {
                            if (!ReferenceEquals(lookupRequest, inFlightBeatmapLookupRequest))
                                return;

                            inFlightBeatmapLookupRequest = null;

                            if (resolvedBeatmap.OnlineID <= 0
                                || !string.Equals(resolvedBeatmap.MD5Hash, newCriteria.Beatmap.MD5Hash, StringComparison.OrdinalIgnoreCase))
                            {
                                Logger.Log(
                                    $@"Refusing to fetch leaderboard for server-exclusive beatmap {newCriteria.Beatmap}: checksum lookup returned id={resolvedBeatmap.OnlineID}, checksum={resolvedBeatmap.MD5Hash}, expected checksum={newCriteria.Beatmap.MD5Hash}.",
                                    LoggingTarget.Network);
                                scores.Value = LeaderboardScores.Failure(LeaderboardFailState.BeatmapUnavailable);
                                return;
                            }

                            Logger.Log(
                                $@"Resolved server-exclusive leaderboard beatmap by checksum: local id={newCriteria.Beatmap.OnlineID}, checksum={newCriteria.Beatmap.MD5Hash}, server id={resolvedBeatmap.OnlineID}.",
                                LoggingTarget.Network);
                            queueScoresRequest(resolvedBeatmap.OnlineID);
                        };
                        lookupRequest.Failure += ex =>
                        {
                            if (!ReferenceEquals(lookupRequest, inFlightBeatmapLookupRequest))
                                return;

                            inFlightBeatmapLookupRequest = null;
                            Logger.Log($@"Failed to resolve server-exclusive leaderboard beatmap by checksum: {ex}", LoggingTarget.Network);
                            if (ex is not OperationCanceledException)
                                scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NetworkFailure);
                        };

                        api.Queue(inFlightBeatmapLookupRequest = lookupRequest);
                    }
                    else
                        queueScoresRequest(newCriteria.Beatmap.OnlineID);

                    break;
                }
            }
        }

        private static bool requiresChecksumLookup(IBeatmapInfo beatmap)
        {
            const int server_exclusive_id_threshold = 2_000_000_000;

            return !string.IsNullOrEmpty(beatmap.MD5Hash)
                   && (beatmap.OnlineID >= server_exclusive_id_threshold
                       || (beatmap.BeatmapSet?.OnlineID ?? -1) >= server_exclusive_id_threshold
                       || beatmap.Metadata.IsServerExclusive()
                       || beatmap.BeatmapSet.IsServerExclusive());
        }

        private void localScoresChanged(IRealmCollection<ScoreInfo> sender, ChangeSet? changes)
        {
            Debug.Assert(CurrentCriteria != null);

            // This subscription may fire from changes to linked beatmaps, which we don't care about.
            // It's currently not possible for a score to be modified after insertion, so we can safely ignore callbacks with only modifications.
            if (changes?.HasCollectionChanges() == false)
                return;

            var newScores = sender.AsEnumerable();

            if (CurrentCriteria.ExactMods != null)
            {
                if (!CurrentCriteria.ExactMods.Any())
                {
                    // we need to filter out all scores that have any mods to get all local nomod scores
                    newScores = newScores.Where(s => !s.Mods.Any());
                }
                else
                {
                    // otherwise find all the scores that have all of the currently selected mods (similar to how web applies mod filters)
                    // we're creating and using a string HashSet representation of selected mods so that it can be translated into the DB query itself
                    var selectedMods = CurrentCriteria.ExactMods.Select(m => m.Acronym).ToHashSet();

                    newScores = newScores.Where(s => selectedMods.SetEquals(s.Mods.Select(m => m.Acronym)));
                }
            }

            newScores = newScores.Detach().OrderByCriteria(CurrentCriteria.Sorting);

            var newScoresArray = newScores.ToArray();
            scores.Value = LeaderboardScores.Success(newScoresArray, scoresRequested: newScoresArray.Length, totalScores: newScoresArray.Length, null);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            localScoreSubscription?.Dispose();
            inFlightOnlineRequest?.Cancel();
            inFlightBeatmapLookupRequest?.Cancel();
        }
    }

    public record LeaderboardCriteria(
        BeatmapInfo? Beatmap,
        RulesetInfo? Ruleset,
        BeatmapLeaderboardScope Scope,
        Mod[]? ExactMods,
        LeaderboardSortMode Sorting = LeaderboardSortMode.Score
    );

    public record LeaderboardScores
    {
        /// <summary>
        /// The collection of all scores received through the leaderboard lookup.
        /// </summary>
        public ICollection<ScoreInfo> TopScores { get; }

        /// <summary>
        /// The number of scores which was requested.
        /// Used to determine whether the returned leaderboard can be judged to be a partial or full leaderboard
        /// (i.e. whether <see cref="TopScores"/> contains all scores that it could ever contain).
        /// </summary>
        public int ScoresRequested { get; }

        /// <summary>
        /// The number of all scores that exist on the leaderboard.
        /// </summary>
        public int TotalScores { get; }

        public bool IsPartial => ScoresRequested < TotalScores;

        /// <summary>
        /// The local user's best score.
        /// </summary>
        public ScoreInfo? UserScore { get; }

        /// <summary>
        /// The failure state that occurred when attempting to retrieve the leaderboard.
        /// </summary>
        public LeaderboardFailState? FailState { get; }

        public IEnumerable<ScoreInfo> AllScores
        {
            get
            {
                foreach (var score in TopScores)
                    yield return score;

                if (UserScore != null && TopScores.All(topScore => !topScore.Equals(UserScore) && !topScore.MatchesOnlineID(UserScore)))
                    yield return UserScore;
            }
        }

        private LeaderboardScores(ICollection<ScoreInfo> topScores, int scoresRequested, int totalScores, ScoreInfo? userScore, LeaderboardFailState? failState)
        {
            TopScores = topScores;
            ScoresRequested = scoresRequested;
            TotalScores = totalScores;
            UserScore = userScore;
            FailState = failState;
        }

        public static LeaderboardScores Success(ICollection<ScoreInfo> topScores, int scoresRequested, int totalScores, ScoreInfo? userScore)
            => new LeaderboardScores(topScores, scoresRequested, totalScores, userScore, null);

        public static LeaderboardScores Failure(LeaderboardFailState failState)
            => new LeaderboardScores([], scoresRequested: 0, totalScores: 0, null, failState);
    }

    public enum LeaderboardFailState
    {
        NetworkFailure = -1,
        BeatmapUnavailable = -2,
        RulesetUnavailable = -3,
        NoneSelected = -4,
        NotLoggedIn = -5,
        NotSupporter = -6,
        NoTeam = -7
    }
}
