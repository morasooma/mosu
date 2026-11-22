using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneMosuModCaptions : OsuTestScene
    {
        [TestCase(0.8f)]
        [TestCase(1f)]
        [TestCase(1.5f)]
        public void TestCaptionSizeAndBounds(float scale)
        {
            ModSwitchSmall small = null!;
            foreach (Mod mod in new Mod[] { new OsuModFlashlight(), new OsuModDoubleTime(), new OsuModHardRock(), new OsuModNightcore(), new OsuModMosuAntiAimAssist(), new OsuModMosuMalevich() })
            {
                AddStep($"load {mod.Acronym}", () => Child = small = new ModSwitchSmall(mod) { Scale = new Vector2(scale) });
                AddUntilStep("loaded", () => small.IsLoaded);
                AddAssert("caption fits its pill and remains at least ten logical pixels", () =>
                {
                    var pill = small.ChildrenOfType<ModSwitchTiny>().Single();
                    var text = pill.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString() == mod.Acronym);
                    return text.Font.Size * pill.Scale.Y >= 10
                        && text.Font.Family == OsuFont.Default.Family
                        && text.DrawWidth <= ModSwitchTiny.WIDTH - 4
                        && text.DrawHeight <= ModSwitchTiny.DEFAULT_HEIGHT
                        && text.ScreenSpaceDrawQuad.TopLeft.X >= pill.ScreenSpaceDrawQuad.TopLeft.X
                        && text.ScreenSpaceDrawQuad.BottomRight.X <= pill.ScreenSpaceDrawQuad.BottomRight.X;
                });
                AddStep("select", () => small.Active.Value = true);
                AddAssert("caption remains inside small badge", () =>
                {
                    var pill = small.ChildrenOfType<ModSwitchTiny>().Single();
                    return pill.ScreenSpaceDrawQuad.BottomRight.Y <= small.ScreenSpaceDrawQuad.BottomRight.Y
                        && pill.ScreenSpaceDrawQuad.TopLeft.Y >= small.ScreenSpaceDrawQuad.TopLeft.Y;
                });
            }
        }

        [Test]
        public void TestStandaloneNumericPillUnchanged()
        {
            ModSwitchTiny pill = null!;
            AddStep("standalone", () => Child = pill = new ModSwitchTiny(new OsuModFlashlight()));
            AddUntilStep("loaded", () => pill.IsLoaded);
            AddAssert("original numeric font and size", () =>
            {
                var text = pill.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString() == "FL");
                return text.Font.Family == OsuFont.Numeric.Family && text.Font.Size == 24;
            });
        }
    }
}
