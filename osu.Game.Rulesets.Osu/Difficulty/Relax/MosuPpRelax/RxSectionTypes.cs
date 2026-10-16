// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp "Section Types (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>, since 0.8.0): how much of the map's PP its streams
    /// earn. The map-type rules (Stream Map Nerf, type scale, RP Balance) used to see one type for the whole map, so a stream map
    /// with hard jump sections had its jumps nerfed as streams too.
    /// </summary>
    /// <remarks>
    /// The difficulty is a weighted sum of 400 ms section peaks (hardest first, weight 0.9 per rank, late spikes softened), so
    /// every section's share of it is known. Aim and speed shares are combined by the share of aim PP and speed PP in the total
    /// (both ^1.1, as in the total PP). A section's stream share is the share of its notes in straight streams
    /// (<see cref="RxStreamMapNerf.StreamNotes"/>, without stacked notes); a section without notes keeps the previous one's. Result: Σ section share ×
    /// section stream share — 0 for a map without streams, about 1 when the streams earn all of its PP.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxSectionTypes
    {
        private const float decay_weight = 0.9f;
        private const double total_exponent = 1.1;

        /// <summary>Stacked notes (moves below 0.2 circle diameters, as in RP) are not streams: Sendan Life's stacked doubles in its
        /// jump sections would otherwise count as 23% streams.</summary>
        private const double stream_min_spacing = 0.2;

        public static double StreamShare(RxBeatmap beatmap, RxNativePerformanceResult result)
        {
            RxDifficultyAttributes difficulty = result.Difficulty;
            int sections = Math.Max(difficulty.AimSectionPeaks.Length, difficulty.SpeedSectionPeaks.Length);
            if (sections == 0 || difficulty.SectionLength <= 0)
                return 0;

            double aim = Math.Pow(Math.Max(0, result.PpAim), total_exponent);
            double speed = Math.Pow(Math.Max(0, result.PpSpeed), total_exponent);
            if (aim + speed <= 0)
                return 0;

            double[] aimShare = SectionShares(difficulty.AimSectionPeaks);
            double[] speedShare = SectionShares(difficulty.SpeedSectionPeaks);
            double[] streamShare = sectionStreamShares(beatmap, difficulty, sections);

            double total = 0;
            for (int k = 0; k < sections; k++)
            {
                double weight = (aim * at(aimShare, k) + speed * at(speedShare, k)) / (aim + speed);
                total += weight * streamShare[k];
            }

            return Math.Clamp(total, 0, 1);
        }

        /// <summary>Each section's share of the skill's difficulty value (same weighting as the difficulty calculator); sums to 1.</summary>
        public static double[] SectionShares(float[] peaks)
        {
            int count = peaks.Length;
            double[] shares = new double[count];
            if (count == 0)
                return shares;

            // Late spikes are softened before sorting (Skill.computeDifficultyValue).
            float[] adjusted = (float[])peaks.Clone();
            int early = (int)MathF.Floor(count * 0.85f);

            if (count > 5 && early > 0)
            {
                float earlyMax = 0;
                for (int k = 0; k < early; k++)
                    earlyMax = MathF.Max(earlyMax, adjusted[k]);

                float threshold = MathF.Max(earlyMax * 1.35f, 1f);
                for (int k = early; k < count; k++)
                {
                    if (adjusted[k] > threshold)
                        adjusted[k] = threshold + (adjusted[k] - threshold) * 0.62f;
                }
            }

            int[] order = new int[count];
            for (int k = 0; k < count; k++)
                order[k] = k;
            Array.Sort(order, (a, b) => adjusted[b].CompareTo(adjusted[a]));

            // Equal peaks share the mean weight of their ranks: the difficulty does not depend on their order, so neither do the shares.
            double weight = 1, sum = 0;
            for (int start = 0; start < count;)
            {
                int end = start;
                while (end + 1 < count && adjusted[order[end + 1]] == adjusted[order[start]])
                    end++;

                double groupWeight = 0;
                for (int r = start; r <= end; r++)
                {
                    groupWeight += weight;
                    weight *= decay_weight;
                }

                groupWeight /= end - start + 1;
                for (int r = start; r <= end; r++)
                {
                    shares[order[r]] = adjusted[order[r]] * groupWeight;
                    sum += shares[order[r]];
                }

                start = end + 1;
            }

            if (sum > 0)
            {
                for (int k = 0; k < count; k++)
                    shares[k] /= sum;
            }

            return shares;
        }

        private static double[] sectionStreamShares(RxBeatmap beatmap, RxDifficultyAttributes difficulty, int sections)
        {
            List<RxHitObject> objects = beatmap.HitObjects;
            bool[] inStream = RxStreamMapNerf.StreamNotes(beatmap);
            double diameter = 2 * (54.4 - 4.48 * beatmap.CircleSize);
            int[] notes = new int[sections];
            int[] streams = new int[sections];

            for (int i = 0; i < objects.Count; i++)
            {
                // Section k ends at FirstSectionEnd + k · SectionLength and contains times in (end − length, end].
                int k = (int)Math.Ceiling((objects[i].StartTime - difficulty.FirstSectionEnd) / difficulty.SectionLength);
                if (k < 0 || k >= sections)
                    continue;

                notes[k]++;
                if (inStream[i] && i > 0 && distance(objects[i], objects[i - 1]) >= stream_min_spacing * diameter)
                    streams[k]++;
            }

            double[] shares = new double[sections];
            double previous = 0;

            for (int k = 0; k < sections; k++)
            {
                if (notes[k] > 0)
                    previous = (double)streams[k] / notes[k];
                shares[k] = previous;
            }

            return shares;
        }

        private static double at(double[] values, int k) => k < values.Length ? values[k] : 0;

        private static double distance(RxHitObject a, RxHitObject b)
        {
            double dx = a.Position.X - b.Position.X;
            double dy = a.Position.Y - b.Position.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
