using System.ComponentModel;

namespace osu.Game.Online.API
{
    // Separate from archive mirrors: a search session must keep one provider's pagination.
    public enum BeatmapCatalogueSource
    {
        [Description("Auto (резервирование, не объединение)"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3776))]
        Auto,
        Nekoha,
        [Description("Mirria (lisek)"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3777))]
        Mirria,
        [Description("osu! (official)"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3778))]
        Official,
        [Description("osu.direct"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3779))]
        OsuDirect,
        Hinamizawa,
    }
}
