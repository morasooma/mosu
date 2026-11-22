using System;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Catch.Scoring
{
    public class FruitNinjaHitWindows : HitWindows
    {
        private double great = 50;
        private double ok = 100;
        private double meh = 150;

        public override bool IsHitResultAllowed(HitResult result) => result is HitResult.Great or HitResult.Ok or HitResult.Meh or HitResult.Miss;

        public override void SetDifficulty(double difficulty)
        {
            difficulty = Math.Clamp(difficulty, 0, 10);
            great = 80 - 6 * difficulty;
            ok = 140 - 8 * difficulty;
            meh = 200 - 10 * difficulty;
        }

        public override double WindowFor(HitResult result) => result switch
        {
            HitResult.Great => great,
            HitResult.Ok => ok,
            HitResult.Meh => meh,
            HitResult.Miss => meh,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };

    }
}
