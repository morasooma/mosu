using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Screens.Play.Break;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LegacyBreakPresentationTest
    {
        [TestCase(480, 0.625f)]
        [TestCase(768, 1f)]
        [TestCase(1080, 1.40625f)]
        [TestCase(1440, 1.875f)]
        public void TestTextureScale(float height, float expected)
            => Assert.That(LegacyBreakPresentation.GetTextureScale(height), Is.EqualTo(expected));

        [TestCase(0, 2880, null)]
        [TestCase(1000, 3881, 1001)]
        [TestCase(1000, 5000, 2120)]
        [TestCase(1000, 10000, 5500)]
        public void TestStart(double start, double end, double? expected)
            => Assert.That(LegacyBreakPresentation.GetStartTime(start, end), Is.EqualTo(expected));

        [TestCase(19, true, 0)]
        [TestCase(20, true, 1)]
        [TestCase(100, true, 0)]
        [TestCase(160, true, 1)]
        [TestCase(230, true, 0)]
        [TestCase(280, true, 1)]
        [TestCase(129, false, 0)]
        [TestCase(130, false, 1)]
        [TestCase(230, false, 0)]
        [TestCase(280, false, 1)]
        [TestCase(1280, true, 1)]
        [TestCase(1380, true, 0.5f)]
        [TestCase(1480, true, 0)]
        [TestCase(10000, false, 0)]
        public void TestAlpha(double elapsed, bool passed, float expected)
            => Assert.That(LegacyBreakPresentation.GetAlpha(elapsed, passed), Is.EqualTo(expected));

        [TestCase(ForkBreakStyle.Default)]
        [TestCase(ForkBreakStyle.Legacy)]
        [TestCase(ForkBreakStyle.Hidden)]
        public void TestStylePersists(ForkBreakStyle style)
        {
            using var storage = new TemporaryNativeStorage("break-style-config");
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<ForkBreakStyle>(OsuSetting.ForkBreakStyle), Is.EqualTo(ForkBreakStyle.Default));
                config.SetValue(OsuSetting.ForkBreakStyle, style);
            }

            using var reopened = new OsuConfigManager(storage);
            Assert.That(reopened.Get<ForkBreakStyle>(OsuSetting.ForkBreakStyle), Is.EqualTo(style));
        }

        [Test]
        public void TestDefaultStyleIsBackwardsCompatible()
            => Assert.That(default(ForkBreakStyle), Is.EqualTo(ForkBreakStyle.Default));
    }
}
