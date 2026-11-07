// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPp2
{
    /// <summary>MosuPp 2 result of a map + mods (SS) with diagnostics.</summary>
    public sealed class Mp2Difficulty
    {
        /// <summary>Map difficulty D = level · length factor · short-map factor.</summary>
        public double Difficulty { get; init; }

        /// <summary>Mean of the hardest moments (<see cref="Mp2Parameters.PeakSeconds"/>, real time).</summary>
        public double Level { get; init; }

        /// <summary>Real seconds of the map at the level's difficulty (moments weighted by (moment / level)^HardPower).</summary>
        public double HardSeconds { get; init; }

        /// <summary>PP for an SS (after the easy-map mod gain cap when the no-mod reference was given).</summary>
        public double PpSs { get; init; }

        /// <summary>SS PP of the same map in Relax only (0 when not given).</summary>
        public double NoModPpSs { get; init; }

        /// <summary>SS PP taken by the easy-map mod gain cap.</summary>
        public double ModCapLoss { get; init; }

        /// <summary>Load of the moment ending at every note.</summary>
        public double[] Moments { get; init; } = Array.Empty<double>();

        /// <summary>Share of the PP per skill: aim, agility, control, track (sums to 1).</summary>
        public IReadOnlyDictionary<string, double> SkillShares { get; init; } = new Dictionary<string, double>();

        /// <summary>Notes weighted by how close their load is to the level (for the miss penalty).</summary>
        public double HardNotes { get; init; }

        public int Notes { get; init; }

        /// <summary>Per-note breakdown; only with <c>trace</c>.</summary>
        public Mp2Trace? Trace { get; init; }
    }

    /// <summary>Everything needed to explain a result: per note load parts and the exact share of D every note earned.</summary>
    public sealed class Mp2Trace
    {
        /// <summary>Note load after all factors and the cap.</summary>
        public double[] Load { get; init; } = Array.Empty<double>();

        /// <summary>[note][skill] share of the note's load: aim, agility, control, track (Euler split of the norm).</summary>
        public double[][] Skill { get; init; } = Array.Empty<double[]>();

        /// <summary>Exact share of D earned by every note (Euler / Aumann–Shapley: Σ = D; may be slightly negative for a peak
        /// note that also raises the bar for the hard time).</summary>
        public double[] Contribution { get; init; } = Array.Empty<double>();

        /// <summary>Multipliers of the note load: reading, circle size, repetition, type scale, awkwardness, time headroom (on aim).</summary>
        public double[] Reading { get; init; } = Array.Empty<double>();

        public double[] Size { get; init; } = Array.Empty<double>();
        public double[] Repetition { get; init; } = Array.Empty<double>();
        public double[] TypeScale { get; init; } = Array.Empty<double>();
        public double[] Awkward { get; init; } = Array.Empty<double>();
        public double[] Headroom { get; init; } = Array.Empty<double>();

        /// <summary>The note's load was cut by the neighbour cap.</summary>
        public bool[] Capped { get; init; } = Array.Empty<bool>();

        public Mp2NoteTypes Types { get; init; } = new Mp2NoteTypes();

        /// <summary>Length factor (1 + H / T0)^λ and short-map factor.</summary>
        public double LengthFactor { get; init; }

        /// <summary>Control stamina multiplier of every moment (1 = none).</summary>
        public double[] Stamina { get; init; } = Array.Empty<double>();

        public double ShortFactor { get; init; }
    }

    /// <summary>Parts of the model that can be switched off, to measure what each one gave or took (<see cref="Mp2Explain"/>).</summary>
    [Flags]
    public enum Mp2Switch
    {
        None = 0,
        NoReading = 1,
        NoSize = 2,
        NoRepetition = 4,
        NoHeadroom = 8,
        NoTypeScale = 16,
        NoAwkward = 32,
        NoCap = 64,
        NoLength = 128,
        NoStamina = 256,
    }

    /// <summary>
    /// The MosuPp 2 model (MOSUPP2_DESIGN.md). A pure function of <see cref="Mp2Features"/> and <see cref="Mp2Parameters"/>:
    /// <list type="number">
    /// <item>lazy cursor: the cursor goes only <c>LazyRadius</c> into each circle (Relax players cut corners);</item>
    /// <item>every note is classified (<see cref="Mp2Classifier"/>): its type scales the movement load, awkwardness adds to jumps
    /// and zigzags;</item>
    /// <item>note load = norm of Fitts aim (bits/s; spare time discounts stop-and-go moves), agility (velocity change against the
    /// nearest natural motion: keep going or bounce back), aim control (fast sideways turns at small spacing) and saturating slider
    /// tracking; times reading, a circle-size factor that only counts without time to correct, a repetition discount; capped at
    /// LoadCap × the median of its neighbours;</item>
    /// <item>moment at every note: power mean of the loads in the last MomentSeconds (partial overlap, so no steps in rate);</item>
    /// <item>level = mean of the hardest PeakSeconds; hard time = Σ real time · (moment / level)^HardPower;
    /// D = level · (1 + hard time / LengthSeconds)^λ · min(1, covered / PeakSeconds)^ShortExponent; PP_SS = PpScale · D^PpExponent.</item>
    /// </list>
    /// </summary>
    public static class Mp2Model
    {
        /// <summary>Neighbours on each side whose median caps a note's load.</summary>
        public const int CAP_NEIGHBOURS = 4;

        /// <summary>Circle diameter at CS 4, osu! pixels: the screen-space unit of distances and speeds.</summary>
        public const double REFERENCE_DIAMETER = 2 * 64 * (1 - 0.7 * (4 - 5) / 5.0) / 2 * 1.00041;

        /// <summary>How far from its centre a chord note can be hit, diameters (inside the circle, a bit of margin).</summary>
        public const double CHORD_REACH = 0.4;

        /// <summary>Movements this far back are compared for repetition.</summary>
        public const int REPETITION_LOOKBACK = 4;

        /// <summary>Skill names, in the order of <see cref="Mp2Trace.Skill"/>.</summary>
        public static readonly string[] SKILLS = { "aim", "agility", "control", "track" };

        /// <param name="f">Features of the map with the mods.</param>
        /// <param name="p">Parameters.</param>
        /// <param name="switches">Parts to switch off (explanations).</param>
        /// <param name="trace">Keep the per-note breakdown.</param>
        /// <param name="noMod">Features of the same map in Relax only, for the easy-map mod gain cap (none = no cap).</param>
        public static Mp2Difficulty Evaluate(Mp2Features f, Mp2Parameters p, Mp2Switch switches = Mp2Switch.None, bool trace = false, Mp2Features? noMod = null)
        {
            int n = f.Count;
            if (n == 0)
                return new Mp2Difficulty();

            Mp2NoteTypes types = f.Types;
            double[] localGap = f.LocalGap;

            // ---- lazy cursor
            double lazy = Math.Clamp(p.LazyRadius, 0, 1);
            double reach = (1 - lazy) * 0.5; // how close to the centre the cursor must get, diameters
            var velocity = new Vector2d[n];
            var movement = new Vector2d[n];
            var bits = new double[n];
            var moveTime = new double[n];
            Vector2d cursor = f.Position[0];
            // Positions are in this map's diameters; the screen-space unit is the CS 4 diameter.
            double screen = f.Diameter / REFERENCE_DIAMETER;

            // The previous real move of every note (chord partners are skipped: a 2B chord is one hand position, not a stop).
            var previousMove = new int[n];
            var discrete = new double[n];

            for (int i = 0; i < n; i++)
            {
                Vector2d start = f.FixedStart[i] ?? cursor;
                double need = reach;

                // A 2B chord partner: the player aims where both circles meet, so measure from the first note's centre and let both
                // acceptance areas count (overlapping chords are free).
                if (types.Chord[i] && i > 0)
                {
                    start = f.Position[i - 1];
                    need = 2 * Math.Max(reach, CHORD_REACH);
                }

                Vector2d offset = f.Position[i] - start;
                double distance = offset.Length;
                double move = Math.Max(0, distance - need);
                Vector2d arrival = move > 0 ? start + offset * (move / distance) : types.Chord[i] ? cursor : start;
                movement[i] = move > 0 ? arrival - start : Vector2d.Zero;
                bits[i] = Math.Log2(1 + move * screen);
                cursor = arrival;

                int j = i - 1;
                while (j > 0 && types.Chord[j])
                    j--;
                previousMove[i] = types.Chord[i] ? -1 : j;

                // 0 = the move continues the previous one (flow), 1 = goes back or starts from rest (a discrete move).
                if (types.Chord[i] || movement[i].Length < 1e-6)
                    discrete[i] = 0;
                else if (j < 0 || movement[j].Length < 1e-6)
                    discrete[i] = 1;
                else
                    discrete[i] = (1 - Vector2d.Dot(movement[j], movement[i]) / (movement[j].Length * movement[i].Length)) / 2;

                // Time to get there: from when the cursor is free (at least a share of the local rhythm: a 2B chord or a note
                // inside a slider is not a teleport), plus Relax leniency and the late part of the 300 window. Lateness cannot pile
                // up in flow (every note of a stream is due on time), so the window counts fully only on discrete moves.
                double free = Math.Max(f.Time[i] - f.FreeTime[i], Math.Max(p.ChordFloorMs / 1000, p.ChordShare * localGap[i]));
                double slack = p.WindowSlack * f.Window[i] * (types.Chord[i] ? 1 : p.FlowSlack + (1 - p.FlowSlack) * discrete[i]);
                moveTime[i] = Math.Max(0.001, free + f.Leniency + slack);
                velocity[i] = movement[i] / moveTime[i];
            }

            // ---- note load
            var load = new double[n];
            var parts = new double[n][];
            double[]? readingF = trace ? new double[n] : null, sizeF = trace ? new double[n] : null, repF = trace ? new double[n] : null;
            double[]? typeF = trace ? new double[n] : null, awkF = trace ? new double[n] : null, headF = trace ? new double[n] : null;
            double sizeRatio = Math.Clamp(REFERENCE_DIAMETER / f.Diameter, 0.5, 2);
            double[] typeScale = typeScales(p);

            for (int i = 0; i < n; i++)
            {
                double time = moveTime[i];
                double ballistic = p.BallisticMs / 1000 + bits[i] / Math.Max(1, p.BallisticBitsPerSecond);
                double speed = velocity[i].Length * screen;

                // Sideways part of the turn from the previous real move.
                int previousIndex = previousMove[i];
                Vector2d previous = previousIndex >= 0 ? velocity[previousIndex] : Vector2d.Zero;
                double sine = 0;
                if (previous.Length > 1e-9 && velocity[i].Length > 1e-9)
                {
                    double cos = Math.Clamp(Vector2d.Dot(previous, velocity[i]) / (previous.Length * velocity[i].Length), -1, 1);
                    sine = Math.Sqrt(Math.Max(0, 1 - cos * cos));
                }

                // Fitts aim; a stop-and-go move with time to spare is easy (the hand is at rest at the turn anyway).
                double headroom = (switches & Mp2Switch.NoHeadroom) != 0 ? 1
                    : 1 - Math.Clamp(p.HeadroomDiscount, 0, 0.95) * discrete[i] * Math.Clamp((time - ballistic) / ballistic, 0, 1);
                double a = p.AimWeight * pow(bits[i], p.AimBitsExponent) / time * headroom;

                // Agility: velocity change against the nearest natural motion — keep going (stream) or bounce back (reciprocal
                // jumps, already priced by Fitts). Speed changes and 90° turns count.
                double g = previousIndex >= 0 ? p.AgilityWeight * Math.Min((velocity[i] - previous).Length, (velocity[i] + previous).Length) * screen : 0;

                // Aim control: sideways turns at small spacing inside fast runs (the classifier's run at the map's own pulse, so the
                // rate scales it like every other skill — an absolute time gate made DT explode — and CS does not change it).
                double spacing = movement[i].Length * screen;
                double c = p.ControlWeight * speed * sine * (p.ControlGateClassifier > 0 && types.AimControl.Length == n
                    ? types.AimControl[i]
                    : (1 - Mp2Classifier.ss(spacing, 1.5, 2.5)) * types.FastRun[i]);

                double cap = Math.Max(1e-6, p.TrackCap);
                double t = p.TrackWeight * p.ScaleTrack * cap * Math.Tanh(f.Track[i] * screen / cap);

                // Note type (classifier): the movement parts are scaled by what the note is; awkward jumps / zigzags cost more.
                double[] m = types.Membership[i];
                double scale = 0, awkwardShare = 0;
                for (int k = 0; k < Mp2NoteTypes.COUNT; k++)
                {
                    scale += m[k] * typeScale[k];
                    if (Mp2NoteTypes.IsJump(k) || k == (int)Mp2NoteType.Zigzag || k == (int)Mp2NoteType.Jitter)
                        awkwardShare += m[k];
                }

                if ((switches & Mp2Switch.NoTypeScale) != 0)
                    scale = 1;
                double awkward = (switches & Mp2Switch.NoAwkward) != 0 ? 1 : 1 + p.AwkwardBonus * types.Awkward[i] * awkwardShare;

                // Small circles matter only without time for a corrective sub-movement (an early arrival is free in Relax).
                double u = bits[i] > 0 && spacing >= 0.3 ? 1 - Mp2Classifier.ss(time - ballistic, 0, Math.Max(1e-3, p.CsCorrectionMs / 1000)) : 0;
                if (sizeRatio < 1 && bits[i] > 0)
                    u = p.CsBigFloor + (1 - p.CsBigFloor) * u;
                double size = (switches & Mp2Switch.NoSize) != 0 ? 1 : Math.Pow(sizeRatio, (sizeRatio < 1 ? p.CsBigExponent : p.CsExponent) * u);

                double controlFeature = types.Control.Length == n ? types.Control[i] : 0;
                double movementFactor = scale * awkward * size * (1 + Math.Max(0, p.ControlTypeBonus) * controlFeature);
                a *= movementFactor;
                g *= movementFactor;
                c *= movementFactor;

                double readingRaw = p.ReadingPerNote * Math.Max(0, f.Visible[i] - p.ReadingFreeNotes)
                                    + p.ReactionBonus * Math.Clamp((p.ReactionMs - f.Preempt[i]) / Math.Max(1, p.ReactionMs), 0, 1)
                                    + (f.Hidden ? p.HiddenPerNote * f.Visible[i] : 0)
                                    + (f.Flashlight ? p.FlashlightBonus * Mp2Classifier.ss(spacing, 1.5, 3.5) : 0);
                double reading = (switches & Mp2Switch.NoReading) != 0 || p.ReadingMax <= 0 ? 1 : 1 + p.ReadingMax * Math.Tanh(readingRaw / p.ReadingMax);
                double repetitionFactor = (switches & Mp2Switch.NoRepetition) != 0 ? 1
                    : 1 - Math.Clamp(p.RepetitionDiscount, 0, 1) * repetition(movement, f.Time, i, p.RepetitionTolerance, screen);

                parts[i] = new[] { a, g, c, t };
                load[i] = norm(parts[i], p.SkillNorm) * reading * repetitionFactor;

                if (trace)
                {
                    readingF![i] = reading;
                    sizeF![i] = size;
                    repF![i] = repetitionFactor;
                    typeF![i] = scale;
                    awkF![i] = awkward;
                    headF![i] = headroom;
                }
            }

            // A note is at most LoadCap × the median of its neighbours (4 on each side): a lone 2B chord or teleport is capped,
            // a whole section of hard jumps is not (a map-wide median would cut the jumps of a map that is mostly easy streams).
            var capped = new bool[n];
            if (p.LoadCap > 0 && n > 2 && (switches & Mp2Switch.NoCap) == 0)
            {
                var result = (double[])load.Clone();
                var neighbours = new List<double>(8);

                for (int i = 0; i < n; i++)
                {
                    neighbours.Clear();
                    for (int j = Math.Max(0, i - CAP_NEIGHBOURS); j <= Math.Min(n - 1, i + CAP_NEIGHBOURS); j++)
                    {
                        if (j != i && !types.Chord[j])
                            neighbours.Add(load[j]);
                    }

                    if (neighbours.Count == 0)
                        continue;

                    neighbours.Sort();
                    double median = neighbours[neighbours.Count / 2];
                    if (median > 0 && load[i] > p.LoadCap * median)
                    {
                        result[i] = p.LoadCap * median;
                        capped[i] = true;
                    }
                }

                load = result;
            }

            // ---- moments: sliding window ending at every note; each load occupies its move time and counts by its overlap
            // with the window (no jump when a note crosses the edge), idle time counts as 0.
            double window = Math.Max(0.05, p.MomentSeconds);
            double q = p.MomentPower;
            var moments = new double[n];
            var occupied = new double[n];
            var powered = new double[n];
            for (int i = 0; i < n; i++)
            {
                occupied[i] = Math.Min(moveTime[i], window);
                powered[i] = pow(load[i], q);
            }

            for (int k = 0; k < n; k++)
            {
                double sum = 0, from = f.Time[k] - window;
                for (int i = k; i >= 0 && f.Time[i] > from; i--)
                    sum += powered[i] * Math.Min(occupied[i], f.Time[i] - from);

                moments[k] = pow(sum / window, 1 / q);
            }

            var real = new double[n];
            for (int i = 0; i < n; i++)
                real[i] = Math.Min(window, i == 0 ? occupied[i] : f.Time[i] - f.Time[i - 1]); // a chord partner adds no time

            // ---- control stamina (Mp2Parameters.ControlStaminaBonus): a moment grows with the share of the last span spent on
            // control at least as intense as it. m_k · g_k is non-decreasing in m_k (lighter neighbours add a constant) and in every
            // other moment, and degree 0 in the loads.
            var stamina = new double[n];
            Array.Fill(stamina, 1.0);
            if (p.ControlStaminaBonus > 0 && (switches & Mp2Switch.NoStamina) == 0)
            {
                var controlShare = new double[n];
                for (int i = 0; i < n; i++)
                    controlShare[i] = skillSplit(parts[i], p.SkillNorm)[2];

                double span = Math.Max(1, p.ControlStaminaSeconds), recovery = Math.Clamp(p.ControlStaminaRecovery, 0, 0.95);
                for (int k = 0; k < n; k++)
                {
                    if (moments[k] <= 0)
                        continue;

                    double duty = 0, from = f.Time[k] - span;
                    for (int j = k; j >= 0 && f.Time[j] > from; j--)
                        duty += real[j] * controlShare[j] * Math.Min(1, moments[j] / moments[k]);

                    stamina[k] = 1 + p.ControlStaminaBonus * Math.Max(0, duty / span - recovery) / (1 - recovery);
                }

                // Easy control does not tire a top player: only maps hard without it get it (SS PP before stamina, as the RP gate).
                double gate = Mp2Classifier.ss(Math.Log(Math.Max(1e-9, PpOfDifficulty(difficultyOf(moments, real, p, switches), p))),
                    Math.Log(Math.Max(1, p.ControlStaminaEasyPp)), Math.Log(Math.Max(p.ControlStaminaEasyPp + 1, p.ControlStaminaHardPp)));
                for (int k = 0; k < n; k++)
                {
                    stamina[k] = 1 + (stamina[k] - 1) * gate;
                    moments[k] *= stamina[k];
                }
            }

            // ---- level (hardest PeakSeconds), hard time, D — real time (a rate edit = the same rate mod)

            // Level: the hardest PeakSeconds of moments (not necessarily contiguous: the owner's targets reward maps with sharp
            // hard moments; a contiguous window fitted worse — Sendan Life, Hills Have Eyes, Flamewall). A looped hardest section
            // therefore counts its hardest moments more than once (long-range repetition is open, MOSUPP2_DESIGN.md).
            double peak = Math.Max(0.05, p.PeakSeconds), covered = 0, levelSum = 0;
            var take = new double[n];
            foreach (int i in Enumerable.Range(0, n).OrderByDescending(i => moments[i]))
            {
                take[i] = Math.Min(real[i], peak - covered);
                levelSum += moments[i] * take[i];
                covered += take[i];
                if (covered >= peak)
                    break;
            }

            double level = covered > 0 ? levelSum / covered : 0;
            double hardSeconds = 0;
            if (level > 0)
            {
                for (int i = 0; i < n; i++)
                    hardSeconds += real[i] * pow(moments[i] / level, p.HardPower);
            }

            // λ · HardPower ≤ 1, or making a hard part harder could lower D (it raises the level and shrinks the hard time).
            double lambda = (switches & Mp2Switch.NoLength) != 0 ? 0 : Math.Min(p.LengthExponent, 1 / Math.Max(1, p.HardPower));
            double lengthSeconds = Math.Max(1, p.LengthSeconds);
            double lengthFactor = Math.Pow(1 + hardSeconds / lengthSeconds, lambda);
            double shortFactor = Math.Pow(Math.Min(1, covered / peak), Math.Max(0, p.ShortExponent));
            double difficulty = level * lengthFactor * shortFactor;
            double hardNotes = level > 0 ? moments.Sum(m => Math.Min(1, pow(m / level, 2))) : 0;

            // ---- exact attribution: D is homogeneous of degree 1 in the moments and every moment in its note loads, so
            // D = Σ_k m_k ∂D/∂m_k (Euler) splits D over moments, then each moment over the notes in its window by load^q · overlap.
            double beta = hardSeconds > 0 ? p.HardPower * lambda * hardSeconds / (lengthSeconds + hardSeconds) : 0;
            var contribution = new double[n];
            var skillOfNote = new double[n][];
            var skillPp = new double[SKILLS.Length];

            if (difficulty > 0)
            {
                for (int k = 0; k < n; k++)
                {
                    double lambdaK = covered > 0 ? moments[k] * take[k] / (covered * level) : 0;
                    double etaK = hardSeconds > 0 ? real[k] * pow(moments[k] / level, p.HardPower) / hardSeconds : 0;
                    double momentShare = difficulty * ((1 - beta) * lambdaK + beta * etaK);
                    if (momentShare == 0)
                        continue;

                    double sum = 0, from = f.Time[k] - window;
                    for (int i = k; i >= 0 && f.Time[i] > from; i--)
                        sum += powered[i] * Math.Min(occupied[i], f.Time[i] - from);
                    if (sum <= 0)
                        continue;

                    for (int i = k; i >= 0 && f.Time[i] > from; i--)
                        contribution[i] += momentShare * powered[i] * Math.Min(occupied[i], f.Time[i] - from) / sum;
                }

                double total = 0;
                for (int i = 0; i < n; i++)
                {
                    skillOfNote[i] = skillSplit(parts[i], p.SkillNorm);
                    for (int s = 0; s < SKILLS.Length; s++)
                        skillPp[s] += contribution[i] * skillOfNote[i][s];
                    total += contribution[i];
                }

                for (int s = 0; s < SKILLS.Length; s++)
                    skillPp[s] = total != 0 ? skillPp[s] / total : 0;
            }

            double uncapped = PpOfDifficulty(difficulty, p), ppSs = uncapped, noModPpSs = 0;
            if (noMod != null && !ReferenceEquals(noMod, f))
            {
                noModPpSs = Evaluate(noMod, p, switches).PpSs;
                ppSs = CapModGain(uncapped, noModPpSs, p);
            }

            var skillShares = new Dictionary<string, double>();
            for (int s = 0; s < SKILLS.Length; s++)
                skillShares[SKILLS[s]] = skillPp[s];

            return new Mp2Difficulty
            {
                Difficulty = difficulty,
                Level = level,
                HardSeconds = hardSeconds,
                PpSs = ppSs,
                NoModPpSs = noModPpSs,
                ModCapLoss = uncapped - ppSs,
                Moments = moments,
                HardNotes = hardNotes,
                Notes = n,
                SkillShares = skillShares,
                Trace = trace
                    ? new Mp2Trace
                    {
                        Load = load, Skill = skillOfNote, Contribution = contribution, Capped = capped, Types = types,
                        Reading = readingF!, Size = sizeF!, Repetition = repF!, TypeScale = typeF!, Awkward = awkF!, Headroom = headF!,
                        LengthFactor = lengthFactor, ShortFactor = shortFactor, Stamina = stamina,
                    }
                    : null,
            };
        }

        /// <summary>Load of every note as a part of the miss penalty:
        /// min(1, (moment / level)^2) — the same convention as <see cref="Mp2Difficulty.HardNotes"/> (its sum).
        /// A miss on a hard moment (load ~ level) costs a full miss, on a weak spot only its share.</summary>
        public static double[] NoteLoads(Mp2Difficulty d)
        {
            var loads = new double[d.Moments.Length];
            if (d.Level <= 0 || d.Moments.Length == 0)
                return loads;

            for (int i = 0; i < loads.Length; i++)
                loads[i] = Math.Min(1, pow(d.Moments[i] / d.Level, 2));
            return loads;
        }

        /// <summary>missLoad of a score for <see cref="Performance"/>: the load of the missed notes (Σ over
        /// <paramref name="missed"/> of min(1, (moment / level)^MissExponent)).</summary>
        /// <param name="d">The map's SS result.</param>
        /// <param name="missed">Per note, in the order of <see cref="Mp2Difficulty.Moments"/>: true = the note was missed.</param>
        /// <remarks>A degenerate map (level 0, no hard notes) has no load language; its misses count flat.</remarks>
        public static double MissLoad(Mp2Difficulty d, bool[] missed)
        {
            if (d.Level <= 0)
                return missed.Length > 0 && missed.Any(m => m) ? missed.Count(m => m) : 0;

            double sum = 0;
            int count = Math.Min(missed.Length, d.Moments.Length);
            for (int i = 0; i < count; i++)
            {
                if (missed[i])
                    sum += Math.Min(1, pow(d.Moments[i] / d.Level, 2));
            }
            // Suspect data (more missed notes than the map has notes): the rest count flat.
            for (int i = d.Moments.Length; i < missed.Length; i++)
            {
                if (missed[i])
                    sum += 1;
            }
            return sum;
        }

        /// <summary>D of moments (level, hard time, length and short-map factors) without the attribution.</summary>
        private static double difficultyOf(double[] moments, double[] real, Mp2Parameters p, Mp2Switch switches)
        {
            int n = moments.Length;
            double peak = Math.Max(0.05, p.PeakSeconds), covered = 0, levelSum = 0;
            foreach (int i in Enumerable.Range(0, n).OrderByDescending(i => moments[i]))
            {
                double take = Math.Min(real[i], peak - covered);
                levelSum += moments[i] * take;
                covered += take;
                if (covered >= peak)
                    break;
            }

            double level = covered > 0 ? levelSum / covered : 0;
            if (level <= 0)
                return 0;

            double hardSeconds = 0;
            for (int i = 0; i < n; i++)
                hardSeconds += real[i] * pow(moments[i] / level, p.HardPower);

            double lambda = (switches & Mp2Switch.NoLength) != 0 ? 0 : Math.Min(p.LengthExponent, 1 / Math.Max(1, p.HardPower));
            return level * Math.Pow(1 + hardSeconds / Math.Max(1, p.LengthSeconds), lambda) * Math.Pow(Math.Min(1, covered / peak), Math.Max(0, p.ShortExponent));
        }

        /// <param name="d">The map's SS result.</param>
        /// <param name="p">Parameters.</param>
        /// <param name="accuracy">0–1, osu! accuracy of the score.</param>
        /// <param name="misses">Misses of the score.</param>
        /// <param name="missLoad">Load of the missed notes: Σ over the missed notes of min(1, (moment / level)^MissExponent). A miss on a hard moment (load ~ level) costs a full miss, on a weak spot only its share, so easy-spot misses are shielded. Defaults to the flat miss count.</param>
        public static double Performance(Mp2Difficulty d, Mp2Parameters p, double accuracy, int misses, double? missLoad = null)
        {
            double easy = p.AccuracyEasyPp > 0 ? 1 - Mp2Classifier.ss(d.PpSs, p.AccuracyEasyPp, 4 * p.AccuracyEasyPp) : 0;
            double accuracyFactor = pow(Math.Clamp(accuracy, 0, 1), p.AccuracyExponent + p.AccuracyEasyBonus * easy);
            double hard = Math.Max(1, d.HardNotes);
            double effectiveMissLoad = missLoad ?? Math.Max(0, misses);
            double missFactor = pow(hard / (hard + effectiveMissLoad), p.MissExponent);
            return d.PpSs * accuracyFactor * missFactor;
        }

        /// <summary>SS PP of a difficulty: log-quadratic around PpReference, slope floored at PpMinSlope.</summary>
        public static double PpOfDifficulty(double difficulty, Mp2Parameters p)
        {
            if (difficulty <= 0)
                return 0;

            double x = Math.Log(difficulty / Math.Max(1e-9, p.PpReference));
            double slope = p.PpExponent + 2 * p.PpCurvature * x;
            if (p.PpCurvature != 0 && slope < p.PpMinSlope)
            {
                // Below (above, for a negative curvature) the point where the slope reaches the floor, continue linearly in log.
                double x0 = (p.PpMinSlope - p.PpExponent) / (2 * p.PpCurvature);
                return p.PpScale * Math.Exp(p.PpExponent * x0 + p.PpCurvature * x0 * x0 + p.PpMinSlope * (x - x0));
            }

            return p.PpScale * Math.Exp(p.PpExponent * x + p.PpCurvature * x * x);
        }

        /// <summary>SS PP with mods after the easy-map mod gain cap (<see cref="Mp2Parameters.ModCapEasy"/>); <paramref name="noModPpSs"/>
        /// is the SS PP of the same map in Relax only. Monotone in the PP with mods; gains below 1 (big circles) are not touched.</summary>
        public static double CapModGain(double ppSs, double noModPpSs, Mp2Parameters p)
        {
            if (noModPpSs <= 0 || ppSs <= noModPpSs || p.ModCapEasy <= 1)
                return ppSs;

            double gain = ppSs / noModPpSs;
            double room = p.ModCapEasy - 1;
            double capped = 1 + room * Math.Tanh((gain - 1) / room);
            // Only results that stay easy are capped: a map built for DT (easy in NM, hard with DT) keeps its rate gain.
            double hard = Mp2Classifier.ss(Math.Log(ppSs), Math.Log(Math.Max(1, p.ModCapEasyPp)), Math.Log(Math.Max(p.ModCapEasyPp + 1, p.ModCapHardPp)));
            // ... and only easy maps: a map with real difficulty in Relax only keeps its mod gain.
            double hardMap = Mp2Classifier.ss(Math.Log(noModPpSs), Math.Log(Math.Max(1, p.ModCapNoModEasyPp)), Math.Log(Math.Max(p.ModCapNoModEasyPp + 1, p.ModCapNoModHardPp)));
            double free = 1 - (1 - hard) * (1 - hardMap);
            return noModPpSs * (capped + free * (gain - capped));
        }

        /// <summary>d ln PP / d ln D at a difficulty (how much a 1% harder map is worth).</summary>
        public static double PpSlope(double difficulty, Mp2Parameters p)
        {
            if (difficulty <= 0)
                return p.PpMinSlope;

            double x = Math.Log(difficulty / Math.Max(1e-9, p.PpReference));
            return Math.Max(p.PpCurvature != 0 ? p.PpMinSlope : double.NegativeInfinity, p.PpExponent + 2 * p.PpCurvature * x);
        }

        /// <summary>Type scale per <see cref="Mp2NoteType"/> (free notes keep 1: they have no movement anyway).</summary>
        private static double[] typeScales(Mp2Parameters p)
        {
            var scale = new double[Mp2NoteTypes.COUNT];
            scale[(int)Mp2NoteType.Free] = 1;
            scale[(int)Mp2NoteType.Simple] = p.ScaleSimple;
            scale[(int)Mp2NoteType.Stream] = p.ScaleStream;
            scale[(int)Mp2NoteType.SpacedStream] = p.ScaleSpacedStream;
            scale[(int)Mp2NoteType.Burst] = p.ScaleBurst;
            scale[(int)Mp2NoteType.Zigzag] = p.ScaleZigzag;
            scale[(int)Mp2NoteType.Jitter] = p.ScaleJitter;
            scale[(int)Mp2NoteType.JumpBackForth] = p.ScaleJumpBackForth;
            scale[(int)Mp2NoteType.JumpAngular] = p.ScaleJumpAngular;
            scale[(int)Mp2NoteType.JumpFlow] = p.ScaleJumpFlow;
            return scale;
        }

        /// <summary>1 when the movement into note i equals one of the <see cref="REPETITION_LOOKBACK"/> before it (vector and rhythm:
        /// the gap since the previous note), falling off smoothly with the difference in both.</summary>
        private static double repetition(Vector2d[] movement, double[] time, int i, double tolerance, double screen)
        {
            double best = 0;
            // In screen units (CS 4 diameters), so the circle size does not change what counts as the same move.
            double scale = tolerance * (movement[i].Length * screen + 0.5);

            for (int k = 2; k <= REPETITION_LOOKBACK && i - k >= 0; k++)
            {
                double gap = i > 0 ? time[i] - time[i - 1] : 0, gapBefore = i - k > 0 ? time[i - k] - time[i - k - 1] : 0;
                double timing = Math.Abs(gap - gapBefore) / Math.Max(1e-3, Math.Max(gap, gapBefore)) / 0.1;
                double difference = (movement[i] - movement[i - k]).Length * screen / Math.Max(1e-6, scale);
                best = Math.Max(best, Math.Exp(-difference * difference - timing * timing));
            }

            return best;
        }

        private static double norm(double[] parts, double q)
        {
            if (q <= 1)
                return parts.Sum();

            double sum = 0;
            foreach (double part in parts)
                sum += pow(part, q);
            return pow(sum, 1 / q);
        }

        /// <summary>Euler split of the norm: part_s · ∂N/∂part_s / N = part_s^q / Σ part^q.</summary>
        private static double[] skillSplit(double[] parts, double q)
        {
            var split = new double[parts.Length];
            double sum = 0;
            for (int s = 0; s < parts.Length; s++)
                sum += split[s] = q <= 1 ? parts[s] : pow(parts[s], q);
            for (int s = 0; s < parts.Length; s++)
                split[s] = sum > 0 ? split[s] / sum : 0;
            return split;
        }

        private static double pow(double value, double exponent) => value <= 0 ? 0 : Math.Pow(value, exponent);
    }
}
