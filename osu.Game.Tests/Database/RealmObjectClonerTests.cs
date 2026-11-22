using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class RealmObjectClonerTests : RealmTest
    {
        [Test]
        public void TestCyclicBeatmapCloneRetainsRootAndIndependentMetadata()
        {
            var set = CreateBeatmapSet(CreateRuleset());
            set.Beatmaps[0].Metadata.UserTags.Add("custom tag");
            var source = set.Beatmaps[0];
            var copy = RealmObjectCloner.Detach(source);

            Assert.That(copy, Is.Not.SameAs(source));
            Assert.That(copy.BeatmapSet!.Beatmaps.Count, Is.EqualTo(set.Beatmaps.Count));
            Assert.That(copy.BeatmapSet.Beatmaps.Single(b => b.ID == copy.ID), Is.SameAs(copy));
            Assert.That(copy.BeatmapSet.Files.Count, Is.EqualTo(set.Files.Count));
            Assert.That(copy.Metadata.UserTags, Is.EquivalentTo(source.Metadata.UserTags));
            copy.Metadata.Title = "changed";
            Assert.That(source.Metadata.Title, Is.Not.EqualTo(copy.Metadata.Title));
            Assert.That(copy.BeatmapSet.Files[0].File, Is.Not.SameAs(set.Files[0].File));
        }

        [Test]
        public void TestManagedBeatmapCloneReconnectsOneSet()
        {
            RunTestWithRealm((access, _) => access.Run(realm =>
            {
                var set = CreateBeatmapSet(CreateRuleset());
                realm.Write(() => realm.Add(set));
                var copy = set.Beatmaps[0].Detach();
                Assert.That(copy.IsManaged, Is.False);
                Assert.That(copy.BeatmapSet!.Files.Count, Is.EqualTo(set.Files.Count));
                Assert.That(copy.BeatmapSet.Beatmaps.All(b => ReferenceEquals(b.BeatmapSet, copy.BeatmapSet)), Is.True);
                Assert.That(copy.BeatmapSet.Beatmaps.Single(b => b.ID == copy.ID), Is.SameAs(copy));
                Assert.That(copy.BeatmapSet.Beatmaps.All(b => !b.IsManaged && !b.Metadata.IsManaged), Is.True);
            }));
        }

        [Test]
        public void TestFrozenSetCloneCanRunOnWorkerThread()
        {
            RunTestWithRealm((access, _) => access.Run(realm =>
            {
                var set = CreateBeatmapSet(CreateRuleset());
                set.Beatmaps[0].Metadata.UserTags.Add("frozen tag");
                realm.Write(() => realm.Add(set));
                var frozen = set.Freeze();
                try
                {
                    var copy = Task.Run(() => RealmObjectCloner.Detach(frozen)).GetAwaiter().GetResult();
                    Assert.That(copy.IsManaged, Is.False);
                    Assert.That(copy.Files, Is.Empty);
                    Assert.That(copy.Beatmaps.Count, Is.EqualTo(set.Beatmaps.Count));
                    Assert.That(copy.Beatmaps[0].Metadata.UserTags, Is.EqualTo(new[] { "frozen tag" }));
                    Assert.That(copy.Beatmaps.All(b => ReferenceEquals(b.BeatmapSet, copy)), Is.True);
                }
                finally
                {
                    frozen.Realm.Dispose();
                }
            }));
        }

        [Test]
        public void TestParallelClonesDoNotShareDestinationsOrContext()
        {
            var sets = Enumerable.Range(0, 32).Select(_ => CreateBeatmapSet(CreateRuleset())).ToArray();
            var copies = new BeatmapSetInfo[sets.Length];
            Parallel.For(0, sets.Length, i => copies[i] = RealmObjectCloner.Detach(sets[i]));
            for (int i = 0; i < sets.Length; i++)
            {
                Assert.That(copies[i], Is.Not.SameAs(sets[i]));
                Assert.That(copies[i].ID, Is.EqualTo(sets[i].ID));
                Assert.That(copies[i].Beatmaps.All(b => ReferenceEquals(b.BeatmapSet, copies[i])), Is.True);
                Assert.That(copies[i].Beatmaps[0].Metadata, Is.Not.SameAs(sets[i].Beatmaps[0].Metadata));
            }
        }

        [Test]
        public void TestSiblingDifficultiesDoNotMultiplyFileGraphAllocations()
        {
            var small = CreateBeatmapSet(CreateRuleset());
            while (small.Beatmaps.Count > 1)
                small.Beatmaps.RemoveAt(small.Beatmaps.Count - 1);
            var large = CreateBeatmapSet(CreateRuleset());
            for (int i = 0; i < 8; i++)
                large.Beatmaps.Add(new BeatmapInfo(CreateRuleset()) { BeatmapSet = large });
            foreach (var set in new[] { small, large })
            {
                for (int i = 0; i < 500; i++)
                    set.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = $"hash-{i}" }, $"sound-{i}.wav"));
                _ = RealmObjectCloner.Detach(set.Beatmaps[0]); // Warm reflection metadata.
            }

            static long allocated(BeatmapSetInfo set)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var copy = RealmObjectCloner.Detach(set.Beatmaps[0]);
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                GC.KeepAlive(copy);
                return bytes;
            }

            long smallBytes = allocated(small);
            long largeBytes = allocated(large);
            TestContext.WriteLine($"Detach 512 files: one difficulty={smallBytes} B, twelve difficulties={largeBytes} B");
            Assert.That(largeBytes, Is.LessThan(smallBytes * 2), "Files must be copied once per set, not once per sibling difficulty.");
        }

        [Test]
        public void TestSetCloneOmitsFilesAndReconnectsChildren()
        {
            var set = CreateBeatmapSet(CreateRuleset());
            var copy = RealmObjectCloner.Detach(set);
            Assert.That(copy.Files, Is.Empty);
            Assert.That(copy.Beatmaps.Count, Is.EqualTo(set.Beatmaps.Count));
            Assert.That(copy.Beatmaps.All(b => ReferenceEquals(b.BeatmapSet, copy)), Is.True);
        }

        [Test]
        public void TestStartupSetCloneAllocationMeasurement()
        {
            const int iterations = 2000;
            var set = CreateBeatmapSet(CreateRuleset());
            foreach (var beatmap in set.Beatmaps)
                beatmap.Metadata.UserTags.Add("startup tag");

            for (int i = 0; i < 20; i++)
                _ = RealmObjectCloner.Detach(set);

            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            BeatmapSetInfo copy = null!;
            for (int i = 0; i < iterations; i++)
                copy = RealmObjectCloner.Detach(set);
            watch.Stop();
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

            TestContext.WriteLine($"Startup set clones: {iterations} sets / {iterations * set.Beatmaps.Count} difficulties; {allocated} B; {watch.Elapsed.TotalMilliseconds:0.###} ms");
            Assert.That(copy.Files, Is.Empty);
            Assert.That(copy.Beatmaps.Count, Is.EqualTo(set.Beatmaps.Count));
            Assert.That(copy.Beatmaps[0].Metadata.UserTags, Is.EquivalentTo(set.Beatmaps[0].Metadata.UserTags));
            Assert.That(copy.Beatmaps.All(b => ReferenceEquals(b.BeatmapSet, copy)), Is.True);
        }

        [Test]
        public void TestPrimitiveListsAreIndependentAndKeepOrder()
        {
            var score = new ScoreInfo(CreateBeatmapSet(CreateRuleset()).Beatmaps[0]);
            score.Pauses.Add(30);
            score.Pauses.Add(10);
            score.Pauses.Add(30);
            var copy = RealmObjectCloner.Detach(score);
            Assert.That(copy.Pauses, Is.EqualTo(new[] { 30, 10, 30 }));
            copy.Pauses.Clear();
            Assert.That(score.Pauses.Count, Is.EqualTo(3));
        }

        [Test]
        public void TestWritablePropertyCopyLeavesListsAndIgnoredPropertiesUntouched()
        {
            var source = new BeatmapMetadata { Title = "source", Artist = "source artist" };
            source.UserTags.Add("source tag");
            var destination = new BeatmapMetadata { Title = "destination" };
            destination.UserTags.Add("destination tag");
            RealmObjectCloner.CopyWritableProperties(source, destination, nameof(BeatmapMetadata.Title));
            Assert.That(destination.Title, Is.EqualTo("destination"));
            Assert.That(destination.Artist, Is.EqualTo(source.Artist));
            Assert.That(destination.UserTags, Is.EqualTo(new[] { "destination tag" }));
        }

        [Test]
        public void TestScoreCloneCopiesPersistedListsButNotIgnoredState()
        {
            var score = new ScoreInfo(CreateBeatmapSet(CreateRuleset()).Beatmaps[0])
            {
                TotalScore = 123456,
                MosuTotalScore = 999,
                RealmUser = new RealmUser { Username = "test" }
            };
            score.Pauses.Add(100);
            var copy = RealmObjectCloner.Detach(score);
            Assert.That(copy.TotalScore, Is.EqualTo(score.TotalScore));
            Assert.That(copy.MosuTotalScore, Is.Null);
            Assert.That(copy.Pauses, Is.EquivalentTo(score.Pauses));
            Assert.That(copy.RealmUser, Is.Not.SameAs(score.RealmUser));
            copy.Pauses.Add(200);
            Assert.That(score.Pauses.Count, Is.EqualTo(1));
        }
    }
}
