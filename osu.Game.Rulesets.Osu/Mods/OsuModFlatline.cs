using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModFlatline : ModFlatline
    {
        protected override Ruleset CreateRuleset() => new OsuRuleset();
        protected override bool IsNeutralObject(HitObject hitObject) => hitObject is Spinner;
    }
}
