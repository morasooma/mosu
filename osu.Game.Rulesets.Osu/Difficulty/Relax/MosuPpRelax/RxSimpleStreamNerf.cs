// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Simple Stream Nerf (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): stream-dominated maps whose
    /// streams are simple (straight lines / one smooth curve, almost no change of direction) lose up to 30% of the total PP.
    /// Streams with changing angles and shapes are not touched.
    /// </summary>
    /// <remarks>
    /// Stream notes: runs of ≥ 6 notes with equal spacing in time (±10%) and ≤ 125 ms between notes (raw map time, so the
    /// result is the same for NM and DT), counting only movements up to 2.5 circle radii (equal-timed jumps are not
    /// streams). Stream share = stream notes / all notes.
    /// Stream shape: angle variety = standard deviation (degrees) of the angle at each stream note; direction changes =
    /// share of consecutive turns where the stream switches between bending left and right (movements under 5 px ignored).
    /// Only stream maps: gate smoothstep(speedPP / aimPP, 1.3, 1.65), so jump maps with streams (drivers license, Novae)
    /// are not touched (added in MosuPp version 9 after drivers license lost 21%).
    /// Multiplier on the total: 1 − 0.30 · gate · smoothstep(share, 40%, 60%) · (1 − smoothstep(variety, 16°, 26°))
    /// · (1 − smoothstep(direction changes, 12%, 18%)) — a map must be simple by both measures to be nerfed.
    /// Examples (SS, RX): Worms of Soul (63% streams, 13°, 10%) −30% NM and DT; A Tale Of Salt And Light (70%, 26°, 22%),
    /// drivers license (speed/aim 1.23 NM, 0.95 DT), heat, Novae Ruptis and jump maps ±0%.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxSimpleStreamNerf
    {
        public const double MAX_NERF = 0.30;
        public const double STREAM_MAX_DELTA_MS = 125;
        public const double STREAM_DELTA_TOLERANCE = 0.10;
        public const int STREAM_MIN_NOTES = 6;
        public const double SHARE_START = 0.40;
        public const double SHARE_FULL = 0.60;
        public const double VARIETY_SIMPLE_BELOW = 16;
        public const double VARIETY_NORMAL_FROM = 26;
        public const double MIN_MOVEMENT = 5;
        public const double MAX_SPACING_IN_RADII = 2.5;
        public const double STREAM_MAP_RATIO_START = 1.3;
        public const double STREAM_MAP_RATIO_FULL = 1.65;
        public const double DIRECTION_CHANGES_SIMPLE_BELOW = 0.12;
        public const double DIRECTION_CHANGES_NORMAL_FROM = 0.18;

        // speedAimRatio: speed PP / aim PP of the SS reference (see MosuPpRelaxCalculator).
        public static double Multiplier(RxBeatmap beatmap, double speedAimRatio)
        {
            var shape = StreamShape(beatmap);
            return 1 - MAX_NERF
                   * smoothStep(speedAimRatio, STREAM_MAP_RATIO_START, STREAM_MAP_RATIO_FULL)
                   * smoothStep(shape.Share, SHARE_START, SHARE_FULL)
                   * (1 - smoothStep(shape.AngleVarietyDegrees, VARIETY_SIMPLE_BELOW, VARIETY_NORMAL_FROM))
                   * (1 - smoothStep(shape.DirectionChangeShare, DIRECTION_CHANGES_SIMPLE_BELOW, DIRECTION_CHANGES_NORMAL_FROM));
        }

        public static (double Share, double AngleVarietyDegrees, double DirectionChangeShare) StreamShape(RxBeatmap beatmap)
        {
            List<RxHitObject> objects = beatmap.HitObjects;
            int count = objects.Count;
            if (count < STREAM_MIN_NOTES)
                return (0, 0, 0);

            bool[] inStream = new bool[count];
            double radius = 54.4 - 4.48 * beatmap.CircleSize;
            double maxSpacing = MAX_SPACING_IN_RADII * radius;
            int start = 1;

            while (start < count)
            {
                double delta = objects[start].StartTime - objects[start - 1].StartTime;
                if (delta <= 0 || delta > STREAM_MAX_DELTA_MS)
                {
                    start++;
                    continue;
                }

                int end = start;
                while (end + 1 < count
                       && Math.Abs((objects[end + 1].StartTime - objects[end].StartTime) / delta - 1) < STREAM_DELTA_TOLERANCE)
                    end++;

                // Notes start - 1 .. end form one run; only short movements count as streaming
                // (equal-timed full-screen jumps are not streams).
                if (end - start + 2 >= STREAM_MIN_NOTES)
                {
                    for (int i = start; i <= end; i++)
                    {
                        if (distance(objects[i], objects[i - 1]) > maxSpacing)
                            continue;

                        inStream[i] = true;
                        inStream[i - 1] = true;
                    }
                }

                start = end + 1;
            }

            int streamNotes = 0;
            foreach (bool value in inStream)
                if (value) streamNotes++;

            double sum = 0, sumSquares = 0;
            int angles = 0;
            int turnPairs = 0, directionChanges = 0;
            double previousTurn = 0;

            for (int i = 2; i < count; i++)
            {
                if (!inStream[i] || !inStream[i - 1] || !inStream[i - 2])
                {
                    previousTurn = 0;
                    continue;
                }

                double ax = objects[i - 1].Position.X - objects[i - 2].Position.X;
                double ay = objects[i - 1].Position.Y - objects[i - 2].Position.Y;
                double bx = objects[i].Position.X - objects[i - 1].Position.X;
                double by = objects[i].Position.Y - objects[i - 1].Position.Y;
                double a = Math.Sqrt(ax * ax + ay * ay);
                double b = Math.Sqrt(bx * bx + by * by);
                if (a <= MIN_MOVEMENT || b <= MIN_MOVEMENT)
                {
                    previousTurn = 0;
                    continue;
                }

                // Turning left or right; a flip means the stream changes its bending direction.
                double turn = ax * by - ay * bx;
                if (previousTurn != 0 && turn != 0)
                {
                    turnPairs++;
                    if (Math.Sign(turn) != Math.Sign(previousTurn))
                        directionChanges++;
                }

                previousTurn = turn;

                // Angle at the middle note: 180° = straight line.
                double angle = Math.Acos(Math.Clamp(-(ax * bx + ay * by) / (a * b), -1, 1)) * 180 / Math.PI;
                sum += angle;
                sumSquares += angle * angle;
                angles++;
            }

            double variety = 0;
            if (angles > 1)
            {
                double mean = sum / angles;
                variety = Math.Sqrt(Math.Max(0, sumSquares / angles - mean * mean));
            }

            return ((double)streamNotes / count, variety, turnPairs > 0 ? (double)directionChanges / turnPairs : 0);
        }

        private static double distance(RxHitObject a, RxHitObject b)
        {
            double dx = a.Position.X - b.Position.X;
            double dy = a.Position.Y - b.Position.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
