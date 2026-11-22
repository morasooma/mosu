// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Timing
{
    public static class TimingSectionAdjustments
    {
        /// <summary>
        /// Moves selected rows and the following effect rows belonging to selected timing points.
        /// Membership is captured before modifying any offsets so overlapping selections only move once.
        /// </summary>
        public static ControlPointGroup[] MoveControlPointGroups(IBeatmap beatmap, IReadOnlyList<ControlPointGroup> selectedGroups, double adjustment, bool adjustHitObjects)
        {
            var info = beatmap.ControlPointInfo;
            var timingPoints = selectedGroups.SelectMany(g => g.ControlPoints).OfType<TimingControlPoint>().ToArray();
            var ranges = timingPoints.Select(t => (Start: t.Time, End: info.TimingPointAfter(t.Time)?.Time ?? double.PositiveInfinity)).ToArray();
            var moving = info.Groups.Where(g => selectedGroups.Contains(g) || ranges.Any(r => g.Time >= r.Start && g.Time < r.End))
                             .Select(g => (Group: g, Time: g.Time + adjustment, Points: g.ControlPoints.ToArray())).ToArray();

            if (!double.IsFinite(adjustment) || moving.Any(g => !double.IsFinite(g.Time)))
                throw new ArgumentOutOfRangeException(nameof(adjustment));

            var hitObjects = adjustHitObjects
                ? timingPoints.SelectMany(t => HitObjectsInTimingRange(beatmap, t)).Distinct().ToArray()
                : Array.Empty<HitObject>();

            foreach (var hitObject in hitObjects)
                hitObject.StartTime += adjustment;

            // Remove all sources first, otherwise moving one selected row onto another would overwrite its points.
            foreach (var entry in moving)
                info.RemoveGroup(entry.Group);

            foreach (var entry in moving)
            {
                var destination = info.GroupAt(entry.Time, true);
                foreach (var point in entry.Points)
                    destination.Add(point);
            }

            return selectedGroups.Select(g => info.GroupAt(g.Time + adjustment)).ToArray();
        }

        /// <summary>
        /// Returns all objects from <paramref name="beatmap"/> which are affected by the supplied <paramref name="timingControlPoint"/>.
        /// </summary>
        public static List<HitObject> HitObjectsInTimingRange(IBeatmap beatmap, TimingControlPoint timingControlPoint)
        {
            // If the first group, we grab all hitobjects prior to the next, if the last group, we grab all remaining hitobjects
            double startTime = beatmap.ControlPointInfo.TimingPoints.Any(x => x.Time < timingControlPoint.Time) ? timingControlPoint.Time : double.MinValue;
            double endTime = beatmap.ControlPointInfo.TimingPoints.FirstOrDefault(x => x.Time > timingControlPoint.Time)?.Time ?? double.MaxValue;

            return beatmap.HitObjects.Where(x => Precision.AlmostBigger(x.StartTime, startTime) && Precision.DefinitelyBigger(endTime, x.StartTime)).ToList();
        }

        /// <summary>
        /// Moves all relevant objects after <paramref name="timingControlPoint"/>'s offset has been changed by <paramref name="adjustment"/>.
        /// </summary>
        public static void AdjustHitObjectOffset(IBeatmap beatmap, TimingControlPoint timingControlPoint, double adjustment)
        {
            foreach (HitObject hitObject in HitObjectsInTimingRange(beatmap, timingControlPoint))
            {
                hitObject.StartTime += adjustment;
            }
        }

        /// <summary>
        /// Ensures all relevant objects are still snapped to the same beats after <paramref name="timingControlPoint"/>'s beat length / BPM has been changed.
        /// </summary>
        public static void SetHitObjectBPM(IBeatmap beatmap, TimingControlPoint timingControlPoint, double oldBeatLength)
        {
            foreach (HitObject hitObject in HitObjectsInTimingRange(beatmap, timingControlPoint))
            {
                double beat = (hitObject.StartTime - timingControlPoint.Time) / oldBeatLength;

                hitObject.StartTime = (beat * timingControlPoint.BeatLength) + timingControlPoint.Time;

                if (hitObject is not IHasRepeats && hitObject is IHasDuration hitObjectWithDuration)
                    hitObjectWithDuration.Duration *= timingControlPoint.BeatLength / oldBeatLength;
            }
        }
    }
}
