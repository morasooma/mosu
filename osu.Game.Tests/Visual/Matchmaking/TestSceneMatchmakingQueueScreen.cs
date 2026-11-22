// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Screens.OnlinePlay.Matchmaking.Intro;
using osu.Game.Screens.OnlinePlay.Matchmaking.Queue;
using osu.Game.Tests.Visual.Multiplayer;

namespace osu.Game.Tests.Visual.Matchmaking
{
    public partial class TestSceneMatchmakingQueueScreen : MultiplayerTestScene
    {
        [Cached(typeof(INotificationOverlay))]
        private readonly NotificationOverlay notificationOverlay = new NotificationOverlay();

        private ScreenQueue? queueScreen => Stack.CurrentScreen as ScreenQueue;
        private MatchmakingLobbyStatus lobbyStatus = null!;

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("join room", () => JoinRoom(CreateDefaultRoom(MatchType.Matchmaking)));
            WaitForJoined();

            AddStep("load screen", () => LoadScreen(new ScreenIntro(MatchmakingPoolType.QuickPlay)));
            AddUntilStep("wait for queue screen", () => queueScreen?.IsLoaded == true);

            AddStep("send status update", () =>
            {
                int userId1 = Random.Shared.Next(1, 11);
                int userId2 = Random.Shared.GetItems(Enumerable.Range(1, 10).Except([userId1]).ToArray(), 1).Single();

                lobbyStatus = new MatchmakingLobbyStatus
                {
                    UsersInQueue = Enumerable.Range(1, 10).ToArray(),
                    RatingDistribution = Enumerable.Range(0, 24).Select(i => (400 + i * 100, (int)Math.Round(generateCount(400 + i * 100, 1600, 400, 7200)))).ToArray(),
                    UserRating = Random.Shared.Next(400, 2800),
                    RecentMatches = Enumerable.Range(1, 10).Select(index => new RankedPlayRecentMatch
                    {
                        RoomId = index,
                        CompletedAtUnixMilliseconds = DateTimeOffset.UtcNow.AddMinutes(-index).ToUnixTimeMilliseconds(),
                        HasFinalState = true,
                        State = new RankedPlayRoomState
                        {
                            Users =
                            {
                                { userId1, new RankedPlayUserInfo { Rating = 0, Life = Random.Shared.Next(0, 1_000_001), RoundsWon = Random.Shared.Next(0, 4) } },
                                { userId2, new RankedPlayUserInfo { Rating = 0, Life = Random.Shared.Next(0, 1_000_001), RoundsWon = Random.Shared.Next(0, 4) } },
                            }
                        }
                    }).ToArray()
                };

                MultiplayerClient.MatchmakingLobbyStatusChanged(lobbyStatus).WaitSafely();
            });
        }

        [Test]
        public void TestJoiningQueueLeavesStaleRoomFirst()
        {
            AddAssert("room initially joined", () => MultiplayerClient.ClientRoom != null);
            AddStep("join matchmaking queue", () => QueueController.JoinQueue(new MatchmakingPool { Id = 1, RulesetId = 0 }));
            AddUntilStep("stale room left", () => MultiplayerClient.ClientRoom == null);
            AddUntilStep("queue join sent", () => MultiplayerClient.MatchmakingJoinQueueCallCount, () => Is.EqualTo(1));
            AddUntilStep("controller enters queueing", () => QueueController.CurrentState.Value, () => Is.EqualTo(ScreenQueue.MatchmakingScreenState.Queueing));
        }

        [Test]
        public void TestJoiningQueueWaitsForPendingRoomLeave()
        {
            System.Threading.Tasks.TaskCompletionSource leaveAcknowledged = null!;
            int previousJoins = 0;
            AddStep("delay server leave acknowledgement", () =>
            {
                previousJoins = MultiplayerClient.MatchmakingJoinQueueCallCount;
                leaveAcknowledged = new System.Threading.Tasks.TaskCompletionSource(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
                MultiplayerClient.LeaveRoomDelay = leaveAcknowledged.Task;
                MultiplayerClient.LeaveRoom().FireAndForget();
            });
            AddUntilStep("local room cleared before server leave", () => MultiplayerClient.ClientRoom == null && MultiplayerClient.RoomJoined);
            AddStep("join queue while leave is pending", () => QueueController.JoinQueue(new MatchmakingPool { Id = 1, RulesetId = 0 }));
            AddWaitStep("allow queue request to run", 3);
            AddStep("acknowledge leave and check admission was deferred", () =>
            {
                bool admissionDeferred = MultiplayerClient.MatchmakingJoinQueueCallCount == previousJoins;
                leaveAcknowledged.SetResult();
                Assert.That(admissionDeferred, Is.True);
            });
            AddUntilStep("server room left", () => !MultiplayerClient.RoomJoined);
            AddUntilStep("queue joined after leave", () => MultiplayerClient.MatchmakingJoinQueueCallCount == previousJoins + 1);
            AddStep("restore immediate leaves", () => MultiplayerClient.LeaveRoomDelay = System.Threading.Tasks.Task.CompletedTask);
        }

        [Test]
        public void TestBasic()
        {
            AddStep("change state to idle", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.Idle);

            AddStep("change state to queueing", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.Queueing);

            AddStep("change state to found match", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.PendingAccept);
            AddAssert("controller enters waiting state", () => QueueController.CurrentState.Value, () => Is.EqualTo(ScreenQueue.MatchmakingScreenState.AcceptedWaitingForRoom));

            AddStep("change state to in room", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.InRoom);
            AddStep("return state to idle", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.Idle);
        }

        [Test]
        public void TestPoolRefreshAfterLeavingIdleDoesNotUpdateDisposedDropdown()
        {
            FormDropdown<string> previousDropdown = null!;

            AddUntilStep("exit quick play flow", () =>
            {
                if (Stack.CurrentScreen == null)
                    return true;

                Stack.Exit();
                return false;
            });
            AddStep("load ranked screen", () => LoadScreen(new ScreenIntro(MatchmakingPoolType.RankedPlay)));
            AddUntilStep("wait for ranked queue screen", () => queueScreen?.IsLoaded == true);
            AddStep("change state to idle", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.Idle);
            AddStep("select standard pool", () => QueueController.SelectedPool.Value = new MatchmakingPool { Id = 10, RulesetId = 0 });
            AddUntilStep("idle dropdown loaded", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().SingleOrDefault()?.IsLoaded == true);
            AddStep("leave idle content", () =>
            {
                previousDropdown = queueScreen!.ChildrenOfType<FormDropdown<string>>().Single();
                QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.Queueing;
            });
            AddUntilStep("previous dropdown disposed", () => (bool)typeof(Drawable).GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(previousDropdown)!);
            // OnResuming triggers this same notification when returning from ranked play.
            AddStep("refresh pool while dropdown is gone", () => QueueController.SelectedPool.TriggerChange());
            AddStep("change pool while dropdown is gone", () => QueueController.SelectedPool.Value = new MatchmakingPool { Id = 11, RulesetId = 3 });
            AddStep("return to idle", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.Idle);
            AddUntilStep("new dropdown uses current pool", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().SingleOrDefault()?.Items,
                () => Is.EquivalentTo(new[] { "None", "HD", "CO", "FI" }));
            AddAssert("new dropdown created", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().Single() != previousDropdown);
        }

        private void loadRankedQueue()
        {
            AddUntilStep("exit quick play", () =>
            {
                if (Stack.CurrentScreen == null) return true;
                Stack.Exit();
                return false;
            });
            AddStep("load ranked queue", () => LoadScreen(new ScreenIntro(MatchmakingPoolType.RankedPlay)));
            AddUntilStep("ranked queue loaded", () => queueScreen?.IsLoaded == true);
            AddStep("ranked queue idle", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.Idle);
        }

        [Test]
        public void TestActiveMatchesBelowHistoryAndSnapshotsReplaceRows()
        {
            loadRankedQueue();
            AddStep("send ongoing matches", () =>
            {
                lobbyStatus.ActiveMatches =
                [
                    new RankedPlayActiveMatch { RoomId = 100, UserIds = [1, 2], CurrentRound = 3, Stage = RankedPlayStage.CardPlay },
                    new RankedPlayActiveMatch { RoomId = 100, UserIds = [1, 2], CurrentRound = 3, Stage = RankedPlayStage.CardPlay },
                    new RankedPlayActiveMatch { RoomId = 101, UserIds = [3], Stage = RankedPlayStage.Gameplay },
                    new RankedPlayActiveMatch { RoomId = 102, UserIds = [4, 5], Stage = RankedPlayStage.Ended },
                ];
                MultiplayerClient.MatchmakingLobbyStatusChanged(lobbyStatus).WaitSafely();
            });
            AddUntilStep("only one valid match displayed", () => queueScreen!.ChildrenOfType<RankedPlayActiveMatchRow>().Count(), () => Is.EqualTo(1));
            AddAssert("active matches below history", () =>
            {
                var active = queueScreen!.ChildrenOfType<RankedPlayActiveMatches>().Single();
                var history = queueScreen.ChildrenOfType<RankedPlayMatchPanel>().First();
                return active.ScreenSpaceDrawQuad.TopLeft.Y > history.ScreenSpaceDrawQuad.TopLeft.Y;
            });
            AddAssert("watch button has visible geometry", () => queueScreen!.ChildrenOfType<RankedPlayActiveMatchRow>().Single().WatchButton.DrawWidth > 0);
            AddStep("repeat snapshot", () => MultiplayerClient.MatchmakingLobbyStatusChanged(lobbyStatus).WaitSafely());
            AddWaitStep("wait for repeated snapshot", 3);
            AddAssert("not duplicated", () => queueScreen!.ChildrenOfType<RankedPlayActiveMatchRow>().Count() == 1);
            AddStep("match ends", () =>
            {
                lobbyStatus.ActiveMatches = [];
                MultiplayerClient.MatchmakingLobbyStatusChanged(lobbyStatus).WaitSafely();
            });
            AddUntilStep("ended match removed", () => !queueScreen!.ChildrenOfType<RankedPlayActiveMatchRow>().Any());
            AddUntilStep("empty state displayed", () => queueScreen!.ChildrenOfType<RankedPlayActiveMatches>().Single().EmptyMessage.IsPresent);
        }

        [Test]
        public void TestWatchActiveMatchOpensRankedSpectatorLoader()
        {
            loadRankedQueue();
            AddStep("leave fixture room", () => MultiplayerClient.LeaveRoom().FireAndForget());
            AddUntilStep("fixture room left", () => MultiplayerClient.ClientRoom == null);
            AddStep("send ongoing match", () => MultiplayerClient.MatchmakingLobbyStatusChanged(new MatchmakingLobbyStatus
            {
                ActiveMatches = [new RankedPlayActiveMatch { RoomId = 100, UserIds = [1, 2], Stage = RankedPlayStage.Gameplay }]
            }).WaitSafely());
            AddUntilStep("match button loaded", () => queueScreen!.ChildrenOfType<RankedPlayActiveMatchRow>().SingleOrDefault()?.WatchButton.IsLoaded == true);
            AddStep("click watch", () => queueScreen!.ChildrenOfType<RankedPlayActiveMatchRow>().Single().WatchButton.TriggerClick());
            AddUntilStep("ranked spectator loader opened", () => Stack.CurrentScreen is osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.RankedPlaySpectatorLoader);
        }

        [Test]
        public void TestRepeatedRecentMatchSnapshotDoesNotDuplicatePanels()
        {
            AddUntilStep("ten recent matches shown", () => queueScreen!.ChildrenOfType<RankedPlayMatchPanel>().Count(), () => Is.EqualTo(10));
            AddStep("repeat same status snapshot", () => MultiplayerClient.MatchmakingLobbyStatusChanged(lobbyStatus).WaitSafely());
            AddWaitStep("wait for async refresh", 5);
            AddAssert("still ten recent matches", () => queueScreen!.ChildrenOfType<RankedPlayMatchPanel>().Count(), () => Is.EqualTo(10));
        }

        [Test]
        public void TestMalformedRecentMatchIsIgnored()
        {
            AddUntilStep("ten recent matches shown", () => queueScreen!.ChildrenOfType<RankedPlayMatchPanel>().Count(), () => Is.EqualTo(10));
            AddStep("send malformed recent match", () => MultiplayerClient.MatchmakingLobbyStatusChanged(new MatchmakingLobbyStatus
            {
                UsersInQueue = lobbyStatus.UsersInQueue,
                RatingDistribution = lobbyStatus.RatingDistribution,
                UserRating = lobbyStatus.UserRating,
                RecentMatches = lobbyStatus.RecentMatches.Append(new RankedPlayRecentMatch
                {
                    RoomId = 999,
                    CompletedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    State = new RankedPlayRoomState
                    {
                        Users = { { 1, new RankedPlayUserInfo { Rating = 0 } } }
                    }
                }).ToArray()
            }).WaitSafely());
            AddWaitStep("wait for async refresh", 5);
            AddAssert("malformed match ignored", () => queueScreen!.ChildrenOfType<RankedPlayMatchPanel>().Count(), () => Is.EqualTo(10));
        }

        [Test]
        public void TestRandomModsControlHasBoundedWidth()
        {
            AddStep("change state to idle", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.Idle);
            AddUntilStep("random mods control loaded", () => queueScreen!.ChildrenOfType<FormCheckBox>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("random mods control is not stretched", () => queueScreen!.ChildrenOfType<FormCheckBox>().Single().DrawWidth, () => Is.EqualTo(180).Within(1));
        }

        [Test]
        public void TestManagePoolUsesSelectedModeAndSharedContributors()
        {
            var requests = new System.Collections.Generic.List<int>();
            osu.Game.Online.API.Requests.ReplaceRelaxRankedPoolRequest? saved = null;
            AddStep("mock shared pools", () =>
            {
                var fallback = ((osu.Game.Online.API.DummyAPIAccess)API).HandleRequest;
                ((osu.Game.Online.API.DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is osu.Game.Online.API.Requests.GetRelaxRankedPoolRequest get)
                    {
                        requests.Add(get.RulesetId);
                        get.TriggerSuccess(new osu.Game.Online.API.Requests.RelaxRankedPoolResponse
                        {
                            RulesetId = get.RulesetId,
                            Revision = 7,
                            CanManage = true,
                            Editors = [new() { Id = 1, Username = "editor one" }, new() { Id = 2, Username = "editor two" }],
                            Beatmaps = [new() { BeatmapId = 10, BeatmapsetId = 1, Title = "shared map", Version = "test", AddedBy = 2, AddedByUsername = "editor two" }],
                        });
                        return true;
                    }
                    if (request is osu.Game.Online.API.Requests.ReplaceRelaxRankedPoolRequest replace)
                    {
                        saved = replace;
                        replace.TriggerSuccess(new osu.Game.Online.API.Requests.ReplaceRelaxRankedPoolResponse { Accepted = 1 });
                        return true;
                    }
                    return fallback?.Invoke(request) ?? false;
                };
            });
            loadRankedQueue();
            AddStep("select standard", () => QueueController.SelectedPool.Value = new MatchmakingPool { Id = 10, RulesetId = 0, Name = "Standard" });
            AddUntilStep("standard request sent", () => requests.Contains(0));
            AddStep("select mania", () => QueueController.SelectedPool.Value = new MatchmakingPool { Id = 11, RulesetId = 3, Name = "Mania" });
            AddUntilStep("mania request sent", () => requests.Contains(3));
            AddStep("open manage pool", () => queueScreen!.CreateFooterButtons().Single(button => button.Text.ToString() == "Manage pool").TriggerClick());
            AddUntilStep("mode editor opened", () => Stack.CurrentScreen is RelaxPoolEditorScreen { IsLoaded: true });
            AddAssert("editor title uses selected mode", () => ((RelaxPoolEditorScreen)Stack.CurrentScreen).Title == "Mania pool");
            AddUntilStep("playlist loaded", () => ((Drawable)Stack.CurrentScreen).ChildrenOfType<osu.Game.Screens.OnlinePlay.DrawableRoomPlaylistItem>().Any());
            AddAssert("contributor retained", () => ((Drawable)Stack.CurrentScreen).ChildrenOfType<osu.Game.Screens.OnlinePlay.DrawableRoomPlaylistItem>().Single().PoolContributor == "editor two");
            AddAssert("map uses mania ruleset", () => ((Drawable)Stack.CurrentScreen).ChildrenOfType<osu.Game.Screens.OnlinePlay.DrawableRoomPlaylistItem>().Single().Item.RulesetID == 3);
            AddStep("save shared pool", () => ((RelaxPoolEditorScreen)Stack.CurrentScreen).CreateFooterButtons().Single(button => button.Text.ToString() == "Save pool").TriggerClick());
            AddUntilStep("save uses selected mode and revision", () => saved is { RulesetId: 3, Revision: 7 } && saved.BeatmapIds.SequenceEqual(new[] { 10 }));
        }

        [Test]
        public void TestRankedPlayDoesNotShowQuickPlayNotice()
        {
            AddUntilStep("exit quick play flow", () =>
            {
                if (Stack.CurrentScreen == null)
                    return true;

                Stack.Exit();
                return false;
            });
            AddStep("load ranked screen", () => LoadScreen(new ScreenIntro(MatchmakingPoolType.RankedPlay)));
            AddUntilStep("wait for ranked queue screen", () => queueScreen?.IsLoaded == true);
            AddAssert("quick play notice absent", () => !queueScreen!.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString().Contains("continuous and rapid development")));
            AddUntilStep("visual mod dropdown loaded", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().SingleOrDefault()?.IsLoaded == true);
            AddStep("select standard pool", () => QueueController.SelectedPool.Value = new MatchmakingPool { Id = 10, RulesetId = 0 });
            AddUntilStep("standard offers HD and TC", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().Single().Items,
                () => Is.EquivalentTo(new[] { "None", "HD", "TC" }));
            AddStep("choose TC", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().Single().Current.Value = "TC");
            AddStep("select mania pool", () => QueueController.SelectedPool.Value = new MatchmakingPool { Id = 11, RulesetId = 3 });
            AddUntilStep("mania offers HD CO FI not TC", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().Single().Items,
                () => Is.EquivalentTo(new[] { "None", "HD", "CO", "FI" }));
            AddAssert("unsupported TC reset", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().Single().Current.Value == "None");
            AddStep("select relax pool", () => QueueController.SelectedPool.Value = new MatchmakingPool { Id = 12, RulesetId = 4 });
            AddUntilStep("relax offers standard visual mods", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().Single().Items,
                () => Is.EquivalentTo(new[] { "None", "HD", "TC" }));
            AddAssert("visual mod dropdown width bounded", () => queueScreen!.ChildrenOfType<FormDropdown<string>>().Single().DrawWidth,
                () => Is.EqualTo(400).Within(1));
            AddAssert("visual mod dropdown inside screen", () =>
            {
                var control = queueScreen!.ChildrenOfType<FormDropdown<string>>().Single();
                var bounds = control.ScreenSpaceDrawQuad.AABBFloat;
                var screen = queueScreen!.ScreenSpaceDrawQuad.AABBFloat;
                return bounds.Left >= screen.Left && bounds.Right <= screen.Right
                       && bounds.Top >= screen.Top && bounds.Bottom <= screen.Bottom;
            });
        }

        [Test]
        public void TestDelayedRoomScreenPushDoesNotRunIfRoomIsLeftPrematurely()
        {
            AddStep("change state to in room then immediately leave room", () =>
            {
                QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.InRoom;
                MultiplayerClient.LeaveRoom();
            });

            // the queue screen waits 2 seconds between transitioning to `InRoom` state and actually pushing the relevant screen.
            // if the room goes to `null` in that time, things die very hard.
            // therefore the wait here is to check that things don't die very hard.
            // if they do the test will throw an exception and fail.
            AddWaitStep("wait a little bit", 10);
        }

        [Test]
        public void TestRoomScreenPushNullHandling()
        {
            AddStep("leave room", () => MultiplayerClient.LeaveRoom());

            AddWaitStep("wait for room leave", 5);

            AddStep("change state to in room", () => QueueController.CurrentState.Value = ScreenQueue.MatchmakingScreenState.InRoom);

            AddUntilStep("controller returns to idle", () => QueueController.CurrentState.Value, () => Is.EqualTo(ScreenQueue.MatchmakingScreenState.Idle));
        }

        private static double generateCount(double x, double mean, double stdDev, double amplitude)
        {
            return amplitude * Math.Exp(-Math.Pow(x - mean, 2) / (2 * Math.Pow(stdDev, 2))) + Random.Shared.Next(300);
        }
    }
}
