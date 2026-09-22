// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
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
