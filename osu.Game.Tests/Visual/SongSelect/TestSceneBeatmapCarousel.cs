// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Scoring;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// Covers common steps which can be used for manual testing.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBeatmapCarousel : BeatmapCarouselTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private void resetRankTestScene()
        {
            AddStep("remove previous carousel rank subscribers", () =>
            {
                // The fixture also hosts full-carousel tests. Their still-live panels
                // must not contribute subscribers or queries to isolated rank tests.
                foreach (var panel in this.ChildrenOfType<PanelLocalRankDisplay>())
                    panel.Active = false;
                Clear();
                Carousel = null!;
                BeatmapSets.Clear();
            });
        }

        [Test]
        public void TestRankAnchorDeactivationTransfersPendingSnapshotAndLastUnsubscribeStartsFresh()
        {
            resetRankTestScene();
            PanelLocalRankDisplay first = null!;
            PanelLocalRankDisplay survivor = null!;
            PanelLocalRankDisplay replacement = null!;
            RankSnapshotCoordinator coordinator = null!;
            BeatmapInfo beatmap = null!;
            int baseline = 0;

            AddStep("load first and surviving panels", () =>
            {
                coordinator = RankSnapshotCoordinator.For(Realm);
                beatmap = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                Add(first = new PanelLocalRankDisplay(beatmap));
                Add(survivor = new PanelLocalRankDisplay(beatmap));
            });
            AddUntilStep("initial pass completes", () => coordinator.IsIdle && coordinator.WorkerPassCount > 0);
            AddStep("invalidate, remove scheduling anchor before delayed callback", () =>
            {
                baseline = coordinator.WorkerPassCount;
                Realm.Write(r => r.Add(new BeatmapSetInfo()));
                // HashSet iteration does not promise insertion order, especially when
                // earlier test subscriptions have vacated slots. Remove the real owner.
                var anchor = coordinator.ScheduledOnForTesting;
                Assert.That(anchor, Is.SameAs(first).Or.SameAs(survivor));
                if (anchor == survivor)
                    (first, survivor) = (survivor, first);
                first.Active = false; // pool/offscreen: the drawable scheduler can stop updating.
                Remove(first, true);
                Assert.That(coordinator.ScheduledOnForTesting, Is.SameAs(survivor));
            });
            AddUntilStep("surviving panel receives transferred snapshot", () => coordinator.IsIdle && coordinator.WorkerPassCount > baseline);
            AddStep("invalidate then unsubscribe last panel", () =>
            {
                Realm.Write(r => r.Add(new BeatmapSetInfo()));
                Remove(survivor, true);
            });
            AddAssert("zero panels are idle", () => coordinator.IsIdle);
            AddStep("subscribe new first panel and import its rank", () =>
            {
                Add(replacement = new PanelLocalRankDisplay(beatmap));
                var score = TestResources.CreateTestScoreInfo(beatmap);
                score.BeatmapHash = beatmap.Hash;
                score.User = API.LocalUser.Value;
                score.Rank = ScoreRank.S;
                Realm.Write(r => r.Add(score, update: true));
            });
            AddUntilStep("fresh first panel sees rank", () => replacement.CurrentRank == ScoreRank.S && coordinator.IsIdle);
            AddStep("dispose new first", () => Remove(replacement, true));
        }

        [Test]
        public void TestRankAnchorTurnoverKeepsOriginalSnapshotDeadline()
        {
            resetRankTestScene();
            PanelLocalRankDisplay rotating = null!;
            PanelLocalRankDisplay stable = null!;
            RankSnapshotCoordinator coordinator = null!;
            BeatmapInfo beatmap = null!;
            int baseline = 0;
            int turnover = 0;
            long invalidatedAt = 0;
            long workerStartedAt = 0;

            AddStep("load rank panels", () =>
            {
                coordinator = RankSnapshotCoordinator.For(Realm);
                beatmap = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                Add(rotating = new PanelLocalRankDisplay(beatmap));
                Add(stable = new PanelLocalRankDisplay(beatmap));
            });
            AddUntilStep("initial rank snapshot completes", () => coordinator.IsIdle && coordinator.WorkerPassCount > 0);
            AddStep("turn over callback owner past original deadline", () =>
            {
                baseline = coordinator.WorkerPassCount;
                coordinator.BeforeQueryForTesting = () => Interlocked.CompareExchange(ref workerStartedAt, Stopwatch.GetTimestamp(), 0);
                invalidatedAt = Stopwatch.GetTimestamp();
                Realm.Write(r => r.Add(new BeatmapSetInfo()));
                coordinator.AddOrUpdate(rotating);
                for (int i = 0; i < 8; i++)
                {
                    // Already-loaded panels remain subscribed. Block this test step's Update
                    // to keep replacing the callback before its scheduler processes a frame.
                    Thread.Sleep(12);
                    var anchor = coordinator.ScheduledOnForTesting!;
                    Assert.That(anchor, Is.Not.Null);
                    var other = anchor == stable ? rotating : stable;
                    long originalDeadline = coordinator.ScheduledAtForTesting!.Value;
                    coordinator.Remove(anchor);
                    Assert.That(coordinator.ScheduledAtForTesting, Is.EqualTo(originalDeadline), "transfer must preserve first deadline");
                    coordinator.AddOrUpdate(anchor);
                    coordinator.Remove(other);
                    coordinator.AddOrUpdate(other);
                    turnover++;
                }
            });
            AddUntilStep("rank pass started after turnover", () => Interlocked.Read(ref workerStartedAt) != 0);
            AddStep("turnover did not postpone original deadline", () =>
            {
                TestContext.Progress.WriteLine($"RANK_TURNOVER replacements={turnover} elapsed_ms={Stopwatch.GetElapsedTime(invalidatedAt).TotalMilliseconds:F1} "
                                               + $"worker_passes={coordinator.WorkerPassCount - baseline}");
                Assert.That(turnover, Is.EqualTo(8));
                Assert.That(coordinator.WorkerPassCount, Is.GreaterThan(baseline));
                Assert.That(Interlocked.Read(ref workerStartedAt), Is.GreaterThan(0));
                Assert.That(Stopwatch.GetElapsedTime(invalidatedAt, Interlocked.Read(ref workerStartedAt)).TotalMilliseconds, Is.LessThan(140));
            });
            AddStep("dispose rank panels", () =>
            {
                coordinator.BeforeQueryForTesting = null;
                Remove(rotating, true);
                Remove(stable, true);
            });
        }

        [Test]
        public void TestRankPersistentWorkerFaultWaitsForInvalidationAndRecovers()
        {
            resetRankTestScene();
            PanelLocalRankDisplay panel = null!;
            RankSnapshotCoordinator coordinator = null!;
            int failedPasses = 0;
            int baseline = 0;
            BeatmapInfo beatmap = null!;

            AddStep("start failing rank worker", () =>
            {
                coordinator = RankSnapshotCoordinator.For(Realm);
                coordinator.BeforeQueryForTesting = () => throw new InvalidOperationException("rank snapshot test fault");
                beatmap = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                Add(panel = new PanelLocalRankDisplay(beatmap));
            });
            AddUntilStep("failed snapshot settled", () => coordinator.IsIdle && coordinator.WorkerPassCount > 0);
            AddStep("record failing pass", () => failedPasses = coordinator.WorkerPassCount);
            AddWaitStep("observe longer than several debounce periods", 250);
            AddAssert("persistent failure has no retry loop", () => coordinator.WorkerPassCount == failedPasses);
            AddStep("new commit retries once while fault persists", () => Realm.Write(r => r.Add(new BeatmapSetInfo())));
            AddUntilStep("second failure settled", () => coordinator.IsIdle && coordinator.WorkerPassCount > failedPasses);
            AddStep("record second failure", () => failedPasses = coordinator.WorkerPassCount);
            AddWaitStep("no further retry", 250);
            AddAssert("one pass per invalidation", () => coordinator.WorkerPassCount == failedPasses);
            AddStep("Realm rebind retries once while fault persists", () =>
            {
                using (Realm.BlockAllOperations("rank fault rebind test"))
                {
                }
            });
            AddUntilStep("rebind failure settled", () => coordinator.IsIdle && coordinator.WorkerPassCount > failedPasses);
            AddStep("record rebind failure", () => failedPasses = coordinator.WorkerPassCount);
            AddWaitStep("rebind does not spin", 250);
            AddAssert("one pass after rebind", () => coordinator.WorkerPassCount == failedPasses);
            AddStep("clear fault and commit score", () =>
            {
                coordinator.BeforeQueryForTesting = null;
                baseline = coordinator.WorkerPassCount;
                var score = TestResources.CreateTestScoreInfo(beatmap);
                score.BeatmapHash = beatmap.Hash;
                score.User = API.LocalUser.Value;
                score.Rank = ScoreRank.S;
                Realm.Write(r => r.Add(score, update: true));
            });
            AddUntilStep("recovers on commit", () => panel.CurrentRank == ScoreRank.S && coordinator.WorkerPassCount > baseline);
            AddStep("dispose panel", () => Remove(panel, true));
        }

        [Test]
        public void TestRankZeroPanelsDuringInflightWorkerThenNewSubscription()
        {
            resetRankTestScene();
            PanelLocalRankDisplay first = null!;
            PanelLocalRankDisplay replacement = null!;
            BeatmapInfo beatmap = null!;
            RankSnapshotCoordinator coordinator = null!;
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();

            AddStep("block worker after launch", () =>
            {
                coordinator = RankSnapshotCoordinator.For(Realm);
                coordinator.BeforeQueryForTesting = () =>
                {
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(4)))
                        throw new TimeoutException("rank gate timed out");
                };
                beatmap = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                Add(first = new PanelLocalRankDisplay(beatmap));
            });
            AddUntilStep("worker entered", () => entered.IsSet);
            AddStep("unsubscribe zero panels and subscribe replacement before completion", () =>
            {
                Remove(first, true);
                Add(replacement = new PanelLocalRankDisplay(beatmap));
                release.Set();
            });
            AddUntilStep("stale completion discarded and replacement refreshed", () => coordinator.IsIdle && coordinator.WorkerPassCount >= 2);
            AddStep("dispose replacement and gates", () =>
            {
                coordinator.BeforeQueryForTesting = null;
                Remove(replacement, true);
                entered.Dispose();
                release.Dispose();
            });
        }

        [Test]
        public void TestUnrelatedWritesShareOneRankWorkerPassAcrossVisiblePanels()
        {
            resetRankTestScene();
            PanelLocalRankDisplay[] panels = null!;
            RankSnapshotCoordinator coordinator = null!;
            int baselinePasses = 0;
            int baselineQueries = 0;
            long beforeAllocated = 0;
            long start = 0;
            AddStep("load 12 visible rank panels with duplicate hashes", () =>
            {
                coordinator = RankSnapshotCoordinator.For(Realm);
                var beatmaps = Enumerable.Range(0, 6).Select(_ => TestResources.CreateTestBeatmapSetInfo(1).Beatmaps.First()).ToArray();
                panels = Enumerable.Range(0, 12).Select(i => new PanelLocalRankDisplay(beatmaps[i % 6])).ToArray();
                foreach (var panel in panels)
                    Add(panel);
            });
            AddUntilStep("initial shared pass finishes", () => coordinator.IsIdle && coordinator.WorkerPassCount > 0);
            AddStep("write ten unrelated transactions", () =>
            {
                baselinePasses = coordinator.WorkerPassCount;
                baselineQueries = coordinator.WorkerQueryCount;
                beforeAllocated = GC.GetTotalAllocatedBytes(false);
                start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 10; i++)
                    Realm.Write(r => r.Add(new BeatmapSetInfo()));
            });
            AddUntilStep("coalesced rerun finished", () => coordinator.IsIdle && coordinator.WorkerPassCount > baselinePasses);
            AddAssert("one pass for unrelated write burst and six unique hashes", () =>
            {
                TestContext.Progress.WriteLine($"RANK_BURST_BENCH active_panels=12 hashes=6 unrelated_writes=10 "
                                               + $"passes={coordinator.WorkerPassCount - baselinePasses} queries={coordinator.WorkerQueryCount - baselineQueries} "
                                               + $"wall_ms={Stopwatch.GetElapsedTime(start).TotalMilliseconds:F2} "
                                               + $"process_alloc_kib={(GC.GetTotalAllocatedBytes(false) - beforeAllocated) / 1024.0:F1}");
                return coordinator.WorkerPassCount - baselinePasses == 1 && coordinator.WorkerQueryCount - baselineQueries == 6;
            });
            AddStep("dispose all", () =>
            {
                foreach (var panel in panels)
                    Remove(panel, true);
            });
        }

        [Test]
        public void TestRankSnapshotResetDuringWorkerQuery()
        {
            resetRankTestScene();
            PanelLocalRankDisplay panel = null!;
            BeatmapInfo beatmap = null!;
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            Task? releaseTask = null;
            RankSnapshotCoordinator coordinator = null!;

            AddStep("load blocked rank snapshot", () =>
            {
                coordinator = RankSnapshotCoordinator.For(Realm);
                beatmap = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                coordinator.BeforeQueryForTesting = () =>
                {
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(4)))
                        throw new TimeoutException("rank worker release gate timed out");
                };
                Add(panel = new PanelLocalRankDisplay(beatmap));
            });
            AddUntilStep("worker entered Realm", () => entered.IsSet);
            AddStep("release on independent thread and reset during query", () =>
            {
                // Never rely on the blocked Update thread to release the worker.
                releaseTask = Task.Run(() =>
                {
                    if (!release.Wait(TimeSpan.FromSeconds(1)))
                        release.Set();
                });
                using (Realm.BlockAllOperations("rank snapshot inflight reset"))
                {
                }
                coordinator.BeforeQueryForTesting = null;
            });
            AddUntilStep("post-reset snapshot finishes", () => coordinator.IsIdle && !panel.HasRank);
            AddStep("commit after worker reset", () =>
            {
                var score = TestResources.CreateTestScoreInfo(beatmap);
                score.BeatmapHash = beatmap.Hash;
                score.User = API.LocalUser.Value;
                score.Rank = ScoreRank.S;
                Realm.WriteAsync(r => r.Add(score, update: true));
            });
            AddUntilStep("fresh post-reset rank appears", () => panel.CurrentRank == ScoreRank.S);
            AddStep("dispose gate and panel", () =>
            {
                release.Set();
                coordinator.BeforeQueryForTesting = null;
                Remove(panel, true);
                // The helper is independent of Update, but wait until it has stopped
                // before disposing its gate to avoid a race on its Wait call.
                Assert.That(releaseTask, Is.Not.Null);
                Assert.That(releaseTask!.IsCompleted, Is.True);
                entered.Dispose();
                release.Dispose();
            });
        }

        [Test]
        public void TestLoadedRankSnapshotFollowsWorkerImportDeleteRestoreAndReuse()
        {
            resetRankTestScene();
            PanelLocalRankDisplay panel = null!;
            BeatmapInfo first = null!;
            BeatmapInfo second = null!;
            Guid scoreId = Guid.Empty;

            AddStep("load rank panel", () =>
            {
                first = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                second = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                Add(panel = new PanelLocalRankDisplay(first));
            });
            AddUntilStep("initial snapshot empty", () => panel.IsLoaded && !panel.HasRank);
            AddStep("import score from worker", () =>
            {
                var score = TestResources.CreateTestScoreInfo(first);
                score.BeatmapHash = first.Hash;
                score.User = API.LocalUser.Value;
                score.Rank = ScoreRank.S;
                score.TotalScore = 100000;
                scoreId = score.ID;
                Realm.WriteAsync(r => r.Add(score, update: true));
            });
            AddUntilStep("rank appears", () => panel.CurrentRank == ScoreRank.S);
            AddStep("update imported score", () => Realm.Write(r => r.Find<ScoreInfo>(scoreId)!.Rank = ScoreRank.A));
            AddUntilStep("rank update appears", () => panel.CurrentRank == ScoreRank.A);
            AddStep("soft-delete score", () => Realm.Write(r => r.Find<ScoreInfo>(scoreId)!.DeletePending = true));
            AddUntilStep("rank disappears", () => !panel.HasRank);
            AddStep("restore score", () => Realm.Write(r => r.Find<ScoreInfo>(scoreId)!.DeletePending = false));
            AddUntilStep("rank restored", () => panel.CurrentRank == ScoreRank.A);
            AddUntilStep("rank worker quiesced", () => RankSnapshotCoordinator.For(Realm).IsIdle);
            AddStep("reset Realm while rank panel loaded", () =>
            {
                using (Realm.BlockAllOperations("rank snapshot isolated reset"))
                {
                }
            });
            AddUntilStep("rank survives rebind", () => panel.CurrentRank == ScoreRank.A);
            AddStep("reset while rank snapshot is pending", () =>
            {
                // Initial panel invalidation is pending on its own scheduler; reset must rebind
                // the transaction listener without requiring the panel to run a scheduled task.
                panel.Beatmap = second;
                using (Realm.BlockAllOperations("rank snapshot pending reset"))
                {
                }
            });
            AddUntilStep("pending reset leaves new map empty", () => !panel.HasRank);
            AddStep("reuse for empty map", () => panel.Beatmap = second);
            AddUntilStep("stale rank rejected", () => !panel.HasRank);
            AddStep("deactivate", () => panel.Active = false);
            AddStep("dispose", () => Remove(panel, true));
        }

        [Test]
        [Explicit]
        public void TestBasics()
        {
            CreateCarousel();
            RemoveAllBeatmaps();

            AddBeatmaps(10, randomMetadata: true);
            AddBeatmaps(10);
            AddBeatmaps(1);
        }

        [Test]
        [Explicit]
        public void TestSorting()
        {
            SortAndGroupBy(SortMode.Artist, GroupMode.None);
            SortAndGroupBy(SortMode.Difficulty, GroupMode.Difficulty);
            SortAndGroupBy(SortMode.Artist, GroupMode.Artist);
        }

        [Test]
        [Explicit]
        public void TestRemovals()
        {
            RemoveFirstBeatmap();
            RemoveAllBeatmaps();
        }

        [Test]
        [Explicit]
        public void TestLoadingDisplay()
        {
            AddStep("induce slow filtering", () => Carousel.FilterDelay = 2000);
            SortAndGroupBy(SortMode.Artist, GroupMode.None);
        }

        [Test]
        [Explicit]
        public void TestAddRemoveRepeatedOps()
        {
            AddRepeatStep("add beatmaps", () => BeatmapSets.Add(TestResources.CreateTestBeatmapSetInfo(RNG.Next(1, 4))), 20);
            AddRepeatStep("remove beatmaps", () => BeatmapSets.RemoveAt(RNG.Next(0, BeatmapSets.Count)), 20);
        }

        [Test]
        [Explicit]
        public void TestMasking()
        {
            AddStep("disable masking", () => Scroll.Masking = false);
            AddStep("enable masking", () => Scroll.Masking = true);
        }

        [Test]
        [Explicit]
        public void TestRandomStatus()
        {
            SortBy(SortMode.Title);
            AddStep("add beatmaps", () =>
            {
                for (int i = 0; i < 50; i++)
                {
                    var set = TestResources.CreateTestBeatmapSetInfo();
                    set.Status = Enum.GetValues<BeatmapOnlineStatus>().MinBy(_ => RNG.Next());

                    if (i % 2 == 0)
                        set.Status = BeatmapOnlineStatus.None;

                    BeatmapSets.Add(set);
                }
            });
        }

        [Test]
        public void TestClickExpiredPanel()
        {
            CreateCarousel();

            AddStep("set eager loading very low", () =>
            {
                Carousel.DistanceOffscreenToPreload = -100;
            });

            AddBeatmaps(50, 10);

            AddUntilStep("wait for panels", () => Carousel.ChildrenOfType<Panel>().Any());

            AddRepeatStep("click last panel", () => Carousel.ChildrenOfType<Panel>().FirstOrDefault()?.TriggerClick(), 20);
        }

        [Test]
        public void TestHighChurnUpdatesStillShowsPanels()
        {
            ScheduledDelegate updateTask = null!;

            AddBeatmaps(1, 1);

            AddStep("start constantly updating beatmap in background", () =>
            {
                updateTask = Scheduler.AddDelayed(() => { BeatmapSets.ReplaceRange(0, 1, [BeatmapSets.First()]); }, 1, true);
            });

            CreateCarousel();

            AddUntilStep("panels loaded", () => Carousel.ChildrenOfType<Panel>(), () => Is.Not.Empty);

            AddStep("end task", () => updateTask.Cancel());
        }

        [Test]
        [Explicit]
        public void TestPerformanceWithManyBeatmaps()
        {
            const int count = 200000;

            List<BeatmapSetInfo> generated = new List<BeatmapSetInfo>();

            AddStep($"populate {count} test beatmaps", () =>
            {
                generated.Clear();
                Task.Run(() =>
                {
                    for (int j = 0; j < count; j++)
                        generated.Add(CreateTestBeatmapSetInfo(3, true));
                }).ConfigureAwait(true);
            });

            AddUntilStep("wait for beatmaps populated", () => generated.Count, () => Is.GreaterThan(count / 3));
            AddUntilStep("this takes a while", () => generated.Count, () => Is.GreaterThan(count / 3 * 2));
            AddUntilStep("maybe they are done now", () => generated.Count, () => Is.EqualTo(count));

            AddStep("add all beatmaps", () => BeatmapSets.AddRange(generated));
        }

        [Test]
        public void TestSingleItemDisplayed()
        {
            CreateCarousel();
            RemoveAllBeatmaps();

            SortAndGroupBy(SortMode.Difficulty, GroupMode.None);
            AddBeatmaps(1, fixedDifficultiesPerSet: 1);
            AddUntilStep("single item is shown", () => this.ChildrenOfType<PanelBeatmapStandalone>().Count(), () => Is.EqualTo(1));
        }
    }
}
