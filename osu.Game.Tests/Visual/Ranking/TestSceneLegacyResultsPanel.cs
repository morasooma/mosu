using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Ranking
{
    [TestFixture]
    public partial class TestSceneLegacyResultsPanel : OsuTestScene
    {
        [Test]
        public void TestScoreChangesAndResize()
        {
            LegacyResultsPanel panel = null!;
            AddStep("load panel", () => Child = panel = new LegacyResultsPanel());
            AddStep("set score", () => panel.Score.Value = new ScoreInfo
            {
                Ruleset = new OsuRuleset().RulesetInfo,
                BeatmapInfo = new BeatmapInfo { Metadata = new BeatmapMetadata { Title = "Legacy result", Artist = "Test" } },
                TotalScore = 123456,
                Accuracy = 0.9876,
                MaxCombo = 321,
                Rank = ScoreRank.A,
                Statistics = { [HitResult.Great] = 100, [HitResult.Ok] = 3, [HitResult.Miss] = 1 },
            });
            AddUntilStep("score rendered", () => panel.ChildrenOfType<LegacySpriteText>().Any(t => t.Text.ToString() == "000123456" || t.Text.ToString() == "00123456"));
            AddStep("relax ruleset", () =>
            {
                var score = panel.Score.Value!;
                panel.Score.Value = null;
                score.Ruleset = score.Ruleset.CreateSpecialRuleset("osurx", 4);
                panel.Score.Value = score;
            });
            AddUntilStep("relax uses judgement columns", () => panel.ChildrenOfType<LegacySpriteText>().Any(t => t.Text.ToString() == "100x"));
            AddAssert("no debug fallback", () => !panel.ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>().Any(t => t.Text.ToString() == "Hit results"));
            AddStep("resize", () => { panel.RelativeSizeAxes = Axes.None; panel.Size = new osuTK.Vector2(1280, 720); });
            AddUntilStep("uniform scale", () => panel.CanvasScale.X == panel.CanvasScale.Y && panel.CanvasScale.X == 720f / 768);
            AddStep("clear score", () => panel.Score.Value = null);
            AddUntilStep("no stale numbers", () => !panel.ChildrenOfType<LegacySpriteText>().Any());
        }
    }
}
