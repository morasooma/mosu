// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Relax.Realistik;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        // Small compatibility corrections for the pinned calculator's raw .osu geometry.
        // lazer's slider preprocessing and reading object window are intentionally newer,
        // so the legacy ratings need these stable conversion factors before PP conversion.
        private readonly bool applyMappingAntiAbuse;

        // Relax PP has its own cache revision in ForkDataStore. Changes to that
        // formula must not invalidate the displayed star ratings for every map.
        public override int Version => 20260802;

        public OsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            applyMappingAntiAbuse = !beatmap.BeatmapInfo.GetOnlineStatus().BypassesMappingAntiAbuse();
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
            {
                var emptyAttributes = new OsuDifficultyAttributes { Mods = mods };

                // The RX performance calculator requires a prepared beatmap for any relax score,
                // including scores on object-less maps; skipping Prepare here made such maps fail
                // forever in background PP processing ("RX beatmap was not prepared").
                if (PreparePerformanceCalculation
                    && RelaxPpSystemSelection.Current != ForkRelaxPpSystem.LazerVanilla
                    && ManagedRealistikRelaxCalculator.IsRelax(mods))
                    ManagedRealistikRelaxCalculator.Prepare(beatmap, mods, emptyAttributes);

                return emptyAttributes;
            }

            var aim = skills.OfType<Aim>().Single(a => a.IncludeSliders);
            var aimWithoutSliders = skills.OfType<Aim>().Single(a => !a.IncludeSliders);
            var speed = skills.OfType<Speed>().Single();
            var flashlight = skills.OfType<Flashlight>().SingleOrDefault();
            var reading = skills.OfType<Reading>().Single(r => !r.UseMosuRelaxProfile);
            var mosuRelaxAim = skills.OfType<MosuRelaxDifficultySkill>().SingleOrDefault(s => s.Kind == MosuRelaxDifficultySkill.SkillKind.Aim);
            var mosuRelaxSpeed = skills.OfType<MosuRelaxDifficultySkill>().SingleOrDefault(s => s.Kind == MosuRelaxDifficultySkill.SkillKind.Speed);
            var mosuRelaxReading = skills.OfType<Reading>().SingleOrDefault(r => r.UseMosuRelaxProfile);

            double aimDifficultyValue = aim.DifficultyValue();
            double aimNoSlidersDifficultyValue = aimWithoutSliders.DifficultyValue();
            double speedDifficultyValue = speed.DifficultyValue();
            double readingDifficultyValue = reading.DifficultyValue();

            double mosuRelaxAimDifficultyValue = mosuRelaxAim?.DifficultyValue() ?? 0;
            double mosuRelaxSpeedDifficultyValue = mosuRelaxSpeed?.DifficultyValue() ?? 0;
            double mosuRelaxReadingDifficultyValue = mosuRelaxReading?.DifficultyValue() ?? 0;

            double aimDifficultStrainCount = aim.CountTopWeightedStrains(aimDifficultyValue);
            double speedDifficultStrainCount = speed.CountTopWeightedObjectDifficulties(speedDifficultyValue);
            double readingDifficultNoteCount = reading.CountTopWeightedObjectDifficulties(readingDifficultyValue);

            double mosuRelaxAimDifficultStrainCount = mosuRelaxAim?.CountDifficultStrains() ?? 0;
            double mosuRelaxSpeedDifficultStrainCount = mosuRelaxSpeed?.CountDifficultStrains() ?? 0;
            double mosuRelaxReadingDifficultNoteCount = mosuRelaxReading?.CountTopWeightedObjectDifficulties(mosuRelaxReadingDifficultyValue) ?? 0;

            double speedNotes = speed.RelevantObjectCount();

            double aimNoSlidersTopWeightedSliderCount = aimWithoutSliders.CountTopWeightedSliders(aimNoSlidersDifficultyValue);
            double aimNoSlidersDifficultStrainCount = aimWithoutSliders.CountTopWeightedStrains(aimNoSlidersDifficultyValue);

            double aimTopWeightedSliderFactor = aimNoSlidersTopWeightedSliderCount / Math.Max(1, aimNoSlidersDifficultStrainCount - aimNoSlidersTopWeightedSliderCount);

            double speedTopWeightedSliderCount = speed.CountTopWeightedSliders(speedDifficultyValue);
            double speedTopWeightedSliderFactor = speedTopWeightedSliderCount / Math.Max(1, speedDifficultStrainCount - speedTopWeightedSliderCount);

            double difficultSliders = aim.GetDifficultSliders();

            int hitCircleCount = beatmap.HitObjects.Count(h => h is HitCircle);
            int sliderCount = beatmap.HitObjects.Count(h => h is Slider);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);

            int totalHits = beatmap.HitObjects.Count;
            Aim.RelaxMetrics relaxMetrics = aim.CalculateRelaxMetrics(totalHits);

            double aimRating = calculateAimDifficultyRating(aimDifficultyValue);
            double aimNoSlidersRating = calculateAimDifficultyRating(aimNoSlidersDifficultyValue);

            double sliderFactor = aimDifficultyValue > 0
                ? aimNoSlidersRating / aimRating
                : 1;

            double speedRating = calculateDifficultyRating(speedDifficultyValue);
            double readingRating = calculateDifficultyRating(readingDifficultyValue);

            double flashlightRating = 0.0;

            if (flashlight is not null)
                flashlightRating = calculateDifficultyRating(flashlight.DifficultyValue());

            double sliderNestedScorePerObject = LegacyScoreUtils.CalculateNestedScorePerObject(beatmap, totalHits);
            double legacyScoreBaseMultiplier = LegacyScoreUtils.CalculateDifficultyPeppyStars(WorkingBeatmap.Beatmap);
            double mosuAimRatingMultiplier = calculateMosuAimRatingMultiplier(beatmap.Difficulty.CircleSize);
            double mosuReadingRatingMultiplier = calculateMosuReadingRatingMultiplier(beatmap.Difficulty.CircleSize);

            var simulator = new OsuLegacyScoreSimulator();
            var scoreAttributes = simulator.Simulate(WorkingBeatmap, beatmap);

            double baseAimPerformance = OsuPerformanceCalculator.DifficultyToPerformance(aimRating);
            double baseSpeedPerformance = HarmonicSkill.DifficultyToPerformance(speedRating);
            double baseReadingPerformance = HarmonicSkill.DifficultyToPerformance(readingRating);
            double baseFlashlightPerformance = Flashlight.DifficultyToPerformance(flashlightRating);
            double baseCognitionPerformance = SumCognitionDifficulty(baseReadingPerformance, baseFlashlightPerformance);

            double basePerformance = DiffUtils.Norm(OsuPerformanceCalculator.PERFORMANCE_NORM_EXPONENT, baseAimPerformance, baseSpeedPerformance, baseCognitionPerformance);

            double starRating = calculateStarRating(basePerformance);

            OsuDifficultyAttributes attributes = new OsuDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                AimDifficulty = aimRating,
                AimDifficultSliderCount = difficultSliders,
                SpeedDifficulty = speedRating,
                SpeedNoteCount = speedNotes,
                FlashlightDifficulty = flashlightRating,
                ReadingDifficulty = readingRating,
                SliderFactor = sliderFactor,
                AimDifficultStrainCount = aimDifficultStrainCount,
                SpeedDifficultStrainCount = speedDifficultStrainCount,
                ReadingDifficultNoteCount = readingDifficultNoteCount,
                RelaxFlowAimBonusRatio = relaxMetrics.FlowAimBonusRatio,
                RelaxJumpSpikeFillerWeight = mosuRelaxAim?.CalculateSectionSpikeFillerWeight() ?? relaxMetrics.JumpSpikeFillerWeight,
                RelaxWideFlowPatternWeight = relaxMetrics.WideFlowPatternWeight,
                RelaxFlowSectionCount = mosuRelaxAim?.SectionCount ?? relaxMetrics.FlowSectionCount,
                RelaxPatternPenaltyRatio = relaxMetrics.PatternPenaltyRatio,
                RelaxStreamWeight = calculateRelaxStreamWeight(
                    mosuRelaxAimDifficultStrainCount,
                    mosuRelaxSpeedDifficultStrainCount,
                    calculateDifficultyRating(mosuRelaxAimDifficultyValue),
                    calculateDifficultyRating(mosuRelaxSpeedDifficultyValue)),
                RelaxVerticalAimPressure = relaxMetrics.VerticalAimPressure,
                MosuRelaxAimDifficulty = calculateDifficultyRating(mosuRelaxAimDifficultyValue)
                                           * mosuAimRatingMultiplier
                                           * calculateMosuPatternPenalty(relaxMetrics.PatternPenaltyRatio),
                MosuRelaxSpeedDifficulty = calculateDifficultyRating(mosuRelaxSpeedDifficultyValue),
                MosuRelaxReadingDifficulty = calculateDifficultyRating(mosuRelaxReadingDifficultyValue) * mosuReadingRatingMultiplier,
                MosuRelaxAimDifficultStrainCount = mosuRelaxAimDifficultStrainCount,
                MosuRelaxSpeedDifficultStrainCount = mosuRelaxSpeedDifficultStrainCount,
                MosuRelaxReadingDifficultNoteCount = mosuRelaxReadingDifficultNoteCount,
                AimTopWeightedSliderFactor = aimTopWeightedSliderFactor,
                SpeedTopWeightedSliderFactor = speedTopWeightedSliderFactor,
                MaxCombo = beatmap.GetMaxCombo(),
                HitCircleCount = hitCircleCount,
                SliderCount = sliderCount,
                SpinnerCount = spinnerCount,
                NestedScorePerObject = sliderNestedScorePerObject,
                LegacyScoreBaseMultiplier = legacyScoreBaseMultiplier,
                MaximumLegacyComboScore = scoreAttributes.ComboScore
            };

            // Displayed SR remains on the current ruleset pipeline. The playable,
            // already-modified beatmap is prepared for the independent RX PP core.
            if (PreparePerformanceCalculation
                && RelaxPpSystemSelection.Current != ForkRelaxPpSystem.LazerVanilla
                && ManagedRealistikRelaxCalculator.IsRelax(mods))
                ManagedRealistikRelaxCalculator.Prepare(beatmap, mods, attributes);

            return attributes;
        }

        protected override void PrepareTimedPerformanceCalculation(IBeatmap beatmap, Mod[] mods, IReadOnlyList<TimedDifficultyAttributes> attributes)
        {
            if (RelaxPpSystemSelection.Current == ForkRelaxPpSystem.LazerVanilla
                || !ManagedRealistikRelaxCalculator.IsRelax(mods))
                return;

            // The RX representation is immutable and valid for every time-sliced attributes
            // instance. Convert the complete map once and associate that one representation with
            // all prefixes used by the live PP counter.
            ManagedRealistikRelaxCalculator.PrepareTimed(beatmap, mods,
                attributes.Select(attribute => (OsuDifficultyAttributes)attribute.Attributes));
        }

        public static double SumCognitionDifficulty(double reading, double flashlight)
        {
            if (reading <= 0)
                return flashlight;

            if (flashlight <= 0)
                return reading;

            // Nerf flashlight value in cognition sum when reading is greater than flashlight
            return DiffUtils.Norm(OsuPerformanceCalculator.PERFORMANCE_NORM_EXPONENT, reading, flashlight * Math.Clamp(flashlight / reading, 0.25, 1.0));
        }

        private double calculateAimDifficultyRating(double difficultyValue) => DiffUtils.Pow(difficultyValue, 0.63) * 0.02275;

        private double calculateDifficultyRating(double difficultyValue) => Math.Sqrt(difficultyValue) * 0.0675;

        private static double calculateMosuPatternPenalty(double rawPenaltyRatio)
        {
            // The legacy Mosu aim formula already prices ordinary short bursts and streams.
            // Only blend in the modern detector once its map-wide evidence is strong enough;
            // otherwise normal maps receive the same values as the pinned calculator.
            double abuseWeight = DiffUtils.Smoothstep(0.80 - rawPenaltyRatio, 0, 0.65);
            return 1 - 0.65 * abuseWeight;
        }

        private static double calculateMosuAimRatingMultiplier(double circleSize) =>
            1 + Math.Max(Math.Clamp(circleSize, 0, 12) - 4, 0) / 120;

        private static double calculateMosuReadingRatingMultiplier(double circleSize) =>
            1.01 + 0.017 * Math.Clamp(circleSize, 0, 12);

        private static double calculateRelaxStreamWeight(double aimDifficultStrainCount, double speedDifficultStrainCount,
                                                         double aimDifficulty, double speedDifficulty)
        {
            double difficultCountRatio = speedDifficultStrainCount / Math.Max(aimDifficultStrainCount, 0.0001);
            double countDominance = DiffUtils.Smoothstep(difficultCountRatio, 1.75, 2.50);
            double strainRatio = aimDifficulty / Math.Max(speedDifficulty, 0.0001);
            double strainBalance = 1 - DiffUtils.Smoothstep(strainRatio, 1.45, 1.75);

            return Math.Clamp(countDominance * strainBalance, 0, 1);
        }

        private double calculateStarRating(double basePerformance)
        {
            return Math.Cbrt(basePerformance * OsuPerformanceCalculator.PERFORMANCE_BASE_MULTIPLIER);
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            List<DifficultyHitObject> objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                objects.Add(new OsuDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], clockRate, objects, objects.Count));
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            var skills = new List<Skill>
            {
                new Aim(mods, true, applyMappingAntiAbuse),
                new Aim(mods, false, applyMappingAntiAbuse),
                new Speed(mods),
                new Reading(mods)
            };

            // The pinned Mosu/Realistik skills feed only the RX performance calculator
            // and its attributes; they are skipped under the vanilla PP system.
            if (RelaxPpSystemSelection.Current != ForkRelaxPpSystem.LazerVanilla
                && mods.Any(m => m is OsuModRelax or OsuModMosuRelax))
            {
                skills.Add(new MosuRelaxDifficultySkill(mods, MosuRelaxDifficultySkill.SkillKind.Aim, applyMappingAntiAbuse));
                skills.Add(new MosuRelaxDifficultySkill(mods, MosuRelaxDifficultySkill.SkillKind.Speed, applyMappingAntiAbuse));
                skills.Add(new Reading(mods, useMosuRelaxProfile: true));
            }

            if (mods.Any(h => h is OsuModFlashlight))
                skills.Add(new Flashlight(mods, beatmap.HitObjects.Count));

            return skills.ToArray();
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModFlashlight(),
            new OsuModHidden(),
        };
    }
}
