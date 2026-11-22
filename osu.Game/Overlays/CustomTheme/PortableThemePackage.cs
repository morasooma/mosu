using System;
using Newtonsoft.Json;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>
    /// Portable container package for exporting and importing Morasooma custom themes.
    /// Excludes personal data, account credentials, and system paths.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class PortableThemePackage
    {
        public const string FORMAT_MAGIC = "MorasoomaTheme";
        public const int CURRENT_FORMAT_VERSION = 1;

        /// <summary>
        /// Format identifier to differentiate theme packages from arbitrary JSON files.
        /// </summary>
        [JsonProperty("format")]
        public string Format { get; set; } = FORMAT_MAGIC;

        /// <summary>
        /// Version of the portable theme package envelope.
        /// </summary>
        [JsonProperty("formatVersion")]
        public int FormatVersion { get; set; } = CURRENT_FORMAT_VERSION;

        /// <summary>
        /// User-friendly name of the theme.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "Custom Theme";

        /// <summary>
        /// Optional author attribution name.
        /// </summary>
        [JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
        public string? Author { get; set; }

        /// <summary>
        /// The custom theme definition payload.
        /// </summary>
        [JsonProperty("theme")]
        public CustomThemeDefinition Theme { get; set; } = new CustomThemeDefinition();
    }
}
