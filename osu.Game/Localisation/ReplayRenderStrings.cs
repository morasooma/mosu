using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ReplayRenderStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ReplayRender";

        public static LocalisableString Title => new TranslatableString(getKey(@"title"), @"Replay render");
        public static LocalisableString Resolution => new TranslatableString(getKey(@"resolution"), @"Resolution");
        public static LocalisableString Framerate => new TranslatableString(getKey(@"framerate"), @"Framerate");
        public static LocalisableString QualityPreset => new TranslatableString(getKey(@"quality_preset"), @"Quality preset");
        public static LocalisableString VideoBitrate => new TranslatableString(getKey(@"video_bitrate"), @"Video bitrate (Mbps)");
        public static LocalisableString Encoder => new TranslatableString(getKey(@"encoder"), @"Encoder");
        public static LocalisableString RecommendedDetecting => new TranslatableString(getKey(@"recommended_detecting"), @"Recommended: detecting preferred encoder...");
        public static LocalisableString PeriodFullReplay => new TranslatableString(getKey(@"period_full_replay"), @"Period: Full replay");
        public static LocalisableString SelectPeriod => new TranslatableString(getKey(@"select_period"), @"Select period");
        public static LocalisableString ShowResultsAfterPeriod => new TranslatableString(getKey(@"show_results_after_period"), @"Force results screen after selected period");
        public static LocalisableString StartRender => new TranslatableString(getKey(@"start_render"), @"Start render");
        public static LocalisableString PreparingRender => new TranslatableString(getKey(@"preparing_render"), @"Preparing replay render...");
        public static LocalisableString RenderFinished => new TranslatableString(getKey(@"render_finished"), @"Replay render finished.");
        public static LocalisableString FFmpegNotFound => new TranslatableString(getKey(@"ffmpeg_not_found"), @"FFmpeg not found. Click to open the official download page.");
        public static LocalisableString FFmpegInstallHint => new TranslatableString(getKey(@"ffmpeg_install_hint"), @"After installing FFmpeg, add it to PATH or set FFMPEG_PATH.");
        public static LocalisableString SeekBackward => new TranslatableString(getKey(@"seek_backward"), @"Seek backward 5s (Left Arrow)");
        public static LocalisableString PlayPause => new TranslatableString(getKey(@"play_pause"), @"Play/Pause (Space)");
        public static LocalisableString SeekForward => new TranslatableString(getKey(@"seek_forward"), @"Seek forward 5s (Right Arrow)");
        public static LocalisableString Reset => new TranslatableString(getKey(@"reset"), @"Reset");
        public static LocalisableString Confirm => new TranslatableString(getKey(@"confirm"), @"Confirm");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
