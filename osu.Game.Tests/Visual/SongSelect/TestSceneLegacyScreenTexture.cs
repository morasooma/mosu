// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Skinning.Select;
using osuTK;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneLegacyScreenTexture : OsuTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [TestCase(1)]
        [TestCase(2)]
        public void TestResizeAndSkinChangePreserveTextureScale(int resolutionScale)
        {
            Container viewport = null!;
            LegacyScreenTexture sprite = null!;

            AddStep("create widescreen skin texture", () => Child = viewport = new Container
            {
                Size = new Vector2(1366, 768),
                Child = sprite = new LegacyScreenTexture
                {
                    Texture = createTexture(1366, 155, resolutionScale),
                },
            });

            foreach (float width in new[] { 1024f, 1366f, 1920f, 1024f })
            {
                AddStep($"resize viewport to {width}", () => viewport.Width = width);
                AddUntilStep("sprite fills viewport", () => sprite.DrawWidth, () => Is.EqualTo(width));
                AddAssert("texture height is unchanged", () => sprite.DrawHeight, () => Is.EqualTo(155));
                AddAssert("texture is neither stretched nor squeezed", () =>
                {
                    var sampledPixels = sprite.DrawRectangle.RelativeIn(sprite.DrawTextureRectangle)
                                        * new Vector2(sprite.Texture.DisplayWidth, sprite.Texture.DisplayHeight);
                    return sampledPixels.Width;
                }, () => Is.EqualTo(width).Within(0.001f));
            }

            AddStep("change to a different skin size", () => sprite.Texture = createTexture(1800, 120, resolutionScale));
            AddUntilStep("new skin height applied", () => sprite.DrawHeight, () => Is.EqualTo(120));
            AddAssert("new skin retains native width", () => sprite.DrawTextureRectangle.Width, () => Is.EqualTo(1800));

            AddStep("remove texture", () => sprite.Texture = null!);
            AddUntilStep("missing texture clears geometry", () => sprite.DrawSize, () => Is.EqualTo(Vector2.Zero));
        }

        private Texture createTexture(int width, int height, int resolutionScale)
        {
            var texture = renderer.CreateTexture(width * resolutionScale, height * resolutionScale,
                wrapModeS: WrapMode.ClampToEdge, wrapModeT: WrapMode.ClampToEdge);
            texture.ScaleAdjust = resolutionScale;
            return texture;
        }
    }
}
