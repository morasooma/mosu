// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Input.StateChanges;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeReplayTest
    {
        [Test]
        public void TestLegacyRoundTripPreservesPositionAndActions()
        {
            var source = new DodgeReplayFrame(
                1234,
                new Vector2(120, 240),
                DodgeAction.MoveLeft,
                DodgeAction.MoveUp,
                DodgeAction.Slow);

            var legacy = source.ToLegacy(new Beatmap());
            var decoded = new DodgeReplayFrame();
            decoded.Time = legacy.Time;
            decoded.FromLegacy(legacy, new Beatmap());

            Assert.That(decoded.IsEquivalentTo(source), Is.True);
            Assert.That(new DodgeRuleset().CreateConvertibleReplayFrame(), Is.TypeOf<DodgeReplayFrame>());
        }

        [Test]
        public void TestPlaybackInterpolatesPlayerPosition()
        {
            var replay = new Replay
            {
                Frames = new List<osu.Game.Rulesets.Replays.ReplayFrame>
                {
                    new DodgeReplayFrame(0, Vector2.Zero, DodgeAction.MoveRight),
                    new DodgeReplayFrame(1000, new Vector2(100, 200), DodgeAction.Slow),
                },
            };
            var handler = new DodgeFramedReplayInputHandler(replay);

            handler.SetFrameFromTime(0);
            handler.SetFrameFromTime(500);

            var inputs = new List<IInput>();
            handler.CollectPendingInputs(inputs);
            var state = inputs.OfType<DodgeFramedReplayInputHandler.DodgeReplayState>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(state.PlayerPosition, Is.EqualTo(new Vector2(50, 100)));
                Assert.That(state.PressedActions, Is.EqualTo(new[] { DodgeAction.MoveRight }));
            });
        }

        [Test]
        public void TestOsrContainerRoundTrip()
        {
            var ruleset = new DodgeRuleset();
            var beatmapInfo = new BeatmapInfo
            {
                MD5Hash = "0123456789abcdef0123456789abcdef",
                Ruleset = ruleset.RulesetInfo,
            };
            var beatmap = new Beatmap
            {
                BeatmapInfo = beatmapInfo,
                HitObjects =
                {
                    new DodgeBullet
                    {
                        StartTime = 1000,
                        Position = new Vector2(64, 64),
                        EndPosition = new Vector2(448, 320),
                    },
                },
            };
            var score = new Score
            {
                ScoreInfo = new ScoreInfo(beatmapInfo, ruleset.RulesetInfo)
                {
                    User = new APIUser { Id = 1, Username = "replay-test" },
                    Date = DateTimeOffset.UtcNow,
                    TotalScore = 1_000_000,
                    MaxCombo = 1,
                    Accuracy = 1,
                    Statistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 1 },
                    MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 1 },
                },
                Replay = new Replay
                {
                    Frames = new List<osu.Game.Rulesets.Replays.ReplayFrame>
                    {
                        new DodgeReplayFrame(0, new Vector2(100, 100), DodgeAction.MoveRight),
                        new DodgeReplayFrame(500, new Vector2(160, 100), DodgeAction.Slow),
                    },
                },
            };

            using var stream = new MemoryStream();
            new LegacyScoreEncoder(score, beatmap).Encode(stream, leaveOpen: true);
            stream.Position = 0;

            var decoded = new DodgeScoreDecoder(beatmap).Parse(stream);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.ScoreInfo.Ruleset.OnlineID, Is.EqualTo(DodgeRuleset.ONLINE_ID));
                Assert.That(decoded.Replay.Frames, Has.Count.EqualTo(2));
                Assert.That(decoded.Replay.Frames[0].IsEquivalentTo(score.Replay.Frames[0]), Is.True);
                Assert.That(decoded.Replay.Frames[1].IsEquivalentTo(score.Replay.Frames[1]), Is.True);
            });
        }

        private sealed class DodgeScoreDecoder : LegacyScoreDecoder
        {
            private readonly IBeatmap beatmap;

            public DodgeScoreDecoder(IBeatmap beatmap)
            {
                this.beatmap = beatmap;
            }

            protected override Ruleset GetRuleset(int rulesetId)
            {
                Assert.That(rulesetId, Is.EqualTo(DodgeRuleset.ONLINE_ID));
                return new DodgeRuleset();
            }

            protected override WorkingBeatmap GetBeatmap(string md5Hash)
            {
                Assert.That(md5Hash, Is.EqualTo(beatmap.BeatmapInfo.MD5Hash));
                return new TestWorkingBeatmap(beatmap);
            }
        }
    }
}
