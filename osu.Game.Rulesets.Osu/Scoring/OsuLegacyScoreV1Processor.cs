// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Scoring.Legacy;

namespace osu.Game.Rulesets.Osu.Scoring
{
    public sealed class OsuLegacyScoreV1Processor : ILegacyScoreV1Processor
    {
        private readonly Stack<State> states = new Stack<State>();
        private readonly int difficultyMultiplier;
        private readonly double modMultiplier;

        private int combo;

        public long TotalScore { get; private set; }

        public OsuLegacyScoreV1Processor(IWorkingBeatmap workingBeatmap, IReadOnlyList<Mod> mods)
            : this(workingBeatmap.Beatmap, mods)
        {
        }

        public OsuLegacyScoreV1Processor(IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            int drainLength = 0;

            if (beatmap.HitObjects.Count > 0)
            {
                int breakLength = beatmap.Breaks.Select(b => (int)Math.Round(b.EndTime) - (int)Math.Round(b.StartTime)).Sum();
                drainLength = ((int)Math.Round(beatmap.HitObjects[^1].StartTime) - (int)Math.Round(beatmap.HitObjects[0].StartTime) - breakLength) / 1000;
            }

            difficultyMultiplier = LegacyRulesetExtensions.CalculateDifficultyPeppyStars(beatmap.Difficulty, beatmap.HitObjects.Count, drainLength);

            var ruleset = new OsuRuleset();
            modMultiplier = ruleset.CreateLegacyScoreSimulator().GetLegacyScoreMultiplier(mods, new LegacyBeatmapConversionDifficultyInfo
            {
                SourceRuleset = ruleset.RulesetInfo,
            });
        }

        public void ApplyResult(JudgementResult result)
        {
            states.Push(new State(TotalScore, combo));

            switch (result.HitObject)
            {
                case SpinnerBonusTick:
                    if (result.IsHit)
                        TotalScore += 1100;
                    return;

                case SpinnerTick:
                    if (result.IsHit)
                        TotalScore += 100;
                    return;

                case SliderTick:
                    applySliderPart(result, 10);
                    return;

                case SliderHeadCircle:
                case SliderTailCircle:
                case SliderRepeat:
                    applySliderPart(result, 30);
                    return;

                case Slider:
                    applyBasicResult(result, increaseCombo: false);
                    return;

                case Spinner:
                case HitCircle:
                    applyBasicResult(result, increaseCombo: true);
                    return;
            }
        }

        public void RevertResult(JudgementResult result)
        {
            if (states.Count == 0)
                return;

            State state = states.Pop();
            TotalScore = state.TotalScore;
            combo = state.Combo;
        }

        private void applySliderPart(JudgementResult result, int score)
        {
            if (result.IsHit)
            {
                TotalScore += score;
                combo++;
            }
            else if (result.Type.BreaksCombo())
                combo = 0;
        }

        private void applyBasicResult(JudgementResult result, bool increaseCombo)
        {
            int baseScore = getBaseScore(result.Type);

            if (baseScore == 0)
            {
                if (result.Type.BreaksCombo())
                    combo = 0;
                return;
            }

            TotalScore += baseScore;
            TotalScore += (int)(Math.Max(0, combo - 1) * (baseScore / 25 * (difficultyMultiplier * modMultiplier)));

            if (increaseCombo)
                combo++;
        }

        private static int getBaseScore(HitResult result)
        {
            switch (result)
            {
                case HitResult.Great:
                    return 300;

                case HitResult.Ok:
                    return 100;

                case HitResult.Meh:
                    return 50;

                default:
                    return 0;
            }
        }

        private readonly struct State
        {
            public readonly long TotalScore;
            public readonly int Combo;

            public State(long totalScore, int combo)
            {
                TotalScore = totalScore;
                Combo = combo;
            }
        }
    }
}
