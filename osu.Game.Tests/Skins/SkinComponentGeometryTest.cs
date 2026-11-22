using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinComponentGeometryTest
    {
        [Test]
        public void TestSnapshotRoundtripAndUndoDefault()
        {
            using var panel = new BlurredBeatmapBackground();
            var original = panel.CreateSerialisedInfo();
            Assert.That(original.Settings.ContainsKey("mosu_corner_radius"), Is.False);
            SkinComponentGeometry.For(panel).Radius.Value = 25.5f;
            var snapshot = panel.CreateSerialisedInfo();
            snapshot.Settings = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(JsonConvert.SerializeObject(snapshot.Settings))!;
            using var restored = snapshot.CreateInstance();
            Assert.That(SkinComponentGeometry.For(restored).Radius.Value, Is.EqualTo(25.5f));
            panel.ApplySerialisedInfo(original);
            Assert.That(SkinComponentGeometry.For(panel).Radius.Value, Is.EqualTo(8));
            Assert.That(panel.CreateSerialisedInfo().Settings.ContainsKey("mosu_corner_radius"), Is.False);
        }

        [Test]
        public void TestSeparateSkinSettingsRoundtripAndTargetIsolation()
        {
            using var first = new BlurredBeatmapBackground();
            using var second = new ArgonWedgePiece();
            SkinComponentGeometry.For(first).Radius.Value = 35;
            SkinComponentGeometry.For(second).Radius.Value = 0;
            var infos = new[] { first.CreateSerialisedInfo(), second.CreateSerialisedInfo() };
            var settings = new MosuSkinSettings();
            SkinComponentGeometrySerializer.Store(settings, GlobalSkinnableContainers.MainHUDComponents, null, infos);
            Assert.That(infos.All(i => !i.Settings.ContainsKey("mosu_corner_radius")), Is.True);
            Assert.That(settings.ComponentGeometry.Select(o => o.Radius), Is.EqualTo(new[] { 35f, 0f }));
            var loaded = JsonConvert.DeserializeObject<MosuSkinSettings>(JsonConvert.SerializeObject(settings))!;
            var drawables = infos.Select(i => i.CreateInstance()).ToArray();
            try
            {
                SkinComponentGeometrySerializer.Apply(loaded, GlobalSkinnableContainers.MainHUDComponents, new osu.Game.Rulesets.RulesetInfo { ShortName = "mania" }, drawables);
                Assert.That(SkinComponentGeometry.GetOverride(drawables[0]), Is.Null);
                SkinComponentGeometrySerializer.Apply(loaded, GlobalSkinnableContainers.MainHUDComponents, null, drawables);
                Assert.That(SkinComponentGeometry.For(drawables[0]).Radius.Value, Is.EqualTo(35));
                Assert.That(SkinComponentGeometry.For(drawables[1]).Radius.Value, Is.Zero);
                SkinComponentGeometrySerializer.Store(loaded, GlobalSkinnableContainers.MainHUDComponents, null, new[] { new SerialisedDrawableInfo { Type = typeof(BlurredBeatmapBackground) } });
                Assert.That(loaded.ComponentGeometry, Is.Empty);
            }
            finally { foreach (var drawable in drawables) drawable.Dispose(); }
        }

        [Test]
        public void TestNativeControlsArePreserved()
        {
            using var box = new BoxElement();
            using var avatar = new PlayerAvatar();
            Assert.That(SkinComponentGeometry.HasNativeSetting(box), Is.True);
            Assert.That(SkinComponentGeometry.HasNativeSetting(avatar), Is.True);
            Assert.That(box.CreateSerialisedInfo().Settings.ContainsKey("corner_radius"), Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TestRejectNonFinite(float value)
        {
            using var panel = new BlurredBeatmapBackground();
            SkinComponentGeometry.Restore(panel, value);
            Assert.That(SkinComponentGeometry.GetOverride(panel), Is.Null);
        }
    }
}
