// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using MessagePack;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.Online;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.Multiplayer.MatchTypes.Dodge;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Spectator;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.Online
{
    [GeneratedMessagePackResolver]
    public partial class OsuGameTestsMessagePackResolver
    {
    }

    [TestFixture]
    public class TestMultiplayerMessagePackSerialization
    {
        [Test]
        public void TestSerialiseRoom()
        {
            var room = new MultiplayerRoom(1)
            {
                MatchState = new TeamVersusRoomState()
            };

            byte[] serialized = MessagePackSerializer.Serialize(room);

            var deserialized = MessagePackSerializer.Deserialize<MultiplayerRoom>(serialized);

            ClassicAssert.True(deserialized.MatchState is TeamVersusRoomState);
        }

        [Test]
        public void TestSerialiseRankedPlayRecentMatchFinalStateCompatibility()
        {
            var match = new RankedPlayRecentMatch
            {
                RoomId = 247,
                PoolId = 3,
                CompletedAtUnixMilliseconds = 1234,
                HasFinalState = true,
                State = new RankedPlayRoomState
                {
                    Users =
                    {
                        { 10, new RankedPlayUserInfo { Rating = 1500, Life = 734_567, RoundsWon = 3 } },
                        { 20, new RankedPlayUserInfo { Rating = 1500, Life = 0, RoundsWon = 1 } },
                    }
                }
            };

            byte[] serialized = MessagePackSerializer.Serialize(match);
            var deserialized = MessagePackSerializer.Deserialize<RankedPlayRecentMatch>(serialized);
            var legacyCompatible = MessagePackSerializer.Deserialize<LegacyCompatibleRankedPlayRecentMatch>(serialized);
            byte[] legacySerialized = MessagePackSerializer.Serialize(legacyCompatible);
            var extendedFromLegacy = MessagePackSerializer.Deserialize<RankedPlayRecentMatch>(legacySerialized);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.HasFinalState, Is.True);
                Assert.That(deserialized.State.Users[10].Life, Is.EqualTo(734_567));
                Assert.That(deserialized.State.Users[10].RoundsWon, Is.EqualTo(3));
                Assert.That(legacyCompatible.RoomId, Is.EqualTo(247));
                Assert.That(extendedFromLegacy.HasFinalState, Is.False);
            });
        }

        [Test]
        public void TestSerialiseUserStateExpected()
        {
            var state = new TeamVersusUserState();

            byte[] serialized = MessagePackSerializer.Serialize(typeof(MatchUserState), state);
            var deserialized = MessagePackSerializer.Deserialize<MatchUserState>(serialized);

            ClassicAssert.True(deserialized is TeamVersusUserState);
        }

        [Test]
        public void TestSerialiseUnionFailsWithSignalR()
        {
            var state = new TeamVersusUserState();

            // SignalR serialises using the actual type, rather than a base specification.
            byte[] serialized = MessagePackSerializer.Serialize(typeof(TeamVersusUserState), state);

            // works with explicit type specified.
            MessagePackSerializer.Deserialize<TeamVersusUserState>(serialized);

            // fails with base (union) type.
            Assert.Throws<MessagePackSerializationException>(() => MessagePackSerializer.Deserialize<MatchUserState>(serialized));
        }

        [Test]
        public void TestSerialiseUnionSucceedsWithWorkaround()
        {
            var state = new TeamVersusUserState();

            // SignalR serialises using the actual type, rather than a base specification.
            byte[] serialized = MessagePackSerializer.Serialize(typeof(TeamVersusUserState), state, SignalRUnionWorkaroundResolver.OPTIONS);

            // works with explicit type specified.
            MessagePackSerializer.Deserialize<TeamVersusUserState>(serialized);

            // works with custom resolver.
            var deserialized = MessagePackSerializer.Deserialize<MatchUserState>(serialized, SignalRUnionWorkaroundResolver.OPTIONS);
            ClassicAssert.True(deserialized is TeamVersusUserState);
        }

        [Test]
        public void TestSerialiseTagCoopRoomState()
        {
            MatchRoomState state = new TagCoopRoomState { PlayerOrder = new[] { 12, 34 } };

            byte[] serialized = MessagePackSerializer.Serialize(state);
            var deserialized = MessagePackSerializer.Deserialize<MatchRoomState>(serialized);

            ClassicAssert.IsInstanceOf<TagCoopRoomState>(deserialized);
            CollectionAssert.AreEqual(new[] { 12, 34 }, ((TagCoopRoomState)deserialized).PlayerOrder);
        }

        [Test]
        public void TestSerialiseTagCoopJudgementRequestWithSignalRWorkaround()
        {
            var request = new TagCoopJudgementRequest
            {
                ObjectIndex = 42,
                Result = HitResult.Great,
            };

            byte[] serialized = MessagePackSerializer.Serialize(typeof(TagCoopJudgementRequest), request, SignalRUnionWorkaroundResolver.OPTIONS);
            var deserialized = MessagePackSerializer.Deserialize<MatchUserRequest>(serialized, SignalRUnionWorkaroundResolver.OPTIONS);

            ClassicAssert.IsInstanceOf<TagCoopJudgementRequest>(deserialized);
            ClassicAssert.AreEqual(42, ((TagCoopJudgementRequest)deserialized).ObjectIndex);
            ClassicAssert.AreEqual(HitResult.Great, ((TagCoopJudgementRequest)deserialized).Result);
        }

        [Test]
        public void TestSerialiseTagCoopReplayState()
        {
            var state = new SpectatorState
            {
                BeatmapID = 123,
                RulesetID = 0,
                TagCoopPlayers =
                [
                    new TagCoopReplayPlayer { UserID = 12, Username = "first" },
                    new TagCoopReplayPlayer { UserID = 34, Username = "second" },
                ]
            };

            byte[] serialized = MessagePackSerializer.Serialize(state);
            var deserialized = MessagePackSerializer.Deserialize<SpectatorState>(serialized);
            var legacyCompatible = MessagePackSerializer.Deserialize<LegacyCompatibleSpectatorState>(serialized);
            byte[] legacySerialized = MessagePackSerializer.Serialize(new LegacyCompatibleSpectatorState { BeatmapID = 456, RulesetID = 0 });
            var extendedFromLegacy = MessagePackSerializer.Deserialize<SpectatorState>(legacySerialized);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.TagCoopPlayers, Has.Length.EqualTo(2));
                Assert.That(deserialized.TagCoopPlayers[1].UserID, Is.EqualTo(34));
                Assert.That(deserialized.TagCoopPlayers[1].Username, Is.EqualTo("second"));
                Assert.That(legacyCompatible.BeatmapID, Is.EqualTo(123));
                Assert.That(legacyCompatible.RulesetID, Is.Zero);
                Assert.That(extendedFromLegacy.BeatmapID, Is.EqualTo(456));
                Assert.That(extendedFromLegacy.TagCoopPlayers, Is.Empty);
            });
        }

        [Test]
        public void TestSerialiseTagCoopReplayFrames()
        {
            var header = new FrameHeader(0, 1, 0, 0, [], new ScoreProcessorStatistics(), default, [], 0, []);
            var bundle = new FrameDataBundle(header, [new LegacyReplayFrame(123, 10, 20, ReplayButtonState.Left1)],
                [new TagCoopReplayFrame { UserID = 34, Sequence = 7, GameplayTime = 123, X = 0.25f, Y = 0.75f, ButtonState = 1 }]);

            byte[] serialized = MessagePackSerializer.Serialize(bundle);
            var deserialized = MessagePackSerializer.Deserialize<FrameDataBundle>(serialized);
            var legacyCompatible = MessagePackSerializer.Deserialize<LegacyCompatibleFrameDataBundle>(serialized);
            byte[] legacySerialized = MessagePackSerializer.Serialize(legacyCompatible);
            var extendedFromLegacy = MessagePackSerializer.Deserialize<FrameDataBundle>(legacySerialized);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Frames, Has.Count.EqualTo(1));
                Assert.That(deserialized.TagCoopFrames, Has.Count.EqualTo(1));
                Assert.That(deserialized.TagCoopFrames[0].UserID, Is.EqualTo(34));
                Assert.That(deserialized.TagCoopFrames[0].GameplayTime, Is.EqualTo(123));
                Assert.That(deserialized.ReplaceTagCoopReplay, Is.False);
                Assert.That(legacyCompatible.Frames, Has.Count.EqualTo(1));
                Assert.That(extendedFromLegacy.TagCoopFrames, Is.Empty);
            });
        }

        [Test]
        public void TestSerialiseCompletedTagCoopReplayReplacement()
        {
            var header = new FrameHeader(0, 1, 0, 0, [], new ScoreProcessorStatistics(), default, [], 0, []);
            var bundle = new FrameDataBundle(header, [new LegacyReplayFrame(123, 10, 20, ReplayButtonState.Left1)])
            {
                ReplaceTagCoopReplay = true,
            };

            byte[] serialized = MessagePackSerializer.Serialize(bundle);
            var deserialized = MessagePackSerializer.Deserialize<FrameDataBundle>(serialized);
            var legacyCompatible = MessagePackSerializer.Deserialize<LegacyCompatibleFrameDataBundle>(serialized);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.ReplaceTagCoopReplay, Is.True);
                Assert.That(legacyCompatible.Frames, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void TestSerialiseTagCoopNativeReplayBatch()
        {
            var request = new TagCoopReplayFramesRequest
            {
                BatchSequence = 3,
                IsFinal = true,
                Frames = [new TagCoopReplayFrame { GameplayTime = 123, X = 0.25f, Y = 0.75f, ButtonState = 1 }],
            };

            byte[] serialized = MessagePackSerializer.Serialize(typeof(TagCoopReplayFramesRequest), request, SignalRUnionWorkaroundResolver.OPTIONS);
            var deserialized = (TagCoopReplayFramesRequest)MessagePackSerializer.Deserialize<MatchUserRequest>(serialized, SignalRUnionWorkaroundResolver.OPTIONS);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.BatchSequence, Is.EqualTo(3));
                Assert.That(deserialized.IsFinal, Is.True);
                Assert.That(deserialized.Frames, Has.Length.EqualTo(1));
                Assert.That(deserialized.Frames[0].ButtonState, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestSerialiseDodgePositionRequestWithSignalRWorkaround()
        {
            var request = new DodgePlayerPositionRequest
            {
                Sequence = 42,
                GameplayTime = 1234,
                X = 0.25f,
                Y = 0.75f,
                PingMilliseconds = 199,
            };

            byte[] serialized = MessagePackSerializer.Serialize(typeof(DodgePlayerPositionRequest), request, SignalRUnionWorkaroundResolver.OPTIONS);
            var deserialized = MessagePackSerializer.Deserialize<MatchUserRequest>(serialized, SignalRUnionWorkaroundResolver.OPTIONS);

            var position = (DodgePlayerPositionRequest)deserialized;
            ClassicAssert.AreEqual(42, position.Sequence);
            ClassicAssert.AreEqual(0.25f, position.X);
            ClassicAssert.AreEqual(0.75f, position.Y);
            ClassicAssert.AreEqual(199, position.PingMilliseconds);
        }

        [Test]
        public void TestSerialiseDodgePositionEvent()
        {
            MatchServerEvent matchEvent = new DodgePlayerPositionEvent
            {
                UserID = 12,
                Sequence = 7,
                GameplayTime = 456,
                X = 0.4f,
                Y = 0.6f,
                PingMilliseconds = 220,
            };

            byte[] serialized = MessagePackSerializer.Serialize(matchEvent);
            var deserialized = (DodgePlayerPositionEvent)MessagePackSerializer.Deserialize<MatchServerEvent>(serialized);

            ClassicAssert.AreEqual(12, deserialized.UserID);
            ClassicAssert.AreEqual(7, deserialized.Sequence);
            ClassicAssert.AreEqual(220, deserialized.PingMilliseconds);
        }

        [Test]
        public void TestSerialiseRankedPlayCursorRequestWithSignalRWorkaround()
        {
            var request = new RankedPlayCursorPositionRequest
            {
                Sequence = 51,
                GameplayTime = 4321,
                X = 0.2f,
                Y = 0.8f,
                ButtonState = 2,
                PingMilliseconds = 87,
            };

            byte[] serialized = MessagePackSerializer.Serialize(typeof(RankedPlayCursorPositionRequest), request, SignalRUnionWorkaroundResolver.OPTIONS);
            var deserialized = (RankedPlayCursorPositionRequest)MessagePackSerializer.Deserialize<MatchUserRequest>(serialized, SignalRUnionWorkaroundResolver.OPTIONS);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Sequence, Is.EqualTo(51));
                Assert.That(deserialized.GameplayTime, Is.EqualTo(4321));
                Assert.That(deserialized.X, Is.EqualTo(0.2f));
                Assert.That(deserialized.Y, Is.EqualTo(0.8f));
                Assert.That(deserialized.ButtonState, Is.EqualTo(2));
                Assert.That(deserialized.PingMilliseconds, Is.EqualTo(87));
            });
        }

        [Test]
        public void TestSerialiseRankedPlayCursorEventWithSignalRWorkaround()
        {
            MatchServerEvent matchEvent = new RankedPlayCursorPositionEvent
            {
                UserID = 12,
                Sequence = 9,
                GameplayTime = 654,
                X = 0.35f,
                Y = 0.65f,
                ButtonState = 1,
                PingMilliseconds = 104,
            };

            byte[] serialized = MessagePackSerializer.Serialize(typeof(RankedPlayCursorPositionEvent), matchEvent, SignalRUnionWorkaroundResolver.OPTIONS);
            var deserialized = (RankedPlayCursorPositionEvent)MessagePackSerializer.Deserialize<MatchServerEvent>(serialized, SignalRUnionWorkaroundResolver.OPTIONS);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.UserID, Is.EqualTo(12));
                Assert.That(deserialized.Sequence, Is.EqualTo(9));
                Assert.That(deserialized.ButtonState, Is.EqualTo(1));
                Assert.That(deserialized.PingMilliseconds, Is.EqualTo(104));
            });
        }

        [MessagePackObject]
        public class LegacyCompatibleSpectatorState
        {
            [Key(0)]
            public int? BeatmapID { get; set; }

            [Key(1)]
            public int? RulesetID { get; set; }
        }

        [MessagePackObject]
        public class LegacyCompatibleRankedPlayRecentMatch
        {
            [Key(0)]
            public long RoomId { get; set; }

            [Key(1)]
            public int PoolId { get; set; }

            [Key(2)]
            public long CompletedAtUnixMilliseconds { get; set; }

            [Key(3)]
            public RankedPlayRoomState State { get; set; } = new RankedPlayRoomState();
        }

        [MessagePackObject]
        public class LegacyCompatibleFrameDataBundle
        {
            [Key(0)]
            public FrameHeader Header { get; set; } = null!;

            [Key(1)]
            public IList<LegacyReplayFrame> Frames { get; set; } = null!;
        }
    }
}
