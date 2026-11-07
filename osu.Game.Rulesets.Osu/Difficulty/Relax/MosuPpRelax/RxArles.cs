// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// Content-based Arles detection shared by MosuPp RX and its lab. Wide jumps mixed with bursts require changes of
    /// cursor speed and direction. Names, authors, map IDs and score accuracy are deliberately absent from the features.
    /// Thresholds are calibrated on the ten labelled maps in tools/MosuPpLab/dataset.txt; they are not a trained ML model.
    /// </summary>
    public static class RxArles
    {
        public const double CLASSIFICATION_THRESHOLD = 0.6;
        public const double MAX_BONUS = 0.30;

        public readonly record struct Analysis(double Confidence, double WideShare, double SlowWideShare, double VariableTurnShare)
        {
            public bool IsArles => Confidence >= CLASSIFICATION_THRESHOLD;
            // No discontinuity at classification: weak evidence gets no PP, full evidence gets at most +30%.
            public double Multiplier => 1 + MosuPpBalance.Current.ArlesBonus * smoothStep(Confidence, CLASSIFICATION_THRESHOLD, 0.85);
        }

        /// <param name="circleSize">CS to rate the map at instead of the played one (CS Gain reference; only the circle diameter changes).</param>
        public static Analysis Analyze(IBeatmap beatmap, float? circleSize = null)
        {
            var notes = beatmap.HitObjects.OfType<OsuHitObject>().Where(o => o is not Spinner).ToList();
            if (notes.Count < 3)
                return default;

            double diameter = Math.Max(1, circleSize is float referenceCs ? RxCsGain.Diameter(referenceCs) : notes[0].Radius * 2);
            var gaps = new double[notes.Count];
            var spacing = new double[notes.Count];
            var angles = Enumerable.Repeat(double.NaN, notes.Count).ToArray();
            var dense = new bool[notes.Count];
            var fast = new bool[notes.Count];
            var inStream = new bool[notes.Count];

            for (int i = 1; i < notes.Count; i++)
            {
                gaps[i] = notes[i].StartTime - notes[i - 1].StartTime;
                Vector2 from = notes[i - 1] is Slider s ? s.StackedEndPosition : notes[i - 1].StackedPosition;
                spacing[i] = Vector2.Distance(from, notes[i].StackedPosition) / diameter;
                fast[i] = gaps[i] > 0 && gaps[i] <= 125 && spacing[i] >= 0.2;
                dense[i] = gaps[i] > 0 && gaps[i] <= 95 && spacing[i] >= 0.2 && spacing[i] <= 2.5 && notes[i - 1] is not Slider;

                if (i + 1 >= notes.Count)
                    continue;

                Vector2 outFrom = notes[i] is Slider cs ? cs.StackedEndPosition : notes[i].StackedPosition;
                Vector2 incoming = from - notes[i].StackedPosition;
                Vector2 outgoing = notes[i + 1].StackedPosition - outFrom;
                if (incoming.Length > 1 && outgoing.Length > 1)
                    angles[i] = Math.Acos(Math.Clamp(Vector2.Dot(incoming, outgoing) / (incoming.Length * outgoing.Length), -1, 1)) * 180 / Math.PI;
            }

            for (int i = 1; i < notes.Count;)
            {
                if (!dense[i])
                {
                    i++;
                    continue;
                }

                int start = i - 1, end = i;
                while (end + 1 < notes.Count && dense[end + 1])
                    end++;
                if (end - start + 1 >= 5)
                    for (int k = start; k <= end; k++)
                        inStream[k] = true;
                i = end + 1;
            }

            int shortBurstNotes = 0, run = 0;
            for (int i = 1; i <= notes.Count; i++)
            {
                if (i < notes.Count && fast[i])
                {
                    run++;
                    continue;
                }

                if (run + 1 >= 3 && run + 1 <= 8)
                    shortBurstNotes += run + 1;
                run = 0;
            }

            int wide = 0, slowWide = 0, variableTurns = 0;
            var runAngles = new List<double>();
            var runSpacings = new List<double>();
            var jumpAngles = new List<double>();
            for (int i = 1; i < notes.Count; i++)
            {
                if (spacing[i] >= 3)
                {
                    wide++;
                    if (gaps[i] > 125 && gaps[i] <= 300)
                        slowWide++;
                }

                if (fast[i])
                {
                    runSpacings.Add(spacing[i]);
                    if (i + 1 < notes.Count && fast[i + 1] && !double.IsNaN(angles[i]))
                        runAngles.Add(angles[i]);
                }
                else if (spacing[i] >= 1.5 && !double.IsNaN(angles[i]))
                    jumpAngles.Add(angles[i]);

                // Large spacing changes at non-trivial turns, away from stacks and breaks.
                if (i + 1 < notes.Count && gaps[i] > 0 && gaps[i] <= 300 && gaps[i + 1] > 0 && gaps[i + 1] <= 300
                    && Math.Min(spacing[i], spacing[i + 1]) >= 0.3 && Math.Max(spacing[i], spacing[i + 1]) >= 3
                    && Math.Max(spacing[i], spacing[i + 1]) / Math.Min(spacing[i], spacing[i + 1]) >= 2
                    && angles[i] >= 40 && angles[i] <= 140)
                    variableTurns++;
            }

            double total = notes.Count;
            double wideShare = wide / total;
            double slowWideShare = slowWide / total;
            double variableTurnShare = variableTurns / total;
            double fastShare = fast.Count(v => v) / total;
            double fastTurnShare = runAngles.Count == 0 ? 0 : (double)runAngles.Count(a => a < 120) / runAngles.Count;
            double jumpAngleSpread = 0;
            if (jumpAngles.Count >= 2)
            {
                double mean = jumpAngles.Average();
                jumpAngleSpread = Math.Sqrt(jumpAngles.Average(a => (a - mean) * (a - mean)));
            }

            runSpacings.Sort();
            double fastSpacing = runSpacings.Count == 0 ? 0 : runSpacings.Count % 2 == 1
                ? runSpacings[runSpacings.Count / 2]
                : (runSpacings[runSpacings.Count / 2 - 1] + runSpacings[runSpacings.Count / 2]) / 2;

            // Two variants: slow fullscreens with flowing bursts, or wider mixed-speed technical jump/burst sections.
            double slowFlow = smoothStep(slowWideShare, 0.10, 0.18) * (1 - smoothStep(fastTurnShare, 0.15, 0.40));
            double mixed = smoothStep(fastSpacing, 1, 1.5) * smoothStep(jumpAngleSpread, 30, 35)
                           * smoothStep(slowWideShare, 0.08, 0.13) * smoothStep(shortBurstNotes / total, 0.24, 0.30);
            double confidence = smoothStep(wideShare, 0.18, 0.24) * smoothStep(variableTurnShare, 0.02, 0.03)
                                * smoothStep(fastShare, 0.03, 0.07) * Math.Max(slowFlow, mixed)
                                * (1 - smoothStep(inStream.Count(v => v) / total, 0.40, 0.50))
                                * (1 - smoothStep(notes.Count(o => o is Slider) / total, 0.45, 0.55));
            return new Analysis(confidence, wideShare, slowWideShare, variableTurnShare);
        }

        private static double smoothStep(double value, double start, double end)
        {
            double t = Math.Clamp((value - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
