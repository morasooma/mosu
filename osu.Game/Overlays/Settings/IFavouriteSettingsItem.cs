using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.Settings
{
    internal interface IFavouriteSettingsItem
    {
        string? FavouriteId { get; }
        LocalisableString FavouriteCaption { get; }
        Container FavouriteSelectionOverlay { get; }
    }
}
