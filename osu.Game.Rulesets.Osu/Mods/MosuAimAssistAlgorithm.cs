using System.ComponentModel;

namespace osu.Game.Rulesets.Osu.Mods
{
    public enum MosuAimAssistAlgorithm
    {
        [Description("Mosu"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3652))]
        Mosu = 0,

        [Description("Maple V2"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3653))]
        MapleV2 = 1,

        [Description("Maple V3"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3654))]
        MapleV3 = 2,
    }
}
