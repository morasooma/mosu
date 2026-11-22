using System.ComponentModel;

namespace osu.Game.Rulesets.Osu.Mods
{
    public enum MosuRelaxAlgorithm
    {
        [Description("Mosu"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3652))]
        Mosu = 0,

        [Description("Maple"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3655))]
        Maple = 1,
    }

    // lazer exposes two logical gameplay actions rather than stable's mouse/keyboard bit masks.
    public enum MapleRelaxButton
    {
        [Description("Left / K1"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3656))]
        Left,

        [Description("Right / K2"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3657))]
        Right,
    }
}
