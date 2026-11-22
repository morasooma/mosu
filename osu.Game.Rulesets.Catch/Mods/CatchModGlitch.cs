using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Mods
{
    public class CatchModGlitch : ModGlitch
    {
        protected override bool IsNeutralObject(HitObject hitObject) => hitObject is BananaShower;
    }
}
