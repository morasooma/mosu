using System;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Performance;

namespace osu.Desktop.Linux
{
    public partial class LinuxGamePriority : Component, ILinuxGamePriority
    {
        private readonly Bindable<LinuxGamePriorityStatus> status = new Bindable<LinuxGamePriorityStatus>();
        public IBindable<LinuxGamePriorityStatus> Status => status;

        private readonly LinuxGamePriorityBackend backend = new LinuxGamePriorityBackend(Environment.ProcessId);
        private Bindable<bool> enabled = null!;
        private IBindable<bool> active = null!;
        private readonly object workLock = new object();
        private Task pending = Task.CompletedTask;
        private int revision;
        private bool shuttingDown;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, GameHost host)
        {
            enabled = config.GetBindable<bool>(OsuSetting.ForkLinuxGamePriority);
            active = host.IsActive.GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            enabled.BindValueChanged(_ => updatePolicy());
            active.BindValueChanged(_ => Schedule(updatePolicy));
            updatePolicy();
        }

        private void updatePolicy()
        {
            bool requested = enabled.Value && active.Value;
            LinuxGamePriorityStatus inactiveStatus = enabled.Value ? LinuxGamePriorityStatus.WaitingForFocus : LinuxGamePriorityStatus.Disabled;
            status.Value = LinuxGamePriorityStatus.Applying;
            queuePolicy(requested, inactiveStatus);
        }

        private void queuePolicy(bool requested, LinuxGamePriorityStatus inactiveStatus)
        {
            lock (workLock)
            {
                int currentRevision = ++revision;
                Task previous = pending;
                pending = Task.Run(async () =>
                {
                    // D-Bus can block: no OS calls on Input/Update/Draw, and no overlapping start/end.
                    await previous.ConfigureAwait(false);
                    lock (workLock)
                    {
                        if (currentRevision != revision)
                            return;
                    }
                    LinuxGamePriorityStatus result;
                    try
                    {
                        if (requested)
                        {
                            var applied = await backend.ApplyAsync().ConfigureAwait(false);
                            result = (applied.Cpu, applied.GameMode) switch
                            {
                                (true, true) => LinuxGamePriorityStatus.Active,
                                (true, false) => LinuxGamePriorityStatus.CpuOnly,
                                (false, true) => LinuxGamePriorityStatus.GameModeOnly,
                                _ => LinuxGamePriorityStatus.Unavailable,
                            };
                        }
                        else
                            result = await backend.RestoreAsync().ConfigureAwait(false) ? inactiveStatus : LinuxGamePriorityStatus.RestoreFailed;
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Linux game priority failed: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                        result = requested ? LinuxGamePriorityStatus.Unavailable : LinuxGamePriorityStatus.RestoreFailed;
                    }

                    Logger.Log($"Linux game priority: {result}; {backend.LastError}", LoggingTarget.Runtime,
                        result is LinuxGamePriorityStatus.Unavailable or LinuxGamePriorityStatus.RestoreFailed ? LogLevel.Important : LogLevel.Verbose);

                    lock (workLock)
                    {
                        if (shuttingDown || currentRevision != revision)
                            return;
                        Schedule(() =>
                        {
                            if (!shuttingDown && currentRevision == revision)
                                status.Value = result;
                        });
                    }
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                enabled?.UnbindAll();
                active?.UnbindAll();
                lock (workLock)
                {
                    shuttingDown = true;
                    queuePolicy(false, LinuxGamePriorityStatus.Disabled);
                }
                // At shutdown only. The worker never waits for an Update callback. GameMode also
                // reaps dead clients, and systemd removes the transient scope when the process exits.
                if (!pending.Wait(TimeSpan.FromSeconds(10)))
                    Logger.Log("Linux game priority cleanup timed out; session daemons will reap the exited process.", LoggingTarget.Runtime, LogLevel.Important);
            }
            base.Dispose(isDisposing);
        }
    }
}
