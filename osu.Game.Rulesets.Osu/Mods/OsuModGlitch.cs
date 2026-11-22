using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModGlitch : ModGlitch
    {
        protected override bool IsNeutralObject(HitObject hitObject) => hitObject is Spinner;
    }
}
