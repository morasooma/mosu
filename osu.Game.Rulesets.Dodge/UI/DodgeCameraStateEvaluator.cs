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

        public Vector2 Evaluate(double time) => evaluate(time, changes.Length);

        private Vector2 evaluate(double time, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                DodgeCameraChange change = changes[i];

                if (change.StartTime > time)
                    continue;

                Vector2 vector = change.EndPosition - change.Position;

                if (change.Continuous)
                {
                    double elapsed = Math.Max(0, time - change.StartTime) / 1000;
                    return startOffsets[i] + vector * (float)elapsed;
                }

                if (change.Duration <= 0)
                    return startOffsets[i] + vector;

                float progress = (float)Math.Clamp((time - change.StartTime) / change.Duration, 0, 1);
                return startOffsets[i] + vector * progress;
            }

            return Vector2.Zero;
        }
    }
}
