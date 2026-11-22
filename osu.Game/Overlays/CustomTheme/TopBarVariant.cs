using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>
    /// Supported visual shape and layout variants for the main top bar.
    /// </summary>
    public enum TopBarVariant
    {
        /// <summary>
        /// Edge-to-edge classic flat bar (default).
        /// </summary>
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.TopBarVariantFlat))]
        Flat = 0,

        /// <summary>
        /// Edge-to-edge bar with rounded bottom corners and drop shadow.
        /// </summary>
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.TopBarVariantRounded))]
        Rounded = 1,

        /// <summary>
        /// Modular split-plate bar visually docked to the song select carousel and screen wedges.
        /// </summary>
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.TopBarVariantAttached))]
        Attached = 2,

        /// <summary>
        /// Detached island pill floating at the top of the screen with all-around margins.
        /// </summary>
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.TopBarVariantFloating))]
        Floating = 3,

        /// <summary>
        /// Edge-to-edge bar with flat top edge at Y=0 and rounded bottom corners protruding downward.
        /// </summary>
        [LocalisableDescription(typeof(ForkSettingsStrings), nameof(ForkSettingsStrings.TopBarVariantDownwardRounded))]
        DownwardRounded = 4,
    }
}
