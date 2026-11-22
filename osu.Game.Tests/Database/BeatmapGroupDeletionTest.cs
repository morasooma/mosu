using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Localisation;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class BeatmapGroupDeletionTest : RealmTest
    {
        private static BeatmapSetInfo createSet(int count = 2)
        {
            var set = new BeatmapSetInfo();
            for (int i = 0; i < count; i++)
                set.Beatmaps.Add(new BeatmapInfo { BeatmapSet = set, Hash = "hash-" + i });
            return set;
        }

        [Test]
        public void TestPartialSetAndHiddenDifficultiesAreNeverExpanded()
        {
            var complete = createSet();
            var partial = createSet();
            partial.Beatmaps[1].Hidden = true;
            var maps = complete.Beatmaps.Concat(partial.Beatmaps.Take(1)).Concat(complete.Beatmaps).ToArray();
            var plan = new BeatmapGroupDeletion(maps);
            Assert.That(plan.Entries, Has.Count.EqualTo(2));
            Assert.That(plan.Entries.Single(e => e.Set.ID == complete.ID).CanDelete, Is.True);
            var excluded = plan.Entries.Single(e => e.Set.ID == partial.ID);
            Assert.That(excluded.CanDelete, Is.False);
            Assert.That(excluded.Keep.Value && excluded.Keep.Disabled, Is.True);
            Assert.That(excluded.Reason, Is.EqualTo(BeatmapGroupDeletionStrings.Partial));
        }

        [Test]
        public void TestProjectedCarouselSetUsesCompleteDatabaseMembership()
        {
            var full = createSet();
            var projected = new BeatmapSetInfo { ID = full.ID };
            projected.Beatmaps.Add(new BeatmapInfo { ID = full.Beatmaps[0].ID, BeatmapSet = projected });
            var plan = new BeatmapGroupDeletion(projected.Beatmaps, new Dictionary<Guid, BeatmapSetInfo> { [full.ID] = full });
            Assert.That(plan.Entries.Single().CanDelete, Is.False);
            Assert.That(plan.Entries.Single().Reason, Is.EqualTo(BeatmapGroupDeletionStrings.Partial));
        }

        [Test]
        public void TestReadOnlyAndProtectedSetsAreKept()
        {
            var set = createSet();
            set.Protected = true;
            Assert.That(new BeatmapGroupDeletion(set.Beatmaps).Entries.Single().CanDelete, Is.False);
            set.Protected = false;
            set.Beatmaps[0].IsExternalCatalogue = true;
            Assert.That(new BeatmapGroupDeletion(set.Beatmaps).Entries.Single().Reason, Is.EqualTo(BeatmapGroupDeletionStrings.ReadOnly));
            set.Beatmaps[0].IsExternalCatalogue = false;
            StablePathManager.Replace(set.Beatmaps.ToDictionary(b => b.ID, _ => "/test/map.osu"), new Dictionary<Guid, string>());
            try
            {
                Assert.That(new BeatmapGroupDeletion(set.Beatmaps).Entries.Single().CanDelete, Is.False);
            }
            finally { StablePathManager.Replace(new Dictionary<Guid, string>(), new Dictionary<Guid, string>()); }
        }

        [Test]
        public void TestDeletesOnlyWholeSelectedSetsAndCanRestore() => RunTestWithRealm((realm, storage) =>
        {
            var complete = createSet();
            var kept = createSet();
            var partial = createSet();
            var maps = complete.Beatmaps.Concat(kept.Beatmaps).Concat(partial.Beatmaps.Take(1)).ToArray();
            var plan = new BeatmapGroupDeletion(maps);
            plan.Entries.Single(e => e.Set.ID == kept.ID).Keep.Value = true;
            realm.Write(r => { r.Add(complete, update: true); r.Add(kept, update: true); r.Add(partial, update: true); });
            var manager = new ModelManager<BeatmapSetInfo>(storage, realm);
            Assert.That(BeatmapGroupDeletion.Delete(manager, plan.Entries, maps.Select(b => b.ID).ToHashSet()), Is.EqualTo(1));
            realm.Run(r =>
            {
                Assert.That(r.Find<BeatmapSetInfo>(complete.ID)!.DeletePending, Is.True);
                Assert.That(r.Find<BeatmapSetInfo>(kept.ID)!.DeletePending, Is.False);
                Assert.That(r.Find<BeatmapSetInfo>(partial.ID)!.DeletePending, Is.False);
                Assert.That(r.Find<BeatmapSetInfo>(partial.ID)!.Beatmaps.Count, Is.EqualTo(2));
            });
            manager.Undelete(plan.Entries.Single(e => e.Set.ID == complete.ID).Set);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(complete.ID)!.DeletePending), Is.False);
        });

        [TestCase("added")]
        [TestCase("removed")]
        [TestCase("modified")]
        [TestCase("protected")]
        [TestCase("group-changed")]
        [TestCase("deleted")]
        public void TestLiveRevalidation(string change) => RunTestWithRealm((realm, storage) =>
        {
            var set = createSet();
            var plan = new BeatmapGroupDeletion(set.Beatmaps);
            var ids = set.Beatmaps.Select(b => b.ID).ToHashSet();
            realm.Write(r => r.Add(set, update: true));
            realm.Write(r =>
            {
                var live = r.Find<BeatmapSetInfo>(set.ID)!;
                switch (change)
                {
                    case "added": live.Beatmaps.Add(new BeatmapInfo { BeatmapSet = live, Ruleset = live.Beatmaps[0].Ruleset }); break;
                    case "removed": live.Beatmaps.RemoveAt(0); break;
                    case "modified": live.Beatmaps[0].Hash = "changed"; break;
                    case "protected": live.Protected = true; break;
                    case "deleted": live.DeletePending = true; break;
                    case "group-changed": ids.Clear(); break;
                }
            });
            Assert.That(BeatmapGroupDeletion.Delete(new ModelManager<BeatmapSetInfo>(storage, realm), plan.Entries, ids), Is.Zero);
            Assert.That(realm.Run(r => r.Find<BeatmapSetInfo>(set.ID)!.DeletePending), Is.EqualTo(change == "deleted"));
        });
    }
}
