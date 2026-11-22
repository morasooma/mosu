using System;
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
    public partial class TestSceneLinuxGamePriority : OsuGameTestScene
    {
        [Test]
        public void TestPlatformVisibilityAndLiveConfigBinding()
        {
            SettingsItemV2 item = null!;
            AddUntilStep("settings loaded", () => Game.Settings.IsLoaded);
            AddStep("open settings", () => Game.Settings.Show());
            AddUntilStep("priority row loaded", () =>
            {
                item = Game.Settings.ChildrenOfType<ForkPerformanceSettings>().SelectMany(s => s.ChildrenOfType<SettingsItemV2>())
                           .SingleOrDefault(i => i.FavouriteId == "OsuSetting.ForkLinuxGamePriority")!;
                return item?.IsLoaded == true;
            });
            AddAssert("only visible on Linux", () => item.CanBeShown.Value == OperatingSystem.IsLinux());
            AddStep("enable checkbox", () => ((FormCheckBox)item.Control).Current.Value = true);
            AddAssert("configuration enabled", () => Game.LocalConfig.Get<bool>(OsuSetting.ForkLinuxGamePriority));
            AddStep("disable through config", () => Game.LocalConfig.SetValue(OsuSetting.ForkLinuxGamePriority, false));
            AddAssert("checkbox follows config", () => !((FormCheckBox)item.Control).Current.Value);
            AddAssert("Linux shows status", () => !OperatingSystem.IsLinux() || item.Note.Value != null);
        }
    }
}
