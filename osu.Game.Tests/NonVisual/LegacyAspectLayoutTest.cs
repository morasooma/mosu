// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics.Primitives;
using osu.Game.Screens.Select;
using osu.Game.Skinning.Select;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LegacyAspectLayoutTest
    {
        [TestCase(1024)]
        [TestCase(1366)]
        public void TestTopTextureUsesViewportWidthWithoutChangingSkinHeight(float viewportWidth)
        {
            Assert.That(LegacyScreenTexture.GetDrawSize(155, viewportWidth), Is.EqualTo(new Vector2(viewportWidth, 155)));
        }

        [TestCase(1024)]
        [TestCase(1366)]
        [TestCase(1920)]
        public void TestTopTextureKeepsNativePixelScale(float viewportWidth)
        {
            var textureRectangle = LegacyScreenTexture.GetTextureRectangle(1366, 155);
            var drawRectangle = new RectangleF(0, 0, viewportWidth, 155);
            // SpriteDrawNode uses this mapping to sample the texture, including when the
            // viewport crops its right side or extends past it using ClampToEdge.
            var sampledPixels = drawRectangle.RelativeIn(textureRectangle) * new Vector2(1366, 155);

            Assert.Multiple(() =>
            {
                Assert.That(sampledPixels.Width, Is.EqualTo(viewportWidth).Within(0.001f));
                Assert.That(sampledPixels.Height, Is.EqualTo(155));
            });
        }

        [TestCase(1024, 544)]
        [TestCase(1366, 544)]
        [TestCase(640, 544)]
        public void TestLegacyCarouselKeepsStableCardWidth(float viewportWidth, float expectedWidth)
        {
            Assert.That(SongSelect.GetLegacyCarouselWidth(viewportWidth), Is.EqualTo(expectedWidth));
        }

        [TestCase(768, 544)]
        [TestCase(960, 680)]
        [TestCase(640, 453.3333f)]
        public void TestLegacyCarouselWidthTracksScreenHeight(float height, float expectedWidth)
        {
            Assert.That(SongSelect.GetLegacyCarouselWidth(1920, height), Is.EqualTo(expectedWidth).Within(0.001));
        }

        [TestCase(1)]
        [TestCase(0.8f)]
        [TestCase(1.25f)]
        public void TestClassicPreviewUsesStableDimensions(float scale)
        {
            Vector2 size = PanelSetBackground.GetClassicPreviewSize(PanelBeatmapStandalone.CLASSIC_HEIGHT * scale);
            Assert.Multiple(() =>
            {
                Assert.That(size.X, Is.EqualTo(80 * 1.425f * scale).Within(0.001));
                Assert.That(size.Y, Is.EqualTo(60 * 1.425f * scale).Within(0.001));
            });
        }

        [TestCase(1024, 192)]
        [TestCase(1366, 224)]
        public void TestModeDecorationUsesAspectSpecificPosition(float viewportWidth, float expectedX)
        {
            Assert.That(LegacyTopDecoration.GetComponentsX(viewportWidth), Is.EqualTo(expectedX));
        }
    }
}
