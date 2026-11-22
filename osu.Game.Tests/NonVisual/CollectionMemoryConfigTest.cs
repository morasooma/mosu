using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class CollectionMemoryConfigTest
    {
        [Test]
        public void TestDefaultAndPersistence()
        {
            using var storage = new TemporaryNativeStorage($"collection-memory-{Guid.NewGuid():N}");
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.SongSelectRememberCollection), Is.True);
                config.SetValue(OsuSetting.SongSelectRememberCollection, false);
                config.Save();
            }

            using var reopened = new OsuConfigManager(storage);
            Assert.That(reopened.Get<bool>(OsuSetting.SongSelectRememberCollection), Is.False);
        }
    }
}
