// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Visible Notes Reading (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): the more spaced notes are on
    /// screen at once, the harder the map is to read — lower AR and higher BPM both put more notes on screen.
    /// </summary>
    /// <remarks>
    /// For every note: the notes after it that have already appeared (start time within the approach time of the effective AR),
    /// each weighted by smoothstep(spacing to its previous note, 0.5, 1.5 circle diameters), so stacks and tight streams
    /// (one line, easy to read) count little. Map value = average over the 20% most cluttered notes.
    /// Normal jump maps at AR 10 have 4–5 notes on screen. Total × (1 + 0.20 · smoothstep(visible notes, 5, 9)).
    /// Measured in map time with the effective AR, so DT / NC / HT without lock change nothing (AR rises with the rate),
    /// while AR/OD lock, DA AR and HR / EZ do. Fades out below AR 8 → 7 (effective AR), where the Realistik low-AR reading bonus
    /// takes over, so low AR is not paid twice.
    /// Examples (SS): ganbare!kumokosan (/b/2833124) DT lock 7.9 notes; Attack (/b/5637274) DT lock 6.7, DA AR 8 7.4;
    /// stream maps ≈ 1.5–2.5 (no bonus).
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxVisibleNotesReading
    {
        public const double BONUS_START = 5;
        public const double BONUS_FULL = 9;
        public const double MAX_BONUS = 0.20;

        public const double SPACING_START = 0.5; // circle diameters
        public const double SPACING_FULL = 1.5;
        public const double TOP_SHARE = 0.20;

        public const double LOW_AR_FADE_START = 8; // full rule from this effective AR
        public const double LOW_AR_FADE_END = 7; // no rule below

        public static double Multiplier(RxBeatmap beatmap, float effectiveAr)
        {
            double fade = smoothStep(effectiveAr, LOW_AR_FADE_END, LOW_AR_FADE_START);
            if (fade <= 0)
                return 1;

            return 1 + MAX_BONUS * fade * smoothStep(VisibleNotes(beatmap, effectiveAr), BONUS_START, BONUS_FULL);
        }

        /// <summary>Average spaced notes on screen over the <see cref="TOP_SHARE"/> most cluttered notes.</summary>
        public static double VisibleNotes(RxBeatmap beatmap, float effectiveAr)
        {
            List<RxHitObject> notes = beatmap.HitObjects.Where(o => o.Kind != RxHitObjectKind.Spinner).ToList();
            if (notes.Count < 2)
                return 0;

            double preempt = effectiveAr < 5 ? 1800 - 120 * effectiveAr : 1950 - 150 * effectiveAr;
            double diameter = 2 * (54.4 - 4.48 * beatmap.CircleSize);

            double[] weight = new double[notes.Count];
            for (int i = 1; i < notes.Count; i++)
            {
                double dx = notes[i].Position.X - notes[i - 1].Position.X;
                double dy = notes[i].Position.Y - notes[i - 1].Position.Y;
                weight[i] = smoothStep(Math.Sqrt(dx * dx + dy * dy) / diameter, SPACING_START, SPACING_FULL);
            }

            double[] visible = new double[notes.Count];
            for (int i = 0; i < notes.Count; i++)
            {
                for (int j = i + 1; j < notes.Count && notes[j].StartTime - notes[i].StartTime < preempt; j++)
                    visible[i] += weight[j];
            }

            int take = Math.Max(1, (int)(notes.Count * TOP_SHARE));
            return visible.OrderByDescending(v => v).Take(take).Average();
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
