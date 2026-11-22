using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Overlays.Settings.Sections.Fork;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>Historical .morafx data remains importable; its editor is no longer part of settings.</summary>
    [HeadlessTest]
    public partial class TestSceneInterfaceSounds : OsuGameTestScene
    {
        [Test]
        public void TestLegacyFileImportWithoutRestoringSoundEditor()
        {
            Task? operation = null;
            InterfaceSoundManager manager = null!;
            AddUntilStep("settings loaded", () => Game.Settings.IsLoaded);
            AddStep("import old sound pack through global file handling", () =>
            {
                manager = Game.Dependencies.Get<InterfaceSoundManager>();
                operation = Task.Run(async () =>
                {
                    string path = await manager.Export();
                    string uppercase = Path.ChangeExtension(path, ".MORAFX");
                    File.Move(path, uppercase);
                    await Game.Import(uppercase);
                });
            });
            AddUntilStep("legacy import finished", () => operation?.IsCompleted == true);
            AddAssert("custom recordings preserved", () =>
            {
                operation!.GetAwaiter().GetResult();
                return manager.SelectedId != InterfaceSoundLibrary.DefaultPack
                       && manager.Library.Find(manager.SelectedId)?.Sounds.Count == manager.Events.Count;
            });
            AddAssert("no sound editor mounted", () => !Game.Settings.ChildrenOfType<InterfaceSoundSettingsPanel>().Any()
                                                                    && !Game.Settings.ChildrenOfType<SoundSettingsPanel>().Any());
        }
    }
}
