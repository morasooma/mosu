// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.Select.Filter;
using osu.Game.Utils;

namespace osu.Game.Screens.Select
{
    public class BeatmapCarouselFilterSorting : ICarouselFilter
    {
        public int BeatmapItemsCount { get; private set; }

        private readonly Func<FilterCriteria> getCriteria;

        public BeatmapCarouselFilterSorting(Func<FilterCriteria> getCriteria)
        {
            this.getCriteria = getCriteria;
        }

        public async Task<List<CarouselItem>> Run(IEnumerable<CarouselItem> items, CancellationToken cancellationToken) => await Task.Run(() =>
        {
            var criteria = getCriteria();

            bool groupedSets = BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria);

            cancellationToken.ThrowIfCancellationRequested();
            // The matching stage already supplies a list. Only materialise other inputs,
            // and do not enumerate a lazy source separately to obtain its count.
            var itemsToSort = items as List<CarouselItem>;
            if (itemsToSort == null)
            {
                itemsToSort = new List<CarouselItem>();
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    itemsToSort.Add(item);
                }
            }

            if (!groupedSets && criteria.Sort is SortMode.Difficulty or SortMode.Performance)
            {
                var metricSorted = sortByRating(itemsToSort, criteria, cancellationToken);
                BeatmapItemsCount = metricSorted.Count;
                return metricSorted;
            }

            Func<BeatmapInfo, double>? aggregateSelector = !groupedSets ? null : criteria.Sort switch
            {
                SortMode.BPM => static beatmap => beatmap.BPM,
                SortMode.Length => static beatmap => beatmap.Length,
                SortMode.LastPlayed => static beatmap => (beatmap.LastPlayed ?? DateTimeOffset.MinValue).ToUnixTimeSeconds(),
                _ => null
            };
            Dictionary<BeatmapSetInfo, double?>? aggregateValues = null;
            if (aggregateSelector != null)
            {
                // Scope the cache to this operation: hidden flags and metadata may change
                // between filters. Distinct detached objects must keep their own values.
                aggregateValues = new Dictionary<BeatmapSetInfo, double?>(ReferenceEqualityComparer.Instance);
                foreach (var item in itemsToSort)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var set = ((BeatmapInfo)item.Model).BeatmapSet!;
                    if (!aggregateValues.ContainsKey(set))
                        aggregateValues.Add(set, visibleMaximum(set, aggregateSelector, cancellationToken));
                }
            }

            var sorted = itemsToSort.Order(Comparer<CarouselItem>.Create((a, b) =>
            {
                var ab = (BeatmapInfo)a.Model;
                var bb = (BeatmapInfo)b.Model;

                if (groupedSets)
                {
                    if (ab.BeatmapSet!.Equals(bb.BeatmapSet))
                        return compareDifficulty(ab, bb, criteria);

                    // If we're grouping by sets, all fallback sorts need to be aggregates for the set.
                    return compare(ab, bb, criteria, aggregateValues);
                }

                return compare(ab, bb, criteria, null);
            })).ToList();

            cancellationToken.ThrowIfCancellationRequested();
            BeatmapItemsCount = sorted.Count;
            return sorted;
        }, cancellationToken).ConfigureAwait(false);

        private readonly record struct RatingSortKey(CarouselItem Item, double Rating, string Title, DateTimeOffset DateAdded, Guid SetId, int Index);

        private static List<CarouselItem> sortByRating(List<CarouselItem> items, FilterCriteria criteria, CancellationToken token)
        {
            // Project once per row, not twice per comparison. In particular, Realm
            // metadata getters and the mod snapshot dictionary must stay out of O(n log n).
            var keys = new RatingSortKey[items.Count];
            bool performance = criteria.Sort == SortMode.Performance;
            for (int i = 0; i < items.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var map = (BeatmapInfo)items[i].Model;
                var set = map.BeatmapSet!;
                keys[i] = new RatingSortKey(items[i], performance ? criteria.GetPp(map) : criteria.GetSortStars(map),
                    set.Metadata.Title, set.DateAdded, set.ID, i);
            }

            Array.Sort(keys, Comparer<RatingSortKey>.Create((a, b) =>
            {
                int comparison = a.Rating.CompareTo(b.Rating);
                if (comparison == 0) comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.Title, b.Title);
                if (comparison == 0) comparison = b.DateAdded.CompareTo(a.DateAdded);
                if (comparison == 0) comparison = b.SetId.CompareTo(a.SetId);
                // Array.Sort is not stable. Retain the original input order for exact
                // ties, as Enumerable.Order did (including maps from the same set).
                return comparison != 0 ? comparison : a.Index.CompareTo(b.Index);
            }));

            token.ThrowIfCancellationRequested();
            var result = new List<CarouselItem>(keys.Length);
            foreach (var key in keys) result.Add(key.Item);
            return result;
        }

        private static int compare(BeatmapInfo a, BeatmapInfo b, FilterCriteria criteria, IReadOnlyDictionary<BeatmapSetInfo, double?>? aggregateValues)
        {
            int comparison;

            switch (criteria.Sort)
            {
                case SortMode.Artist:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Artist, b.BeatmapSet!.Metadata.Artist);
                    if (comparison == 0)
                        goto case SortMode.Title;
                    break;

                case SortMode.Title:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Title, b.BeatmapSet!.Metadata.Title);
                    break;

                case SortMode.Author:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Author.Username, b.BeatmapSet!.Metadata.Author.Username);
                    break;

                case SortMode.Source:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Source, b.BeatmapSet!.Metadata.Source);
                    break;

                case SortMode.Difficulty:
                    comparison = criteria.GetSortStars(a).CompareTo(criteria.GetSortStars(b));
                    break;

                case SortMode.Performance:
                    comparison = criteria.GetPp(a).CompareTo(criteria.GetPp(b));
                    break;

                case SortMode.DateAdded:
                    comparison = b.BeatmapSet!.DateAdded.CompareTo(a.BeatmapSet!.DateAdded);
                    break;

                case SortMode.DateRanked:
                    comparison = Nullable.Compare(b.BeatmapSet!.DateRanked, a.BeatmapSet!.DateRanked);
                    break;

                case SortMode.DateSubmitted:
                    comparison = Nullable.Compare(b.BeatmapSet!.DateSubmitted, a.BeatmapSet!.DateSubmitted);
                    break;

                case SortMode.LastPlayed:
                    if (aggregateValues != null)
                        comparison = Nullable.Compare(aggregateValues[b.BeatmapSet!], aggregateValues[a.BeatmapSet!]);
                    else
                        comparison = Nullable.Compare(b.LastPlayed, a.LastPlayed);
                    break;

                case SortMode.BPM:
                    if (aggregateValues != null)
                        comparison = Nullable.Compare(aggregateValues[a.BeatmapSet!], aggregateValues[b.BeatmapSet!]);
                    else
                        comparison = a.BPM.CompareTo(b.BPM);
                    break;

                case SortMode.Length:
                    if (aggregateValues != null)
                        comparison = Nullable.Compare(aggregateValues[a.BeatmapSet!], aggregateValues[b.BeatmapSet!]);
                    else
                        comparison = a.Length.CompareTo(b.Length);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            // If the initial sort could not differentiate, attempt to use Title then DateAdded to order sets in a stable fashion.
            if (comparison == 0)
                comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Title, b.BeatmapSet!.Metadata.Title);

            // The directionality of this matches the current SortMode.DateAdded, but we may want to reconsider if that becomes a user decision (ie. asc / desc).
            if (comparison == 0)
                comparison = b.BeatmapSet!.DateAdded.CompareTo(a.BeatmapSet!.DateAdded);

            // If both failed to break the tie, fallback to our internal GUID for stability.
            // This basically means it's a stable random sort.
            if (comparison == 0)
                comparison = b.BeatmapSet!.ID.CompareTo(a.BeatmapSet!.ID);

            return comparison;
        }

        private static int compareDifficulty(BeatmapInfo a, BeatmapInfo b, FilterCriteria criteria)
        {
            int comparison = a.Ruleset.CompareTo(b.Ruleset);

            if (comparison == 0)
                comparison = criteria.GetSortStars(a).CompareTo(criteria.GetSortStars(b));

            return comparison;
        }

        private static double? visibleMaximum(BeatmapSetInfo set, Func<BeatmapInfo, double> selector, CancellationToken cancellationToken)
        {
            double? maximum = null;
            foreach (var beatmap in set.Beatmaps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (beatmap.Hidden)
                    continue;

                double value = selector(beatmap);
                // Match Enumerable.Max(double): a finite value supersedes NaN,
                // while an all-NaN collection retains NaN.
                if (!maximum.HasValue || value > maximum.Value || double.IsNaN(maximum.Value))
                    maximum = value;
            }
            return maximum;
        }
    }
}
