using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BeatmapGroupDeletionStrings
    {
        private const string prefix = "osu.Game.Localisation.BeatmapGroupDeletion";
        /// <summary>"Delete maps in group…"</summary>
        public static LocalisableString Open => new TranslatableString($"{prefix}:open", "Delete maps in group…");
        /// <summary>"Check the maps you want to keep."</summary>
        public static LocalisableString Instructions => new TranslatableString($"{prefix}:instructions", "Check the maps you want to keep.");
        /// <summary>"Cancel"</summary>
        public static LocalisableString Cancel => new TranslatableString($"{prefix}:cancel", "Cancel");
        /// <summary>"Keep"</summary>
        public static LocalisableString Keep => new TranslatableString($"{prefix}:keep", "Keep");
        /// <summary>"Keep all"</summary>
        public static LocalisableString KeepAll => new TranslatableString($"{prefix}:keep_all", "Keep all");
        /// <summary>"Clear selection"</summary>
        public static LocalisableString ClearKept => new TranslatableString($"{prefix}:clear_kept", "Clear selection");
        /// <summary>"Other difficulties outside this group"</summary>
        public static LocalisableString Partial => new TranslatableString($"{prefix}:partial", "Other difficulties outside this group");
        /// <summary>"Protected"</summary>
        public static LocalisableString Protected => new TranslatableString($"{prefix}:protected", "Protected");
        /// <summary>"Read-only"</summary>
        public static LocalisableString ReadOnly => new TranslatableString($"{prefix}:read_only", "Read-only");
        /// <summary>"Unavailable"</summary>
        public static LocalisableString Unavailable => new TranslatableString($"{prefix}:unavailable", "Unavailable");
        /// <summary>"Delete…"</summary>
        public static LocalisableString Continue => new TranslatableString($"{prefix}:continue", "Delete…");
        /// <summary>"Delete maps"</summary>
        public static LocalisableString ConfirmHeader => new TranslatableString($"{prefix}:confirm_header", "Delete maps");
        /// <summary>"{0} difficulties"</summary>
        public static LocalisableString Difficulties(int count) => new TranslatableString($"{prefix}:difficulties", "{0} difficulties", count);
        /// <summary>"Delete: {0} sets. Keep: {2}."</summary>
        public static LocalisableString Summary(int sets, int diffs, int kept) => new TranslatableString($"{prefix}:summary", "Delete: {0} sets. Keep: {2}.", sets, diffs, kept);
        /// <summary>"Delete {0} sets ({1} difficulties)?"</summary>
        public static LocalisableString ConfirmBody(int sets, int diffs) => new TranslatableString($"{prefix}:confirm_body", "Delete {0} sets ({1} difficulties)?", sets, diffs);
        /// <summary>"Deleted {0} sets. Skipped {1} changed or unavailable sets."</summary>
        public static LocalisableString Completed(int deleted, int skipped) => new TranslatableString($"{prefix}:completed", "Deleted {0} sets. Skipped {1} changed or unavailable sets.", deleted, skipped);
    }
}
