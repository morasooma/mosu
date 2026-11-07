// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPp2
{
    /// <summary>Movement types of a note (what the cursor does to reach it). Slider tracking is a separate part of the load.</summary>
    public enum Mp2NoteType
    {
        /// <summary>Stacked / one-point note, chord partner or first note after a break: nothing to aim.</summary>
        Free,

        /// <summary>Slow small move (below 1.2–1.8 diameters, not fast).</summary>
        Simple,

        /// <summary>Straight run of 5+ notes at the local pulse.</summary>
        Stream,

        /// <summary>Same with spacing above 1.3–1.9 diameters.</summary>
        SpacedStream,

        /// <summary>Straight run of 3–4 notes.</summary>
        Burst,

        /// <summary>Fast run that keeps turning (aim control).</summary>
        Zigzag,

        /// <summary>Fast turning run at tiny spacing.</summary>
        Jitter,

        /// <summary>Jump that goes back (angle below ~45–68°).</summary>
        JumpBackForth,

        /// <summary>Jump at a sharp-to-right angle.</summary>
        JumpAngular,

        /// <summary>Jump that keeps the direction (angle above ~105–125°).</summary>
        JumpFlow,
    }

    /// <summary>Shape classification of every note: soft memberships (sum 1) and modifiers.</summary>
    public sealed class Mp2NoteTypes
    {
        public const int COUNT = 10;

        /// <summary>[note][type] membership, Σ over types = 1.</summary>
        public double[][] Membership { get; init; } = Array.Empty<double[]>();

        /// <summary>Awkwardness: angle, spacing and rhythm change against the previous move (0–1).</summary>
        public double[] Awkward { get; init; } = Array.Empty<double>();

        /// <summary>Repetition: the move repeats one of the 2–4 before it (0–1).</summary>
        public double[] Repeat { get; init; } = Array.Empty<double>();

        /// <summary>Off-grid rhythm (1/3, 1/6, unsnapped) (0–1).</summary>
        public double[] OddRhythm { get; init; } = Array.Empty<double>();

        /// <summary>Note starts a new sequence (break, spinner) or is a chord partner.</summary>
        public bool[] Break { get; init; } = Array.Empty<bool>();

        /// <summary>Note is hit together with the previous one (2B chord partner, under 20 map ms).</summary>
        public bool[] Chord { get; init; } = Array.Empty<bool>();

        /// <summary>The note is inside a fast run (at the local pulse, 3+ notes), whatever its spacing (0–1): CS does not change it.</summary>
        public double[] FastRun { get; init; } = Array.Empty<double>();

        /// <summary>Aim control (0–1): the note is a turn inside a fast run — zigzag or jitter membership (same shape gates as the
        /// types, so the reward and the map type see the same notes; spacing in diameters as played, map time only).</summary>
        public double[] AimControl { get; init; } = Array.Empty<double>();

        /// <summary>Jump control (0–1): a jump at an uncomfortable mid angle (55–125°) and / or one that breaks the previous move
        /// (awkwardness), less when it repeats a move just before. Separates "awkward jump" maps from plain aim slop.</summary>
        public double[] JumpControl { get; init; } = Array.Empty<double>();

        /// <summary>Control demand of the note: <see cref="AimControl"/> + <see cref="JumpControl"/> (disjoint types, so ≤ 1).</summary>
        public double[] Control { get; init; } = Array.Empty<double>();

        public static bool IsJump(int type) => type is (int)Mp2NoteType.JumpBackForth or (int)Mp2NoteType.JumpAngular or (int)Mp2NoteType.JumpFlow;
    }

    /// <summary>
    /// MosuPp 2 classifier (review/classifier.md): what the cursor does at every note, from shape only — spacing (diameters as
    /// played: CS / HR / EZ / DA change the type, as the owner wants), angles, the gap relative to the local fastest pulse and
    /// map-time gates. No BPM threshold and nothing in real time, so DT / HT never change a type; only the load changes.
    /// </summary>
    public static class Mp2Classifier
    {
        // Shape thresholds (fitted on the owner's labels, leave-one-map-out 83%).
        private const double fast_wide_lo = 2.8, fast_wide_hi = 3.8;
        private const double turn_lo = 0.22, turn_hi = 0.40;
        private const double spaced_lo = 1.3, spaced_hi = 1.9;
        private const double fast_gate_lo = 130, fast_gate_hi = 185;
        private const double irr_angle = 45, irr_spacing = 0.6, irr_rhythm = 0.5, irr_lo = 1.2, irr_hi = 2.2;

        // Jump control: mid angle (between back-and-forth and flow), mixed half and half with awkwardness, half off for repeats.
        // Map level vs plain aim slop (contribution weighted): AUC 0.86 against 0.80 of awkwardness alone (REPORT_DETECTOR.md).
        private const double mid_lo = 55, mid_lo_full = 75, mid_hi_full = 105, mid_hi = 125;
        private const double jump_awkward_share = 0.5, jump_repeat_discount = 0.5;

        // Arles needs jumps mixed with straight runs; a map whose moves are mostly fast turns is control with jumps, not Arles
        // (VANISH). Plateau 0.25–0.30 on the labelled maps.
        private const double arles_aim_lo = 0.25, arles_aim_hi = 0.40;

        public static Mp2NoteTypes Classify(Mp2Features f)
        {
            int n = f.Count;
            var t = new double[n];
            var gap = new double[n];
            var vec = new Vector2d[n];
            var s = new double[n];
            var isBreak = new bool[n];
            var chord = new bool[n];

            for (int i = 0; i < n; i++)
            {
                t[i] = f.MapTime[i] * 1000;
                gap[i] = i == 0 ? 1e9 : t[i] - t[i - 1];
                Vector2d start = f.FixedStart[i] ?? (i > 0 ? f.Position[i - 1] : f.Position[i]);
                vec[i] = f.Position[i] - start;
                s[i] = vec[i].Length;
                isBreak[i] = i == 0 || f.AfterBreak[i] || gap[i] > 1500;
                chord[i] = !isBreak[i] && gap[i] < 20 && s[i] > 0.3;
            }

            // Chord partners are transparent for shapes: previous / next note that is not a chord partner.
            var prev = new int[n];
            var next = new int[n];
            for (int i = 0; i < n; i++)
            {
                prev[i] = i - 1;
                while (prev[i] > 0 && chord[prev[i]])
                    prev[i]--;
            }

            for (int i = n - 1; i >= 0; i--)
                next[i] = i + 1 < n && chord[i + 1] ? next[i + 1] : i + 1;

            // Angle at each note between the move in and the move out: 180 = straight, 0 = reversal.
            var theta = Enumerable.Repeat(double.NaN, n).ToArray();
            for (int i = 1; i + 1 < n; i++)
            {
                int o = next[i];
                if (chord[i] || o >= n || isBreak[o] || s[i] <= 0.15 || s[o] <= 0.15)
                    continue;

                double cos = Math.Clamp(Vector2d.Dot(vec[i], vec[o]) / (s[i] * s[o]), -1, 1);
                theta[i] = 180 - Math.Acos(cos) * 180 / Math.PI;
            }

            // Local pulse (25th percentile of ±12 gaps) and "fast": at the local pulse, not slower than 1/2 at ~166 BPM (map time).
            var fastRel = new double[n];
            var fast = new double[n];
            for (int i = 0; i < n; i++)
            {
                var window = new List<double>();
                for (int j = Math.Max(1, i - 12); j < Math.Min(n, i + 13); j++)
                {
                    if (gap[j] >= 20 && gap[j] <= 1500)
                        window.Add(gap[j]);
                }

                double pulse = window.Count > 0 ? percentile(window, 0.25) : gap[i];
                fastRel[i] = !isBreak[i] && gap[i] >= 20 ? 1 - ss(gap[i] / Math.Max(1, pulse), 1.25, 1.7) : 0;
                fast[i] = fastRel[i] * (1 - ss(gap[i], fast_gate_lo, fast_gate_hi));
            }

            // Runs of fast notes with equal gaps (±15%).
            var run = Enumerable.Repeat(1, n).ToArray();
            for (int i = 1; i < n;)
            {
                if (fast[i] < 0.5)
                {
                    i++;
                    continue;
                }

                int j = i;
                while (j + 1 < n && fast[j + 1] >= 0.5 && Math.Abs(gap[j + 1] / gap[i] - 1) < 0.15)
                    j++;
                for (int k = i; k <= j; k++)
                    run[k] = j - i + 2;
                i = j + 1;
            }

            var membership = new double[n][];
            var awkward = new double[n];
            var repeat = new double[n];
            var odd = new double[n];
            var fastRun = new double[n];
            var turnOf = new double[n];

            for (int i = 0; i < n; i++)
            {
                var m = new double[Mp2NoteTypes.COUNT];
                membership[i] = m;

                if (!isBreak[i] && gap[i] >= 20 && gap[i] <= 2 * f.BeatLength[i] && f.BeatLength[i] > 0)
                {
                    double q = gap[i] / (f.BeatLength[i] / 8);
                    odd[i] = ss(Math.Abs(q - Math.Round(q)), 0.12, 0.25);
                }

                if (isBreak[i] || chord[i])
                {
                    m[(int)Mp2NoteType.Free] = 1;
                    continue;
                }

                double thIn = prev[i] >= 0 ? theta[prev[i]] : double.NaN;
                double thOut = theta[i];
                double turnSum = 0;
                int turnCount = 0;
                foreach (double th in new[] { thIn, thOut })
                {
                    if (double.IsNaN(th))
                        continue;
                    turnSum += (180 - th) / 180;
                    turnCount++;
                }

                double turn = turnCount > 0 ? turnSum / turnCount : 0; // 0 straight .. 1 reversal
                turnOf[i] = turn;
                double fastM0 = fastRel[i] * (1 - ss(gap[i], fast_gate_lo, fast_gate_hi));
                double longRun = ss(run[i], 4.5, 6.5);
                double stack = (1 - ss(s[i], 0.15, 0.35)) * (1 - fastM0 * longRun);
                double moving = 1 - stack;
                double fastM = fastM0 * (1 - ss(s[i], fast_wide_lo, fast_wide_hi));
                double shortRun = ss(run[i], 2.5, 3.0) * (1 - longRun);
                fastRun[i] = fastM0 * (longRun + shortRun);
                double straight = 1 - ss(turn, turn_lo, turn_hi);
                double spaced = ss(s[i], spaced_lo, spaced_hi);
                double tiny = 1 - ss(s[i], 0.45, 0.8);

                double zigAll = fastM * (longRun + shortRun) * (1 - straight);
                double jumpish = (1 - fastM) + fastM * (1 - longRun - shortRun);
                double big = ss(s[i], 1.2, 1.8);
                double jump = jumpish * big;
                double bf = ss(turn, 0.62, 0.75);
                double flow = 1 - ss(turn, 0.30, 0.42);

                m[(int)Mp2NoteType.Stream] = fastM * longRun * straight * (1 - spaced);
                m[(int)Mp2NoteType.SpacedStream] = fastM * longRun * straight * spaced;
                m[(int)Mp2NoteType.Burst] = fastM * shortRun * straight;
                m[(int)Mp2NoteType.Jitter] = zigAll * tiny;
                m[(int)Mp2NoteType.Zigzag] = zigAll * (1 - tiny);
                m[(int)Mp2NoteType.Simple] = jumpish * (1 - big);
                m[(int)Mp2NoteType.JumpBackForth] = jump * bf;
                m[(int)Mp2NoteType.JumpFlow] = jump * flow;
                m[(int)Mp2NoteType.JumpAngular] = jump * Math.Max(0, 1 - bf - flow);

                double total = m.Sum();
                for (int k = 0; k < m.Length; k++)
                    m[k] = total > 0 ? m[k] / total * moving : 0;
                m[(int)Mp2NoteType.Free] += stack + (total > 0 ? 0 : moving);
            }

            for (int i = 2; i < n; i++)
            {
                int b = prev[i];
                if (chord[i] || b < 1 || isBreak[i] || isBreak[b])
                    continue;

                double dA = !double.IsNaN(theta[i]) && !double.IsNaN(theta[b]) ? Math.Abs(theta[i] - theta[b]) : 30;
                double dS = Math.Abs(Math.Log(Math.Max(0.3, s[i]) / Math.Max(0.3, s[b])));
                double dR = Math.Abs(Math.Log(Math.Max(1, gap[i]) / Math.Max(1, gap[b]))) > 0.2 ? 1 : 0;
                awkward[i] = ss(dA / irr_angle + dS / irr_spacing + irr_rhythm * dR, irr_lo, irr_hi);

                double best = 0;
                int j = b;
                for (int k = 2; k <= 4; k++)
                {
                    j = j > 0 ? prev[j] : -1;
                    if (j < 1)
                        break;
                    if (isBreak[j] || Math.Abs(gap[j] - gap[i]) > 0.1 * Math.Max(gap[i], 1))
                        continue;

                    double ds = Math.Abs(s[j] - s[i]) / (0.25 * s[i] + 0.3);
                    double da = double.IsNaN(theta[i]) && double.IsNaN(theta[j]) ? 0
                        : double.IsNaN(theta[i]) || double.IsNaN(theta[j]) ? 2 : Math.Abs(theta[i] - theta[j]) / 25;
                    best = Math.Max(best, Math.Exp(-(ds * ds + da * da)));
                }

                repeat[i] = best;
            }

            // Control demand: turns inside fast runs (aim control) and uncomfortable / broken jumps (jump control).
            var aimControl = new double[n];
            var jumpControl = new double[n];
            var control = new double[n];
            for (int i = 0; i < n; i++)
            {
                double[] m = membership[i];
                aimControl[i] = m[(int)Mp2NoteType.Zigzag] + m[(int)Mp2NoteType.Jitter];

                double jumps = m[(int)Mp2NoteType.JumpBackForth] + m[(int)Mp2NoteType.JumpAngular] + m[(int)Mp2NoteType.JumpFlow];
                double angle = 180 * (1 - turnOf[i]);
                double mid = ss(angle, mid_lo, mid_lo_full) * (1 - ss(angle, mid_hi_full, mid_hi));
                double uncomfortable = Math.Clamp(jump_awkward_share * awkward[i] + (1 - jump_awkward_share) * mid, 0, 1);
                jumpControl[i] = jumps * uncomfortable * (1 - jump_repeat_discount * repeat[i]);
                control[i] = aimControl[i] + jumpControl[i];
            }

            return new Mp2NoteTypes
            {
                Membership = membership, Awkward = awkward, Repeat = repeat, OddRhythm = odd, Break = isBreak.Select((b, i) => b || chord[i]).ToArray(), Chord = chord,
                FastRun = fastRun, AimControl = aimControl, JumpControl = jumpControl, Control = control,
            };
        }

        /// <summary>
        /// Owner-style map type (stream / slop / control / tech / reading / arles, sum 1) from what is played: note composition
        /// (each note counts once), awkward jumps half control, map-level gates for tech (sliders in the hard part), reading (AR,
        /// notes on screen) and Arles (wide jumps mixed with spaced runs and speed changes in the hard part).
        /// </summary>
        public static Dictionary<string, double> MapType(Mp2Features f, Mp2NoteTypes types, double[] hardWeight, IDictionary<string, double>? evidence = null)
        {
            int n = f.Count;
            var fine = new double[Mp2NoteTypes.COUNT];
            double awkwardJumps = 0;

            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < Mp2NoteTypes.COUNT; k++)
                {
                    fine[k] += types.Membership[i][k] / n;
                    if (Mp2NoteTypes.IsJump(k))
                        awkwardJumps += types.Membership[i][k] * types.Awkward[i] / n;
                }
            }

            double fv(Mp2NoteType type) => fine[(int)type];
            double stream = fv(Mp2NoteType.Stream) + fv(Mp2NoteType.SpacedStream) + 0.1 * fv(Mp2NoteType.Burst);
            double aimControl = 1.3 * (fv(Mp2NoteType.Zigzag) + fv(Mp2NoteType.Jitter) + 0.9 * fv(Mp2NoteType.Burst));
            double control = aimControl + 1.3 * 0.5 * awkwardJumps;
            double slop = Math.Max(0, fv(Mp2NoteType.Simple) + fv(Mp2NoteType.JumpBackForth) + fv(Mp2NoteType.JumpFlow) + fv(Mp2NoteType.JumpAngular) - 0.5 * awkwardJumps);
            double movementTotal = Math.Max(1e-9, stream + control + slop);

            double weightTotal = Math.Max(1e-12, hardWeight.Sum());
            double sliderHard = 0, sliderMoveHard = 0;
            for (int i = 0; i < n; i++)
            {
                if (!f.IsSlider[i])
                    continue;
                sliderHard += hardWeight[i] / weightTotal;
                sliderMoveHard += hardWeight[i] * ss(f.SliderTravel[i], 0.3, 1.0) / weightTotal;
            }

            double techEvidence = sliderHard / 0.55 * Math.Pow(Math.Max(sliderMoveHard, 1e-3) / 0.12, 0.3);
            double tech = 1 / (1 + Math.Pow(Math.Max(techEvidence, 1e-6) / 1.1, -6));

            double preemptMap = n > 0 ? f.Preempt.Average() * f.Rate : 0; // map ms (AR as shown)
            int top = Math.Max(1, n / 5);
            double visibleTop = n > 0 ? f.Visible.OrderByDescending(v => v).Take(top).Average() : 0;
            double reading = Math.Max(ss(preemptMap, 650, 900), 0.8 * ss(visibleTop, 5.5, 7.5));

            var (mixedShare, speedShare, wideShare) = ArlesParts(f, types, hardWeight);
            // Thresholds from the labelled maps (8 of 11 Arles maps, 5 false alarms among 69 others: the parts overlap with control).
            // Tech goes first: tech maps mix jumps and sliders the same way.
            double arlesShape = ss(mixedShare, 0.16, 0.26) * ss(speedShare, 0.02, 0.04) * ss(wideShare, 0.30, 0.39);
            double aimShare = aimControl / movementTotal;
            double arles = arlesShape * (1 - ss(aimShare, arles_aim_lo, arles_aim_hi));
            double rest = (1 - reading) * (1 - arles) * (1 - tech);

            if (evidence != null)
            {
                evidence["awkward_jumps"] = awkwardJumps;
                evidence["slider_hard"] = sliderHard;
                evidence["slider_move_hard"] = sliderMoveHard;
                evidence["tech_evidence"] = techEvidence;
                evidence["preempt_map_ms"] = preemptMap;
                evidence["visible_top"] = visibleTop;
                evidence["arles_mixed"] = mixedShare;
                evidence["arles_speed_change"] = speedShare;
                evidence["arles_wide"] = wideShare;
                evidence["arles_shape"] = arlesShape;
                evidence["control_aim_share"] = aimShare;

                // Control subtypes in the hard part (weights = PP earned), for the explanation and the reward.
                double aimHard = 0, jumpHard = 0;
                for (int i = 0; i < n; i++)
                {
                    aimHard += hardWeight[i] * types.AimControl[i] / weightTotal;
                    jumpHard += hardWeight[i] * types.JumpControl[i] / weightTotal;
                }

                evidence["control_aim_hard"] = aimHard;
                evidence["control_jump_hard"] = jumpHard;
            }

            return new Dictionary<string, double>
            {
                ["stream"] = stream / movementTotal * rest,
                ["slop"] = slop / movementTotal * rest,
                ["control"] = control / movementTotal * rest,
                ["tech"] = tech * (1 - reading),
                ["reading"] = reading,
                ["arles"] = arles * (1 - tech) * (1 - reading),
            };
        }

        /// <summary>Arles evidence in the hard part (weights <paramref name="w"/>): jumps mixed with spaced runs within a moment,
        /// large speed changes between wide moves, wide moves (3+ diameters).</summary>
        public static (double Mixed, double SpeedChange, double Wide) ArlesParts(Mp2Features f, Mp2NoteTypes types, double[] w)
        {
            double total = Math.Max(1e-12, w.Sum());
            int n = f.Count;
            double mixed = 0, speedChange = 0, wide = 0;

            // Jumps and runs mixed within a moment: composition around each note (Gaussian, σ = 1 map second).
            var jumpMix = new double[n];
            var runMix = new double[n];
            for (int i = 0, lo = 0, hi = 0; i < n; i++)
            {
                while (f.MapTime[lo] < f.MapTime[i] - 3)
                    lo++;
                while (hi + 1 < n && f.MapTime[hi + 1] <= f.MapTime[i] + 3)
                    hi++;

                double z = 0;
                for (int j = lo; j <= hi; j++)
                {
                    double d = f.MapTime[j] - f.MapTime[i];
                    double k = Math.Exp(-0.5 * d * d);
                    double[] m = types.Membership[j];
                    jumpMix[i] += k * (m[(int)Mp2NoteType.JumpBackForth] + m[(int)Mp2NoteType.JumpFlow] + m[(int)Mp2NoteType.JumpAngular]);
                    runMix[i] += k * (m[(int)Mp2NoteType.SpacedStream] + m[(int)Mp2NoteType.Burst] + 0.5 * m[(int)Mp2NoteType.Stream]);
                    z += k;
                }

                jumpMix[i] /= z;
                runMix[i] /= z;
            }

            for (int i = 0; i < n; i++)
            {
                mixed += w[i] * Math.Sqrt(Math.Max(0, jumpMix[i] * runMix[i])) / total;

                double s = spacing(f, i);
                if (s >= 3 && !types.Break[i])
                    wide += w[i] / total;

                if (i >= 2 && !types.Break[i] && !types.Break[i - 1])
                {
                    double sa = spacing(f, i - 1);
                    if (sa >= 0.8 && s >= 0.8)
                    {
                        double va = sa / moveTime(f, i - 1), vb = s / moveTime(f, i);
                        if (Math.Max(va, vb) / Math.Max(1e-6, Math.Min(va, vb)) >= 2.5 && Math.Max(sa, s) >= 3)
                            speedChange += w[i] / total;
                    }
                }
            }

            return (mixed, speedChange, wide);
        }

        /// <summary>Time the cursor has for the move into note i (free time + late window), real seconds.</summary>
        private static double moveTime(Mp2Features f, int i) => Math.Max(1e-3, f.Time[i] - f.FreeTime[i] + f.Window[i]);

        private static double spacing(Mp2Features f, int i)
            => i == 0 ? 0 : (f.Position[i] - (f.FixedStart[i] ?? f.Position[i - 1])).Length;

        private static double percentile(List<double> values, double q)
        {
            values.Sort();
            double position = q * (values.Count - 1);
            int low = (int)Math.Floor(position), high = (int)Math.Ceiling(position);
            return values[low] + (values[high] - values[low]) * (position - low);
        }

        internal static double ss(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
