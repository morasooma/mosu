using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Localisation;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class GraphicsAdapterLocalisationTest
    {
        [TestCase("en", "Rendering GPU")]
        [TestCase("ru", "Видеокарта для рендеринга")]
        [TestCase("es", "GPU de renderizado")]
        [TestCase("ko", "렌더링 GPU")]
        [TestCase("zh", "渲染显卡")]
        public void TestActualRuntimeResolver(string culture, string expectedCaption)
        {
            using var storage = new TemporaryNativeStorage("gpu-localisation-" + culture);
            using var config = new FrameworkConfigManager(storage);
            using var manager = new LocalisationManager(config);
            using var store = new ResourceManagerLocalisationStore(culture);
            manager.AddLanguage(culture, store);
            config.SetValue(FrameworkSetting.Locale, culture);

            foreach (var property in typeof(GraphicsAdapterStrings).GetProperties())
            {
                string key = "osu.Game.Localisation.GraphicsAdapter:" + property.Name;
                string? translated = store.Get(key);
                var text = (LocalisableString)property.GetValue(null)!;
                if (culture == "en")
                {
                    // English intentionally bypasses resource stores in the actual client resolver.
                    Assert.That(translated, Is.Null, key);
                    Assert.That(manager.GetLocalisedBindableString(text).Value, Is.Not.Null.And.Not.Empty, key);
                }
                else
                {
                    Assert.That(translated, Is.Not.Null.And.Not.Empty, key);
                    Assert.That(manager.GetLocalisedBindableString(text).Value, Is.EqualTo(translated), key);
                }
            }
            Assert.That(manager.GetLocalisedBindableString(GraphicsAdapterStrings.Caption).Value, Is.EqualTo(expectedCaption));
            string? format = culture == "en" ? "Currently rendering on: {0}" : store.Get("osu.Game.Localisation.GraphicsAdapter:Active");
            Assert.That(format, Does.Contain("{0}"));
            Assert.That(manager.GetLocalisedBindableString(GraphicsAdapterStrings.Active("TEST GPU")).Value,
                Is.EqualTo(string.Format(format!, "TEST GPU")));
        }
    }
}
