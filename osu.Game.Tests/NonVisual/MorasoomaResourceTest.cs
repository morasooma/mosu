using System.IO;
using Morasooma.Resources;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Graphics.Fonts;
using osu.Game.Skinning;
using osu.Game.Users;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MorasoomaResourceTest
    {
        [TestCase("Fonts/Torus/Torus-Regular", "Fonts/Nunito/Nunito-Medium")]
        [TestCase("Fonts/Torus-Alternate/Torus-Alternate-Bold", "Fonts/MosuRoundedAlt/MosuRoundedAlt-Bold")]
        [TestCase("Fonts/Venera/Venera-Bold", "Fonts/Michroma/Michroma-Regular")]
        public void TestLicensedFontAliases(string alias, string replacement)
        {
            using var fonts = new ResourceStore<byte[]>(new MosuFontResources());
            foreach (string extension in new[] { ".bin", "_0.png" })
            {
                byte[] actual = fonts.Get(alias + extension);

                Assert.That(actual, Is.Not.Null.And.Not.Empty);
                Assert.That(actual, Is.EqualTo(fonts.Get(replacement + extension)));
            }
        }

        [TestCase("skin.ini")]
        [TestCase("approachcircle.png")]
        [TestCase("normal-hitnormal.wav")]
        public void TestBundledSkinResources(string name)
        {
            using var resources = MorasoomaSkin.CreateResourceStore();
            using Stream stream = resources.GetStream(name);

            Assert.That(stream, Is.Not.Null);
            Assert.That(stream.ReadByte(), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void TestBundledMedalAndCustomCover()
        {
            const string slug = "all-intro-conversion";
            const string cover = "https://example.org/medal.png";
            using var resources = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "MedalOverrides/Textures");

            string? name = MorasoomaMedalAssets.Resolve(slug, cover);
            Assert.That(name, Is.EqualTo($"Medals/{slug}@2x.png"));
            Assert.That(resources.Get(name!), Is.Not.Null.And.Not.Empty);
            Assert.That(MorasoomaMedalAssets.Resolve("custom-medal", cover), Is.EqualTo(cover));
        }
    }
}
