// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Difficulty.Skills
{
    public abstract class HarmonicSkill : Skill
    {
        /// <summary>
        /// The sum of object weights, calculated during summation.
        /// Required for any calculations which need to normalise difficulty value.
        /// </summary>
        protected double ObjectWeightSum;

        private readonly List<double> sortedDifficulties = new List<double>();
        private readonly List<double> previousDifficulties = new List<double>();
        private readonly List<double> weights = new List<double>();
        private double cachedScale = double.NaN;
        private double cachedExponent = double.NaN;
        private static readonly IComparer<double> descendingDifficulty = Comparer<double>.Create(static (a, b) => b.CompareTo(a));

        /// <summary>
        /// Scaling factor applied as HarmonicScale / (1 + index) during weight calculations.
        /// A higher value will increase the influence of the hardest object difficulties during summation.
        /// </summary>
        protected virtual double HarmonicScale => 1.0;

        /// <summary>
        /// Exponent that controls the rate of which decay increases as the index increases.
        /// Values closer to 1 decay faster whilst lower values give more weight to lower object difficulties.
        /// </summary>
        protected virtual double DecayExponent => 0.9;

        protected HarmonicSkill(Mod[] mods)
            : base(mods)
        {
        }

        /// <summary>
        /// Returns the difficulty value of the current <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double ObjectDifficultyOf(DifficultyHitObject current);

        protected sealed override double ProcessInternal(DifficultyHitObject current)
            => ObjectDifficultyOf(current);

        /// <summary>
        /// Transforms the object difficulties specifically for final difficulty summation.
        /// This can be used to decrease weight of certain objects based on a skill-specific criteria.
        /// </summary>
        protected virtual List<double> GetTransformedDifficulties(List<double> difficulties) => difficulties;

        public override double DifficultyValue()
        {
            ObjectWeightSum = 0;

            if (ObjectDifficulties.Count == 0)
            {
                previousDifficulties.Clear();
                sortedDifficulties.Clear();
                return 0;
            }

            List<double> difficulties = GetTransformedDifficulties(ObjectDifficulties);
            updateSortedDifficulties(difficulties);

            // Weights depend on rank and skill parameters, not on the strain values.
            // Keep them local to this calculation and preserve the original arithmetic.
            double scale = HarmonicScale;
            double exponent = DecayExponent;
            if (!cachedScale.Equals(scale) || !cachedExponent.Equals(exponent))
            {
                cachedScale = scale;
                cachedExponent = exponent;
                weights.Clear();
            }

            while (weights.Count < sortedDifficulties.Count)
            {
                int rank = weights.Count;
                weights.Add((1 + (scale / (1 + rank))) / (DiffUtils.Pow(rank, exponent) + 1 + (scale / (1 + rank))));
            }

            double difficulty = 0;
            int index = 0;

            // Objects with 0 difficulty are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These objects will not contribute to the difficulty.
            foreach (double obj in sortedDifficulties)
            {
                // Use a harmonic sum that considers each object of the map according to a predefined weight.
                double weight = weights[index];

                ObjectWeightSum += weight;

                difficulty += obj * weight;
                index += 1;
            }

            return difficulty;
        }

        private void updateSortedDifficulties(List<double> difficulties)
        {
            // Timed calculation usually appends one strain. Reading may instead
            // transform every prior value as its memorisation interval grows.
            // Validate that prefix before reusing its order; never assume append-only.
            // A first query or a large batch uses one sort, not quadratic insertion.
            bool append = previousDifficulties.Count > 0
                          && difficulties.Count >= previousDifficulties.Count
                          && difficulties.Count - previousDifficulties.Count <= 32;
            if (append)
            {
                for (int i = 0; i < previousDifficulties.Count; i++)
                {
                    if (!difficulties[i].Equals(previousDifficulties[i]))
                    {
                        append = false;
                        break;
                    }
                }
            }

            int first = append ? previousDifficulties.Count : 0;
            if (!append)
            {
                previousDifficulties.Clear();
                sortedDifficulties.Clear();
            }

            for (int i = first; i < difficulties.Count; i++)
            {
                double value = difficulties[i];
                previousDifficulties.Add(value);
                if (!(value > 0))
                    continue;

                if (append)
                {
                    int insertion = sortedDifficulties.BinarySearch(value, descendingDifficulty);
                    sortedDifficulties.Insert(insertion < 0 ? ~insertion : insertion, value);
                }
                else
                    sortedDifficulties.Add(value);
            }

            if (!append)
                sortedDifficulties.Sort(descendingDifficulty);
        }

        /// <summary>
        /// Calculates the number of object difficulties weighted against the top object difficulty.
        /// </summary>
        public virtual double CountTopWeightedObjectDifficulties(double difficultyValue)
        {
            if (ObjectDifficulties.Count == 0)
                return 0.0;

            if (ObjectWeightSum == 0)
                return 0.0;

            double consistentTopObject = difficultyValue / ObjectWeightSum; // What would the top difficulty be if all object difficulties were identical

            if (consistentTopObject == 0)
                return 0;

            return ObjectDifficulties.Sum(d => DiffUtils.Logistic(d / consistentTopObject, 0.88, 10, 1.1));
        }

        public static double DifficultyToPerformance(double difficulty) => 4.0 * DiffUtils.Pow(difficulty, 3);
    }
}
