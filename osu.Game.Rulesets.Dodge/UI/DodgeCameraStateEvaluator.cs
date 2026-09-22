// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.UI
{
    /// <summary>
    /// Evaluates a sorted list of <see cref="DodgeCameraChange"/> keyframes and returns
    /// the accumulated field scroll offset at any point in time.
    /// Each change contributes its arrow vector: as a bounded displacement over its
    /// duration, or as an unbounded velocity while <see cref="DodgeCameraChange.Continuous"/>
    /// is set. Offsets chain: every change starts from the offset accumulated by
    /// all previous changes.
    /// </summary>
    public class DodgeCameraStateEvaluator
    {
        private readonly DodgeCameraChange[] changes;
        private readonly Vector2[] startOffsets;

        public DodgeCameraStateEvaluator(IEnumerable<DodgeCameraChange> changes)
        {
            this.changes = changes.OrderBy(c => c.StartTime).ToArray();
            startOffsets = new Vector2[this.changes.Length];

            for (int i = 0; i < this.changes.Length; i++)
                startOffsets[i] = evaluate(this.changes[i].StartTime, i);
        }

        public bool HasChanges => changes.Length > 0;

        public Vector2 Evaluate(double time) => changes.Length > 0 ? evaluate(time, changes.Length) : Vector2.Zero;

        private Vector2 evaluate(double time, int count)
        {
            // `changes[0..count)` is sorted ascending by StartTime. Find the latest
            // keyframe that is already active at `time` with a binary search. The
            // previous O(N) backwards scan is hit once per sample by
            // DodgeTrajectory.CalculateExitTimeWithCamera, so this is the hot path
            // that spikes when many ContinueUntilExit bullets spawn at once.
            int low = 0;
            int high = count - 1;
            int activeIndex = -1;

            while (low <= high)
            {
                int middle = (low + high) / 2;

                if (changes[middle].StartTime <= time)
                {
                    activeIndex = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            if (activeIndex < 0)
                return Vector2.Zero;

            DodgeCameraChange change = changes[activeIndex];
            Vector2 vector = change.EndPosition - change.Position;

            if (change.Continuous)
            {
                double elapsed = Math.Max(0, time - change.StartTime) / 1000;
                return startOffsets[activeIndex] + vector * (float)elapsed;
            }

            if (change.Duration <= 0)
                return startOffsets[activeIndex] + vector;

            float progress = (float)Math.Clamp((time - change.StartTime) / change.Duration, 0, 1);
            progress = change.Easing.Apply(progress);
            return startOffsets[activeIndex] + vector * progress;
        }
    }
}
