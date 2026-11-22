using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Backgrounds.AI;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK;

namespace osu.Game.Tests.Visual.Background
{
    public partial class TestSceneBackgroundSafety : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;
        [Resolved]
        private ForkDataStore store { get; set; } = null!;
        [Resolved]
        private BackgroundSafetyService service { get; set; } = null!;
        [Resolved]
        private IRenderer renderer { get; set; } = null!;
        [Resolved]
        private osu.Framework.Platform.Storage storage { get; set; } = null!;

        [Test]
        public void TestBlurPolicyRestoreAndLateCancellation()
        {
            BeatmapBackground background = null!;
            Texture original = null!;
            AddStep("prepare verified local model and cached explicit result", () =>
            {
                config.SetValue(OsuSetting.ForkAiBackgroundBlur, false);
                config.SetValue(OsuSetting.ForkAiGeneralModel, true);
                config.SetValue(OsuSetting.ForkAiAnimeModel, true);
                config.SetValue(OsuSetting.ForkAiGeneralThreshold, 0.3);
                config.SetValue(OsuSetting.ForkAiAnimeThreshold, 0.3);
                config.SetValue(OsuSetting.ForkAiBackgroundFilterMode, BackgroundFilterMode.Suggestive);
                using var image = new Image<Rgb24>(640, 360, new Rgb24(90, 120, 160));
                using var output = new MemoryStream();
                image.SaveAsPng(output);
                byte[] bytes = output.ToArray();
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version,
                    new BackgroundClassification(0.05f, 0.85f, 0.1f, 0, 0));
                store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.Anime.Version,
                    new BackgroundClassification(0.9f, 0.05f, 0.05f, 0, 0));
                original = new DisposableTexture(renderer.CreateTexture(640, 360));
                original.SetData(new TextureUpload(image.CloneAs<Rgba32>()));
                Child = background = new BeatmapBackground(new ImageBeatmap(bytes, hash, original))
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new Vector2(640, 360),
                };
            });
            AddUntilStep("original shown", () => background.IsLoaded && background.Sprite.Texture?.Width == 640);
            AddStep("enable explicit filter", () => config.SetValue(OsuSetting.ForkAiBackgroundBlur, true));
            AddUntilStep("worker replaces with proportional blurred thumbnail", () => background.Sprite.Texture?.Width == 320 && background.Sprite.Texture.Height == 180);
            AddAssert("no inference for persisted cache", () => service.InferenceCount == 0);
            AddStep("switch to anime-only", () => config.SetValue(OsuSetting.ForkAiBackgroundFilterMode, BackgroundFilterMode.Hentai));
            AddUntilStep("photo restored for anime-only", () => background.Sprite.Texture?.Width == 640 && background.Sprite.Texture.Available);
            AddStep("restore explicit policy", () => config.SetValue(OsuSetting.ForkAiBackgroundFilterMode, BackgroundFilterMode.Explicit));
            AddUntilStep("blur returns without inference", () => background.Sprite.Texture?.Width == 320 && service.InferenceCount == 0);
            AddStep("require both models", () => config.SetValue(OsuSetting.ForkAiCombination, BackgroundSafetyCombination.All));
            AddUntilStep("safe anime result reveals without inference", () => background.Sprite.Texture?.Width == 640 && service.InferenceCount == 0);
            AddStep("lower anime threshold", () => config.SetValue(OsuSetting.ForkAiAnimeThreshold, .01));
            AddUntilStep("threshold reuses prepared blur", () => background.Sprite.Texture?.Width == 320 && service.InferenceCount == 0);
            AddStep("change blur strength", () => config.SetValue(OsuSetting.ForkAiBlurStrength, 24.0));
            AddWaitStep("allow worker to prepare new blur", 10);
            AddUntilStep("new blur without inference", () => background.Sprite.Texture?.Width == 320 && service.InferenceCount == 0);
            AddStep("disable", () => config.SetValue(OsuSetting.ForkAiBackgroundBlur, false));
            AddUntilStep("original texture remains valid", () => background.Sprite.Texture?.Width == 640 && background.Sprite.Texture.Available);
            AddStep("dispose", () => Clear());
            AddStep("release test-owned texture", () => original.Dispose());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPreviewConsumesFullBackgroundResultImmediately(bool mutableFile)
        {
            BeatmapBackground background = null!;
            PanelSetBackground.PanelBeatmapBackground preview = null!;
            ImageBeatmap beatmap = null!;
            Texture original = null!;
            AddStep("prepare two surfaces for the same artwork", () =>
            {
                config.SetValue(OsuSetting.ForkAiBackgroundBlur, true);
                config.SetValue(OsuSetting.ForkAiCombination, BackgroundSafetyCombination.Any);
                config.SetValue(OsuSetting.ForkAiBackgroundFilterMode, BackgroundFilterMode.Suggestive);
                using var image = new Image<Rgb24>(640, 360, new Rgb24(90, 120, 160));
                using var output = new MemoryStream();
                image.SaveAsPng(output);
                byte[] bytes = output.ToArray();
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version, new BackgroundClassification(0, 1, 0, 0, 0));
                store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.Anime.Version, new BackgroundClassification(0, 1, 0, 0, 0));
                original = new DisposableTexture(renderer.CreateTexture(640, 360));
                original.SetData(new TextureUpload(image.CloneAs<Rgba32>()));
                beatmap = new ImageBeatmap(bytes, hash, original, mutableFile);
                Add(background = new BeatmapBackground(beatmap) { RelativeSizeAxes = Axes.None, Size = new Vector2(640, 360) });
            });
            AddUntilStep("full background processed", () => background.Sprite.Texture?.Width == 320);
            AddStep("load preview after full background", () => Add(preview = new PanelSetBackground.PanelBeatmapBackground(beatmap)
            {
                Size = new Vector2(160, 90),
            }));
            AddUntilStep("preview loaded", () => preview.IsLoaded);
            AddStep("preview uses ready result on first update", () =>
            {
                preview.UpdateSubTree();
                Assert.That(preview.Texture?.Width, Is.EqualTo(320));
                Assert.That(preview.Texture?.Height, Is.EqualTo(180));
                Assert.That(service.InferenceCount, Is.Zero);
            });
            AddStep("disable and clean up", () =>
            {
                config.SetValue(OsuSetting.ForkAiBackgroundBlur, false);
                Clear();
                original.Dispose();
            });
        }

        [Test]
        public void TestStillLifeReplacementAndSafetyPriority()
        {
            BeatmapBackground background = null!;
            Texture original = null!;
            string hash = string.Empty;
            StillLifeService stillLife = null!;
            AddStep("enable fake worker on the real background path", () =>
            {
                stillLife = Dependencies.Get<StillLifeService>();
                config.SetValue(OsuSetting.ForkAiStillLife, true);
                config.SetValue(OsuSetting.ForkAiBackgroundBlur, false);
                stillLife.GenerateForTest = (_, _, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var rgb = new byte[192 * 128 * 3];
                    for (int i = 0; i < rgb.Length; i += 3) rgb[i] = 200;
                    return new BackgroundImageCache.Artwork(rgb, 192, 128);
                };
                using var image = new Image<Rgb24>(640, 360, new Rgb24(30, 70, 90));
                using var output = new MemoryStream();
                image.SaveAsPng(output);
                byte[] bytes = output.ToArray();
                hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.General.Version, new BackgroundClassification(0, 1, 0, 0, 0));
                store.SetBackgroundClassification(hash, EnsembleBackgroundClassifier.Anime.Version, new BackgroundClassification(0, 1, 0, 0, 0));
                original = new DisposableTexture(renderer.CreateTexture(640, 360));
                original.SetData(new TextureUpload(image.CloneAs<Rgba32>()));
                Child = background = new BeatmapBackground(new ImageBeatmap(bytes, hash, original)) { Size = new Vector2(640, 360) };
            });
            AddUntilStep("generated texture replaces original", () => background.IsLoaded && background.Sprite.Texture?.Width == 192);
            AddStep("enable strict safety", () =>
            {
                config.SetValue(OsuSetting.ForkAiGeneralModel, true);
                config.SetValue(OsuSetting.ForkAiAnimeModel, true);
                config.SetValue(OsuSetting.ForkAiCombination, BackgroundSafetyCombination.Any);
                config.SetValue(OsuSetting.ForkAiBackgroundBlur, true);
            });
            AddUntilStep("unsafe original remains blurred instead of generated", () => background.Sprite.Texture?.Width == 320);
            AddStep("disable both options and release", () =>
            {
                config.SetValue(OsuSetting.ForkAiStillLife, false);
                config.SetValue(OsuSetting.ForkAiBackgroundBlur, false);
                stillLife.GenerateForTest = null;
            });
            AddUntilStep("original restored", () => background.Sprite.Texture?.Width == 640);
            AddStep("dispose", () => { Clear(); original.Dispose(); });
        }

        private sealed class ImageBeatmap : TestWorkingBeatmap
        {
            private readonly byte[] data;
            private readonly string hash;
            private readonly Texture texture;
            private readonly bool mutableFile;
            public ImageBeatmap(byte[] data, string hash, Texture texture, bool mutableFile = false) : base(new Beatmap())
            {
                this.data = data;
                this.hash = hash;
                this.texture = texture;
                this.mutableFile = mutableFile;
            }
            internal override Stream? OpenBackgroundStream() => new MemoryStream(data);
            internal override string? BackgroundContentHash => mutableFile ? null : hash;
            internal override string? BackgroundSafetyCacheKey => mutableFile ? "stable:test-artwork:revision" : hash;
            public override Texture? GetBackground() => new Texture(texture);
        }
    }
}
