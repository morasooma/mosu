using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Catch.Scoring;
using osu.Framework.Testing;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Catch.Tests.Mods
{
    public partial class TestSceneFruitNinjaPlayer : PlayerTestScene
    {
        protected override bool HasCustomSteps => true;
        protected override Ruleset CreatePlayerRuleset() => new CatchRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new Beatmap
        {
            BeatmapInfo = { Ruleset = ruleset },
            HitObjects =
            {
                new Fruit { StartTime = 5000, X = 256, NewCombo = true },
                new Fruit { StartTime = 10000, X = 128 },
            },
        };

        [Test]
        public void TestPlayerLoadsAndRecordsBlade()
        {
            AddStep("load Fruit Ninja player", () => LoadPlayer(new Mod[] { new CatchModFruitNinja() }));
            AddUntilStep("player loaded", () => Player.IsLoaded && Player.Alpha == 1);
            AddAssert("dedicated ninja field", () => Player.DrawableRuleset.Playfield is FruitNinjaPlayfield);
            AddAssert("Fruit Ninja health processor selected by player", () => Player.ChildrenOfType<FruitNinjaHealthProcessor>().Count() == 1);
            AddStep("move blade and press", () =>
            {
                InputManager.MoveMouseTo(Player.DrawableRuleset.Playfield.ToScreenSpace(new Vector2(123, 234)));
                InputManager.PressButton(MouseButton.Left);
            });
            AddWaitStep("record held blade", 5);
            AddStep("release blade", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("two-dimensional blade recorded", () => Player.Score.Replay.Frames.OfType<CatchReplayFrame>()
                .Any(f => f.Dashing && f.BladeY is float y && System.Math.Abs(y - 234) < 1 && System.Math.Abs(f.Position - 123) < 1));
        }
    }
}
