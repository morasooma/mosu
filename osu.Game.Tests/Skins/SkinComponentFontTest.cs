using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinComponentFontTest
    {
        [Test]
        public void TestSnapshotAndSeparateFontFile()
        {
            using var counter = new ArgonScoreCounter();
            var original = counter.CreateSerialisedInfo();
            SkinComponentFont.For(counter).Family.Value = "Inter";
            var snapshot = counter.CreateSerialisedInfo();
            using var restored = snapshot.CreateInstance();
            Assert.That(SkinComponentFont.For(restored).Family.Value, Is.EqualTo("Inter"));
            var overrides = SkinCustomFontSerializer.ExtractOverrides(GlobalSkinnableContainers.MainHUDComponents, null, new Drawable[] { counter });
            Assert.That(overrides.Single().FontFamily, Is.EqualTo("Inter"));
            var fontInfo = JsonConvert.DeserializeObject<SkinCustomFontInfo>(JsonConvert.SerializeObject(new SkinCustomFontInfo { Overrides = overrides }))!;
            var snapshots = new[] { snapshot };
            SkinComponentGeometrySerializer.Store(new MosuSkinSettings(), GlobalSkinnableContainers.MainHUDComponents, null, snapshots);
            Assert.That(snapshot.Settings.ContainsKey("mosu_font_family"), Is.False);
            using var loaded = snapshot.CreateInstance();
            SkinCustomFontSerializer.ApplyOverrides(fontInfo, GlobalSkinnableContainers.MainHUDComponents, null, snapshots, new[] { loaded });
            Assert.That(SkinComponentFont.For(loaded).Family.Value, Is.EqualTo("Inter"));
            counter.ApplySerialisedInfo(original);
            Assert.That(SkinComponentFont.For(counter).Family.Value, Is.Empty);
        }
    }
}
