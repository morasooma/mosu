// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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
using osu.Game.Online.Legacy;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Rulesets.Scoring;
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
        private CancellationTokenSource? stableLeaderboardCancellation;

        [Resolved(CanBeNull = true)]
        private StableScoreSubmissionClient? stableClient { get; set; }

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
            stableLeaderboardCancellation?.Cancel();
            stableLeaderboardCancellation?.Dispose();
            stableLeaderboardCancellation = null;
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
                    if (MosuServerEnvironment.UsesStableProtocol)
                    {
                        fetchStableLeaderboard(newCriteria);
                        return;
                    }

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
                                scores.Value = LeaderboardScores.Failure(GetFailureState(ex));
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
                                scores.Value = LeaderboardScores.Failure(GetFailureState(ex));
                        };

                        api.Queue(inFlightBeatmapLookupRequest = lookupRequest);
                    }
                    else
                        queueScoresRequest(newCriteria.Beatmap.OnlineID);

                    break;
                }
            }
        }

        internal static LeaderboardFailState GetFailureState(Exception exception) =>
            exception is APIException { StatusCode: HttpStatusCode.NotFound }
                ? LeaderboardFailState.BeatmapUnavailable
                : LeaderboardFailState.NetworkFailure;

        private void fetchStableLeaderboard(LeaderboardCriteria criteria)
        {
            if (stableClient == null || !stableClient.IsConfigured(out _))
            {
                scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NotLoggedIn);
                return;
            }

            // Special rulesets are addressed by a separate mode in the Mosu API, while the
            // stable protocol uses the base ruleset plus the RX/AP legacy mod bit. Keep the
            // bit separate from ExactMods: with no explicit mod filter, v must remain the
            // normal leaderboard type so stable servers can return every RX/AP score.
            (RulesetInfo stableRuleset, LegacyMods specialModeMod) = NormaliseStableRuleset(criteria.Ruleset!);
            criteria = criteria with { Ruleset = stableRuleset };

            if (criteria.Ruleset.OnlineID != 0)
            {
                scores.Value = LeaderboardScores.Failure(LeaderboardFailState.RulesetUnavailable);
                return;
            }

            if (criteria.Scope == BeatmapLeaderboardScope.Team)
            {
                scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NoTeam);
                return;
            }

            int leaderboardType = criteria.ExactMods != null
                ? 2
                : criteria.Scope switch
                {
                    BeatmapLeaderboardScope.Country => 4,
                    BeatmapLeaderboardScope.Friend => 3,
                    _ => 1,
                };

            var rulesetInstance = criteria.Ruleset.CreateInstance();
            LegacyMods legacyMods = criteria.ExactMods == null ? LegacyMods.None : rulesetInstance.ConvertToLegacyMods(criteria.ExactMods);
            legacyMods |= specialModeMod;
            var cancellation = stableLeaderboardCancellation = new CancellationTokenSource();

            Task.Run(async () =>
            {
                try
                {
                    StableLeaderboardResult response = await stableClient.FetchLeaderboardAsync(criteria.Beatmap!, criteria.Ruleset.OnlineID, (int)legacyMods,
                        leaderboardType, cancellation.Token).ConfigureAwait(false);

                    if (!response.BeatmapAvailable)
                    {
                        Schedule(() => scores.Value = LeaderboardScores.Failure(LeaderboardFailState.BeatmapUnavailable));
                        return;
                    }

                    ScoreInfo[] topScores = response.Scores.Select(score => createStableScoreInfo(score, criteria.Beatmap!, criteria.Ruleset)).ToArray();
                    ScoreInfo? personalBest = response.PersonalBest == null
                        ? null
                        : createStableScoreInfo(response.PersonalBest, criteria.Beatmap!, criteria.Ruleset);

                    Schedule(() =>
                    {
                        if (ReferenceEquals(cancellation, stableLeaderboardCancellation))
                        {
                            int totalScores = Math.Max(response.ScoreCount, personalBest?.Position ?? 0);
                            scores.Value = LeaderboardScores.Success(topScores, response.Scores.Count, totalScores, personalBest);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, "Failed to fetch stable leaderboard.");
                    Schedule(() =>
                    {
                        if (ReferenceEquals(cancellation, stableLeaderboardCancellation))
                            scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NetworkFailure);
                    });
                }
            });
        }

        internal static (RulesetInfo Ruleset, LegacyMods SpecialModeMod) NormaliseStableRuleset(RulesetInfo ruleset)
        {
            if (!ruleset.IsSpecialRuleset())
                return (ruleset, LegacyMods.None);

            LegacyMods specialModeMod = ruleset.OnlineID switch
            {
                RulesetInfo.OSU_RELAX_ONLINE_ID or RulesetInfo.TAIKO_RELAX_ONLINE_ID or RulesetInfo.CATCH_RELAX_ONLINE_ID => LegacyMods.Relax,
                RulesetInfo.OSU_AUTOPILOT_ONLINE_ID => LegacyMods.Autopilot,
                _ => LegacyMods.None,
            };

            return (ruleset.CreateNormalRuleset(), specialModeMod);
        }

        private ScoreInfo createStableScoreInfo(StableLeaderboardScore source, BeatmapInfo beatmap, RulesetInfo ruleset)
        {
            var rulesetInstance = ruleset.CreateInstance();
            var mods = rulesetInstance.ConvertFromLegacyMods((LegacyMods)source.Mods).ToList();
            Mod? classic = rulesetInstance.CreateModFromAcronym("CL");
            if (classic != null)
                mods.Add(classic);

            var score = new ScoreInfo
            {
                // Stable score identifiers belong to the legacy namespace. Setting OnlineID
                // makes lazer try /api/v2 score and replay routes which stable servers do not expose.
                OnlineID = -1,
                LegacyOnlineID = source.Id,
                User = new APIUser
                {
                    Id = source.UserId,
                    Username = source.Username,
                    AvatarUrl = stableClient?.GetAvatarUrl(source.UserId) ?? string.Empty,
                },
                BeatmapInfo = beatmap,
                BeatmapHash = beatmap.Hash,
                Ruleset = ruleset,
                TotalScore = source.TotalScore,
                TotalScoreWithoutMods = source.TotalScore,
                LegacyTotalScore = source.TotalScore,
                MaxCombo = source.MaxCombo,
                Date = source.Date,
                // Legacy replay download uses /web/osu-getreplay.php and is not implemented yet.
                HasOnlineReplay = false,
                Mods = mods.ToArray(),
                Position = source.Position,
                Passed = true,
            };

            score.SetCount50(source.Count50);
            score.SetCount100(source.Count100);
            score.SetCount300(source.Count300);
            score.SetCountMiss(source.CountMiss);
            score.SetCountKatu(source.CountKatu);
            score.SetCountGeki(source.CountGeki);
            score.Accuracy = calculateAccuracy(score);
            score.Rank = calculateRank(score);
            return score;
        }

        private static double calculateAccuracy(ScoreInfo score)
        {
            int count300 = score.GetCount300() ?? 0;
            int count100 = score.GetCount100() ?? 0;
            int count50 = score.GetCount50() ?? 0;
            int countMiss = score.GetCountMiss() ?? 0;
            int totalHits = count300 + count100 + count50 + countMiss;

            return totalHits == 0 ? 0 : (count300 * 300d + count100 * 100d + count50 * 50d) / (totalHits * 300d);
        }

        private static ScoreRank calculateRank(ScoreInfo score)
        {
            if (!score.Passed)
                return ScoreRank.F;

            int count300 = score.GetCount300() ?? 0;
            int count50 = score.GetCount50() ?? 0;
            int countMiss = score.GetCountMiss() ?? 0;
            int totalHits = count300 + (score.GetCount100() ?? 0) + count50 + countMiss;

            if (totalHits == 0)
                return ScoreRank.D;

            double ratio300 = count300 / (double)totalHits;
            double ratio50 = count50 / (double)totalHits;
            ScoreRank rank;

            if (count300 == totalHits)
                rank = ScoreRank.X;
            else if (ratio300 > 0.9 && ratio50 <= 0.01 && countMiss == 0)
                rank = ScoreRank.S;
            else if ((ratio300 > 0.8 && countMiss == 0) || ratio300 > 0.9)
                rank = ScoreRank.A;
            else if ((ratio300 > 0.7 && countMiss == 0) || ratio300 > 0.8)
                rank = ScoreRank.B;
            else if (ratio300 > 0.6)
                rank = ScoreRank.C;
            else
                rank = ScoreRank.D;

            bool silver = score.Mods.Any(mod => mod is Rulesets.Mods.ModHidden or Rulesets.Mods.ModFlashlight);
            if (silver && rank == ScoreRank.X)
                return ScoreRank.XH;
            if (silver && rank == ScoreRank.S)
                return ScoreRank.SH;

            return rank;
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
            stableLeaderboardCancellation?.Cancel();
            stableLeaderboardCancellation?.Dispose();
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
