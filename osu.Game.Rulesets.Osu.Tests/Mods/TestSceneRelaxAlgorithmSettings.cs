using System;
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
    public partial class TestSceneRelaxAlgorithmSettings : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [TestCase(false)]
        [TestCase(true)]
        public void TestActualDropdownReplacesSettingsWithoutGaps(bool numericInput)
        {
            var mod = new OsuModMosuRelax();
            Drawable[] controls = null!;
            ModCustomisationSection section = null!;
            SettingsEnumDropdown<MosuRelaxAlgorithm> dropdown = null!;
            float mosuHeight = 0;
            AddStep("load real MRX settings", () =>
            {
                controls = mod.CreateSettingsControls(numericInput).ToArray();
                dropdown = (SettingsEnumDropdown<MosuRelaxAlgorithm>)controls[0];
                Child = section = new ModCustomisationSection(mod, controls) { Width = 600 };
            });
            AddUntilStep("Mosu settings visible", () => section.IsLoaded && controls[1].DrawHeight > 0 && section.DrawHeight > 0);
            AddStep("choose Maple using dropdown", () =>
            {
                mosuHeight = section.DrawHeight;
                dropdown.Current.Value = MosuRelaxAlgorithm.Maple;
            });
            AddUntilStep("Maple settings visible", () => !controls[1].IsPresent && controls[^1].IsPresent);
            AddAssert("Mosu preset hidden", () => !controls[1].IsPresent);
            AddAssert("no gaps from inactive settings", () =>
            {
                Drawable[] visible = controls.Where(c => c.IsPresent).ToArray();
                return visible.Zip(visible.Skip(1), (a, b) => b.Y - a.Y - a.DrawHeight).All(g => g >= 7.9f && g <= 8.1f);
            });
            AddStep("restore Mosu", () => dropdown.Current.Value = MosuRelaxAlgorithm.Mosu);
            AddUntilStep("original layout restored", () => Math.Abs(section.DrawHeight - mosuHeight) < 0.01f && controls[1].IsPresent);
        }
    }
}
