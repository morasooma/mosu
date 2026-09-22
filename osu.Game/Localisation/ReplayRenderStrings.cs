using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ReplayRenderStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ReplayRender";

        /// <summary>
        /// "Replay render"
        /// </summary>
        public static LocalisableString Title => new TranslatableString(getKey(@"title"), @"Replay render");
        /// <summary>
        /// "Resolution"
        /// </summary>
        public static LocalisableString Resolution => new TranslatableString(getKey(@"resolution"), @"Resolution");
        /// <summary>
        /// "Framerate"
        /// </summary>
        public static LocalisableString Framerate => new TranslatableString(getKey(@"framerate"), @"Framerate");
        /// <summary>
        /// "Quality preset"
        /// </summary>
        public static LocalisableString QualityPreset => new TranslatableString(getKey(@"quality_preset"), @"Quality preset");
        /// <summary>
        /// "Video bitrate (Mbps)"
        /// </summary>
        public static LocalisableString VideoBitrate => new TranslatableString(getKey(@"video_bitrate"), @"Video bitrate (Mbps)");
        /// <summary>
        /// "Encoder"
        /// </summary>
        public static LocalisableString Encoder => new TranslatableString(getKey(@"encoder"), @"Encoder");
        /// <summary>
        /// "Recommended: detecting preferred encoder..."
        /// </summary>
        public static LocalisableString RecommendedDetecting => new TranslatableString(getKey(@"recommended_detecting"), @"Recommended: detecting preferred encoder...");
        /// <summary>
        /// "Period: Full replay"
        /// </summary>
        public static LocalisableString PeriodFullReplay => new TranslatableString(getKey(@"period_full_replay"), @"Period: Full replay");
        /// <summary>
        /// "Select period"
        /// </summary>
        public static LocalisableString SelectPeriod => new TranslatableString(getKey(@"select_period"), @"Select period");
        /// <summary>
        /// "Force results screen after selected period"
        /// </summary>
        public static LocalisableString ShowResultsAfterPeriod => new TranslatableString(getKey(@"show_results_after_period"), @"Force results screen after selected period");
        /// <summary>
        /// "Start render"
        /// </summary>
        public static LocalisableString StartRender => new TranslatableString(getKey(@"start_render"), @"Start render");
        /// <summary>
        /// "Preparing replay render..."
        /// </summary>
        public static LocalisableString PreparingRender => new TranslatableString(getKey(@"preparing_render"), @"Preparing replay render...");
        /// <summary>
        /// "Replay render finished."
        /// </summary>
        public static LocalisableString RenderFinished => new TranslatableString(getKey(@"render_finished"), @"Replay render finished.");
        /// <summary>
        /// "FFmpeg not found. Click to open the official download page."
        /// </summary>
        public static LocalisableString FFmpegNotFound => new TranslatableString(getKey(@"ffmpeg_not_found"), @"FFmpeg not found. Click to open the official download page.");
        /// <summary>
        /// "After installing FFmpeg, add it to PATH or set FFMPEG_PATH."
        /// </summary>
        public static LocalisableString FFmpegInstallHint => new TranslatableString(getKey(@"ffmpeg_install_hint"), @"After installing FFmpeg, add it to PATH or set FFMPEG_PATH.");
        /// <summary>
        /// "Seek backward 5s (Left Arrow)"
        /// </summary>
        public static LocalisableString SeekBackward => new TranslatableString(getKey(@"seek_backward"), @"Seek backward 5s (Left Arrow)");
        /// <summary>
        /// "Play/Pause (Space)"
        /// </summary>
        public static LocalisableString PlayPause => new TranslatableString(getKey(@"play_pause"), @"Play/Pause (Space)");
        /// <summary>
        /// "Seek forward 5s (Right Arrow)"
        /// </summary>
        public static LocalisableString SeekForward => new TranslatableString(getKey(@"seek_forward"), @"Seek forward 5s (Right Arrow)");
        /// <summary>
        /// "Reset"
        /// </summary>
        public static LocalisableString Reset => new TranslatableString(getKey(@"reset"), @"Reset");
        /// <summary>
        /// "Confirm"
        /// </summary>
        public static LocalisableString Confirm => new TranslatableString(getKey(@"confirm"), @"Confirm");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
