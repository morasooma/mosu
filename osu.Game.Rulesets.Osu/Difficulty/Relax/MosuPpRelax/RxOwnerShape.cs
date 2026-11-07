// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MosuPpRxCs;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// What the owner's rules (<see cref="RxOwnerRules"/>) read from the map itself, independent of the rate: editor distance spacing
    /// of streams and jumps, BPM, slider share, wide back-and-forth ("1-2", gvb) jumps and repeated 8-note patterns.
    /// </summary>
    /// <remarks>
    /// Distance spacing is the editor's at beat divisor 1/4: distance from the previous object's end to the next object / the slider
    /// distance of a quarter beat (slider multiplier · 100 · SV / 4), whatever the time between them. Streams are 0.5–1.6×,
    /// wide farm jumps 6–11× (Walk This Way 6.4, Tom Sawyer 7.5, Philophobia 8.1).
    /// Streams: runs of 6+ circles at 1/3 of a beat or faster with equal spacing in time (±10%).
    /// Patterns: blocks of 8 objects (every 8th object starts one) compared with every other window of 8 objects — same rhythm (±10%)
    /// and, around their centres, every note within <see cref="PATTERN_TOLERANCE"/> px, mirrored copies included (a slight rotation
    /// stays within the tolerance). Windows of nearly stacked notes are not patterns.
    /// </remarks>
    internal static class RxOwnerShape
    {
        public const int PATTERN_NOTES = 8;
        public const double PATTERN_TOLERANCE = 20;
        public const double PATTERN_MIN_MOVE = 50; // px, mean move of a window
        public const int STREAM_MIN_NOTES = 6;

        public const double WIDE_SPACING_START = 7; // editor distance spacing; "wide" from 8
        public const double WIDE_SPACING_FULL = 9;
        public const double BACK_AND_FORTH_ANGLE = 40; // degrees at the note; gvb crossings are 45°, triangles 60°
        public const double BACK_AND_FORTH_ANGLE_END = 60;

        /// <param name="Matches">Starts of the other windows with the same pattern, ascending.</param>
        public readonly record struct Block(int Start, int[] Matches);

        /// <param name="StreamSpaced">Stream moves weighted by smoothstep(spacing at the map's own CS, 0.25, 0.5 circle diameters), averaged:
        /// 1 for normal streams, near 0 for streams of overlapping notes (−45 Crimsonic dimension: 0.41, its notes 0.30 diameters apart).</param>
        /// <param name="TightStreamShare">Share of the objects (without spinners) reached by a stream move closer than one circle diameter
        /// at the map's own CS, whatever the rate and CS (jumps inside a stream rhythm do not count; drivers license 0.70, Crimsonic 1.00).</param>
        public sealed record Shape(double LengthMs, double SliderShare, double StreamSpacing, double StreamBpm, double MainBpm,
                                   double WideBackAndForthShare, double[] NoteTimes, Block[] Blocks, double StreamSpaced = 1, double TightStreamShare = 0);

        public static Shape Analyze(IBeatmap beatmap)
        {
            List<OsuHitObject> objects = beatmap.HitObjects.OfType<OsuHitObject>().Where(h => h is not Spinner).ToList();
            if (objects.Count < 2)
                return new Shape(0, 0, 0, 0, 0, 0, objects.Select(h => h.StartTime).ToArray(), Array.Empty<Block>());

            double ownDiameter = RxCsGain.Diameter(beatmap.BeatmapInfo.Difficulty.CircleSize);

            int sliders = objects.Count(h => h is Slider);
            double length = objects.Max(h => h.GetEndTime()) - objects[0].StartTime;

            // Slider velocity points (as RealistikBeatmapConverter reads them).
            var velocities = beatmap.ControlPointInfo.Groups.SelectMany(g => g.ControlPoints.OfType<DifficultyControlPoint>().Select(p => (g.Time, p.SliderVelocity)))
                                    .OrderBy(p => p.Time).ToList();

            double velocityAt(double time)
            {
                double velocity = 1;
                foreach (var point in velocities)
                {
                    if (point.Time > time)
                        break;

                    velocity = point.SliderVelocity;
                }

                return velocity;
            }

            // Distance spacing of the move into object i (NaN when it cannot be measured).
            double[] spacing = new double[objects.Count];
            spacing[0] = double.NaN;

            for (int i = 1; i < objects.Count; i++)
            {
                OsuHitObject previous = objects[i - 1], current = objects[i];
                double quarterBeat = beatmap.Difficulty.SliderMultiplier * 100 * velocityAt(current.StartTime) / 4;
                spacing[i] = current.StartTime > previous.GetEndTime() && quarterBeat > 0
                    ? Vector2.Distance(previous.EndPosition, current.Position) / quarterBeat
                    : double.NaN;
            }

            (double streamSpacing, double streamBpm, double streamSpaced, int tightMoves) = streams(beatmap, objects, spacing, ownDiameter);

            double wide = 0;
            for (int i = 1; i < objects.Count - 1; i++)
            {
                if (double.IsNaN(spacing[i]))
                    continue;

                Vector2 back = objects[i - 1].EndPosition - objects[i].Position, next = objects[i + 1].Position - objects[i].EndPosition;
                if (back.Length < 1 || next.Length < 1)
                    continue;

                double angle = MathHelper.RadiansToDegrees(Math.Acos(Math.Clamp(Vector2.Dot(back, next) / (back.Length * next.Length), -1, 1)));
                wide += RxMath.SmoothStep(spacing[i], WIDE_SPACING_START, WIDE_SPACING_FULL)
                        * (1 - RxMath.SmoothStep(angle, BACK_AND_FORTH_ANGLE, BACK_AND_FORTH_ANGLE_END));
            }

            return new Shape(length, (double)sliders / objects.Count, streamSpacing, streamBpm, mainBpm(beatmap, objects), wide / objects.Count,
                objects.Select(h => h.StartTime).ToArray(), patterns(objects), streamSpaced, (double)tightMoves / objects.Count);
        }

        private static (double Spacing, double Bpm, double Spaced, int Tight) streams(IBeatmap beatmap, List<OsuHitObject> objects, double[] spacing, double ownDiameter)
        {
            double spacingSum = 0, bpmSum = 0, spacedSum = 0;
            int moves = 0, tight = 0;

            // Moves first .. last (move i goes into object i) form one run of equally timed stream moves.
            for (int first = 1; first < objects.Count;)
            {
                if (!isStreamMove(beatmap, objects, first))
                {
                    first++;
                    continue;
                }

                int last = first;
                while (last + 1 < objects.Count && isStreamMove(beatmap, objects, last + 1)
                                                && Math.Abs(gap(objects, last + 1) - gap(objects, last)) <= 0.1 * gap(objects, last))
                    last++;

                if (last - first + 2 >= STREAM_MIN_NOTES)
                {
                    for (int k = first; k <= last; k++)
                    {
                        if (double.IsNaN(spacing[k]))
                            continue;

                        double diameters = Vector2.Distance(objects[k - 1].Position, objects[k].Position) / ownDiameter;
                        spacingSum += spacing[k];
                        spacedSum += RxMath.SmoothStep(diameters, 0.25, 0.5);
                        if (diameters < 1)
                            tight++;
                        bpmSum += 60000 / beatmap.ControlPointInfo.TimingPointAt(objects[k].StartTime).BeatLength;
                        moves++;
                    }
                }

                first = last + 1;
            }

            return moves > 0 ? (spacingSum / moves, bpmSum / moves, spacedSum / moves, tight) : (0, 0, 1, 0);
        }

        private static bool isStreamMove(IBeatmap beatmap, List<OsuHitObject> objects, int i)
            => objects[i] is HitCircle && objects[i - 1] is HitCircle
                                       && gap(objects, i) <= beatmap.ControlPointInfo.TimingPointAt(objects[i].StartTime).BeatLength / 3 * 1.05;

        private static double gap(List<OsuHitObject> objects, int i) => objects[i].StartTime - objects[i - 1].StartTime;

        /// <summary>The BPM that lasts longest between the first and the last object (as osu! shows it).</summary>
        private static double mainBpm(IBeatmap beatmap, List<OsuHitObject> objects)
        {
            var points = beatmap.ControlPointInfo.TimingPoints;
            if (points.Count == 0)
                return 0;

            double start = objects[0].StartTime, end = objects[^1].GetEndTime();
            var durations = new Dictionary<double, double>();

            for (int i = 0; i < points.Count; i++)
            {
                double from = i == 0 ? start : Math.Max(start, points[i].Time);
                double to = i + 1 < points.Count ? Math.Min(end, points[i + 1].Time) : end;
                if (to <= from)
                    continue;

                double bpm = Math.Round(60000 / points[i].BeatLength, 1);
                durations[bpm] = durations.GetValueOrDefault(bpm) + to - from;
            }

            return durations.Count > 0 ? durations.MaxBy(p => p.Value).Key : 60000 / points[0].BeatLength;
        }

        private static Block[] patterns(List<OsuHitObject> objects)
        {
            int windows = objects.Count - PATTERN_NOTES + 1;
            if (windows < 2)
                return Array.Empty<Block>();

            var centred = new Vector2[windows][];
            var gaps = new double[windows][];
            bool[] moving = new bool[windows];

            for (int j = 0; j < windows; j++)
            {
                Vector2 centre = Vector2.Zero;
                for (int k = 0; k < PATTERN_NOTES; k++)
                    centre += objects[j + k].Position;
                centre /= PATTERN_NOTES;

                centred[j] = new Vector2[PATTERN_NOTES];
                gaps[j] = new double[PATTERN_NOTES - 1];
                double moves = 0;

                for (int k = 0; k < PATTERN_NOTES; k++)
                {
                    centred[j][k] = objects[j + k].Position - centre;
                    if (k > 0)
                    {
                        gaps[j][k - 1] = objects[j + k].StartTime - objects[j + k - 1].StartTime;
                        moves += Vector2.Distance(objects[j + k].Position, objects[j + k - 1].Position);
                    }
                }

                moving[j] = moves / (PATTERN_NOTES - 1) >= PATTERN_MIN_MOVE;
            }

            var blocks = new List<Block>();

            for (int b = 0; b < windows; b += PATTERN_NOTES)
            {
                if (!moving[b])
                    continue;

                var matches = new List<int>();
                for (int j = 0; j < windows; j++)
                {
                    if (Math.Abs(j - b) >= PATTERN_NOTES && moving[j] && sameRhythm(gaps[b], gaps[j]) && samePattern(centred[b], centred[j]))
                        matches.Add(j);
                }

                blocks.Add(new Block(b, matches.ToArray()));
            }

            return blocks.ToArray();
        }

        private static bool sameRhythm(double[] a, double[] b)
        {
            for (int k = 0; k < a.Length; k++)
            {
                if (Math.Abs(a[k] - b[k]) > 0.1 * Math.Max(a[k], b[k]) + 1)
                    return false;
            }

            return true;
        }

        private static bool samePattern(Vector2[] a, Vector2[] b)
        {
            foreach (var mirror in mirrors)
            {
                bool same = true;
                for (int k = 0; k < a.Length && same; k++)
                    same = Vector2.Distance(a[k], b[k] * mirror) <= PATTERN_TOLERANCE;

                if (same)
                    return true;
            }

            return false;
        }

        private static readonly Vector2[] mirrors = { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) };
    }
}
