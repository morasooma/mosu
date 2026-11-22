using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Menu;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneLibraryLocationGame : OsuGameTestScene
    {
        private LibraryLocationOverlay overlay => Game.ChildrenOfType<LibraryLocationOverlay>().Single();
        private ScreenFooter footer => Game.ChildrenOfType<ScreenFooter>().Single();

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddUntilStep("library choice visible", () => overlay.State.Value == Visibility.Visible && overlay.CurrentScreen?.IsLoaded == true);
            AddUntilStep("footer registered", () => footer.ActiveOverlay == overlay);
        }

        [Test]
        public void TestLogoDoesNotInterceptInput()
        {
            AddAssert("logo below overlay", () => Game.ChildrenOfType<OsuLogo>().Single().Parent!.Depth > overlay.Parent!.Depth);
            AddStep("click over menu logo", () =>
            {
                InputManager.MoveMouseTo(Game.ChildrenOfType<OsuLogo>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("choice remains visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("click path input", () =>
            {
                InputManager.MoveMouseTo(overlay.ChildrenOfType<OsuTextBox>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("path input focused", () => overlay.ChildrenOfType<OsuTextBox>().Single().HasFocus);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestNavigateFromDriveToDevice(bool useParentDirectory)
        {
            OsuDirectorySelector selector = null!;
            OsuTextBox pathInput = null!;
            string drivePath = null!;

            AddStep("open directory selector", () =>
            {
                drivePath = Path.GetPathRoot(LocalStorage.GetFullPath("."))!;
                overlay.ChildrenOfType<RoundedButton>().Single(b => b.Text.Equals(LibraryLocationStrings.Browse)).TriggerClick();
                selector = overlay.ChildrenOfType<OsuDirectorySelector>().Single();
                pathInput = overlay.ChildrenOfType<OsuTextBox>().Single();
            });
            AddUntilStep("selector loaded", () => selector.IsLoaded);
            AddStep("select drive root", () => selector.CurrentPath.Value = new DirectoryInfo(drivePath));
            AddAssert("drive path shown", () => pathInput.Text == drivePath);
            AddStep("navigate to device", () =>
            {
                var breadcrumb = selector.ChildrenOfType<DirectorySelectorBreadcrumbDisplay>().Single();
                var breadcrumbItems = breadcrumb.ChildrenOfType<DirectorySelectorDirectory>().ToArray();
                var target = useParentDirectory
                    ? selector.ChildrenOfType<DirectorySelectorDirectory>().Except(breadcrumbItems).First()
                    : breadcrumbItems.First();
                target.TriggerClick();
            });
            AddAssert("device view has no directory", () => selector.CurrentPath.Value == null);
            AddAssert("stale drive path cleared", () => pathInput.Text == string.Empty);
            AddStep("select a drive again", () =>
            {
                var breadcrumb = selector.ChildrenOfType<DirectorySelectorBreadcrumbDisplay>().Single();
                selector.ChildrenOfType<DirectorySelectorDirectory>()
                        .Except(breadcrumb.ChildrenOfType<DirectorySelectorDirectory>()).First().TriggerClick();
            });
            AddAssert("selected drive path shown", () => selector.CurrentPath.Value != null
                                                        && pathInput.Text == selector.CurrentPath.Value.FullName);
            AddAssert("library choice still open", () => overlay.State.Value == Visibility.Visible);
            AddAssert("library choice not persisted", () => !Game.LocalConfig.Get<bool>(OsuSetting.ForkLibraryLocationChosen));
        }

        [Test]
        public void TestBackKeepsCurrentLibrary()
        {
            AddStep("click back", () =>
            {
                InputManager.MoveMouseTo(footer.BackButton);
                InputManager.Click(MouseButton.Left);
            });
            assertChoiceCompleted();
        }

        [Test]
        public void TestEscapeKeepsCurrentLibrary()
        {
            AddStep("press escape", () => InputManager.Key(Key.Escape));
            assertChoiceCompleted();
        }

        private void assertChoiceCompleted()
        {
            AddUntilStep("choice hidden", () => overlay.State.Value == Visibility.Hidden);
            AddAssert("choice persisted", () => Game.LocalConfig.Get<bool>(OsuSetting.ForkLibraryLocationChosen));
            AddUntilStep("first-run setup visible", () => Game.FirstRunOverlay.State.Value == Visibility.Visible);
            AddAssert("still at main menu", () => Game.ScreenStack.CurrentScreen is MainMenu);
        }

        protected override TestOsuGame CreateTestGame() => new LibraryChoiceGame(LocalStorage, API);

        private partial class LibraryChoiceGame : TestOsuGame
        {
            public LibraryChoiceGame(Storage storage, IAPIProvider api)
                : base(storage, api)
            {
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                LocalConfig.SetValue(OsuSetting.ForkLibraryLocationChosen, false);
                LocalConfig.SetValue(OsuSetting.ShowFirstRunSetup, true);
            }
        }
    }
}
