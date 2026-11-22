using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SkinElementModStrings
    {
        private const string prefix = "osu.Game.Localisation.SkinElementMod";

        /// <summary>"Hide selected gameplay elements without changing your skin. Can be combined with Hidden."</summary>
        public static LocalisableString Description => new TranslatableString($"{prefix}:description", "Hide selected gameplay elements without changing your skin. Can be combined with Hidden.");
        /// <summary>"Hide combo numbers"</summary>
        public static LocalisableString Numbers => new TranslatableString($"{prefix}:numbers", "Hide combo numbers");
        /// <summary>"Remove numbers from circles and slider heads."</summary>
        public static LocalisableString NumbersHint => new TranslatableString($"{prefix}:numbers_hint", "Remove numbers from circles and slider heads.");
        /// <summary>"Hide follow points"</summary>
        public static LocalisableString FollowPoints => new TranslatableString($"{prefix}:follow_points", "Hide follow points");
        /// <summary>"Remove the connections between consecutive objects."</summary>
        public static LocalisableString FollowPointsHint => new TranslatableString($"{prefix}:follow_points_hint", "Remove the connections between consecutive objects.");
        /// <summary>"Hide slider ball"</summary>
        public static LocalisableString Ball => new TranslatableString($"{prefix}:ball", "Hide slider ball");
        /// <summary>"Remove the moving slider ball. The follow circle is controlled separately."</summary>
        public static LocalisableString BallHint => new TranslatableString($"{prefix}:ball_hint", "Remove the moving slider ball. The follow circle is controlled separately.");
        /// <summary>"Hide slider follow circle"</summary>
        public static LocalisableString FollowCircle => new TranslatableString($"{prefix}:follow_circle", "Hide slider follow circle");
        /// <summary>"Remove the tracking ring around the slider ball."</summary>
        public static LocalisableString FollowCircleHint => new TranslatableString($"{prefix}:follow_circle_hint", "Remove the tracking ring around the slider ball.");
        /// <summary>"Hide reverse arrows"</summary>
        public static LocalisableString Arrows => new TranslatableString($"{prefix}:arrows", "Hide reverse arrows");
        /// <summary>"Remove slider reversal arrows."</summary>
        public static LocalisableString ArrowsHint => new TranslatableString($"{prefix}:arrows_hint", "Remove slider reversal arrows.");
        /// <summary>"Hide slider ticks"</summary>
        public static LocalisableString Ticks => new TranslatableString($"{prefix}:ticks", "Hide slider ticks");
        /// <summary>"Remove visible slider ticks. Ticks are still judged normally."</summary>
        public static LocalisableString TicksHint => new TranslatableString($"{prefix}:ticks_hint", "Remove visible slider ticks. Ticks are still judged normally.");
        /// <summary>"Hide spinners"</summary>
        public static LocalisableString Spinner => new TranslatableString($"{prefix}:spinner", "Hide spinners");
        /// <summary>"Remove the spinner graphic, progress indicators and internal counters. You must still spin."</summary>
        public static LocalisableString SpinnerHint => new TranslatableString($"{prefix}:spinner_hint", "Remove the spinner graphic, progress indicators and internal counters. You must still spin.");
    }
}
