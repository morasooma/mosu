// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// Runtime-only bonus object emitted when a projectile enters the graze area.
    /// </summary>
    public class DodgeGrazeHitObject : DodgeHitObject
    {
        public DodgeGrazeHitObject()
        {
            // Graze is a proximity bonus, not a timing judgement. Runtime graze
            // objects do not pass through beatmap default application, so assign
            // this eagerly as well as overriding CreateHitWindows().
            HitWindows = HitWindows.Empty;
        }

        public double ScoreValue { get; set; }

        public override Judgement CreateJudgement() => new DodgeGrazeJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;

        private class DodgeGrazeJudgement : Judgement
        {
            public override HitResult MaxResult => HitResult.SmallBonus;
        }
    }
}
