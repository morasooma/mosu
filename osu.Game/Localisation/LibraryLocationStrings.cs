using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class LibraryLocationStrings
    {
        public static LocalisableString Title => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Title", "Your library");
        public static LocalisableString Current => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Current", "Morasooma is ready. Your library is currently stored here:\n");
        public static LocalisableString Separate => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Separate", "Keep a separate Morasooma library — recommended");
        public static LocalisableString Kept => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Kept", "Keeping the current folder. Click “Continue with this library”.");
        public static LocalisableString SeparateHint => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:SeparateHint", "Start with a fresh library, separate from osu!lazer. Its updates will not affect your Morasooma maps or scores. You can import maps later.");
        public static LocalisableString Lazer => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Lazer", "Use my osu!lazer library");
        public static LocalisableString LazerHint => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:LazerHint", "Use your maps, skins and collections without copying. Both clients will modify the same library: do not run them together. After updating lazer, Morasooma may also need an update.");
        public static LocalisableString Other => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Other", "Or choose an empty folder or an osu!lazer data folder. Do not select your osu!stable installation or its Songs/Skins folders:");
        public static LocalisableString Path => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Path", "Full path to the data folder");
        public static LocalisableString Browse => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Browse", "Browse folders");
        public static LocalisableString UseFolder => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:UseFolder", "Use this folder and restart");
        public static LocalisableString Hint => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Hint", "Not sure? Keep the separate library and click Continue. No files will be moved or deleted.");
        public static LocalisableString NotFound => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:NotFound", "Could not find the osu!lazer library. Open lazer settings, find the data folder location and enter it above.");
        public static LocalisableString Continue => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Continue", "Continue with this library");
        public static LocalisableString InvalidPath => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:InvalidPath", "Enter a full folder path.");
        public static LocalisableString StableFolderNotSupported => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:StableFolderNotSupported", "This is an osu!stable folder. It cannot be used as a Morasooma library. Choose a separate folder or the osu!lazer data folder; import stable maps through settings instead.");
        public static LocalisableString Unavailable => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Unavailable", "Changing folders is unavailable in this environment.");
        public static LocalisableString RestartUnavailable => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:RestartUnavailable", "Automatic restart is unavailable. The folder was not changed.");
        public static LocalisableString Welcome => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Welcome", "Welcome to Morasooma");
        public static LocalisableString Description => new TranslatableString("osu.Game.Resources.Localisation.LibraryLocation:Description", "Choose your library before you start");
    }
}
