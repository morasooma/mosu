using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SegmentRateModsStrings
    {
        private const string prefix = "osu.Game.Localisation.SegmentRateMods";
        /// <summary>"Distort the rhythm of the music and beatmap together without changing pitch. Distortions repeat on every play."</summary>
        public static LocalisableString GlitchDescription => new TranslatableString($"{prefix}:glitch_description", "Distort the rhythm of the music and beatmap together without changing pitch. Distortions repeat on every play.");
        /// <summary>"Bring sections closer in difficulty by adjusting music tempo and note rhythm without changing pitch."</summary>
        public static LocalisableString FlatlineDescription => new TranslatableString($"{prefix}:flatline_description", "Bring sections closer in difficulty by adjusting music tempo and note rhythm without changing pitch.");
        /// <summary>"Constant AR"</summary>
        public static LocalisableString BakedTiming => new TranslatableString($"{prefix}:baked_timing", "Constant AR");
        /// <summary>"Keep effective AR unchanged when tempo changes. When disabled, effective AR follows tempo."</summary>
        public static LocalisableString BakedTimingHint => new TranslatableString($"{prefix}:baked_timing_hint", "Keep effective AR unchanged when tempo changes. When disabled, effective AR follows tempo.");
        /// <summary>"Constant scroll speed"</summary>
        public static LocalisableString ConstantScroll => new TranslatableString($"{prefix}:constant_scroll", "Constant scroll speed");
        /// <summary>"Tempo changes do not affect scroll speed. The map's own scroll speed changes are preserved."</summary>
        public static LocalisableString ConstantScrollHint => new TranslatableString($"{prefix}:constant_scroll_hint", "Tempo changes do not affect scroll speed. The map's own scroll speed changes are preserved.");
        /// <summary>"Constant appearance time"</summary>
        public static LocalisableString ConstantAppearance => new TranslatableString($"{prefix}:constant_appearance", "Constant appearance time");
        /// <summary>"Tempo changes affect object timings without speeding up the entire gameplay clock."</summary>
        public static LocalisableString ConstantAppearanceHint => new TranslatableString($"{prefix}:constant_appearance_hint", "Tempo changes affect object timings without speeding up the entire gameplay clock.");
        /// <summary>"Changed segments (%)"</summary>
        public static LocalisableString Amount => new TranslatableString($"{prefix}:amount", "Changed segments (%)");
        /// <summary>"Probability of changing each eligible quarter-beat segment."</summary>
        public static LocalisableString AmountHint => new TranslatableString($"{prefix}:amount_hint", "Probability of changing each eligible quarter-beat segment.");
        /// <summary>"Distortion strength"</summary>
        public static LocalisableString Strength => new TranslatableString($"{prefix}:strength", "Distortion strength");
        /// <summary>"1 applies the full subdivision change; 0 leaves the original tempo."</summary>
        public static LocalisableString StrengthHint => new TranslatableString($"{prefix}:strength_hint", "1 applies the full subdivision change; 0 leaves the original tempo.");
        /// <summary>"Allow 1/3"</summary>
        public static LocalisableString Thirds => new TranslatableString($"{prefix}:thirds", "Allow 1/3");
        /// <summary>"Allow quarter-beat segments to become thirds (0.75x)."</summary>
        public static LocalisableString ThirdsHint => new TranslatableString($"{prefix}:thirds_hint", "Allow quarter-beat segments to become thirds (0.75x).");
        /// <summary>"Allow 1/5"</summary>
        public static LocalisableString Fifths => new TranslatableString($"{prefix}:fifths", "Allow 1/5");
        /// <summary>"Allow quarter-beat segments to become fifths (1.25x)."</summary>
        public static LocalisableString FifthsHint => new TranslatableString($"{prefix}:fifths_hint", "Allow quarter-beat segments to become fifths (1.25x).");
        /// <summary>"Allow 1/6"</summary>
        public static LocalisableString Sixths => new TranslatableString($"{prefix}:sixths", "Allow 1/6");
        /// <summary>"Allow quarter-beat segments to become sixths (1.5x)."</summary>
        public static LocalisableString SixthsHint => new TranslatableString($"{prefix}:sixths_hint", "Allow quarter-beat segments to become sixths (1.5x).");
        /// <summary>"Equalization direction"</summary>
        public static LocalisableString Direction => new TranslatableString($"{prefix}:direction", "Equalization direction");
        /// <summary>"Harder: original peak; easier: easiest active block; both: intermediate target."</summary>
        public static LocalisableString DirectionHint => new TranslatableString($"{prefix}:direction_hint", "Harder: original peak; easier: easiest active block; both: intermediate target.");
        /// <summary>"Maximum speed"</summary>
        public static LocalisableString MaxRate => new TranslatableString($"{prefix}:max_rate", "Maximum speed");
        /// <summary>"Acceleration limit. Original peaks are never slowed in harder mode."</summary>
        public static LocalisableString MaxRateHint => new TranslatableString($"{prefix}:max_rate_hint", "Acceleration limit. Original peaks are never slowed in harder mode.");
        /// <summary>"Minimum speed"</summary>
        public static LocalisableString MinRate => new TranslatableString($"{prefix}:min_rate", "Minimum speed");
        /// <summary>"Slowdown limit. Easy passages are never accelerated in easier mode."</summary>
        public static LocalisableString MinRateHint => new TranslatableString($"{prefix}:min_rate_hint", "Slowdown limit. Easy passages are never accelerated in easier mode.");
        /// <summary>"Harder"</summary>
        public static LocalisableString Harder => new TranslatableString($"{prefix}:harder", "Harder");
        /// <summary>"Easier"</summary>
        public static LocalisableString Easier => new TranslatableString($"{prefix}:easier", "Easier");
        /// <summary>"Both directions"</summary>
        public static LocalisableString Both => new TranslatableString($"{prefix}:both", "Both directions");
    }
}
