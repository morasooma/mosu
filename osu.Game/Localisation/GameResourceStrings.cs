using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class GameResourceStrings
    {
        private static LocalisableString text(string key, string fallback) => new TranslatableString("osu.Game.Localisation.GameResource:" + key, fallback);
        public static LocalisableString Pack => text("pack", "Game resources");
        public static LocalisableString Default => text("default", "Built-in resources");
        public static LocalisableString Hint => text("hint", "Images, sounds and fonts. Drag a .morars file into the window or open it with Morasooma. Shaders cannot be changed. Applies after restarting.");
        public static LocalisableString Export => text("export", "Export current resources (.morars)");
        public static LocalisableString ExportHint => text("export_hint", "Exports the current resource pack, interface sounds, fonts and licences. Does not include shaders, beatmaps or personal skin libraries.");
        public static LocalisableString Exporting => text("exporting", "Exporting game resources…");
        public static LocalisableString Importing => text("importing", "Checking and importing game resources…");
        public static LocalisableString Imported => text("imported", "Resource pack imported. Restart Morasooma to apply it.");
        public static LocalisableString Exported => text("exported", "Game resources exported. Click to show the .morars file.");
        public static LocalisableString RestartRequired => text("restart", "Resource pack selected. Restart Morasooma to apply it.");
        public static LocalisableString Selected => text("selected", "The current resource pack is selected.");
        public static LocalisableString Busy => text("busy", "A resource operation is already in progress. Please wait for it to finish.");
        public static LocalisableString ImportFailed => text("import_failed", "Could not import resources. The archive may be damaged, incompatible or contain disallowed files. Your current resources have not changed.");
        public static LocalisableString ExportFailed => text("export_failed", "Could not export resources. Check free disk space and that the selected font files are available.");
        public static LocalisableString LoadFailed => text("load_failed", "The selected resource pack could not be loaded. Built-in resources are being used; your imported files were kept.");
    }
}
