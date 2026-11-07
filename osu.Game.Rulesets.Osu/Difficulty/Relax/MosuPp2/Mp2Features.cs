// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPp2
{
    /// <summary>
    /// What a map + mods asks of the cursor in Relax, before any parameter: one entry per note (circle or slider head), in
    /// real time (rate applied) and in circle diameters (CS applied). Computed once; <see cref="Mp2Model"/> applies the
    /// parameters (cheap, so the fitter can evaluate thousands of parameter sets).
    /// </summary>
    public sealed class Mp2Features
    {
        /// <summary>Follow circle radius in circle radii (osu! slider ball follow area).</summary>
        public const double FOLLOW_AREA = 2.4;

        /// <summary>The slider tail is judged this much before the slider end (lazer SliderEventGenerator.TAIL_LENIENCY).</summary>
        public const double TAIL_LENIENCY_MS = 36;

        /// <summary>Slider path sampling step, map milliseconds (lazer checks tracking every frame).</summary>
        public const double SLIDER_STEP_MS = 10;

        /// <summary>Playfield area the cursor can reach, osu! pixels: notes outside are clamped to it (off-screen notes would make
        /// arbitrarily long movements).</summary>
        public const float MIN_X = -171, MAX_X = 683, MIN_Y = -48, MAX_Y = 432;

        /// <summary>The cursor is free at least this share of the head-to-head gap before a note, even inside a slider that is
        /// still running (a note during a slider is a slider break risk, not a teleport).</summary>
        public const double MIN_FREE_SHARE = 0.25;

        /// <summary>Circle diameter in osu! pixels after mods (CS, HR / EZ / DA).</summary>
        public double Diameter { get; init; }

        /// <summary>Clock rate of the mods (DT 1.5, HT 0.75; the mean rate for Wind Up / Down): map time ≈ real time × rate.</summary>
        public double Rate { get; init; } = 1;

        /// <summary>Note time, map seconds (as in the editor; rate-independent).</summary>
        public double[] MapTime { get; init; } = Array.Empty<double>();

        /// <summary>Beat length at each note, map milliseconds (for rhythm snapping).</summary>
        public double[] BeatLength { get; init; } = Array.Empty<double>();

        /// <summary>The note is a slider head.</summary>
        public bool[] IsSlider { get; init; } = Array.Empty<bool>();

        /// <summary>Lazy cursor travel of the slider starting at the note, diameters (0 for circles).</summary>
        public double[] SliderTravel { get; init; } = Array.Empty<double>();

        /// <summary>The note starts a new sequence: first note, or the first note after a spinner.</summary>
        public bool[] AfterBreak { get; init; } = Array.Empty<bool>();

        public bool Hidden { get; init; }
        public bool Flashlight { get; init; }

        /// <summary>Note centre, diameters.</summary>
        public Vector2d[] Position { get; init; } = Array.Empty<Vector2d>();

        /// <summary>Note time, real seconds.</summary>
        public double[] Time { get; init; } = Array.Empty<double>();

        /// <summary>When the cursor is free to move to this note (previous note time, slider end or spinner end), real seconds.</summary>
        public double[] FreeTime { get; init; } = Array.Empty<double>();

        /// <summary>Where the cursor is when it becomes free, if fixed by the previous object (a slider's lazy end or a spinner),
        /// diameters; null when the previous object is a circle (then the lazy cursor position at that circle is used).</summary>
        public Vector2d?[] FixedStart { get; init; } = Array.Empty<Vector2d?>();

        /// <summary>Extra time a late arrival is allowed for a 300 (half the 300 window), real seconds.</summary>
        public double[] Window { get; init; } = Array.Empty<double>();

        /// <summary>Relax hits a note once the cursor is over it from this long before its time, real seconds (RELAX_LENIENCY).</summary>
        public double Leniency { get; init; }

        /// <summary>Slider tracking: path the cursor must travel to stay in the follow circle, diameters per real second (0 for
        /// circles and short sliders).</summary>
        public double[] Track { get; init; } = Array.Empty<double>();

        /// <summary>Spaced upcoming notes already on screen when the note is hit (moves below half a diameter do not count).</summary>
        public double[] Visible { get; init; } = Array.Empty<double>();

        /// <summary>Approach time of the note, real milliseconds (AR after mods and rate).</summary>
        public double[] Preempt { get; init; } = Array.Empty<double>();

        public int Count => Time.Length;

        private Mp2NoteTypes? types;
        private double[]? localGap;

        /// <summary>Classifier result (parameter-free, computed once).</summary>
        public Mp2NoteTypes Types => types ??= Mp2Classifier.Classify(this);

        /// <summary>Median head-to-head gap (above 5 ms) among the 4 notes on each side, real seconds: the local rhythm.</summary>
        public double[] LocalGap => localGap ??= computeLocalGap();

        private double[] computeLocalGap()
        {
            int n = Count;
            var result = new double[n];
            var gaps = new List<double>(9);

            for (int i = 0; i < n; i++)
            {
                gaps.Clear();
                for (int j = Math.Max(1, i - 4); j <= Math.Min(n - 1, i + 4); j++)
                {
                    if (Time[j] - Time[j - 1] > 0.005)
                        gaps.Add(Time[j] - Time[j - 1]);
                }

                gaps.Sort();
                result[i] = gaps.Count > 0 ? gaps[gaps.Count / 2] : 0;
            }

            return result;
        }

        public static Mp2Features Extract(IBeatmap playable, IReadOnlyList<Mod> mods)
        {
            // Approach time and hit windows of the objects are in map time; "Lock AR/OD" already stretched them on the
            // objects (DT / HT ApplyToHitObject), so dividing by the rate always gives the real values.
            var clock = new RealClock(playable, mods);
            var objects = playable.HitObjects.OfType<OsuHitObject>().ToList();
            var notes = objects.Where(o => o is not Spinner).ToList();

            double diameter = 2 * Math.Max(4, OsuHitObject.OBJECT_RADIUS * LegacyRulesetExtensions.CalculateScaleFromCircleSize(playable.Difficulty.CircleSize, true));
            int n = notes.Count;

            var position = new Vector2d[n];
            var time = new double[n];
            var mapTime = new double[n];
            var freeTime = new double[n];
            var fixedStart = new Vector2d?[n];
            var window = new double[n];
            var track = new double[n];
            var visible = new double[n];
            var preempt = new double[n];
            var beatLength = new double[n];
            var isSlider = new bool[n];
            var sliderTravel = new double[n];
            var afterBreak = new bool[n];

            Vector2d? nextStart = null;
            double nextFree = double.NaN;
            bool spinnerBefore = false;
            int noteIndex = 0;

            foreach (var hitObject in objects)
            {
                if (hitObject is Spinner spinner)
                {
                    nextStart = new Vector2d(256, 192) / diameter;
                    spinnerBefore = true;
                    nextFree = clock.Seconds(spinner.EndTime);
                    continue;
                }

                int i = noteIndex++;
                position[i] = clamp(hitObject.StackedPosition) / diameter;
                time[i] = clock.Seconds(hitObject.StartTime);
                mapTime[i] = hitObject.StartTime / 1000;
                afterBreak[i] = double.IsNaN(nextFree) || spinnerBefore;
                spinnerBefore = false;
                freeTime[i] = double.IsNaN(nextFree) ? time[i] - 1 : Math.Min(nextFree, time[i]);
                if (i > 0)
                    freeTime[i] = Math.Min(freeTime[i], time[i] - MIN_FREE_SHARE * (time[i] - time[i - 1]));
                fixedStart[i] = double.IsNaN(nextFree) ? position[i] : nextStart;
                window[i] = (hitObject.HitWindows?.WindowFor(HitResult.Great) ?? 0) / clock.RateAt(hitObject.StartTime) / 1000;
                preempt[i] = hitObject.TimePreempt / clock.RateAt(hitObject.StartTime);
                beatLength[i] = playable.ControlPointInfo.TimingPointAt(hitObject.StartTime).BeatLength;

                if (hitObject is Slider slider)
                {
                    (Vector2d end, double travelled) = lazyPath(slider, diameter);
                    double endTime = Math.Max(slider.StartTime, slider.EndTime - TAIL_LENIENCY_MS);
                    double duration = Math.Max(0.01, clock.Seconds(endTime) - clock.Seconds(slider.StartTime));
                    track[i] = travelled / duration;
                    isSlider[i] = true;
                    sliderTravel[i] = travelled;
                    nextStart = end;
                    nextFree = clock.Seconds(endTime);
                }
                else
                {
                    nextStart = null;
                    nextFree = time[i];
                }
            }

            for (int i = 0; i < n; i++)
            {
                double now = notes[i].StartTime;
                for (int j = i + 1; j < n && notes[j].StartTime - notes[j].TimePreempt <= now; j++)
                    visible[i] += Math.Clamp((position[j] - position[j - 1]).Length - 0.5, 0, 1);
            }

            return new Mp2Features
            {
                Diameter = diameter,
                Hidden = mods.Any(m => m is OsuModHidden || m.Acronym == "TC"),
                Flashlight = mods.Any(m => m is OsuModFlashlight),
                Position = position, Time = time, FreeTime = freeTime, FixedStart = fixedStart, Window = window,
                Leniency = OsuModRelax.RELAX_LENIENCY / clock.MeanRate / 1000,
                Track = track, Visible = visible, Preempt = preempt,
                Rate = clock.MeanRate, MapTime = mapTime, BeatLength = beatLength, IsSlider = isSlider, SliderTravel = sliderTravel, AfterBreak = afterBreak,
            };
        }

        /// <summary>
        /// Map time → real time under the rate mods. Constant rates (DT / HT / rate edits) divide; Wind Up / Down are integrated
        /// over the map (ModUtils.CalculateRateWithMods would score them at their initial rate).
        /// </summary>
        private sealed class RealClock
        {
            private const double step_ms = 50;

            private readonly IApplicableToRate[] rateMods;
            private readonly double constant = 1, start;
            private readonly double[]? table;

            public double MeanRate { get; } = 1;

            public RealClock(IBeatmap playable, IReadOnlyList<Mod> mods)
            {
                // Ramps learn the map's span in ApplyToBeatmap; use initialised copies so the caller's mods are not touched.
                rateMods = mods.OfType<IApplicableToRate>().Select(m => m is ModTimeRamp ? (IApplicableToRate)((Mod)m).DeepClone() : m).ToArray();
                foreach (var ramp in rateMods.OfType<ModTimeRamp>())
                    ramp.ApplyToBeatmap(playable);

                if (!rateMods.Any(m => m is ModTimeRamp))
                {
                    constant = MeanRate = rateMods.Aggregate(1.0, (rate, mod) => mod.ApplyToRate(0, rate));
                    return;
                }

                double first = playable.HitObjects.FirstOrDefault()?.StartTime ?? 0;
                double end = playable.HitObjects.Any() ? playable.GetLastObjectTime() : 0;
                start = Math.Min(0, first);
                int count = (int)Math.Ceiling((end - start) / step_ms) + 2;
                table = new double[count];
                table[0] = start / RateAt(start) / 1000;
                for (int k = 1; k < count; k++)
                    table[k] = table[k - 1] + step_ms / RateAt(start + (k - 0.5) * step_ms) / 1000;

                MeanRate = end > first ? (end - first) / 1000 / Math.Max(1e-6, Seconds(end) - Seconds(first)) : RateAt(first);
            }

            public double RateAt(double mapMs) => table == null && rateMods.Length == 0 ? 1
                : table == null ? constant : Math.Max(0.05, rateMods.Aggregate(1.0, (rate, mod) => mod.ApplyToRate(mapMs, rate)));

            /// <summary>Real seconds of a map time, ms.</summary>
            public double Seconds(double mapMs)
            {
                if (table == null)
                    return mapMs / constant / 1000;

                double position = (mapMs - start) / step_ms;
                if (position <= 0)
                    return table[0] + (mapMs - start) / RateAt(start) / 1000;

                int k = Math.Min(table.Length - 2, (int)position);
                return table[k] + (mapMs - (start + k * step_ms)) / RateAt(start + (k + 0.5) * step_ms) / 1000;
            }
        }

        /// <summary>
        /// The laziest cursor that keeps the slider ball inside the follow circle every <see cref="SLIDER_STEP_MS"/> until the
        /// lenient tail: where it ends (diameters) and how far it travelled (diameters). Short or tiny sliders cost nothing.
        /// </summary>
        private static (Vector2d End, double Travelled) lazyPath(Slider slider, double diameter)
        {
            double follow = FOLLOW_AREA / 2;
            double end = Math.Max(slider.StartTime, slider.EndTime - TAIL_LENIENCY_MS);
            double duration = slider.EndTime - slider.StartTime;
            Vector2d cursor = clamp(slider.StackedPosition) / diameter;
            double travelled = 0;

            for (double t = slider.StartTime + SLIDER_STEP_MS; duration > 0; t += SLIDER_STEP_MS)
            {
                double at = Math.Min(t, end);
                Vector2d ball = clamp(slider.StackedPositionAt((at - slider.StartTime) / duration)) / diameter;
                Vector2d offset = ball - cursor;
                double length = offset.Length;

                if (length > follow)
                {
                    double move = length - follow;
                    cursor += offset * (move / length);
                    travelled += move;
                }

                if (at >= end)
                    break;
            }

            return (cursor, travelled);
        }

        private static Vector2d toVector(Vector2 v) => new Vector2d(v.X, v.Y);

        private static Vector2d clamp(Vector2 v) => new Vector2d(Math.Clamp(v.X, MIN_X, MAX_X), Math.Clamp(v.Y, MIN_Y, MAX_Y));
    }
}
