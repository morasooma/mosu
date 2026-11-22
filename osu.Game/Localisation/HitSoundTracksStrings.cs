using osu.Framework.Localisation;
using osu.Game.Audio;

namespace osu.Game.Localisation
{
    public static class HitSoundTracksStrings
    {
        private const string prefix = "osu.Game.Localisation.HitSoundTracks";

        public static LocalisableString Sounds => text("sounds", "Sounds");
        public static LocalisableString Whistle => "Whistle";
        public static LocalisableString Finish => "Finish";
        public static LocalisableString Clap => "Clap";
        public static LocalisableString Bank => text("bank", "Bank");
        public static LocalisableString Volume => text("volume", "Volume");
        public static LocalisableString Object => text("object", "object");
        public static LocalisableString Node(int index) => text("node", "node {0}", index);
        public static LocalisableString SampleTooltip(string name, int node)
            => text("sample_tooltip", "{0} ({1})", SampleName(name), node < 0 ? Object : Node(node));
        public static LocalisableString BankTooltip(string bank)
            => text("bank_tooltip", "Bank: {0}. Click to switch between Normal, Soft and Drum.", BankName(bank));
        public static LocalisableString VolumeTooltip(int volume)
            => text("volume_tooltip", "Volume: {0}%. Click or drag to adjust (5–100%).", volume);

        public static LocalisableString SampleName(string name) => name switch
        {
            HitSampleInfo.HIT_WHISTLE => Whistle,
            HitSampleInfo.HIT_FINISH => Finish,
            HitSampleInfo.HIT_CLAP => Clap,
            _ => name,
        };

        public static LocalisableString BankName(string bank) => bank switch
        {
            HitSampleInfo.BANK_NORMAL => "Normal",
            HitSampleInfo.BANK_SOFT => "Soft",
            HitSampleInfo.BANK_DRUM => "Drum",
            _ => bank,
        };

        private static LocalisableString text(string key, string fallback, params object[] args)
            => new TranslatableString($"{prefix}:{key}", fallback, args);
    }
}
