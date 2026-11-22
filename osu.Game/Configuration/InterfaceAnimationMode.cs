using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum InterfaceAnimationMode
    {
        [LocalisableDescription(typeof(InterfaceAnimationStrings), nameof(InterfaceAnimationStrings.Full))]
        Full,

        [LocalisableDescription(typeof(InterfaceAnimationStrings), nameof(InterfaceAnimationStrings.Minimal))]
        Minimal,

        [LocalisableDescription(typeof(InterfaceAnimationStrings), nameof(InterfaceAnimationStrings.Disabled))]
        Disabled,
    }
}
