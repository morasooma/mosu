using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ServerProfileBuildStrings
    {
        public static LocalisableString Automatic => new TranslatableString("osu.Game.Localisation.ServerProfileBuild:automatic", "Automatically select g0v0 client version and hash");
        public static LocalisableString AutomaticHint => new TranslatableString("osu.Game.Localisation.ServerProfileBuild:automatic_hint", "Uses g0v0's published stable lazer version and hash for your OS. Checks at startup and when selecting this profile; caches for 6 hours. Keeps saved values if unavailable. Turn off for manual input. Applies to the connection after restart.");
    }
}
