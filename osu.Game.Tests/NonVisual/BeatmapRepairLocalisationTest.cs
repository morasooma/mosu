using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BeatmapRepairLocalisationTest
    {
        private static readonly string[] keys =
        {
            "reload_beatmap_data", "couldnt_reload_beatmap_data", "cannot_upload_custom_difficulty_to_server",
            "upload_beatmap_to_server", "beatmap_upload_help", "beatmap_upload_help_header", "beatmap_upload_help_body",
            "preparing_beatmap_upload", "uploading_beatmap_to_server", "beatmap_uploaded_to_server", "couldnt_upload_beatmap_to_server",
        };

        [TestCase("en", "Refresh beatmap data", "Help", "What should I do?")]
        [TestCase("ru", "Обновить данные карты", "Помощь", "Что мне делать?")]
        [TestCase("es", "Actualizar datos del mapa", "Ayuda", "¿Qué debo hacer?")]
        [TestCase("zh", "刷新谱面数据", "帮助", "我该怎么做？")]
        public void TestRepairStrings(string locale, string refresh, string help, string header)
        {
            Assembly.Load("osu.Game.Resources");
            using var config = new TestConfig();
            using var manager = new LocalisationManager(config);
            using var store = new ResourceManagerLocalisationStore(locale);
            manager.AddLanguage(locale, store);
            config.SetValue(FrameworkSetting.Locale, locale);

            Assert.That(manager.GetLocalisedString(LeaderboardStrings.ReloadBeatmapData), Is.EqualTo(refresh));
            Assert.That(manager.GetLocalisedString(LeaderboardStrings.BeatmapUploadHelp), Is.EqualTo(help));
            Assert.That(manager.GetLocalisedString(LeaderboardStrings.BeatmapUploadHelpHeader), Is.EqualTo(header));
            Assert.That(manager.GetLocalisedString(LeaderboardStrings.BeatmapUploadHelpBody), Is.Not.Empty);

            // English intentionally uses code fallbacks; all other locales must have actual resources.
            if (locale != "en")
            {
                foreach (string key in keys)
                    Assert.That(store.Get($"osu.Game.Resources.Localisation.Leaderboard:{key}"), Is.Not.Null.And.Not.Empty, key);
            }

            if (locale == "en")
            {
                var properties = typeof(LeaderboardStrings).GetProperties(BindingFlags.Public | BindingFlags.Static);
                foreach (var property in properties.Where(p => p.PropertyType == typeof(LocalisableString)))
                {
                    string value = manager.GetLocalisedString((LocalisableString)property.GetValue(null)!);
                    Assert.That(value.Any(c => c >= '\u0400' && c <= '\u04ff'), Is.False, property.Name);
                }
            }
        }

        [TestCase("en", "Back")]
        [TestCase("ru", "Назад")]
        [TestCase("es", "Atrás")]
        [TestCase("zh", "返回")]
        [TestCase("ko", "뒤로")]
        public void TestHelpDialogBackButton(string locale, string expected)
        {
            Assembly.Load("osu.Game.Resources");
            using var config = new TestConfig();
            using var manager = new LocalisationManager(config);
            using var store = new ResourceManagerLocalisationStore(locale);
            manager.AddLanguage(locale, store);
            config.SetValue(FrameworkSetting.Locale, locale);
            using var dialog = new BeatmapUploadHelpDialog();

            Assert.That(manager.GetLocalisedString(dialog.Buttons.Single().Text), Is.EqualTo(expected));
        }

        private sealed class TestConfig : FrameworkConfigManager
        {
            protected override string Filename => null!;

            public TestConfig()
                : base(null!)
            {
            }
        }
    }
}
