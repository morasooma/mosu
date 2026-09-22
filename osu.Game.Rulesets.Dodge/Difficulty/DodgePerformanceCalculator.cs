// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Dodge.Difficulty
{
    /// <summary>
    /// Converts Dodge's object-derived difficulty into survival performance.
    /// Grazes and total score are intentionally excluded: only successfully
    /// surviving relevant patterns contribute to performance.
    /// </summary>
    public class DodgePerformanceCalculator : PerformanceCalculator
    {
        private const double star_offset = 0.15;
        private const double performance_divisor = 130000;
        private const double skill_weight_exponent = 1.5;
        private const double full_length_effective_patterns = 100;
        private const double difficult_section_pattern_equivalent = 2;

        public DodgePerformanceCalculator()
            : base(new DodgeRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var dodgeAttributes = (DodgeDifficultyAttributes)attributes;
            int perfectCount = Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Perfect));
            int missCount = Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Miss));
            int gameplayJudgementCount = perfectCount + missCount;
            double effectiveStars = finiteNonNegative(dodgeAttributes.StarRating - star_offset);

            if (effectiveStars <= 0 || gameplayJudgementCount == 0)
                return new DodgePerformanceAttributes { EffectiveMissCount = missCount };

            // The score's raw accuracy can be diluted by simultaneous bullets or
            // decorative gameplay judgements. Pattern accuracy uses the grouped,
            // route-relevant decisions measured by difficulty calculation and
            // takes the stricter of the two views.
            double judgementAccuracy = (double)perfectCount / gameplayJudgementCount;
            int relevantPatterns = Math.Max(1, dodgeAttributes.RelevantPatternCount);
            double patternAccuracy = Math.Clamp(1 - (double)missCount / relevantPatterns, 0, 1);
            double effectiveAccuracy = Math.Min(judgementAccuracy, patternAccuracy);

            double difficultSections = finiteNonNegative(dodgeAttributes.DifficultSectionCount);
            double enduranceBonus = 1 + 0.2 * difficultSections / (difficultSections + 30);
            double effectivePatternCount = finiteNonNegative(dodgeAttributes.EffectiveReadingPatternCount);

            if (effectivePatternCount <= 0)
            {
                // Older difficulty attributes do not contain the urgency-weighted
                // count. Fall back to grouped patterns when they are available,
                // and avoid silently nerfing cached star-only attributes.
                effectivePatternCount = dodgeAttributes.RelevantPatternCount > 0
                    ? dodgeAttributes.RelevantPatternCount
                    : full_length_effective_patterns;
            }

            // PP represents the amount of demonstrated performance, not only the
            // hardest instant. Urgency-weighted pattern onsets measure meaningful
            // decisions without rewarding decorative bullet spam. Sustained hard
            // sections are an alternate path to full length for long continuous
            // patterns which only have a small number of authored onsets.
            double demonstratedLength = Math.Max(
                effectivePatternCount,
                difficultSections * difficult_section_pattern_equivalent);
            double lengthFactor = Math.Sqrt(Math.Clamp(
                demonstratedLength / full_length_effective_patterns,
                0,
                1));
            double basePerformance = Math.Pow(5 * Math.Max(1, effectiveStars / 0.0675) - 4, 3)
                                     / performance_divisor;
            double fullComboPerformance = basePerformance * enduranceBonus * lengthFactor;

            double perMissRetention = 0.78 + 0.12 * difficultSections / (difficultSections + 20);
            // A collision is already represented by pattern accuracy and combo.
            // Applying the full retention once per miss made ordinary imperfect
            // clears collapse exponentially (11 misses on Dead Air retained only
            // 17% before the other penalties). Sublinear severity keeps misses
            // meaningful without making every non-FC score nearly worthless.
            double missFactor = Math.Pow(perMissRetention, Math.Sqrt(missCount));
            double comboRatio = dodgeAttributes.MaxCombo <= 0
                ? 1
                : Math.Clamp((double)score.MaxCombo / dodgeAttributes.MaxCombo, 0, 1);
            comboRatio = Math.Min(comboRatio, patternAccuracy);
            // Combo is deliberately a secondary consistency signal. Giving it
            // osu!-style dominance would make raw judgement padding profitable.
            double comboFactor = 0.8 + 0.2 * Math.Sqrt(comboRatio);
            double completionRatio = dodgeAttributes.MaxCombo <= 0
                ? 1
                : Math.Clamp((double)gameplayJudgementCount / dodgeAttributes.MaxCombo, 0, 1);
            double completionFactor = Math.Pow(completionRatio, 0.8);
            double scoreQuality = Math.Pow(effectiveAccuracy, 2)
                                  * missFactor
                                  * comboFactor
                                  * completionFactor;

            if (score.Mods.Any(mod => mod is DodgeModNoFail))
                scoreQuality *= Math.Max(0.9, 1 - 0.02 * missCount);

            double movementWeight = Math.Pow(finiteNonNegative(dodgeAttributes.MovementDifficulty), skill_weight_exponent);
            double pathWeight = Math.Pow(finiteNonNegative(dodgeAttributes.PathDifficulty), skill_weight_exponent);
            double readingWeight = Math.Pow(finiteNonNegative(dodgeAttributes.ReadingDifficulty), skill_weight_exponent);
            double totalWeight = movementWeight + pathWeight + readingWeight;

            if (totalWeight <= 0 || !double.IsFinite(scoreQuality))
            {
                return new DodgePerformanceAttributes
                {
                    EffectiveAccuracy = effectiveAccuracy,
                    EffectiveMissCount = missCount,
                    EnduranceBonus = enduranceBonus,
                    LengthFactor = lengthFactor,
                };
            }

            double achievedPerformance = fullComboPerformance * scoreQuality;
            double movementPerformance = achievedPerformance * movementWeight / totalWeight;
            double pathPerformance = achievedPerformance * pathWeight / totalWeight;
            double readingPerformance = achievedPerformance * readingWeight / totalWeight;

            return new DodgePerformanceAttributes
            {
                Total = movementPerformance + pathPerformance + readingPerformance,
                Movement = movementPerformance,
                Path = pathPerformance,
                Reading = readingPerformance,
                EffectiveAccuracy = effectiveAccuracy,
                EffectiveMissCount = missCount,
                EnduranceBonus = enduranceBonus,
                LengthFactor = lengthFactor,
            };
        }

        private static double finiteNonNegative(double value)
            => double.IsFinite(value) ? Math.Max(0, value) : 0;
    }
}
