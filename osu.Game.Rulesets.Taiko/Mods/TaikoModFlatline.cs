using osu.Game.Rulesets.Mods;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Taiko.Mods
{
    public class TaikoModFlatline : ModFlatline
    {
        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.ConstantScroll), nameof(SegmentRateModsStrings.ConstantScrollHint), -1)]
        public override BindableBool BakedTiming => base.BakedTiming;
        protected override Ruleset CreateRuleset() => new TaikoRuleset();
        protected override bool IsNeutralObject(HitObject hitObject) => hitObject is Swell;
    }
}
