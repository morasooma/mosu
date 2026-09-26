// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MosuPpRxCs;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// Mosu RX-only balance rules applied after the parity calculator.
    /// </summary>
    internal static class RealistikRelaxBalance
    {
        // Keeps strong point-stream detection relevant when low component PP would otherwise zero the old speed gate.

        public static bool IsRelax(IEnumerable<Mod> mods)
            => mods.Any(m => m is OsuModRelax or OsuModMosuRelax);

        public static bool IsVerticalBalanceEligible(IEnumerable<Mod> mods)
        {
            bool hasRelax = false;

            foreach (Mod mod in mods)
            {
                if (mod is OsuModRelax or OsuModMosuRelax)
                {
                    hasRelax = true;
                    continue;
                }

                // These do not make vertical jump aim harder and must not bypass the rule.
                if (mod.Acronym is "CL" or "NF" or "SD" or "PF")
                    continue;

                // DT/NC and all genuine difficulty/gameplay changes remain excluded.
                return false;
            }

            return hasRelax;
        }

        public static double OdMultiplier(double od)
        {
            if (od <= 7) return 0.70;
            if (od < 8) return 0.70 + 0.10 * (od - 7);
            if (od < 9) return 0.80 + 0.10 * (od - 8);
            if (od < 10) return 0.90 + 0.10 * (od - 9);
            if (od < 11) return 1.00 + 0.05 * (od - 10);
            return 1.05;
        }

        public static double PatternMultiplier(IEnumerable<Mod> mods, RxNativePerformanceResult result)
        {
            if (!IsRelax(mods))
                return 1;

            RxDifficultyAttributes difficulty = result.Difficulty;
            double aimSpike = Math.Clamp(difficulty.JumpSpikeFillerWeight, 0, 1);
            double aimMultiplier = 1 - 0.08 * aimSpike;
            double ppAim = Math.Max(1, result.PpAim);
            double ppSpeed = Math.Max(0, result.PpSpeed);
            double speedAimRatio = ppSpeed / ppAim;
            double speedDominance = smoothStep(speedAimRatio, 1.20, 1.70);
            double speedMagnitude = smoothStep(ppSpeed, 400, 850);
            double speedStackGuard = 1 - smoothStep(aimSpike, 0.20, 0.70);
            double speedWeight = speedDominance * speedMagnitude * speedStackGuard;
            double wideStreamPattern = Math.Clamp(difficulty.WideFlowPatternWeight, 0, 1);
            double shortMapWeight = 1 - smoothStep(difficulty.FlowSectionCount, 120, 225);
            double wideStreamSpeedWeight = smoothStep(speedAimRatio, 1.50, 2.20);
            double wideStreamPatternSignal = smoothStep(wideStreamPattern, 0.08, 0.18);
            double wideShortStreamWeight = wideStreamPatternSignal * shortMapWeight * wideStreamSpeedWeight;
            double speedMultiplier = 1 - (0.32 - 0.17 * wideShortStreamWeight) * speedWeight;
            double flowBonus = Math.Clamp(difficulty.CustomFlowAimBonusRatio, 0, 0.17);
            double flowOverboost = smoothStep(flowBonus, 0.08, 0.15);
            double nonSpeedDominance = 1 - smoothStep(speedAimRatio, 1.30, 1.80);
            double flowMultiplier = 1 - 0.25 * flowOverboost * nonSpeedDominance;
            bool hasHardRock = mods.Any(m => m.Acronym == "HR");
            double hrShortWeight = 1 - smoothStep(difficulty.FlowSectionCount, 140, 225);
            double hrWideWeight = hasHardRock ? wideStreamPatternSignal * hrShortWeight : 0;
            double hrWideMultiplier = 1 - 0.25 * hrWideWeight;

            return Math.Clamp(aimMultiplier * speedMultiplier * flowMultiplier * hrWideMultiplier, 0.50, 1);
        }

        public static double LengthAimMultiplier(IEnumerable<Mod> mods, RxNativePerformanceResult result)
        {
            if (!IsRelax(mods))
                return 1;

            double maxCombo = Math.Max(0, result.Difficulty.MaxCombo);
            double ppAim = Math.Max(0, result.PpAim);
            double ppSpeed = Math.Max(0, result.PpSpeed);
            double aimShare = ppAim / Math.Max(1, ppAim + ppSpeed);
            double flowSignal = Math.Clamp(result.Difficulty.CustomFlowAimBonusRatio, 0, 0.17);
            double jumpBonus = 0.08
                               * smoothStep(maxCombo, 500, 2000)
                               * smoothStep(aimShare, 0.55, 0.72)
                               * (1 - smoothStep(flowSignal, 0.05, 0.12));
            double flowBonus = 0.06
                               * smoothStep(maxCombo, 2000, 4000)
                               * smoothStep(flowSignal, 0.05, 0.13);

            return 1 + Math.Min(0.10, jumpBonus + flowBonus);
        }

        public static double VerticalPressure(RxBeatmap beatmap, float circleSize, uint? passedObjects, double clockRate)
        {
            int take = (int)Math.Min(passedObjects ?? (uint)beatmap.HitObjects.Count, (uint)beatmap.HitObjects.Count);
            if (take < 2)
                return 0;

            double radius = 64 * (1 - 0.7 * (circleSize - 5) / 5) / 2;
            double scalingFactor = 52 / radius;
            double allHardJumpWeight = 0;
            double verticalHardJumpWeight = 0;
            int qualifyingVerticalJumps = 0;
            RxHitObject? previous = null;

            for (int i = 0; i < take; i++)
            {
                RxHitObject current = beatmap.HitObjects[i];
                if (current.Kind != RxHitObjectKind.Circle)
                {
                    previous = null;
                    continue;
                }

                if (previous is not null)
                {
                    RxVec2 vector = (current.Position - previous.Position) * (float)scalingFactor;
                    double distance = vector.Length;
                    double delta = (current.StartTime - previous.StartTime) / clockRate;

                    if (distance > 0 && delta > 0)
                    {
                        // Thresholds are normalized osu! difficulty-space distances (52 = one normalized radius).
                        double hardWeight = smoothStep(distance, 65, 130) * (1 - smoothStep(delta, 300, 500));
                        if (hardWeight > 0)
                        {
                            double directionWeight = smoothStep(Math.Abs(vector.Y) / distance, 0.8191520, 0.9659258);
                            allHardJumpWeight += hardWeight;
                            verticalHardJumpWeight += hardWeight * directionWeight;

                            if (hardWeight >= 0.30 && directionWeight >= 0.45)
                                qualifyingVerticalJumps++;
                        }
                    }
                }

                previous = current;
            }

            if (allHardJumpWeight <= 0.001)
                return 0;

            double verticalShare = verticalHardJumpWeight / allHardJumpWeight;
            return Math.Clamp(
                smoothStep(verticalShare, 0.25, 0.55)
                * smoothStep(qualifyingVerticalJumps, 8, 20)
                * smoothStep(allHardJumpWeight, 8, 20),
                0,
                1);
        }

        public static void ApplyComponentGuards(
            RxNativePerformanceResult result,
            IEnumerable<Mod> mods,
            RxScoreState score,
            RxBeatmap beatmap,
            float circleSize,
            double clockRate)
        {
            if (!IsRelax(mods))
                return;

            applyExtremeSpacedStreamGuard(result, score, beatmap, circleSize, clockRate);
            applyHiddenReadingGuard(result, mods, score);
        }

        internal static double ApplyLengthAimBonusForTesting(RxNativePerformanceResult result, double multiplier, RxScoreState score)
        {
            double nativeMultiplier = result.Pp / Math.Max(RebuildTotal(result, score, 1), double.Epsilon);
            result.PpAim *= multiplier;
            result.Pp = RebuildTotal(result, score, nativeMultiplier);
            return result.Pp;
        }

        public static double RebuildTotal(RxNativePerformanceResult result, RxScoreState score, double nativeMultiplier)
        {
            double accuracy = calculateAccuracy(score, result.Difficulty);
            double accDepression = calculateAccDepression(result.Difficulty, accuracy);
            double sum = Math.Pow(Math.Max(0, result.PpAim), 1.185)
                         + Math.Pow(Math.Max(0, result.PpSpeed), 0.83 * accDepression)
                         + Math.Pow(Math.Max(0, result.PpAccuracy), 1.14)
                         + Math.Pow(Math.Max(0, result.PpReading), 1.1);
            return Math.Pow(sum, 1 / 1.1) * nativeMultiplier;
        }

        private static void applyExtremeSpacedStreamGuard(RxNativePerformanceResult result, RxScoreState score, RxBeatmap beatmap, float circleSize, double clockRate)
        {
            int take = (int)Math.Min(score.PassedObjects ?? (uint)beatmap.HitObjects.Count, (uint)beatmap.HitObjects.Count);
            if (take < 40 || result.PpAim <= 0)
                return;

            double radius = 64 * (1 - 0.7 * (circleSize - 5) / 5) / 2;
            double scalingFactor = 52 / radius;
            int circleTransitions = 0;
            int extremeFastTransitions = 0;
            int currentRun = 0;
            int longestRun = 0;
            RxHitObject? previous = null;

            for (int i = 0; i < take; i++)
            {
                RxHitObject current = beatmap.HitObjects[i];
                if (current.Kind != RxHitObjectKind.Circle)
                {
                    previous = null;
                    currentRun = 0;
                    continue;
                }

                if (previous is not null)
                {
                    circleTransitions++;
                    double delta = (current.StartTime - previous.StartTime) / clockRate;
                    double jump = ((current.Position - previous.Position) * (float)scalingFactor).Length;

                    if (delta <= 100 && jump >= 4 * 52)
                    {
                        extremeFastTransitions++;
                        longestRun = Math.Max(longestRun, ++currentRun);
                    }
                    else
                    {
                        currentRun = 0;
                    }
                }

                previous = current;
            }

            if (circleTransitions == 0)
                return;

            double share = (double)extremeFastTransitions / circleTransitions;
            double pressure = smoothStep(share, 0.55, 0.85)
                              * smoothStep(longestRun, 24, 80)
                              * smoothStep(result.Difficulty.AimStrain, 7.5, 10);

            // This is a bounded component nerf for continuous CS-scaled spaced-stream farm.
            // It intentionally does not zero the map and never applies without the geometry signal.
            if (pressure > 0)
                result.PpAim *= 1 - 0.15 * pressure;
        }

        private static void applyHiddenReadingGuard(RxNativePerformanceResult result, IEnumerable<Mod> mods, RxScoreState score)
        {
            if (!mods.Any(m => m.Acronym == "HD") || result.PpReading <= 0)
                return;

            double movementPp = Math.Max(1, result.PpAim + result.PpSpeed);
            double pressure = Math.Clamp(
                smoothStep(result.PpReading, 250, 700)
                * smoothStep(result.PpReading / movementPp, 0.65, 1.30)
                * smoothStep(result.Difficulty.ReadingStrain / Math.Max(0.01, result.Difficulty.AimStrain), 1.35, 2.50),
                0,
                1);

            if (pressure > 0)
                result.PpReading *= 1 - 0.95 * pressure;
        }

        private static double calculateAccuracy(RxScoreState score, RxDifficultyAttributes difficulty)
        {
            uint objectCount = score.PassedObjects ?? (uint)(difficulty.CircleCount + difficulty.SliderCount + difficulty.SpinnerCount);
            return objectCount == 0 ? 0 : (6d * score.Count300 + 2d * score.Count100 + score.Count50) / (6d * objectCount);
        }

        private static double calculateAccDepression(RxDifficultyAttributes difficulty, double accuracy)
        {
            double streamsNerf = Math.Round(difficulty.AimStrain / Math.Max(0.0001, difficulty.SpeedStrain), 2, MidpointRounding.AwayFromZero);
            return streamsNerf < 1.09 ? Math.Max(0.78 - Math.Abs(1 - accuracy), 0.5) : 1;
        }

        private static double smoothStep(double value, double start, double end)
        {
            if (value <= start) return 0;
            if (value >= end) return 1;
            double x = (value - start) / (end - start);
            return x * x * (3 - 2 * x);
        }
    }
}
