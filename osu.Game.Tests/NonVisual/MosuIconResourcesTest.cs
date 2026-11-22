using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Morasooma.Resources;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Graphics;
using osu.Game.Resources;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MosuIconResourcesTest
    {
        [Test]
        public void TestNavigationOutlinesResolveAsIconGlyphs()
        {
            using var own = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            var manifest = JObject.Parse(System.Text.Encoding.UTF8.GetString(own.Get("navigation-icon-manifest.json")));
            Assert.That(manifest["icons"]!.Count(), Is.EqualTo(13));
            using var resources = new NamespacedResourceStore<byte[]>(own, "IconOverrides/Textures");
            using var textures = new TextureStore(new DummyRenderer(), new TextureLoaderStore(resources), useAtlas: false);
            var glyphs = new OsuIcon.OsuIconStore(textures);
            foreach (var property in typeof(OsuIcon).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.Name.StartsWith("Navigation", StringComparison.Ordinal)))
            {
                var usage = (IconUsage)property.GetValue(null)!;
                var glyph = glyphs.Get(usage.FontName, usage.Icon);
                Assert.That(glyph, Is.Not.Null, property.Name);
                Assert.That(glyph!.Texture.Width, Is.EqualTo(100));
            }
            Assert.That(new osu.Game.Overlays.CustomTheme.ThemeLibraryOverlay().Icon, Is.EqualTo(OsuIcon.NavigationPalette));
            Assert.That(new osu.Game.Overlays.Discovery.DiscoveryOverlay().Icon, Is.EqualTo(OsuIcon.NavigationCompass));
            Assert.That(new osu.Game.Overlays.SupporterQuestsOverlay().Icon, Is.EqualTo(OsuIcon.NavigationCrown));
            foreach (var row in manifest["icons"]!)
            {
                byte[] data = own.Get("IconOverrides/" + (string)row["resource"]!);
                Assert.That(Convert.ToHexStringLower(SHA256.HashData(data)), Is.EqualTo((string)row["sha256"]!));
            }
        }

        [Test]
        public async Task TestApprovedIconsResolveThroughProductionResourceAndGlyphStores()
        {
            using var independent = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            var entries = JArray.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("icon-manifest.json")));
            Assert.That(entries.Count, Is.EqualTo(170));
            using var resources = new ResourceStore<byte[]>();
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "IconOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));
            resources.AddStore(new DllResourceStore(OsuResources.ResourceAssembly));
            using var textureResources = new NamespacedResourceStore<byte[]>(resources, "Textures");
            var renderer = new DummyRenderer();
            using var textures = new TextureStore(renderer, new TextureLoaderStore(textureResources), useAtlas: false);
            var icons = new OsuIcon.OsuIconStore(textures);
            int glyphCount = 0;
            foreach (var entry in entries)
            {
                string key = (string)entry["resource"]!;
                byte[] bytes = resources.Get(key);
                Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), Is.EqualTo((string)entry["sha256"]!), key);
                Assert.That(await resources.GetAsync(key), Is.EqualTo(bytes), key);
                using Stream stream = resources.GetStream(key);
                Assert.That(stream, Is.Not.Null, key);
                var texture = textures.Get(key["Textures/".Length..^4]);
                Assert.That(texture, Is.Not.Null, key);
                // TextureStore's established 2x scale is unchanged; padding/canvas is not trimmed.
                Assert.That(texture.Width, Is.EqualTo((int)entry["width"]!));
                Assert.That(texture.Height, Is.EqualTo((int)entry["height"]!));
                var property = typeof(OsuIcon).GetProperty((string)entry["name"]!, BindingFlags.Public | BindingFlags.Static);
                if (property == null)
                    continue; // Beatmap-detail/editor Sprite keys are not all IconUsage properties.
                var usage = (IconUsage)property.GetValue(null)!;
                var glyph = icons.Get(usage.FontName, usage.Icon);
                Assert.That(glyph, Is.Not.Null, property.Name);
                Assert.That(glyph!.Texture, Is.SameAs(texture), property.Name);
                glyphCount++;
            }
            Assert.That(glyphCount, Is.EqualTo(153));
            var replaced = entries.Select(e => (string)e["resource"]!).ToArray();
            Assert.That(replaced.Any(k => k.Contains("mosu-", StringComparison.Ordinal) || k.Contains("RulesetDodge", StringComparison.Ordinal)), Is.False);
            // The owner-original Dodge icon continues to resolve exactly as before.
            using var upstream = new DllResourceStore(OsuResources.ResourceAssembly);
            Assert.That(resources.Get("Textures/Icons/RulesetDodge.png"), Is.EqualTo(upstream.Get("Textures/Icons/RulesetDodge.png")));
        }
    }
}
