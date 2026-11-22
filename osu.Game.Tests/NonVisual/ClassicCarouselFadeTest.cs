using NUnit.Framework;
using osu.Game.Screens.SelectLegacy.Carousel;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class ClassicCarouselFadeTest
    {
        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(1f)]
        public void TestHeaderFadeDoesNotHideNestedDifficulties(float alpha)
        {
            using var item = new TestCarouselItem();
            item.SetMultiplicativeAlpha(alpha);

            Assert.Multiple(() =>
            {
                Assert.That(item.Header.Alpha, Is.EqualTo(alpha));
                Assert.That(item.DifficultyContainerAlpha, Is.EqualTo(1));
                Assert.That(item.ParentContainerAlpha, Is.EqualTo(1));
            });
        }

        private partial class TestCarouselItem : DrawableCarouselItem
        {
            public float DifficultyContainerAlpha => Content.Alpha;
            public float ParentContainerAlpha => MovementContainer.Alpha;
        }
    }
}
