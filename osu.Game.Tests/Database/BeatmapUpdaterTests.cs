using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    [NonParallelizable]
    public class BeatmapUpdaterTests : RealmTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestQueuedCalculationDoesNotHoldWriterAndRejectsStaleResults(bool editWhileCalculating)
        {
            RunTestWithRealmAsync(async (realm, storage) =>
            {
                using var forkStore = new ForkDataStore(storage);
                using var config = new OsuConfigManager(storage);
                config.SetValue(OsuSetting.ForkBackgroundCalculationDutyPercent, 100);
                using var difficulty = new BeatmapDifficultyCache();
                using var api = new DummyAPIAccess();
                using var release = new ManualResetEventSlim();
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var notifications = new ConcurrentQueue<Notification>();
                var cache = new Mock<IWorkingBeatmapCache>();
                int calls = 0;
                cache.Setup(c => c.GetWorkingBeatmapForCalculation(It.IsAny<BeatmapInfo>())).Returns((BeatmapInfo info) =>
                {
                    if (Interlocked.Increment(ref calls) == 1)
                    {
                        started.TrySetResult();
                        if (!release.Wait(TimeSpan.FromSeconds(10)))
                            throw new TimeoutException("Test did not release the calculation worker.");
                    }

                    var content = new TestBeatmap(info.Ruleset) { BeatmapInfo = info };
                    return new TestWorkingBeatmap(content);
                });
                using var updater = new BeatmapUpdater(cache.Object, difficulty, api, storage, config)
                {
                    PostNotification = notifications.Enqueue
                };

                var set = new TestBeatmap(new OsuRuleset().RulesetInfo).BeatmapInfo.BeatmapSet!;
                set.Beatmaps[0].StarRating = -1;
                realm.Write(r => r.Add(set));
                var live = set.ToLive(realm);
                Guid setId = set.ID;
                Guid beatmapId = set.Beatmaps[0].ID;
                forkStore.SetPP(beatmapId, 42, 1);

                try
                {
                    updater.Queue(live, MetadataLookupScope.None);
                    await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.That(updater.HasPendingUpdates, Is.True);

                    // This is a real second Realm writer, not a mock of the persistence boundary.
                    await Task.Run(() => realm.Write(r =>
                    {
                        var current = r.Find<BeatmapSetInfo>(setId)!;
                        current.Beatmaps[0].UserSettings.Offset = 23;
                        if (editWhileCalculating)
                        {
                            current.Hash = "new saved revision";
                            current.Beatmaps[0].LastLocalUpdate = DateTimeOffset.UtcNow;
                            current.Beatmaps[0].StarRating = 42;
                        }
                    })).WaitAsync(TimeSpan.FromSeconds(3));

                    if (!editWhileCalculating)
                    {
                        // Repeated requests for a running set collapse into a single follow-up, sharing one toast.
                        for (int i = 0; i < 20; i++)
                            updater.Queue(live, MetadataLookupScope.None);
                    }
                }
                finally
                {
                    release.Set();
                }

                await waitUntil(() => !updater.HasPendingUpdates);
                live.PerformRead(current =>
                {
                    Assert.That(current.Beatmaps[0].UserSettings.Offset, Is.EqualTo(23));
                    if (editWhileCalculating)
                        Assert.That(current.Beatmaps[0].StarRating, Is.EqualTo(42), "Stale results must not replace the newly saved revision.");
                    else
                        Assert.That(current.Beatmaps[0].StarRating, Is.GreaterThan(0));
                });
                Assert.That(forkStore.GetPP(beatmapId), editWhileCalculating ? Is.EqualTo(42) : Is.GreaterThan(0).And.Not.EqualTo(42));
                Assert.That(calls, Is.EqualTo(editWhileCalculating ? 1 : 2));
                Assert.That(notifications.Count, Is.EqualTo(1));
                var notification = notifications.Single();
                Assert.That(notification.Transient, Is.True);
                Assert.That(notification.IsImportant, Is.False);
                Assert.That(notification.PopInSampleName, Is.Empty);
                Assert.That(notification.PopOutSampleName, Is.Empty);
                await waitUntil(() => notification.WasClosed);
            });
        }

        [Test]
        public void TestQueuedBatchResolvesEveryImportedDifficulty()
        {
            RunTestWithRealmAsync(async (realm, storage) =>
            {
                using var rulesets = new RealmRulesetStore(realm, storage);
                using var config = new OsuConfigManager(storage);
                config.SetValue(OsuSetting.ForkBackgroundCalculationDutyPercent, 100);
                using var difficulty = new BeatmapDifficultyCache();
                using var api = new DummyAPIAccess();
                var processed = new ConcurrentDictionary<Guid, bool>();
                var cache = new Mock<IWorkingBeatmapCache>();
                cache.Setup(c => c.GetWorkingBeatmapForCalculation(It.IsAny<BeatmapInfo>())).Returns((BeatmapInfo info) =>
                {
                    Assert.That(info.IsManaged, Is.False);
                    Assert.That(info.BeatmapSet!.Files, Is.Not.Empty, "Processing snapshots must retain the imported file mapping.");
                    Assert.That(info.Path, Is.Not.Null);
                    processed[info.ID] = true;
                    return new TestWorkingBeatmap(new TestBeatmap(info.Ruleset) { BeatmapInfo = info });
                });
                using var updater = new BeatmapUpdater(cache.Object, difficulty, api, storage, config);
                var importer = new BeatmapImporter(storage, realm)
                {
                    ProcessBeatmap = (set, scope) => updater.Queue(set.ToLive(realm), MetadataLookupScope.None)
                };
                var imported = await importer.Import(new ImportTask(TestResources.GetTestBeatmapStream(), "renatus.osz"));
                Assert.That(imported, Is.Not.Null);
                await waitUntil(() => !updater.HasPendingUpdates);
                imported!.PerformRead(set =>
                {
                    set.Realm!.Refresh();
                    Assert.That(set.Beatmaps.Count, Is.GreaterThan(1));
                    Assert.That(processed.Count, Is.EqualTo(set.Beatmaps.Count));
                    foreach (var beatmap in set.Beatmaps)
                    {
                        Assert.That(beatmap.StarRating, Is.GreaterThan(0), beatmap.DifficultyName);
                        Assert.That(beatmap.TotalObjectCount, Is.GreaterThan(0), beatmap.DifficultyName);
                    }
                });
            });
        }

        [Test]
        public void TestImportProcessingStartsAfterCommit()
        {
            RunTestWithRealmAsync(async (realm, storage) =>
            {
                using var rulesets = new RealmRulesetStore(realm, storage);
                var importer = new BeatmapImporter(storage, realm);
                bool called = false;
                importer.ProcessBeatmap = (set, _) =>
                {
                    called = true;
                    Assert.That(set.Realm!.IsInTransaction, Is.False);
                    Guid id = set.ID;
                    Assert.That(Task.Run(() => realm.Run(r => r.Find<BeatmapSetInfo>(id) != null))
                                    .WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult(), Is.True);
                };
                var imported = await importer.Import(new ImportTask(TestResources.GetTestBeatmapStream(), "renatus.osz"));
                Assert.That(imported, Is.Not.Null);
                Assert.That(called, Is.True);
            });
        }

        private static async Task waitUntil(Func<bool> condition)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (!condition())
                await Task.Delay(10, timeout.Token);
        }
    }
}
