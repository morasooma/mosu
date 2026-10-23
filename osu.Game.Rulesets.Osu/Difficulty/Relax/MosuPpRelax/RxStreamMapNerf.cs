// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Stream Map Nerf (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): every stream map loses up to 25% (1.0.0;
    /// 35% in 0.7.0–0.9.0, 50% before) of the PP its streams earn, whatever the shape of its streams. Replaces "Simple Stream Nerf (RX)" (v10–v24), which only hit
    /// straight / one-curve streams.
    /// </summary>
    /// <remarks>
    /// Stream map = speed PP / aim PP of the SS reference ≥ 1.0–1.3 (smooth); since 0.8.0 the nerf is proportional to the share of
    /// the SS PP earned in streams (<see cref="RxSectionTypes"/>), before it was smoothstep(stream note share, 30%, 60%).
    /// Stream notes: runs of ≥ 6 notes with equal spacing in time (±10%) and ≤ 125 ms between notes (raw map time, so NM and
    /// DT use the same share), counting only movements up to 2.5 circle radii (equal-timed jumps are not streams).
    /// Multiplier on the total: 1 − 0.25 · smoothstep(speed/aim, 1.0, 1.3) · stream PP share.
    /// Since 0.3.0 a note where the cursor turns (&lt; 120°) is not a stream note (see <see cref="TURN_ANGLE"/>).
    /// Jump maps with streams (drivers license [full]: 73% streams but speed/aim 0.96 / 0.74, Novae Ruptis 0.80 / 0.65) are
    /// not touched. Examples (SS, RX, at the 0.3.0 maximum −50%; −35% since 0.7.0): Expurget, Worms of Soul −50% NM and DT;
    /// A Tale Of Salt And Light −28% NM (speed/aim 1.16) and −50% DT; heat −1%.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxStreamMapNerf
    {
        public const double MAX_NERF = 0.25; // 1.0.0 (cache 38, owner's scenario): 0.35 since 0.7.0, 0.50 before
        public const double STREAM_MAP_RATIO_START = 1.0;
        public const double STREAM_MAP_RATIO_FULL = 1.3;
        public const double STREAM_MAX_DELTA_MS = 125;
        public const double STREAM_DELTA_TOLERANCE = 0.10;
        public const int STREAM_MIN_NOTES = 6;
        public const double MAX_SPACING_IN_RADII = 2.5;

        /// <summary>
        /// MosuPp 0.3.0: a note where the cursor turns (angle between the move in and the move out below this) is aim control,
        /// not streaming — fast patterns that keep changing direction (LATHI, Rubik's Cube, FEEEEVER TIME) are no longer nerfed
        /// as stream maps. Real streams go straight or along a curve (8% of their notes turn, aim control maps 57%).
        /// </summary>
        public const double TURN_ANGLE = 120;

        // speedAimRatio: speed PP / aim PP of the SS reference (see ManagedRealistikRelaxCalculator).
        // streamWeight: share of the SS PP earned in streams (RxSectionTypes, 0.8.0) — only the streams' part is nerfed.
        // Before 0.8.0: smoothstep(stream note share, 30%, 60%), which nerfed the hard jump sections of stream maps as well.
        public static double Multiplier(double speedAimRatio, double streamWeight)
            => 1 - MosuPpBalance.Current.StreamNerf
                 * smoothStep(speedAimRatio, STREAM_MAP_RATIO_START, STREAM_MAP_RATIO_FULL)
                 * Math.Clamp(streamWeight, 0, 1);

        /// <summary>Share of notes that belong to streams (0–1).</summary>
        public static double StreamShare(RxBeatmap beatmap) => share(StreamNotes(beatmap));

        private static double share(bool[] inStream)
        {
            int streamNotes = 0;
            foreach (bool value in inStream)
                if (value) streamNotes++;

            return inStream.Length == 0 ? 0 : (double)streamNotes / inStream.Length;
        }

        public static bool[] StreamNotes(RxBeatmap beatmap)
        {
            List<RxHitObject> objects = beatmap.HitObjects;
            int count = objects.Count;
            bool[] inStream = new bool[count];
            if (count < STREAM_MIN_NOTES)
                return inStream;

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
                        if (distance(objects[i], objects[i - 1]) > maxSpacing || IsTurn(objects, i))
                            continue;

                        inStream[i] = true;
                        inStream[i - 1] = true;
                    }
                }

                start = end + 1;
            }

            return inStream;
        }

        /// <summary>Whether the cursor turns at note <paramref name="i"/> (angle between the move into it and the move out of it).</summary>
        public static bool IsTurn(List<RxHitObject> objects, int i)
        {
            if (i <= 0 || i + 1 >= objects.Count)
                return false;

            double angle = Angle(objects[i - 1].Position, objects[i].Position, objects[i + 1].Position);
            return !double.IsNaN(angle) && angle < TURN_ANGLE;
        }

        /// <summary>Angle at <paramref name="middle"/> in degrees: 180 = straight on, 0 = full reversal; NaN for (almost) stacked notes.</summary>
        public static double Angle(RxVec2 previous, RxVec2 middle, RxVec2 next)
        {
            double ax = previous.X - middle.X, ay = previous.Y - middle.Y;
            double bx = next.X - middle.X, by = next.Y - middle.Y;
            double la = Math.Sqrt(ax * ax + ay * ay), lb = Math.Sqrt(bx * bx + by * by);
            if (la < 1 || lb < 1)
                return double.NaN;

            return Math.Acos(Math.Clamp((ax * bx + ay * by) / (la * lb), -1, 1)) * 180 / Math.PI;
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
