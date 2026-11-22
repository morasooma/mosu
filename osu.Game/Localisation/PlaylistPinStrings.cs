using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class PlaylistPinStrings
    {
        private const string prefix = "osu.Game.Localisation.PlaylistPin";

        public static LocalisableString Pin => new TranslatableString($"{prefix}:pin", "Pin playlist");
        public static LocalisableString Unpin => new TranslatableString($"{prefix}:unpin", "Unpin playlist");
        public static LocalisableString UpdateFailed => new TranslatableString($"{prefix}:update_failed", "Couldn't update the playlist pin. Try again.");
    }
}
