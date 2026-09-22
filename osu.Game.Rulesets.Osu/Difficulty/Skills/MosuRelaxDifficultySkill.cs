// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Calculates the pinned Mosu/Realistik Relax aim and speed difficulty values.
    ///
    /// This deliberately lives alongside the current osu! difficulty skills. The regular
    /// skills continue to provide star rating and anti-abuse diagnostics, while these values
    /// are consumed only by the Relax performance calculator for server parity.
    /// </summary>
    internal sealed class MosuRelaxDifficultySkill : StrainSkill
    {
        internal enum SkillKind
        {
            Aim,
            Speed,
        }

        private const double normalised_radius = 52;
        private const int recent_object_limit = 8;

        private readonly SkillKind kind;
        private readonly bool applyMappingAntiAbuse;
        private readonly List<RecentObject> recentObjects = new List<RecentObject>(recent_object_limit);

        private PreparedObject? previousPrepared;
        private PreparedObject? previousPreviousPrepared;
        private (double JumpDistance, double StrainTime)? previousValues;
        private double currentStrain = 1;
        private double previousTime;
        private double maxEndTime;
        private double? cachedDifficultyValue;

        public SkillKind Kind => kind;

        public MosuRelaxDifficultySkill(Mod[] mods, SkillKind kind, bool applyMappingAntiAbuse)
            : base(mods)
        {
            this.kind = kind;
            this.applyMappingAntiAbuse = applyMappingAntiAbuse;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) =>
            currentStrain * strainDecay(time - previousTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            if (previousPrepared == null)
                previousPrepared = prepare(current.LastObject);

            PreparedObject prepared = prepare(current.BaseObject);
            DifficultyObject difficultyObject = createDifficultyObject(
                prepared,
                previousPrepared.Value,
                previousValues,
                previousPreviousPrepared,
                current.DeltaTime);

            bool isOverlapping = applyMappingAntiAbuse && current.StartTime < maxEndTime - 1;
            maxEndTime = Math.Max(maxEndTime, current.EndTime);

            currentStrain *= strainDecay(current.DeltaTime);

            if (!isOverlapping)
            {
                double addition = strainValueOf(difficultyObject) * skillMultiplier;

                if (kind == SkillKind.Aim && applyMappingAntiAbuse)
                    addition *= Aim.CalculateLargeCirclePenalty(((OsuHitObject)current.BaseObject).Radius);

                currentStrain += addition;
            }

            previousPreviousPrepared = previousPrepared;
            previousPrepared = prepared;
            previousValues = (difficultyObject.JumpDistance, difficultyObject.StrainTime);
            previousTime = current.StartTime;

            if (kind == SkillKind.Aim)
            {
                var recent = new RecentObject(
                    difficultyObject.NormalisedVectorAngle,
                    difficultyObject.StrainTime,
                    difficultyObject.JumpDistance,
                    difficultyObject.Angle);

                if (recentObjects.Count >= recent_object_limit)
                    recentObjects.RemoveAt(recentObjects.Count - 1);

                recentObjects.Insert(0, recent);
            }

            return currentStrain;
        }

        public override double DifficultyValue()
        {
            double[] peaks = GetCurrentStrainPeaks().Where(p => p > 0).ToArray();

            if (peaks.Length == 0)
                return 0;

            int earlySectionCount = (int)Math.Floor(peaks.Length * 0.85);

            if (peaks.Length > 5 && earlySectionCount > 0)
            {
                double earlyMax = peaks.Take(earlySectionCount).Max();
                double spikeThreshold = Math.Max(earlyMax * 1.35, 1);

                for (int i = earlySectionCount; i < peaks.Length; i++)
                {
                    if (peaks[i] > spikeThreshold)
                        peaks[i] = spikeThreshold + (peaks[i] - spikeThreshold) * 0.62;
                }
            }

            Array.Sort(peaks, (a, b) => b.CompareTo(a));

            double difficulty = 0;
            double weight = 1;

            foreach (double peak in peaks)
            {
                difficulty += peak * weight;
                weight *= 0.9;
            }

            cachedDifficultyValue = difficulty;
            return difficulty;
        }

        public double CountDifficultStrains()
        {
            double difficultyValue = cachedDifficultyValue ?? DifficultyValue();
            double singleStrain = difficultyValue / 10;

            if (singleStrain <= 0)
                return 0;

            return GetObjectDifficulties().Sum(strain =>
                1.1 / (1 + Math.Exp(-10 * (strain / singleStrain - 0.88))));
        }

        public double CalculateSectionSpikeFillerWeight()
        {
            double[] peaks = GetCurrentStrainPeaks().ToArray();

            if (peaks.Length < 150)
                return 0;

            Array.Sort(peaks);
            double peakReference = peaks.Skip(peaks.Length - Math.Min(peaks.Length, 3)).Average();

            if (peakReference <= 0.0001)
                return 0;

            int baselineIndex = (int)Math.Round((peaks.Length - 1) * 0.60, MidpointRounding.AwayFromZero);
            double baseline = Math.Max(peaks[Math.Min(baselineIndex, peaks.Length - 1)], 0.0001);
            double spikeStrength = smoothStep(peakReference / baseline, 1.35, 1.90);

            if (spikeStrength <= 0)
                return 0;

            double hardCoverage = (double)peaks.Count(peak => peak >= peakReference * 0.72) / peaks.Length;
            double sparseWeight = 1 - smoothStep(hardCoverage, 0.08, 0.24);
            double longWeight = smoothStep(peaks.Length, 150, 300);

            return Math.Clamp(spikeStrength * sparseWeight * longWeight, 0, 1);
        }

        public int SectionCount => GetCurrentStrainPeaks().Count();

        private double strainValueOf(DifficultyObject current) =>
            kind == SkillKind.Aim ? aimStrainValue(current) : speedStrainValue(current);

        private double skillMultiplier => kind == SkillKind.Aim ? 26.25 : 1400;
        private double strainDecayBase => kind == SkillKind.Aim ? 0.15 : 0.3;
        private double strainDecay(double milliseconds) => Math.Pow(strainDecayBase, milliseconds / 1000);

        private double aimStrainValue(DifficultyObject current)
        {
            const double aim_angle_bonus_begin = Math.PI / 3;
            const double timing_threshold = 107;

            if (current.Base.IsSpinner)
                return 0;

            double result = 0;

            if (current.PreviousValues is { } previous && current.Angle is { } angle && angle > aim_angle_bonus_begin)
            {
                const double scale = 90;
                double angleBonus = Math.Sqrt(
                    Math.Pow(Math.Sin(angle - aim_angle_bonus_begin), 2)
                    * Math.Max(previous.JumpDistance - scale, 0)
                    * Math.Max(current.JumpDistance - scale, 0));

                result = 1.5 * diminishingExponent(Math.Max(angleBonus, 0))
                         / Math.Max(timing_threshold, previous.StrainTime);
            }

            double jumpDistance = diminishingExponent(current.JumpDistance);
            double travelDistance = diminishingExponent(current.TravelDistance);
            double distance = jumpDistance + travelDistance + Math.Sqrt(travelDistance * jumpDistance);

            double strain = Math.Max(
                result + distance / Math.Max(current.StrainTime, timing_threshold),
                distance / current.StrainTime);

            return strain * vectorAngleRepetition(current);
        }

        private static double speedStrainValue(DifficultyObject current)
        {
            const double single_spacing_threshold = 125;
            const double speed_angle_bonus_begin = 5 * Math.PI / 6;
            const double min_speed_bonus = 75;
            const double max_speed_bonus = 45;
            const double speed_balancing_factor = 40;

            if (current.Base.IsSpinner)
                return 0;

            double distance = Math.Min(single_spacing_threshold, current.TravelDistance + current.JumpDistance);
            double deltaTime = Math.Max(max_speed_bonus, current.Delta);
            double speedBonus = 1;

            if (deltaTime < min_speed_bonus)
            {
                double exponent = (min_speed_bonus - deltaTime) / speed_balancing_factor;
                speedBonus += exponent * exponent;
            }

            double angleBonus = 1;

            if (current.Angle is { } angle && angle < speed_angle_bonus_begin)
            {
                double exponent = Math.Sin(1.5 * (speed_angle_bonus_begin - angle));
                angleBonus = 1 + exponent * exponent / 3.57;

                if (angle < Math.PI / 2)
                {
                    angleBonus = 1.28;

                    if (distance < 90 && angle < Math.PI / 4)
                    {
                        angleBonus += (1 - angleBonus) * Math.Min((90 - distance) / 10, 1);
                    }
                    else if (distance < 90)
                    {
                        angleBonus += (1 - angleBonus)
                                      * Math.Min((90 - distance) / 10, 1)
                                      * Math.Sin((Math.PI / 2 - angle) / (Math.PI / 4));
                    }
                }
            }

            return (1 + (speedBonus - 1) * 0.75)
                   * angleBonus
                   * (0.95 + speedBonus * Math.Pow(distance / single_spacing_threshold, 3.5))
                   / current.StrainTime;
        }

        private double vectorAngleRepetition(DifficultyObject current)
        {
            const double maximum_repetition_nerf = 0.08;
            const double repetition_threshold = 1.3;
            const double normalised_diameter = normalised_radius * 2;
            const int note_limit = 6;

            if (current.Angle is not { } currentAngle || current.NormalisedVectorAngle is not { } currentVectorAngle)
                return 1;

            if (recentObjects.Count == 0 || recentObjects[0].Angle == null)
                return 1;

            double constantAngleCount = 0;

            for (int i = 0; i < Math.Min(note_limit, recentObjects.Count); i++)
            {
                RecentObject recent = recentObjects[i];

                if (Math.Max(current.StrainTime, recent.StrainTime) > 1.1 * Math.Min(current.StrainTime, recent.StrainTime))
                    break;

                if (recent.NormalisedVectorAngle is { } vectorAngle)
                {
                    double difference = Math.Abs(currentVectorAngle - vectorAngle);
                    constantAngleCount += Math.Cos(8 * Math.Min(difference, Math.PI / 16));
                }
            }

            double vectorRepetition = Math.Pow(Math.Min(repetition_threshold / Math.Max(constantAngleCount, 0), 1), 2);
            double stackFactor = smootherStep(current.JumpDistance, 0, normalised_diameter);
            double recentAngle = recentObjects[0].Angle.GetValueOrDefault();
            double angleDifference = Math.Cos(2 * Math.Min(Math.Abs(currentAngle - recentAngle) * stackFactor, Math.PI / 4));
            double baseNerf = 1 - maximum_repetition_nerf * reverseAngleSmoothStep(recentAngle) * angleDifference;

            return Math.Pow(baseNerf + (1 - baseNerf) * vectorRepetition * stackFactor, 2);
        }

        private static DifficultyObject createDifficultyObject(
            PreparedObject current,
            PreparedObject previous,
            (double JumpDistance, double StrainTime)? previousValues,
            PreparedObject? previousPrevious,
            double delta)
        {
            double strainTime = Math.Max(delta, 50);
            double travelDistance = previous.TravelDistance ?? 0;
            double jumpDistance = current.IsSpinner ? 0 : (current.Position - previous.EndPosition).Length * current.ScalingFactor;

            double? vectorAngle = null;
            double? angle = null;

            if (!current.IsSpinner && previousPrevious != null)
            {
                Vector2 vector = current.Position - previous.EndPosition;
                vectorAngle = Math.Atan2(Math.Abs(vector.Y), Math.Abs(vector.X));
            }

            if (previousPrevious != null)
            {
                Vector2 first = previousPrevious.Value.EndPosition - previous.Position;
                Vector2 second = current.Position - previous.EndPosition;
                angle = Math.Abs(Math.Atan2(first.X * second.Y - first.Y * second.X, Vector2.Dot(first, second)));
            }

            return new DifficultyObject(current, previousValues, jumpDistance, travelDistance, angle, vectorAngle, delta, strainTime);
        }

        private static PreparedObject prepare(osu.Game.Rulesets.Objects.HitObject hitObject)
        {
            var osuObject = (OsuHitObject)hitObject;
            double scalingFactor = normalised_radius / osuObject.Radius;

            if (osuObject is Spinner)
                return new PreparedObject(osuObject.Position, osuObject.Position, null, scalingFactor);

            if (osuObject is not Slider slider || slider.Duration <= 0 || slider.SpanDuration <= 0)
                return new PreparedObject(osuObject.Position, osuObject.Position, 0, scalingFactor);

            Vector2 cursorPosition = slider.Position;
            double travelDistance = 0;
            double followRadius = osuObject.Radius * 3;

            void processVertex(Vector2 vertex)
            {
                Vector2 movement = vertex - cursorPosition;
                double distance = movement.Length;

                if (distance <= followRadius)
                    return;

                double requiredDistance = distance - followRadius;
                cursorPosition += movement * (float)(requiredDistance / distance);
                travelDistance += requiredDistance;
            }

            // The pinned calculator evaluates slider ticks and repeats before its lenient tail.
            // lazer has already generated those nested scoring vertices for the playable beatmap.
            for (int i = 1; i < Math.Max(1, slider.NestedHitObjects.Count - 1); i++)
            {
                if (slider.NestedHitObjects[i] is OsuHitObject nested)
                    processVertex(nested.Position);
            }

            double trackingEndTime = Math.Max(slider.StartTime + slider.Duration / 2, slider.EndTime - 36);
            double progress = (trackingEndTime - slider.StartTime) / slider.SpanDuration;

            if (progress % 2 >= 1)
                progress = 1 - progress % 1;
            else
                progress %= 1;

            processVertex(slider.Position + slider.Path.PositionAt(progress));

            return new PreparedObject(slider.Position, cursorPosition, travelDistance * scalingFactor, scalingFactor);
        }

        private static double diminishingExponent(double value) => Math.Pow(value, 0.99);

        private static double smootherStep(double value, double start, double end)
        {
            double x = Math.Clamp((value - start) / (end - start), 0, 1);
            return x * x * x * (x * (x * 6 - 15) + 10);
        }

        private static double smoothStep(double value, double start, double end)
        {
            double x = Math.Clamp((value - start) / (end - start), 0, 1);
            return x * x * (3 - 2 * x);
        }

        private static double reverseAngleSmoothStep(double angle)
        {
            double start = 140 * Math.PI / 180;
            double end = 40 * Math.PI / 180;
            double x = Math.Clamp((angle - start) / (end - start), 0, 1);
            return x * x * (3 - 2 * x);
        }

        private readonly record struct PreparedObject(
            Vector2 Position,
            Vector2 EndPosition,
            double? TravelDistance,
            double ScalingFactor)
        {
            public bool IsSpinner => TravelDistance == null;
        }

        private readonly record struct DifficultyObject(
            PreparedObject Base,
            (double JumpDistance, double StrainTime)? PreviousValues,
            double JumpDistance,
            double TravelDistance,
            double? Angle,
            double? NormalisedVectorAngle,
            double Delta,
            double StrainTime);

        private readonly record struct RecentObject(
            double? NormalisedVectorAngle,
            double StrainTime,
            double JumpDistance,
            double? Angle);
    }
}
