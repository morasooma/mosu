// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "RP Balance (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): RP (respect points) is a difficulty value of a
    /// map + mods built only from what the player physically faces — how fast the cursor has to move in the hardest 20 s, how long
    /// the map stays hard, reading, hit window — independent of the PP formulas. Total PP moves to RP, within ±20%:
    /// total × clamp(RP / SS PP, 0.8, 1.2), where SS PP is the MosuPp PP of an SS on the same map + mods (all other rules applied).
    /// </summary>
    /// <remarks>
    /// Port of the prototype in tools/MosuPpLab (MapAnalyzer.cs + Rp.cs), fitted on 25 labelled maps on 2026-10-02 to the PP agreed
    /// for them (ganbare 2800, Six Seven 2400, Attack ≈ Hills Have Eyes ≈ 2100, Flamewall [ETERNAL] 1400, Vanish 2700, Mor 3 2000,
    /// glass beach as it is). The polygon prototype and this rule must stay identical; tune in the polygon first.
    /// <list type="bullet">
    /// <item>Map type by content: streams (≥ ~190 BPM), aim slop (jumps), tech (≥ 55% sliders), aim control (stream-like density
    /// below ~190 BPM), reading (effective AR ≤ ~8). The type picks the RP scale and, for aim control, the intensity measure.</item>
    /// <item>Skill pressure: every move (velocity / 50 circles per s)^1.5 — after a slider from its end; streams count only above the
    /// curve "BPM × spacing" (1.5 circles at 230 BPM, 0.5 at 410 BPM); every slider span (span velocity / 50)^1.5.
    /// Aim control uses the Fitts throughput instead (log2(spacing + 1) per move + 0.5 per note, per second).</item>
    /// <item>RP = scale(type) · intensity^0.75 (aim control: Fitts^1.25) · (1 + hard seconds / 30)^0.15 · reading · precision.</item>
    /// </list>
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxRespectPoints
    {
        public const double MULTIPLIER_MIN = 0.8;
        public const double MULTIPLIER_MAX = 1.2;

        public static double Multiplier(double respectPoints, double ssPp)
            => respectPoints <= 0 || ssPp <= 0 ? 1 : Math.Clamp(respectPoints / ssPp, MULTIPLIER_MIN, MULTIPLIER_MAX);

        public enum MapType
        {
            Unknown,
            Stream,
            AimSlop,
            Tech,
            AimControl,
            Reading,
        }

        /// <param name="Value">RP.</param>
        /// <param name="Type">Map type.</param>
        /// <param name="HardMoments">Hard moments (≥ 60% of the hardest 20 s for ≥ 8 map seconds, gaps ≤ 3 s merged) — used by
        /// <see cref="RxSpikeFillerGuard"/>.</param>
        /// <param name="Composition">Share of the map's notes by pattern: "jumps", "streams", "control", "sliders", "simple";
        /// sums to 1 (diagnostics for the site, not used by the PP).</param>
        /// <param name="RemainderType">0.8.0 (Section Types): type of the map without its streams — the type of a stream map
        /// (<see cref="MapType.Stream"/>) outside its streams (classification with no stream notes, aim slop when nothing fits),
        /// <paramref name="Type"/> for every other type.</param>
        /// <param name="RemainderValue">RP of the map rated as <paramref name="RemainderType"/>.</param>
        public readonly record struct Result(double Value, MapType Type, int HardMoments = 0, Dictionary<string, double>? Composition = null,
                                             MapType RemainderType = MapType.Unknown, double RemainderValue = 0);

        // ---------------------------------------------------------------- RP

        private static double scale(MapType type) => type switch
        {
            MapType.Stream => 200,
            MapType.AimSlop => 340.4,
            MapType.Tech => 280, // 0.6.0: was 147 (fitted on 3 tech maps); tech maps now reach the +20% limit
            MapType.AimControl => 32.3,
            MapType.Reading => 439,
            _ => 363,
        };

        private const double intensity_exponent = 0.75;
        private const double control_intensity_exponent = 1.25;
        private const double endurance_seconds = 30;
        private const double endurance_exponent = 0.15;
        private const double reading_per_note = 0.03;
        private const double reading_notes_free = 4;
        private const double reaction_bonus = 0.10;
        private const double precision_exponent = 0.3;

        // Aim slop (0.3.0): jumps repeating the previous one (spacing ±15%, angle ±15°) are easy — RP × (1 − 0.45 · smoothstep(share, 35%, 50%)).
        // Sayfalse "simple comfortable jumps" 47% repeated, Attack / #WE ♥ YURI ~31% (not touched), most maps 5–20%.
        private const double repetition_penalty = 0.45;
        private const double repetition_start = 0.35;
        private const double repetition_full = 0.50;
        private const double fast_move_ms = 125;
        private const double turn_angle = 120;
        private const double unknown_control_max_spacing = 1.1;

        // ---------------------------------------------------------------- analysis (see tools/MosuPpLab/MapAnalyzer.cs)

        private const double stream_max_gap_ms = 95;
        private const double stream_max_spacing = 2.5;
        private const double stream_min_spacing = 0.2;
        private const int stream_min_notes = 5;
        private const double jump_min_spacing = 1.5;
        // 0.6.0 tech gate: slider share 33–43%, sliders at least ~half as fast as the jumps (slider path / jump travel over the
        // hardest 20 s 0.40–0.55; slider-jump and jump maps with slow sliders stay below 0.47), streams tolerated up to 25–45%.
        private const double tech_slider_share_start = 0.33;
        private const double tech_slider_share_full = 0.43;
        private const double tech_slider_aim_start = 0.40;
        private const double tech_slider_aim_full = 0.55;
        private const double tech_stream_start = 0.25;
        private const double tech_stream_full = 0.45;
        private const double density_weight = 0.5;
        private const double density_spacing_start = 0.5;
        private const double density_spacing_full = 1.0;
        private const int smooth_seconds = 5;
        private const int hardest_seconds = 20;
        private const double fitts_hard_share = 0.8;
        private const double velocity_reference = 50;
        private const double velocity_exponent = 1.5;
        private const double stream_curve_bpm = 410;
        private const double stream_curve_spacing = 0.5;
        private const double stream_curve_exponent = 0.526;
        private const double slider_weight = 1.0;
        private const double slider_end_leniency_ms = 36;
        private const double min_move_time_ms = 30;
        private const double endurance_share = 0.4;
        private const double moment_share = 0.6;
        private const int moment_merge_seconds = 3;
        private const int moment_min_seconds = 8;

        /// <summary>RP of the playable beatmap (mods applied) played with <paramref name="mods"/>.</summary>
        public static Result Calculate(IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            var objects = beatmap.HitObjects.OfType<OsuHitObject>().ToList();
            var notes = objects.Where(o => o is not Spinner).ToList();

            if (notes.Count < 2)
                return new Result(0, MapType.Unknown);

            double rate = ModUtils.CalculateRateWithMods(mods);
            bool locked = RxLockedDifficulty.IsLocked(mods);
            float ar = beatmap.Difficulty.ApproachRate;
            float od = beatmap.Difficulty.OverallDifficulty;
            double effectiveAr = locked ? RxLockedDifficulty.Ar(ar, rate) : ar;
            double arRate = locked ? 1 : rate;
            double preemptRealMs = preemptMs(ar) / arRate;
            double hit300RealMs = (80 - 6 * od) / arRate;

            double diameter = notes[0].Radius * 2;

            // ---------------- streams, jumps, rhythm (map time, rate-independent)
            double[] gaps = new double[notes.Count];
            double[] spacing = new double[notes.Count];
            bool[] fast = new bool[notes.Count];

            for (int i = 1; i < notes.Count; i++)
            {
                gaps[i] = notes[i].StartTime - notes[i - 1].StartTime;
                Vector2 from = notes[i - 1] is Slider s ? s.StackedEndPosition : notes[i - 1].StackedPosition;
                spacing[i] = Vector2.Distance(from, notes[i].StackedPosition) / diameter;
                fast[i] = gaps[i] > 0 && gaps[i] <= stream_max_gap_ms && spacing[i] >= stream_min_spacing
                          && spacing[i] <= stream_max_spacing && notes[i - 1] is not Slider;
            }

            bool[] inStream = new bool[notes.Count];
            int burstNotes = 0;
            var streamGaps = new List<double>();

            for (int i = 1; i < notes.Count;)
            {
                if (!fast[i])
                {
                    i++;
                    continue;
                }

                int start = i - 1;
                int end = i;
                while (end + 1 < notes.Count && fast[end + 1])
                    end++;

                int length = end - start + 1;

                if (length >= stream_min_notes)
                {
                    for (int k = start; k <= end; k++)
                        inStream[k] = true;
                    for (int k = start + 1; k <= end; k++)
                        streamGaps.Add(gaps[k]);
                }
                else if (length >= 3)
                    burstNotes += length;

                i = end + 1;
            }

            double streamShare = (double)inStream.Count(x => x) / notes.Count;
            double burstShare = (double)burstNotes / notes.Count;
            double streamBpm = streamGaps.Count == 0 ? 0 : 60000 / (4 * median(streamGaps));

            int jumps = 0, movements = 0;

            for (int i = 1; i < notes.Count; i++)
            {
                if (inStream[i] && inStream[i - 1])
                    continue;

                movements++;
                if (spacing[i] >= jump_min_spacing)
                    jumps++;
            }

            double jumpShare = movements == 0 ? 0 : (double)jumps / movements;
            double jumpRepetition = calculateJumpRepetition(notes, gaps, spacing);

            var sliders = notes.OfType<Slider>().ToList();
            double sliderShare = (double)sliders.Count / notes.Count;
            double sliderVelocitySpread = 0;

            var velocities = sliders.Where(s => s.SpanDuration > 0).Select(s => s.Path.Distance / s.SpanDuration).ToList();

            if (velocities.Count >= 2)
            {
                double mean = velocities.Average();
                sliderVelocitySpread = mean <= 0 ? 0 : Math.Sqrt(velocities.Average(v => (v - mean) * (v - mean))) / mean;
            }

            int odd = 0, counted = 0;

            for (int i = 1; i < notes.Count; i++)
            {
                double beat = beatmap.ControlPointInfo.TimingPointAt(notes[i].StartTime).BeatLength;
                if (gaps[i] <= 0 || gaps[i] > beat * 2)
                    continue;

                counted++;
                double quarters = gaps[i] / (beat / 4);
                if (Math.Abs(quarters - Math.Round(quarters)) > 0.12)
                    odd++;
            }

            double oddRhythmShare = counted == 0 ? 0 : (double)odd / counted;

            // ---------------- per-second timelines (1 s bins in map time; × rate = per played second)
            double t0 = objects[0].StartTime;
            int bins = Math.Max(1, (int)Math.Ceiling((objects[^1].GetEndTime() - t0) / 1000) + 1);
            double[] fitts = new double[bins];
            double[] skill = new double[bins];
            double[] jumpTravel = new double[bins];
            double[] sliderTravel = new double[bins];
            double[] composition = new double[5]; // jumps, streams, control, sliders, simple (note counts)

            for (int i = 0; i < notes.Count; i++)
            {
                int bin = Math.Clamp((int)((notes[i].StartTime - t0) / 1000), 0, bins - 1);
                // 0.5.0: the density term pays only for notes reached by a move (0 below 0.5 circles, full from 1 circle) —
                // many tiny moves are not hard aim control in RX, where nothing has to be tapped.
                if (i > 0)
                    fitts[bin] += density_weight * smoothStep(spacing[i], density_spacing_start, density_spacing_full);

                if (i > 0)
                {
                    fitts[bin] += Math.Log2(spacing[i] + 1);

                    double moveTime = notes[i - 1] is Slider previousSlider
                        ? Math.Max(notes[i].StartTime - previousSlider.EndTime + slider_end_leniency_ms, min_move_time_ms)
                        : gaps[i];
                    double velocity = moveTime > 0 ? spacing[i] * 1000 * rate / moveTime : 0;
                    double move = Math.Pow(velocity / velocity_reference, velocity_exponent);

                    // Composition: a fast move that turns at small / medium spacing is aim control, straight fast runs are streams.
                    bool fastMove = gaps[i] > 0 && gaps[i] <= fast_move_ms && spacing[i] >= stream_min_spacing;
                    double turnAngle = noteAngle(notes, i - 1);
                    bool turning = fastMove && spacing[i] <= stream_max_spacing && !double.IsNaN(turnAngle) && turnAngle < turn_angle;

                    if (inStream[i] && inStream[i - 1])
                    {
                        double bpm = 15000 * rate / gaps[i];
                        double effort = bpm / stream_curve_bpm * Math.Pow(spacing[i] / stream_curve_spacing, stream_curve_exponent);
                        skill[bin] += move * Math.Max(smoothStep(effort, 0.85, 1.15), smoothStep(spacing[i], 0.8, 1.5));
                    }
                    else
                    {
                        jumpTravel[bin] += spacing[i];
                        skill[bin] += move;
                    }

                    // Composition: one pattern per note (sliders, then streams / bursts, aim control, jumps, simple notes).
                    // A fast but wide move (> 2.5 circles, e.g. 1/2 jumps at 260 BPM) is a jump, not a stream.
                    composition[notes[i] is Slider ? 3
                        : fastMove && spacing[i] <= stream_max_spacing ? (turning ? 2 : 1)
                        : spacing[i] >= jump_min_spacing ? 0
                        : 4]++;
                }

                if (notes[i] is Slider slider && slider.SpanDuration > 0)
                {
                    double spanVelocity = slider.Path.Distance / diameter * 1000 * rate / slider.SpanDuration;
                    sliderTravel[bin] += slider.Path.Distance / diameter * (slider.RepeatCount + 1);
                    skill[bin] += slider_weight * (slider.RepeatCount + 1) * Math.Pow(spanVelocity / velocity_reference, velocity_exponent);
                }
            }

            double[] skillSmooth = movingAverage(skill.Select(v => v * rate).ToArray(), smooth_seconds);
            double skillPressure = skillSmooth.OrderByDescending(v => v).Take(hardest_seconds).Average();
            double skillHardSeconds = skillSmooth.Count(v => v >= endurance_share * skillPressure);
            int hardMoments = countHardMoments(skillSmooth, skillPressure);
            // Rate-independent: both speeds are per played second.
            double sliderAimRatio = hardestTravel(sliderTravel, rate) / Math.Max(hardestTravel(jumpTravel, rate), 1e-9);

            double[] fittsSmooth = movingAverage(fitts.Select(v => v * rate).ToArray(), smooth_seconds);
            double fittsPressure = fittsSmooth.OrderByDescending(v => v).Take(hardest_seconds).Average();
            double fittsHardSeconds = fittsSmooth.Count(v => v >= fitts_hard_share * fittsPressure) / rate;

            // ---------------- reading: spaced notes on screen (same as RxVisibleNotesReading)
            double preemptMapMs = preemptMs(effectiveAr);
            double[] weight = new double[notes.Count];
            for (int i = 1; i < notes.Count; i++)
                weight[i] = smoothStep(Vector2.Distance(notes[i - 1].Position, notes[i].Position) / diameter, 0.5, 1.5);

            double[] visible = new double[notes.Count];
            for (int i = 0; i < notes.Count; i++)
            {
                for (int j = i + 1; j < notes.Count && notes[j].StartTime - notes[i].StartTime < preemptMapMs; j++)
                    visible[i] += weight[j];
            }

            double visibleNotesTop = visible.OrderByDescending(v => v).Take(Math.Max(1, notes.Count / 5)).Average();

            MapType type = classify(streamShare, burstShare, streamBpm, jumpShare, sliderShare, sliderAimRatio, sliderVelocitySpread, oddRhythmShare,
                preemptMapMs);

            // 0.5.0: maps the 0.2.0 classification does not know (dense small-spacing control, tech) were rated with the generous
            // "unknown" scale and escaped the per-type balance. They now take the content type of the polygon's pattern classifier.
            if (type == MapType.Unknown)
                type = classifyUnknown(notes, gaps, spacing, streamShare, streamBpm, jumpShare, sliderShare, sliderAimRatio, sliderVelocitySpread,
                    oddRhythmShare);

            MapType remainderType = type == MapType.Stream
                ? classifyRemainder(burstShare, streamBpm, jumpShare, sliderShare, sliderAimRatio, sliderVelocitySpread, oddRhythmShare, preemptMapMs)
                : type;

            // ---------------- RP
            double reading = 1 + reading_per_note * Math.Max(0, visibleNotesTop - reading_notes_free)
                               + reaction_bonus * smoothStep(450 - preemptRealMs, 0, 150);
            double precision = Math.Pow(20 / Math.Max(1, hit300RealMs), precision_exponent);

            double compositionTotal = composition.Sum();
            var shares = compositionTotal <= 0
                ? null
                : new Dictionary<string, double>
                {
                    ["jumps"] = composition[0] / compositionTotal,
                    ["streams"] = composition[1] / compositionTotal,
                    ["control"] = composition[2] / compositionTotal,
                    ["sliders"] = composition[3] / compositionTotal,
                    ["simple"] = composition[4] / compositionTotal,
                };

            double rp(MapType t)
            {
                bool control = t == MapType.AimControl;
                double repetition = t == MapType.AimSlop ? 1 - repetition_penalty * smoothStep(jumpRepetition, repetition_start, repetition_full) : 1;
                double intensity = control
                    ? Math.Pow(Math.Max(0, fittsPressure), control_intensity_exponent)
                    : Math.Pow(Math.Max(0, skillPressure), intensity_exponent);
                double endurance = Math.Pow(1 + (control ? fittsHardSeconds : skillHardSeconds) / endurance_seconds, endurance_exponent);
                return scale(t) * intensity * endurance * reading * precision * repetition;
            }

            return new Result(rp(type), type, hardMoments, shares, remainderType, rp(remainderType));
        }

        private static int countHardMoments(double[] smooth, double hardest)
        {
            if (hardest <= 0)
                return 0;

            int count = 0, start = -1, end = -1;

            for (int b = 0; b <= smooth.Length; b++)
            {
                bool hard = b < smooth.Length && smooth[b] >= moment_share * hardest;

                if (hard && start >= 0 && b - end <= moment_merge_seconds)
                {
                    end = b;
                    continue;
                }

                if (hard || b == smooth.Length)
                {
                    if (start >= 0 && end - start + 1 >= moment_min_seconds)
                        count++;

                    start = end = hard ? b : -1;
                }
            }

            return count;
        }

        /// <summary>Share of jumps (≥ 1.5 circles, not fast moves) repeating the previous jump: spacing ±15% and angle ±15°.</summary>
        private static double calculateJumpRepetition(List<OsuHitObject> notes, double[] gaps, double[] spacing)
        {
            var jumps = new List<(double Spacing, double Angle)>();

            for (int i = 1; i + 1 < notes.Count; i++)
            {
                bool fastMove = gaps[i] > 0 && gaps[i] <= fast_move_ms && spacing[i] >= stream_min_spacing;
                if (fastMove || spacing[i] < jump_min_spacing)
                    continue;

                // Angle at note i between the move into it and the move out of it (stacked positions, slider ends).
                Vector2 inFrom = notes[i - 1] is Slider ps ? ps.StackedEndPosition : notes[i - 1].StackedPosition;
                Vector2 outFrom = notes[i] is Slider cs ? cs.StackedEndPosition : notes[i].StackedPosition;
                Vector2 a1 = inFrom - notes[i].StackedPosition;
                Vector2 a2 = notes[i + 1].StackedPosition - outFrom;
                if (a1.Length <= 1 || a2.Length <= 1)
                    continue;

                double angle = Math.Acos(Math.Clamp(Vector2.Dot(a1, a2) / (a1.Length * a2.Length), -1, 1)) * 180 / Math.PI;
                jumps.Add((spacing[i], angle));
            }

            int repeated = 0;
            for (int j = 1; j < jumps.Count; j++)
            {
                if (Math.Abs(jumps[j].Spacing - jumps[j - 1].Spacing) <= 0.15 * jumps[j - 1].Spacing && Math.Abs(jumps[j].Angle - jumps[j - 1].Angle) <= 15)
                    repeated++;
            }

            return jumps.Count < 2 ? 0 : (double)repeated / (jumps.Count - 1);
        }

        private static MapType classify(double streamShare, double burstShare, double streamBpm, double jumpShare, double sliderShare,
                                        double sliderAimRatio, double sliderVelocitySpread, double oddRhythmShare, double preemptMapMs)
        {
            double notStream = 1 - smoothStep(streamShare, 0.15, 0.35);
            double sliderHeavy = techSliders(sliderShare, sliderAimRatio);
            double fastStreams = smoothStep(streamBpm, 180, 200);

            // Low AR decides the map more than its patterns: a stream map at AR 0 is a reading map.
            if (smoothStep(preemptMapMs, 700, 1000) >= 0.5)
                return MapType.Reading;

            var scores = new[]
            {
                (MapType.Stream, smoothStep(streamShare, 0.20, 0.45) * fastStreams),
                (MapType.AimSlop, notStream * smoothStep(jumpShare, 0.45, 0.70) * (1 - sliderHeavy)),
                (MapType.Tech, techNotStream(streamShare) * sliderHeavy
                               * (0.7 + 0.3 * Math.Max(smoothStep(oddRhythmShare, 0.05, 0.15), smoothStep(sliderVelocitySpread, 0.15, 0.35)))),
                (MapType.AimControl, smoothStep(burstShare + streamShare, 0.20, 0.40) * (1 - fastStreams) * (1 - sliderHeavy)),
                (MapType.Reading, smoothStep(preemptMapMs, 700, 1000)),
            };

            // First maximum wins on ties (same order as the prototype's dictionary).
            var best = scores[0];
            foreach (var score in scores)
            {
                if (score.Item2 > best.Item2)
                    best = score;
            }

            return best.Item2 >= 0.25 ? best.Item1 : MapType.Unknown;
        }

        /// <summary>
        /// 0.8.0 (Section Types): the type of a stream map outside its streams — the 0.2.0 classification with no stream notes
        /// (the content fallback is skipped: its fast runs are the streams themselves); aim slop when nothing fits.
        /// </summary>
        private static MapType classifyRemainder(double burstShare, double streamBpm, double jumpShare, double sliderShare, double sliderAimRatio,
                                                 double sliderVelocitySpread, double oddRhythmShare, double preemptMapMs)
        {
            MapType type = classify(0, burstShare, streamBpm, jumpShare, sliderShare, sliderAimRatio, sliderVelocitySpread, oddRhythmShare,
                preemptMapMs);
            return type is MapType.AimSlop or MapType.Tech or MapType.AimControl ? type : MapType.AimSlop;
        }

        /// <summary>
        /// How much the map is made of tech sliders (0.6.0): enough sliders, and sliders that move about as much as the jumps.
        /// Before 0.6.0 it was the slider share alone (45–55%), which missed most tech maps (39–49% sliders).
        /// </summary>
        private static double techSliders(double sliderShare, double sliderAimRatio)
            => smoothStep(sliderShare, tech_slider_share_start, tech_slider_share_full)
               * smoothStep(sliderAimRatio, tech_slider_aim_start, tech_slider_aim_full);

        /// <summary>Tech maps often carry short streams between the slider sections (Lost Requiem 30%, RPG 27%).</summary>
        private static double techNotStream(double streamShare) => 1 - smoothStep(streamShare, tech_stream_start, tech_stream_full);

        /// <summary>Path in circle diameters per played second over the hardest 20 s (5 s moving average).</summary>
        private static double hardestTravel(double[] perSecond, double rate)
            => movingAverage(perSecond.Select(v => v * rate).ToArray(), smooth_seconds).OrderByDescending(v => v).Take(hardest_seconds).Average();

        /// <summary>
        /// Content type for maps the 0.2.0 classification leaves unknown (same as MapAnalyzer.classify of the polygon, without the
        /// reading and Arles overrides): streams go straight, the same density with turns or in short bursts at small / medium
        /// spacing is aim control.
        /// </summary>
        private static MapType classifyUnknown(List<OsuHitObject> notes, double[] gaps, double[] spacing, double streamShare, double streamBpm,
                                               double jumpShare, double sliderShare, double sliderAimRatio, double sliderVelocitySpread,
                                               double oddRhythmShare)
        {
            bool[] fastMove = new bool[notes.Count];
            for (int i = 1; i < notes.Count; i++)
                fastMove[i] = gaps[i] > 0 && gaps[i] <= fast_move_ms && spacing[i] >= stream_min_spacing;

            int shortBurstNotes = 0, run = 0;
            for (int i = 1; i <= notes.Count; i++)
            {
                if (i < notes.Count && fastMove[i])
                {
                    run++;
                    continue;
                }

                if (run > 0 && run + 1 >= 3 && run + 1 <= 8)
                    shortBurstNotes += run + 1;
                run = 0;
            }

            int runTurns = 0, runAngles = 0, fastCount = 0;
            var runSpacings = new List<double>();

            for (int i = 1; i < notes.Count; i++)
            {
                if (!fastMove[i])
                    continue;

                fastCount++;
                runSpacings.Add(spacing[i]);

                if (i + 1 < notes.Count && fastMove[i + 1])
                {
                    double angle = noteAngle(notes, i);
                    if (double.IsNaN(angle))
                        continue;

                    runAngles++;
                    if (angle < turn_angle)
                        runTurns++;
                }
            }

            double fastShare = (double)fastCount / notes.Count;
            double shortBurstShare = (double)shortBurstNotes / notes.Count;
            double fastTurnShare = runAngles == 0 ? 0 : (double)runTurns / runAngles;
            double fastRunSpacing = runSpacings.Count == 0 ? 0 : median(runSpacings);

            double notStream = 1 - smoothStep(streamShare, 0.15, 0.35);
            double sliderHeavy = techSliders(sliderShare, sliderAimRatio);
            double fastStreams = smoothStep(streamBpm, 180, 200);
            double turning = smoothStep(fastTurnShare, 0.20, 0.50);
            double control = Math.Max(turning * smoothStep(fastShare, 0.20, 0.40), smoothStep(shortBurstShare, 0.20, 0.33));

            var scores = new[]
            {
                (MapType.Stream, smoothStep(streamShare, 0.20, 0.45) * fastStreams * (1 - turning)),
                (MapType.AimSlop, notStream * smoothStep(jumpShare, 0.45, 0.70) * (1 - sliderHeavy)),
                (MapType.Tech, techNotStream(streamShare) * sliderHeavy
                               * (0.7 + 0.3 * Math.Max(smoothStep(oddRhythmShare, 0.05, 0.15), smoothStep(sliderVelocitySpread, 0.15, 0.35)))),
                (MapType.AimControl, control * (1 - smoothStep(fastRunSpacing, 1.5, 2.5)) * (1 - sliderHeavy)),
            };

            var best = scores[0];
            foreach (var score in scores)
            {
                if (score.Item2 > best.Item2)
                    best = score;
            }

            if (best.Item2 < 0.25)
                return MapType.Unknown;

            // Only small-spacing dense patterns become aim control here (Bleed 0.58, Sunless Rise 0.85, Hashire 1.07 circles);
            // wider ones stay unknown (We Could Get More Machinegun Psystyle!, 1.13, is a jump map). Measured in the circles played,
            // like the rest of the classification: the type is the type of the map as played with its mods (lower CS can turn weak
            // control into streams / control, HR can turn streams into control).
            return best.Item1 == MapType.AimControl && fastRunSpacing > unknown_control_max_spacing ? MapType.Unknown : best.Item1;
        }

        /// <summary>Angle at note <paramref name="i"/> between the move into it and the move out of it (stacked positions, slider ends); NaN if undefined.</summary>
        private static double noteAngle(List<OsuHitObject> notes, int i)
        {
            if (i <= 0 || i + 1 >= notes.Count)
                return double.NaN;

            Vector2 inFrom = notes[i - 1] is Slider ps ? ps.StackedEndPosition : notes[i - 1].StackedPosition;
            Vector2 outFrom = notes[i] is Slider cs ? cs.StackedEndPosition : notes[i].StackedPosition;
            Vector2 a1 = inFrom - notes[i].StackedPosition;
            Vector2 a2 = notes[i + 1].StackedPosition - outFrom;
            if (a1.Length <= 1 || a2.Length <= 1)
                return double.NaN;

            return Math.Acos(Math.Clamp(Vector2.Dot(a1, a2) / (a1.Length * a2.Length), -1, 1)) * 180 / Math.PI;
        }

        private static double preemptMs(double ar) => ar < 5 ? 1800 - 120 * ar : 1950 - 150 * ar;

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }

        private static double median(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
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
