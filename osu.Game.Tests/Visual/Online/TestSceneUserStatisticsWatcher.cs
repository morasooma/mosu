// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Models;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Online
{
    [HeadlessTest]
    public partial class TestSceneUserStatisticsWatcher : OsuTestScene
    {
        protected override bool UseOnlineAPI => false;

        private LocalUserStatisticsProvider statisticsProvider = null!;
        private UserStatisticsWatcher watcher = null!;

        [Resolved]
        private SpectatorClient spectatorClient { get; set; } = null!;

        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        private Action<GetUsersRequest>? handleGetUsersRequest;
        private Action<GetUserRequest>? handleGetUserRequest;
        private Action<GetScoreRequest>? handleGetScoreRequest;

        private readonly Dictionary<(int userId, string rulesetName), UserStatistics> serverSideStatistics = new Dictionary<(int userId, string rulesetName), UserStatistics>();

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("clear server-side stats", () => serverSideStatistics.Clear());
            AddStep("set up request handling", () =>
            {
                handleGetUserRequest = null;
                handleGetUsersRequest = null;
                handleGetScoreRequest = null;

                dummyAPI.HandleRequest = request =>
                {
                    switch (request)
                    {
                        case GetScoreRequest getScoreRequest:
                            if (handleGetScoreRequest != null)
                                handleGetScoreRequest(getScoreRequest);
                            else
                                getScoreRequest.TriggerSuccess(new SoloScoreInfo { ID = (ulong)getScoreRequest.ScoreId, UserID = API.LocalUser.Value.Id });
                            return true;

                        case GetUsersRequest getUsersRequest:
                            if (handleGetUsersRequest != null)
                            {
                                handleGetUsersRequest?.Invoke(getUsersRequest);
                            }
                            else
                            {
                                int userId = getUsersRequest.UserIds.Single();
                                var response = new GetUsersResponse
                                {
                                    Users = new List<APIUser>
                                    {
                                        new APIUser
                                        {
                                            Id = userId,
                                            RulesetsStatistics = new Dictionary<string, UserStatistics>
                                            {
                                                ["osu"] = tryGetStatistics(userId, "osu"),
                                                ["taiko"] = tryGetStatistics(userId, "taiko"),
                                                ["fruits"] = tryGetStatistics(userId, "fruits"),
                                                ["mania"] = tryGetStatistics(userId, "mania"),
                                            }
                                        }
                                    }
                                };
                                getUsersRequest.TriggerSuccess(response);
                            }

                            return true;

                        case GetUserRequest getUserRequest:
                            if (handleGetUserRequest != null)
                            {
                                handleGetUserRequest.Invoke(getUserRequest);
                            }
                            else
                            {
                                int userId = int.Parse(getUserRequest.Lookup);
                                string rulesetName = getUserRequest.Ruleset!.ShortName;
                                var response = new APIUser
                                {
                                    Id = userId,
                                    Statistics = tryGetStatistics(userId, rulesetName)
                                };
                                getUserRequest.TriggerSuccess(response);
                            }

                            return true;

                        default:
                            return false;
                    }
                };
            });

            AddStep("create watcher", () =>
            {
                Clear();
                Add(statisticsProvider = new LocalUserStatisticsProvider());
                Add(watcher = new UserStatisticsWatcher(statisticsProvider));
            });
        }

        private UserStatistics tryGetStatistics(int userId, string rulesetName)
            => serverSideStatistics.TryGetValue((userId, rulesetName), out var stats) ? stats : new UserStatistics();

        [Test]
        public void TestStatisticsUpdateFiredAfterRegistrationAddedAndScoreProcessed()
        {
            int userId = getUserId();
            long scoreId = getScoreId();
            setUpUser(userId);

            var ruleset = new OsuRuleset().RulesetInfo;

            ScoreBasedUserStatisticsUpdate? update = null;
            registerForUpdates(scoreId, ruleset, receivedUpdate => update = receivedUpdate);

            feignScoreProcessing(userId, ruleset, 5_000_000);

            AddStep("signal score processed", () => ((ISpectatorClient)spectatorClient).UserScoreProcessed(userId, scoreId));
            AddUntilStep("update received", () => update != null);
            AddAssert("values before are correct", () => update!.Before.TotalScore, () => Is.EqualTo(4_000_000));
            AddAssert("values after are correct", () => update!.After.TotalScore, () => Is.EqualTo(5_000_000));
        }

        [Test]
        public void TestStatisticsUpdateFiredAfterScoreProcessedAndRegistrationAdded()
        {
            int userId = getUserId();
            setUpUser(userId);

            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;

            // Note ordering: both processing and its notification complete before registration is added.
            feignScoreProcessing(userId, ruleset, 5_000_000);
            AddStep("signal score processed", () => ((ISpectatorClient)spectatorClient).UserScoreProcessed(userId, scoreId));
            AddWaitStep("allow early notification delivery", 1);

            ScoreBasedUserStatisticsUpdate? update = null;
            registerForUpdates(scoreId, ruleset, receivedUpdate => update = receivedUpdate);

            AddUntilStep("update received", () => update != null);
            AddAssert("values before are correct", () => update!.Before.TotalScore, () => Is.EqualTo(4_000_000));
            AddAssert("values after are correct", () => update!.After.TotalScore, () => Is.EqualTo(5_000_000));
        }

        [Test]
        public void TestStatisticsUpdateNotFiredIfUserLoggedOut()
        {
            int userId = getUserId();
            setUpUser(userId);

            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;

            ScoreBasedUserStatisticsUpdate? update = null;
            registerForUpdates(scoreId, ruleset, receivedUpdate => update = receivedUpdate);

            feignScoreProcessing(userId, ruleset, 5_000_000);

            AddStep("log out user", () => dummyAPI.Logout());

            AddStep("signal score processed", () => ((ISpectatorClient)spectatorClient).UserScoreProcessed(userId, scoreId));
            AddWaitStep("wait a bit", 5);
            AddAssert("update not received", () => update == null);

            AddStep("log in user", () =>
            {
                dummyAPI.Login("user", "password");
                dummyAPI.AuthenticateSecondFactor("abcdefgh");
            });
        }

        [Test]
        public void TestStatisticsUpdateNotFiredIfAnotherUserLoggedIn()
        {
            int userId = getUserId();
            setUpUser(userId);

            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;

            ScoreBasedUserStatisticsUpdate? update = null;
            registerForUpdates(scoreId, ruleset, receivedUpdate => update = receivedUpdate);

            feignScoreProcessing(userId, ruleset, 5_000_000);

            AddStep("change user", () => dummyAPI.LocalUser.Value = new APIUser { Id = getUserId() });

            AddStep("signal score processed", () => ((ISpectatorClient)spectatorClient).UserScoreProcessed(userId, scoreId));
            AddWaitStep("wait a bit", 5);
            AddAssert("update not received", () => update == null);
        }

        [Test]
        public void TestStatisticsUpdateNotFiredIfScoreIdDoesNotMatch()
        {
            int userId = getUserId();
            setUpUser(userId);

            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;

            ScoreBasedUserStatisticsUpdate? update = null;
            registerForUpdates(scoreId, ruleset, receivedUpdate => update = receivedUpdate);

            feignScoreProcessing(userId, ruleset, 5_000_000);

            AddStep("signal another score processed", () => ((ISpectatorClient)spectatorClient).UserScoreProcessed(userId, getScoreId()));
            AddWaitStep("wait a bit", 5);
            AddAssert("update not received", () => update == null);
        }

        // the behaviour exercised in this test may not be final, it is mostly assumed for simplicity.
        // in the long run we may want each score's update to be entirely isolated from others, rather than have prior unobserved updates merge into the latest.
        [Test]
        public void TestIgnoredScoreUpdateIsMergedIntoNextOne()
        {
            int userId = getUserId();
            setUpUser(userId);

            long firstScoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;

            feignScoreProcessing(userId, ruleset, 5_000_000);

            AddStep("signal score processed", () => ((ISpectatorClient)spectatorClient).UserScoreProcessed(userId, firstScoreId));

            long secondScoreId = getScoreId();

            feignScoreProcessing(userId, ruleset, 6_000_000);

            ScoreBasedUserStatisticsUpdate? update = null;
            registerForUpdates(secondScoreId, ruleset, receivedUpdate => update = receivedUpdate);

            AddStep("signal score processed", () => ((ISpectatorClient)spectatorClient).UserScoreProcessed(userId, secondScoreId));
            AddUntilStep("update received", () => update != null);
            AddAssert("values before are correct", () => update!.Before.TotalScore, () => Is.EqualTo(4_000_000));
            AddAssert("values after are correct", () => update!.After.TotalScore, () => Is.EqualTo(6_000_000));
        }

        [Test]
        public void TestGlobalStatisticsUpdatedAfterRegistrationAddedAndScoreProcessed()
        {
            int userId = getUserId();
            long scoreId = getScoreId();
            setUpUser(userId);

            var ruleset = new OsuRuleset().RulesetInfo;

            ScoreBasedUserStatisticsUpdate? update = null;
            registerForUpdates(scoreId, ruleset, receivedUpdate => update = receivedUpdate);

            feignScoreProcessing(userId, ruleset, 5_000_000);

            AddStep("signal score processed", () => ((ISpectatorClient)spectatorClient).UserScoreProcessed(userId, scoreId));
            AddUntilStep("update received", () => update != null);
            AddAssert("statistics values are correct", () => statisticsProvider.GetStatisticsFor(ruleset)!.TotalScore, () => Is.EqualTo(5_000_000));
        }

        [Test]
        public void TestLostPushRefreshesOnlyAfterDurableProcessing()
        {
            int userId = getUserId();
            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;
            setUpUser(userId);
            bool processed = false;
            int polls = 0;
            ScoreBasedUserStatisticsUpdate? update = null;
            AddStep("handle score readiness", () => handleGetScoreRequest = request =>
            {
                polls++;
                request.TriggerSuccess(new SoloScoreInfo { ID = (ulong)scoreId, UserID = userId, Processed = processed });
            });
            registerForUpdates(scoreId, ruleset, value => update = value);
            AddUntilStep("unprocessed score polled", () => polls > 0);
            AddAssert("no stale statistics published", () => update == null);
            feignScoreProcessing(userId, ruleset, 5_000_000);
            AddStep("commit score processing", () => processed = true);
            AddUntilStep("fallback update received", () => update != null);
            AddAssert("correct delta retained", () => update!.Before.TotalScore == 4_000_000 && update.After.TotalScore == 5_000_000);
        }

        [Test]
        public void TestReadinessFailureRetriesAndDuplicateRegistrationIsSafe()
        {
            int userId = getUserId();
            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;
            setUpUser(userId);
            int polls = 0;
            ScoreBasedUserStatisticsUpdate? update = null;
            AddStep("fail first readiness request", () => handleGetScoreRequest = request =>
            {
                if (++polls == 1)
                    request.TriggerFailure(new InvalidOperationException("temporary outage"));
                else
                    request.TriggerSuccess(new SoloScoreInfo { ID = (ulong)scoreId, UserID = userId, Processed = true });
            });
            registerForUpdates(scoreId, ruleset, value => update = value);
            registerForUpdates(scoreId, ruleset, _ => { });
            feignScoreProcessing(userId, ruleset, 5_000_000);
            AddUntilStep("retried and refreshed", () => update != null);
            AddAssert("only one retry chain", () => polls, () => Is.EqualTo(2));
        }

        [Test]
        public void TestStatisticsFetchFailureDoesNotPublishCachedValues()
        {
            int userId = getUserId();
            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;
            setUpUser(userId);
            int requests = 0;
            ScoreBasedUserStatisticsUpdate? update = null;
            AddStep("ready score and failing statistics", () =>
            {
                handleGetScoreRequest = request => request.TriggerSuccess(new SoloScoreInfo { ID = (ulong)scoreId, UserID = userId, Processed = true });
                handleGetUserRequest = request =>
                {
                    requests++;
                    request.TriggerFailure(new InvalidOperationException("temporary stats outage"));
                };
            });
            registerForUpdates(scoreId, ruleset, value => update = value);
            AddUntilStep("statistics fetch failed", () => requests > 0);
            AddAssert("cached values not published", () => update == null);
            feignScoreProcessing(userId, ruleset, 5_000_000);
            AddStep("restore statistics endpoint", () => handleGetUserRequest = null);
            AddUntilStep("statistics retry succeeded", () => update != null);
            AddAssert("new statistics", () => update!.After.TotalScore, () => Is.EqualTo(5_000_000));
        }

        [Test]
        public void TestReadinessResponseAfterAccountChangeIsIgnored()
        {
            int userId = getUserId();
            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;
            setUpUser(userId);
            GetScoreRequest? pending = null;
            ScoreBasedUserStatisticsUpdate? update = null;
            AddStep("hold readiness request", () => handleGetScoreRequest = request => pending = request);
            registerForUpdates(scoreId, ruleset, value => update = value);
            AddUntilStep("request pending", () => pending != null);
            AddStep("switch account", () => dummyAPI.LocalUser.Value = new APIUser { Id = getUserId() });
            AddStep("release old account response", () => pending!.TriggerSuccess(new SoloScoreInfo { ID = (ulong)scoreId, UserID = userId, Processed = true }));
            AddWaitStep("allow callbacks", 3);
            AddAssert("no old account update", () => update == null);
        }

        [Test]
        public void TestStatisticsResponseAfterAccountChangeIsIgnored()
        {
            int userId = getUserId();
            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;
            setUpUser(userId);
            GetUserRequest? pending = null;
            ScoreBasedUserStatisticsUpdate? update = null;
            AddStep("hold statistics response", () =>
            {
                handleGetScoreRequest = request => request.TriggerSuccess(new SoloScoreInfo { ID = (ulong)scoreId, UserID = userId, Processed = true });
                handleGetUserRequest = request => pending = request;
            });
            registerForUpdates(scoreId, ruleset, value => update = value);
            AddUntilStep("statistics pending", () => pending != null);
            AddStep("switch account", () => dummyAPI.LocalUser.Value = new APIUser { Id = getUserId() });
            AddStep("release old statistics", () => pending!.TriggerSuccess(new APIUser
            {
                Id = userId,
                Statistics = new UserStatistics { TotalScore = 99_000_000 }
            }));
            AddWaitStep("allow callbacks", 3);
            AddAssert("no old account update", () => update == null);
            AddAssert("new account cache not overwritten", () => statisticsProvider.GetStatisticsFor(ruleset)?.TotalScore != 99_000_000);
        }

        [Test]
        public void TestPushAndReadinessRacePublishesOnce()
        {
            int userId = getUserId();
            long scoreId = getScoreId();
            var ruleset = new OsuRuleset().RulesetInfo;
            setUpUser(userId);
            GetScoreRequest? pending = null;
            int updates = 0;
            AddStep("hold readiness request", () => handleGetScoreRequest = request => pending = request);
            registerForUpdates(scoreId, ruleset, _ => updates++);
            AddUntilStep("request pending", () => pending != null);
            feignScoreProcessing(userId, ruleset, 5_000_000);
            AddStep("deliver push", () => ((ISpectatorClient)spectatorClient).UserScoreProcessed(userId, scoreId));
            AddUntilStep("push received", () => updates == 1);
            AddStep("release readiness", () => pending!.TriggerSuccess(new SoloScoreInfo { ID = (ulong)scoreId, UserID = userId, Processed = true }));
            AddWaitStep("allow callbacks", 3);
            AddAssert("single update", () => updates, () => Is.EqualTo(1));
        }

        private int nextUserId = 2000;
        private long nextScoreId = 50000;

        private int getUserId() => ++nextUserId;
        private long getScoreId() => ++nextScoreId;

        private void setUpUser(int userId)
        {
            AddStep("fetch initial stats", () =>
            {
                serverSideStatistics[(userId, "osu")] = new UserStatistics { TotalScore = 4_000_000 };
                serverSideStatistics[(userId, "taiko")] = new UserStatistics { TotalScore = 3_000_000 };
                serverSideStatistics[(userId, "fruits")] = new UserStatistics { TotalScore = 2_000_000 };
                serverSideStatistics[(userId, "mania")] = new UserStatistics { TotalScore = 1_000_000 };

                dummyAPI.LocalUser.Value = new APIUser { Id = userId };
            });
        }

        private void registerForUpdates(long scoreId, RulesetInfo rulesetInfo, Action<ScoreBasedUserStatisticsUpdate> onUpdateReady) =>
            AddStep("register for updates", () =>
            {
                watcher.RegisterForStatisticsUpdateAfter(
                    new ScoreInfo(Beatmap.Value.BeatmapInfo, new OsuRuleset().RulesetInfo, new RealmUser())
                    {
                        Ruleset = rulesetInfo,
                        OnlineID = scoreId
                    });
                watcher.LatestUpdate.BindValueChanged(update =>
                {
                    if (update.NewValue?.Score.OnlineID == scoreId)
                        onUpdateReady.Invoke(update.NewValue);
                });
            });

        private void feignScoreProcessing(int userId, RulesetInfo rulesetInfo, long newTotalScore)
            => AddStep("feign score processing", () => serverSideStatistics[(userId, rulesetInfo.ShortName)] = new UserStatistics { TotalScore = newTotalScore });
    }
}
