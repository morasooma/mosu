// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Aim Control (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): fast patterns that keep changing direction at
    /// small / medium spacing (aim control, flow aim control) get their own PP component — the Realistik core prices them like
    /// easy streams or small jumps (LATHI DT 581, FEEEEVER TIME DT 626 for SS while they are hard maps).
    /// </summary>
    /// <remarks>
    /// Control pressure per second: Σ over fast moves (≤ 110 ms in map time) of turn · spacing window · (velocity / 10 circles per s),
    /// turn = 1 − smoothstep(angle at the previous note, 70°, 130°) (straight streams count nothing, sharp turns fully),
    /// spacing window = 1 − smoothstep(spacing, 1.5, 2.5 circles) (big fast jumps are aim, already paid), stacks (&lt; 0.3) skipped.
    /// Control PP = SCALE · (hardest 20 s)^1.0 · (1 + map seconds ≥ 40% of it / 30)^0.5.
    /// Multiplier on the total = (1 + (control PP / SS PP)^3)^(1/3) — the component joins the other PP like a third skill, so it
    /// only matters where it is comparable to the rest; one multiplier per map + mods (an SS reference), same for every accuracy.
    /// Fitted 2026-10-02 to DT SS targets UMMU 1700, Rubik's Cube 1450, FEEEEVER TIME 2000, Nhelv 1500, LATHI 1600 and Mor 3 NM 2000;
    /// jump, stream and reading maps change by &lt; 3%.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxAimControl
    {
        public const double SCALE = 67.2;
        public const double FAST_MOVE_MS = 110;
        public const double TURN_FULL_ANGLE = 70;
        public const double TURN_NONE_ANGLE = 130;
        public const double SPACING_FULL = 1.5; // circle diameters
        public const double SPACING_NONE = 2.5;
        /// <summary>Circle diameter (osu!px) at CS 4: the spacing window is measured in these circles, independent of CS mods.</summary>
        public const double REFERENCE_DIAMETER = 2 * (54.4 - 4.48 * 4);
        public const double STACK_SPACING = 0.3;
        public const double VELOCITY_REFERENCE = 10; // circle diameters per played second
        public const int HARDEST_SECONDS = 20;
        public const int SMOOTH_SECONDS = 5;
        public const double ENDURANCE_SHARE = 0.4;
        public const double ENDURANCE_SECONDS = 30;
        public const double ENDURANCE_EXPONENT = 0.5;
        public const double NORM_EXPONENT = 3;

        public static double Multiplier(double controlPp, double ssPp)
            => controlPp <= 0 || ssPp <= 0 ? 1 : Math.Pow(1 + Math.Pow(controlPp / ssPp, NORM_EXPONENT), 1 / NORM_EXPONENT);

        public static double ControlPp(RxBeatmap beatmap, double clockRate)
        {
            List<RxHitObject> notes = beatmap.HitObjects.Where(o => o.Kind != RxHitObjectKind.Spinner).ToList();
            if (notes.Count < 3 || clockRate <= 0)
                return 0;

            double diameter = 2 * (54.4 - 4.48 * beatmap.CircleSize);
            double t0 = notes[0].StartTime;
            int bins = Math.Max(1, (int)Math.Ceiling((notes[^1].StartTime - t0) / 1000) + 1);
            double[] pressure = new double[bins];

            for (int i = 2; i < notes.Count; i++)
            {
                double gap = notes[i].StartTime - notes[i - 1].StartTime;
                if (gap <= 0 || gap > FAST_MOVE_MS)
                    continue;

                double spacing = distance(notes[i - 1].Position, notes[i].Position) / diameter;
                if (spacing < STACK_SPACING)
                    continue;

                double angle = RxStreamMapNerf.Angle(notes[i - 2].Position, notes[i - 1].Position, notes[i].Position);
                if (double.IsNaN(angle))
                    continue;

                double turn = 1 - smoothStep(angle, TURN_FULL_ANGLE, TURN_NONE_ANGLE);
                // 0.5.0: which moves are "control" is decided in circles of the reference CS 4, so bigger circles (DA / EZ) do not
                // turn wide jumps into control moves; the velocity below still uses the circles really played.
                double window = 1 - smoothStep(distance(notes[i - 1].Position, notes[i].Position) / REFERENCE_DIAMETER, SPACING_FULL, SPACING_NONE);
                double velocity = spacing * 1000 * clockRate / gap;

                int bin = Math.Clamp((int)((notes[i].StartTime - t0) / 1000), 0, bins - 1);
                pressure[bin] += turn * window * velocity / VELOCITY_REFERENCE;
            }

            double[] smooth = movingAverage(pressure.Select(v => v * clockRate).ToArray(), SMOOTH_SECONDS);
            double hardest = smooth.OrderByDescending(v => v).Take(HARDEST_SECONDS).Average();
            if (hardest <= 0)
                return 0;

            double hardSeconds = smooth.Count(v => v >= ENDURANCE_SHARE * hardest);
            return SCALE * hardest * Math.Pow(1 + hardSeconds / ENDURANCE_SECONDS, ENDURANCE_EXPONENT);
        }

        private static double distance(RxVec2 a, RxVec2 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }

        private static double[] movingAverage(double[] values, int width)
        {
            double[] result = new double[values.Length];
            int half = width / 2;

            for (int i = 0; i < values.Length; i++)
            {
                int lo = Math.Max(0, i - half), hi = Math.Min(values.Length - 1, i + half);
                double sum = 0;
                for (int k = lo; k <= hi; k++)
                    sum += values[k];
                result[i] = sum / (hi - lo + 1);
            }

            return result;
        }
    }
}
