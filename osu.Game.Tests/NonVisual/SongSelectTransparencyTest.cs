using NUnit.Framework;
using osu.Game.Overlays;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class SongSelectTransparencyTest
    {
        [TestCase(false, false, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void TestCarouselIndependentFromOverlays(bool overlays, bool carousel, bool active)
        {
            bool previousOverlays = OverlayTransparency.Enabled.Value;
            bool previousCarousel = OverlayTransparency.SongSelectEnabled.Value;
            try
            {
                OverlayTransparency.Enabled.Value = overlays;
                OverlayTransparency.SongSelectEnabled.Value = carousel;
                Assert.That(OverlayTransparency.SongSelectActive, Is.EqualTo(active));
                Assert.That(OverlayTransparency.SongSelectSurfaceAlpha, Is.EqualTo(active ? OverlayTransparency.SURFACE_ALPHA : 1f));
            }
            finally
            {
                OverlayTransparency.Enabled.Value = previousOverlays;
                OverlayTransparency.SongSelectEnabled.Value = previousCarousel;
            }
        }
    }
}
