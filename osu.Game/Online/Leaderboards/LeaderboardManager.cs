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
using osu.Framework.Extensions;
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
using osu.Game.Performance;
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

        /// <summary>Exact local revision resolved by the Mosu server when opening its leaderboard.</summary>
        public IBindable<Guid?> MosuResolvedBeatmap => mosuResolvedBeatmap;
        private readonly Bindable<Guid?> mosuResolvedBeatmap = new Bindable<Guid?>();
        private readonly HashSet<Guid> mosuResolvedBeatmaps = new HashSet<Guid>();

        public bool IsMosuResolved(Guid beatmapId) => mosuResolvedBeatmaps.Contains(beatmapId);

        public LeaderboardCriteria? CurrentCriteria { get; private set; }

        private IDisposable? localScoreSubscription;
        private int localRevision;
        private LocalScoreQuery? localScoreQuery;
        private bool localSnapshotDirty;
        private bool localSnapshotInFlight;
        private bool localSnapshotScheduled;
        private GetScoresRequest? inFlightOnlineRequest;
        private GetBeatmapRequest? inFlightBeatmapLookupRequest;
        private CancellationTokenSource? stableLeaderboardCancellation;
        private static readonly AsyncOperationQueue failureLogging = new AsyncOperationQueue();

        // Exception formatting resolves stack symbols and can read PDBs. Preserve
        // the complete diagnostic, but never do that work in the API Update callback.
        internal static Task LogFailureAsync(string message, Exception exception) => failureLogging.Enqueue(() =>
        {
            Logger.GetLogger(LoggingTarget.Network).Add(message, exception: exception);
            return Task.CompletedTask;
        });

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

            // Consumers also inspect CurrentCriteria after Realm recycling. Do not
            // retain Update-owned objects whose Realm can close during a query.
            if (newCriteria.Beatmap?.IsManaged == true || newCriteria.Ruleset?.IsManaged == true)
                newCriteria = newCriteria with { Beatmap = newCriteria.Beatmap?.Detach(), Ruleset = newCriteria.Ruleset?.Detach() };

            CurrentCriteria = newCriteria;
            localRevision++;
            localSnapshotDirty = false;
            localScoreQuery = null;
            localScoreSubscription?.Dispose();
            localScoreSubscription = null;
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
                    // Registering a linked score query can synchronously evaluate it in
                    // Realm's native notification machinery. Keep only a cheap transaction
                    // listener on Update; materialise/detach/sort scores on a worker.
                    localScoreQuery = LocalScoreQuery.FromCriteria(newCriteria);
                    localScoreSubscription = realm.RegisterCustomSubscription(r =>
                    {
                        r.RealmChanged += localRealmChanged;
                        invalidateLocalSnapshot(); // also refresh after Realm recycle.
                        return new InvokeOnDisposal(() => r.RealmChanged -= localRealmChanged);
                    });
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
                            _ = LogFailureAsync("Failed to fetch leaderboards when displaying results", ex);
                            if (ex is not OperationCanceledException)
                                scores.Value = LeaderboardScores.Failure(GetFailureState(ex));
                        };

                        api.Queue(inFlightOnlineRequest = newRequest);
                    }

                    if (requiresChecksumLookup(newCriteria.Beatmap))
                    {
                        // This is a Mosu score lookup, not an official catalogue lookup.
                        // Ask only when the user opens this leaderboard; never scan the library.
                        var lookupRequest = new GetBeatmapRequest(md5Hash: newCriteria.Beatmap.MD5Hash, serverExclusive: true);
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
                                $@"Resolved Mosu leaderboard beatmap by checksum: local id={newCriteria.Beatmap.OnlineID}, checksum={newCriteria.Beatmap.MD5Hash}, server id={resolvedBeatmap.OnlineID}.",
                                LoggingTarget.Network);
                            if (newCriteria.Beatmap is BeatmapInfo localBeatmap && localBeatmap.Status == BeatmapOnlineStatus.None)
                            {
                                mosuResolvedBeatmaps.Add(localBeatmap.ID);
                                mosuResolvedBeatmap.Value = localBeatmap.ID;
                            }
                            queueScoresRequest(resolvedBeatmap.OnlineID);
                        };
                        lookupRequest.Failure += ex =>
                        {
                            if (!ReferenceEquals(lookupRequest, inFlightBeatmapLookupRequest))
                                return;

                            inFlightBeatmapLookupRequest = null;
                            _ = LogFailureAsync("Failed to resolve server-exclusive leaderboard beatmap by checksum", ex);
                            if (ex is not OperationCanceledException)
                                scores.Value = LeaderboardScores.Failure(GetFailureState(ex));
                        };

                        api.Queue(inFlightBeatmapLookupRequest = lookupRequest);
                    }
                    else if (newCriteria.Beatmap.OnlineID > 0)
                        queueScoresRequest(newCriteria.Beatmap.OnlineID);
                    else
                        scores.Value = LeaderboardScores.Failure(LeaderboardFailState.BeatmapUnavailable);

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

            if (criteria.Ruleset.OnlineID is < 0 or > 3)
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
                        Schedule(() =>
                        {
                            if (ReferenceEquals(cancellation, stableLeaderboardCancellation))
                                scores.Value = LeaderboardScores.Failure(LeaderboardFailState.BeatmapUnavailable);
                        });
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
            score.Accuracy = StableScoreStatistics.CalculateAccuracy(score);
            score.Rank = StableScoreStatistics.CalculateRank(score);
            return score;
        }

        internal static bool requiresChecksumLookup(IBeatmapInfo beatmap)
        {
            const int server_exclusive_id_threshold = 2_000_000_000;

            return !string.IsNullOrEmpty(beatmap.MD5Hash)
                   && (beatmap.OnlineID <= 0
                       || beatmap.OnlineID >= server_exclusive_id_threshold
                       || (beatmap.BeatmapSet?.OnlineID ?? -1) >= server_exclusive_id_threshold
                       || beatmap.Metadata.IsServerExclusive()
                       || beatmap.BeatmapSet.IsServerExclusive());
        }

        private void localRealmChanged(object? sender, EventArgs args) => invalidateLocalSnapshot();

        private void invalidateLocalSnapshot()
        {
            localRevision++;
            localSnapshotDirty = true;
            queueLocalSnapshot();
        }

        private void queueLocalSnapshot()
        {
            if (localSnapshotScheduled || IsDisposed)
                return;
            localSnapshotScheduled = true;
            realm.PostToUpdateThread(() =>
            {
                localSnapshotScheduled = false;
                fetchLocalSnapshot();
            });
        }

        private void fetchLocalSnapshot()
        {
            if (IsDisposed || localSnapshotInFlight || !localSnapshotDirty || CurrentCriteria?.Scope != BeatmapLeaderboardScope.Local)
                return;

            var criteria = CurrentCriteria;
            int revision = localRevision;
            localSnapshotDirty = false;
            localSnapshotInFlight = true;
            // Criteria may hold live Realm models owned by Update. Only scalar
            // query values and copied mod acronyms may cross into the worker.
            var query = localScoreQuery!;
            var task = realm.RunAsync(r => readLocalScores(r, query));
            _ = task.ContinueWith(completed =>
            {
                // Observe faults even if this component has been disposed before publication.
                var error = completed.Exception;
                realm.PostToUpdateThread(() =>
                {
                    localSnapshotInFlight = false;
                    if (IsDisposed)
                        return;

                    if (revision == localRevision && ReferenceEquals(criteria, CurrentCriteria))
                    {
                        if (completed.IsCompletedSuccessfully)
                        {
                            var result = completed.GetResultSafely();
                            scores.Value = LeaderboardScores.Success(result, result.Length, result.Length, null);
                        }
                        else if (error != null)
                        {
                            _ = LogFailureAsync("Failed to read local leaderboard snapshot", error);
                            scores.Value = LeaderboardScores.Failure(LeaderboardFailState.NetworkFailure);
                        }
                    }
                    if (localSnapshotDirty)
                        queueLocalSnapshot();
                });
            }, TaskScheduler.Default);
        }

        internal record LocalScoreQuery(Guid BeatmapId, string MD5Hash, string RulesetName, string[]? ExactMods, LeaderboardSortMode Sorting)
        {
            internal static LocalScoreQuery FromCriteria(LeaderboardCriteria criteria)
            {
                // RX/AP names only address online API modes, not local scores.
                var ruleset = criteria.Ruleset!.IsSpecialRuleset() ? criteria.Ruleset.CreateNormalRuleset() : criteria.Ruleset;
                return new LocalScoreQuery(criteria.Beatmap!.ID, criteria.Beatmap.MD5Hash, ruleset.ShortName,
                    criteria.ExactMods?.Select(m => m.Acronym).ToArray(), criteria.Sorting);
            }
        }

        internal static ScoreInfo[] readLocalScores(Realm r, LocalScoreQuery query)
        {
            var allScores = r.All<ScoreInfo>();
            string commonFilter = $" AND {nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.Hash)} == {nameof(ScoreInfo.BeatmapHash)}"
                                  + $" AND {nameof(ScoreInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $1"
                                  + $" AND {nameof(ScoreInfo.DeletePending)} == false";
            IEnumerable<ScoreInfo> newScores = string.IsNullOrEmpty(query.MD5Hash)
                ? allScores.Filter($"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.ID)} == $0" + commonFilter, query.BeatmapId, query.RulesetName)
                : allScores.Filter($"({nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.ID)} == $0"
                                   + $" OR {nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.MD5Hash)} == $2)" + commonFilter,
                    query.BeatmapId, query.RulesetName, query.MD5Hash);

            if (query.ExactMods != null)
            {
                if (!query.ExactMods.Any())
                    newScores = newScores.Where(s => !s.Mods.Any());
                else
                {
                    var selectedMods = query.ExactMods.ToHashSet();
                    newScores = newScores.Where(s => selectedMods.SetEquals(s.Mods.Select(m => m.Acronym)));
                }
            }
            return newScores.Detach().OrderByCriteria(query.Sorting).ToArray();
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
