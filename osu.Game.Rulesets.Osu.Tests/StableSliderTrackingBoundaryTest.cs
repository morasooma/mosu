using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Online;
using osu.Game.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class StableSliderTrackingBoundaryTest : LegacyReplayPlaybackTestScene
    {
        private bool previousStable;

        protected override Ruleset CreateRuleset() => new OsuRuleset();
        protected override string? ExportLocation => null;

        [SetUp]
        public void SaveProfile() => previousStable = MosuServerEnvironment.UsesStableProtocol;

        [TearDown]
        public void RestoreProfile() => MosuServerEnvironment.UsesStableProtocol = previousStable;

        [Test]
        public void TestEarlyHeadDoesNotAcquireExpandedFollowRadius()
        {
            AddStep("select stable profile", () => MosuServerEnvironment.UsesStableProtocol = true);
            var origin = new Vector2(150, 150);
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 100 });
            var beatmap = new OsuBeatmap
            {
                Difficulty = new BeatmapDifficulty { CircleSize = 3.7f, OverallDifficulty = 9.2f, SliderMultiplier = 1.4, SliderTickRate = 1 },
                ControlPointInfo = controlPoints,
                BeatmapInfo = { Ruleset = CreateRuleset().RulesetInfo },
                HitObjects =
                {
                    new Slider
                    {
                        StartTime = 1500,
                        Position = origin,
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(29, 142) }, 115),
                    },
                },
            };
            var score = new Score
            {
                ScoreInfo = new ScoreInfo { Ruleset = CreateRuleset().RulesetInfo },
                Replay = new Replay
                {
                    Frames =
                    {
                        new OsuReplayFrame(0, origin),
                        new OsuReplayFrame(1400, origin),
                        new OsuReplayFrame(1497, origin, OsuAction.RightButton),
                        new OsuReplayFrame(1515, origin + new Vector2(-4, -24), OsuAction.RightButton),
                        new OsuReplayFrame(1532, origin + new Vector2(4, -3), OsuAction.RightButton),
                        new OsuReplayFrame(1546, origin + new Vector2(15, 24), OsuAction.RightButton),
                        new OsuReplayFrame(1564, origin + new Vector2(39, 63), OsuAction.RightButton),
                        new OsuReplayFrame(1583, origin + new Vector2(88, 95), OsuAction.RightButton),
                        new OsuReplayFrame(1700, origin),
                    },
                },
            };

            // The early head is hit, but none of the first three in-slider frames is
            // within the unexpanded follow radius. Tracking starts after the tail checkpoint.
            RunTest("stable early slider head tracking", beatmap, "early head with trailing cursor", score,
                new[] { HitResult.LargeTickHit, HitResult.SmallTickMiss, HitResult.Ok });
        }
    }
}
