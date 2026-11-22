using osu.Framework.Bindables;

namespace osu.Game.Performance
{
    public interface ILinuxGamePriority
    {
        IBindable<LinuxGamePriorityStatus> Status { get; }
    }

    public enum LinuxGamePriorityStatus
    {
        Disabled,
        Applying,
        WaitingForFocus,
        Active,
        CpuOnly,
        GameModeOnly,
        Unavailable,
        RestoreFailed,
    }
}
