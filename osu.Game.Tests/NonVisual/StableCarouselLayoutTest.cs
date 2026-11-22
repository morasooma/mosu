using NUnit.Framework;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class StableCarouselLayoutTest
    {
        [TestCase(0, 60)]
        [TestCase(384, 0)]
        [TestCase(768, 60)]
        public void TestLinearCurve(float y, float expected)
            => Assert.That(StableCarouselLayout.CurveOffset(y, 768), Is.EqualTo(expected).Within(0.001));

        [Test]
        public void TestSpacingPreservesCentreStepForDifferentHeights()
        {
            foreach (float top in new[] { 54f, 90f })
            foreach (float bottom in new[] { 54f, 90f })
            {
                Assert.That(top / 2 + StableCarouselLayout.Spacing(top, bottom, false) + bottom / 2,
                    Is.EqualTo(76.8f).Within(0.001));
                Assert.That(top / 2 + StableCarouselLayout.Spacing(top, bottom, true) + bottom / 2,
                    Is.EqualTo(92.8f).Within(0.001));
            }
        }

        [Test]
        public void TestMotionIsFrameRateIndependent()
        {
            foreach (double decay in new[] { 0.95, 0.875 })
            {
                double full = StableCarouselLayout.Approach(0, 100, decay, 1000.0 / 60);
                double half = StableCarouselLayout.Approach(0, 100, decay, 1000.0 / 120);
                double twice = StableCarouselLayout.Approach(half, 100, decay, 1000.0 / 120);
                Assert.That(twice, Is.EqualTo(full).Within(0.000001));
                Assert.That(full, Is.EqualTo(100 * (1 - decay)).Within(0.000001));
                Assert.That(StableCarouselLayout.Approach(0, 100, decay, 0), Is.Zero);
            }
        }

        [TestCase(16, 21.333333f)]
        [TestCase(12, 16)]
        public void TestGdiPointsConvertedToPixels(float points, float pixels)
            => Assert.That(StableCarouselLayout.FontPixels(points), Is.EqualTo(pixels).Within(0.001));

        [Test]
        public void TestStableAlpha()
        {
            Assert.That(StableCarouselLayout.Inactive.A, Is.EqualTo(240f / 255).Within(0.00001));
            Assert.That(StableCarouselLayout.Expanded.A, Is.EqualTo(240f / 255).Within(0.00001));
            Assert.That(StableCarouselLayout.Selected.A, Is.EqualTo(220f / 255).Within(0.00001));
        }
    }
}
