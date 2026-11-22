using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Online;
using osu.Game.Replays;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class StableRelaxReplayTest : RateAdjustedBeatmapTestScene
    {
        private bool previousStable;

        [SetUp]
        public void SaveProfile() => previousStable = MosuServerEnvironment.UsesStableProtocol;

        [TearDown]
        public void RestoreProfile() => MosuServerEnvironment.UsesStableProtocol = previousStable;

        [TestCase(20150414)]
        [TestCase(20260924)]
        public void TestRelaxDoesNotHitFrozenCursorBetweenRecordedFrames(int version)
        {
            var origin = new Vector2(267, 88);
            var beatmap = createBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = 2000, Position = origin });
            var replay = new Replay
            {
                LegacyClientVersion = version,
                Frames =
                {
                    new OsuReplayFrame(0, Vector2.Zero),
                    // The cursor is inside too early, then outside on both eligible
                    // frames. Relax must wait for the in-radius frame at +28ms.
                    new OsuReplayFrame(1965, origin + new Vector2(4.6667f, -16.33334f)),
                    new OsuReplayFrame(1990, origin + new Vector2(15.3333f, -34.66667f)),
                    new OsuReplayFrame(2015, origin + new Vector2(19.6667f, -31.66667f)),
                    new OsuReplayFrame(2028, origin + new Vector2(24.3333f, -15)),
                    new OsuReplayFrame(2200, Vector2.Zero),
                },
            };
            runReplay(beatmap, replay, true, new[] { HitResult.Ok });
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void TestKeylessSliderStillRequiresCursorTracking(bool relax, bool leaveFollowArea)
        {
            var origin = new Vector2(150, 150);
            var beatmap = createBeatmap();
            beatmap.HitObjects.Add(new Slider
            {
                StartTime = 1000,
                Position = origin,
                RepeatCount = 1,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(140, 0) }, 140),
            });
            Vector2 tracked(float x) => leaveFollowArea ? new Vector2(450, 350) : origin + new Vector2(x, 0);
            var replay = new Replay
            {
                LegacyClientVersion = 20150414,
                Frames =
                {
                    new OsuReplayFrame(0, Vector2.Zero),
                    new OsuReplayFrame(990, origin),
                    new OsuReplayFrame(1000, origin),
                    new OsuReplayFrame(1050, tracked(70)),
                    new OsuReplayFrame(1100, tracked(140)),
                    new OsuReplayFrame(1150, tracked(70)),
                    new OsuReplayFrame(1164, tracked(50)),
                    new OsuReplayFrame(1180, tracked(28)),
                    new OsuReplayFrame(1250, Vector2.Zero),
                },
            };
            var expected = !relax
                ? new[] { HitResult.LargeTickMiss, HitResult.LargeTickMiss, HitResult.SmallTickMiss, HitResult.Miss }
                : leaveFollowArea
                    ? new[] { HitResult.LargeTickHit, HitResult.LargeTickMiss, HitResult.SmallTickMiss, HitResult.Meh }
                    : new[] { HitResult.LargeTickHit, HitResult.LargeTickHit, HitResult.SmallTickHit, HitResult.Great };
            runReplay(beatmap, replay, relax, expected);
        }

        [TestCase(20150414)]
        [TestCase(20260924)]
        public void TestSimultaneousPressUsesLeftKeyPriority(int version)
        {
            var origin = new Vector2(150, 150);
            var beatmap = createBeatmap();
            beatmap.HitObjects.Add(new Slider
            {
                StartTime = 1000,
                Position = origin,
                RepeatCount = 1,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(140, 0) }, 140),
            });
            var replay = new Replay
            {
                LegacyClientVersion = version,
                Frames =
                {
                    new OsuReplayFrame(0, origin),
                    new OsuReplayFrame(1000, origin, OsuAction.LeftButton, OsuAction.RightButton),
                    new OsuReplayFrame(1050, origin + new Vector2(70, 0), OsuAction.LeftButton),
                    new OsuReplayFrame(1100, origin + new Vector2(140, 0), OsuAction.LeftButton),
                    new OsuReplayFrame(1164, origin + new Vector2(50, 0), OsuAction.LeftButton),
                    new OsuReplayFrame(1250, origin),
                },
            };
            runReplay(beatmap, replay, false, new[] { HitResult.LargeTickHit, HitResult.LargeTickHit, HitResult.SmallTickHit, HitResult.Great });
        }

        [TestCase(20150414)]
        [TestCase(20260924)]
        public void TestFractionalRepeatUsesTruncatedCheckpoint(int version)
        {
            var origin = new Vector2(150, 150);
            var beatmap = createBeatmap();
            beatmap.HitObjects.Add(new Slider
            {
                StartTime = 1000,
                Position = origin,
                RepeatCount = 1,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(140.7f, 0) }, 140.7),
            });
            var replay = new Replay
            {
                LegacyClientVersion = version,
                Frames =
                {
                    new OsuReplayFrame(0, origin),
                    new OsuReplayFrame(1000, origin, OsuAction.LeftButton),
                    new OsuReplayFrame(1064, origin + new Vector2(90, 0), OsuAction.LeftButton),
                    // Repeat is at 1100.5. Stable checks it on the 1100 frame,
                    // before the key is released at 1101, rather than interpolating.
                    new OsuReplayFrame(1100, origin + new Vector2(140.7f, 0), OsuAction.LeftButton),
                    new OsuReplayFrame(1101, origin + new Vector2(140.7f, 0)),
                    new OsuReplayFrame(1250, origin),
                },
            };
            runReplay(beatmap, replay, false, new[] { HitResult.LargeTickHit, HitResult.LargeTickHit, HitResult.SmallTickMiss, HitResult.Ok });
        }

        [TestCase(20150414, 1064, true)]
        [TestCase(20150414, 1065, false)]
        [TestCase(20260924, 1064, true)]
        [TestCase(20260924, 1065, false)]
        public void TestFractionalTailCannotRecoverAfterCheckpoint(int version, int acquireTime, bool hit)
        {
            var origin = new Vector2(150, 150);
            var beatmap = createBeatmap();
            beatmap.HitObjects.Add(new Slider
            {
                StartTime = 1000,
                Position = origin,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(140.7f, 0) }, 140.7),
            });
            var replay = new Replay
            {
                LegacyClientVersion = version,
                Frames =
                {
                    new OsuReplayFrame(0, origin),
                    new OsuReplayFrame(1000, origin, OsuAction.LeftButton),
                    new OsuReplayFrame(1030, new Vector2(450, 350), OsuAction.LeftButton),
                    new OsuReplayFrame(acquireTime, origin + new Vector2(90, 0), OsuAction.LeftButton),
                    new OsuReplayFrame(1101, origin + new Vector2(140.7f, 0), OsuAction.LeftButton),
                    new OsuReplayFrame(1250, origin),
                },
            };
            runReplay(beatmap, replay, false, new[] { HitResult.LargeTickHit, hit ? HitResult.SmallTickHit : HitResult.SmallTickMiss, hit ? HitResult.Great : HitResult.Ok });
        }

        private static OsuBeatmap createBeatmap()
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 100 });
            return new OsuBeatmap
            {
                Difficulty = new BeatmapDifficulty { CircleSize = 4, OverallDifficulty = 9.5f, SliderMultiplier = 1.4, SliderTickRate = 1 },
                ControlPointInfo = controlPoints,
            };
        }

        private void runReplay(OsuBeatmap beatmap, Replay replay, bool relax, HitResult[] expected)
        {
            ReplayPlayer player = null!;
            var results = new List<JudgementResult>();
            AddStep("load stable replay", () =>
            {
                MosuServerEnvironment.UsesStableProtocol = true;
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                beatmap.BeatmapInfo.Ruleset = Ruleset.Value;
                Beatmap.Value = CreateWorkingBeatmap(beatmap);
                Mod[] mods = relax ? new Mod[] { new OsuModClassic(), new OsuModRelax() } : new Mod[] { new OsuModClassic() };
                SelectedMods.Value = mods;
                var score = new Score
                {
                    Replay = replay,
                    ScoreInfo = new ScoreInfo { Ruleset = Ruleset.Value, IsLegacyScore = true, Mods = mods },
                };
                player = new ReplayPlayer(score);
                player.OnLoadComplete += _ =>
                {
                    player.GameplayState.ScoreProcessor.ApplyNewJudgementsWhenFailed = true;
                    player.GameplayState.ScoreProcessor.NewJudgement += results.Add;
                };
                LoadScreen(player);
            });
            AddUntilStep("player loaded", () => player.IsCurrentScreen());
            AddUntilStep("replay completed", () => player.GameplayState.HasCompleted);
            AddAssert("calculated judgements", () => results.Select(r => r.Type), () => Is.EquivalentTo(expected));
        }
    }
}
