using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Localisation;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class PracticeConfigTest
    {
        [TestCase("")]
        [TestCase("ko")]
        [TestCase("ru")]
        [TestCase("es")]
        [TestCase("zh")]
        public void AllPracticeStringsAreTranslated(string culture)
        {
            using var store = new ResourceManagerLocalisationStore(culture);
            foreach (string key in new[] { "setting_caption", "setting_hint", "continue", "select_time", "time" })
                Assert.That(store.Get($"osu.Game.Localisation.Practice:{key}"), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void EnabledByDefaultAndCanBeDisabled()
        {
            using var storage = new TemporaryNativeStorage("practice-config");
            using var config = new OsuConfigManager(storage);
            Assert.That(config.Get<bool>(OsuSetting.ForkPracticeMode), Is.True);
            config.SetValue(OsuSetting.ForkPracticeMode, false);
            Assert.That(config.Get<bool>(OsuSetting.ForkPracticeMode), Is.False);
        }
    }
}
