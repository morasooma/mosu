using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class CustomThemeLibraryTest
    {
        [Test]
        public void TestMigrateExistingThemeWithoutChangingMode()
        {
            using var storage = new TemporaryNativeStorage("named-theme-migration");
            var original = new CustomThemeDefinition(ThemeMode.Dark);
            original.SetOverride(OverlayColour.Colour1, Colour4.Red);
            using (var stream = storage.GetStream("mosu.ini", FileAccess.Write, FileMode.Create))
            using (var writer = new StreamWriter(stream))
                writer.WriteLine($"ForkThemeMode = Light\nForkCustomThemeJson = {original.ToJson()}");

            using var config = new OsuConfigManager(storage);
            Assert.That(config.CustomThemes.Entries.Count, Is.EqualTo(1));
            Assert.That(config.CustomThemes.Active.Theme, Is.EqualTo(original));
            Assert.That(config.Get<ThemeMode>(OsuSetting.ForkThemeMode), Is.EqualTo(ThemeMode.Light));
            Assert.That(current(config), Is.EqualTo(original));
        }

        [Test]
        public void TestCopiesAreIndependentAndLiveEditsSurviveSwitching()
        {
            using var storage = new TemporaryNativeStorage("named-theme-live");
            using var config = new OsuConfigManager(storage);
            var library = config.CustomThemes;
            string originalId = library.ActiveId;
            var red = new CustomThemeDefinition(ThemeMode.Dark);
            red.SetOverride(OverlayColour.Colour1, Colour4.Red);
            config.SetValue(OsuSetting.ForkCustomThemeJson, red.ToJson());
            Assert.That(library.Create("  Ночная тема  "), Is.True);
            string copyId = library.ActiveId;
            Assert.That(library.Active.Name, Is.EqualTo("Ночная тема"));
            var copied = red.DeepClone();
            copied.Interface = ThemeInterfaceSettings.Capture(config);
            Assert.That(current(config), Is.EqualTo(copied));

            var blue = red.DeepClone();
            blue.SetOverride(OverlayColour.Colour1, Colour4.Blue);
            config.SetValue(OsuSetting.ForkCustomThemeJson, blue.ToJson());
            library.Select(originalId);
            Assert.That(current(config), Is.EqualTo(red));
            library.Select(copyId);
            Assert.That(current(config), Is.EqualTo(blue));
        }

        [Test]
        public void TestRestartRestoresNamesSelectionAndLiveChanges()
        {
            using var storage = new TemporaryNativeStorage("named-theme-restart");
            string selectedId;
            var theme = new CustomThemeDefinition(ThemeMode.Light);
            theme.SetOverride(OverlayColour.Content1, Colour4.Lime);
            using (var config = new OsuConfigManager(storage))
            {
                config.CustomThemes.Create("Лес");
                config.CustomThemes.Rename("Лес ☘");
                selectedId = config.CustomThemes.ActiveId;
                config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
                config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
                config.Save();
            }

            using var restored = new OsuConfigManager(storage);
            Assert.That(restored.CustomThemes.Entries.Count, Is.EqualTo(2));
            Assert.That(restored.CustomThemes.ActiveId, Is.EqualTo(selectedId));
            Assert.That(restored.CustomThemes.Active.Name, Is.EqualTo("Лес ☘"));
            Assert.That(current(restored), Is.EqualTo(theme));
            Assert.That(restored.Get<ThemeMode>(OsuSetting.ForkThemeMode), Is.EqualTo(ThemeMode.Custom));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("bad\nname")]
        public void TestInvalidNameDoesNotChangeLibrary(string name)
        {
            using var storage = new TemporaryNativeStorage("named-theme-validation");
            using var config = new OsuConfigManager(storage);
            Assert.That(config.CustomThemes.Create(name), Is.False);
            Assert.That(config.CustomThemes.Rename(name), Is.False);
            Assert.That(config.CustomThemes.Create(new string('a', CustomThemeLibrary.MAX_NAME_LENGTH + 1)), Is.False);
            Assert.That(config.CustomThemes.Entries.Count, Is.EqualTo(1));
            Assert.That(config.CustomThemes.Active.Name, Is.Empty);
        }

        [Test]
        public void TestDuplicateNamesHaveIndependentIds()
        {
            using var storage = new TemporaryNativeStorage("named-theme-duplicates");
            using var config = new OsuConfigManager(storage);
            config.CustomThemes.Create("Same");
            string first = config.CustomThemes.ActiveId;
            config.CustomThemes.Create("Same");
            Assert.That(config.CustomThemes.ActiveId, Is.Not.EqualTo(first));
            Assert.That(config.CustomThemes.Entries.Select(e => e.Id).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void TestImportAddsEntryInsteadOfReplacingExistingTheme()
        {
            using var storage = new TemporaryNativeStorage("named-theme-import");
            using var config = new OsuConfigManager(storage);
            var original = config.CustomThemes.Active;
            var imported = new CustomThemeDefinition(ThemeMode.Light);
            imported.SetOverride(OverlayColour.Colour1, Colour4.Pink);
            config.CustomThemes.Create("Imported", imported);
            imported.ClearAllOverrides();
            Assert.That(config.CustomThemes.Entries, Has.Count.EqualTo(2));
            Assert.That(original.Theme, Is.EqualTo(new CustomThemeDefinition()));
            Assert.That(config.CustomThemes.Active.Theme.TryGetOverride(OverlayColour.Colour1, out _), Is.True);
            Assert.That(current(config), Is.EqualTo(config.CustomThemes.Active.Theme));
        }

        [Test]
        public void TestExportImportPreservesFullNameAndPalette()
        {
            using var storage = new TemporaryNativeStorage("named-theme-sharing");
            using var config = new OsuConfigManager(storage);
            string name = new string('Я', CustomThemeLibrary.MAX_NAME_LENGTH);
            config.CustomThemes.Create(name, new CustomThemeDefinition(ThemeMode.Light));
            Assert.That(ThemeShareManager.TryExportTheme(config.CustomThemes.Active.Theme, storage, config.CustomThemes.Active.Name,
                out string fileName, out string? error), Is.True, error);
            using var stream = storage.GetStream(fileName);
            Assert.That(ThemeShareManager.ValidateAndImport(stream, out var theme, out string importedName, out _), Is.True);
            Assert.That(importedName, Is.EqualTo(name));
            Assert.That(theme, Is.EqualTo(config.CustomThemes.Active.Theme));
        }

        [Test]
        public void TestDeleteSelectsRemainingThemeAndKeepsLastEntry()
        {
            using var storage = new TemporaryNativeStorage("named-theme-delete");
            using var config = new OsuConfigManager(storage);
            string originalId = config.CustomThemes.ActiveId;
            config.CustomThemes.Create("Delete me");
            config.CustomThemes.DeleteActive();
            Assert.That(config.CustomThemes.ActiveId, Is.EqualTo(originalId));
            config.CustomThemes.DeleteActive();
            Assert.That(config.CustomThemes.Entries, Has.Count.EqualTo(1));
            Assert.That(current(config), Is.EqualTo(config.CustomThemes.Active.Theme));
        }

        [TestCase("broken json")]
        [TestCase("[null]")]
        [TestCase("[]")]
        public void TestDamagedLibraryPreservesWorkingPalette(string json)
        {
            using var storage = new TemporaryNativeStorage("named-theme-damaged");
            var original = new CustomThemeDefinition(ThemeMode.Dark);
            original.SetOverride(OverlayColour.Content1, Colour4.Red);
            using (var stream = storage.GetStream("mosu.ini", FileAccess.Write, FileMode.Create))
            using (var writer = new StreamWriter(stream))
                writer.WriteLine($"ForkCustomThemesJson = {json}\nForkCustomThemeJson = {original.ToJson()}");

            using var config = new OsuConfigManager(storage);
            Assert.That(config.CustomThemes.Entries, Has.Count.EqualTo(1));
            Assert.That(current(config), Is.EqualTo(original));
        }

        [Test]
        public void TestInterfaceSettingsRoundTripAndInstallationIsolation()
        {
            using var storage = new TemporaryNativeStorage("theme-interface-sharing");
            using var config = new OsuConfigManager(storage);
            var theme = new CustomThemeDefinition { Interface = new ThemeInterfaceSettings
            {
                DisableShear = true, SongSelectStyle = ForkSongSelectStyle.Classic2024, OldCarouselPreviews = true,
                Animations = InterfaceAnimationMode.Minimal, EnhancedAnimations = true,
                OverlayTransparency = true, SongSelectTransparency = true, BlurStrength = 0.42, DimAmount = 0.63,
                Font = "Inter",
            } };
            Assert.That(ThemeShareManager.TryExportTheme(theme, storage, "Full theme", out string filename, out var error), Is.True, error);
            using var stream = storage.GetStream(filename);
            Assert.That(ThemeShareManager.ValidateAndImport(stream, out var imported, out _, out var importError), Is.True, importError.ToString());
            Assert.That(imported, Is.EqualTo(theme));
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
            string originalId = config.CustomThemes.ActiveId;
            string before = config.Get<string>(OsuSetting.ForkCustomThemeJson);
            config.CustomThemes.Create("Downloaded", imported, select: false);
            Assert.That(config.Get<string>(OsuSetting.ForkCustomThemeJson), Is.EqualTo(before));
            Assert.That(config.Get<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle), Is.EqualTo(ForkSongSelectStyle.Modern));
            Assert.That(config.Get<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews), Is.False);
            config.CustomThemes.Select(config.CustomThemes.Entries.Last().Id);
            Assert.That(ThemeInterfaceSettings.Capture(config), Is.EqualTo(theme.Interface).Using<ThemeInterfaceSettings>((a, b) => Newtonsoft.Json.JsonConvert.SerializeObject(a) == Newtonsoft.Json.JsonConvert.SerializeObject(b)));
            config.SetValue(OsuSetting.ForkOverlayBlurStrength, 0.15);
            Assert.That(config.CustomThemes.Active.Theme.Interface!.BlurStrength, Is.EqualTo(0.15));
            config.CustomThemes.Select(originalId);
            // A legacy theme without these fields does not overwrite current appearance settings.
            Assert.That(config.Get<bool>(OsuSetting.ForkDisableInterfaceShear), Is.True);
            Assert.That(config.Get<double>(OsuSetting.ForkOverlayBlurStrength), Is.EqualTo(0.15));
            Assert.That(config.Get<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews), Is.True);
        }

        [Test]
        public void TestSwitchingAndReapplyingThemeRestoresInterfaceSettings()
        {
            using var storage = new TemporaryNativeStorage("theme-interface-switch");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
            config.CustomThemes.Create("First", new CustomThemeDefinition { Interface = new ThemeInterfaceSettings { DisableShear = true, OldCarouselPreviews = true, Font = "Noto" } });
            string first = config.CustomThemes.ActiveId;
            config.CustomThemes.Create("Second", new CustomThemeDefinition { Interface = new ThemeInterfaceSettings { DisableShear = false, OldCarouselPreviews = false, Font = "Inter" } });
            Assert.That(config.Get<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews), Is.False);
            config.CustomThemes.Select(first);
            Assert.That(config.Get<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews), Is.True);
            Assert.That(config.Get<bool>(OsuSetting.ForkDisableInterfaceShear), Is.True);
            Assert.That(config.Get<string>(OsuSetting.ForkCustomUIFont), Is.EqualTo("Noto"));
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Dark);
            config.SetValue(OsuSetting.ForkCustomThemeJson, new CustomThemeDefinition { Interface = new ThemeInterfaceSettings { DisableShear = false } }.ToJson());
            Assert.That(config.Get<bool>(OsuSetting.ForkDisableInterfaceShear), Is.True);
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
            Assert.That(config.Get<bool>(OsuSetting.ForkDisableInterfaceShear), Is.False);
        }

        [Test]
        public void TestInterfaceSettingsSurviveRestart()
        {
            using var storage = new TemporaryNativeStorage("theme-interface-restart");
            using (var config = new OsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
                config.CustomThemes.Create("Saved appearance");
                config.SetValue(OsuSetting.ForkDisableInterfaceShear, true);
                config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, true);
                config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.InfiniteGlass);
                config.SetValue(OsuSetting.ForkInterfaceAnimations, InterfaceAnimationMode.Disabled);
                config.SetValue(OsuSetting.ForkOverlayTransparency, true);
                config.SetValue(OsuSetting.ForkCustomUIFont, "Inter");
                config.Save();
            }
            using var restored = new OsuConfigManager(storage);
            var appearance = restored.CustomThemes.Active.Theme.Interface!;
            Assert.That(appearance.DisableShear, Is.True);
            Assert.That(appearance.OldCarouselPreviews, Is.True);
            Assert.That(restored.Get<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews), Is.True);
            Assert.That(appearance.SongSelectStyle, Is.EqualTo(ForkSongSelectStyle.InfiniteGlass));
            Assert.That(appearance.Animations, Is.EqualTo(InterfaceAnimationMode.Disabled));
            Assert.That(appearance.OverlayTransparency, Is.True);
            Assert.That(appearance.Font, Is.EqualTo("Inter"));
            Assert.That(restored.Get<string>(OsuSetting.ForkCustomUIFont), Is.EqualTo("Inter"));
        }

        [TestCase("../font.fnt")]
        [TestCase("https://example.com/font")]
        public void TestUnsafeFontReferenceRejected(string font)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(new PortableThemePackage
            {
                Theme = new CustomThemeDefinition { Interface = new ThemeInterfaceSettings { Font = font } },
            });
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            Assert.That(ThemeShareManager.ValidateAndImport(stream, out _, out _, out _), Is.False);
        }

        [Test]
        public void TestUnavailableFontFallsBackWithoutDroppingReference()
        {
            using var storage = new TemporaryNativeStorage("theme-font-fallback");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
            config.CustomThemes.Create("Missing font", new CustomThemeDefinition { Interface = new ThemeInterfaceSettings { Font = "Missing.fnt" } });
            Assert.That(config.Get<string>(OsuSetting.ForkCustomUIFont), Is.EqualTo("Default"));
            Assert.That(config.CustomThemes.Active.Theme.Interface!.Font, Is.EqualTo("Missing.fnt"));
            Assert.That(config.CustomThemes.GetActiveThemeForSharing().Interface!.Font, Is.EqualTo("Missing.fnt"));
        }

        private static CustomThemeDefinition current(OsuConfigManager config) =>
            CustomThemeDefinition.FromJson(config.Get<string>(OsuSetting.ForkCustomThemeJson));
    }
}
