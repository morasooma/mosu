using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Replays;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class StableNoteLockBoundaryTest : LegacyReplayPlaybackTestScene
    {
        private bool previousStable;

        protected override Ruleset CreateRuleset() => new OsuRuleset();
        protected override string? ExportLocation => null;

        [SetUp]
        public void SaveProfile()
        {
            previousStable = MosuServerEnvironment.UsesStableProtocol;
            MosuServerEnvironment.UsesStableProtocol = true;
        }

        [TearDown]
        public void RestoreProfile() => MosuServerEnvironment.UsesStableProtocol = previousStable;

        [TestCase(108, HitResult.Miss)]
        [TestCase(109, HitResult.Ok)]
        public void TestAutomaticMissRetainsNoteLockAtBoundary(int idleOffset, HitResult secondResult)
        {
            AddStep("select stable profile", () => MosuServerEnvironment.UsesStableProtocol = true);
            var secondPosition = new Vector2(350, 48);
            var beatmap = new OsuBeatmap
            {
                Difficulty = new BeatmapDifficulty { OverallDifficulty = 9.2f },
                BeatmapInfo = { Ruleset = CreateRuleset().RulesetInfo },
                HitObjects =
                {
                    new HitCircle { StartTime = 1000, Position = new Vector2(402, 103) },
                    new HitCircle { StartTime = 1082, Position = secondPosition },
                },
            };
            var score = new Score
            {
                ScoreInfo = new ScoreInfo { Ruleset = CreateRuleset().RulesetInfo },
                Replay = new Replay
                {
                    Frames =
                    {
                        new OsuReplayFrame(0, secondPosition),
                        new OsuReplayFrame(1000 + idleOffset, secondPosition),
                        new OsuReplayFrame(1111, secondPosition, OsuAction.LeftButton),
                        new OsuReplayFrame(1120, secondPosition),
                        new OsuReplayFrame(1200, secondPosition),
                    },
                },
            };

            // At +108 stable has not automatically missed the first circle yet. Input at
            // +111 is handled before that frame's automatic judgements and is still locked.
            // An idle frame at +109 expires it before the subsequent press instead.
            RunTest("stable automatic miss boundary", beatmap, $"idle offset {idleOffset}", score, new[] { HitResult.Miss, secondResult });
        }
    }
}
