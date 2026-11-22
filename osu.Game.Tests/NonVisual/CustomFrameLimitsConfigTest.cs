using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class CustomFrameLimitsConfigTest
    {
        [Test]
        public void TestDefaultsAndPersistenceWhileDisabled()
        {
            using var storage = new TemporaryNativeStorage($"frame-limits-{Guid.NewGuid():N}");
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.ForkCustomFrameLimits), Is.False);
                Assert.That(config.Get<int>(OsuSetting.ForkCustomDrawHz), Is.Zero);
                Assert.That(config.Get<int>(OsuSetting.ForkCustomUpdateHz), Is.EqualTo(8000));
                Assert.That(config.Get<int>(OsuSetting.ForkCustomInputHz), Is.EqualTo(1000));
                Assert.That(config.Get<int>(OsuSetting.ForkCustomAudioHz), Is.EqualTo(1000));
                config.SetValue(OsuSetting.ForkCustomDrawHz, 237);
                config.SetValue(OsuSetting.ForkCustomUpdateHz, 2000);
                config.SetValue(OsuSetting.ForkCustomInputHz, 4000);
                config.SetValue(OsuSetting.ForkCustomAudioHz, 500);
                Assert.That(config.Save(), Is.True);
            }

            using var reopened = new OsuConfigManager(storage);
            Assert.That(reopened.Get<bool>(OsuSetting.ForkCustomFrameLimits), Is.False);
            Assert.That(reopened.Get<int>(OsuSetting.ForkCustomDrawHz), Is.EqualTo(237));
            Assert.That(reopened.Get<int>(OsuSetting.ForkCustomUpdateHz), Is.EqualTo(2000));
            Assert.That(reopened.Get<int>(OsuSetting.ForkCustomInputHz), Is.EqualTo(4000));
            Assert.That(reopened.Get<int>(OsuSetting.ForkCustomAudioHz), Is.EqualTo(500));
        }

        [TestCase(OsuSetting.ForkCustomDrawHz)]
        [TestCase(OsuSetting.ForkCustomUpdateHz)]
        [TestCase(OsuSetting.ForkCustomInputHz)]
        [TestCase(OsuSetting.ForkCustomAudioHz)]
        public void TestRange(OsuSetting setting)
        {
            using var storage = new TemporaryNativeStorage($"frame-limits-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            config.SetValue(setting, -1);
            Assert.That(config.Get<int>(setting), Is.Zero);
            config.SetValue(setting, 16001);
            Assert.That(config.Get<int>(setting), Is.EqualTo(16000));
        }
    }
}
