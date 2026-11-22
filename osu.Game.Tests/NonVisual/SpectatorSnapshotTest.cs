using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SpectatorSnapshotTest
    {
        [Test]
        public void TestBeginSnapshotPreservesWireBytesAndDoesNotFollowMutableSource()
        {
            var speed = new BindableDouble(1.5);
            var mod = new APIMod { Acronym = "DT", Settings = new Dictionary<string, object> { ["speed_change"] = speed } };
            var state = new SpectatorState
            {
                BeatmapID = 42,
                RulesetID = 0,
                Mods = new[] { mod },
                State = SpectatedUserState.Playing,
                MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Great] = 100 },
                TagCoopPlayers = new[] { new TagCoopReplayPlayer { UserID = 7, Username = "test" } },
            };
            var protocol = new MessagePackHubProtocol(Options.Create(new MessagePackHubProtocolOptions
            {
                SerializerOptions = SignalRUnionWorkaroundResolver.OPTIONS,
            }));
            byte[] expected = protocol.GetMessageBytes(new InvocationMessage("1", "BeginPlaySession", new object[] { 123L, state })).ToArray();
            var snapshot = SpectatorSnapshot.State(state);
            Assert.That(protocol.GetMessageBytes(new InvocationMessage("1", "BeginPlaySession", new object[] { 123L, snapshot })).ToArray(), Is.EqualTo(expected));
            state.BeatmapID = 43;
            state.MaximumStatistics[HitResult.Great] = 200;
            state.TagCoopPlayers[0] = new TagCoopReplayPlayer { UserID = 8, Username = "next" };
            speed.Value = 2;
            mod.Acronym = "NC";
            mod.Settings.Clear();
            Assert.That(snapshot.BeatmapID, Is.EqualTo(42));
            Assert.That(snapshot.MaximumStatistics[HitResult.Great], Is.EqualTo(100));
            Assert.That(snapshot.TagCoopPlayers[0].UserID, Is.EqualTo(7));
            Assert.That(snapshot.Mods.Single().Acronym, Is.EqualTo("DT"));
            Assert.That(snapshot.Mods.Single().Settings["speed_change"], Is.EqualTo(1.5));
            Assert.That(protocol.GetMessageBytes(new InvocationMessage("1", "BeginPlaySession", new object[] { 123L, snapshot })).ToArray(), Is.EqualTo(expected));
        }

        [TestCase(false, false, 100_000, 1_000_000L)]
        [TestCase(true, false, 100_000, null)]
        [TestCase(true, true, 123_456, null)]
        [NonParallelizable]
        public void TestLiveScoreIsOnlySentOnMosu(bool thirdParty, bool stable, long expectedOriginal, long? expectedMosu)
        {
            bool previousThirdParty = MosuServerEnvironment.IsThirdPartyServer;
            bool previousStable = MosuServerEnvironment.UsesStableProtocol;
            try
            {
                MosuServerEnvironment.IsThirdPartyServer = thirdParty;
                MosuServerEnvironment.UsesStableProtocol = stable;
                var header = new FrameHeader(new ScoreInfo
                {
                    TotalScore = 100_000,
                    MosuTotalScore = 1_000_000,
                    LegacyTotalScore = 123_456,
                }, new ScoreProcessorStatistics());
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(header.TotalScore, Is.EqualTo(expectedOriginal));
                    Assert.That(header.MosuTotalScore, Is.EqualTo(expectedMosu));
                }
            }
            finally
            {
                MosuServerEnvironment.IsThirdPartyServer = previousThirdParty;
                MosuServerEnvironment.UsesStableProtocol = previousStable;
            }
        }

        [Test]
        public void TestFrameHeaderOwnsScoreAndProcessorSnapshots()
        {
            var speed = new BindableDouble(1.5);
            var score = new ScoreInfo
            {
                TotalScore = 1000,
                Combo = 5,
                MaxCombo = 10,
                Accuracy = .9,
                Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = 5 },
                APIMods = new[] { new APIMod { Acronym = "DT", Settings = new Dictionary<string, object> { ["speed_change"] = speed } } },
            };
            var statistics = new ScoreProcessorStatistics { BaseScore = 1500, MaximumBaseScore = 1800, AccuracyJudgementCount = 6, ComboPortion = .1, BonusPortion = .2 };
            var header = new FrameHeader(score, statistics);
            byte[] wire = MessagePackSerializer.Serialize(header, SignalRUnionWorkaroundResolver.OPTIONS);
            score.TotalScore = 2000;
            score.Combo = 6;
            score.Statistics[HitResult.Great] = 6;
            score.Pauses.Add(100);
            score.APIMods[0].Acronym = "NC";
            speed.Value = 2;
            statistics.BaseScore = 2000;
            statistics.AccuracyJudgementCount = 7;
            Assert.That(MessagePackSerializer.Serialize(header, SignalRUnionWorkaroundResolver.OPTIONS), Is.EqualTo(wire));
            Assert.That(header.ScoreProcessorStatistics, Is.Not.SameAs(statistics));
            Assert.That(header.Statistics[HitResult.Great], Is.EqualTo(5));
            Assert.That(header.Pauses, Is.Empty);
        }
    }
}
