using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Scoring.Legacy;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    /// <summary>
    /// ScoreV1's base and floating bonus portions. Unlike lazer/ScoreV2, a hold
    /// scores once, using combined head/release timing (osu-wiki Judgement/osu!mania).
    /// The original lazer score is preserved; only the detached submission is adjusted.
    /// </summary>
    public sealed class ManiaLegacyScoreV1Processor : ILegacyScoreV1Processor
    {
        private readonly List<JudgementResult> results = new List<JudgementResult>();
        private readonly Dictionary<HeadNote, HoldNote> heads;
        private readonly Dictionary<TailNote, HoldNote> tails;
        private readonly Dictionary<HoldNote, HoldNoteJudgementResult> holdStates = new Dictionary<HoldNote, HoldNoteJudgementResult>();
        private readonly Dictionary<HitResult, int> statistics = new Dictionary<HitResult, int>();
        private readonly double portion;
        private readonly double punishmentDivider;
        private readonly int objectCount;
        private int maxCombo;
        private double score;
        private double bonus = 100;
        private int combo;
        private readonly Dictionary<HoldNote, JudgementResult> headResults = new Dictionary<HoldNote, JudgementResult>();

        public long TotalScore { get; private set; }

        public ManiaLegacyScoreV1Processor(IBeatmap playableBeatmap, IReadOnlyList<Mod> mods, LegacyBeatmapConversionDifficultyInfo conversionDifficulty)
        {
            var holds = playableBeatmap.HitObjects.OfType<HoldNote>().ToArray();
            heads = holds.Where(h => h.Head != null).ToDictionary(h => h.Head, h => h);
            tails = holds.Where(h => h.Tail != null).ToDictionary(h => h.Tail, h => h);
            objectCount = playableBeatmap.HitObjects.Count(obj => obj is HoldNote or Note);
            double multiplier = new ManiaRuleset().CreateLegacyScoreSimulator().GetLegacyScoreMultiplier(mods, conversionDifficulty);
            portion = objectCount == 0 ? 0 : 500000d * multiplier / objectCount;
            punishmentDivider = 1;
            foreach (var mod in mods)
            {
                punishmentDivider *= mod.Acronym switch
                {
                    "HR" => 1.08,
                    "DT" or "NC" => 1.1,
                    "FI" or "HD" or "FL" => 1.06,
                    _ => 1,
                };
            }
        }

        public void ApplyResult(JudgementResult result)
        {
            results.Add(result);
            // The parent carries hold-drop history, which is not present on tail results.
            if (result is HoldNoteJudgementResult holdResult && result.HitObject is HoldNote hold)
            {
                holdStates[hold] = holdResult;
                recalculate();
            }
            else
                processResult(result);
        }

        public void RevertResult(JudgementResult result)
        {
            results.Remove(result);
            if (result.HitObject is HoldNote hold)
                holdStates.Remove(hold);
            recalculate();
        }

        public void PrepareScoreForSubmission(ScoreInfo score)
        {
            recalculate();
            score.Statistics = new Dictionary<HitResult, int>(statistics);
            score.MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = objectCount };
            score.MaxCombo = maxCombo;
        }

        private void recalculate()
        {
            score = 0;
            bonus = 100;
            combo = 0;
            maxCombo = 0;
            statistics.Clear();
            headResults.Clear();
            foreach (var result in results)
                processResult(result);
            TotalScore = (long)Math.Round(score);
        }

        private void processResult(JudgementResult result)
        {
            if (result.HitObject is not Note || !result.Type.IsBasic())
                return;
            if (result.HitObject is HeadNote head && heads.TryGetValue(head, out var parent))
            {
                headResults[parent] = result;
                return;
            }

            HitResult type = result.Type;
            if (result.HitObject is TailNote tail && tails.TryGetValue(tail, out var hold))
            {
                type = headResults.TryGetValue(hold, out var headResult)
                    ? holdJudgement(headResult, result, hold)
                    : HitResult.Miss;
            }

            statistics[type] = statistics.GetValueOrDefault(type) + 1;
            combo = type == HitResult.Miss ? 0 : combo + 1;
            maxCombo = Math.Max(maxCombo, combo);
            (int value, int bonusValue, double change) = type switch
            {
                HitResult.Perfect => (320, 32, 2),
                HitResult.Great => (300, 32, 1),
                HitResult.Good => (200, 16, -8 / punishmentDivider),
                HitResult.Ok => (100, 8, -24 / punishmentDivider),
                HitResult.Meh => (50, 4, -44 / punishmentDivider),
                _ => (0, 0, double.NegativeInfinity),
            };
            bonus = Math.Clamp(bonus + change, 0, 100);
            score += portion * (value + bonusValue * Math.Sqrt(bonus)) / 320;
            TotalScore = (long)Math.Round(score);
        }

        private HitResult holdJudgement(JudgementResult head, JudgementResult tail, HoldNote hold)
        {
            if (!tail.IsHit)
                return HitResult.Miss;
            if (!head.IsHit || (holdStates.TryGetValue(hold, out var state) && state.DroppedHoldBetween(hold.StartTime, tail.TimeAbsolute)))
                return HitResult.Meh;

            double headError = Math.Abs(head.TimeOffset);
            double combinedError = headError + Math.Abs(tail.TimeOffset);
            foreach (var type in new[] { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Ok })
            {
                double lenience = type == HitResult.Perfect ? 1.2 : type == HitResult.Great ? 1.1 : 1;
                double window = (hold.Head.HitWindows.WindowFor(type) - 0.5) * lenience;
                if (headError <= window && combinedError <= window * 2)
                    return type;
            }
            return HitResult.Meh;
        }
    }
}
