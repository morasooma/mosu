using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Scoring.Legacy;

namespace osu.Game.Rulesets.Catch.Scoring
{
    /// <summary>
    /// Live ScoreV1 counterpart of CatchLegacyScoreSimulator. Base points and
    /// bananas are unmodified; only the fruit combo portion uses the mod multiplier.
    /// </summary>
    public sealed class CatchLegacyScoreV1Processor : ILegacyScoreV1Processor
    {
        private readonly Stack<(long Score, int Combo)> states = new Stack<(long, int)>();
        private readonly double multiplier;
        private int combo;

        private readonly bool fruitNinja;
        private long rawScore;
        public long TotalScore => fruitNinja ? (long)Math.Round(rawScore * 0.1) : rawScore;

        public CatchLegacyScoreV1Processor(IBeatmap sourceBeatmap, IReadOnlyList<Mod> mods)
        {
            fruitNinja = mods.Any(m => m is CatchModFruitNinja);
            int drainLength = 0;
            if (sourceBeatmap.HitObjects.Count > 0)
            {
                int breakLength = sourceBeatmap.Breaks.Sum(b => (int)Math.Round(b.EndTime) - (int)Math.Round(b.StartTime));
                drainLength = ((int)Math.Round(sourceBeatmap.HitObjects[^1].StartTime) - (int)Math.Round(sourceBeatmap.HitObjects[0].StartTime) - breakLength) / 1000;
            }

            var ruleset = new CatchRuleset();
            multiplier = LegacyRulesetExtensions.CalculateDifficultyPeppyStars(sourceBeatmap.Difficulty, sourceBeatmap.HitObjects.Count, drainLength)
                         * ruleset.CreateLegacyScoreSimulator().GetLegacyScoreMultiplier(mods, new LegacyBeatmapConversionDifficultyInfo
                         {
                             SourceRuleset = ruleset.RulesetInfo,
                         });
        }

        public void ApplyResult(JudgementResult result)
        {
            states.Push((rawScore, combo));
            if (!result.IsHit)
            {
                if (result.Type.BreaksCombo())
                    combo = 0;
                return;
            }

            switch (result.HitObject)
            {
                case FruitNinjaFruit { IsBomb: true }:
                    break;

                case FruitNinjaFruit:
                    int baseScore = result.Type == HitResult.Great ? 300 : result.Type == HitResult.Ok ? 100 : 50;
                    rawScore += baseScore + (int)(Math.Max(0, combo - 1) * (baseScore / 25.0 * multiplier));
                    combo++;
                    break;
                case Banana:
                    rawScore += 1100;
                    break;

                case TinyDroplet:
                    rawScore += 10;
                    break;

                case Droplet:
                    rawScore += 100;
                    combo++;
                    break;

                case Fruit:
                    rawScore += 300 + (int)(Math.Max(0, combo - 1) * (12 * multiplier));
                    combo++;
                    break;
            }
        }

        public void RevertResult(JudgementResult result)
        {
            if (states.TryPop(out var state))
                (rawScore, combo) = state;
        }
    }
}
