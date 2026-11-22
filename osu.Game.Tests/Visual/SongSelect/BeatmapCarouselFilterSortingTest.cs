// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Scoring;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.SongSelect
{
    [TestFixture]
    public partial class BeatmapCarouselFilterSortingTest
    {
        [Test]
        public async Task TestSorting()
        {
            List<BeatmapSetInfo> beatmapSets = new List<BeatmapSetInfo>();

            const string zzz_lowercase = "zzzzz";
            const string zzz_uppercase = "ZZZZZ";
            const int diff_count = 5;

            for (int i = 0; i < 20; i++)
            {
                var set = TestResources.CreateTestBeatmapSetInfo(diff_count);

                if (i == 4)
                    set.Beatmaps.ForEach(b => b.Metadata.Artist = zzz_uppercase);

                if (i == 8)
                    set.Beatmaps.ForEach(b => b.Metadata.Artist = zzz_lowercase);

                if (i == 12)
                    set.Beatmaps.ForEach(b => b.Metadata.Author.Username = zzz_uppercase);

                if (i == 16)
                    set.Beatmaps.ForEach(b => b.Metadata.Author.Username = zzz_lowercase);

                beatmapSets.Add(set);
            }

            var results = await runSorting(SortMode.Author, beatmapSets);

            Assert.That(results.Last().Metadata.Author.Username, Is.EqualTo(zzz_uppercase));
            Assert.That(results.SkipLast(diff_count).Last().Metadata.Author.Username, Is.EqualTo(zzz_lowercase));

            results = await runSorting(SortMode.Artist, beatmapSets);

            Assert.That(results.Last().Metadata.Artist, Is.EqualTo(zzz_uppercase));
            Assert.That(results.SkipLast(diff_count).Last().Metadata.Artist, Is.EqualTo(zzz_lowercase));
        }

        [Test]
        public async Task TestSortingDateSubmitted()
        {
            List<BeatmapSetInfo> beatmapSets = new List<BeatmapSetInfo>();

            const string zzz_string = "zzzzz";

            for (int i = 0; i < 10; i++)
            {
                var set = TestResources.CreateTestBeatmapSetInfo(5);

                // A total of 6 sets have date submitted (4 don't)
                // A total of 5 sets have artist string (3 of which also have date submitted)

                if (i >= 2 && i < 8) // i = 2, 3, 4, 5, 6, 7 have submitted date
                    set.DateSubmitted = DateTimeOffset.Now.AddMinutes(i);
                if (i < 5) // i = 0, 1, 2, 3, 4 have matching string
                    set.Beatmaps.ForEach(b => b.Metadata.Artist = zzz_string);

                set.Beatmaps.ForEach(b => b.Metadata.Title = $"submitted: {set.DateSubmitted}");

                beatmapSets.Add(set);
            }

            var results = await runSorting(SortMode.DateSubmitted, beatmapSets);

            Assert.That(results.Count(), Is.EqualTo(50));

            Assert.That(results.Reverse().TakeWhile(b => b.BeatmapSet!.DateSubmitted == null).Count(), Is.EqualTo(20), () => "missing dates should be at the end");
            Assert.That(results.TakeWhile(b => b.BeatmapSet!.DateSubmitted != null).Count(), Is.EqualTo(30), () => "non-missing dates should be at the start");
        }

        [Test]
        public async Task TestSortByBpmUsesTitleAsTiebreaker()
        {
            List<BeatmapSetInfo> beatmapSets = [];

            // 2 sets with same BPM but different titles
            const int diff_count = 1;
            {
                var set = TestResources.CreateTestBeatmapSetInfo(diff_count);
                set.DateAdded = new DateTimeOffset(2025, 6, 11, 10, 0, 0, TimeSpan.Zero);
                set.Beatmaps.ForEach(b =>
                {
                    b.ID = Guid.Parse("00000000-0000-0000-0000-000000000000");
                    b.BPM = 175;
                    b.Metadata.Title = "ZZZ";
                });
                beatmapSets.Add(set);
            }
            {
                var set = TestResources.CreateTestBeatmapSetInfo(diff_count);
                set.DateAdded = new DateTimeOffset(2025, 6, 10, 10, 0, 0, TimeSpan.Zero);
                set.Beatmaps.ForEach(b =>
                {
                    b.ID = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
                    b.BPM = 175;
                    b.Metadata.Title = "AAA";
                });
                beatmapSets.Add(set);
            }

            var results = (await runSorting(SortMode.BPM, beatmapSets)).ToList();

            Assert.That(results[0].Metadata.Title, Is.EqualTo("AAA"));
            Assert.That(results[1].Metadata.Title, Is.EqualTo("ZZZ"));
        }

        [Test]
        public async Task TestSortByArtistUsesTitleAsTiebreaker()
        {
            List<BeatmapSetInfo> beatmapSets = new List<BeatmapSetInfo>();

            const int diff_count = 5;

            for (int i = 0; i < 20; i++)
            {
                var set = TestResources.CreateTestBeatmapSetInfo(diff_count);

                if (i == 4)
                {
                    set.Beatmaps.ForEach(b =>
                    {
                        b.Metadata.Artist = "ZZZ";
                        b.Metadata.Title = "AAA";
                    });
                }

                if (i == 8)
                {
                    set.Beatmaps.ForEach(b =>
                    {
                        b.Metadata.Artist = "ZZZ";
                        b.Metadata.Title = "ZZZ";
                    });
                }

                beatmapSets.Add(set);
            }

            var results = await runSorting(SortMode.Artist, beatmapSets);

            Assert.That(() =>
            {
                var lastItem = results.Last();
                return lastItem.Metadata.Artist == "ZZZ" && lastItem.Metadata.Title == "ZZZ";
            });

            Assert.That(() =>
            {
                var secondLastItem = results.SkipLast(diff_count).Last();
                return secondLastItem.Metadata.Artist == "ZZZ" && secondLastItem.Metadata.Title == "AAA";
            });
        }

        /// <summary>
        /// Ensures stability is maintained on different sort modes for items with equal properties.
        /// </summary>
        [Test]
        public async Task TestSortingStabilityDateAdded()
        {
            List<BeatmapSetInfo> beatmapSets = new List<BeatmapSetInfo>();

            for (int i = 0; i < 10; i++)
            {
                var set = TestResources.CreateTestBeatmapSetInfo();

                set.DateAdded = DateTimeOffset.FromUnixTimeSeconds(i);

                // only need to set the first as they are a shared reference.
                var beatmap = set.Beatmaps.First();

                beatmap.Metadata.Artist = "a";
                beatmap.Metadata.Title = "b";

                beatmapSets.Add(set);
            }

            var results = await runSorting(SortMode.Title, beatmapSets);

            Assert.That(results.Select(b => b.BeatmapSet!.DateAdded), Is.Ordered.Descending);

            results = await runSorting(SortMode.Artist, beatmapSets);

            Assert.That(results.Select(b => b.BeatmapSet!.DateAdded), Is.Ordered.Descending);
        }

        [TestCase(SortMode.BPM)]
        [TestCase(SortMode.Length)]
        [TestCase(SortMode.LastPlayed)]
        public async Task TestAggregateIgnoresHiddenDifficulties(SortMode sort)
        {
            var low = TestResources.CreateTestBeatmapSetInfo(2);
            var high = TestResources.CreateTestBeatmapSetInfo(2);
            foreach (var b in low.Beatmaps)
            {
                b.BPM = 100;
                b.Length = 100;
                b.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(100);
            }
            low.Beatmaps[1].Hidden = true;
            low.Beatmaps[1].BPM = 999;
            low.Beatmaps[1].Length = 999;
            low.Beatmaps[1].LastPlayed = DateTimeOffset.FromUnixTimeSeconds(999);
            foreach (var b in high.Beatmaps)
            {
                b.BPM = 200;
                b.Length = 200;
                b.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(200);
            }
            var sorted = (await runSorting(sort, new List<BeatmapSetInfo> { low, high })).ToArray();
            Assert.That(sorted[0].BeatmapSet!.ID, Is.EqualTo(sort == SortMode.LastPlayed ? high.ID : low.ID));
        }

        [TestCase(SortMode.BPM)]
        [TestCase(SortMode.Length)]
        [TestCase(SortMode.LastPlayed)]
        public async Task TestAggregateSortMatchesUncachedOrder(SortMode sort)
        {
            var sets = Enumerable.Range(0, 36).Select(index =>
            {
                var set = TestResources.CreateTestBeatmapSetInfo(4);
                set.DateAdded = DateTimeOffset.FromUnixTimeSeconds(index % 3);
                foreach (var beatmap in set.Beatmaps)
                    beatmap.Metadata.Title = $"title {index % 5}";

                for (int difficulty = 0; difficulty < set.Beatmaps.Count; difficulty++)
                {
                    var beatmap = set.Beatmaps[difficulty];
                    double value = (index + difficulty * 7) % 11;
                    beatmap.BPM = index % 7 == 0 || difficulty == 1 ? double.NaN : value;
                    beatmap.Length = index % 7 == 0 || difficulty == 1 ? double.NaN : value;
                    beatmap.LastPlayed = difficulty == 1 ? null : DateTimeOffset.FromUnixTimeSeconds((long)value);
                    beatmap.Hidden = index % 9 == 0 || difficulty == 3;
                }

                return set;
            }).ToArray();
            Func<BeatmapInfo, double> selector = sort switch
            {
                SortMode.BPM => beatmap => beatmap.BPM,
                SortMode.Length => beatmap => beatmap.Length,
                _ => beatmap => (beatmap.LastPlayed ?? DateTimeOffset.MinValue).ToUnixTimeSeconds()
            };
            double? uncachedMaximum(BeatmapSetInfo set)
                => set.Beatmaps.Any(beatmap => !beatmap.Hidden)
                    ? set.Beatmaps.Where(beatmap => !beatmap.Hidden).Max(selector)
                    : null;

            var expected = (sort == SortMode.LastPlayed
                    ? sets.OrderByDescending(uncachedMaximum)
                    : sets.OrderBy(uncachedMaximum))
                .ThenBy(set => set.Metadata.Title, osu.Game.Utils.OrdinalSortByCaseStringComparer.DEFAULT)
                .ThenByDescending(set => set.DateAdded)
                .ThenByDescending(set => set.ID)
                .Select(set => set.Beatmaps[0].ID);
            // Only one difficulty matches the search, but the aggregate must still include
            // the other non-hidden difficulties in its set, including NaN and null values.
            var sorter = new BeatmapCarouselFilterSorting(() => new FilterCriteria { Sort = sort });
            var result = await sorter.Run(sets.Select(set => new CarouselItem(set.Beatmaps[0])), CancellationToken.None);
            Assert.That(result.Select(item => ((BeatmapInfo)item.Model).ID), Is.EqualTo(expected));
        }

        [TestCase(SortMode.BPM)]
        [TestCase(SortMode.Length)]
        [TestCase(SortMode.LastPlayed)]
        public async Task TestAggregateSortRecomputesAfterHiddenDifficultiesChange(SortMode sort)
        {
            var low = TestResources.CreateTestBeatmapSetInfo(2);
            var high = TestResources.CreateTestBeatmapSetInfo(2);
            foreach (var beatmap in low.Beatmaps.Concat(high.Beatmaps))
            {
                beatmap.BPM = beatmap.Length = 200;
                beatmap.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(200);
            }
            low.Beatmaps[0].BPM = low.Beatmaps[0].Length = 100;
            low.Beatmaps[0].LastPlayed = DateTimeOffset.FromUnixTimeSeconds(100);
            low.Beatmaps[1].BPM = low.Beatmaps[1].Length = 500;
            low.Beatmaps[1].LastPlayed = DateTimeOffset.FromUnixTimeSeconds(500);
            var items = new[] { new CarouselItem(low.Beatmaps[0]), new CarouselItem(high.Beatmaps[0]) };
            var sorter = new BeatmapCarouselFilterSorting(() => new FilterCriteria { Sort = sort });

            var before = await sorter.Run(items, CancellationToken.None);
            Assert.That(((BeatmapInfo)before[0].Model).BeatmapSet, Is.SameAs(sort == SortMode.LastPlayed ? low : high));

            low.Beatmaps[1].Hidden = true;
            var after = await sorter.Run(items, CancellationToken.None);
            Assert.That(((BeatmapInfo)after[0].Model).BeatmapSet, Is.SameAs(sort == SortMode.LastPlayed ? high : low));
        }

        [TestCase(SortMode.BPM)]
        [TestCase(SortMode.Length)]
        [TestCase(SortMode.LastPlayed)]
        [TestCase(SortMode.Title)]
        [TestCase(SortMode.Difficulty)]
        [TestCase(SortMode.Performance)]
        public async Task TestFilterPipelineSkipsBeatmapWithoutSet(SortMode sort)
        {
            var criteria = new FilterCriteria { Sort = sort, Group = GroupMode.None };
            var matcher = new BeatmapCarouselFilterMatching(() => criteria);
            var sorter = new BeatmapCarouselFilterSorting(() => criteria);
            var grouping = new BeatmapCarouselFilterGrouping
            {
                GetCriteria = () => criteria,
                GetCollections = () => [],
                GetLocalUserTopRanks = _ => new Dictionary<Guid, ScoreRank>(),
                GetFavouriteBeatmapSets = () => [],
            };
            var goodItems = TestResources.CreateTestBeatmapSetInfo(2).Beatmaps.Select(beatmap => new CarouselItem(beatmap)).ToArray();
            var orphan = new CarouselItem(new BeatmapInfo { BeatmapSet = null });
            var input = new[] { goodItems[0], orphan, goodItems[1] };

            var expected = await sorter.Run(goodItems, CancellationToken.None);
            var matched = await matcher.Run(input, CancellationToken.None);
            var sorted = await sorter.Run(matched, CancellationToken.None);
            var grouped = await grouping.Run(sorted, CancellationToken.None);

            Assert.That(sorted, Is.EqualTo(expected));
            Assert.That(grouped.Select(item => item.Model).OfType<GroupedBeatmap>().Select(item => item.Beatmap),
                Is.EqualTo(expected.Select(item => item.Model)));
            Assert.That(matcher.BeatmapItemsCount, Is.EqualTo(goodItems.Length));
        }

        [Test]
        public async Task TestSortingEnumeratesInputOnce()
        {
            int enumerations = 0;
            var set = TestResources.CreateTestBeatmapSetInfo(3);
            IEnumerable<CarouselItem> items()
            {
                enumerations++;
                foreach (var beatmap in set.Beatmaps)
                    yield return new CarouselItem(beatmap);
            }

            var sorter = new BeatmapCarouselFilterSorting(() => new FilterCriteria { Sort = SortMode.Title });
            var result = await sorter.Run(items(), CancellationToken.None);
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(enumerations, Is.EqualTo(1));
        }

        [Test]
        public void TestCancellationDuringEnumerationStopsObsoleteSort()
        {
            using var cancellation = new CancellationTokenSource();
            int visited = 0;
            var beatmap = TestResources.CreateTestBeatmapSetInfo(1).Beatmaps[0];
            IEnumerable<CarouselItem> items()
            {
                for (int index = 0; index < 10000; index++)
                {
                    visited++;
                    if (index == 4)
                        cancellation.Cancel();
                    yield return new CarouselItem(beatmap);
                }
            }

            var sorter = new BeatmapCarouselFilterSorting(() => new FilterCriteria { Sort = SortMode.Title });
            Assert.CatchAsync<OperationCanceledException>(async () => await sorter.Run(items(), cancellation.Token));
            Assert.That(visited, Is.LessThanOrEqualTo(5));
        }

        private static async Task<IEnumerable<BeatmapInfo>> runSorting(SortMode sort, List<BeatmapSetInfo> beatmapSets)
        {
            var sorter = new BeatmapCarouselFilterSorting(() => new FilterCriteria { Sort = sort });
            var carouselItems = await sorter.Run(beatmapSets.SelectMany(s => s.Beatmaps.Select(b => new CarouselItem(b))), CancellationToken.None);
            return carouselItems.Select(ci => ci.Model).OfType<BeatmapInfo>();
        }
    }
}
