using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Fonts;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Resources;
using SharpFNT;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MosuFontResourcesTest
    {
        [Test]
        public void TestOldResourceAssemblyContainsNoEmbeddedFonts()
        {
            Assert.That(OsuResources.ResourceAssembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("osu.Game.Resources.Fonts.", StringComparison.Ordinal)), Is.Empty);
        }

        [Test]
        public void TestDancIsNotBundledWithTheClient()
        {
            var assembly = typeof(OsuGameBase).Assembly;
            Assert.That(assembly.GetManifestResourceNames().Where(n =>
                n.EndsWith(".CustomFonts.danc.fnt", StringComparison.OrdinalIgnoreCase)
                || n.EndsWith(".CustomFonts.danc.png", StringComparison.OrdinalIgnoreCase)), Is.Empty);
            foreach (string retained in new[] { "Comfortaa Regular", "Cormorant Garamond", "Great Vibes Regular", "Kablammo Regular", "Rubik Glitch Regular" })
            foreach (string extension in new[] { ".fnt", ".png" })
                Assert.That(assembly.GetManifestResourceNames(), Does.Contain("osu.Game.Resources.CustomFonts." + retained + extension));
        }

        [Test]
        public void TestEveryLegacyFaceAndPageResolvesToNewBytes()
        {
            using var store = new MosuFontResources();
            foreach (var pair in MosuFontResources.LegacyFaces)
            {
                Assert.That(store.Get(pair.Key + ".bin"), Is.EqualTo(store.Get(pair.Value + ".bin")), pair.Key);
                using var stream = store.GetStream(pair.Key + ".bin");
                var font = BitmapFont.FromStream(stream, FormatHint.Binary, false);
                for (int page = 0; page < font.Pages.Count; page++)
                {
                    string suffix = "_" + page.ToString().PadLeft((font.Pages.Count - 1).ToString().Length, '0') + ".png";
                    Assert.That(store.Get(pair.Key + suffix), Is.EqualTo(store.Get(pair.Value + suffix)), pair.Key + suffix);
                }
            }
            Assert.That(store.Get("Fonts/Torus/LICENCE"), Is.Null);
            Assert.That(store.GetFontAssets().Count(), Is.EqualTo(38));
            Assert.That(store.GetFontAssets().First(), Is.EqualTo("Fonts/Nunito/Nunito-Regular"));
        }

        [TestCase("Fonts/Torus/Torus-Regular", "ЯёЇéệ", "Fonts/Nunito/Nunito-Medium")]
        [TestCase("Fonts/Torus-Alternate/Torus-Alternate-Bold", "ЯёЇάệ", "Fonts/MosuRoundedAlt/MosuRoundedAlt-Bold")]
        [TestCase("Fonts/Venera/Venera-Light", "AV0123456789", "Fonts/Syncopate/Syncopate-Regular")]
        [TestCase("Fonts/Venera/Venera-Bold", "AV0123456789", "Fonts/Michroma/Michroma-Regular")]
        [TestCase("Fonts/Venera/Venera-Black", "AV0123456789", "Fonts/Michroma/Michroma-Regular")]
        public async Task TestRealGlyphLoaderMetricsAndTextures(string oldAsset, string text, string newAsset)
        {
            using var resources = new ResourceStore<byte[]>(new MosuFontResources());
            using var loader = new TextureLoaderStore(resources);
            using var oldGlyphs = new GlyphStore(resources, oldAsset, loader);
            using var newGlyphs = new GlyphStore(resources, newAsset, loader);
            await oldGlyphs.LoadFontAsync();
            await newGlyphs.LoadFontAsync();
            foreach (char c in text)
            {
                Assert.That(oldGlyphs.HasGlyph(c), Is.True, $"{oldAsset} {c}");
                Assert.That(oldGlyphs.Get(c).XAdvance, Is.EqualTo(newGlyphs.Get(c).XAdvance));
                Assert.That(oldGlyphs.Get(c).Baseline, Is.EqualTo(newGlyphs.Get(c).Baseline));
                using var texture = oldGlyphs.Get(oldGlyphs.FontName + "/" + c);
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.Width, Is.GreaterThan(0));
            }
        }

        [Test]
        public async Task TestMissingCyrillicFallsBackButNunitoKeepsOwnGlyphs()
        {
            using var resources = new ResourceStore<byte[]>(new MosuFontResources());
            using var loader = new TextureLoaderStore(resources);
            using var nunito = new GlyphStore(resources, "Fonts/Nunito/Nunito-Regular", loader);
            using var michroma = new GlyphStore(resources, "Fonts/Michroma/Michroma-Regular", loader);
            await nunito.LoadFontAsync();
            await michroma.LoadFontAsync();
            using var fonts = new FontStore(new DummyRenderer(), minFilterMode: TextureFilteringMode.Linear);
            fonts.AddTextureSource(nunito);
            fonts.AddTextureSource(michroma);
            var own = fonts.Get("Nunito-Regular", 'ё');
            Assert.That(own, Is.Not.Null);
            Assert.That(fonts.Get("Michroma-Regular", 'ё'), Is.Null);
            // This is TextBuilder's final fallback lookup; there is no forced Comfortaa substitution.
            Assert.That(fonts.Get(string.Empty, 'ё'), Is.Not.Null);
            Assert.That(fonts.Get(string.Empty, 'ё')!.XAdvance, Is.EqualTo(own!.XAdvance));
        }

        [TestCase("Fonts/Nunito/Nunito-Regular", "Nunito-Regular", 0.60f, 0.84f)]
        [TestCase("Fonts/Nunito/Nunito-Bold", "Nunito-Bold", 0.60f, 0.84f)]
        [TestCase("Fonts/MosuRoundedAlt/MosuRoundedAlt-Regular", "MosuRoundedAlt-Regular", 0.60f, 0.84f)]
        [TestCase("Fonts/Syncopate/Syncopate-Regular", "Syncopate-Regular", 0.69f, 0.75f)]
        [TestCase("Fonts/Michroma/Michroma-Regular", "Michroma-Regular", 0.69f, 0.75f)]
        [TestCase("Fonts/Torus/Torus-Regular", "Torus-Regular", 0.60f, 0.84f)]
        [TestCase("Fonts/Torus-Alternate/Torus-Alternate-Regular", "Torus-Alternate-Regular", 0.60f, 0.84f)]
        [TestCase("Fonts/Venera/Venera-Bold", "Venera-Bold", 0.69f, 0.75f)]
        public async Task TestClientCapHeightBaselineAndUniformMetrics(string asset, string name, float capHeight, float baseline)
        {
            using var resources = new ResourceStore<byte[]>(new MosuFontResources());
            using var loader = new TextureLoaderStore(resources);
            using var glyphs = new GlyphStore(resources, asset, loader);
            await glyphs.LoadFontAsync();
            using var raw = new FontStore(new DummyRenderer(), minFilterMode: TextureFilteringMode.Linear);
            using var adjusted = new MosuFontStore(new DummyRenderer());
            // Test the same interface dispatch as the client's nested FontStore.
            using var parent = new FontStore(new DummyRenderer(), minFilterMode: TextureFilteringMode.Linear);
            raw.AddTextureSource(glyphs);
            adjusted.AddTextureSource(glyphs);
            parent.AddStore(adjusted);
            var original = raw.Get(name, 'H')!;
            var fitted = parent.Get(name, 'H')!;
            Assert.That(fitted.Height, Is.EqualTo(capHeight).Within(0.00001));
            Assert.That(fitted.Baseline, Is.EqualTo(baseline).Within(0.00001));
            float scale = fitted.Height / original.Height;
            float tracking = capHeight == 0.60f ? 0.004f : 0;
            Assert.That(fitted.XAdvance, Is.EqualTo(original.XAdvance * scale + tracking).Within(0.00001));
            Assert.That(fitted.XOffset, Is.EqualTo(original.XOffset * scale + tracking / 2).Within(0.00001));
            foreach (char nonSpacing in new[] { ' ', '\u0301' })
            {
                var before = raw.Get(name, nonSpacing);
                if (before != null)
                    Assert.That(parent.Get(name, nonSpacing)!.XAdvance, Is.EqualTo(before.XAdvance * scale).Within(0.00001));
            }
            Assert.That(fitted.YOffset, Is.EqualTo(baseline + (original.YOffset - original.Baseline) * scale).Within(0.00001));
            Assert.That(parent.Get(string.Empty, 'H')!.Height, Is.EqualTo(fitted.Height));
            Assert.That(parent.Get(name.Split('-').Last(), 'H')!.Height, Is.EqualTo(fitted.Height));
            Assert.That(parent.Get("-" + name.Split('-').Last(), 'H')!.Height, Is.EqualTo(fitted.Height));
            // TextBuilder's real no-family fallback retains the leading hyphen.
            foreach (char c in "НхРё")
            {
                var direct = parent.Get(name, c);
                if (direct == null) continue;
                var fallback = parent.Get("-" + name.Split('-').Last(), c)!;
                Assert.That(fallback.Height, Is.EqualTo(direct.Height), name + c);
                Assert.That(fallback.Baseline, Is.EqualTo(direct.Baseline), name + c);
                Assert.That(fallback.YOffset, Is.EqualTo(direct.YOffset), name + c);
                Assert.That(fallback.XAdvance, Is.EqualTo(direct.XAdvance), name + c);
            }
            Assert.That((await parent.GetAsync(name, 'H'))!.Height, Is.EqualTo(fitted.Height));
            var left = raw.Get(name, 'A')!;
            Assert.That(parent.Get(name, 'V')!.GetKerning(left), Is.EqualTo(raw.Get(name, 'V')!.GetKerning(left) * scale).Within(0.00001));
        }

        [TestCase("Light", "Light", "Light")]
        [TestCase("Regular", "Medium", "SemiBold")]
        [TestCase("SemiBold", "Bold", "Bold")]
        [TestCase("Bold", "Black", "Bold")]
        public void TestLegacyVisualWeightMapping(string requested, string nunito, string rounded)
        {
            Assert.That(MosuFontResources.LegacyFaces[$"Fonts/Torus/Torus-{requested}"], Is.EqualTo($"Fonts/Nunito/Nunito-{nunito}"));
            Assert.That(MosuFontResources.LegacyFaces[$"Fonts/Torus-Alternate/Torus-Alternate-{requested}"], Is.EqualTo($"Fonts/MosuRoundedAlt/MosuRoundedAlt-{rounded}"));
        }

        [Test]
        public async Task TestLatinLegacyHeadingsUseHeavierFacesWithoutChangingCyrillic()
        {
            using var resources = new ResourceStore<byte[]>(new MosuFontResources());
            using var loader = new TextureLoaderStore(resources);
            using var fonts = new MosuFontStore(new DummyRenderer());
            foreach (string asset in new[]
                     {
                         "Fonts/MosuRoundedAlt/MosuRoundedAlt-Bold",
                         "Fonts/Nunito/Nunito-Bold", "Fonts/Nunito/Nunito-Black",
                         "Fonts/Torus-Alternate/Torus-Alternate-SemiBold", "Fonts/Torus-Alternate/Torus-Alternate-Bold",
                     })
            {
                var glyphs = new GlyphStore(resources, asset, loader);
                await glyphs.LoadFontAsync();
                fonts.AddTextureSource(glyphs);
            }
            foreach (var (requested, actual) in new[] { ("SemiBold", "Bold"), ("Bold", "Black") })
            {
                foreach (char c in "HyakkiYakou0123-éệ")
                {
                    var heading = fonts.Get("Torus-Alternate-" + requested, c)!;
                    var source = fonts.Get("Nunito-" + actual, c)!;
                    Assert.That(heading.Texture, Is.SameAs(source.Texture));
                    Assert.That(heading.XAdvance, Is.EqualTo(source.XAdvance));
                    Assert.That(heading.Baseline, Is.EqualTo(source.Baseline));
                }
                foreach (char c in "РусскийЁж")
                {
                    var heading = fonts.Get("Torus-Alternate-" + requested, c)!;
                    var source = fonts.Get("MosuRoundedAlt-Bold", c)!;
                    // Aliases are distinct Texture objects over identical atlas bytes.
                    Assert.That(heading.Width, Is.EqualTo(source.Width));
                    Assert.That(heading.Height, Is.EqualTo(source.Height));
                    Assert.That(heading.XOffset, Is.EqualTo(source.XOffset));
                    Assert.That(heading.YOffset, Is.EqualTo(source.YOffset));
                    Assert.That(heading.XAdvance, Is.EqualTo(source.XAdvance));
                }
            }
            // Accepted Russian tracking remains 0.018em, Latin reduces to 0.004em.
            using var raw = new FontStore(new DummyRenderer(), minFilterMode: TextureFilteringMode.Linear);
            var rawFace = new GlyphStore(resources, "Fonts/MosuRoundedAlt/MosuRoundedAlt-Bold", loader);
            await rawFace.LoadFontAsync();
            raw.AddTextureSource(rawFace);
            float scale = 0.60f / raw.Get("MosuRoundedAlt-Bold", 'H')!.Height;
            foreach (var (c, tracking) in new[] { ('ё', 0.018f), ('l', 0.004f) })
                Assert.That(fonts.Get("MosuRoundedAlt-Bold", c)!.XAdvance,
                    Is.EqualTo(raw.Get("MosuRoundedAlt-Bold", c)!.XAdvance * scale + tracking).Within(0.00001));
        }

        [Test]
        public async Task TestRetainedFontMetricsAreNotNormalised()
        {
            using var resources = new ResourceStore<byte[]>(new MosuFontResources());
            using var loader = new TextureLoaderStore(resources);
            using var glyphs = new GlyphStore(resources, "Fonts/Inter/Inter-Regular", loader);
            await glyphs.LoadFontAsync();
            using var raw = new FontStore(new DummyRenderer(), minFilterMode: TextureFilteringMode.Linear);
            using var fitted = new MosuFontStore(new DummyRenderer());
            raw.AddTextureSource(glyphs);
            fitted.AddTextureSource(glyphs);
            foreach (char character in "HёжAV0")
            {
                var before = raw.Get("Inter-Regular", character)!;
                var after = fitted.Get("Inter-Regular", character)!;
                Assert.That(after.Height, Is.EqualTo(before.Height));
                Assert.That(after.XAdvance, Is.EqualTo(before.XAdvance));
                Assert.That(after.YOffset, Is.EqualTo(before.YOffset));
                Assert.That(after.Baseline, Is.EqualTo(before.Baseline));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestLegacyRussianFontSettingIsIgnoredInConfigAndTheme(bool enabled)
        {
            using var storage = new TemporaryNativeStorage("removed-russian-font-fix");
            using (var stream = storage.GetStream("mosu.ini", FileAccess.Write, FileMode.Create))
            using (var writer = new StreamWriter(stream))
                writer.WriteLine($"ForkRussianFontFix = {enabled}\nForkCustomUIFont = Inter");
            using var config = new OsuConfigManager(storage);
            Assert.That(config.Get<string>(OsuSetting.ForkCustomUIFont), Is.EqualTo("Inter"));
            var settings = JsonConvert.DeserializeObject<ThemeInterfaceSettings>("{\"font\":\"Nunito\",\"russianFontFix\":" + enabled.ToString().ToLowerInvariant() + "}")!;
            settings.Apply(config);
            Assert.That(config.Get<string>(OsuSetting.ForkCustomUIFont), Is.EqualTo("Nunito"));
            Assert.That(JsonConvert.SerializeObject(ThemeInterfaceSettings.Capture(config)), Does.Not.Contain("russianFontFix"));
            Assert.That(Enum.GetNames<OsuSetting>(), Does.Not.Contain("ForkRussianFontFix"));
        }
    }
}
