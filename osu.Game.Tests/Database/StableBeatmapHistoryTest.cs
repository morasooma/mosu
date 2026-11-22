using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class StableBeatmapHistoryTest
    {
        [Test]
        public void TestHistoryPersistsAndUnplayedOverridesStableTimestamp()
        {
            using var storage = new TemporaryNativeStorage($"stable-history-{Guid.NewGuid():N}");
            var id = Guid.NewGuid();
            var stableDate = DateTimeOffset.UtcNow.AddDays(-10);
            var now = DateTimeOffset.UtcNow;
            var history = new StableBeatmapHistory(storage);
            var info = new BeatmapInfo { ID = id, LastPlayed = stableDate };
            history.Apply(info);
            Assert.That(info.LastPlayed, Is.EqualTo(stableDate), "no local history should preserve osu!.db history");

            Guid changed = Guid.Empty;
            history.Changed += value => changed = value;
            history.SetLastPlayed(id, now);
            Assert.That(changed, Is.EqualTo(id));
            new StableBeatmapHistory(storage).Apply(info);
            Assert.That(info.LastPlayed, Is.EqualTo(now));

            history.SetLastPlayed(id, null);
            info.LastPlayed = stableDate;
            new StableBeatmapHistory(storage).Apply(info);
            Assert.That(info.LastPlayed, Is.Null);
            Assert.That(storage.Exists("osu!.db"), Is.False, "never create or modify the stable database");
        }

        [Test]
        public void TestDamagedHistoryDoesNotPreventLoading()
        {
            using var storage = new TemporaryNativeStorage($"stable-history-damaged-{Guid.NewGuid():N}");
            using (var writer = new StreamWriter(storage.CreateFileSafely("stable-beatmap-history.json")))
                writer.Write("not json");
            var history = new StableBeatmapHistory(storage);
            var info = new BeatmapInfo { ID = Guid.NewGuid(), LastPlayed = DateTimeOffset.UtcNow };
            var expected = info.LastPlayed;
            history.Apply(info);
            Assert.That(info.LastPlayed, Is.EqualTo(expected));
            history.SetLastPlayed(info.ID, null);
            new StableBeatmapHistory(storage).Apply(info);
            Assert.That(info.LastPlayed, Is.Null);
        }
    }
}
