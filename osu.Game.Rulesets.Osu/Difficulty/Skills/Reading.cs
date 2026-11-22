// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Reading : HarmonicSkill
    {
        private int objectCount;
        private int reducedNoteCount;
        private double reducedEndTime;
        private bool reducedPrefixComplete;

        private readonly bool hasHiddenMod;
        private readonly bool isTouchDevice;
        private readonly bool isAutopilot;
        private readonly OsuModMagnetised? magnetised;

        public bool UseMosuRelaxProfile { get; }

        public Reading(Mod[] mods, bool useMosuRelaxProfile = false)
            : base(mods)
        {
            UseMosuRelaxProfile = useMosuRelaxProfile;
            isTouchDevice = mods.Any(m => m is OsuModTouchDevice);
            isAutopilot = mods.Any(m => m is OsuModAutopilot);
            magnetised = mods.OfType<OsuModMagnetised>().FirstOrDefault();
            hasHiddenMod = mods.OfType<OsuModHidden>().Any(m => !m.OnlyFadeApproachCircles.Value);
        }

        private double localDifficulty;
        public override double LocalDifficulty => localDifficulty;

        private double currentStrain;

        private double strainDecay(double ms) => DiffUtils.Pow(0.8, ms / 1000);

        protected override double ObjectDifficultyOf(DifficultyHitObject current)
        {
            const double skill_multiplier = 2.5;

            if (objectCount == 0)
                reducedEndTime = current.StartTime + 60 * 1000;

            objectCount++;
            if (!reducedPrefixComplete)
            {
                // Preserve the original prefix scan's first-stop rule, including
                // callers which process objects out of chronological order.
                if (current.StartTime > reducedEndTime)
                    reducedPrefixComplete = true;
                else
                    reducedNoteCount++;
            }

            double decay = strainDecay(current.DeltaTime);

            currentStrain *= decay;
            double difficulty = calculateAdjustedDifficulty(current);
            currentStrain += difficulty * (1 - decay) * skill_multiplier;
            localDifficulty = difficulty * skill_multiplier;

            return currentStrain;
        }

        private double calculateAdjustedDifficulty(DifficultyHitObject current)
        {
            double difficulty = ReadingEvaluator.EvaluateDifficultyOf(current, hasHiddenMod, UseMosuRelaxProfile);

            if (UseMosuRelaxProfile)
                return difficulty;

            if (isTouchDevice)
                difficulty = DiffUtils.Pow(difficulty, 0.89);

            if (magnetised != null)
            {
                float magnetisedStrength = magnetised.AttractionStrength.Value;
                difficulty *= 1.0 - magnetisedStrength;
            }

            if (isAutopilot)
                difficulty *= 0.1;

            difficulty *= 0.825 + DiffUtils.Pow(Math.Max(0, ((OsuDifficultyHitObject)current).OverallDifficulty), 2.2) / 1125.0;

            return difficulty;
        }

        private readonly List<double> transformedDifficulties = new List<double>();

        protected override List<double> GetTransformedDifficulties(List<double> difficulties)
        {
            transformedDifficulties.Clear();
            foreach (double value in difficulties)
            {
                if (value > 0)
                    transformedDifficulties.Add(value);
            }
            difficulties = transformedDifficulties;

            const double reduced_difficulty_base_line = 0.0; // Assume the first seconds are completely memorised

            int reducedNoteCount = calculateReducedNoteCount();

            for (int i = 0; i < Math.Min(difficulties.Count, reducedNoteCount); i++)
            {
                double scale = Math.Log10(Interpolation.Lerp(1, 10, Math.Clamp((double)i / reducedNoteCount, 0, 1)));
                difficulties[i] *= Interpolation.Lerp(reduced_difficulty_base_line, 1.0, scale);
            }

            return difficulties;
        }

        // The pinned Mosu profile transforms the complete processed prefix; the
        // ordinary profile stops at the first object beyond its initial minute.
        private int calculateReducedNoteCount() => UseMosuRelaxProfile ? objectCount : reducedNoteCount;

        public override double CountTopWeightedObjectDifficulties(double difficultyValue)
        {
            if (ObjectDifficulties.Count == 0)
                return 0.0;

            if (ObjectWeightSum == 0)
                return 0.0;

            double consistentTopNote = difficultyValue / ObjectWeightSum; // What would the top difficulty be if all object difficulties were identical

            if (consistentTopNote == 0)
                return 0;

            return ObjectDifficulties.Sum(d => DiffUtils.Logistic(d / consistentTopNote, 1.15, 5, 1.1));
        }
    }
}
