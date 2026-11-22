using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Game.Database;
using osu.Framework.Testing;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using Realms;
using Explicit = NUnit.Framework.ExplicitAttribute;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Scoring;
using osu.Game.Graphics.Carousel;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Utils;

namespace osu.Game.Tests.NonVisual.Filtering
{
    [TestFixture]
    public partial class SongSelectRatingTest
    {
        private static readonly RulesetInfo ruleset = new RulesetInfo { ShortName = "osu", OnlineID = 0 };
        private static BeatmapInfo map(double stars = 4)
        {
            var result = new BeatmapInfo
            {
                ID = Guid.NewGuid(), MD5Hash = Guid.NewGuid().ToString(), Ruleset = ruleset, StarRating = stars,
                BeatmapSet = new BeatmapSetInfo { ID = Guid.NewGuid() }
            };
            result.BeatmapSet.Beatmaps.Add(result);
            return result;
        }

        [Test]
        public void ExperimentalSettingDefaultsOffAndPersistsOptIn()
        {
            using var storage = new TemporaryNativeStorage($"carousel-rating-option-{Guid.NewGuid():N}");
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.ForkSongSelectModdedRatings), Is.False);
                config.SetValue(OsuSetting.ForkSongSelectModdedRatings, true);
                Assert.That(config.Save(), Is.True);
            }
            using var reopened = new OsuConfigManager(storage);
            Assert.That(reopened.Get<bool>(OsuSetting.ForkSongSelectModdedRatings), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task WithoutSnapshotSortUsesLegacyNmStarsEvenWithRelax(bool relax)
        {
            var a = map(2);
            var b = map(6);
            var criteria = new FilterCriteria
            {
                Sort = SortMode.Difficulty, Ruleset = ruleset,
                Mods = relax ? new Mod[] { new OsuModRelax(), new OsuModDoubleTime() } : new Mod[] { new OsuModDoubleTime() }
            };
            var sorted = await new BeatmapCarouselFilterSorting(() => criteria).Run(new[] { new CarouselItem(b), new CarouselItem(a) }, CancellationToken.None);
            Assert.That(sorted.Select(i => i.Model), Is.EqualTo(new[] { a, b }));
            Assert.That(criteria.GetSortStars(a), Is.EqualTo(2));
            Assert.That(criteria.GetStars(a), Is.EqualTo(relax ? ForkDataStore.Instance?.GetRelaxStarRating(a.ID) ?? -1 : 2),
                "Existing Relax search filter semantics must not change with the sorting rollback");
        }

        [TestCase("en", "Sort and filter maps using selected mods (experimental)")]
        [TestCase("ru", "Сортировка и фильтры карт с учётом модов (экспериментально)")]
        [TestCase("es", "Ordenar y filtrar mapas según los mods seleccionados (experimental)")]
        [TestCase("ko", "선택한 모드를 반영하여 맵 정렬 및 필터링 (실험적)")]
        [TestCase("zh", "按所选模组排序和筛选谱面（实验性）")]
        public void ExperimentalSettingResolvesThroughRuntimeLocalisation(string locale, string caption)
        {
            using var config = new LocalisationConfig();
            using var manager = new LocalisationManager(config);
            using var store = new ResourceManagerLocalisationStore(locale);
            manager.AddLanguage(locale, store);
            config.SetValue(FrameworkSetting.Locale, locale);
            Assert.That(manager.GetLocalisedString(BackgroundCalculationStrings.ModdedCarouselRatings), Is.EqualTo(caption));
            string hint = manager.GetLocalisedString(BackgroundCalculationStrings.ModdedCarouselRatingsHint);
            Assert.That(hint, Is.Not.Empty);
            if (locale != "en")
                Assert.That(hint, Is.Not.EqualTo("Estimates SR/PP for the list, then calculates visible maps in the background. The order may change as results arrive. Off uses the previous saved-rating logic."));
        }

        private sealed class LocalisationConfig : FrameworkConfigManager
        {
            protected override string Filename => null!;
            public LocalisationConfig() : base(null!) { }
        }

        [Test]
        public async Task ExactSnapshotDrivesSortingAndBothFilters()
        {
            var a = map(2);
            var b = map(6);
            var source = new Dictionary<Guid, SongSelectRatingSnapshot.Value>
            {
                [a.ID] = new SongSelectRatingSnapshot.Value(a.MD5Hash, 8, 600),
                [b.ID] = new SongSelectRatingSnapshot.Value(b.MD5Hash, 3, 150)
            };
            var snapshot = new SongSelectRatingSnapshot(ruleset, new Mod[] { new OsuModDoubleTime() }, source);
            source[a.ID] = new SongSelectRatingSnapshot.Value(a.MD5Hash, 1, 1);
            foreach (var sort in new[] { SortMode.Difficulty, SortMode.Performance })
            {
                var criteria = new FilterCriteria { Sort = sort, Ruleset = ruleset }.WithRatings(snapshot);
                var results = await new BeatmapCarouselFilterSorting(() => criteria).Run(new[] { new CarouselItem(a), new CarouselItem(b) }, CancellationToken.None);
                Assert.That(results.Select(i => i.Model), Is.EqualTo(new[] { b, a }));
                Assert.That(BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria), Is.False);
            }
            var filter = new FilterCriteria { Ruleset = ruleset }.WithRatings(snapshot);
            filter.StarDifficulty = new FilterCriteria.OptionalRange<double> { Min = 7, IsLowerInclusive = true };
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(a, filter), Is.True);
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(b, filter), Is.False);
            filter.StarDifficulty = default;
            filter.PerformancePoints = new FilterCriteria.OptionalRange<double> { Min = 500, IsLowerInclusive = true };
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(a, filter), Is.True);
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(b, filter), Is.False);
            a.MD5Hash = "edited";
            Assert.That(snapshot.Stars(a), Is.Not.EqualTo(8));
            Assert.That(a.StarRating, Is.EqualTo(2));
        }

        [Test]
        public void ConfiguredRateEstimateDoesNotMutateMapOrMods()
        {
            var input = map();
            var dt = new OsuModDoubleTime();
            dt.SpeedChange.Value = 1.8;
            var snapshot = new SongSelectRatingSnapshot(ruleset, new Mod[] { dt }, new Dictionary<Guid, SongSelectRatingSnapshot.Value>());
            Assert.That(snapshot.Stars(input), Is.GreaterThan(4));
            Assert.That(input.StarRating, Is.EqualTo(4));
            Assert.That(dt.SpeedChange.Value, Is.EqualTo(1.8));
        }

        [Test]
        public async Task QueueIsVisibleOnlyBoundedAndReplacesObsoletePendingCards()
        {
            using var cache = new ProbeCache();
            int changed = 0;
            using var work = new SongSelectRatingWork(cache, ruleset, Array.Empty<Mod>(), true, "NM", () => Interlocked.Increment(ref changed));
            work.Start();
            await Task.Delay(400);
            Assert.That(cache.Starts, Is.Zero, "No whole-library decode on opening song select");
            var maps = Enumerable.Range(0, 100).Select(_ => map()).ToArray();
            work.Promote(maps);
            Assert.That(SpinWait.SpinUntil(() => cache.Starts == 2, 3000), Is.True);
            var replacement = map();
            work.Promote(new[] { replacement });
            cache.Release.Set();
            Assert.That(SpinWait.SpinUntil(() => work.Snapshot().Stars(replacement) == 9, 3000), Is.True);
            Assert.That(cache.Starts, Is.EqualTo(3));
            Assert.That(cache.MaximumRunning, Is.LessThanOrEqualTo(2));
            Assert.That(changed, Is.EqualTo(3));
            Assert.That(work.Snapshot().Pp(replacement), Is.EqualTo(500));
            work.Dispose();
            await work.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            int completedChanges = changed;
            work.Accept(new BeatmapDifficultyCache.DifficultyCacheLookup(map(), ruleset, null), result());
            Assert.That(changed, Is.EqualTo(completedChanges));
        }

        [Test]
        public async Task NewGenerationWaitsForCancellationAndRejectsOldSettings()
        {
            using var cache = new ProbeCache();
            var a = map();
            var dt = new OsuModDoubleTime();
            using var old = new SongSelectRatingWork(cache, ruleset, new Mod[] { dt }, true, "old", () => { });
            old.Start(); old.Promote(new[] { a });
            Assert.That(SpinWait.SpinUntil(() => cache.Starts == 1, 3000), Is.True);
            old.Dispose();
            dt.SpeedChange.Value = 1.8;
            using var current = new SongSelectRatingWork(cache, ruleset, new Mod[] { dt }, true, "new", () => { });
            current.Start(old.Completion);
            current.Accept(new BeatmapDifficultyCache.DifficultyCacheLookup(a, ruleset, new[] { new OsuModDoubleTime() }), result());
            Assert.That(current.Snapshot().Stars(a), Is.Not.EqualTo(9));
            current.Promote(new[] { a });
            cache.Release.Set();
            Assert.That(SpinWait.SpinUntil(() => current.Snapshot().Stars(a) == 9, 3000), Is.True);
            current.Dispose();
            await current.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Test]
        public void FullResultCannotBeDowngradedByStarOnlyPublication()
        {
            using var cache = new ProbeCache();
            using var work = new SongSelectRatingWork(cache, ruleset, Array.Empty<Mod>(), true, "NM", () => { });
            var a = map();
            var lookup = new BeatmapDifficultyCache.DifficultyCacheLookup(a, ruleset, null);
            work.Accept(lookup, result());
            work.Accept(lookup, new StarDifficulty(new DifficultyAttributes { StarRating = 8 }));
            Assert.That(work.Snapshot().Pp(a), Is.EqualTo(500));
            Assert.That(work.Snapshot().Stars(a), Is.EqualTo(9));
            work.Accept(new BeatmapDifficultyCache.DifficultyCacheLookup(a, new RulesetInfo { ShortName = "mania" }, null), result());
            Assert.That(work.Snapshot().Stars(a), Is.EqualTo(9));
        }

        [TestCase(SortMode.Difficulty)]
        [TestCase(SortMode.Performance)]
        public async Task ProjectedSortMatchesPreviousComparerIncludingStableTies(SortMode sort)
        {
            var random = new Random(1978);
            var maps = Enumerable.Range(0, 2000).Select(i => map(i % 17)).ToArray();
            var exact = new Dictionary<Guid, SongSelectRatingSnapshot.Value>();
            double[] edgeValues = { 0, -1, double.NaN, double.NegativeInfinity, double.PositiveInfinity, 4, 4 };
            for (int i = 0; i < maps.Length; i++)
            {
                if (i % 3 == 0 && i > 0) maps[i].BeatmapSet = maps[i - 1].BeatmapSet;
                else
                {
                    maps[i].Metadata.Title = i % 2 == 0 ? "A" : "a";
                    maps[i].BeatmapSet!.DateAdded = DateTimeOffset.UnixEpoch.AddDays(i % 7);
                }
                exact[maps[i].ID] = new SongSelectRatingSnapshot.Value(maps[i].MD5Hash,
                    edgeValues[i % edgeValues.Length], edgeValues[(i + 1) % edgeValues.Length]);
            }
            var items = maps.Select(m => new CarouselItem(m)).OrderBy(_ => random.Next()).ToList();
            // Duplicate rows and exact within-set ties must retain source order.
            items.Add(items[0]);
            var criteria = new FilterCriteria { Sort = sort, Ruleset = ruleset }.WithRatings(
                new SongSelectRatingSnapshot(ruleset, new Mod[] { new OsuModHidden(), new OsuModDoubleTime() }, exact));
            var original = items.ToArray();
            var expected = previousRatingSort(items, criteria);
            var actual = await new BeatmapCarouselFilterSorting(() => criteria).Run(items, CancellationToken.None);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(items, Is.EqualTo(original), "Source list is not sorted in place");
        }

        [Test]
        public void ProjectedSortHonoursCancellationBeforeKeyProjection()
        {
            var items = Enumerable.Range(0, 5000).Select(_ => new CarouselItem(map())).ToList();
            using var cancellation = new CancellationTokenSource();
            var sorter = new BeatmapCarouselFilterSorting(() =>
            {
                cancellation.Cancel();
                return new FilterCriteria { Sort = SortMode.Difficulty };
            });
            Assert.CatchAsync<OperationCanceledException>(async () => await sorter.Run(items, cancellation.Token));
        }

        // Frozen pre-projection comparer for same-process differential/timing coverage.
        private static List<CarouselItem> previousRatingSort(List<CarouselItem> items, FilterCriteria criteria)
            => items.Order(Comparer<CarouselItem>.Create((a, b) =>
            {
                var ab = (BeatmapInfo)a.Model;
                var bb = (BeatmapInfo)b.Model;
                int comparison = criteria.Sort == SortMode.Performance
                    ? criteria.GetPp(ab).CompareTo(criteria.GetPp(bb)) : criteria.GetStars(ab).CompareTo(criteria.GetStars(bb));
                if (comparison == 0) comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(ab.BeatmapSet!.Metadata.Title, bb.BeatmapSet!.Metadata.Title);
                if (comparison == 0) comparison = bb.BeatmapSet!.DateAdded.CompareTo(ab.BeatmapSet!.DateAdded);
                if (comparison == 0) comparison = bb.BeatmapSet!.ID.CompareTo(ab.BeatmapSet!.ID);
                return comparison;
            })).ToList();

        [Test, Explicit("Metadata-only first-pass throughput, no files or database")]
        public async Task MeasureLargeLibraryFirstPass()
        {
            var maps = Enumerable.Range(0, 50000).Select(i => map(1 + i % 90 / 10.0)).ToArray();
            for (int i = 0; i < maps.Length; i++)
            {
                maps[i].Metadata.Title = "Song " + (i % 5000);
                maps[i].BeatmapSet!.DateAdded = DateTimeOffset.UnixEpoch.AddDays(i % 365);
            }
            var items = maps.Select(m => new CarouselItem(m)).ToList();
            foreach (var sort in new[] { SortMode.Difficulty, SortMode.Performance })
            {
                var times = new List<double>();
                var baseline = new List<double>();
                var allocated = new List<long>();
                var beforeAllocated = new List<long>();
                for (int repeat = 0; repeat < 6; repeat++)
                {
                    List<CarouselItem>? expected = null;
                    List<CarouselItem>? actual = null;
                    foreach (bool optimized in repeat % 2 == 0 ? new[] { false, true } : new[] { true, false })
                    {
                        var criteria = new FilterCriteria { Sort = sort, Ruleset = ruleset }.WithRatings(
                            new SongSelectRatingSnapshot(ruleset, new Mod[] { new OsuModDoubleTime() }, new Dictionary<Guid, SongSelectRatingSnapshot.Value>()));
                        long allocationStart = GC.GetTotalAllocatedBytes(true);
                        var watch = Stopwatch.StartNew();
                        var sorted = optimized
                            ? await new BeatmapCarouselFilterSorting(() => criteria).Run(items, CancellationToken.None)
                            : await Task.Run(() => previousRatingSort(items, criteria));
                        watch.Stop();
                        long bytes = GC.GetTotalAllocatedBytes(true) - allocationStart;
                        if (repeat > 0)
                        {
                            (optimized ? times : baseline).Add(watch.Elapsed.TotalMilliseconds);
                            (optimized ? allocated : beforeAllocated).Add(bytes);
                        }
                        if (optimized) actual = sorted;
                        else expected = sorted;
                    }
                    Assert.That(actual, Is.EqualTo(expected));
                }
                TestContext.WriteLine($"50000 {sort} previous ms: {string.Join(", ", baseline.Select(t => t.ToString("F2")))}");
                TestContext.WriteLine($"50000 {sort} projected ms: {string.Join(", ", times.Select(t => t.ToString("F2")))}");
                TestContext.WriteLine($"50000 {sort} previous allocated B: {string.Join(", ", beforeAllocated)}");
                TestContext.WriteLine($"50000 {sort} projected allocated B: {string.Join(", ", allocated)}");
            }
        }

        [Test, Explicit("Complete metadata filtering/sorting/grouping path, no files or database")]
        public async Task MeasureLargeLibraryPipeline()
        {
            var maps = Enumerable.Range(0, 50000).Select(i => map(1 + i % 90 / 10.0)).ToArray();
            for (int i = 0; i < maps.Length; i++)
                maps[i].Metadata.Title = "Song " + i % 5000;
            var items = maps.Select(m => new CarouselItem(m)).ToList();
            var before = new List<double>();
            var after = new List<double>();
            for (int repeat = 0; repeat < 6; repeat++)
            {
                List<Guid>? expected = null;
                foreach (bool optimized in repeat % 2 == 0 ? new[] { false, true } : new[] { true, false })
                {
                    var criteria = new FilterCriteria { Sort = SortMode.Difficulty, Ruleset = ruleset }.WithRatings(
                        new SongSelectRatingSnapshot(ruleset, new Mod[] { new OsuModDoubleTime() }, new Dictionary<Guid, SongSelectRatingSnapshot.Value>()));
                    var grouping = new BeatmapCarouselFilterGrouping
                    {
                        GetCriteria = () => criteria,
                        GetCollections = () => new List<osu.Game.Collections.BeatmapCollection>(),
                        GetLocalUserTopRanks = _ => new Dictionary<Guid, ScoreRank>(),
                        GetFavouriteBeatmapSets = () => new HashSet<int>()
                    };
                    var watch = Stopwatch.StartNew();
                    var matched = await new BeatmapCarouselFilterMatching(() => criteria).Run(items, CancellationToken.None);
                    var sorted = optimized
                        ? await new BeatmapCarouselFilterSorting(() => criteria).Run(matched, CancellationToken.None)
                        : await Task.Run(() => previousRatingSort(matched, criteria));
                    var grouped = await grouping.Run(sorted, CancellationToken.None);
                    watch.Stop();
                    if (repeat > 0) (optimized ? after : before).Add(watch.Elapsed.TotalMilliseconds);
                    var ids = grouped.Select(i => i.Model).OfType<GroupedBeatmap>().Select(g => g.Beatmap.ID).ToList();
                    Assert.That(ids.Count, Is.EqualTo(50000));
                    if (expected == null) expected = ids;
                    else Assert.That(ids, Is.EqualTo(expected));
                }
            }
            TestContext.WriteLine($"50000 full matching/sorting/grouping previous ms: {string.Join(", ", before.Select(t => t.ToString("F2")))}");
            TestContext.WriteLine($"50000 full matching/sorting/grouping projected ms: {string.Join(", ", after.Select(t => t.ToString("F2")))}");
        }

        [Test, Explicit("Read-only owner library benchmark; requires MOSU_RATING_DB_ROOT")]
        public async Task MeasureOwnerLibraryReadOnly()
        {
            string root = Environment.GetEnvironmentVariable("MOSU_RATING_DB_ROOT") ?? throw new InvalidOperationException("MOSU_RATING_DB_ROOT is required");
            var read = Stopwatch.StartNew();
            List<BeatmapSetInfo> sets;
            using (var realm = Realm.GetInstance(new RealmConfiguration(Path.Combine(root, "client.realm")) { IsReadOnly = true, SchemaVersion = 52 }))
                sets = realm.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected).AsEnumerable().Select(s => s.Detach()).ToList();
            var pp = new ConcurrentDictionary<Guid, double>();
            var rx = new ConcurrentDictionary<Guid, ForkDataStore.RelaxBeatmapData>();
            using (var realm = Realm.GetInstance(new RealmConfiguration(Path.Combine(root, "fork.realm"))
            {
                IsReadOnly = true, SchemaVersion = 14,
                Schema = new[] { typeof(ForkBeatmapData), typeof(ForkScoreData), typeof(ForkSeasonalBackgroundData), typeof(ForkBackgroundClassificationData) }
            }))
                foreach (var row in realm.All<ForkBeatmapData>())
                {
                    pp[row.BeatmapID] = row.MaxPerformancePoints;
                    rx[row.BeatmapID] = new ForkDataStore.RelaxBeatmapData(row.RelaxTazikPpStarRating, row.RelaxTazikPpMaxPerformancePoints);
                }
            read.Stop();
            // Install only detached, read-only projections for the existing GetPP/GetRelax
            // consumers. Never call ForkDataStore's normal constructor: it opens writable
            // Realm and may migrate. This fixture is explicit and runs in its own testhost.
            var store = (ForkDataStore)RuntimeHelpers.GetUninitializedObject(typeof(ForkDataStore));
            typeof(ForkDataStore).GetField("ppCache", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, pp);
            typeof(ForkDataStore).GetField("relaxTazikPpCache", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, rx);
            var instance = typeof(ForkDataStore).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)!;
            var previousStore = instance.GetValue(null);
            instance.SetValue(null, store);
            try
            {
                var maps = sets.SelectMany(s => s.Beatmaps).ToArray();
                Assert.That(maps.Length, Is.GreaterThan(0), "Owner library must not be empty");
                TestContext.WriteLine($"OWNER read+detach ms={read.Elapsed.TotalMilliseconds:F2}; sets={sets.Count}; maps={maps.Length}; standard={maps.Count(m => m.Ruleset.ShortName == "osu")}; visible={maps.Count(m => !m.Hidden)}; pp_cached={maps.Count(m => pp.GetValueOrDefault(m.ID) > 0)}");
                foreach (bool relax in new[] { false, true })
                foreach (var sort in new[] { SortMode.Difficulty, SortMode.Performance })
                {
                    var mods = relax ? new Mod[] { new OsuModRelax(), new OsuModDoubleTime() } : new Mod[] { new OsuModHidden(), new OsuModDoubleTime() };
                    var before = new List<double>();
                    var after = new List<double>();
                    var beforeAlloc = new List<long>();
                    var afterAlloc = new List<long>();
                    for (int repeat = 0; repeat < 8; repeat++)
                    {
                        List<Guid>? expected = null;
                        foreach (bool optimized in repeat % 2 == 0 ? new[] { false, true } : new[] { true, false })
                        {
                            var criteria = new FilterCriteria { Sort = sort, Ruleset = ruleset }.WithRatings(
                                new SongSelectRatingSnapshot(ruleset, mods, new Dictionary<Guid, SongSelectRatingSnapshot.Value>()));
                            var items = maps.Select(m => new CarouselItem(m)).ToList();
                            var grouping = new BeatmapCarouselFilterGrouping
                            {
                                GetCriteria = () => criteria,
                                GetCollections = () => new List<osu.Game.Collections.BeatmapCollection>(),
                                GetLocalUserTopRanks = _ => new Dictionary<Guid, ScoreRank>(),
                                GetFavouriteBeatmapSets = () => new HashSet<int>()
                            };
                            long alloc = GC.GetTotalAllocatedBytes(true);
                            var watch = Stopwatch.StartNew();
                            var matched = await new BeatmapCarouselFilterMatching(() => criteria).Run(items, CancellationToken.None);
                            var sorted = optimized
                                ? await new BeatmapCarouselFilterSorting(() => criteria).Run(matched, CancellationToken.None)
                                : await Task.Run(() => previousRatingSort(matched, criteria));
                            var grouped = await grouping.Run(sorted, CancellationToken.None);
                            watch.Stop();
                            long bytes = GC.GetTotalAllocatedBytes(true) - alloc;
                            if (repeat > 0)
                            {
                                (optimized ? after : before).Add(watch.Elapsed.TotalMilliseconds);
                                (optimized ? afterAlloc : beforeAlloc).Add(bytes);
                            }
                            var ids = grouped.Select(i => i.Model).OfType<GroupedBeatmap>().Select(g => g.Beatmap.ID).ToList();
                            Assert.That(ids.Count, Is.GreaterThan(0));
                            if (expected == null) expected = ids;
                            else Assert.That(ids, Is.EqualTo(expected), "Every result ID and its order must match");
                        }
                    }
                    TestContext.WriteLine($"OWNER {sort} {(relax ? "RXDT" : "HDDT")} before_ms=[{string.Join(",", before.Select(t => t.ToString("F2")))}] after_ms=[{string.Join(",", after.Select(t => t.ToString("F2")))}]");
                    TestContext.WriteLine($"OWNER {sort} {(relax ? "RXDT" : "HDDT")} before_alloc_B=[{string.Join(",", beforeAlloc)}] after_alloc_B=[{string.Join(",", afterAlloc)}]");
                }
            }
            finally { instance.SetValue(null, previousStore); }
        }

        private static StarDifficulty result() => new StarDifficulty(new DifficultyAttributes { StarRating = 9 }, new PerformanceAttributes { Total = 500 });

        private partial class ProbeCache : BeatmapDifficultyCache
        {
            public readonly ManualResetEventSlim Release = new ManualResetEventSlim();
            public int Starts;
            public int MaximumRunning;
            private int running;

            protected override StarDifficulty? computeDifficulty(in DifficultyCacheLookup lookup, CancellationToken cancellationToken = default, bool calculatePerformance = true)
            {
                Interlocked.Increment(ref Starts);
                int count = Interlocked.Increment(ref running);
                int previous;
                do { previous = MaximumRunning; }
                while (count > previous && Interlocked.CompareExchange(ref MaximumRunning, count, previous) != previous);
                try
                {
                    Release.Wait(cancellationToken);
                    return result();
                }
                finally { Interlocked.Decrement(ref running); }
            }
        }
    }
}
