using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ThemePreviewRendererTest
    {
        [Test]
        public void TestCaptureProcessIsIsolated()
        {
            const string key = "MOSU_THEME_CAPTURE_TEST";
            string? previous = Environment.GetEnvironmentVariable(key);
            try
            {
                Environment.SetEnvironmentVariable(key, "must not propagate");
                var start = ThemePreviewRenderer.CreateStartInfo("/tmp/theme capture");
                Assert.That(start.Arguments, Does.Contain(ThemePreviewRenderer.ARGUMENT).And.Contain("\"/tmp/theme capture\""));
                Assert.That(start.Environment.ContainsKey(key), Is.False);
                Assert.That(start.RedirectStandardError && start.RedirectStandardOutput && start.CreateNoWindow, Is.True);
                Assert.That(start.UseShellExecute, Is.False);
            }
            finally { Environment.SetEnvironmentVariable(key, previous); }
        }

        [TestCase(ThemeMode.Default)]
        [TestCase(ThemeMode.Custom)]
        public void TestCaptureAppliesEveryPortableAppearanceField(ThemeMode previousMode)
        {
            using var storage = new TemporaryNativeStorage("theme-capture-appearance");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkThemeMode, previousMode);
            var theme = new CustomThemeDefinition
            {
                Interface = new ThemeInterfaceSettings
                {
                    DisableShear = true, SongSelectStyle = ForkSongSelectStyle.InfiniteGlass, OldCarouselPreviews = true,
                    Animations = InterfaceAnimationMode.Minimal, EnhancedAnimations = true,
                    OverlayTransparency = true, SongSelectTransparency = true,
                    BlurStrength = 0.37, DimAmount = 0.62, Font = "Inter",
                },
            };
            // Exercise the same JSON boundary used between the parent and capture process.
            ThemePreviewGame.ApplyCaptureTheme(config, CustomThemeDefinition.FromJson(theme.ToJson()));
            Assert.That(config.Get<ThemeMode>(OsuSetting.ForkThemeMode), Is.EqualTo(ThemeMode.Custom));
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(ThemeInterfaceSettings.Capture(config)),
                Is.EqualTo(Newtonsoft.Json.JsonConvert.SerializeObject(theme.Interface)));
            Assert.That(CustomThemeDefinition.FromJson(config.Get<string>(OsuSetting.ForkCustomThemeJson)).Interface,
                Is.Not.Null);
        }

        [Test]
        public void TestCaptureLegacyThemeKeepsBaselineAppearance()
        {
            using var storage = new TemporaryNativeStorage("theme-capture-legacy");
            using var config = new OsuConfigManager(storage);
            var baseline = Newtonsoft.Json.JsonConvert.SerializeObject(ThemeInterfaceSettings.Capture(config));
            ThemePreviewGame.ApplyCaptureTheme(config, new CustomThemeDefinition());
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(ThemeInterfaceSettings.Capture(config)), Is.EqualTo(baseline));
        }

        [Test]
        public void TestPreviewValidation()
        {
            using var image = new Image<Rgba32>(640, 360);
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            Assert.DoesNotThrow(() => ThemePreviewRenderer.Validate(stream.ToArray()));
            Assert.Throws<InvalidOperationException>(() => ThemePreviewRenderer.Validate(new byte[ThemePreviewRenderer.MAXIMUM_BYTES + 1]));
            using var wrongSize = new Image<Rgba32>(32, 32);
            using var wrongStream = new MemoryStream();
            wrongSize.SaveAsPng(wrongStream);
            Assert.Throws<InvalidOperationException>(() => ThemePreviewRenderer.Validate(wrongStream.ToArray()));
        }

        [Test]
        public void TestInstallingDoesNotSelectOrApplyTheme()
        {
            using var storage = new TemporaryNativeStorage("theme-install-no-apply");
            using var config = new OsuConfigManager(storage);
            string previous = config.CustomThemes.ActiveId;
            string json = config.Get<string>(OsuSetting.ForkCustomThemeJson);
            config.CustomThemes.Create("Downloaded", new CustomThemeDefinition(ThemeMode.Light), select: false);
            Assert.That(config.CustomThemes.Entries.Count, Is.EqualTo(2));
            Assert.That(config.CustomThemes.ActiveId, Is.EqualTo(previous));
            Assert.That(config.Get<string>(OsuSetting.ForkCustomThemeJson), Is.EqualTo(json));
            Assert.That(config.Get<ThemeMode>(OsuSetting.ForkThemeMode), Is.EqualTo(ThemeMode.Default));
        }
    }
}
