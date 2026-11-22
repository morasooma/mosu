using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.Scoring
{
    /// <summary>
    /// Stable evaluates slider movement using integer timestamps at path segment endpoints,
    /// not a continuous fraction of the entire slider duration. This matters near repeats.
    /// </summary>
    internal sealed class StableSliderPosition
    {
        private readonly List<Segment> segments = new List<Segment>();

        public StableSliderPosition(IReadOnlyList<Vector2> path, double startTime, double velocity, int spans)
        {
            if (velocity <= 0)
                throw new ArgumentOutOfRangeException(nameof(velocity));

            double time = startTime;
            for (int span = 0; span < spans; span++)
            {
                bool reverse = span % 2 != 0;
                for (int i = 1; i < path.Count; i++)
                {
                    Vector2 from = path[reverse ? path.Count - i : i - 1];
                    Vector2 to = path[reverse ? path.Count - i - 1 : i];
                    double nextTime = time + Vector2.Distance(from, to) / velocity;
                    segments.Add(new Segment((int)time, (int)nextTime, from, to));
                    time = nextTime;
                }
            }
        }

        public Vector2 PositionAt(double time)
        {
            if (segments.Count == 0)
                return Vector2.Zero;

            // Lower bound on segment end preserves stable's first-segment-wins treatment
            // of shared timestamps, including zero-duration segments.
            int low = 0;
            int high = segments.Count - 1;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (segments[mid].End < time)
                    low = mid + 1;
                else
                    high = mid;
            }

            var segment = segments[low];
            if (segment.End == segment.Start)
                return segment.To;

            float progress = (float)Math.Clamp((time - segment.Start) / (segment.End - segment.Start), 0, 1);
            return segment.From + (segment.To - segment.From) * progress;
        }

        private readonly record struct Segment(int Start, int End, Vector2 From, Vector2 To);
    }
}
