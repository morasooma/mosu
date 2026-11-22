using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Fork;

namespace osu.Game.Tests.Visual.Settings
{
    [HeadlessTest]
    public partial class TestSceneManiaEditorSettings : OsuGameTestScene
    {
        [Test]
        public void TestAutoScrollCheckbox()
        {
            FormCheckBox checkbox = null!;
            AddUntilStep("settings loaded", () => Game.Settings.IsLoaded);
            AddStep("open settings", () => Game.Settings.Show());
            AddUntilStep("find mania auto-scroll in Morasooma settings", () =>
            {
                checkbox = Game.Settings.ChildrenOfType<ForkSettings>().SelectMany(section => section.ChildrenOfType<SettingsItemV2>())
                               .SingleOrDefault(item => item.FavouriteId == "OsuSetting.ForkManiaEditorAutoSeekOnPlacement")?.Control as FormCheckBox ?? null!;
                return checkbox?.IsLoaded == true;
            });
            AddAssert("off by default", () => !checkbox.Current.Value);
            AddStep("enable auto-scroll", () => checkbox.Current.Value = true);
            AddAssert("mania setting enabled", () => Game.LocalConfig.Get<bool>(OsuSetting.ForkManiaEditorAutoSeekOnPlacement));
            AddStep("disable through config", () => Game.LocalConfig.SetValue(OsuSetting.ForkManiaEditorAutoSeekOnPlacement, false));
            AddAssert("checkbox follows config", () => !checkbox.Current.Value);
        }
    }
}
