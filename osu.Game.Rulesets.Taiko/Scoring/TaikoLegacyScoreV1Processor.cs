using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Scoring.Legacy;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.Taiko.Scoring
{
    /// <summary>
    /// Live legacy scoring with stepped combo bonus and kiai. Strong bonuses are
    /// awarded only when the nested strong judgement succeeds, not merely because
    /// the beatmap contains a strong note.
    /// </summary>
    public sealed class TaikoLegacyScoreV1Processor : ILegacyScoreV1Processor
    {
        private readonly Stack<(long Score, int Combo)> states = new Stack<(long, int)>();
        private readonly Dictionary<JudgementResult, int> strongScores = new Dictionary<JudgementResult, int>();
        private readonly Dictionary<TaikoHitObject, int> parentScores = new Dictionary<TaikoHitObject, int>();
        private readonly IBeatmap playableBeatmap;
        private readonly double comboMultiplier;
        private int combo;

        public long TotalScore { get; private set; }

        public TaikoLegacyScoreV1Processor(IBeatmap sourceBeatmap, IBeatmap playableBeatmap, IReadOnlyList<Mod> mods)
        {
            this.playableBeatmap = playableBeatmap;
            int drainLength = 0;
            if (sourceBeatmap.HitObjects.Count > 0)
            {
                int breakLength = sourceBeatmap.Breaks.Sum(b => (int)Math.Round(b.EndTime) - (int)Math.Round(b.StartTime));
                drainLength = ((int)Math.Round(sourceBeatmap.HitObjects[^1].StartTime) - (int)Math.Round(sourceBeatmap.HitObjects[0].StartTime) - breakLength) / 1000;
            }
            var difficulty = sourceBeatmap.Difficulty.Clone();
            difficulty.CircleSize = 2;
            int stars = LegacyRulesetExtensions.CalculateDifficultyPeppyStars(difficulty, sourceBeatmap.HitObjects.Count, drainLength);
            var ruleset = new TaikoRuleset();
            comboMultiplier = 16 * (stars + 1) * ruleset.CreateLegacyScoreSimulator().GetLegacyScoreMultiplier(mods,
                LegacyBeatmapConversionDifficultyInfo.FromBeatmap(sourceBeatmap));
        }

        public void ApplyResult(JudgementResult result)
        {
            states.Push((TotalScore, combo));
            int increase = 0;
            switch (result.HitObject)
            {
                case StrongNestedHitObject strong:
                    if (result.IsHit)
                        increase = parentScores.GetValueOrDefault(strong.Parent);
                    break;

                case SwellTick:
                    if (result.IsHit)
                        increase = 300;
                    break;

                case DrumRollTick tick:
                    if (result.IsHit)
                        increase = kiai(300, tick.Parent.StartTime);
                    if (result.IsHit && tick.IsStrong)
                        increase += increase / 5;
                    break;

                case Swell swell:
                    if (result.IsHit)
                        increase = kiai(2 * (300 + comboBonus()), swell.EndTime);
                    break;

                case Hit:
                    if (result.IsHit)
                    {
                        int greatScore = 300 + comboBonus();
                        increase = kiai(result.Type == HitResult.Great ? greatScore : greatScore / 2, result.HitObject.StartTime);
                        combo++;
                    }
                    else if (result.Type.BreaksCombo())
                        combo = 0;
                    break;
            }

            if (result.HitObject is TaikoHitObject parent && result.HitObject is not StrongNestedHitObject)
            {
                // Remember the exact amount for a subsequent strong judgement.
                parentScores[parent] = increase;
                strongScores[result] = increase;
            }
            TotalScore += increase;
        }

        public void RevertResult(JudgementResult result)
        {
            if (states.TryPop(out var state))
                (TotalScore, combo) = state;
            if (strongScores.Remove(result) && result.HitObject is TaikoHitObject parent)
                parentScores.Remove(parent);
        }

        private int comboBonus() => (Math.Min(100, combo) / 10) * (int)comboMultiplier;

        private int kiai(int value, double time) => playableBeatmap.ControlPointInfo.EffectPointAt(time).KiaiMode ? (int)(value * 1.2f) : value;
    }
}
