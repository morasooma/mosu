using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Scoring;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneLastPlayedRealmPublication : SongSelectTestScene
    {
        // Resolved by OsuTestScene before SongSelectTestScene creates Realm and beatmap dependencies.
        protected override bool UseFreshStoragePerRun => true;

        private bool measureRealmFrames;
        private double realmMaxWorkMs;
        private double realmMaxFrameMs;
        private long realmPreviousFrame;

        protected override void Update()
        {
            base.Update();
            if (!measureRealmFrames)
                return;

            long frame = Stopwatch.GetTimestamp();
            if (realmPreviousFrame != 0)
                realmMaxFrameMs = Math.Max(realmMaxFrameMs, Stopwatch.GetElapsedTime(realmPreviousFrame, frame).TotalMilliseconds);
            realmPreviousFrame = frame;
            // Snapshot sampled during Update: previous completed frame, not this callback's elapsed time.
            realmMaxWorkMs = Math.Max(realmMaxWorkMs, Dependencies.Get<GameHost>().UpdateThread.PerformanceSnapshot.WorkMs);
        }

        [Test]
        public void TestLastPlayedRealmPublicationWithLargeLibrary()
        {
            const int setCount = 5000;
            Guid targetId = Guid.Empty;
            Guid beatmapId = Guid.Empty;
            IBindableList<BeatmapSetInfo> published = null!;
            Task? write = null;
            int notifications = 0;
            int replaced = 0;
            int bulkReplaced = 0;
            long callbackTicks = 0;
            long callbackBytes = 0;
            long firstPublication = 0;
            double workerMs = 0;
            double publicationMs = 0;
            int initialFilterCount = 0;
            long started = 0;
            long allocated = 0;
            string phase = string.Empty;

            AddStep("seed fresh Realm with 5000 one-difficulty sets", () =>
            {
                Realm.Write(r =>
                {
                    var ruleset = r.All<osu.Game.Rulesets.RulesetInfo>().First();
                    for (int i = 0; i < setCount; i++)
                    {
                        var set = new BeatmapSetInfo(new[]
                        {
                            new BeatmapInfo(ruleset, metadata: new BeatmapMetadata { Title = $"Set {i:00000}", Artist = "Library" })
                            {
                                DifficultyName = "Normal", Hash = $"fixture-{i}", StarRating = 3,
                            }
                        });
                        r.Add(set);
                        if (i == setCount / 2)
                        {
                            targetId = set.ID;
                            beatmapId = set.Beatmaps[0].ID;
                        }
                    }
                });
            });
            AddUntilStep("real detached store includes seeded sets", () => DetachedBeatmapStore.GetBeatmapSets(null).Count == setCount);
            LoadSongSelect();
            AddUntilStep("real carousel shows seeded sets", () => Carousel.MatchedBeatmapsCount == setCount);
            AddStep("subscribe to real store publications", () =>
            {
                published = DetachedBeatmapStore.GetBeatmapSets(null);
                published.BindCollectionChanged(onPublication);
            });

            for (int round = 0; round < 3; round++)
            {
                int iteration = round;
                var playedAt = DateTimeOffset.UtcNow.AddMinutes(iteration);
                measure($"last-played-{iteration}", r =>
                {
                    var beatmap = r.Find<BeatmapInfo>(beatmapId)!;
                    beatmap.LastPlayed = playedAt;
                }, () => notifications > 0 && published.First(s => s.ID == targetId).Beatmaps[0].LastPlayed == playedAt,
                    () => Assert.That(published.First(s => s.ID == targetId).Beatmaps[0].LastPlayed, Is.EqualTo(playedAt)));
            }

            AddStep("switch to last-played sorting and grouping", () =>
            {
                Config.SetValue(OsuSetting.SongSelectSortingMode, SortMode.LastPlayed);
                Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.LastPlayed);
            });
            WaitForFiltering();
            var sortedPlayedAt = DateTimeOffset.UtcNow.AddDays(1);
            measure("last-played-with-sort-and-group", r =>
            {
                r.Find<BeatmapInfo>(beatmapId)!.LastPlayed = sortedPlayedAt;
            }, () => notifications > 0 && published.First(s => s.ID == targetId).Beatmaps[0].LastPlayed == sortedPlayedAt,
                () => Assert.That(published.First(s => s.ID == targetId).Beatmaps[0].LastPlayed, Is.EqualTo(sortedPlayedAt)));

            measure("difficulty-and-status", r =>
            {
                var beatmap = r.Find<BeatmapInfo>(beatmapId)!;
                beatmap.StarRating = 5;
                beatmap.Status = BeatmapOnlineStatus.Ranked;
            }, () => notifications > 0 && published.First(s => s.ID == targetId).Beatmaps[0].StarRating == 5,
                () => Assert.That(published.First(s => s.ID == targetId).Beatmaps[0].Status, Is.EqualTo(BeatmapOnlineStatus.Ranked)));

            Guid insertedId = Guid.Empty;
            measure("score-only", r =>
            {
                var beatmap = r.Find<BeatmapInfo>(beatmapId)!;
                r.Add(new ScoreInfo(beatmap, beatmap.Ruleset) { Hash = Guid.NewGuid().ToString() });
            }, () => write!.IsCompleted, () => Assert.That(published.Count, Is.EqualTo(setCount)));

            var insertedPlayedAt = DateTimeOffset.UtcNow.AddHours(1);
            measure("insert-and-modify", r =>
            {
                var inserted = new BeatmapSetInfo(new[] { new BeatmapInfo(r.All<osu.Game.Rulesets.RulesetInfo>().First()) { DifficultyName = "Inserted" } });
                insertedId = inserted.ID;
                r.Add(inserted);
                r.Find<BeatmapInfo>(beatmapId)!.LastPlayed = insertedPlayedAt;
            }, () => published.Count == setCount + 1 && published.First(s => s.ID == targetId).Beatmaps[0].LastPlayed == insertedPlayedAt,
                () => Assert.That(published.First(s => s.ID == targetId).Beatmaps[0].LastPlayed, Is.EqualTo(insertedPlayedAt)));
            var deletedPlayedAt = DateTimeOffset.UtcNow.AddHours(2);
            measure("delete-and-modify", r =>
            {
                r.Remove(r.Find<BeatmapSetInfo>(insertedId)!);
                r.Find<BeatmapInfo>(beatmapId)!.LastPlayed = deletedPlayedAt;
            }, () => published.Count == setCount && published.First(s => s.ID == targetId).Beatmaps[0].LastPlayed == deletedPlayedAt,
                () => Assert.That(published.First(s => s.ID == targetId).Beatmaps[0].LastPlayed, Is.EqualTo(deletedPlayedAt)));

            AddStep("unregister publication observer", () => published.UnbindEvents());

            void onPublication(object? sender, NotifyCollectionChangedEventArgs args)
            {
                long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                long before = Stopwatch.GetTimestamp();
                notifications++;
                if (firstPublication == 0) firstPublication = before;
                if (args.Action == NotifyCollectionChangedAction.Replace)
                {
                    replaced++;
                    if (args.NewItems!.Count > 1) bulkReplaced++;
                }
                callbackTicks += Stopwatch.GetTimestamp() - before;
                callbackBytes += GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            }

            void measure(string label, Action<Realms.Realm> change, Func<bool> publishedCondition, Action verify)
            {
                AddStep($"write {label} on Realm worker", () =>
                {
                    phase = label;
                    notifications = replaced = bulkReplaced = 0;
                    callbackTicks = callbackBytes = firstPublication = 0;
                    workerMs = realmMaxWorkMs = realmMaxFrameMs = publicationMs = 0;
                    initialFilterCount = Carousel.FilterCount;
                    realmPreviousFrame = 0;
                    measureRealmFrames = true;
                    allocated = GC.GetTotalAllocatedBytes(false);
                    started = Stopwatch.GetTimestamp();
                    write = Realm.WriteAsync(change);
                });
                AddUntilStep($"worker committed {label}", () => write!.IsCompleted);
                AddStep($"check worker {label}", () =>
                {
                    workerMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    Assert.That(write!.Exception, Is.Null);
                });
                AddUntilStep($"published {label}", publishedCondition);
                AddStep($"record publication {label}", () =>
                {
                    verify();
                    publicationMs = firstPublication == 0 ? -1 : Stopwatch.GetElapsedTime(started, firstPublication).TotalMilliseconds;
                });
                AddUntilStep($"carousel reconciled {label}", () => !Carousel.IsFiltering && (notifications == 0 || Carousel.FilterCount > initialFilterCount));
                AddStep($"verify {label}", () =>
                {
                    measureRealmFrames = false;
                    if (label.StartsWith("last-played", StringComparison.Ordinal) || label == "difficulty-and-status")
                    {
                        Assert.That(notifications, Is.EqualTo(1));
                        Assert.That(replaced, Is.EqualTo(1));
                        Assert.That(bulkReplaced, Is.Zero);
                        Assert.That(Carousel.FilterCount - initialFilterCount, Is.GreaterThan(0));
                    }
                    else if (label == "score-only")
                    {
                        Assert.That(notifications, Is.Zero);
                        Assert.That(Carousel.FilterCount - initialFilterCount, Is.Zero);
                    }
                    else
                    {
                        Assert.That(bulkReplaced, Is.EqualTo(1));
                        Assert.That(Carousel.MatchedBeatmapsCount, Is.EqualTo(published.Count));
                    }
                    TestContext.Progress.WriteLine($"LASTPLAYED_REALM phase={phase} sets={published.Count} subscriber_count=1 "
                                                   + $"worker_wall_ms={workerMs:F2} publication_wall_ms={publicationMs:F2} wall_ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F2} "
                                                   + $"frame_max_ms={realmMaxFrameMs:F2} update_work_previous_frame_sample_max_ms={realmMaxWorkMs:F2} "
                                                   + $"published_notifications={notifications} replacements={replaced} bulk_replacements={bulkReplaced} filters={Carousel.FilterCount - initialFilterCount} "
                                                   + $"observer_ms={callbackTicks * 1000.0 / Stopwatch.Frequency:F4} observer_bytes={callbackBytes} "
                                                   + $"process_alloc_bytes={GC.GetTotalAllocatedBytes(false) - allocated} "
                                                   + $"filtering={Carousel.IsFiltering}");
                });
            }
        }

    }
}
