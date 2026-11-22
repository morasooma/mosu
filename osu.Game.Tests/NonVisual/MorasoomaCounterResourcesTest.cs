using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Resources;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MorasoomaCounterResourcesTest
    {
        [Test]
        public void TestCounterBankAndRetiredAssetsThroughActualResourceStores()
        {
            using var own = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            using var upstream = new DllResourceStore(OsuResources.ResourceAssembly);
            using var resources = new ResourceStore<byte[]>();
            resources.AddStore(new NamespacedResourceStore<byte[]>(own, "CounterOverrides"));
            resources.AddStore(upstream);
            using var textureResources = new NamespacedResourceStore<byte[]>(resources, "Textures");
            using var textures = new TextureStore(new DummyRenderer(), new TextureLoaderStore(textureResources));
            var manifest = JObject.Parse(Encoding.UTF8.GetString(own.Get("counter-manifest.json")));
            Assert.That((bool)manifest["upstream_pixels_used"]!, Is.False);
            var files = (JArray)manifest["files"]!;
            Assert.That(files.Count, Is.EqualTo(14));
            foreach (var file in files)
            {
                string resource = (string)file["resource"]!;
                Assert.That(Convert.ToHexString(SHA256.HashData(resources.Get(resource))).ToLowerInvariant(),
                    Is.EqualTo((string)file["sha256"]!), resource);
                var texture = textures.Get(resource.Substring("Textures/".Length));
                Assert.That(texture, Is.Not.Null, resource);
                Assert.That(texture!.Width, Is.EqualTo((int)file["width"]!), resource);
                Assert.That(texture.Height, Is.EqualTo(240), resource);
                Assert.That(upstream.Get(resource), Is.Null, "Old counter PNG must not remain embedded");
            }
            Assert.That(upstream.GetAvailableResources().Any(path => path.Contains("Skins/Retro/", StringComparison.OrdinalIgnoreCase)), Is.False,
                "Retired Retro artwork and recordings must not be in new resource output");
            Assert.That(own.Get("Licenses/Counters/Michroma-OFL.txt"), Is.Not.Empty);
            Assert.That(own.Get("Licenses/Counters/NOTICE.txt"), Is.Not.Empty);
        }
    }
}
