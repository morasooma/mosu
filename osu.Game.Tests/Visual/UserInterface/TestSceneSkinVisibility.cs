using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Osu;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneSkinVisibility : OsuManualInputManagerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Test]
        public void TestPanel()
        {
            SkinVisibilitySettingsPanel panel = null!;
            AddStep("show element settings", () =>
            {
                Clear();
                Add(panel = new SkinVisibilitySettingsPanel());
                panel.Show();
            });
            AddStep("hide panel", () => panel.Hide());
            AddStep("show panel again", () => panel.Show());
        }

        [Test]
        public void TestVisibilityDoesNotOverwriteHiddenTransforms()
        {
            SkinnableDrawable element = null!;
            AddStep("create slider ball graphic", () =>
            {
                config.SetValue(OsuSetting.ForkShowSliderBall, true);
                Clear();
                Add(element = new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.SliderBall), _ => new Box())
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new osuTK.Vector2(100),
                });
            });
            AddAssert("initially present", () => element.IsPresent);
            AddStep("hide ball", () => config.SetValue(OsuSetting.ForkShowSliderBall, false));
            AddAssert("not present but still alive", () => !element.IsPresent && element.IsAlive);
            AddStep("simulate mod alpha", () => element.Alpha = 0.5f);
            AddStep("show ball", () => config.SetValue(OsuSetting.ForkShowSliderBall, true));
            AddAssert("alpha preserved", () => element.IsPresent && element.Alpha == 0.5f);
            AddStep("restore defaults", () => config.SetValue(OsuSetting.ForkShowSliderBall, true));
        }
    }
}
