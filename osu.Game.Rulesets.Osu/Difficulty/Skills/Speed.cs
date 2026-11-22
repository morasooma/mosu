// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : HarmonicSkill
    {
        private readonly List<double> sliderStrains = new List<double>();

        private double localDifficulty;
        public override double LocalDifficulty => localDifficulty;

        private double currentStrain;
        private double currentRhythm = 1;
        private readonly bool isAutopilot;
        private double maxEndTime;

        protected override double HarmonicScale => 20;
        protected override double DecayExponent => 0.9;

        public Speed(Mod[] mods)
            : base(mods)
        {
            isAutopilot = mods.Any(m => m is OsuModAutopilot);
        }

        private double strainDecay(double ms) => DiffUtils.Pow(0.3, ms / 1000);

        protected override double ObjectDifficultyOf(DifficultyHitObject current)
        {
            const double skill_multiplier = 1.16;

            // Concurrent objects must not create artificial speed strain.
            bool isOverlapping = current.StartTime < maxEndTime - 1;
            maxEndTime = Math.Max(maxEndTime, current.EndTime);

            double decay = strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            currentStrain *= decay;

            localDifficulty = 0;
            if (!isOverlapping)
            {
                double difficulty = calculateAdjustedDifficulty(current);
                currentStrain += difficulty * (1 - decay) * skill_multiplier;
                currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);
                localDifficulty = difficulty * skill_multiplier * currentRhythm;
            }

            double totalStrain = currentStrain * currentRhythm;

            if (current.BaseObject is Slider && !isOverlapping)
                sliderStrains.Add(totalStrain);

            return totalStrain;
        }

        private double calculateAdjustedDifficulty(DifficultyHitObject current)
        {
            double difficulty = SpeedEvaluator.EvaluateDifficultyOf(current);

            if (isAutopilot)
                difficulty *= 0.5;

            return difficulty;
        }

        public double RelevantObjectCount()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            double maxStrain = ObjectDifficulties.Max();

            if (maxStrain == 0)
                return 0;

            return ObjectDifficulties.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            if (sliderStrains.Count == 0)
                return 0;

            if (ObjectWeightSum == 0)
                return 0.0;

            double consistentTopObject = difficultyValue / ObjectWeightSum; // What would the top note be if all note values were identical

            if (consistentTopObject == 0)
                return 0;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DiffUtils.Logistic(s / consistentTopObject, 0.88, 10, 1.1));
        }
    }
}
