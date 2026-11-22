using System;
using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class TagCoopReplayCacheTest
    {
        [Test]
        public void TestSmallReplayReadThroughAndReplacement()
        {
            var cache = new TagCoopReplayCache();
            Guid id = Guid.NewGuid();
            int reads = 0;
            string load() { reads++; return "first"; }
            Assert.That(cache.GetOrLoad(id, load), Is.EqualTo("first"));
            Assert.That(cache.GetOrLoad(id, load), Is.EqualTo("first"));
            Assert.That(reads, Is.EqualTo(1));
            cache.Set(id, "second");
            Assert.That(cache.GetOrLoad(id, load), Is.EqualTo("second"));
            cache.Set(id, string.Empty);
            Assert.That(cache.Count, Is.Zero);
            Assert.That(cache.CachedBytes, Is.Zero);
        }

        [Test]
        public void TestLargeReplayIsReturnedWithoutGlobalRetention()
        {
            var cache = new TagCoopReplayCache();
            string large = new string('x', 3 * 1024 * 1024);
            Guid id = Guid.NewGuid();
            cache.Set(id, large);
            Assert.That(cache.GetOrLoad(id, () => large), Is.SameAs(large));
            Assert.That(cache.Count, Is.Zero);
            Assert.That(cache.CachedBytes, Is.Zero);
        }

        [Test]
        public void TestBoundedByBytesAndEntries()
        {
            var cache = new TagCoopReplayCache();
            for (int i = 0; i < 100; i++)
            {
                cache.Set(Guid.NewGuid(), new string('x', 400000));
                Assert.That(cache.Count, Is.LessThanOrEqualTo(8));
                Assert.That(cache.CachedBytes, Is.LessThanOrEqualTo(4 * 1024 * 1024));
            }
        }

        [Test]
        public void TestMissingOrFailedReadsAreNotCached()
        {
            var cache = new TagCoopReplayCache();
            Guid id = Guid.NewGuid();
            Assert.Throws<InvalidOperationException>(() => cache.GetOrLoad(id, () => throw new InvalidOperationException()));
            Assert.That(cache.GetOrLoad(id, () => string.Empty), Is.Empty);
            Assert.That(cache.Count, Is.Zero);
            Assert.That(cache.GetOrLoad(id, () => "now exists"), Is.EqualTo("now exists"));
        }
    }
}
