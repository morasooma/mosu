using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class RelaxAlgorithmStrings
    {
        private static LocalisableString text(string key, string fallback)
            => new TranslatableString($"osu.Game.Localisation.RelaxAlgorithm:{key}", fallback);

        public static LocalisableString Algorithm => text("algorithm", "Algorithm");
        public static LocalisableString AlgorithmHint => text("algorithm_hint", "Mosu keeps the existing MRX behaviour. Maple uses an independent tapping algorithm.");
        public static LocalisableString TargetUR => text("target_ur", "Target unstable rate");
        public static LocalisableString Offset => text("offset", "Timing offset (ms)");
        public static LocalisableString CircleHold => text("circle_hold", "Circle hold time (ms)");
        public static LocalisableString CircleError => text("circle_error", "Circle hold variation (ms)");
        public static LocalisableString SliderHold => text("slider_hold", "Slider / spinner hold offset (ms)");
        public static LocalisableString SliderError => text("slider_error", "Slider hold variation (ms)");
        public static LocalisableString AlternateBpm => text("alternate_bpm", "Alternate above BPM");
        public static LocalisableString SliderOverride => text("slider_override", "Slider alternation override");
        public static LocalisableString WaitLate => text("wait_late", "Wait for late aim");
        public static LocalisableString Prediction => text("prediction", "Direction prediction");
        public static LocalisableString PredictionAngle => text("prediction_angle", "Prediction angle (degrees)");
        public static LocalisableString PredictionScale => text("prediction_scale", "Prediction radius scale");
        public static LocalisableString LowestHold => text("lowest_hold", "Use lowest possible hold times");
        public static LocalisableString PrimaryButton => text("primary_button", "Primary button");
        public static LocalisableString SecondaryButton => text("secondary_button", "Secondary button");
    }
}
