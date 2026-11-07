// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// The owner's "kiai": the parts of the map that load the player near the map's peak, found from the difficulty itself
    /// instead of the mapper's kiai flags (a third of the maps have none, some flash it every beat).
    /// </summary>
    /// <remarks>
    /// Load of a 400 ms section (× rate in map time) = aim and speed strain peaks, each relative to the mean of its 10 hardest
    /// sections, combined by the share of aim and speed PP in the total (both ^1.1, as in <see cref="RxSectionTypes"/>).
    /// Loaded: load ≥ <see cref="THRESHOLD"/>. Loaded runs closer than <see cref="MERGE_GAP_SECONDS"/> are one section; sections
    /// shorter than <see cref="MIN_SECONDS"/> are dropped. Lengths are played seconds (map time / rate).
    /// </remarks>
    internal static class RxLoadSections
    {
        /// <summary>Calibrated on 300 maps with real mapper kiai (2026-10-28): 0.5 matches their kiai best (Jaccard 0.59 over sections; the
        /// count of parts of 10 s and more is the same on 46% of the maps, ±1 on 76%).</summary>
        public const double THRESHOLD = 0.5;
        public const double MERGE_GAP_SECONDS = 2;
        public const double MIN_SECONDS = 2;
        private const int top_sections = 10;

        /// <param name="Starts">Map time where each section starts.</param>
        /// <param name="Ends">Map time where each section ends.</param>
        /// <param name="Seconds">Played length of each section.</param>
        public sealed record Result(double[] Starts, double[] Ends, double[] Seconds)
        {
            public static readonly Result EMPTY = new Result(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());

            public bool Contains(double time)
            {
                for (int i = 0; i < Starts.Length; i++)
                {
                    if (time > Starts[i] && time <= Ends[i])
                        return true;
                }

                return false;
            }

            public double TotalSeconds => Seconds.Sum();
        }

        public static Result Find(RxNativePerformanceResult result, double clockRate)
        {
            RxDifficultyAttributes difficulty = result.Difficulty;
            int count = Math.Max(difficulty.AimSectionPeaks.Length, difficulty.SpeedSectionPeaks.Length);
            if (count == 0 || difficulty.SectionLength <= 0 || clockRate <= 0)
                return Result.EMPTY;

            double aim = Math.Pow(Math.Max(0, result.PpAim), 1.1), speed = Math.Pow(Math.Max(0, result.PpSpeed), 1.1);
            if (aim + speed <= 0)
                return Result.EMPTY;

            double[] aimLoad = relative(difficulty.AimSectionPeaks, count), speedLoad = relative(difficulty.SpeedSectionPeaks, count);
            int mergeGap = (int)Math.Ceiling(MERGE_GAP_SECONDS * 1000 * clockRate / difficulty.SectionLength);

#if MOSUPP_LAB
            if (Environment.GetEnvironmentVariable("MOSUPP_LAB_DEBUG_LOAD") == "1")
            {
                Console.Error.WriteLine($"load rate {clockRate} sections {count} length {difficulty.SectionLength} first {difficulty.FirstSectionEnd} aimW {aim / (aim + speed):0.00}");
                Console.Error.WriteLine(string.Join(" ", Enumerable.Range(0, count).Select(k => ((aim * aimLoad[k] + speed * speedLoad[k]) / (aim + speed)).ToString("0.00"))));
            }
#endif
            var runs = new List<(int First, int Last)>();
            for (int k = 0; k < count; k++)
            {
                if ((aim * aimLoad[k] + speed * speedLoad[k]) / (aim + speed) < THRESHOLD)
                    continue;

                if (runs.Count > 0 && k - runs[^1].Last <= mergeGap)
                    runs[^1] = (runs[^1].First, k);
                else
                    runs.Add((k, k));
            }

            var starts = new List<double>();
            var ends = new List<double>();
            var seconds = new List<double>();

            foreach (var (first, last) in runs)
            {
                // Section k covers map time (FirstSectionEnd + (k − 1) · length, FirstSectionEnd + k · length].
                double start = difficulty.FirstSectionEnd + (first - 1) * difficulty.SectionLength;
                double end = difficulty.FirstSectionEnd + last * difficulty.SectionLength;
                double played = (end - start) / clockRate / 1000;
                if (played < MIN_SECONDS)
                    continue;

                starts.Add(start);
                ends.Add(end);
                seconds.Add(played);
            }

            return new Result(starts.ToArray(), ends.ToArray(), seconds.ToArray());
        }

        private static double[] relative(float[] peaks, int count)
        {
            double[] values = new double[count];
            if (peaks.Length == 0)
                return values;

            double top = peaks.OrderByDescending(p => p).Take(top_sections).Average();
            if (top <= 0)
                return values;

            for (int k = 0; k < peaks.Length; k++)
                values[k] = peaks[k] / top;

            return values;
        }
    }
}
