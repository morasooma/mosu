using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Mods
{
    public class CatchModFlatline : ModFlatline
    {
        protected override Ruleset CreateRuleset() => new CatchRuleset();
        protected override bool IsNeutralObject(HitObject hitObject) => hitObject is BananaShower;
    }
}
