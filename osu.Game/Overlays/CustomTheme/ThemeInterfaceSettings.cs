using System;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Configuration;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>Sparse, portable fork appearance settings. Missing fields leave the user's configuration alone.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ThemeInterfaceSettings
    {
        [JsonProperty("disableShear", NullValueHandling = NullValueHandling.Ignore)] public bool? DisableShear { get; set; }
        [JsonProperty("songSelectStyle", NullValueHandling = NullValueHandling.Ignore)] public ForkSongSelectStyle? SongSelectStyle { get; set; }
        [JsonProperty("oldCarouselPreviews", NullValueHandling = NullValueHandling.Ignore)] public bool? OldCarouselPreviews { get; set; }
        [JsonProperty("animations", NullValueHandling = NullValueHandling.Ignore)] public InterfaceAnimationMode? Animations { get; set; }
        [JsonProperty("enhancedAnimations", NullValueHandling = NullValueHandling.Ignore)] public bool? EnhancedAnimations { get; set; }
        [JsonProperty("overlayTransparency", NullValueHandling = NullValueHandling.Ignore)] public bool? OverlayTransparency { get; set; }
        [JsonProperty("songSelectTransparency", NullValueHandling = NullValueHandling.Ignore)] public bool? SongSelectTransparency { get; set; }
        [JsonProperty("blurStrength", NullValueHandling = NullValueHandling.Ignore)] public double? BlurStrength { get; set; }
        [JsonProperty("dimAmount", NullValueHandling = NullValueHandling.Ignore)] public double? DimAmount { get; set; }
        [JsonProperty("font", NullValueHandling = NullValueHandling.Ignore)] public string? Font { get; set; }

        [JsonProperty("fonts", NullValueHandling = NullValueHandling.Ignore)] public Graphics.Fonts.FontConfiguration? Fonts { get; set; }

        public ThemeInterfaceSettings DeepClone()
        {
            var result = (ThemeInterfaceSettings)MemberwiseClone();
            result.Fonts = Fonts == null ? null : JsonConvert.DeserializeObject<Graphics.Fonts.FontConfiguration>(JsonConvert.SerializeObject(Fonts));
            return result;
        }

        public bool IsValid() => (!SongSelectStyle.HasValue || Enum.IsDefined(SongSelectStyle.Value))
                                && (!Animations.HasValue || Enum.IsDefined(Animations.Value))
                                && validFraction(BlurStrength) && validFraction(DimAmount)
                                && (Font == null || IsSafeFontName(Font)) && (Fonts == null || Fonts.IsValid());

        // A font reference, never an absolute/relative path, URL, or a request to fetch external resources.
        public static bool IsSafeFontName(string font) => font.Length is > 0 and <= 128 && font == font.Trim()
            && font != "." && font != ".." && !Array.Exists(font.ToCharArray(), c => char.IsControl(c) || "/\\:<>\"|?*".Contains(c));

        private static bool validFraction(double? value) => !value.HasValue || double.IsFinite(value.Value) && value.Value >= 0 && value.Value <= 1;

        public static ThemeInterfaceSettings Capture(OsuConfigManager config) => new ThemeInterfaceSettings
        {
            DisableShear = config.Get<bool>(OsuSetting.ForkDisableInterfaceShear),
            SongSelectStyle = config.Get<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle),
            OldCarouselPreviews = config.Get<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews),
            Animations = config.Get<InterfaceAnimationMode>(OsuSetting.ForkInterfaceAnimations),
            EnhancedAnimations = config.Get<bool>(OsuSetting.ForkEnhancedAnimations),
            OverlayTransparency = config.Get<bool>(OsuSetting.ForkOverlayTransparency),
            SongSelectTransparency = config.Get<bool>(OsuSetting.ForkSongSelectTransparency),
            BlurStrength = config.Get<double>(OsuSetting.ForkOverlayBlurStrength),
            DimAmount = config.Get<double>(OsuSetting.ForkOverlayDimAmount),
            Font = IsSafeFontName(config.Get<string>(OsuSetting.ForkCustomUIFont)) ? config.Get<string>(OsuSetting.ForkCustomUIFont) : "Default",
            Fonts = !config.Get<bool>(OsuSetting.ForkUseSeparateFonts)
                    && new[] { OsuSetting.ForkMainFont, OsuSetting.ForkAlternateFont, OsuSetting.ForkNumericFont, OsuSetting.ForkInterFont }
                        .All(s => config.Get<string>(s) == "Default")
                ? null : Graphics.Fonts.FontConfiguration.Capture(config),
        };

        public void Apply(OsuConfigManager config)
        {
            if (!IsValid()) return;
            if (DisableShear.HasValue) config.SetValue(OsuSetting.ForkDisableInterfaceShear, DisableShear.Value);
            if (SongSelectStyle.HasValue) config.SetValue(OsuSetting.ForkSongSelectStyle, SongSelectStyle.Value);
            if (OldCarouselPreviews.HasValue) config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, OldCarouselPreviews.Value);
            if (Animations.HasValue) config.SetValue(OsuSetting.ForkInterfaceAnimations, Animations.Value);
            if (EnhancedAnimations.HasValue) config.SetValue(OsuSetting.ForkEnhancedAnimations, EnhancedAnimations.Value);
            if (OverlayTransparency.HasValue) config.SetValue(OsuSetting.ForkOverlayTransparency, OverlayTransparency.Value);
            if (SongSelectTransparency.HasValue) config.SetValue(OsuSetting.ForkSongSelectTransparency, SongSelectTransparency.Value);
            if (BlurStrength.HasValue) config.SetValue(OsuSetting.ForkOverlayBlurStrength, BlurStrength.Value);
            if (DimAmount.HasValue) config.SetValue(OsuSetting.ForkOverlayDimAmount, DimAmount.Value);
            if (Font != null)
            {
                // Packages do not carry font binaries. An unavailable or unsupported family uses the bundled default.
                bool available = config.IsThemeFontAvailable(Font);
                config.SetValue(OsuSetting.ForkCustomUIFont, available ? Font : "Default");
                if (Fonts == null) config.SetValue(OsuSetting.ForkUseSeparateFonts, false);
            }
            if (Fonts != null)
            {
                var selected = JsonConvert.DeserializeObject<Graphics.Fonts.FontConfiguration>(JsonConvert.SerializeObject(Fonts))!;
                string safe(string value) => config.IsThemeFontAvailable(value) ? value : "Default";
                selected.Common = safe(selected.Common); selected.Main = safe(selected.Main);
                selected.Alternate = safe(selected.Alternate); selected.Numeric = safe(selected.Numeric); selected.Inter = safe(selected.Inter);
                selected.Apply(config);
            }
        }
    }
}
