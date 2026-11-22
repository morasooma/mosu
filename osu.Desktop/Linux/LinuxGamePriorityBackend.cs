using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace osu.Desktop.Linux
{
    // Uses the public systemd and GameMode D-Bus interfaces through busctl. No shell,
    // privilege prompts, persistent host configuration, or real-time scheduler changes.
    internal sealed class LinuxGamePriorityBackend
    {
        private const string systemd_service = "org.freedesktop.systemd1";
        private const string systemd_path = "/org/freedesktop/systemd1";
        private const string systemd_interface = "org.freedesktop.systemd1.Manager";
        private readonly int processId;
        private readonly string scope;
        private bool scopeCreated;
        private bool gameModeOwned;

        internal LinuxGamePriorityBackend(int processId)
        {
            this.processId = processId;
            scope = $"mosu-game-priority-{processId}.scope";
        }

        internal async Task<(bool Cpu, bool GameMode)> ApplyAsync()
        {
            LastError = null;
            bool cpu = false;
            try
            {
                if (!scopeCreated)
                {
                    await callAsync(systemd_service, systemd_path, systemd_interface, "StartTransientUnit", "ssa(sv)a(sa(sv))",
                        scope, "fail", "4", "Description", "s", "Morasooma game priority", "Slice", "s", "app.slice",
                        "PIDs", "au", "1", processId.ToString(CultureInfo.InvariantCulture), "CPUWeight", "t", "1000", "0").ConfigureAwait(false);
                    scopeCreated = true;
                    // A successful method reply queues a job; verify actual placement and weight.
                    for (int i = 0; i < 20 && !isScopeApplied(); i++)
                        await Task.Delay(50).ConfigureAwait(false);
                    cpu = isScopeApplied();
                }
                else
                {
                    await setWeightAsync(1000).ConfigureAwait(false);
                    cpu = isScopeApplied();
                }
            }
            catch (Exception e)
            {
                LastError = e.Message;
                // A timed-out D-Bus call may still have moved us. Retain cleanup ownership.
                try
                {
                    scopeCreated |= isInScope();
                }
                catch (IOException)
                {
                }
            }

            bool gameMode = false;
            try
            {
                if (await gameModeCallAsync("QueryStatus").ConfigureAwait(false) == 2)
                    gameMode = true; // An external gamemoderun request must not be unregistered by us.
                else if (await gameModeCallAsync("RegisterGame").ConfigureAwait(false) == 0)
                {
                    gameModeOwned = true;
                    gameMode = await gameModeCallAsync("QueryStatus").ConfigureAwait(false) == 2;
                }
                if (!gameMode)
                    LastError = "GameMode did not confirm registration.";
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }

            return (cpu, gameMode);
        }

        internal string? LastError { get; private set; }

        internal async Task<bool> RestoreAsync()
        {
            bool restored = true;
            if (scopeCreated)
            {
                try
                {
                    // Keep our private scope until process exit, but remove its resource advantage.
                    // Moving back could change unrelated launcher scopes or kill sibling processes.
                    await setWeightAsync(100).ConfigureAwait(false);
                    restored = readWeight() == "100";
                }
                catch (Exception e)
                {
                    LastError = e.Message;
                    restored = false;
                }
            }

            if (gameModeOwned)
            {
                try
                {
                    if (await gameModeCallAsync("UnregisterGame").ConfigureAwait(false) == 0)
                        gameModeOwned = false;
                    else
                    {
                        LastError = "GameMode rejected the unregister request.";
                        restored = false;
                    }
                }
                catch (Exception e)
                {
                    LastError = e.Message;
                    restored = false;
                }
            }

            return restored;
        }

        private Task<string> setWeightAsync(int weight) => callAsync(systemd_service, systemd_path, systemd_interface,
            "SetUnitProperties", "sba(sv)", scope, "true", "1", "CPUWeight", "t", weight.ToString(CultureInfo.InvariantCulture));

        private bool isInScope() => getCgroupPath()?.EndsWith('/' + scope, StringComparison.Ordinal) == true;
        private bool isScopeApplied() => isInScope() && readWeight() == "1000";

        private string? readWeight()
        {
            string? path = getCgroupPath();
            return path == null ? null : File.ReadAllText("/sys/fs/cgroup" + path + "/cpu.weight").Trim();
        }

        private string? getCgroupPath() => File.ReadLines($"/proc/{processId}/cgroup")
            .FirstOrDefault(line => line.StartsWith("0::", StringComparison.Ordinal))?[3..];

        private async Task<int> gameModeCallAsync(string method)
        {
            string reply = await callAsync("com.feralinteractive.GameMode", "/com/feralinteractive/GameMode",
                "com.feralinteractive.GameMode", method, "i", processId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            if (!reply.StartsWith("i ", StringComparison.Ordinal) || !int.TryParse(reply.AsSpan(2).Trim(), out int status))
                throw new InvalidOperationException("Invalid GameMode status reply.");
            return status;
        }

        private static async Task<string> callAsync(params string[] arguments)
        {
            var info = new ProcessStartInfo("busctl")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (string argument in new[] { "--user", "--timeout=2s", "--allow-interactive-authorization=no", "call" }.Concat(arguments))
                info.ArgumentList.Add(argument);

            using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start busctl.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(4));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Only the helper we created, never the game or its descendants.
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // The helper may have exited between cancellation and Kill.
                }
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(output, error).ConfigureAwait(false);
                throw new TimeoutException("Linux game priority request timed out.");
            }

            string stdout = await output.ConfigureAwait(false);
            string stderr = await error.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidOperationException(stderr.Trim());
            return stdout.Trim();
        }
    }
}
