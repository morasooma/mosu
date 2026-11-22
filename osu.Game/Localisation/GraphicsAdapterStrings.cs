using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class GraphicsAdapterStrings
    {
        private const string prefix = "osu.Game.Localisation.GraphicsAdapter";

        /// <summary>
        /// "Rendering GPU"
        /// </summary>
        public static LocalisableString Caption => new TranslatableString($"{prefix}:Caption", "Rendering GPU");
        /// <summary>
        /// "Automatic (system default)"
        /// </summary>
        public static LocalisableString Automatic => new TranslatableString($"{prefix}:Automatic", "Automatic (system default)");
        /// <summary>
        /// "Applies after restarting the client. If the GPU is unavailable, automatic selection is used. Choices depend on the active renderer."
        /// </summary>
        public static LocalisableString RestartHint => new TranslatableString($"{prefix}:RestartHint", "Applies after restarting the client. If the GPU is unavailable, automatic selection is used. Choices depend on the active renderer.");
        /// <summary>
        /// "This renderer uses the system GPU selection. On Windows with OpenGL, select the GPU in Windows graphics settings, then restart Mosu. Direct3D 11 and Vulkan support selection here."
        /// </summary>
        public static LocalisableString UnsupportedHint => new TranslatableString($"{prefix}:UnsupportedHint", "This renderer uses the system GPU selection. On Windows with OpenGL, select the GPU in Windows graphics settings, then restart Mosu. Direct3D 11 and Vulkan support selection here.");
        /// <summary>
        /// "Not reported by the renderer"
        /// </summary>
        public static LocalisableString Unknown => new TranslatableString($"{prefix}:Unknown", "Not reported by the renderer");
        /// <summary>
        /// "Currently rendering on: {0}"
        /// </summary>
        public static LocalisableString Active(LocalisableString gpu) => new TranslatableString($"{prefix}:Active", "Currently rendering on: {0}", gpu);
        /// <summary>
        /// "Apply GPU selection and restart"
        /// </summary>
        public static LocalisableString Apply => new TranslatableString($"{prefix}:Apply", "Apply GPU selection and restart");
        /// <summary>
        /// "GPU selection has been saved. Close Mosu now and start it again to apply?"
        /// </summary>
        public static LocalisableString ManualRestart => new TranslatableString($"{prefix}:ManualRestart", "GPU selection has been saved. Close Mosu now and start it again to apply?");
        /// <summary>
        /// "Choose GPU in Windows graphics settings"
        /// </summary>
        public static LocalisableString WindowsSettings => new TranslatableString($"{prefix}:WindowsSettings", "Choose GPU in Windows graphics settings");
    }
}
