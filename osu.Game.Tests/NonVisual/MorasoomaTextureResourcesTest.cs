using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Morasooma.Resources;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Resources;
using osu.Game.Skinning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MorasoomaTextureResourcesTest
    {
        [Test]
        public async Task TestReviewedV5TexturesResolveWithoutOriginalFallback()
        {
            using var own = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            using var upstream = new DllResourceStore(OsuResources.ResourceAssembly);
            using var resources = new ResourceStore<byte[]>();
            resources.AddStore(new NamespacedResourceStore<byte[]>(own, "TextureOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));
            resources.AddStore(upstream);
            using var textureResources = new NamespacedResourceStore<byte[]>(resources, "Textures");
            using var textures = new TextureStore(new DummyRenderer(), new TextureLoaderStore(textureResources));
            var manifest = JObject.Parse(Encoding.UTF8.GetString(own.Get("texture-manifest.json")));
            var rows = (JArray)manifest["textures"]!;
            Assert.That(rows.Count, Is.EqualTo(75));
            Assert.That(rows.Count(r => (bool)r["owner_drawing_pending"]!), Is.EqualTo(3));
            foreach (var row in rows)
            {
                string key = (string)row["resource"]!;
                byte[] bytes = resources.Get(key);
                Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), Is.EqualTo((string)row["sha256"]!), key);
                Assert.That(await resources.GetAsync(key), Is.EqualTo(bytes), key);
                var texture = textures.Get(key["Textures/".Length..]);
                Assert.That(texture, Is.Not.Null, key);
                Assert.That(texture!.Width, Is.EqualTo((int)row["width"]!), key);
                Assert.That(texture.Height, Is.EqualTo((int)row["height"]!), key);
                Assert.That(upstream.Get(key), Is.Null, key);
                if ((bool)row["owner_drawing_pending"]!)
                {
                    using var image = Image.Load<Rgba32>(bytes);
                    bool empty = true;
                    image.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < accessor.Height; y++)
                            foreach (var pixel in accessor.GetRowSpan(y))
                                empty &= pixel.Equals(default(Rgba32));
                    });
                    Assert.That(empty, Is.True, key);
                }
            }
            foreach (string ownerMark in new[] { "morasooma-logo", "mora-logo", "morasooma-wordmark" })
                Assert.That(resources.Get("Textures/Menu/" + ownerMark + ".png"), Is.Not.Null, ownerMark);
        }

        [Test]
        public void TestOriginalMediaBankIsRestrictedToExplicitExceptions()
        {
            using var upstream = new DllResourceStore(OsuResources.ResourceAssembly);
            var names = upstream.GetAvailableResources().ToArray();
            Assert.That(names.Where(n => n.StartsWith("Textures/", StringComparison.Ordinal))
                             .All(n => n.StartsWith("Textures/Flags/", StringComparison.Ordinal)
                                       || n == "Textures/Icons/RulesetDodge.png"), Is.True);
            foreach (string retired in new[] { "Samples/", "Skins/", "Tracks/", "Beatmaps/", "Fonts/" })
                Assert.That(names.Any(n => n.StartsWith(retired, StringComparison.Ordinal)), Is.False, retired);
            using var client = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources");
            Assert.That(client.GetAvailableResources().Any(n => n.StartsWith("Skins/", StringComparison.Ordinal)
                                                                || n.StartsWith("Samples/", StringComparison.Ordinal)
                                                                || n.StartsWith("InterfaceSounds/", StringComparison.Ordinal)), Is.False);
            Assert.That(DefaultLegacySkin.CreateInfo().ID, Is.EqualTo(SkinInfo.CLASSIC_SKIN));
            Assert.That(DefaultLegacySkin.CreateInfo().Creator, Does.Contain("P1 White, MIT"));
        }
    }
}
