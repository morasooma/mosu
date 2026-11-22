using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Fork;

namespace osu.Game.Tests.Visual.Settings
{
    [HeadlessTest]
    public partial class TestSceneGameResources : OsuGameTestScene
    {
        [Test]
        public void TestDancIsNotExtractedButPersonalFontsRemainSupported()
        {
            byte[]? personalDefinition = null;
            byte[]? personalPage = null;
            AddStep("check fresh profile has no bundled danc", () =>
            {
                var storage = Game.Storage.GetStorageForDirectory("Fonts");
                Assert.That(storage.Exists("danc.fnt"), Is.False);
                Assert.That(storage.Exists("danc.png"), Is.False);
                Assert.That(storage.Exists("danc.fnt_0.png"), Is.False);
                Assert.That(OsuGameBase.AvailableCustomUIFonts, Does.Not.Contain("danc.fnt"));
            });
            AddStep("install an unrelated personal font using the same filename", () =>
            {
                var storage = Game.Storage.GetStorageForDirectory("Fonts");
                using (var input = storage.GetStream("Comfortaa Regular.fnt"))
                using (var copy = new MemoryStream())
                {
                    input.CopyTo(copy);
                    personalDefinition = copy.ToArray();
                }
                using (var input = storage.GetStream("Comfortaa Regular.fnt_0.png"))
                using (var copy = new MemoryStream())
                {
                    input.CopyTo(copy);
                    personalPage = copy.ToArray();
                }
                using (var output = storage.GetStream("danc.fnt", FileAccess.Write, FileMode.Create))
                    output.Write(personalDefinition);
                using (var output = storage.GetStream("danc.fnt_0.png", FileAccess.Write, FileMode.Create))
                    output.Write(personalPage);
                Game.RefreshCustomFonts();
            });
            AddAssert("personal font filename is not blocked", () => OsuGameBase.AvailableCustomUIFonts.Contains("danc.fnt"));
            AddStep("restart isolated client with the personal font", () =>
            {
                Remove(Game, true);
                CreateGame();
            });
            AddUntilStep("personal-font profile loaded", () => Game.IsLoaded);
            AddStep("check personal files are not deleted or replaced", () =>
            {
                var storage = Game.Storage.GetStorageForDirectory("Fonts");
                using (var input = storage.GetStream("danc.fnt"))
                using (var copy = new MemoryStream())
                {
                    input.CopyTo(copy);
                    Assert.That(copy.ToArray(), Is.EqualTo(personalDefinition));
                }
                using (var input = storage.GetStream("danc.fnt_0.png"))
                using (var copy = new MemoryStream())
                {
                    input.CopyTo(copy);
                    Assert.That(copy.ToArray(), Is.EqualTo(personalPage));
                }
                Assert.That(OsuGameBase.AvailableCustomUIFonts.Count(f => f == "danc.fnt"), Is.EqualTo(1));
                Assert.That(storage.Exists("danc.png"), Is.False);
            });
            AddStep("release isolated personal font fixture", () => OsuGameBase.AvailableCustomUIFonts.RemoveAll(f => f == "danc.fnt"));
        }

        [Test]
        public void TestFontPackGlobalImportPreservesOriginalMetrics()
        {
            Task? operation = null;
            osu.Game.Graphics.Fonts.FontPackLibrary.Validated pack = null!;
            string? requested = Environment.GetEnvironmentVariable("MOSU_TEST_ORIGINAL_FONTS");
            AddStep("prepare font archive in isolated storage", () => operation = Task.Run(() =>
            {
                string target = LocalStorage.GetFullPath("comparison.MORAFN");
                using (var output = File.Create(target))
                {
                    if (!string.IsNullOrEmpty(requested))
                    {
                        using var original = File.OpenRead(requested);
                        original.CopyTo(output);
                    }
                    else osu.Game.Graphics.Fonts.FontPackExporter.Export(output,
                        new osu.Game.Graphics.Fonts.FontConfiguration { Separate = false, Common = "Inter" },
                        LocalStorage, Array.Empty<osu.Game.Graphics.Fonts.FontPackLibrary.Validated>());
                }
                using var source = File.OpenRead(target);
                pack = osu.Game.Graphics.Fonts.FontPackLibrary.Validate(source);
            }));
            AddUntilStep("archive prepared and validated", () => operation?.IsCompleted == true);
            AddStep("import through uppercase extension handler", () =>
            {
                operation!.GetAwaiter().GetResult();
                operation = Task.Run(() => Game.Import(LocalStorage.GetFullPath("comparison.MORAFN")));
            });
            AddUntilStep("font file imported", () => operation?.IsCompleted == true);
            AddStep("check selected families and original glyph metrics", () =>
            {
                operation!.GetAwaiter().GetResult();
                Assert.That(osu.Game.Graphics.Fonts.FontConfiguration.Capture(Game.LocalConfig).Main,
                    Is.EqualTo(pack.InstalledSettings().Main));
                operation = Task.Run(() =>
                {
                    foreach (var family in pack.Manifest.Families)
                    {
                        if (!string.IsNullOrEmpty(requested))
                            Assert.That(Game.FontPackDisplayName(pack.InstalledFamily(family.Name)), Does.StartWith(family.Name + " · "));
                        foreach (var face in family.Faces)
                        {
                            using var descriptor = new MemoryStream(pack.Files[face.Asset], false);
                            var raw = SharpFNT.BitmapFont.FromStream(descriptor, SharpFNT.FormatHint.Binary, false);
                            char character = raw.Characters.ContainsKey('H') ? 'H' : '3';
                            var glyph = Game.Fonts.Get(pack.InstalledFamily(family.Name) + "-" + face.Weight, character);
                            Assert.That(glyph, Is.Not.Null, family.Name + " " + face.Weight);
                            Assert.That(glyph!.Baseline, Is.EqualTo(raw.Common.Base / 100f).Within(0.00001));
                            Assert.That(glyph.XAdvance, Is.EqualTo(raw.Characters[character].XAdvance / 100f).Within(0.00001));
                        }
                    }
                });
            });
            AddUntilStep("all font faces checked", () => operation?.IsCompleted == true);
            AddStep("propagate glyph assertions", () => operation!.GetAwaiter().GetResult());
            AddStep("restart isolated game with original fonts installed", () =>
            {
                Remove(Game, true);
                CreateGame();
            });
            AddUntilStep("font pack and settings available after restart", () => Game.IsLoaded && Game.Settings.IsLoaded);
            AddStep("check persistent role settings and original metrics", () =>
            {
                foreach (var family in pack.Manifest.Families)
                    Assert.That(OsuGameBase.AvailableCustomUIFonts.Count(f => f == pack.InstalledFamily(family.Name)), Is.EqualTo(1), "Restart must not duplicate dropdown values");
                var actual = osu.Game.Graphics.Fonts.FontConfiguration.Capture(Game.LocalConfig);
                var expected = pack.InstalledSettings();
                Assert.That(actual.Separate, Is.EqualTo(expected.Separate));
                Assert.That(actual.Main, Is.EqualTo(expected.Main));
                Assert.That(actual.Alternate, Is.EqualTo(expected.Alternate));
                Assert.That(actual.Numeric, Is.EqualTo(expected.Numeric));
                Assert.That(actual.Inter, Is.EqualTo(expected.Inter));
                operation = Task.Run(() =>
                {
                    foreach (var family in pack.Manifest.Families)
                    foreach (var face in family.Faces)
                    {
                        using var descriptor = new MemoryStream(pack.Files[face.Asset], false);
                        var raw = SharpFNT.BitmapFont.FromStream(descriptor, SharpFNT.FormatHint.Binary, false);
                        char character = raw.Characters.ContainsKey('H') ? 'H' : '3';
                        var glyph = Game.Fonts.Get(pack.InstalledFamily(family.Name) + "-" + face.Weight, character);
                        Assert.That(glyph, Is.Not.Null, family.Name + " " + face.Weight);
                        Assert.That(glyph!.Baseline, Is.EqualTo(raw.Common.Base / 100f).Within(0.00001));
                        Assert.That(glyph.XAdvance, Is.EqualTo(raw.Characters[character].XAdvance / 100f).Within(0.00001));
                    }
                });
            });
            AddUntilStep("all installed faces checked after restart", () => operation?.IsCompleted == true);
            AddStep("propagate restart assertions", () => operation!.GetAwaiter().GetResult());
        }

        [Test]
        public void TestRealSettingsExportAndFileOpenImport()
        {
            Task<string?>? export = null;
            Task? import = null;
            string? path = null;
            string selectedBefore = "";
            GameResourceSettings controls = null!;
            AddUntilStep("settings loaded", () => Game.Settings.IsLoaded);
            AddStep("show settings", () => Game.Settings.Show());
            AddUntilStep("resource controls loaded", () =>
            {
                controls = Game.Settings.SectionsContainer.Children.OfType<ForkSection>().SingleOrDefault()
                               ?.ChildrenOfType<GameResourceSettings>().SingleOrDefault()!;
                return controls?.IsLoaded == true;
            });
            AddAssert("no sound editor or sound settings remain mounted", () =>
                !Game.Settings.ChildrenOfType<InterfaceSoundSettingsPanel>().Any()
                && !Game.Settings.ChildrenOfType<SoundSettingsPanel>().Any());
            AddAssert("one export and no import or edit button", () =>
                controls.ChildrenOfType<SettingsButtonV2>().Count() == 1
                && controls.ChildrenOfType<SettingsButtonV2>().Single().Text.Equals(osu.Game.Localisation.GameResourceStrings.Export));
            AddStep("start full resource export", () => export = Game.ExportResourcePack());
            AddUntilStep("full export finished", () => export?.IsCompleted == true);
            AddAssert("export succeeded", () => (path = export!.GetAwaiter().GetResult()) != null && File.Exists(path));
            AddStep("inspect actual exported file", () =>
            {
                using var file = File.OpenRead(path!);
                using var zip = new ZipArchive(file, ZipArchiveMode.Read);
                Assert.That(zip.Entries.Any(e => e.FullName.StartsWith("Shaders/", StringComparison.Ordinal)
                                                 || e.FullName.StartsWith("ShaderOverrides/", StringComparison.Ordinal)), Is.False);
                Assert.That(zip.GetEntry("fonts.morafn"), Is.Not.Null);
                Assert.That(zip.GetEntry("Notices/Morasooma/LICENSE.txt"), Is.Not.Null);
                Assert.That(zip.GetEntry("Notices/Morasooma/Licenses/Icons/Phosphor-LICENSE.txt"), Is.Not.Null);
                Assert.That(zip.GetEntry("Skins/Morasooma/skin.ini"), Is.Not.Null);
                Assert.That(zip.Entries.Count, Is.GreaterThan(1500));
                TestContext.Progress.WriteLine("MORARS_FULL_EXPORT=" + path + " ENTRIES=" + zip.Entries.Count);
                string? requestedExport = Environment.GetEnvironmentVariable("MOSU_TEST_RESOURCE_EXPORT");
                if (!string.IsNullOrEmpty(requestedExport))
                {
                    // An explicitly requested inspection artifact, never a live profile.
                    Directory.CreateDirectory(Path.GetDirectoryName(requestedExport)!);
                    File.Copy(path!, requestedExport, overwrite: false);
                }
            });
            AddStep("select a personal sound after the default snapshot", () =>
            {
                var sounds = Game.Dependencies.Get<osu.Game.Audio.InterfaceSoundManager>();
                using var wave = new MemoryStream(osu.Game.Tests.NonVisual.InterfaceSoundLibraryTest.wave());
                var personal = sounds.Library.Replace(osu.Game.Audio.InterfaceSoundLibrary.DefaultPack, "UI/check-on", "check.wav", wave);
                sounds.Select(personal.Id);
            });
            AddStep("open exported file through global importer", () =>
            {
                selectedBefore = Game.LoadedResourcePack;
                string upper = Path.ChangeExtension(path!, ".MORARS");
                File.Move(path!, upper);
                path = upper;
                import = Task.Run(() => Game.Import(path));
            });
            AddUntilStep("global import finished", () => import?.IsCompleted == true);
            AddAssert("saved selection without hot swapping live resources", () =>
            {
                import!.GetAwaiter().GetResult();
                return Game.SelectedResourcePack != GameResourcePackLibrary.DefaultPack
                       && Game.LoadedResourcePack == selectedBefore && Game.ResourcePacks.Installed().Any(p => p.Id == Game.SelectedResourcePack);
            });
            AddStep("reopen selected archive through startup consumer", () =>
            {
                using var store = Game.ResourcePacks.Open(Game.SelectedResourcePack);
                Assert.That(store.Get("Textures/Icons/Navigation/palette.png"), Is.Not.Null);
                Assert.That(store.Get("Shaders/sh_SpinnerGlow.fs"), Is.Null);
                Assert.That(Game.ResourcePacks.ReadFonts(Game.SelectedResourcePack), Is.Not.Null);
            });
            string personalSound = "";
            AddStep("capture personal sound baseline", () => personalSound = Game.LocalConfig.Get<string>(OsuSetting.InterfaceSoundPack));
            AddStep("restart isolated test game with saved selection", () =>
            {
                Remove(Game, true);
                CreateGame();
            });
            AddUntilStep("imported pack loaded after restart", () => Game.IsLoaded && Game.LoadedResourcePack != GameResourcePackLibrary.DefaultPack);
            AddAssert("pack fonts applied through saved settings", () =>
                Game.LocalConfig.Get<string>(OsuSetting.ForkMainFont).StartsWith("morafn-", StringComparison.Ordinal)
                && Game.Resources.Get("Textures/Icons/Navigation/palette.png") != null);
            AddAssert("pack sound snapshot not shadowed by a previous personal bank", () =>
                Game.LocalConfig.Get<string>(OsuSetting.InterfaceSoundPack) == osu.Game.Audio.InterfaceSoundLibrary.DefaultPack);
            AddStep("select built-in resources", () => Game.SelectResourcePack(GameResourcePackLibrary.DefaultPack));
            AddStep("restart isolated test game with defaults", () =>
            {
                Remove(Game, true);
                CreateGame();
            });
            AddUntilStep("built-in resources restored", () => Game.IsLoaded && Game.LoadedResourcePack == GameResourcePackLibrary.DefaultPack);
            AddAssert("personal font and sound baselines restored", () => Game.LocalConfig.Get<string>(OsuSetting.ForkMainFont) == "Default"
                && Game.LocalConfig.Get<string>(OsuSetting.InterfaceSoundPack) == personalSound);
            AddAssert("no operation left busy", () => !Game.ResourcePackBusy.Value);

            string? originalReference = Environment.GetEnvironmentVariable("MOSU_TEST_ORIGINAL_RESOURCES");
            if (!string.IsNullOrEmpty(originalReference))
            {
                AddStep("open requested original comparison archive", () => import = Task.Run(() => Game.Import(originalReference)));
                AddUntilStep("original comparison import finished", () => import?.IsCompleted == true);
                AddStep("check original import result", () =>
                {
                    import!.GetAwaiter().GetResult();
                    Assert.That(Game.SelectedResourcePack, Is.Not.EqualTo(GameResourcePackLibrary.DefaultPack));
                });
                AddStep("restart isolated game with original comparison resources", () =>
                {
                    Remove(Game, true);
                    CreateGame();
                });
                AddUntilStep("original comparison resources loaded", () => Game.IsLoaded && Game.LoadedResourcePack == Game.SelectedResourcePack);
                AddStep("original Argon counters resolve without shader or font replacement", () =>
                {
                    using var zip = ZipFile.OpenRead(originalReference);
                    using var input = zip.GetEntry("Textures/Gameplay/Fonts/argon-counter-3.png")!.Open();
                    using var expected = new MemoryStream();
                    input.CopyTo(expected);
                    Assert.That(Game.Resources.Get("Textures/Gameplay/Fonts/argon-counter-3.png"), Is.EqualTo(expected.ToArray()));
                    Assert.That(Game.LocalConfig.Get<string>(OsuSetting.ForkMainFont), Is.EqualTo("Default"));
                    using var store = Game.ResourcePacks.Open(Game.LoadedResourcePack);
                    Assert.That(store.Get("Shaders/sh_SpinnerGlow.fs"), Is.Null);
                });
            }
        }
    }
}
