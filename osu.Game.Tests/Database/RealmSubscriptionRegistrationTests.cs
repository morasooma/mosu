// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Tests.Resources;
using Realms;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class RealmSubscriptionRegistrationTests : RealmTest
    {
        [Test]
        public void TestRealmChangedCustomSubscriptionAcrossWorkerCommitAndContextReset()
        {
            RunTestWithRealm((realm, _) =>
            {
                int bindings = 0;
                int changes = 0;
                using var listener = realm.RegisterCustomSubscription(r =>
                {
                    bindings++;
                    r.RealmChanged += onChanged;
                    return new InvokeOnDisposal(() => r.RealmChanged -= onChanged);
                });

                realm.Run(r => r.Refresh());
                int before = changes;
                realm.WriteAsync(r => r.Add(new BeatmapSetInfo { DeletePending = false })).WaitSafely();
                realm.Run(r => r.Refresh());
                Assert.That(changes, Is.GreaterThan(before), "worker commit should invalidate the update-thread Realm");

                using (realm.BlockAllOperations("rank event reset"))
                {
                }
                realm.Run(r => r.Refresh());
                Assert.That(bindings, Is.EqualTo(2));
                before = changes;
                realm.WriteAsync(r => r.Add(new BeatmapSetInfo { DeletePending = false })).WaitSafely();
                realm.Run(r => r.Refresh());
                Assert.That(changes, Is.GreaterThan(before), "restored listener should invalidate again");

                listener.Dispose();
                before = changes;
                realm.WriteAsync(r => r.Add(new BeatmapSetInfo { DeletePending = false })).WaitSafely();
                realm.Run(r => r.Refresh());
                Assert.That(changes, Is.EqualTo(before), "disposed listener must not retain update notifications");

                void onChanged(object? sender, EventArgs args) => changes++;
            });
        }

        [Test]
        public void TestRankRegistrationVsWorkerSnapshotBenchmark()
        {
            RunTestWithRealm((realm, _) =>
            {
                string hash = "rank-snapshot-benchmark";
                realm.Write(r =>
                {
                    var ruleset = r.Add(CreateRuleset());
                    var beatmap = r.Add(new BeatmapInfo(ruleset));
                    for (int i = 0; i < 1500; i++)
                        r.Add(new ScoreInfo(beatmap, ruleset) { BeatmapHash = i % 5 == 0 ? hash : "unrelated", Hash = Guid.NewGuid().ToString() });
                });
                long begin = Stopwatch.GetTimestamp();
                using (realm.RegisterForNotifications(r => r.All<ScoreInfo>().Where(s => s.BeatmapHash == hash && !s.DeletePending), (_, _) => { }))
                {
                }
                double oldUpdateMs = Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
                begin = Stopwatch.GetTimestamp();
                using (realm.RegisterCustomSubscription(r =>
                       {
                           r.RealmChanged += onChanged;
                           return new InvokeOnDisposal(() => r.RealmChanged -= onChanged);
                       }))
                {
                    double newUpdateMs = Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
                    begin = Stopwatch.GetTimestamp();
                    var snapshot = realm.RunAsync(r => r.All<ScoreInfo>().Where(s => s.BeatmapHash == hash && !s.DeletePending)
                                                          .ToArray().Select(s => s.TotalScore).Max());
                    snapshot.WaitSafely();
                    double workerMs = Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
                    TestContext.Progress.WriteLine($"RANK_SNAPSHOT_BENCH indexed_scores=1500 old_update_register_dispose_ms={oldUpdateMs:F2} "
                                                   + $"new_update_event_register_ms={newUpdateMs:F2} worker_snapshot_wall_ms={workerMs:F2}");
                }

                void onChanged(object? sender, EventArgs args) { }
            });
        }

        [Test]
        public void TestFiveRankSubscriptionsOnRealScoreQuery()
        {
            RunTestWithRealm((realm, _) =>
            {
                string[] hashes = Enumerable.Range(0, 5).Select(i => $"rank-benchmark-{i}").ToArray();
                realm.Write(r =>
                {
                    var ruleset = r.Add(CreateRuleset());
                    var beatmap = r.Add(new BeatmapInfo(ruleset));
                    for (int i = 0; i < 1500; i++)
                        r.Add(new ScoreInfo(beatmap, ruleset) { BeatmapHash = hashes[i % hashes.Length], Hash = Guid.NewGuid().ToString() });
                });

                long allocated = GC.GetAllocatedBytesForCurrentThread();
                var baseline = Stopwatch.StartNew();
                var registrations = new List<IDisposable>();
                var times = new double[hashes.Length];
                for (int i = 0; i < hashes.Length; i++)
                {
                    string hash = hashes[i];
                    long start = Stopwatch.GetTimestamp();
                    registrations.Add(realm.RegisterForNotifications(r => r.All<ScoreInfo>().Where(s => s.BeatmapHash == hash && !s.DeletePending), (_, _) => { }));
                    times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                }
                baseline.Stop();
                TestContext.Progress.WriteLine($"RANK_REALM_BENCH same_frame_ms={baseline.Elapsed.TotalMilliseconds:F2} "
                                               + $"individual_ms={string.Join(',', times.Select(t => t.ToString("F2")))} "
                                               + $"allocated_kib={(GC.GetAllocatedBytesForCurrentThread() - allocated) / 1024.0:F1}");

                foreach (var registration in registrations)
                    registration.Dispose();

                // Warm both paths before comparison; first-ever Realm query includes startup overhead.
                var warm = new List<IDisposable>();
                for (int i = 0; i < hashes.Length; i++)
                {
                    string hash = hashes[i];
                    warm.Add(realm.RegisterForNotifications(r => r.All<ScoreInfo>().Where(s => s.BeatmapHash == hash && !s.DeletePending), (_, _) => { }));
                }
                foreach (var registration in warm)
                    registration.Dispose();

                // The same production query, all five registrations in one frame.
                var sameFrameRegistrations = new List<IDisposable>();
                long sameFrameStart = Stopwatch.GetTimestamp();
                for (int i = 0; i < hashes.Length; i++)
                {
                    string hash = hashes[i];
                    sameFrameRegistrations.Add(realm.RegisterForNotifications(r => r.All<ScoreInfo>().Where(s => s.BeatmapHash == hash && !s.DeletePending), (_, _) => { }));
                }
                double warmSameFrameRegistration = Stopwatch.GetElapsedTime(sameFrameStart).TotalMilliseconds;
                foreach (var registration in sameFrameRegistrations)
                    registration.Dispose();
                double warmSameFrameTotal = Stopwatch.GetElapsedTime(sameFrameStart).TotalMilliseconds;

                // Same five indexed-hash subscriptions and disposal, one per frame (carousel cap).
                double maxFrame = 0;
                double total = 0;
                for (int i = 0; i < hashes.Length; i++)
                {
                    string hash = hashes[i];
                    long start = Stopwatch.GetTimestamp();
                    using (realm.RegisterForNotifications(r => r.All<ScoreInfo>().Where(s => s.BeatmapHash == hash && !s.DeletePending), (_, _) => { }))
                    {
                    }
                    double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    maxFrame = Math.Max(maxFrame, elapsed);
                    total += elapsed;
                }
                TestContext.Progress.WriteLine($"RANK_REALM_BENCH warm_same_frame_registration_ms={warmSameFrameRegistration:F2} "
                                               + $"warm_same_frame_register_dispose_ms={warmSameFrameTotal:F2} "
                                               + $"amortised_max_frame_register_dispose_ms={maxFrame:F2} total_ms={total:F2} indexed_scores=1500");
                Assert.That(registrations, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public void TestSubscriptionCollectionAndPropertyChanges()
        {
            int collectionChanges = 0;
            int propertyChanges = 0;

            ChangeSet? lastChanges = null;

            RunTestWithRealm((realm, _) =>
            {
                var registration = realm.RegisterForNotifications(r => r.All<BeatmapSetInfo>(), onChanged);

                realm.Run(r => r.Refresh());

                realm.Write(r => r.Add(TestResources.CreateTestBeatmapSetInfo()));
                realm.Run(r => r.Refresh());

                Assert.That(collectionChanges, Is.EqualTo(1));
                Assert.That(propertyChanges, Is.EqualTo(0));
                Assert.That(lastChanges?.InsertedIndices, Has.One.Items);
                Assert.That(lastChanges?.ModifiedIndices, Is.Empty);
                Assert.That(lastChanges?.NewModifiedIndices, Is.Empty);

                realm.Write(r => r.All<BeatmapSetInfo>().First().Beatmaps.First().EditorTimestamp = 5);
                realm.Run(r => r.Refresh());

                Assert.That(collectionChanges, Is.EqualTo(1));
                Assert.That(propertyChanges, Is.EqualTo(1));
                Assert.That(lastChanges?.InsertedIndices, Is.Empty);
                Assert.That(lastChanges?.ModifiedIndices, Has.One.Items);
                Assert.That(lastChanges?.NewModifiedIndices, Has.One.Items);

                registration.Dispose();
            });

            void onChanged(IRealmCollection<BeatmapSetInfo> sender, ChangeSet? changes)
            {
                lastChanges = changes;

                if (changes == null)
                    return;

                if (changes.HasCollectionChanges())
                {
                    Interlocked.Increment(ref collectionChanges);
                }
                else
                {
                    Interlocked.Increment(ref propertyChanges);
                }
            }
        }

        [Test]
        public void TestSubscriptionInitialChangeSetNull()
        {
            ChangeSet? firstChanges = null;
            int receivedChangesCount = 0;

            RunTestWithRealm((realm, _) =>
            {
                var registration = realm.RegisterForNotifications(r => r.All<BeatmapSetInfo>(), onChanged);

                realm.WriteAsync(r => r.Add(TestResources.CreateTestBeatmapSetInfo())).WaitSafely();

                realm.Run(r => r.Refresh());

                Assert.That(receivedChangesCount, Is.EqualTo(1));
                Assert.That(firstChanges, Is.Null);

                registration.Dispose();
            });

            void onChanged(IRealmCollection<BeatmapSetInfo> sender, ChangeSet? changes)
            {
                if (receivedChangesCount == 0)
                    firstChanges = changes;

                receivedChangesCount++;
            }
        }

        [Test]
        public void TestSubscriptionWithAsyncWrite()
        {
            ChangeSet? lastChanges = null;

            RunTestWithRealm((realm, _) =>
            {
                var registration = realm.RegisterForNotifications(r => r.All<BeatmapSetInfo>(), onChanged);

                realm.Run(r => r.Refresh());

                realm.WriteAsync(r => r.Add(TestResources.CreateTestBeatmapSetInfo())).WaitSafely();

                realm.Run(r => r.Refresh());

                Assert.That(lastChanges?.InsertedIndices, Has.One.Items);

                registration.Dispose();
            });

            void onChanged(IRealmCollection<BeatmapSetInfo> sender, ChangeSet? changes) => lastChanges = changes;
        }

        [Test]
        public void TestPropertyChangedSubscription()
        {
            RunTestWithRealm((realm, _) =>
            {
                bool? receivedValue = null;

                realm.Write(r => r.Add(TestResources.CreateTestBeatmapSetInfo()));

                using (realm.SubscribeToPropertyChanged(r => r.All<BeatmapSetInfo>().First(), setInfo => setInfo.Protected, val => receivedValue = val))
                {
                    Assert.That(receivedValue, Is.False);

                    realm.Write(r => r.All<BeatmapSetInfo>().First().Protected = true);

                    realm.Run(r => r.Refresh());

                    Assert.That(receivedValue, Is.True);
                }
            });
        }

        [Test]
        public void TestSubscriptionWithContextLoss()
        {
            IEnumerable<BeatmapSetInfo>? resolvedItems = null;
            ChangeSet? lastChanges = null;

            RunTestWithRealm((realm, _) =>
            {
                realm.Write(r => r.Add(TestResources.CreateTestBeatmapSetInfo()));

                var registration = realm.RegisterForNotifications(r => r.All<BeatmapSetInfo>(), onChanged);

                testEventsArriving(true);

                // All normal until here.
                // Now let's yank the main realm context.
                resolvedItems = null;
                lastChanges = null;

                using (realm.BlockAllOperations("testing"))
                    Assert.That(resolvedItems, Is.Empty);

                realm.Write(r => r.Add(TestResources.CreateTestBeatmapSetInfo()));

                testEventsArriving(true);

                // Now let's try unsubscribing.
                resolvedItems = null;
                lastChanges = null;

                registration.Dispose();

                realm.Write(r => r.Add(TestResources.CreateTestBeatmapSetInfo()));

                testEventsArriving(false);

                // And make sure even after another context loss we don't get firings.
                using (realm.BlockAllOperations("testing"))
                    Assert.That(resolvedItems, Is.Null);

                realm.Write(r => r.Add(TestResources.CreateTestBeatmapSetInfo()));

                testEventsArriving(false);

                void testEventsArriving(bool shouldArrive)
                {
                    realm.Run(r => r.Refresh());

                    if (shouldArrive)
                        Assert.That(resolvedItems, Has.One.Items);
                    else
                        Assert.That(resolvedItems, Is.Null);

                    realm.Write(r =>
                    {
                        r.RemoveAll<BeatmapSetInfo>();
                        r.RemoveAll<RulesetInfo>();
                    });

                    realm.Run(r => r.Refresh());

                    if (shouldArrive)
                        Assert.That(lastChanges?.DeletedIndices, Has.One.Items);
                    else
                        Assert.That(lastChanges, Is.Null);
                }
            });

            void onChanged(IRealmCollection<BeatmapSetInfo> sender, ChangeSet? changes)
            {
                if (changes == null)
                    resolvedItems = sender;

                lastChanges = changes;
            }
        }

        [Test]
        public void TestCustomRegisterWithContextLoss()
        {
            RunTestWithRealm((realm, _) =>
            {
                BeatmapSetInfo? beatmapSetInfo = null;

                realm.Write(r => r.Add(TestResources.CreateTestBeatmapSetInfo()));

                var subscription = realm.RegisterCustomSubscription(r =>
                {
                    beatmapSetInfo = r.All<BeatmapSetInfo>().First();

                    return new InvokeOnDisposal(() => beatmapSetInfo = null);
                });

                Assert.That(beatmapSetInfo, Is.Not.Null);

                using (realm.BlockAllOperations("testing"))
                {
                    // custom disposal action fired when context lost.
                    Assert.That(beatmapSetInfo, Is.Null);
                }

                // re-registration after context restore.
                realm.Run(r => r.Refresh());
                Assert.That(beatmapSetInfo, Is.Not.Null);

                subscription.Dispose();

                Assert.That(beatmapSetInfo, Is.Null);

                using (realm.BlockAllOperations("testing"))
                    Assert.That(beatmapSetInfo, Is.Null);

                realm.Run(r => r.Refresh());
                Assert.That(beatmapSetInfo, Is.Null);
            });
        }

        [Test]
        public void TestPropertyChangedSubscriptionWithContextLoss()
        {
            RunTestWithRealm((realm, _) =>
            {
                bool? receivedValue = null;

                realm.Write(r => r.Add(TestResources.CreateTestBeatmapSetInfo()));

                var subscription = realm.SubscribeToPropertyChanged(
                    r => r.All<BeatmapSetInfo>().First(),
                    setInfo => setInfo.Protected,
                    val => receivedValue = val);

                Assert.That(receivedValue, Is.Not.Null);
                receivedValue = null;

                using (realm.BlockAllOperations("testing"))
                {
                }

                // re-registration after context restore.
                realm.Run(r => r.Refresh());
                Assert.That(receivedValue, Is.Not.Null);

                subscription.Dispose();
                receivedValue = null;

                using (realm.BlockAllOperations("testing"))
                    Assert.That(receivedValue, Is.Null);

                realm.Run(r => r.Refresh());
                Assert.That(receivedValue, Is.Null);
            });
        }
    }
}
