// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Online.DodgeWorld;
using osuTK;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// Covers the client half of Dodge World presence: which room the server is told about, how often
    /// positions go out, and what the room's occupants look like locally.
    /// </summary>
    [HeadlessTest]
    public partial class TestSceneDodgeWorldClient : OsuTestScene
    {
        protected override bool UseOnlineAPI => false;

        private TestDodgeWorldClient client = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create client", () => Child = client = new TestDodgeWorldClient());
            AddUntilStep("client loaded", () => client.IsLoaded);
            AddStep("connect", () => client.SetConnected(true));
        }

        [Test]
        public void TestJoiningARoomListsWhoIsAlreadyThere()
        {
            AddStep("someone is already in the plaza", () => client.Occupants = new[]
            {
                new DodgeWorldUser { UserId = 7, State = new DodgeWorldPlayerState { X = 40, Health = 10 } },
            });

            AddStep("join the plaza", () => client.JoinRoom("mora-plaza"));

            AddUntilStep("the occupant is known", () => client.Players.Count, () => Is.EqualTo(1));
            AddAssert("at the position reported", () => client.Players[7].X, () => Is.EqualTo(40));
            AddAssert("the room was joined once", () => client.Joins, () => Is.EqualTo(new[] { "mora-plaza" }));
        }

        /// <summary>
        /// Moving to another room must not leave the previous room's players on screen.
        /// </summary>
        [Test]
        public void TestChangingRoomForgetsTheOldOne()
        {
            AddStep("join the plaza", () => client.JoinRoom("mora-plaza"));
            AddStep("someone joins it", () => ((IDodgeWorldClient)client).UserJoined(7, new DodgeWorldPlayerState { Health = 10 }));
            AddUntilStep("they are here", () => client.Players.Count, () => Is.EqualTo(1));

            AddStep("walk into the east room", () => client.JoinRoom("east"));
            AddAssert("nobody is here", () => client.Players, () => Is.Empty);
            AddAssert("both rooms were joined", () => client.Joins, () => Is.EqualTo(new[] { "mora-plaza", "east" }));
        }

        /// <summary>
        /// An update for somebody who is not in the room would otherwise conjure a player out of
        /// nothing, which is what a stale message arriving after a room change looks like.
        /// </summary>
        [Test]
        public void TestUpdatesForUnknownPlayersAreIgnored()
        {
            AddStep("join the plaza", () => client.JoinRoom("mora-plaza"));
            AddStep("a stranger moves", () => ((IDodgeWorldClient)client).UserStateChanged(9, new DodgeWorldPlayerState { Health = 10 }));
            AddAssert("nobody appeared", () => client.Players, () => Is.Empty);
        }

        [Test]
        public void TestBrokenStateIsRefused()
        {
            AddStep("join the plaza", () => client.JoinRoom("mora-plaza"));
            AddStep("a player arrives at nowhere",
                () => ((IDodgeWorldClient)client).UserJoined(7, new DodgeWorldPlayerState { X = float.NaN }));
            AddAssert("they were not accepted", () => client.Players, () => Is.Empty);
        }

        /// <summary>
        /// Standing still must not cost anything: nothing has changed, so there is nothing to send.
        /// </summary>
        [Test]
        public void TestStandingStillSendsNothing()
        {
            AddStep("join the plaza", () => client.JoinRoom("mora-plaza"));
            AddStep("stand at one spot", () => client.SetLocalState(new Vector2(10, 20), Vector2.Zero, false, 10));

            AddUntilStep("the position went out once", () => client.Sent, () => Is.EqualTo(1));
            AddWaitStep("wait a while", 20);
            AddAssert("and only once", () => client.Sent, () => Is.EqualTo(1));

            AddStep("take a step", () => client.SetLocalState(new Vector2(11, 20), Vector2.Zero, true, 10));
            AddUntilStep("which was sent", () => client.Sent, () => Is.EqualTo(2));
        }

        [Test]
        public void TestNothingIsSentOutsideARoom()
        {
            AddStep("move without being in a room", () => client.SetLocalState(new Vector2(10, 20), Vector2.Zero, true, 10));
            AddWaitStep("wait a while", 10);
            AddAssert("nothing was sent", () => client.Sent, () => Is.Zero);
        }

        [Test]
        public void TestLeavingTheWorldClearsTheRoom()
        {
            AddStep("join the plaza", () => client.JoinRoom("mora-plaza"));
            AddStep("someone joins it", () => ((IDodgeWorldClient)client).UserJoined(7, new DodgeWorldPlayerState { Health = 10 }));
            AddUntilStep("they are here", () => client.Players.Count, () => Is.EqualTo(1));

            AddStep("leave", () => client.LeaveRoom());

            AddAssert("the room emptied", () => client.Players, () => Is.Empty);
            AddAssert("no room is held", () => client.CurrentRoomId, () => Is.Null);
            AddAssert("the server was told", () => client.Leaves, () => Is.EqualTo(1));
        }

        /// <summary>
        /// A reconnection leaves the server knowing nothing, so the room has to be entered again rather
        /// than assumed to still be there.
        /// </summary>
        [Test]
        public void TestReconnectingRejoinsTheRoom()
        {
            AddStep("join the plaza", () => client.JoinRoom("mora-plaza"));
            AddStep("someone joins it", () => ((IDodgeWorldClient)client).UserJoined(7, new DodgeWorldPlayerState { Health = 10 }));
            AddUntilStep("they are here", () => client.Players.Count, () => Is.EqualTo(1));

            AddStep("connection drops", () => client.SetConnected(false));
            AddUntilStep("the room emptied", () => client.Players, () => Is.Empty);

            AddStep("connection returns", () => client.SetConnected(true));
            AddUntilStep("the room was rejoined",
                () => client.Joins, () => Is.EqualTo(new[] { "mora-plaza", "mora-plaza" }));
        }

        /// <summary>
        /// The room's mobs belong to the server only once it says so, and an attack is pointless before
        /// then: locally there is nothing to swing at, and the server is not listening for it.
        /// </summary>
        [Test]
        public void TestAttacksOnlyGoOutForARoomTheServerRuns()
        {
            AddStep("join a room the server does not run", () => client.JoinRoom("mora-plaza"));
            AddUntilStep("the room was joined", () => client.Joins, () => Is.Not.Empty);
            AddStep("swing", () => client.Attack(new Vector2(1, 0)));
            AddAssert("nothing was sent", () => client.Attacks, () => Is.Zero);
            AddAssert("and the room is not server-run", () => client.ServerSimulated.Value, () => Is.False);

            AddStep("the server takes the room over", () =>
            {
                client.ServerRunsTheRoom = true;
                client.JoinRoom("east");
            });

            AddUntilStep("which the client is told", () => client.ServerSimulated.Value);
            AddStep("swing", () => client.Attack(new Vector2(1, 0)));
            AddAssert("the swing was sent", () => client.Attacks, () => Is.EqualTo(1));

            AddStep("swing at nowhere", () => client.Attack(Vector2.Zero));
            AddAssert("which is not a swing", () => client.Attacks, () => Is.EqualTo(1));
        }

        /// <summary>
        /// A room snapshot describes the room the client is in. One arriving after leaving would leave
        /// mobs standing in a room the player is no longer in.
        /// </summary>
        [Test]
        public void TestSnapshotsOutsideARoomAreDropped()
        {
            var received = 0;

            AddStep("count snapshots", () => client.RoomStateReceived += () => received++);

            AddStep("send one before joining", () => ((IDodgeWorldClient)client).RoomStateChanged(new DodgeWorldRoomSnapshot
            {
                Mobs = [new DodgeWorldMob { Id = 1 }],
            }));

            AddAssert("it was dropped", () => client.RoomState.Mobs, () => Is.Empty);

            AddStep("join a room", () => client.JoinRoom("mora-plaza"));
            AddStep("send another", () => ((IDodgeWorldClient)client).RoomStateChanged(new DodgeWorldRoomSnapshot
            {
                Mobs = [new DodgeWorldMob { Id = 1 }],
            }));

            AddUntilStep("it was kept", () => client.RoomState.Mobs, () => Has.Length.EqualTo(1));

            AddStep("leave", () => client.LeaveRoom());
            AddAssert("the room emptied", () => client.RoomState.Mobs, () => Is.Empty);
            AddAssert("and the change was announced", () => received, () => Is.GreaterThan(0));
        }

        /// <summary>
        /// Nothing here ever asks to be paid: a reward arrives already decided, which is the whole reason
        /// it travels this way rather than as an HTTP claim the client makes.
        /// </summary>
        [Test]
        public void TestRewardsArriveDecidedByTheServer()
        {
            DodgeWorldReward? received = null;

            AddStep("watch for rewards", () => client.RewardReceived += reward => received = reward);
            AddStep("join the plaza", () => client.JoinRoom("mora-plaza"));

            AddStep("a kill is paid for", () => ((IDodgeWorldClient)client).RewardGranted(new DodgeWorldReward
            {
                ZoneId = "slimes",
                Granted = true,
                Experience = 35,
                Coins = 4,
                Level = 2,
                TotalCoins = 4,
            }));

            AddUntilStep("the reward arrived", () => received?.ZoneId, () => Is.EqualTo("slimes"));
            AddAssert("with the server's numbers", () => received!.Coins, () => Is.EqualTo(4));

            // A reward is not gated on still being in the room: the grant has already happened, and a
            // player who walked out as their last kill landed is still owed the news of it.
            AddStep("leave", () => client.LeaveRoom());
            AddStep("a last kill is refused", () => ((IDodgeWorldClient)client).RewardGranted(new DodgeWorldReward
            {
                ZoneId = "slimes",
                Reason = "maximum_farm_level",
            }));

            AddUntilStep("which arrived with its reason", () => received?.Reason, () => Is.EqualTo("maximum_farm_level"));
        }
    }
}
