// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : VariableLengthStrainSkill
    {
        private static readonly double reference_circle_radius = OsuHitObject.OBJECT_RADIUS * LegacyRulesetExtensions.CalculateScaleFromCircleSize(4, true);
        private const double relax_pattern_full_speed_time = 100;
        private const double relax_pattern_fade_end_time = 130;
        private const double relax_spaced_stream_full_distance = 150;
        private const double relax_spaced_stream_max_jump_distance = 215;

        public readonly bool IncludeSliders;

        private readonly bool applyMappingAntiAbuse;
        private readonly bool isRelax;

        public Aim(Mod[] mods, bool includeSliders, bool applyMappingAntiAbuse = true)
            : base(mods)
        {
            IncludeSliders = includeSliders;
            this.applyMappingAntiAbuse = applyMappingAntiAbuse;
            isRelax = mods.Any(m => m is OsuModRelax or OsuModMosuRelax);
        }

        private double currentStrain;
        private double maxEndTime;
        private int relaxPatternStreak;

        private int relaxFlowRunNotes;
        private double relaxFlowWeightSum;
        private double relaxExtremeJumpWeightSum;
        private double relaxWideFlowWeightSum;
        private double relaxRawPatternDifficultySum;
        private double relaxBalancedPatternDifficultySum;
        private double relaxHardJumpWeightSum;
        private double relaxVerticalHardJumpWeightSum;
        private int relaxQualifyingVerticalJumps;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => DiffUtils.Pow(0.2, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) =>
            currentStrain * strainDecay(time - current.Previous().StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            if (Mods.Any(m => m is OsuModAutopilot))
                return 0;

            // Do not award additional aim strain for overlapping objects. This preserves
            // the fork's anti-abuse handling for maps containing concurrent objects.
            bool isOverlapping = applyMappingAntiAbuse && current.StartTime < maxEndTime - 1;
            maxEndTime = Math.Max(maxEndTime, current.EndTime);

            double decay = strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            if (isRelax && IncludeSliders)
            {
                processRelaxVerticalMetrics((OsuDifficultyHitObject)current);
                processRelaxFlowMetrics((OsuDifficultyHitObject)current);
            }

            currentStrain *= decay;

            if (!isOverlapping)
                currentStrain += calculateAdjustedDifficulty(current) * (1 - decay);
            else
                resetRelaxPatternState();

            if (current.BaseObject is Slider && !isOverlapping)
                sliderStrains.Add(currentStrain);

            return currentStrain;
        }

        private void processRelaxFlowMetrics(OsuDifficultyHitObject current)
        {
            if (current.Index <= 1 || current.Previous(0) is not OsuDifficultyHitObject previous)
                return;

            // The pinned Mosu calculator normalises to radius 52 while the current lazer
            // preprocessing uses radius 50. Convert only for its parity metrics.
            const double mosu_distance_scale = 52.0 / OsuDifficultyHitObject.NORMALISED_RADIUS;
            double jumpDistance = current.LazyJumpDistance * mosu_distance_scale;
            double previousJumpDistance = previous.LazyJumpDistance * mosu_distance_scale;

            double maxTime = Math.Max(current.AdjustedDeltaTime, previous.AdjustedDeltaTime);
            double minTime = Math.Max(1, Math.Min(current.AdjustedDeltaTime, previous.AdjustedDeltaTime));
            double timingWeight = 1 - relaxSmoothStep(maxTime / minTime, 1.05, 1.22);

            double maxSpacing = Math.Max(jumpDistance, previousJumpDistance);
            double minSpacing = Math.Max(1, Math.Min(jumpDistance, previousJumpDistance));
            double spacingConsistencyWeight = 1 - relaxSmoothStep(maxSpacing / minSpacing, 1.10, 1.45);

            double speedWeight = 1 - relaxSmoothStep(current.AdjustedDeltaTime, 92, 125);
            double spacingWeight = relaxSmoothStep(jumpDistance, 42, 88);
            double jumpGuard = 1 - relaxSmoothStep(jumpDistance, 150, 215);
            double angleWeight = current.Angle.HasValue
                ? relaxSmoothStep(current.Angle.Value, double.DegreesToRadians(80), double.DegreesToRadians(132))
                : 0;

            relaxExtremeJumpWeightSum +=
                relaxSmoothStep(jumpDistance, 175, 250) *
                (1 - relaxSmoothStep(current.AdjustedDeltaTime, 90, 150));

            double noteFlowWeight = timingWeight
                                    * spacingConsistencyWeight
                                    * Math.Max(speedWeight, 0.25)
                                    * spacingWeight
                                    * jumpGuard
                                    * angleWeight;

            if (current.AdjustedDeltaTime <= 125 && jumpDistance >= 42 && noteFlowWeight >= 0.14)
            {
                relaxFlowRunNotes++;

                if (relaxFlowRunNotes >= 6)
                {
                    double contribution = noteFlowWeight * relaxSmoothStep(relaxFlowRunNotes, 6, 8);
                    relaxFlowWeightSum += contribution;
                    relaxWideFlowWeightSum += contribution * relaxSmoothStep(jumpDistance, 105, 150);
                }
            }
            else
            {
                relaxFlowRunNotes = 0;
            }
        }

        private void processRelaxVerticalMetrics(OsuDifficultyHitObject current)
        {
            if (current.BaseObject is not HitCircle currentCircle
                || current.LastObject is not HitCircle previousCircle)
                return;

            double distance = current.JumpDistance * 52.0 / OsuDifficultyHitObject.NORMALISED_RADIUS;
            double hardWeight = relaxSmoothStep(distance, 65, 130)
                                * (1 - relaxSmoothStep(current.DeltaTime, 300, 500));

            if (hardWeight <= 0)
                return;

            Vector2 vector = currentCircle.StackedPosition - previousCircle.StackedPosition;
            double rawDistance = vector.Length;

            if (rawDistance <= 0)
                return;

            double directionWeight = relaxSmoothStep(Math.Abs(vector.Y) / rawDistance, 0.8191520, 0.9659258);
            relaxHardJumpWeightSum += hardWeight;
            relaxVerticalHardJumpWeightSum += hardWeight * directionWeight;

            if (hardWeight >= 0.30 && directionWeight >= 0.45)
                relaxQualifyingVerticalJumps++;
        }

        public RelaxMetrics CalculateRelaxMetrics(int objectCount)
        {
            if (!isRelax || !IncludeSliders || objectCount <= 0)
                return default;

            double baseMapFlowWeight = relaxSmoothStep(relaxFlowWeightSum / objectCount, 0, 0.035);
            double jumpPresence = relaxSmoothStep(relaxExtremeJumpWeightSum / objectCount, 0.02, 0.10);
            double jumpShare = relaxExtremeJumpWeightSum / (relaxExtremeJumpWeightSum + relaxFlowWeightSum + 0.0001);
            double jumpCompetition = relaxSmoothStep(jumpShare, 0.20, 0.60);
            double hybridGuard = 1 - 0.68 * jumpPresence * jumpCompetition;

            double wideFlowShare = relaxWideFlowWeightSum / (relaxFlowWeightSum + 0.0001);
            double wideFlowPressure = relaxSmoothStep(wideFlowShare, 0.25, 0.75);
            double wideFlowGuard = 1 - 0.80 * wideFlowPressure;

            double flowBonusRatio = 0.17 * baseMapFlowWeight * hybridGuard * wideFlowGuard;
            double wideFlowPatternWeight = Math.Clamp(baseMapFlowWeight * wideFlowPressure, 0, 1);
            double patternPenaltyRatio = relaxRawPatternDifficultySum > 0
                ? Math.Clamp(relaxBalancedPatternDifficultySum / relaxRawPatternDifficultySum, 0.1, 1)
                : 1;
            double verticalShare = relaxVerticalHardJumpWeightSum / Math.Max(relaxHardJumpWeightSum, 0.0001);
            double verticalAimPressure = Math.Clamp(
                relaxSmoothStep(verticalShare, 0.25, 0.55)
                * relaxSmoothStep(relaxQualifyingVerticalJumps, 8, 20)
                * relaxSmoothStep(relaxHardJumpWeightSum, 8, 20),
                0,
                1);
            double[] peaks = GetCurrentStrainPeaks().Select(p => p.Value).ToArray();

            return new RelaxMetrics(
                flowBonusRatio,
                calculateSectionSpikeFillerWeight(peaks),
                wideFlowPatternWeight,
                peaks.Length,
                patternPenaltyRatio,
                verticalAimPressure);
        }

        private static double calculateSectionSpikeFillerWeight(double[] peaks)
        {
            int count = peaks.Length;

            if (count < 150)
                return 0;

            double[] sorted = peaks.Order().ToArray();
            int topCount = Math.Min(count, 3);
            double peakReference = sorted.Skip(count - topCount).Average();

            if (peakReference <= 0.0001)
                return 0;

            int baselineIndex = (int)Math.Round((count - 1) * 0.60, MidpointRounding.AwayFromZero);
            double baseline = Math.Max(sorted[Math.Min(baselineIndex, count - 1)], 0.0001);
            double spikeStrength = relaxSmoothStep(peakReference / baseline, 1.35, 1.90);

            if (spikeStrength <= 0)
                return 0;

            double hardThreshold = peakReference * 0.72;
            double hardCoverage = (double)peaks.Count(p => p >= hardThreshold) / count;
            double sparseWeight = 1 - relaxSmoothStep(hardCoverage, 0.08, 0.24);
            double longWeight = relaxSmoothStep(count, 150, 300);

            return Math.Clamp(spikeStrength * sparseWeight * longWeight, 0, 1);
        }

        private static double relaxSmoothStep(double value, double start, double end)
        {
            if (end <= start)
                return value >= end ? 1 : 0;

            double x = Math.Clamp((value - start) / (end - start), 0, 1);
            return x * x * (3 - 2 * x);
        }

        public readonly record struct RelaxMetrics(
            double FlowAimBonusRatio,
            double JumpSpikeFillerWeight,
            double WideFlowPatternWeight,
            int FlowSectionCount,
            double PatternPenaltyRatio,
            double VerticalAimPressure);

        private double calculateAdjustedDifficulty(DifficultyHitObject current)
        {
            const double skill_multiplier_snap = 70.9;
            const double skill_multiplier_agility = 2.35;
            const double skill_multiplier_flow = 242.0;

            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * skill_multiplier_snap;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current) * skill_multiplier_agility;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * skill_multiplier_flow;

            double pointSpamPenalty = 1;
            double spacedStreamPenalty = 1;

            if (Mods.Any(m => m is OsuModRelax or OsuModMosuRelax))
            {
                var osuCurrent = (OsuDifficultyHitObject)current;
                (bool isPointSpamTransition, bool isSpacedStreamTransition) = updateRelaxPatternState(osuCurrent);

                // Difficulty is evaluated forwards, but strain peaks near the beginning of a
                // stream must not escape the anti-abuse adjustment. Include the uninterrupted
                // look-ahead run while retaining the rolling past evidence across one-off
                // mapping irregularities.
                if (isPointSpamTransition)
                {
                    int followingTransitions = countFollowingRelaxPatternTransitions(osuCurrent, 16);
                    int evidence = CalculateRelaxPatternEvidence(relaxPatternStreak, followingTransitions, 16);
                    pointSpamPenalty = blendRelaxPatternPenalty(
                        CalculateRelaxPointSpamPenalty(evidence),
                        CalculateRelaxPatternSpeedWeight(rateNeutralAdjustedDeltaTime(osuCurrent)));
                }

                if (isSpacedStreamTransition)
                {
                    int evidence = CalculateRelaxPatternEvidence(relaxPatternStreak, countFollowingRelaxPatternTransitions(osuCurrent, 24), 24);
                    var previous = osuCurrent.Previous(0) as OsuDifficultyHitObject;
                    double interval = previous == null
                        ? rateNeutralAdjustedDeltaTime(osuCurrent)
                        : Math.Max(rateNeutralAdjustedDeltaTime(osuCurrent), rateNeutralAdjustedDeltaTime(previous));
                    double patternWeight = CalculateRelaxPatternSpeedWeight(interval)
                                           * CalculateRelaxSpacedStreamGeometryWeight(relaxReferenceNormalisedJumpDistance(osuCurrent));
                    spacedStreamPenalty = blendRelaxPatternPenalty(CalculateRelaxSpacedStreamPenalty(evidence), patternWeight);
                }
            }

            double totalDifficulty = calculateTotalValue(snapDifficulty, agilityDifficulty, flowDifficulty, pointSpamPenalty, spacedStreamPenalty);

            // Distance normalisation already accounts for circle size, but very large circles
            // still provide additional positional lenience which is not represented by speed
            // or accuracy skills. Keep that adjustment local to aim so timing performance is
            // not reduced, and use the actual object radius so native CS and DA behave equally.
            if (applyMappingAntiAbuse)
                totalDifficulty *= CalculateLargeCirclePenalty(((OsuHitObject)current.BaseObject).Radius);

            if (Mods.Any(m => m is OsuModMagnetised))
            {
                float magnetisedStrength = Mods.OfType<OsuModMagnetised>().First().AttractionStrength.Value;
                totalDifficulty *= 1.0 - magnetisedStrength;
            }

            totalDifficulty *= 0.985 + DiffUtils.Pow(Math.Max(0, ((OsuDifficultyHitObject)current).OverallDifficulty), 2) / 4000;

            return totalDifficulty;
        }

        /// <summary>
        /// Applies an additional precision adjustment to circles larger than CS4.
        /// The fourth root keeps this secondary adjustment much softer than the existing
        /// distance normalisation and avoids double-penalising low circle sizes.
        /// </summary>
        internal static double CalculateLargeCirclePenalty(double radius)
        {
            if (radius <= reference_circle_radius)
                return 1;

            return Math.Sqrt(Math.Sqrt(reference_circle_radius / radius));
        }

        private double calculateTotalValue(double snapDifficulty, double agilityDifficulty, double flowDifficulty,
                                           double pointSpamPenalty, double spacedStreamPenalty)
        {
            const double skill_multiplier_total = 1.12;
            const double combined_snap_norm_exponent = 1.2;

            // We compare flow to combined snap and agility because snap by itself doesn't have enough difficulty to be above flow on streams
            // Agility on the other hand is supposed to measure the rate of cursor velocity changes while snapping
            // So snapping every circle on a stream requires an enormous amount of agility at which point it's easier to flow
            double combinedSnapDifficulty = DiffUtils.Norm(combined_snap_norm_exponent, snapDifficulty, agilityDifficulty);

            double pSnap = calculateSnapFlowProbability(flowDifficulty / combinedSnapDifficulty);
            double pFlow = 1 - pSnap;

            if (Mods.Any(m => m is OsuModTouchDevice))
            {
                // we don't adjust agility here since agility represents TD difficulty in a decent enough way
                snapDifficulty = DiffUtils.Pow(snapDifficulty, 0.89);
                combinedSnapDifficulty = DiffUtils.Norm(combined_snap_norm_exponent, snapDifficulty, agilityDifficulty);
            }

            if (Mods.Any(m => m is OsuModRelax or OsuModMosuRelax))
            {
                double rawPatternDifficulty = combinedSnapDifficulty * pSnap + flowDifficulty * pFlow;
                double balancedPatternDifficulty = (combinedSnapDifficulty * pointSpamPenalty) * pSnap + flowDifficulty * pFlow;
                balancedPatternDifficulty *= spacedStreamPenalty;

                relaxRawPatternDifficultySum += rawPatternDifficulty;
                relaxBalancedPatternDifficultySum += balancedPatternDifficulty;

                if (RelaxPpSystemSelection.Current == ForkRelaxPpSystem.LazerVanilla)
                {
                    // Upstream lazer relax balance: flat reductions, no fork anti-abuse.
                    combinedSnapDifficulty *= 0.75;
                    flowDifficulty *= 0.6;
                }
                else
                {
                    // Keep snap difficulty unchanged. Relax-specific jump balancing is applied
                    // directly to aim PP; flow still loses its tapping component.
                    combinedSnapDifficulty *= pointSpamPenalty;
                    flowDifficulty *= 0.5;
                }
            }

            double totalDifficulty = combinedSnapDifficulty * pSnap + flowDifficulty * pFlow;

            if (Mods.Any(m => m is OsuModRelax or OsuModMosuRelax)
                && RelaxPpSystemSelection.Current != ForkRelaxPpSystem.LazerVanilla)
                totalDifficulty *= spacedStreamPenalty;

            double totalStrain = totalDifficulty * skill_multiplier_total;

            return totalStrain;
        }

        private (bool IsPointSpamTransition, bool IsSpacedStreamTransition) updateRelaxPatternState(OsuDifficultyHitObject current)
        {
            if (current.BaseObject is not HitCircle || current.Previous(0)?.BaseObject is not HitCircle)
            {
                resetRelaxPatternState();

                // A slider (or the first circle after it) can split an otherwise continuous
                // stream into fresh strain sections. If a full spaced stream immediately
                // follows, treat this separator as its lead-in so it cannot hold the section's
                // unpenalised peak by itself.
                bool isSpacedStreamLeadIn = current.BaseObject is HitCircle or Slider &&
                                            countFollowingRelaxPatternTransitions(current, 24) > 8;

                return (false, isSpacedStreamLeadIn);
            }

            var previous = current.Previous(0) as OsuDifficultyHitObject;

            if (previous == null)
            {
                resetRelaxPatternState();
                return (false, false);
            }

            bool isPointSpamTransition = isRelaxPointSpamTransition(current);
            bool isSpacedStreamTransition = isRelaxSpacedStreamTransition(current);
            bool hasTimingBreak = rateNeutralAdjustedDeltaTime(current) > 150;

            // Point spam and spaced streams share evidence so alternating just below and above
            // the radius boundary cannot reset both detectors. The current transition still
            // receives exactly one category-specific penalty.
            relaxPatternStreak = CalculateNextRelaxPatternStreak(
                relaxPatternStreak,
                isPointSpamTransition || isSpacedStreamTransition,
                hasTimingBreak);

            return (isPointSpamTransition, isSpacedStreamTransition);
        }

        private void resetRelaxPatternState()
        {
            relaxPatternStreak = 0;
        }

        private static bool isRelaxPointSpamTransition(OsuDifficultyHitObject current) =>
            IsRelaxPointSpamTransition(rateNeutralAdjustedDeltaTime(current), relaxReferenceNormalisedJumpDistance(current));

        private static bool isRelaxFastPatternTransition(OsuDifficultyHitObject current) =>
            isRelaxPointSpamTransition(current) || isRelaxSpacedStreamTransition(current);

        private static bool isRelaxSpacedStreamTransition(OsuDifficultyHitObject current)
        {
            var previous = current.Previous(0) as OsuDifficultyHitObject;

            if (previous == null)
                return false;

            // The first complete circle-to-circle transition after a separator has no useful
            // previous stream interval to compare against. It is safe to accept as a bootstrap
            // candidate because no penalty is applied unless the look-ahead run exceeds grace.
            if (previous.Previous(0)?.BaseObject is not HitCircle)
            {
                return current.BaseObject is HitCircle && previous.BaseObject is HitCircle &&
                       rateNeutralAdjustedDeltaTime(current) <= relax_pattern_fade_end_time &&
                       relaxReferenceNormalisedJumpDistance(current) >= OsuDifficultyHitObject.NORMALISED_RADIUS &&
                       relaxReferenceNormalisedJumpDistance(current) <= relax_spaced_stream_max_jump_distance;
            }

            return IsRelaxSpacedStreamTransition(
                rateNeutralAdjustedDeltaTime(current),
                rateNeutralAdjustedDeltaTime(previous),
                relaxReferenceNormalisedJumpDistance(current));
        }

        private static double rateNeutralAdjustedDeltaTime(OsuDifficultyHitObject current) =>
            Math.Max(current.DeltaTime * current.ClockRate, OsuDifficultyHitObject.MIN_DELTA_TIME);

        /// <summary>
        /// Converts the preprocessed jump distance back to a CS4 reference space.
        /// This keeps pattern classification stable when DA changes circle size while
        /// leaving the actual aim difficulty sensitive to the effective circle size.
        /// </summary>
        private static double relaxReferenceNormalisedJumpDistance(OsuDifficultyHitObject current) =>
            Math.Abs(current.LazyJumpDistance * current.SignedRadius / reference_circle_radius);

        internal static bool IsRelaxPointSpamTransition(double deltaTime, double normalisedJumpDistance) =>
            deltaTime <= relax_pattern_fade_end_time && normalisedJumpDistance < OsuDifficultyHitObject.NORMALISED_RADIUS;

        internal static bool IsRelaxSpacedStreamTransition(double deltaTime, double previousDeltaTime,
                                                            double jumpDistance)
        {
            if (deltaTime > relax_pattern_fade_end_time || previousDeltaTime > relax_pattern_fade_end_time ||
                jumpDistance < OsuDifficultyHitObject.NORMALISED_RADIUS ||
                jumpDistance > relax_spaced_stream_max_jump_distance)
                return false;

            double rhythmRatio = Math.Min(deltaTime, previousDeltaTime) / Math.Max(deltaTime, previousDeltaTime);

            // Relax removes the tapping requirement from flowable streams, but wide 1-2 jump
            // aim still has to be aimed. Keep those patterns out of the stream anti-abuse path.
            return rhythmRatio >= 0.85;
        }

        internal static int CalculateNextRelaxPatternStreak(int streak, bool isPatternTransition, bool hasTimingBreak)
        {
            if (hasTimingBreak)
                return 0;

            return isPatternTransition ? streak + 1 : Math.Max(0, streak - 1);
        }

        internal static int CalculateRelaxPatternEvidence(int pastEvidence, int followingTransitions, int maximumEvidence) =>
            Math.Min(maximumEvidence, pastEvidence + followingTransitions);

        private static int countFollowingRelaxPatternTransitions(OsuDifficultyHitObject current, int maximumEvidence)
        {
            int firstCandidate = current.BaseObject is Slider ? 1 : 0;
            int count = 0;

            for (int i = firstCandidate; i < maximumEvidence + firstCandidate; i++)
            {
                if (current.Next(i) is not OsuDifficultyHitObject next ||
                    next.BaseObject is not HitCircle || next.Previous(0)?.BaseObject is not HitCircle ||
                    rateNeutralAdjustedDeltaTime(next) > 150 || !isRelaxFastPatternTransition(next))
                    break;

                count++;
            }

            return count;
        }

        internal static double CalculateRelaxPointSpamPenalty(int streak) =>
            calculateRelaxPatternPenalty(streak, 4, 16, 0.85);

        internal static double CalculateRelaxSpacedStreamPenalty(int streak) =>
            calculateRelaxPatternPenalty(streak, 8, 24, 0.65);

        internal static double CalculateRelaxPatternSpeedWeight(double deltaTime) =>
            1 - DiffUtils.Smootherstep(deltaTime, relax_pattern_full_speed_time, relax_pattern_fade_end_time);

        internal static double CalculateRelaxSpacedStreamGeometryWeight(double jumpDistance) =>
            1 - DiffUtils.Smootherstep(jumpDistance, relax_spaced_stream_full_distance, relax_spaced_stream_max_jump_distance);

        private static double blendRelaxPatternPenalty(double penalty, double patternWeight) =>
            1 - (1 - penalty) * patternWeight;

        private static double calculateRelaxPatternPenalty(int streak, int graceStreak, int fullPenaltyStreak, double maximumReduction)
        {
            double progress = DiffUtils.Smootherstep(streak, graceStreak, fullPenaltyStreak);
            return 1 - maximumReduction * progress;
        }

        // A function that turns the ratio of snap : flow into the probability of snapping/flowing
        // It has the constraints:
        // P(snap) + P(flow) = 1 (the object is always either snapped or flowed)
        // P(snap) = f(snap/flow), P(flow) = f(flow/snap) (ie snap and flow are symmetric and reversible)
        // Therefore: f(x) + f(1/x) = 1
        // 0 <= f(x) <= 1 (cannot have negative or greater than 100% probability of snapping or flowing)
        // This logistic function is a solution, which fits nicely with the general idea of interpolation and provides a tuneable constant
        private static double calculateSnapFlowProbability(double ratio)
        {
            const double k = 7.27;

            if (ratio == 0)
                return 0;

            if (double.IsNaN(ratio))
                return 1;

            return DiffUtils.Logistic(-k * Math.Log(ratio));
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => DiffUtils.Logistic(strain / maxSliderStrain, 0.5, 12.0));
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            if (sliderStrains.Count == 0)
                return 0;

            double consistentTopStrain = difficultyValue * (1 - DecayWeight); // What would the top strain be if all strain values were identical

            if (consistentTopStrain == 0)
                return 0;

            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DiffUtils.Logistic(s / consistentTopStrain, 0.88, 10, 1.1));
        }

        public override double DifficultyValue()
        {
            double difficulty = 0;
            double time = 0;

            var strains = getReducedStrainPeaks();

            // Difficulty is a continuous weighted sum of the sorted strains
            foreach (StrainPeak strain in strains)
            {
                /* Weighting function can be thought of as:
                        b
                        ∫ DecayWeight^x dx
                        a
                    where a = startTime and b = endTime

                    Technically, the function below has been slightly modified from the equation above.
                    The real function would be
                        double weight = DiffUtils.Pow(DecayWeight, startTime) - DiffUtils.Pow(DecayWeight, endTime);
                        ...
                        return difficulty / Math.Log(1 / DecayWeight);
                    E.g. for a DecayWeight of 0.9, we're multiplying by 10 instead of 9.49122...

                    This change makes it so that a map composed solely of MaxSectionLength chunks will have the exact same value when summed in this class and StrainSkill.
                    Doing this ensures the relationship between strain values and difficulty values remains the same between the two classes.
                */
                double startTime = time;
                double endTime = time + strain.SectionLength / MaxSectionLength;

                double weight = DiffUtils.Pow(DecayWeight, startTime) - DiffUtils.Pow(DecayWeight, endTime);

                difficulty += strain.Value * weight;
                time = endTime;
            }

            return difficulty / (1 - DecayWeight);
        }

        /// <summary>
        /// Returns a sorted enumerable of strain peaks with the highest values reduced.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<StrainPeak> getReducedStrainPeaks()
        {
            const int reduced_section_time = 4000;
            const double reduced_strain_baseline = 0.727;

            // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These sections will not contribute to the difficulty.
            List<StrainPeak> strains = GetCurrentStrainPeaks()
                                       .Where(p => p.Value > 0)
                                       .ToList();

            const int chunk_size = 20;
            double time = 0;
            int skipCount = 0;

            // We are reducing the highest strains first to account for extreme difficulty spikes
            // Strains are split into 20ms chunks to try to mitigate inconsistencies caused by reducing strains
            while (strains.Count > skipCount && time < reduced_section_time)
            {
                StrainPeak strain = strains[skipCount];

                for (double addedTime = 0; addedTime < strain.SectionLength; addedTime += chunk_size)
                {
                    double scale = Math.Log10(Interpolation.Lerp(1, 10, Math.Clamp((time + addedTime) / reduced_section_time, 0, 1)));

                    // intentionally add at end and sort afterwards, should be cheaper.
                    strains.Add(new StrainPeak(
                        strain.Value * Interpolation.Lerp(reduced_strain_baseline, 1.0, scale),
                        Math.Min(chunk_size, strain.SectionLength - addedTime)
                    ));
                }

                time += strain.SectionLength;
                skipCount++;
            }

            return strains.Skip(skipCount).OrderByDescending(p => p.Value);
        }
    }
}
