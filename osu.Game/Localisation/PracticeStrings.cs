using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class PracticeStrings
    {
        private const string prefix = "osu.Game.Localisation.Practice";

        public static LocalisableString SettingCaption => new TranslatableString($"{prefix}:setting_caption", "Enable practice in solo play");
        public static LocalisableString SettingHint => new TranslatableString($"{prefix}:setting_hint", "Hold Shift alone, then select a time with a mouse click or a gameplay key, or continue after failing. Practice is invincible and scores are not saved or submitted.");
        public static LocalisableString Continue => new TranslatableString($"{prefix}:continue", "Continue from here · practice");
        public static LocalisableString SelectTime => new TranslatableString($"{prefix}:select_time", "Practice — select with mouse or gameplay key · score will not be submitted");
        public static LocalisableString Time(string time) => new TranslatableString($"{prefix}:time", "Practice · {0} · score will not be submitted", time);
    }
}
