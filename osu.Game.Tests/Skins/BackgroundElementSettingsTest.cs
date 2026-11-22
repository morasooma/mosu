using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class BackgroundElementSettingsTest
    {
        [Test]
        public void OldLayoutsKeepTheirAppearance()
        {
            using var panel = (BlurredBeatmapBackground)new SerialisedDrawableInfo { Type = typeof(BlurredBeatmapBackground) }.CreateInstance();
            Assert.That(panel.BlurEnabled.Value, Is.True);
            Assert.That(panel.Dim.Value, Is.Zero);
            Assert.That(panel.Opacity.Value, Is.EqualTo(1));
            Assert.That(panel.ShowFullImage.Value, Is.False);
        }

        [Test]
        public void SettingsRoundTripThroughRealSkinSerialisation()
        {
            using var source = new BlurredBeatmapBackground();
            source.BlurEnabled.Value = false;
            source.Dim.Value = 0.65f;
            source.Opacity.Value = 0.4f;
            source.ShowFullImage.Value = true;
            var info = source.CreateSerialisedInfo();
            info.Settings = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(JsonConvert.SerializeObject(info.Settings))!;
            using var restored = (BlurredBeatmapBackground)info.CreateInstance();
            Assert.That(restored.BlurEnabled.Value, Is.False);
            Assert.That(restored.Dim.Value, Is.EqualTo(0.65f));
            Assert.That(restored.Opacity.Value, Is.EqualTo(0.4f));
            Assert.That(restored.ShowFullImage.Value, Is.True);
            Assert.That(source.CreateSettingsControls().Count(), Is.EqualTo(4));
        }
    }
}
