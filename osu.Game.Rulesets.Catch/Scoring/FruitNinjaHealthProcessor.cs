using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Catch.Scoring
{
    /// <summary>
    /// No passive Catch drain: droplets keep their original health weight even though they look like fruit.
    /// </summary>
    public partial class FruitNinjaHealthProcessor : HealthProcessor
    {
        protected override double GetHealthIncreaseFor(JudgementResult result)
        {
            if (result.HitObject is not FruitNinjaFruit fruit)
                return 0;

            if (fruit.IsBomb)
                return result.Type == HitResult.ComboBreak ? -0.08 : 0;

            double weight = fruit.SourceObject switch
            {
                TinyDroplet => 0.002,
                Droplet => 0.008,
                Banana => 0.002,
                _ => 0.025,
            };
            return result.Type switch
            {
                HitResult.Great => weight,
                HitResult.Ok => weight * 0.6,
                HitResult.Meh => weight * 0.3,
                HitResult.Miss => -weight,
                _ => 0,
            };
        }
    }
}
