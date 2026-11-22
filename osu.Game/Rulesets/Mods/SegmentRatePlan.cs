using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// A deterministic, stateless rate function in source (beatmap/audio) milliseconds.
    /// Seeks, retries and replay rewinds therefore never depend on the history of updates.
    /// </summary>
    public sealed class SegmentRatePlan
    {
        public readonly record struct Segment(double Start, double End, double Rate);

        public static readonly SegmentRatePlan Empty = new SegmentRatePlan(Array.Empty<Segment>());

        public IReadOnlyList<Segment> Segments { get; }

        private readonly double[] mappedStarts;
        private readonly double[] mappedEnds;
        private readonly double[] cumulativeOffsets;
        private readonly double zeroOffset;

        public SegmentRatePlan(IEnumerable<Segment> segments)
        {
            var ordered = segments.OrderBy(s => s.Start).ToArray();
            double previousEnd = double.NegativeInfinity;
            foreach (var segment in ordered)
            {
                if (!double.IsFinite(segment.Start) || !double.IsFinite(segment.End) || !double.IsFinite(segment.Rate)
                    || segment.End <= segment.Start || segment.Rate <= 0 || segment.Start < previousEnd)
                    throw new ArgumentException("Rate segments must be finite, positive and non-overlapping.", nameof(segments));
                previousEnd = segment.End;
            }

            Segments = Array.AsReadOnly(ordered);
            mappedStarts = new double[ordered.Length];
            mappedEnds = new double[ordered.Length];
            cumulativeOffsets = new double[ordered.Length];
            double offset = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                var segment = ordered[i];
                mappedStarts[i] = segment.Start + offset;
                mappedEnds[i] = mappedStarts[i] + (segment.End - segment.Start) / segment.Rate;
                offset += (segment.End - segment.Start) * (1 / segment.Rate - 1);
                cumulativeOffsets[i] = offset;
            }

            zeroOffset = mapUnanchored(0);
        }

        public double RateAt(double time)
        {
            int low = 0;
            int high = Segments.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                var segment = Segments[middle];
                if (time < segment.Start)
                    high = middle - 1;
                else if (time >= segment.End)
                    low = middle + 1;
                else
                    return segment.Rate;
            }

            return 1;
        }

        /// <summary>Map source timestamps to the precomputed beatmap timeline, anchored at audio zero.</summary>
        public double Map(double sourceTime) => mapUnanchored(sourceTime) - zeroOffset;

        public double Unmap(double mappedTime)
        {
            double time = mappedTime + zeroOffset;
            int index = Array.BinarySearch(mappedStarts, time);
            if (index < 0)
                index = ~index - 1;
            if (index < 0)
                return time;
            return time < mappedEnds[index]
                ? Segments[index].Start + (time - mappedStarts[index]) * Segments[index].Rate
                : time - cumulativeOffsets[index];
        }

        private double mapUnanchored(double time)
        {
            int low = 0;
            int high = Segments.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (Segments[middle].Start <= time)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            if (high < 0)
                return time;
            return time < Segments[high].End
                ? mappedStarts[high] + (time - Segments[high].Start) / Segments[high].Rate
                : time + cumulativeOffsets[high];
        }

        /// <summary>Elapsed wall time between two source timestamps, useful for synchronization checks.</summary>
        public double Elapsed(double start, double end)
        {
            if (end < start)
                return -Elapsed(end, start);

            double elapsed = end - start;
            foreach (var segment in Segments)
            {
                double overlap = Math.Min(end, segment.End) - Math.Max(start, segment.Start);
                if (overlap > 0)
                    elapsed += overlap * (1 / segment.Rate - 1);
            }

            return elapsed;
        }
    }

    internal sealed class SegmentRateGeometry
    {
        internal readonly record struct Span(double Start, double End);

        public readonly Span[] Active;
        public readonly Span[] Protected;
        public readonly Span[] Neutral;

        public SegmentRateGeometry(IBeatmap beatmap, Func<HitObject, bool> neutral, Func<HitObject, bool> preserveDuration)
        {
            Neutral = merge(beatmap.Breaks.Select(b => new Span(b.StartTime, b.EndTime))
                                   .Concat(beatmap.HitObjects.Where(neutral).Select(h => new Span(h.StartTime, h.GetEndTime()))));
            Protected = merge(beatmap.HitObjects.Where(h => !neutral(h) && preserveDuration(h) && h.GetEndTime() > h.StartTime)
                                     .Select(h => new Span(h.StartTime, h.GetEndTime())));
            Active = merge(beatmap.HitObjects.Where(h => !neutral(h))
                                  .Select(h => new Span(h.StartTime, Math.Max(h.StartTime + 500, h.GetEndTime()))));
        }

        public bool IsActive(double start, double end) => overlaps(Active, start, end);
        public bool IsNeutral(double start, double end) => overlaps(Neutral, start, end);

        public bool ExtendThroughLongObjects(double start, ref double end)
        {
            int index = firstEndingAfter(Protected, start);
            for (; index < Protected.Length && Protected[index].Start < end; index++)
            {
                if (Protected[index].Start < start)
                    return false;
                end = Math.Max(end, Protected[index].End);
            }

            return true;
        }

        public IEnumerable<Span> ActiveWithoutBreaks()
        {
            foreach (var active in Active)
            {
                double start = active.Start;
                for (int i = firstEndingAfter(Neutral, start); i < Neutral.Length && Neutral[i].Start < active.End; i++)
                {
                    if (Neutral[i].Start > start)
                        yield return new Span(start, Neutral[i].Start);
                    start = Math.Max(start, Neutral[i].End);
                }

                if (start < active.End)
                    yield return new Span(start, active.End);
            }
        }

        private static bool overlaps(Span[] spans, double start, double end)
        {
            int index = firstEndingAfter(spans, start);
            return index < spans.Length && spans[index].Start < end;
        }

        private static int firstEndingAfter(Span[] spans, double time)
        {
            int low = 0;
            int high = spans.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (spans[middle].End <= time)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        private static Span[] merge(IEnumerable<Span> input)
        {
            var result = new List<Span>();
            foreach (var span in input.Where(s => double.IsFinite(s.Start) && double.IsFinite(s.End) && s.End > s.Start).OrderBy(s => s.Start))
            {
                // Touching holds/sliders may use different rates; only overlapping ones form a chain.
                if (result.Count > 0 && span.Start < result[^1].End)
                    result[^1] = new Span(result[^1].Start, Math.Max(span.End, result[^1].End));
                else
                    result.Add(span);
            }

            return result.ToArray();
        }
    }
}
