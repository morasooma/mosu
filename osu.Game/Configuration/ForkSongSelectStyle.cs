// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Which visual style the solo song select screen uses.
    /// </summary>
    public enum ForkSongSelectStyle
    {
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.SongSelectStyleModern))]
        Modern,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.SongSelectStyleClassic2024))]
        Classic2024,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.SongSelectStyleLegacySkinned))]
        LegacySkinned,

        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.SongSelectStyleInfiniteGlass))]
        InfiniteGlass,
    }

    public static class ForkSongSelectStyleExtensions
    {
        /// <summary>
        /// Whether this style uses the restored 2024-era (v1) song select screen.
        /// The skinned legacy style is hosted by the modern song select implementation.
        /// </summary>
        public static bool UsesV1Screen(this ForkSongSelectStyle style) => style == ForkSongSelectStyle.Classic2024;

        /// <summary>
        /// Whether the Torii stable-style chrome is active around the regular song select carousel.
        /// </summary>
        public static bool UsesStableStyle(this ForkSongSelectStyle style) => style == ForkSongSelectStyle.LegacySkinned;

        /// <summary>
        /// Whether stable-style presentation is valid for the current screen. Online song selects
        /// have their own header and footer actions and must retain the regular layout.
        /// </summary>
        public static bool UsesStableStyleOn(this ForkSongSelectStyle style, bool screenSupportsStableStyle)
            => screenSupportsStableStyle && style.UsesStableStyle();

        /// <summary>
        /// The song-select logo remains fully opaque. Stable-style occlusion is achieved through depth ordering,
        /// not by fading the logo itself.
        /// </summary>
        public static float GetSongSelectLogoAlpha(this ForkSongSelectStyle style) => 1f;

        /// <summary>
        /// Whether song select is presented as a pannable, zoomable two-dimensional map of difficulties.
        /// </summary>
        public static bool UsesInfiniteGlass(this ForkSongSelectStyle style) => style == ForkSongSelectStyle.InfiniteGlass;

        /// <summary>
        /// Whether the stable-style carousel should use the current skin's
        /// menu-button-background texture.
        /// </summary>
        public static bool UsesSkinnedLegacyCarousel(this ForkSongSelectStyle style) => style == ForkSongSelectStyle.LegacySkinned;
    }

    /// <summary>
    /// Helper bindings that expose <see cref="ForkSongSelectStyle"/> as derived booleans
    /// for consumers which only care about one aspect of the style.
    /// </summary>
    public static class ForkSongSelectStyleBinding
    {
        /// <summary>
        /// Keeps <paramref name="target"/> in sync with whether the configured style uses
        /// the skinned stable-look (legacy) carousel presentation.
        /// </summary>
        public static void BindSkinnedLegacyCarousel(OsuConfigManager config, BindableBool target, Func<bool>? supportsStableStyle = null)
        {
            var style = config.GetBindable<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle);
            style.BindValueChanged(v => target.Value = v.NewValue.UsesStableStyleOn(supportsStableStyle?.Invoke() ?? true), true);
        }
    }
}
