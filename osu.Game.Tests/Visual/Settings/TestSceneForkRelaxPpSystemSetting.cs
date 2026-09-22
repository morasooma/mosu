// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Tests.Visual.Navigation;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestSceneForkRelaxPpSystemSetting : OsuGameTestScene
    {
        [Test]
        public void TestDropdownChangesConfigInGameSettings()
        {
            FormEnumDropdown<ForkRelaxPpSystem> dropdown = null!;
            Menu menu = null!;

            AddStep("reset PP system", () => Game.LocalConfig.SetValue(OsuSetting.ForkRelaxPpSystem, ForkRelaxPpSystem.MosuRealistik));
            AddStep("open settings", () => Game.Settings.Show());
            AddUntilStep("settings loaded", () => Game.Settings.SectionsContainer?.Children.Count > 0);
            AddStep("filter PP setting", () => Game.Settings.SectionsContainer
                                                           .ChildrenOfType<SettingsSearchTextBox>()
                                                           .Single()
                                                           .Current.Value = "Relax PP system");
            AddUntilStep("dropdown loaded", () =>
            {
                dropdown = Game.Settings.ChildrenOfType<FormEnumDropdown<ForkRelaxPpSystem>>().SingleOrDefault()!;
                return dropdown?.IsLoaded == true && dropdown.IsPresent;
            });
            AddStep("scroll to dropdown", () => Game.Settings.SectionsContainer.ScrollTo(dropdown));
            AddUntilStep("dropdown visible", () => dropdown.IsPresent && dropdown.Alpha > 0);
            AddStep("open dropdown", () =>
            {
                InputManager.MoveMouseTo(dropdown.ChildrenOfType<DropdownHeader>().Single());
                InputManager.Click(MouseButton.Left);
                menu = dropdown.ChildrenOfType<Menu>().Single();
            });
            AddUntilStep("dropdown open", () => menu.State == MenuState.Open);
            AddStep("select lazer vanilla", () =>
            {
                InputManager.MoveMouseTo(menu.ChildrenOfType<Menu.DrawableMenuItem>().ElementAt(1));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("config changed", () => Game.LocalConfig.Get<ForkRelaxPpSystem>(OsuSetting.ForkRelaxPpSystem) == ForkRelaxPpSystem.LazerVanilla);
            AddUntilStep("calculator system changed", () => RelaxPpSystemSelection.Current == ForkRelaxPpSystem.LazerVanilla);
        }
    }
}
