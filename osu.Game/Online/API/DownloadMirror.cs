using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Online.API
{
    public enum DownloadMirror
    {
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.DownloadMirrorAuto))]
        Auto,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.DownloadMirrorSayobot))]
        Sayobot,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.DownloadMirrorNerinyan))]
        Nerinyan,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.DownloadMirrorMino))]
        Mino,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.DownloadMirrorBeatConnect))]
        BeatConnect,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.DownloadMirrorChimu))]
        Chimu,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.DownloadMirrorOsuDirect))]
        OsuDirect,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.DownloadMirrorServer))]
        Server,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.DownloadMirrorHinamizawa))]
        Hinamizawa,

        [Description("Mirria (lisek)"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3777))]
        Mirria,

        [Description("Nekoha"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3780))]
        Nekoha
    }
}
