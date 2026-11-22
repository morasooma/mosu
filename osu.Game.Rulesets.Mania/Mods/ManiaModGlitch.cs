using osu.Game.Rulesets.Mods;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Mania.Mods
{
    public class ManiaModGlitch : ModGlitch
    {
        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.ConstantScroll), nameof(SegmentRateModsStrings.ConstantScrollHint), -1)]
        public override BindableBool BakedTiming => base.BakedTiming;
    }
}
