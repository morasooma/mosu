// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPp2
{
    /// <summary>
    /// Every number of the MosuPp 2 model (see MOSUPP2_DESIGN.md). They are fitted to the players' ratings; the defaults are
    /// physically reasonable starting values with the PP scale set from the current system.
    /// </summary>
    public sealed class Mp2Parameters
    {
        // Declared first: the static members below use it (static fields initialise in text order).
        private static readonly PropertyInfo[] properties = typeof(Mp2Parameters).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                                                 .Where(p => p.PropertyType == typeof(double) && p.CanWrite)
                                                                                 .ToArray();

        // ---- the cursor

        /// <summary>The cursor only needs to get this far into a circle (share of the radius from its edge: 0 = touch the edge,
        /// 1 = reach the centre). Relax players cut corners: overlapped streams and tiny back-and-forth cost little.</summary>
        public double LazyRadius { get; set; } = 0.5;

        /// <summary>Share of the 300 hit window a late arrival may use and still be a 300 (OD as timing slack).</summary>
        public double WindowSlack { get; set; } = 0.17;

        /// <summary>Share of the window slack usable in flow (streams: lateness cannot pile up, every note is due on time); discrete
        /// moves (reversals, moves from rest) get all of it.</summary>
        public double FlowSlack { get; set; } = 0.15;

        /// <summary>Fastest ballistic move a top player needs: BallisticMs + ID / BallisticBitsPerSecond (Fitts in movement time).</summary>
        public double BallisticMs { get; set; } = 30;

        public double BallisticBitsPerSecond { get; set; } = 35;

        /// <summary>Spare time on a stop-and-go move makes the aim easier, at most by this share (debate consensus 24.10: 25%):
        /// aim × (1 − HeadroomDiscount · turn · clamp((T − T_ballistic) / T_ballistic, 0, 1)) — nothing up to the ballistic time,
        /// the full discount from twice the ballistic time. Streams (no turn) are not discounted.</summary>
        public double HeadroomDiscount { get; set; } = 0.25;

        /// <summary>Circle size effect: load × (CS 4 diameter / diameter)^(CsExponent · u), u = 1 when there is no spare time to
        /// correct the aim, 0 from CsCorrectionMs of spare time. Small circles are hard only on hard movements (CS gain pays on
        /// hard maps, not on easy ones); big circles make hard movements easier the same way (<see cref="CsBigExponent"/>). The
        /// size ratio is kept in 0.5–2.</summary>
        public double CsExponent { get; set; } = 1.023;

        /// <summary>Share of the big-circle effect that applies even with time to correct (a big circle forgives every move a
        /// little; debate consensus: CS 2 ≈ ×0.95 on easy maps, ≈ ×0.8 without time to correct). Small circles get no such floor:
        /// CS 8 must not pay on easy maps.</summary>
        public double CsBigFloor { get; set; } = 0.357;

        /// <summary>The same for circles bigger than CS 4 (owner: CS 2 / EZ ≈ −45% on hard maps, while CS 8 must not farm, so the
        /// two sides differ).</summary>
        public double CsBigExponent { get; set; } = 1.086;

        /// <summary>Spare time (real ms beyond the ballistic minimum) from which the circle size no longer matters.</summary>
        public double CsCorrectionMs { get; set; } = 26;

        // ---- skills (per note)

        /// <summary>Weight of the Fitts aim demand (bits per second, distances in CS 4 diameters). Fixed at 1: the PP scale is
        /// <see cref="PpScale"/>.</summary>
        public double AimWeight { get; set; } = 1;

        /// <summary>Aim = bits^this / time. Above 1, distance (rate-independent) weighs more than in pure Fitts, so wide jumps
        /// separate from comfortable ones without changing how the rate scales.</summary>
        public double AimBitsExponent { get; set; } = 1;

        /// <summary>Weight of the agility demand: change of cursor velocity at the note, in CS 4 diameters per second (screen
        /// space: the hand accelerates the same whatever the circle size; rate¹ like aim).</summary>
        public double AgilityWeight { get; set; } = 0.553;

        /// <summary>Weight of the slider tracking demand: lazy cursor path inside the follow circle, diameters per second.</summary>
        public double TrackWeight { get; set; } = 0.488;

        /// <summary>Tracking saturates at this speed (CS 4 diameters/s): c · tanh(track / c). Aspire-style sliders do not explode.</summary>
        public double TrackCap { get; set; } = 12;

        /// <summary>Weight of aim control: fast sideways turns at small spacing (|v| · sin θ, CS 4 diameters/s; zigzags, cuts,
        /// tight flow that is neither a reversal nor straight), on notes the classifier puts in fast runs (zigzag, jitter, burst).</summary>
        public double ControlWeight { get; set; } = 1.349;

        /// <summary>Extra movement load on control notes (<see cref="Mp2NoteTypes.Control"/>: aim control + awkward-angle jumps,
        /// 0–1): load × (1 + ControlTypeBonus · control).</summary>
        public double ControlTypeBonus { get; set; }

        /// <summary>1 = aim control counts on the classifier's aim-control notes (<see cref="Mp2NoteTypes.AimControl"/>: zigzag +
        /// jitter); 0 = the old gate (fast run × spacing below 1.5–2.5 diameters, which cut the zigzags of Mor 3 / UMMU).</summary>
        public double ControlGateClassifier { get; set; }

        /// <summary>Load discount when a movement repeats one of the 2–4 before it (same vector and timing): back-and-forth,
        /// triangles, squares and straight streams are played from muscle memory.</summary>
        public double RepetitionDiscount { get; set; } = 0.35;

        /// <summary>How different two movements may be (share of their length + half a diameter) and still count as a repeat.</summary>
        public double RepetitionTolerance { get; set; } = 0.25;

        /// <summary>Norm that combines the skills of one note (1 = sum, large = the hardest skill only).</summary>
        public double SkillNorm { get; set; } = 2;

        /// <summary>A note's load is at most this many times the median load of its neighbours (2B chords and teleports do not explode).</summary>
        public double LoadCap { get; set; } = 2;

        /// <summary>A movement gets at least this share of the local rhythm (median note gap of ±4 notes) and at least
        /// ChordFloorMs: whole 2B chord sections cost at most a 1/4 at their rhythm.</summary>
        public double ChordShare { get; set; } = 0.25;

        public double ChordFloorMs { get; set; } = 40;

        // ---- map types (Mp2Classifier): the movement load of a note × Σ type share × scale

        /// <summary>Owner balance rule (2026-11): aim slop a little easier, streams cheaper. Calibrated on the owner's
        /// 43 stream/slop cells: 0.80 on the stream types gives −28.7% PP on stream maps (the first 0.83 gave −23.2%),
        /// 1.02 on the jump types gives +5.1% on slop.</summary>
        public double ScaleSimple { get; set; } = 1;
        public double ScaleStream { get; set; } = 0.80;
        public double ScaleSpacedStream { get; set; } = 0.80;
        public double ScaleBurst { get; set; } = 1;
        public double ScaleZigzag { get; set; } = 1;
        public double ScaleJitter { get; set; } = 1;
        public double ScaleJumpBackForth { get; set; } = 1.02;
        public double ScaleJumpAngular { get; set; } = 1.02;
        public double ScaleJumpFlow { get; set; } = 1.02;

        /// <summary>Scale of the slider tracking part.</summary>
        public double ScaleTrack { get; set; } = 1;

        /// <summary>Jumps and aim control whose angle, spacing and rhythm keep changing (awkward) are harder: × (1 + this · awkwardness).</summary>
        public double AwkwardBonus { get; set; } = 0.215;

        // ---- reading (multiplier on the note load, saturating at 1 + ReadingMax)

        public double ReadingMax { get; set; } = 0.5;

        /// <summary>Extra load per upcoming spaced note on screen beyond <see cref="ReadingFreeNotes"/>.</summary>
        public double ReadingPerNote { get; set; } = 0.018;

        public double ReadingFreeNotes { get; set; } = 4;

        /// <summary>Extra load when the approach is shorter than <see cref="ReactionMs"/> (AR 10.3+ after mods).</summary>
        public double ReactionBonus { get; set; } = 0.262;

        public double ReactionMs { get; set; } = 642.3;

        /// <summary>Hidden: extra load per visible note (no approach circles, notes fade).</summary>
        public double HiddenPerNote { get; set; } = 0.02;

        /// <summary>Flashlight: extra load on moves that leave the light (spacing 1.5 → 3.5 CS 4 diameters; the next stream note is always lit).</summary>
        public double FlashlightBonus { get; set; } = 0.2;

        // ---- moments and the map

        /// <summary>A moment is the window of this many real seconds ending at a note; its load is the power mean below.</summary>
        public double MomentSeconds { get; set; } = 1;

        public double MomentPower { get; set; } = 2;

        /// <summary>The map's level: mean load of its hardest moments covering this many seconds of map time (so DT does not pull
        /// easier parts into the peak).</summary>
        public double PeakSeconds { get; set; } = 10;

        /// <summary>Hard time: Σ moment time · (moment / level)^this. The higher, the less easy parts count.</summary>
        public double HardPower { get; set; } = 4;

        /// <summary>D = level · (1 + hard time / LengthSeconds)^LengthExponent: length and endurance, separate from the level. The
        /// exponent is used at most 1 / HardPower, so making a hard part harder never lowers D.</summary>
        public double LengthSeconds { get; set; } = 30;

        public double LengthExponent { get; set; } = 0.035;

        /// <summary>Control stamina: sustained aim control without rest tires the hand (owner: Mor 3 = "7 минут безприрывного аим
        /// контроля"). Duty of a moment = share of the last <see cref="ControlStaminaSeconds"/> (real time) spent on control at
        /// least as intense as the moment (Σ real time · control share · min(1, moment_j / moment_k) / span). Above the share
        /// the hand recovers from (<see cref="ControlStaminaRecovery"/>) the moment grows:
        /// moment × (1 + ControlStaminaBonus · (duty − recovery) / (1 − recovery)). Degree 0 in the loads (the scale of the map does
        /// not change it) and non-decreasing in every load, so a harder part never lowers D; at most × (1 + bonus).</summary>
        public double ControlStaminaBonus { get; set; } = 0.8;

        public double ControlStaminaSeconds { get; set; } = 60;

        public double ControlStaminaRecovery { get; set; } = 0.15;

        /// <summary>Easy control does not tire a top player: the stamina fades in with the SS PP before it, from ControlStaminaEasyPp
        /// to ControlStaminaHardPp (as the owner's RP gate, 300–600). An easy 10-minute zigzag map would otherwise gain up to ×2.</summary>
        public double ControlStaminaEasyPp { get; set; } = 300;

        public double ControlStaminaHardPp { get; set; } = 600;

        /// <summary>Maps shorter than PeakSeconds: D × (covered / PeakSeconds)^this (a few notes are not a map).</summary>
        public double ShortExponent { get; set; } = 0.5;

        /// <summary>PP_SS = PpScale · exp(PpExponent · x + PpCurvature · x²), x = ln(D / PpReference): PpScale is the SS PP of a
        /// map with D = PpReference, PpExponent the slope there. The curvature makes the slope grow with D, so rate and length
        /// pay more on hard maps than on easy ones (debate consensus: DT ≈ ×1.8 on easy maps, ×2.3–2.7 on hard ones). The slope
        /// never drops below <see cref="PpMinSlope"/> (PP always grows with D).</summary>
        public double PpScale { get; set; } = 222.5;

        public double PpExponent { get; set; } = 2.719;

        public double PpCurvature { get; set; } = 0.027;

        public double PpReference { get; set; } = 15;

        public double PpMinSlope { get; set; } = 1;

        /// <summary>Mod gain cap on easy maps (debate consensus: easy DT ≈ ×1.8; if HR × DT × CS on an easy map is worth more than ×2.0 of NM,
        /// cap it smoothly). Gain g = SS PP with the mods / SS PP of the same map in Relax only; above 1 it becomes
        /// 1 + (ModCapEasy − 1) · tanh((g − 1) / (ModCapEasy − 1)) while the uncapped SS PP with the mods is up to ModCapEasyPp,
        /// fading out by ModCapHardPp (a map built for DT, easy in NM and hard with DT, keeps its gain). Physics alone cannot
        /// give it: a rate mod moves every map up the same scale, while the owner's targets spread easy and hard maps ×18.</summary>
        public double ModCapEasy { get; set; } = 1.8;

        public double ModCapEasyPp { get; set; } = 500;

        public double ModCapHardPp { get; set; } = 1500;

        /// <summary>The cap is for easy maps: it fades out with the SS PP of the map in Relax only, from ModCapNoModEasyPp to
        /// ModCapNoModHardPp (the easy set and the consensus maps are 90–210 PP; LATHI, 300 PP in NM and built for DT, lost 17% of
        /// its DT to the cap).</summary>
        public double ModCapNoModEasyPp { get; set; } = 210;

        public double ModCapNoModHardPp { get; set; } = 330;

        // ---- score

        /// <summary>PP × accuracy^this (RX: hundreds are late arrivals).</summary>
        public double AccuracyExponent { get; set; } = 4;

        /// <summary>Easy maps weigh accuracy more: the exponent grows by this below AccuracyEasyPp of SS PP and fades out by
        /// 4 × AccuracyEasyPp (debate consensus: acc^4 everywhere, up to acc^4.5 on easy maps).</summary>
        public double AccuracyEasyBonus { get; set; } = 0.5;

        public double AccuracyEasyPp { get; set; } = 150;

        /// <summary>PP × (hard notes / (hard notes + misses))^this. Not learnable from ratings (they have no misses): set by hand.</summary>
        public double MissExponent { get; set; } = 2;

        public static Mp2Parameters Default { get; } = new Mp2Parameters();

        /// <summary>Names of the fitted numbers, in vector order.</summary>
        public static IReadOnlyList<string> Names { get; } = properties.Select(p => p.Name).ToArray();

        public double[] ToVector() => properties.Select(p => (double)p.GetValue(this)!).ToArray();

        public static Mp2Parameters FromVector(IReadOnlyList<double> values)
        {
            if (values.Count != properties.Length)
                throw new ArgumentException($"Expected {properties.Length} values.");

            var result = new Mp2Parameters();
            for (int i = 0; i < properties.Length; i++)
                properties[i].SetValue(result, values[i]);
            return result;
        }

        public Mp2Parameters Clone() => FromVector(ToVector());

        public string ToJson() => JsonSerializer.Serialize(properties.ToDictionary(p => p.Name, p => p.GetValue(this)), new JsonSerializerOptions { WriteIndented = true });

        /// <summary>Parameters from JSON; missing names keep their defaults, unknown names are an error.</summary>
        public static Mp2Parameters FromJson(string json)
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new Dictionary<string, double>();
            var result = new Mp2Parameters();

            foreach (var (name, value) in values)
            {
                var property = properties.FirstOrDefault(p => p.Name == name) ?? throw new ArgumentException($"Unknown MosuPp 2 parameter '{name}'.");
                if (!double.IsFinite(value))
                    throw new ArgumentException($"MosuPp 2 parameter '{name}' is not a finite number.");

                property.SetValue(result, value);
            }

            return result;
        }

    }
}
