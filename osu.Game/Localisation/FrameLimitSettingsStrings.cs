using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class FrameLimitSettingsStrings
    {
        private const string prefix = "osu.Game.Localisation.FrameLimitSettings";

        /// <summary>
        /// "Custom thread limits"
        /// </summary>
        public static LocalisableString CustomLimits => new TranslatableString($"{prefix}:custom_limits", "Custom thread limits");
        /// <summary>
        /// "Set the active rates of Draw, Update, Input and Audio independently. The menu 2× limit overrides Draw and Update only. Background rates are unchanged. Single-threaded mode uses the Update rate for the shared loop. Values are saved when disabled."
        /// </summary>
        public static LocalisableString CustomLimitsHint => new TranslatableString($"{prefix}:custom_limits_hint", "Set the active rates of Draw, Update, Input and Audio independently. The menu 2× limit overrides Draw and Update only. Background rates are unchanged. Single-threaded mode uses the Update rate for the shared loop. Values are saved when disabled.");
        /// <summary>
        /// "Draw (FPS)"
        /// </summary>
        public static LocalisableString DrawLimit => new TranslatableString($"{prefix}:draw_limit", "Draw (FPS)");
        /// <summary>
        /// "Draw thread: rendering frames. Click the value to enter 0–16000; 0 means unlimited."
        /// </summary>
        public static LocalisableString DrawLimitHint => new TranslatableString($"{prefix}:draw_limit_hint", "Draw thread: rendering frames. Click the value to enter 0–16000; 0 means unlimited.");
        /// <summary>
        /// "Update (Hz)"
        /// </summary>
        public static LocalisableString UpdateLimit => new TranslatableString($"{prefix}:update_limit", "Update (Hz)");
        /// <summary>
        /// "Update thread: game logic. Click the value to enter 0–16000; 0 means unlimited. Low rates reduce responsiveness; high rates increase CPU load."
        /// </summary>
        public static LocalisableString UpdateLimitHint => new TranslatableString($"{prefix}:update_limit_hint", "Update thread: game logic. Click the value to enter 0–16000; 0 means unlimited. Low rates reduce responsiveness; high rates increase CPU load.");
        /// <summary>
        /// "Input (Hz)"
        /// </summary>
        public static LocalisableString InputLimit => new TranslatableString($"{prefix}:input_limit", "Input (Hz)");
        /// <summary>
        /// "Input thread: window events and input polling, not the mouse USB polling rate. Overrides the 8000 Hz input option while custom limits are active. Click the value to enter 0–16000; 0 means unlimited."
        /// </summary>
        public static LocalisableString InputLimitHint => new TranslatableString($"{prefix}:input_limit_hint", "Input thread: window events and input polling, not the mouse USB polling rate. Overrides the 8000 Hz input option while custom limits are active. Click the value to enter 0–16000; 0 means unlimited.");
        /// <summary>
        /// "Audio (Hz)"
        /// </summary>
        public static LocalisableString AudioLimit => new TranslatableString($"{prefix}:audio_limit", "Audio (Hz)");
        /// <summary>
        /// "Audio thread: processing audio components, not the audio sample rate. Low rates can delay sound commands. Click the value to enter 0–16000; 0 means unlimited."
        /// </summary>
        public static LocalisableString AudioLimitHint => new TranslatableString($"{prefix}:audio_limit_hint", "Audio thread: processing audio components, not the audio sample rate. Low rates can delay sound commands. Click the value to enter 0–16000; 0 means unlimited.");
        /// <summary>
        /// "Unlimited"
        /// </summary>
        public static LocalisableString Unlimited => new TranslatableString($"{prefix}:unlimited", "Unlimited");
    }
}
