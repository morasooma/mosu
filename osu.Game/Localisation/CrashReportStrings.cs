using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class CrashReportStrings
    {
        private const string prefix = "osu.Game.Localisation.CrashReport";
        /// <summary>"The previous session crashed"</summary>
        public static LocalisableString Header => new TranslatableString($"{prefix}:Header", "The previous session crashed");
        /// <summary>"Mosu closed unexpectedly. The fatal exception from that session is shown below."</summary>
        public static LocalisableString Body => new TranslatableString($"{prefix}:Body", "Mosu closed unexpectedly. The fatal exception from that session is shown below.");
        /// <summary>"Copy crash report"</summary>
        public static LocalisableString Copy => new TranslatableString($"{prefix}:Copy", "Copy crash report");
        /// <summary>"Crash report copied."</summary>
        public static LocalisableString Copied => new TranslatableString($"{prefix}:Copied", "Crash report copied.");
        /// <summary>"Could not copy the report. Export the logs instead."</summary>
        public static LocalisableString CopyFailed => new TranslatableString($"{prefix}:CopyFailed", "Could not copy the report. Export the logs instead.");
        /// <summary>"Export crash logs"</summary>
        public static LocalisableString Export => new TranslatableString($"{prefix}:Export", "Export crash logs");
        /// <summary>"Saves a ZIP with the exception and available logs from the crashed session."</summary>
        public static LocalisableString ExportHint => new TranslatableString($"{prefix}:ExportHint", "Saves a ZIP with the exception and available logs from the crashed session.");
        /// <summary>"Exporting logs…"</summary>
        public static LocalisableString Exporting => new TranslatableString($"{prefix}:Exporting", "Exporting logs…");
        /// <summary>"Logs exported. Attach the ZIP to your bug report."</summary>
        public static LocalisableString Exported => new TranslatableString($"{prefix}:Exported", "Logs exported. Attach the ZIP to your bug report.");
        /// <summary>"Could not export logs. Check available disk space and try again."</summary>
        public static LocalisableString ExportFailed => new TranslatableString($"{prefix}:ExportFailed", "Could not export logs. Check available disk space and try again.");
        /// <summary>"Report the bug on our Discord server. Attach the log archive and describe what you were doing before the crash."</summary>
        public static LocalisableString DiscordHint => new TranslatableString($"{prefix}:DiscordHint", "Report the bug on our Discord server. Attach the log archive and describe what you were doing before the crash.");
        /// <summary>"Discord server"</summary>
        public static LocalisableString Discord => new TranslatableString($"{prefix}:Discord", "Discord server");
        /// <summary>"Back"</summary>
        public static LocalisableString Back => new TranslatableString($"{prefix}:Back", "Back");
        /// <summary>"Crash the client (test)"</summary>
        public static LocalisableString TriggerCrash => new TranslatableString($"{prefix}:TriggerCrash", "Crash the client (test)");
        /// <summary>"Throws a fatal exception to test the report shown on next startup. Unsaved changes will be lost."</summary>
        public static LocalisableString TriggerCrashHint => new TranslatableString($"{prefix}:TriggerCrashHint", "Throws a fatal exception to test the report shown on next startup. Unsaved changes will be lost.");
        /// <summary>"Crash the client now? Unsaved changes will be lost. Start Mosu again to view the crash report."</summary>
        public static LocalisableString ConfirmCrash => new TranslatableString($"{prefix}:ConfirmCrash", "Crash the client now? Unsaved changes will be lost. Start Mosu again to view the crash report.");
    }
}
