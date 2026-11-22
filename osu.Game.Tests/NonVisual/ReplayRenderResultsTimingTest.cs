using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Render;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.NonVisual
{
    [HeadlessTest]
    public partial class ReplayRenderResultsTimingTest : OsuTestScene
    {
        [TestCase(ScoreRank.S, 0.99)]
        [TestCase(ScoreRank.X, 1)]
        public void TestResultsAnimationsFinishBeforeCaptureEnds(ScoreRank rank, double accuracy)
        {
            var clock = new ManualClock();
            RenderResultsScreen results = null!;
            AccuracyCircle circle = null!;
            ForkSongSelectStyle originalStyle = default;
            var config = Dependencies.Get<OsuConfigManager>();
            var score = new ScoreInfo(Beatmap.Value.BeatmapInfo, new OsuRuleset().RulesetInfo)
            {
                User = new APIUser { Id = 22, Username = "Rendered player" },
                Rank = rank,
                Accuracy = accuracy,
                TotalScore = 1000000,
            };

            AddStep("load animated render results", () =>
            {
                originalStyle = config.Get<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle);
                config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern);
                var stack = new OsuScreenStack { RelativeSizeAxes = Axes.Both };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(clock),
                    // ResultsScreen uses the originating player to enable entry animations.
                    CachedDependencies = [(typeof(Player), new ReplayPlayer(new Score { ScoreInfo = score, Replay = new Replay() }))],
                    Child = stack,
                };
                stack.PushSynchronously(results = new RenderResultsScreen(score)
                {
                    AllowRetry = false,
                    AllowWatchingReplay = false,
                    IsLocalPlay = true,
                });
            });
            AddUntilStep("accuracy animation loaded", () =>
            {
                // Panels fade in before their expanded content starts updating.
                clock.CurrentTime = System.Math.Min(1000, clock.CurrentTime + 16);
                circle = results.ChildrenOfType<AccuracyCircle>().SingleOrDefault()!;
                return circle?.IsLoaded == true && circle.ChildrenOfType<RankText>().Any();
            });
            AddStep("advance to old cutoff", () => clock.CurrentTime = 2500);
            AddWaitStep("update animations", 2);
            AddAssert("old cutoff truncates rank animation", () => circle.ChildrenOfType<Drawable>()
                .SelectMany(d => d.Transforms).Any(t => !t.IsLooping && t.EndTime > clock.CurrentTime));
            AddStep("advance to new cutoff", () => clock.CurrentTime = RenderResultsScreen.DISPLAY_DURATION);
            AddWaitStep("finish entry animations", 2);
            AddAssert("rank animation has completed", () => circle.ChildrenOfType<Drawable>()
                .SelectMany(d => d.Transforms).All(t => t.IsLooping || t.EndTime <= clock.CurrentTime));
            AddAssert("time remains to read results", () => RenderResultsScreen.DISPLAY_DURATION,
                () => Is.GreaterThanOrEqualTo(AccuracyCircle.ACCURACY_TRANSFORM_DELAY + AccuracyCircle.TEXT_APPEAR_DELAY + 3000 + 2000));
            AddStep("restore results style", () => config.SetValue(OsuSetting.ForkSongSelectStyle, originalStyle));
        }
    }
}
