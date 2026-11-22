using osu.Framework.Localisation;
using osu.Game.Performance;

namespace osu.Game.Localisation
{
    public static class LinuxGamePriorityStrings
    {
        private const string prefix = "osu.Game.Localisation.LinuxGamePriority";

        public static LocalisableString Caption => new TranslatableString($"{prefix}:caption", "Linux game priority");
        public static LocalisableString Hint => new TranslatableString($"{prefix}:hint", "While the window is focused, request GameMode and a private systemd scope with 10× the normal CPU weight. This is a relative preference under CPU contention, not reserved cores or real-time scheduling. Requires busctl and a systemd user session; GameMode is optional. It does not prevent RAM, swap, disk or GPU stalls. No root access or system configuration changes. Applied live and released on focus loss, disable or exit.");

        public static LocalisableString Status(LinuxGamePriorityStatus status) => status switch
        {
            LinuxGamePriorityStatus.Applying => new TranslatableString($"{prefix}:applying", "Applying Linux game priority…"),
            LinuxGamePriorityStatus.WaitingForFocus => new TranslatableString($"{prefix}:waiting", "Paused while the game window is unfocused."),
            LinuxGamePriorityStatus.Active => new TranslatableString($"{prefix}:active", "CPU weight 1000 verified; GameMode registered. Individual GameMode optimisations depend on host configuration and permissions."),
            LinuxGamePriorityStatus.CpuOnly => new TranslatableString($"{prefix}:cpu_only", "CPU weight 1000 verified. GameMode is unavailable; install/configure GameMode for additional host optimisations."),
            LinuxGamePriorityStatus.GameModeOnly => new TranslatableString($"{prefix}:gamemode_only", "GameMode registered, but the private CPU scope could not be applied. See the runtime log. GameMode optimisations depend on host configuration and permissions."),
            LinuxGamePriorityStatus.Unavailable => new TranslatableString($"{prefix}:unavailable", "Priority could not be applied. Requires busctl and a systemd user session, or GameMode. See the runtime log."),
            LinuxGamePriorityStatus.RestoreFailed => new TranslatableString($"{prefix}:restore_failed", "Could not fully release game priority. See the runtime log; the session daemons clean up when the process exits."),
            _ => new TranslatableString($"{prefix}:disabled", "Off. Normal CPU weight; any external GameMode request is left untouched."),
        };
    }
}
