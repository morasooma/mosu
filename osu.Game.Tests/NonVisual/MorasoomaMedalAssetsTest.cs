using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Users;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MorasoomaMedalAssetsTest
    {
        [Test]
        public void TestAllReviewedMedalsResolveOfflineThroughProductionTextureNamespace()
        {
            using var independent = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            var manifest = JObject.Parse(Encoding.UTF8.GetString(independent.Get("medal-manifest.json")));
            var medals = (JArray)manifest["medals"]!;
            Assert.That(medals.Count, Is.EqualTo(132));
            using var resources = new NamespacedResourceStore<byte[]>(
                new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "MedalOverrides"), "Textures");
            using var textures = new LargeTextureStore(new DummyRenderer(), new TextureLoaderStore(resources));
            foreach (var medal in medals)
            {
                string slug = (string)medal["slug"]!;
                string key = MorasoomaMedalAssets.Resolve(slug, $"https://assets.ppy.sh/medals/client/{slug}@2x.png")!;
                Assert.That(key, Is.EqualTo($"Medals/{slug}@2x.png"));
                Assert.That(new Medal { InternalName = slug, CoverUrl = "https://old.test/medal.png" }.ImageUrl, Is.EqualTo(key));
                byte[] bytes = resources.Get(key);
                Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                    Is.EqualTo((string)medal["images"]!["2x"]!["sha256"]!), slug);
                var texture = textures.Get(key);
                Assert.That(texture, Is.Not.Null, slug);
                Assert.That(texture.DisplayWidth, Is.GreaterThan(0), slug);
                Assert.That(texture.DisplayHeight, Is.GreaterThan(0), slug);
            }
            Assert.That(medals.Select(m => (int)m["id"]!), Does.Not.Contain(105));
            Assert.That(independent.Get("Licenses/Medals/Phosphor-MIT-LICENSE.txt"), Is.Not.Null);
            Assert.That(independent.Get("Licenses/Medals/ppy-MIT-LICENSE.txt"), Is.Not.Null);
        }

        [Test]
        public void TestUnknownMedalRetainsExplicitCoverWithoutInventingUpstreamUrl()
        {
            Assert.That(MorasoomaMedalAssets.Resolve("custom", "https://custom.test/medal.png"), Is.EqualTo("https://custom.test/medal.png"));
            Assert.That(MorasoomaMedalAssets.Resolve("custom"), Is.Null);
            Assert.That(MorasoomaMedalAssets.Resolve("../osu-skill-pass-1"), Is.Null);
        }
    }
}
