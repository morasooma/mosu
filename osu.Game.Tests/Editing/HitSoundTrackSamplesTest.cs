using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.Compose.Components.Timeline;

namespace osu.Game.Tests.Editing
{
    [TestFixture]
    public class HitSoundTrackSamplesTest
    {
        [Test]
        public void TestTogglePreservesSampleSettings()
        {
            var samples = new List<HitSampleInfo>
            {
                new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, "3", 42, useBeatmapSamples: true),
            };
            HitSoundTrackSamples.Toggle(new HitCircle(), samples, HitSampleInfo.HIT_CLAP);
            var clap = samples.Single(s => s.Name == HitSampleInfo.HIT_CLAP);
            Assert.That(clap.Bank, Is.EqualTo(HitSampleInfo.BANK_SOFT));
            Assert.That(clap.Suffix, Is.EqualTo("3"));
            Assert.That(clap.Volume, Is.EqualTo(42));
            Assert.That(clap.UseBeatmapSamples, Is.True);
            HitSoundTrackSamples.Toggle(new HitCircle(), samples, HitSampleInfo.HIT_CLAP);
            Assert.That(samples.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestBankRespectsAutoInheritance()
        {
            var samples = new List<HitSampleInfo>
            {
                new HitSampleInfo(HitSampleInfo.HIT_NORMAL),
                new HitSampleInfo(HitSampleInfo.HIT_CLAP, editorAutoBank: true),
                new HitSampleInfo(HitSampleInfo.HIT_FINISH, HitSampleInfo.BANK_DRUM, editorAutoBank: false),
            };
            HitSoundTrackSamples.SetBank(samples, HitSampleInfo.BANK_SOFT);
            Assert.That(samples[0].Bank, Is.EqualTo(HitSampleInfo.BANK_SOFT));
            Assert.That(samples[1].Bank, Is.EqualTo(HitSampleInfo.BANK_SOFT));
            Assert.That(samples[2].Bank, Is.EqualTo(HitSampleInfo.BANK_DRUM));
        }

        [TestCase(-10, 5)]
        [TestCase(52, 52)]
        [TestCase(200, 100)]
        public void TestVolumeBounds(int input, int expected)
        {
            var samples = new List<HitSampleInfo>
            {
                new HitSampleInfo(HitSampleInfo.HIT_NORMAL, suffix: "4"),
                new HitSampleInfo(HitSampleInfo.HIT_CLAP),
            };
            HitSoundTrackSamples.SetVolume(samples, input);
            Assert.That(samples.All(s => s.Volume == expected), Is.True);
            Assert.That(samples[0].Suffix, Is.EqualTo("4"));
        }
    }
}
