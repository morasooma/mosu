// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Dodge.Scoring
{
    public class DodgeScoreMultiplierCalculator : ScoreMultiplierCalculator
    {
        public DodgeScoreMultiplierCalculator(ScoreMultiplierContext context)
            : base(context)
        {
            Single<DodgeModNoFail>(hasMultiplier: 0.5);
            Single<DodgeModHalfTime>(hasMultiplier: halfTime => rateAdjustMultiplier(halfTime.SpeedChange.Value));
            Single<DodgeModDaycore>(hasMultiplier: daycore => rateAdjustMultiplier(daycore.SpeedChange.Value));

            Single<DodgeModDoubleTime>(hasMultiplier: doubleTime => rateAdjustMultiplier(doubleTime.SpeedChange.Value));
            Single<DodgeModNightcore>(hasMultiplier: nightcore => rateAdjustMultiplier(nightcore.SpeedChange.Value));

            Single<ModWindUp>(hasMultiplier: 0.5);
            Single<ModWindDown>(hasMultiplier: 0.5);
            Single<DodgeModFullPaths>(hasMultiplier: 0.7);
        }

        private static double rateAdjustMultiplier(double speedChange)
        {
            double value = (int)(speedChange * 10) / 10.0 - 1;
            return speedChange >= 1 ? 1 + value / 5 : 0.6 + value;
        }
    }
}
