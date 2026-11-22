using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// Copies only miss evidence for the background calculator. Appended judgements are scanned once;
    /// successful hits and their object graphs never enter the queued snapshot.
    /// </summary>
    internal sealed class LivePerformanceMissSnapshot
    {
        private readonly List<HitEvent> misses = new();
        private int processed;
        private HitEvent last;

        public void Invalidate()
        {
            processed = 0;
            misses.Clear();
        }

        public List<HitEvent> Capture(IReadOnlyList<HitEvent> events)
        {
            // Rewind can replace a suffix and then advance past the old cursor before the next
            // throttled calculation. Validate that cursor as well as the collection length.
            if (processed > events.Count || (processed > 0 && !EqualityComparer<HitEvent>.Default.Equals(last, events[processed - 1])))
            {
                Invalidate();
            }

            for (; processed < events.Count; processed++)
            {
                HitEvent hit = events[processed];
                if (hit.Result == HitResult.Miss)
                    misses.Add(hit);
            }

            if (processed > 0)
                last = events[processed - 1];

            return new List<HitEvent>(misses);
        }
    }
}
