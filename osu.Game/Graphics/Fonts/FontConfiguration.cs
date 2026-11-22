using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Configuration;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Graphics.Fonts
{
    public sealed class FontConfiguration
    {
        public bool Separate { get; set; } = true;
        public string Common { get; set; } = "Default";
        public string Main { get; set; } = "Default";
        public string Alternate { get; set; } = "Default";
        public string Numeric { get; set; } = "Default";
        public string Inter { get; set; } = "Default";

        public static readonly OsuSetting[] Settings =
        {
            OsuSetting.ForkUseSeparateFonts, OsuSetting.ForkCustomUIFont,
            OsuSetting.ForkMainFont, OsuSetting.ForkAlternateFont, OsuSetting.ForkNumericFont, OsuSetting.ForkInterFont,
        };

        [Newtonsoft.Json.JsonIgnore]
        public IEnumerable<string> Families => new[] { Common, Main, Alternate, Numeric, Inter };
        public bool IsValid() => Families.All(f => f != null && ThemeInterfaceSettings.IsSafeFontName(f));

        public static FontConfiguration Capture(OsuConfigManager config) => new FontConfiguration
        {
            Separate = config.Get<bool>(OsuSetting.ForkUseSeparateFonts),
            Common = config.Get<string>(OsuSetting.ForkCustomUIFont),
            Main = config.Get<string>(OsuSetting.ForkMainFont),
            Alternate = config.Get<string>(OsuSetting.ForkAlternateFont),
            Numeric = config.Get<string>(OsuSetting.ForkNumericFont),
            Inter = config.Get<string>(OsuSetting.ForkInterFont),
        };

        public void Apply(OsuConfigManager config)
        {
            if (!IsValid()) throw new ArgumentException("Invalid font configuration.");
            config.SetValue(OsuSetting.ForkCustomUIFont, Common);
            config.SetValue(OsuSetting.ForkMainFont, Main);
            config.SetValue(OsuSetting.ForkAlternateFont, Alternate);
            config.SetValue(OsuSetting.ForkNumericFont, Numeric);
            config.SetValue(OsuSetting.ForkInterFont, Inter);
            config.SetValue(OsuSetting.ForkUseSeparateFonts, Separate);
        }

        internal static bool IsLegacyAlias(string family) => family is "Torus" or "Torus-Alternate" or "Venera";

        internal static string DisplayFamilyName(string family) => family switch
        {
            "Torus" => "Nunito",
            "Torus-Alternate" => "MosuRoundedAlt / Nunito",
            "Venera" => "Syncopate / Michroma",
            _ => family,
        };

        public string Resolve(string original)
        {
            // Sprite-based skin fonts and icon fonts own their lookup stores.
            string? selected = original switch
            {
                "Torus" => Separate ? Main : Common,
                "Torus-Alternate" => Separate ? Alternate : Common,
                "Venera" => Separate ? Numeric : Common,
                "Inter" => Separate ? Inter : Common,
                _ => null,
            };
            return string.IsNullOrEmpty(selected) || selected == "Default" ? original : selected;
        }
    }
}
