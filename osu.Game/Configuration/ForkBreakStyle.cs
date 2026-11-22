using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum ForkBreakStyle
    {
        [LocalisableDescription(typeof(BreakStyleStrings), nameof(BreakStyleStrings.Default))]
        Default,
        [LocalisableDescription(typeof(BreakStyleStrings), nameof(BreakStyleStrings.Legacy))]
        Legacy,
        [LocalisableDescription(typeof(BreakStyleStrings), nameof(BreakStyleStrings.Hidden))]
        Hidden,
    }
}
