using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [HeadlessTest]
    public partial class TestSceneAimAssistAlgorithmSettings : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [TestCase(false)]
        [TestCase(true)]
        public void TestActualModSectionCollapsesInactiveSettings(bool numericInput)
        {
            var mod = new OsuModMosuAimAssist();
            Drawable[] controls = null!;
            ModCustomisationSection section = null!;
            SettingsEnumDropdown<MosuAimAssistAlgorithm> dropdown = null!;
            float mosuHeight = 0;
            AddStep("load real mod customisation section", () =>
            {
                controls = mod.CreateSettingsControls(numericInput).ToArray();
                dropdown = (SettingsEnumDropdown<MosuAimAssistAlgorithm>)controls[0];
                Child = section = new ModCustomisationSection(mod, controls) { Width = 500 };
            });
            AddUntilStep("Mosu settings laid out", () => section.IsLoaded && controls[1].DrawHeight > 0 && section.DrawHeight > 0);
            AddAssert("algorithm, Mosu strength and both shared toggles visible", () => controls.Count(c => c.IsPresent) == (numericInput ? 5 : 4));
            AddStep("remember Mosu layout and select V2 via dropdown", () =>
            {
                mosuHeight = section.DrawHeight;
                dropdown.Current.Value = MosuAimAssistAlgorithm.MapleV2;
            });
            AddUntilStep("V2 controls replace Mosu controls", () => controls.Count(c => c.IsPresent) == (numericInput ? 6 : 5));
            AddAssert("two V2 settings use more space than one Mosu setting", () => section.DrawHeight > mosuHeight);
            AddAssert("no hidden controls leave a vertical gap", () =>
            {
                Drawable[] visible = controls.Where(c => c.IsPresent).ToArray();
                return visible.Zip(visible.Skip(1), (first, next) => next.Y - (first.Y + first.DrawHeight))
                              .All(gap => gap >= 7.9f && gap <= 8.1f);
            });
            AddStep("select V3 via dropdown", () => dropdown.Current.Value = MosuAimAssistAlgorithm.MapleV3);
            AddUntilStep("V3 controls replace V2 controls", () => controls.Count(c => c.IsPresent) == (numericInput ? 7 : 5));
            AddStep("switch back to Mosu", () => dropdown.Current.Value = MosuAimAssistAlgorithm.Mosu);
            AddUntilStep("original height restored without dead rows", () => System.Math.Abs(section.DrawHeight - mosuHeight) < 0.01f);
        }
    }
}
