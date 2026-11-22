using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LinuxGamePriorityConfigTest
    {
        [Test]
        public void TestOptInAndPersistence()
        {
            using var storage = new TemporaryNativeStorage($"linux-priority-{Guid.NewGuid():N}");
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.ForkLinuxGamePriority), Is.False);
                config.SetValue(OsuSetting.ForkLinuxGamePriority, true);
                Assert.That(config.Save(), Is.True);
            }

            using var reopened = new OsuConfigManager(storage);
            Assert.That(reopened.Get<bool>(OsuSetting.ForkLinuxGamePriority), Is.True);
        }
    }
}
