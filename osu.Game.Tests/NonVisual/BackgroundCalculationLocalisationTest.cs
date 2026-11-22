using NUnit.Framework;
using osu.Framework.Localisation;
using osu.Framework.Configuration;
using osu.Framework.Testing;
using osu.Game.Localisation;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BackgroundCalculationLocalisationTest
    {
        [TestCase("ru", "Фоновые расчёты")]
        [TestCase("es", "Cálculos en segundo plano")]
        [TestCase("ko", "백그라운드 계산")]
        [TestCase("zh", "后台计算")]
        public void TestEveryCaptionLoadsFromActualClientStore(string culture, string title)
        {
            using var storage = new TemporaryNativeStorage("calculation-localisation-" + culture);
            using var config = new FrameworkConfigManager(storage);
            using var manager = new LocalisationManager(config);
            using var store = new ResourceManagerLocalisationStore(culture);
            manager.AddLanguage(culture, store);
            config.SetValue(FrameworkSetting.Locale, culture);
            foreach (var property in typeof(BackgroundCalculationStrings).GetProperties())
            {
                var text = (LocalisableString)property.GetValue(null)!;
                string? translated = store.Get("osu.Game.Localisation.BackgroundCalculation:" + property.Name);
                Assert.That(translated, Is.Not.Null.And.Not.Empty, property.Name);
                Assert.That(manager.GetLocalisedBindableString(text).Value, Is.EqualTo(translated), property.Name);
            }
            Assert.That(store.Get("osu.Game.Localisation.BackgroundCalculation:Title"), Is.EqualTo(title));
        }
    }
}
