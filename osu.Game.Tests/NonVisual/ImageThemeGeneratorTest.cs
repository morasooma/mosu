using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Rulesets.Mods;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ImageThemeGeneratorTest
    {
        private static byte[] image(Rgba32 fill)
        {
            using var bitmap = new Image<Rgba32>(64, 64, fill);
            using var stream = new MemoryStream();
            bitmap.SaveAsPng(stream);
            return stream.ToArray();
        }

        [TestCase(220, 30, 60)]
        [TestCase(30, 180, 90)]
        [TestCase(40, 80, 220)]
        public void TestExtractsImageHue(int r, int g, int b)
        {
            var result = ImageThemeGenerator.Analyse(image(new Rgba32((byte)r, (byte)g, (byte)b)));
            Assert.That(result.Colours.Count, Is.InRange(1, 4));
            var actual = result.Colours[0];
            Assert.That((actual >> 16) & 255, Is.EqualTo(r).Within(8));
            Assert.That((actual >> 8) & 255, Is.EqualTo(g).Within(8));
            Assert.That(actual & 255, Is.EqualTo(b).Within(8));
            using var thumb = Image.Load(result.Thumbnail);
            Assert.That(thumb.Width, Is.LessThanOrEqualTo(128));
        }

        [TestCase(0)]
        [TestCase(120)]
        [TestCase(255)]
        public void TestMonochromeNeverFallsBackToBlue(int tone)
        {
            var result = ImageThemeGenerator.Analyse(image(new Rgba32((byte)tone, (byte)tone, (byte)tone)));
            Assert.That(result.Colours[0], Is.Not.EqualTo(0xff4285f4u));
            var theme = ImageThemeGenerator.Generate(result.Colours[0], new CustomThemeDefinition(), new ThemeInterfaceSettings(), false);
            theme.TryGetOverride(OverlayColour.Highlight1, out var accent);
            Assert.That(Math.Abs(accent.R - accent.G), Is.LessThan(0.025));
            Assert.That(Math.Abs(accent.G - accent.B), Is.LessThan(0.025));
        }

        [Test]
        public void TestRejectsTransparentInvalidAndOversizedImages()
        {
            Assert.Throws<InvalidDataException>(() => ImageThemeGenerator.Analyse(image(new Rgba32(0, 0, 0, 0))));
            Assert.Throws<InvalidDataException>(() => ImageThemeGenerator.Analyse(Array.Empty<byte>()));
            Assert.Throws<InvalidDataException>(() => ImageThemeGenerator.Analyse(new byte[ImageThemeGenerator.MAXIMUM_BYTES + 1]));
            Assert.Throws<UnknownImageFormatException>(() => ImageThemeGenerator.Analyse(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void TestGenerationRetainsEntireAppearanceAndDoesNotMutateSource()
        {
            var original = new CustomThemeDefinition(ThemeMode.Dark)
            {
                Geometry = new ThemeGeometryDefinition { ButtonRadius = 3, CardRadius = 21, PanelRadius = 17, TopBarVariant = TopBarVariant.Attached, EnableGradients = false },
                Extensions = new System.Collections.Generic.Dictionary<string, JToken> { ["future"] = "keep" },
            };
            original.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.Accent, Colour4.Red);
            original.SetModTypeOverride(ModType.DifficultyIncrease, Colour4.Red);
            var appearance = new ThemeInterfaceSettings
            {
                Font = "Inter", OverlayTransparency = true, SongSelectTransparency = true,
                BlurStrength = 0.3, DimAmount = 0.6, Animations = InterfaceAnimationMode.Minimal,
                SongSelectStyle = ForkSongSelectStyle.InfiniteGlass, DisableShear = false,
            };
            string before = original.ToJson();
            string interfaceBefore = JsonConvert.SerializeObject(appearance);
            var result = ImageThemeGenerator.Generate(0xff43a487, original, appearance, false);
            Assert.That(original.ToJson(), Is.EqualTo(before));
            Assert.That(JsonConvert.SerializeObject(appearance), Is.EqualTo(interfaceBefore));
            Assert.That(result.Geometry, Is.EqualTo(original.Geometry).And.Not.SameAs(original.Geometry));
            Assert.That(JsonConvert.SerializeObject(result.Interface), Is.EqualTo(interfaceBefore));
            Assert.That(result.ModTypeOverrides, Is.EqualTo(original.ModTypeOverrides));
            Assert.That(result.Extensions!["future"].ToString(), Is.EqualTo("keep"));
            Assert.That(result.Overrides.Count, Is.EqualTo(Enum.GetValues<OverlayColour>().Length));
            Assert.That(result.ContextOverrides.Keys, Is.EquivalentTo(Enum.GetValues<ThemeSurfaceContext>()));
            Assert.That(result.ContextOverrides[ThemeSurfaceContext.Editor][SurfaceColourRole.Accent], Is.Not.EqualTo(Colour4.Red.ToHex()));
            Assert.That(CustomThemeDefinition.FromJson(result.ToJson()), Is.EqualTo(result));
        }

        [TestCase(ImageThemeStyle.Rounded, 12, TopBarVariant.Floating)]
        [TestCase(ImageThemeStyle.Sharp, 0, TopBarVariant.Flat)]
        public void TestShapePresetsKeepFontMotionTransparencyAndLayout(ImageThemeStyle style, float radius, TopBarVariant bar)
        {
            var appearance = new ThemeInterfaceSettings { Font = "Inter", Animations = InterfaceAnimationMode.Minimal, OverlayTransparency = true, BlurStrength = 0.7, SongSelectStyle = ForkSongSelectStyle.Classic2024 };
            var theme = ImageThemeGenerator.Generate(0xff8844bb, new CustomThemeDefinition(), appearance, false, style);
            Assert.That(theme.Geometry.ButtonRadius, Is.EqualTo(radius));
            Assert.That(theme.TopBarVariant, Is.EqualTo(bar));
            Assert.That(theme.Interface!.Font, Is.EqualTo("Inter"));
            Assert.That(theme.Interface.Animations, Is.EqualTo(appearance.Animations));
            Assert.That(theme.Interface.OverlayTransparency, Is.True);
            Assert.That(theme.Interface.BlurStrength, Is.EqualTo(0.7));
            Assert.That(theme.Interface.SongSelectStyle, Is.EqualTo(appearance.SongSelectStyle));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReadableTextOnGeneratedSurfaces(bool light)
        {
            foreach (uint seed in new uint[] { 0xffdf2345, 0xff368b43, 0xff315bdd, 0xffd4bc35, 0xff777777, 0xff000000, 0xffffffff })
            {
                var theme = ImageThemeGenerator.Generate(seed, new CustomThemeDefinition(), new ThemeInterfaceSettings(), light);
                theme.TryGetOverride(OverlayColour.Content1, out var text);
                theme.TryGetOverride(OverlayColour.Content2, out var secondary);
                foreach (var token in new[] { OverlayColour.Background1, OverlayColour.Background2, OverlayColour.Background3, OverlayColour.Background4, OverlayColour.Background5, OverlayColour.Background6, OverlayColour.Light4 })
                {
                    theme.TryGetOverride(token, out var surface);
                    Assert.That(contrast(text, surface), Is.GreaterThanOrEqualTo(4.5), $"{seed:X8} {token} primary");
                    Assert.That(contrast(secondary, surface), Is.GreaterThanOrEqualTo(4.5), $"{seed:X8} {token} secondary");
                }
            }
        }

        [Test]
        public void TestSourceUrlsAreDistinctAndBackgroundDeserializes()
        {
            var user = JsonConvert.DeserializeObject<APIUser>("{\"avatar_url\":\"a\",\"cover_url\":\"b\",\"profile_background_url\":\"c\"}")!;
            Assert.That(new[] { ImageThemeSource.Avatar, ImageThemeSource.Banner, ImageThemeSource.Background }.Select(s => ImageThemeGenerator.GetSourceUrl(user, s)), Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void TestCurrentBackgroundStreamAndLimit()
        {
            using var stream = new MemoryStream(image(new Rgba32(30, 180, 90)));
            Assert.That(ImageThemeGenerator.Analyse(stream).Colours, Is.Not.Empty);
            using var oversized = new MemoryStream(new byte[ImageThemeGenerator.MAXIMUM_BYTES + 1]);
            Assert.Throws<InvalidDataException>(() => ImageThemeGenerator.Analyse(oversized));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestFooterActionsRetainDistinctOriginalTints(bool light)
        {
            var result = ImageThemeGenerator.Generate(0xff987632, new CustomThemeDefinition(), new ThemeInterfaceSettings(), light);
            var accents = new[] { ThemeSurfaceContext.FooterMods, ThemeSurfaceContext.FooterRandom, ThemeSurfaceContext.FooterOptions }
                .Select(c => result.ContextOverrides[c][SurfaceColourRole.Accent]).ToArray();
            Assert.That(accents.Distinct().Count(), Is.EqualTo(3));
            foreach (var context in new[] { ThemeSurfaceContext.FooterMods, ThemeSurfaceContext.FooterRandom, ThemeSurfaceContext.FooterOptions })
            {
                result.TryGetContextOverride(context, SurfaceColourRole.TextPrimary, out var text);
                result.TryGetContextOverride(context, SurfaceColourRole.ControlIdle, out var surface);
                Assert.That(contrast(text, surface), Is.GreaterThanOrEqualTo(4.5));
            }
        }

        [TestCase(false, 0xff987632u)]
        [TestCase(true, 0xff987632u)]
        [TestCase(false, 0xff5078c8u)]
        [TestCase(true, 0xff5078c8u)]
        public void TestFooterTintsDoNotLiftSurfaceLightness(bool light, uint seed)
        {
            var result = ImageThemeGenerator.Generate(seed, new CustomThemeDefinition(), new ThemeInterfaceSettings(), light);
            result.TryGetOverride(OverlayColour.Background2, out var idleBase);
            result.TryGetOverride(OverlayColour.Light4, out var hoverBase);
            result.TryGetOverride(OverlayColour.Colour1, out var accentBase);
            foreach (var context in new[] { ThemeSurfaceContext.FooterMods, ThemeSurfaceContext.FooterRandom, ThemeSurfaceContext.FooterOptions })
            {
                result.TryGetContextOverride(context, SurfaceColourRole.ControlIdle, out var idle);
                result.TryGetContextOverride(context, SurfaceColourRole.ControlHover, out var hover);
                result.TryGetContextOverride(context, SurfaceColourRole.Accent, out var accent);
                Assert.That(tone(idle), Is.EqualTo(tone(idleBase)).Within(0.6), $"{context} idle");
                Assert.That(tone(hover), Is.EqualTo(tone(hoverBase)).Within(0.6), $"{context} hover");
                Assert.That(tone(accent), Is.EqualTo(tone(accentBase)).Within(0.6), $"{context} accent");
            }

            static double tone(Colour4 c) => MaterialTheming.HctColor.FromRgbColor(
                MaterialTheming.RgbColor.FromArgb(unchecked((int)(0xff000000u | (uint)Math.Round(c.R * 255) << 16
                    | (uint)Math.Round(c.G * 255) << 8 | (uint)Math.Round(c.B * 255))))).Tone;
        }

        [Test]
        public void TestSaveCreatesIndependentFullTheme()
        {
            using var storage = new TemporaryNativeStorage("image-theme");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkCustomUIFont, "Inter");
            config.SetValue(OsuSetting.ForkOverlayTransparency, true);
            string previous = config.CustomThemes.ActiveId;
            var original = config.CustomThemes.Active.Theme.DeepClone();
            var generated = ImageThemeGenerator.Generate(0xff987632, original, ThemeInterfaceSettings.Capture(config), false, ImageThemeStyle.Rounded);
            config.CustomThemes.Create("From banner", generated);
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
            Assert.That(config.CustomThemes.Entries.Count, Is.EqualTo(2));
            Assert.That(config.Get<string>(OsuSetting.ForkCustomUIFont), Is.EqualTo("Inter"));
            Assert.That(config.Get<bool>(OsuSetting.ForkOverlayTransparency), Is.True);
            config.CustomThemes.Select(previous);
            Assert.That(config.CustomThemes.Active.Theme, Is.EqualTo(original));
        }

        private static double contrast(Colour4 a, Colour4 b)
        {
            static double channel(float c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            static double luminance(Colour4 c) => 0.2126 * channel(c.R) + 0.7152 * channel(c.G) + 0.0722 * channel(c.B);
            double x = luminance(a), y = luminance(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }
    }
}
