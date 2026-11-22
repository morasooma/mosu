using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Fork;

namespace osu.Game.Tests.Visual.Navigation
{
    [HeadlessTest]
    public partial class TestSceneAiSettingsNavigation : OsuGameTestScene
    {
        protected override TestOsuGame CreateTestGame() => new AiTestGame(LocalStorage, API);

        private partial class AiTestGame : TestOsuGame
        {
            public AiTestGame(osu.Framework.Platform.Storage storage, osu.Game.Online.API.IAPIProvider api) : base(storage, api) { }
            protected override void LoadComplete()
            {
                base.LoadComplete();
                LocalConfig.SetValue(osu.Game.Configuration.OsuSetting.ForkLibraryLocationChosen, true);
            }
        }

        [Test]
        public void TestAiButtonsUseOneRegisteredSubPanel()
        {
            ForkAiSettings entries = null!;
            AiFunctionSettingsPanel shared = null!;
            AddStep("open main settings", () => Game.Settings.Show());
            AddUntilStep("AI entry points loaded", () =>
            {
                entries = Game.Settings.ChildrenOfType<ForkAiSettings>().FirstOrDefault()!;
                return entries?.IsLoaded == true;
            });
            AddStep("open distortion via registered button", () => entries.ChildrenOfType<SettingsButtonV2>().ElementAt(1).Action.Invoke());
            AddUntilStep("registered subpanel selected distortion", () =>
            {
                shared = Game.Settings.ChildrenOfType<AiFunctionSettingsPanel>().Single();
                return shared.State.Value == Visibility.Visible && shared.SectionsContainer.SelectedSection.Value
                    is AiFunctionSettingsPanel.DistortionSection;
            });
            AddStep("close subpanel", () => shared.Hide());
            AddStep("open NSFW via other registered button", () => entries.ChildrenOfType<SettingsButtonV2>().First().Action.Invoke());
            AddUntilStep("same registered subpanel selected NSFW", () => shared.State.Value == Visibility.Visible
                && shared.SectionsContainer.SelectedSection.Value is AiFunctionSettingsPanel.NsfwSection);
            AddStep("open audio via registered button", () => entries.ChildrenOfType<SettingsButtonV2>().Last().Action.Invoke());
            AddUntilStep("same registered subpanel selected audio", () => shared.State.Value == Visibility.Visible
                && shared.SectionsContainer.SelectedSection.Value is AiFunctionSettingsPanel.AudioSection);
            AddAssert("main panel registered only one AI subpanel", () => Game.Settings.ChildrenOfType<AiFunctionSettingsPanel>().Count() == 1);
        }
    }
}
