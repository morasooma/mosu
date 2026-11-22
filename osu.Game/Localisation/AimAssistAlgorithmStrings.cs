using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class AimAssistAlgorithmStrings
    {
        private static LocalisableString text(string key, string fallback)
            => new TranslatableString($"osu.Game.Localisation.AimAssistAlgorithm:{key}", fallback);

        public static LocalisableString Algorithm => text("algorithm", "Algorithm");
        public static LocalisableString AlgorithmHint => text("algorithm_hint", "Mosu uses the existing assist. Maple V2 and V3 use independent movement algorithms.");
        public static LocalisableString V2Power => text("v2_power", "Maple V2 power");
        public static LocalisableString V2PowerHint => text("v2_power_hint", "Controls capture radius, smoothing and maximum cursor offset.");
        public static LocalisableString Sliders => text("sliders", "Assist on sliders");
        public static LocalisableString SlidersHint => text("sliders_hint", "Follow the active slider path with Maple V2. When disabled, only the head is assisted.");
        public static LocalisableString V3Power => text("v3_power", "Maple V3 power");
        public static LocalisableString V3PowerHint => text("v3_power_hint", "Controls movement-driven correction and maximum cursor offset.");
        public static LocalisableString SliderPower => text("slider_power", "Maple V3 slider power");
        public static LocalisableString SliderPowerHint => text("slider_power_hint", "Multiplier for correction while following a slider. Zero disables correction after its start.");
        public static LocalisableString ShowRealCursor => text("show_real_cursor", "Show real cursor");
        public static LocalisableString ShowRealCursorHint => text("show_real_cursor_hint", "Show a small red circle at the physical mouse position, above the gameplay cursor, without debug overlays.");
    }
}
