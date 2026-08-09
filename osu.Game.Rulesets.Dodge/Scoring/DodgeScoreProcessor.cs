// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Dodge.Scoring
{
    public partial class DodgeScoreProcessor : ScoreProcessor
    {
        private bool grazeEnabled;
        private double grazeScore;

        public DodgeScoreProcessor(DodgeRuleset ruleset)
            : base(ruleset)
        {
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            grazeEnabled = DodgeBeatmapSettings.GetGrazeDistance(beatmap.Difficulty) > 0;
            grazeScore = DodgeBeatmapSettings.GetGrazeScore(beatmap.Difficulty);
            base.ApplyBeatmap(beatmap);
        }

        protected override IEnumerable<HitObject> EnumerateHitObjects(IBeatmap beatmap)
        {
            foreach (HitObject hitObject in base.EnumerateHitObjects(beatmap))
            {
                if (grazeEnabled)
                {
                    int grazeCount = hitObject is DodgeEmitter emitter
                        ? emitter.EffectiveBulletCount * emitter.EffectiveBurstCount
                        : hitObject is DodgeBullet ? 1 : 0;

                    for (int i = 0; i < grazeCount; i++)
                    {
                        yield return new DodgeGrazeHitObject
                        {
                            StartTime = hitObject.StartTime,
                            ScoreValue = grazeScore,
                        };
                    }
                }

                yield return hitObject;
            }
        }

        protected override double GetBonusScoreChange(JudgementResult result)
            => result.HitObject is DodgeGrazeHitObject graze ? graze.ScoreValue : base.GetBonusScoreChange(result);
    }
}
