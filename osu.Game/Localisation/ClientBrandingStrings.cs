using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    /// <summary>Client-specific copy, without changing external service names or licence credits.</summary>
    public static class ClientBrandingStrings
    {
        private static LocalisableString text(string key, string fallback, params object[] args) =>
            new TranslatableString("osu.Game.Localisation.ClientBranding:" + key, fallback, args);

        public static LocalisableString Website => text("website", "Morasooma website");
        public static LocalisableString WebsiteHint => text("website_hint", "Open the Morasooma website.");
        public static LocalisableString ExistingLibrary => text("existing_library", "This folder already contains a game library. Use its data instead? Morasooma will restart.");
        public static LocalisableString BrowseMaps(LocalisableString keybind) => text("browse_maps", "The beatmap catalogue is available to everyone. Open it from anywhere in the game with {0}!", keybind);
        public static LocalisableString ResourcePacks => text("resource_packs", "You can export your current images, sounds and fonts as a .morars resource pack from the interface settings.");
        public static LocalisableString LegacyScoreV2 => text("legacy_score_v2", "Legacy score recorded with the V2 scoring algorithm.");
        public static LocalisableString MatchmakingNotice => text("matchmaking_notice", "Matchmaking is under active development. Follow Morasooma news and share feedback with the community.");
        public static LocalisableString News => text("news", "Morasooma news");
        public static LocalisableString Community => text("community", "Community and feedback");
        public static LocalisableString ClassicBehaviour => text("classic_behaviour", "Try the current default settings first. If you prefer a more classic feel, you can apply the classic defaults below.");
    }
}
