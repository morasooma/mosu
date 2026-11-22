using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class FavouriteSettingsTest
    {
        [Test]
        public void TestOrderedDeduplicatedPersistence()
        {
            var config = new Bindable<string>("[\"A\",null,\"A\",\"\",\"B\"]");
            using var store = new FavouriteSettingsStore(config.GetBoundCopy());
            Assert.That(store.Ids, Is.EqualTo(new[] { "A", "B" }));
            store.Toggle("A");
            store.Toggle("C");
            store.Toggle("A");
            using var reopened = new FavouriteSettingsStore(config.GetBoundCopy());
            Assert.That(reopened.Ids, Is.EqualTo(new[] { "B", "C", "A" }));
        }

        [TestCase("broken")]
        [TestCase("null")]
        [TestCase("{}")]
        public void TestMalformedConfiguration(string value)
        {
            using var store = new FavouriteSettingsStore(new Bindable<string>(value));
            Assert.That(store.Ids, Is.Empty);
            store.Toggle("valid");
            Assert.That(store.Contains("valid"), Is.True);
        }

        [Test]
        public void TestRealConfigSaveAndReload()
        {
            using var storage = new TemporaryNativeStorage("favourite-settings-" + Guid.NewGuid());
            using (var config = new OsuConfigManager(storage))
            {
                using var store = new FavouriteSettingsStore(config.GetBindable<string>(OsuSetting.ForkFavouriteSettings));
                store.Toggle("OsuSetting.CursorScale");
                Assert.That(config.Save(), Is.True);
            }
            using var loaded = new OsuConfigManager(storage);
            using var reopened = new FavouriteSettingsStore(loaded.GetBindable<string>(OsuSetting.ForkFavouriteSettings));
            Assert.That(reopened.Ids, Is.EqualTo(new[] { "OsuSetting.CursorScale" }));
        }

        [Test]
        public void TestDefaultHotkeyUsesNormalRemappableOverlayBindings()
        {
            var defaults = GlobalActionContainer.GetDefaultBindingsFor(GlobalActionCategory.Overlays).ToArray();
            Assert.That(defaults.Single(b => (GlobalAction)b.Action == GlobalAction.ToggleFavouriteSettings).KeyCombination.Keys,
                Is.EquivalentTo(new[] { InputKey.Control, InputKey.Alt, InputKey.O }));
            Assert.That(defaults.Single(b => (GlobalAction)b.Action == GlobalAction.ToggleSettings).KeyCombination.Keys,
                Is.EquivalentTo(new[] { InputKey.Control, InputKey.O }));
            Assert.That((int)GlobalAction.ToggleSettings, Is.EqualTo(4), "Persisted existing enum values must not move");
        }

        [TestCase("en", "Favourite settings")]
        [TestCase("ru", "Избранные настройки")]
        [TestCase("es", "Ajustes favoritos")]
        [TestCase("ko", "즐겨찾는 설정")]
        [TestCase("zh", "收藏设置")]
        public void TestRuntimeLocalisation(string culture, string expected)
        {
            using var storage = new TemporaryNativeStorage("favourite-localisation-" + culture);
            using var config = new FrameworkConfigManager(storage);
            using var manager = new LocalisationManager(config);
            using var store = new ResourceManagerLocalisationStore(culture);
            manager.AddLanguage(culture, store);
            config.SetValue(FrameworkSetting.Locale, culture);
            foreach (var property in typeof(FavouriteSettingsStrings).GetProperties())
            {
                var text = (LocalisableString)property.GetValue(null)!;
                string resolved = manager.GetLocalisedBindableString(text).Value;
                Assert.That(resolved, Is.Not.Empty);
                if (culture != "en")
                {
                    var translated = store.Get("osu.Game.Localisation.FavouriteSettings:" + property.Name);
                    Assert.That(translated, Is.Not.Null.And.Not.Empty);
                    Assert.That(resolved, Is.EqualTo(translated));
                    Assert.That(resolved, Is.Not.EqualTo(text.ToString()), "Must not silently use English fallback");
                }
            }
            Assert.That(manager.GetLocalisedBindableString(FavouriteSettingsStrings.Title).Value, Is.EqualTo(expected));
            string format = culture == "en" ? "Shortcut: {0}" : store.Get("osu.Game.Localisation.FavouriteSettings:Shortcut")!;
            Assert.That(format, Does.Contain("{0}"));
            Assert.That(manager.GetLocalisedBindableString(FavouriteSettingsStrings.Shortcut("Ctrl+Alt+O")).Value,
                Is.EqualTo(string.Format(format, "Ctrl+Alt+O")));
        }
    }
}
