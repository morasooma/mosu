using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Fonts;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class FontPackTest
    {
        [Test]
        public void TestDefaultsAndLegacyGlobalMigration()
        {
            using var storage = new TemporaryNativeStorage("font-config");
            using (var config = new OsuConfigManager(storage))
            {
                var settings = FontConfiguration.Capture(config);
                Assert.That(settings.Separate, Is.True);
                Assert.That(settings.Resolve("Torus"), Is.EqualTo("Torus"));
                settings.Main = "Inter"; settings.Alternate = "Nunito";
                settings.Apply(config);
                Assert.That(FontConfiguration.Capture(config).Resolve("Torus"), Is.EqualTo("Inter"));
                Assert.That(settings.Resolve("Torus-Alternate"), Is.EqualTo("Nunito"));
                Assert.That(settings.Resolve("argon-counter"), Is.EqualTo("argon-counter"));
                Assert.That(settings.Resolve("OsuIcon"), Is.EqualTo("OsuIcon"));
            }
            using var legacy = new TemporaryNativeStorage("font-config-legacy");
            using (var stream = legacy.GetStream("mosu.ini", FileAccess.Write, FileMode.Create))
            using (var writer = new StreamWriter(stream)) writer.WriteLine("ForkCustomUIFont = Inter");
            using var migrated = new OsuConfigManager(legacy);
            Assert.That(migrated.Get<bool>(OsuSetting.ForkUseSeparateFonts), Is.False);
            Assert.That(FontConfiguration.Capture(migrated).Resolve("Torus"), Is.EqualTo("Inter"));
        }

        [Test]
        public void TestThemeRoundTripAndLegacyTheme()
        {
            using var storage = new TemporaryNativeStorage("font-theme");
            using var config = new OsuConfigManager(storage);
            var selection = new FontConfiguration { Main = "Inter", Alternate = "Nunito", Numeric = "Michroma", Inter = "MosuRoundedAlt" };
            selection.Apply(config);
            var theme = ThemeInterfaceSettings.Capture(config);
            var copy = theme.DeepClone();
            copy.Fonts!.Main = "Nunito";
            Assert.That(theme.Fonts!.Main, Is.EqualTo("Inter"));
            var restored = JsonConvert.DeserializeObject<ThemeInterfaceSettings>(JsonConvert.SerializeObject(theme))!;
            restored.Apply(config);
            Assert.That(FontConfiguration.Capture(config).Resolve("Venera"), Is.EqualTo("Michroma"));
            new ThemeInterfaceSettings { Font = "Inter" }.Apply(config);
            Assert.That(config.Get<bool>(OsuSetting.ForkUseSeparateFonts), Is.False);
        }

        [Test]
        public void TestArchiveRoundTripInstallsWithoutOverwritingUserFiles()
        {
            using var storage = new TemporaryNativeStorage("font-roundtrip");
            using var archive = new MemoryStream();
            var settings = new FontConfiguration { Common = "Inter", Main = "Nunito", Alternate = "MosuRoundedAlt", Numeric = "Michroma", Inter = "Inter" };
            FontPackExporter.Export(archive, settings, storage, Array.Empty<FontPackLibrary.Validated>());
            archive.Position = 0;
            var pack = FontPackLibrary.Validate(archive);
            Assert.That(pack.Manifest.Families.Count, Is.EqualTo(4));
            Assert.That(pack.Files.Keys.Any(k => k.EndsWith(".png")), Is.True);
            Assert.That(pack.Manifest.Families.All(f => f.Notices.Count > 0), Is.True);
            var library = new FontPackLibrary(storage);
            string path = library.Install(pack);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(library.Install(pack), Is.EqualTo(path));
            var loaded = library.Installed().Single();
            Assert.That(loaded.Id, Is.EqualTo(pack.Id));
            Assert.That(loaded.InstalledSettings().Resolve("Torus"), Does.StartWith("morafn-"));
            Assert.That(storage.GetStorageForDirectory("Fonts").GetFiles(string.Empty), Is.Empty);
            using var second = new MemoryStream();
            FontPackExporter.Export(second, loaded.InstalledSettings(), storage, new[] { loaded });
            second.Position = 0;
            Assert.That(FontPackLibrary.Validate(second).Manifest.Families.Count, Is.EqualTo(4));
        }

        [Test]
        public void TestDefaultArchiveIncludesAllRoleAtlases()
        {
            using var storage = new TemporaryNativeStorage("font-default-export");
            using var output = new MemoryStream();
            FontPackExporter.Export(output, new FontConfiguration(), storage, Array.Empty<FontPackLibrary.Validated>());
            output.Position = 0;
            var pack = FontPackLibrary.Validate(output);
            Assert.That(pack.Manifest.Settings.Main, Is.EqualTo("Torus"));
            Assert.That(pack.Manifest.Settings.Alternate, Is.EqualTo("Torus-Alternate"));
            Assert.That(pack.Manifest.Families.Select(f => f.Name), Does.Contain("Inter"));
        }

        [Test]
        public void TestInstalledThemeFontsSurviveFreshConfiguration()
        {
            using var storage = new TemporaryNativeStorage("font-theme-restart");
            using var archive = new MemoryStream();
            FontPackExporter.Export(archive, new FontConfiguration { Main = "Inter" }, storage, Array.Empty<FontPackLibrary.Validated>());
            archive.Position = 0;
            var pack = FontPackLibrary.Validate(archive);
            new FontPackLibrary(storage).Install(pack);
            using var config = new OsuConfigManager(storage);
            // Deliberately do not register any runtime stores; startup loads themes first.
            new ThemeInterfaceSettings { Fonts = pack.InstalledSettings() }.Apply(config);
            Assert.That(config.Get<string>(OsuSetting.ForkMainFont), Is.EqualTo(pack.InstalledFamily("Inter")));
            using var other = new TemporaryNativeStorage("font-other-profile");
            using var otherConfig = new OsuConfigManager(other);
            new ThemeInterfaceSettings { Fonts = pack.InstalledSettings() }.Apply(otherConfig);
            Assert.That(otherConfig.Get<string>(OsuSetting.ForkMainFont), Is.EqualTo("Default"));
        }

        [Test]
        public void TestMultiPageNotoExportAndValidation()
        {
            using var storage = new TemporaryNativeStorage("font-noto-export");
            using var archive = new MemoryStream();
            FontPackExporter.Export(archive, new FontConfiguration { Separate = false, Common = "Noto" }, storage, Array.Empty<FontPackLibrary.Validated>());
            archive.Position = 0;
            var pack = FontPackLibrary.Validate(archive);
            Assert.That(pack.Manifest.Families.Single().Faces.Count, Is.EqualTo(6));
            Assert.That(pack.Files.Keys.Count(k => k.EndsWith(".png")), Is.EqualTo(176));
        }

        [Test]
        public void TestCustomBmFontAndNoticesRoundTrip()
        {
            using var storage = new TemporaryNativeStorage("font-custom-export");
            var fonts = storage.GetStorageForDirectory("Fonts");
            using var resources = new MosuFontResources();
            byte[] definition = resources.Get("Fonts/Inter/Inter-Regular.bin");
            using (var output = fonts.GetStream("User Font.fnt", FileAccess.Write, FileMode.Create)) output.Write(definition);
            using var descriptor = new MemoryStream(definition);
            int pages = SharpFNT.BitmapFont.FromStream(descriptor, SharpFNT.FormatHint.Binary, false).Pages.Count;
            for (int page = 0; page < pages; page++)
            {
                using var output = fonts.GetStream(FontPackLibrary.PageName("User Font.fnt.bin", page, pages), FileAccess.Write, FileMode.Create);
                output.Write(resources.Get(FontPackLibrary.PageName("Fonts/Inter/Inter-Regular.bin", page, pages)));
            }
            using (var output = fonts.GetStream("User Font-LICENSE.txt", FileAccess.Write, FileMode.Create))
                output.Write(Encoding.UTF8.GetBytes("User-supplied font licence notice."));
            using var archive = new MemoryStream();
            FontPackExporter.Export(archive, new FontConfiguration { Separate = false, Common = "User Font.fnt" }, storage, Array.Empty<FontPackLibrary.Validated>());
            archive.Position = 0;
            var pack = FontPackLibrary.Validate(archive);
            Assert.That(pack.Manifest.Families.Single().MetricsFamily, Is.EqualTo("Custom"));
            Assert.That(Encoding.UTF8.GetString(pack.Files[pack.Manifest.Families.Single().Notices.Single()]), Is.EqualTo("User-supplied font licence notice."));
            Assert.That(pack.InstalledSettings().Common, Does.StartWith("morafn-"));
            Assert.That(fonts.Exists("User Font.fnt"), Is.True);
        }

        [Test]
        public void TestNamedThemeRestoresSeparateAssignments()
        {
            using var storage = new TemporaryNativeStorage("font-library-roles");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
            new FontConfiguration { Main = "Inter", Alternate = "Nunito" }.Apply(config);
            string original = config.CustomThemes.ActiveId;
            config.CustomThemes.Create("Second");
            config.SetValue(OsuSetting.ForkAlternateFont, "Michroma");
            Assert.That(config.CustomThemes.Active.Theme.Interface!.Fonts!.Alternate, Is.EqualTo("Michroma"));
            config.CustomThemes.Select(original);
            Assert.That(config.Get<string>(OsuSetting.ForkMainFont), Is.EqualTo("Inter"));
            Assert.That(config.Get<string>(OsuSetting.ForkAlternateFont), Is.EqualTo("Nunito"));
            var shared = config.CustomThemes.GetActiveThemeForSharing();
            Assert.That(ThemeShareManager.TryExportTheme(shared, storage, "Separate", out string name, out var error), Is.True, error);
            using var input = storage.GetStream(name);
            Assert.That(ThemeShareManager.ValidateAndImport(input, out var restored, out _, out _), Is.True);
            Assert.That(restored.Interface!.Fonts!.Alternate, Is.EqualTo("Nunito"));
        }

        [TestCase("missing-page")]
        [TestCase("corrupt-page")]
        [TestCase("corrupt-descriptor")]
        [TestCase("unsupported-version")]
        [TestCase("missing-family")]
        [TestCase("duplicate-case")]
        [TestCase("symlink")]
        [TestCase("oversized-entry")]
        public void TestMalformedPortableDataIsRejected(string mutation)
        {
            using var storage = new TemporaryNativeStorage("font-malformed");
            using var valid = new MemoryStream();
            FontPackExporter.Export(valid, new FontConfiguration { Separate = false, Common = "Inter" }, storage, Array.Empty<FontPackLibrary.Validated>());
            valid.Position = 0;
            using var original = new ZipArchive(valid, ZipArchiveMode.Read);
            using var damaged = new MemoryStream();
            using (var zip = new ZipArchive(damaged, ZipArchiveMode.Create, true))
            {
                bool changed = false;
                foreach (var entry in original.Entries)
                {
                    if (!changed && mutation == "missing-page" && entry.Name.EndsWith(".png")) { changed = true; continue; }
                    var target = zip.CreateEntry(entry.FullName);
                    using var output = target.Open();
                    using var input = entry.Open();
                    if (!changed && mutation == "corrupt-page" && entry.Name.EndsWith(".png")) { output.Write(new byte[8]); changed = true; }
                    else if (!changed && mutation == "corrupt-descriptor" && entry.Name.EndsWith(".bin")) { output.Write(new byte[] { 66, 77, 70, 3, 4, 255, 255, 255, 127 }); changed = true; }
                    else if (entry.FullName == "pack.json" && mutation is "unsupported-version" or "missing-family")
                    {
                        using var reader = new StreamReader(input);
                        var manifest = JsonConvert.DeserializeObject<FontPackLibrary.Pack>(reader.ReadToEnd())!;
                        if (mutation == "unsupported-version") manifest.Version = 999;
                        else manifest.Settings.Common = "Not present";
                        output.Write(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest)));
                    }
                    else input.CopyTo(output);
                }
                if (mutation is "duplicate-case" or "symlink" or "oversized-entry")
                {
                    var extra = zip.CreateEntry(mutation == "duplicate-case" ? "PACK.JSON" : "extra.txt");
                    if (mutation == "symlink") extra.ExternalAttributes = unchecked((int)0xa1ff0000);
                    using var output = extra.Open();
                    if (mutation == "oversized-entry") output.Write(new byte[16 * 1024 * 1024 + 1]);
                }
            }
            damaged.Position = 0;
            Assert.That(() => FontPackLibrary.Validate(damaged), Throws.Exception, mutation);
        }

        [TestCase("../escape.txt")]
        [TestCase("/absolute.png")]
        [TestCase("fonts\\escape.png")]
        [TestCase("fonts/a.exe")]
        [TestCase("pack.json")]
        public void TestUnsafeOrUnexpectedEntriesAreRejected(string name)
        {
            using var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("pack.json").Open())) writer.Write("{}");
                using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write("bad");
            }
            archive.Position = 0;
            Assert.That(() => FontPackLibrary.Validate(archive), Throws.Exception);
        }
    }
}
