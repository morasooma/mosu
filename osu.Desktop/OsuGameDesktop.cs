// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;
using osu.Desktop.IPC;
using osu.Desktop.Performance;
using osu.Desktop.Security;
using osu.Framework.Platform;
using osu.Game;
using osu.Desktop.Updater;
using osu.Framework;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Updater;
using osu.Desktop.MacOS;
using osu.Desktop.Linux;
using osu.Desktop.Windows;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using DrawablePath = osu.Framework.Graphics.Lines.Path;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.IPC;
using osu.Game.Performance;
using osu.Game.Performance.Diagnostics;
using osu.Game.Performance.Debug;
using osu.Game.Screens.Menu;
using osu.Game.Utils;

namespace osu.Desktop
{
    [Cached(typeof(OsuGameDesktop))]
    internal partial class OsuGameDesktop : OsuGame
    {
        private OsuSchemeLinkIPCChannel? osuSchemeLinkIPCChannel;
        private ArchiveImportIPCChannel? archiveImportIPCChannel;

        private readonly string[] launchArgs;
        private bool performanceRunnersLoaded;

        [Cached(typeof(IHighPerformanceSessionManager))]
        private readonly HighPerformanceSessionManager highPerformanceSessionManager = new HighPerformanceSessionManager();

        [Cached(typeof(IPerformanceDiagnosticsManager))]
        [Cached(typeof(PerformanceDiagnosticsManager))]
        private readonly PerformanceDiagnosticsManager performanceDiagnosticsManager = new PerformanceDiagnosticsManager();

        [Cached(typeof(ILinuxGamePriority))]
        private readonly LinuxGamePriority linuxGamePriority = new LinuxGamePriority();

        public bool IsFirstRun { get; init; }

        public bool EnableWebSocketServer { get; init; }

        /// <summary>
        /// Whether a staged server configuration backup should be applied during startup.
        /// Secondary processes must not mutate the primary installation's configuration.
        /// </summary>
        public bool ApplyPendingConfigurationRestore { get; init; }

        public OsuGameDesktop(string[]? args = null)
            : base(args)
        {
            launchArgs = args ?? Array.Empty<string>();
        }

        public override void SetupLogging(Storage gameStorage, Storage cacheStorage)
        {
            // GameHost.Storage is first created inside GameHost.Run(). This is the earliest callback
            // at which it is guaranteed to exist, and it still runs before SetHost() constructs the
            // framework, input, game and Mosu configuration managers.
            if (ApplyPendingConfigurationRestore)
                ConfigurationBackupManager.ApplyPendingRestore(gameStorage);

            base.SetupLogging(gameStorage, cacheStorage);
        }

        public override StableStorage? GetStorageForStableInstall()
        {
            try
            {
                if (Host is DesktopGameHost desktopHost)
                {
                    string? stablePath = LocalConfig?.Get<string>(osu.Game.Configuration.OsuSetting.ForkStableDirectoryPath);
                    if (string.IsNullOrEmpty(stablePath) || (!Directory.Exists(Path.Combine(stablePath, "Songs")) && !File.Exists(Path.Combine(stablePath, "osu!.cfg"))))
                        stablePath = getStableInstallPath();

                    if (!string.IsNullOrEmpty(stablePath))
                        return new StableStorage(stablePath, desktopHost);
                }
            }
            catch (Exception)
            {
                Logger.Log("Could not find a stable install", LoggingTarget.Runtime, LogLevel.Important);
            }

            return null;
        }

        private string? getStableInstallPath()
        {
            static bool checkExists(string p) => Directory.Exists(Path.Combine(p, "Songs")) || File.Exists(Path.Combine(p, "osu!.cfg"));

            string? stableInstallPath;

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    stableInstallPath = getStableInstallPathFromRegistry("osustable.File.osz");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;

                    stableInstallPath = getStableInstallPathFromRegistry("osu!");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;
                }
                catch
                {
                }
            }

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"osu!");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osu");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            return null;
        }

        [SupportedOSPlatform("windows")]
        private string? getStableInstallPathFromRegistry(string progId)
        {
            using (RegistryKey? key = Registry.ClassesRoot.OpenSubKey(progId))
                return key?.OpenSubKey(WindowsAssociationManager.SHELL_OPEN_COMMAND)?.GetValue(string.Empty)?.ToString()?.Split('"')[1].Replace("osu!.exe", "");
        }

        public static bool IsPackageManaged => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OSU_EXTERNAL_UPDATE_PROVIDER"));

        // Experimental Linux-built .app bundles have no UpdateMac or Velopack feed.
        public static bool IsManualMacOSBundle => OperatingSystem.IsMacOS()
                                                 && File.Exists(Path.Combine(AppContext.BaseDirectory, "mosu-manual-updates"));

        protected override UpdateManager CreateUpdateManager()
        {
            // If this is the first time we've run the game, ie it is being installed,
            // reset the user's release stream specified by the installation target.
            //
            // This ensures that if a user is trying to recover from a failed startup, it will keep them
            // on the stream which is imminently being reinstalled.
            if (IsFirstRun)
                LocalConfig.SetValue(OsuSetting.ReleaseStream, ReleaseStream.Lazer);

            if (IsManualMacOSBundle)
                return new UpdateManager();

            if (IsPackageManaged)
                return new NoActionUpdateManager();

            return new VelopackUpdateManager();
        }

        internal bool IsDiagnosticsBenchmark => launchArgs.Contains(MosuPerformanceDiagnosticsRunner.LAUNCH_ARG, StringComparer.Ordinal)
                                                && launchArgs.Contains("--mosu-replay-benchmark", StringComparer.Ordinal);

        protected override bool UseSystemCursor => IsDiagnosticsBenchmark;

        public override bool RestartAppWhenExited()
        {
            RestartOnExitAction = () =>
            {
                var locator = IsManualMacOSBundle ? null : Velopack.Locators.VelopackLocator.Current;
                if (!IsPackageManaged && !string.IsNullOrWhiteSpace(locator?.UpdateExePath) && File.Exists(locator.UpdateExePath))
                {
                    Velopack.UpdateExe.Start(locator, waitPid: (uint)Environment.ProcessId);
                    return;
                }

                restartCurrentExecutable(launchArgs);
            };
            return true;
        }

        internal void RestartForDiagnostics(IEnumerable<string> args)
        {
            // Carry across a development profile, but not imports, old benchmark options or old parent PIDs.
            string[] nextArgs = launchArgs.Where(arg => arg.StartsWith("--debug-client-id=", StringComparison.Ordinal)).Concat(args).ToArray();
            RestartOnExitAction = () => restartCurrentExecutable(nextArgs);
        }

        private static void restartCurrentExecutable(IEnumerable<string> args)
        {
            // Start the original AppImage, not the apphost on the mount that goes away as we exit.
            string? appImage = OperatingSystem.IsLinux() ? Environment.GetEnvironmentVariable("APPIMAGE") : null;
            string? executable = !string.IsNullOrWhiteSpace(appImage) && File.Exists(appImage)
                ? appImage
                : Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(executable))
                throw new InvalidOperationException("Cannot restart Mosu: the current executable path is unavailable.");

            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory,
            };

            if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                string? assemblyPath = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrWhiteSpace(assemblyPath))
                    throw new InvalidOperationException("Cannot restart Mosu: the entry assembly path is unavailable.");

                startInfo.ArgumentList.Add(assemblyPath);
            }

            foreach (string arg in args)
            {
                if (!arg.StartsWith("--mosu-diagnostics-wait-pid=", StringComparison.Ordinal))
                    startInfo.ArgumentList.Add(arg);
            }

            startInfo.ArgumentList.Add($"--mosu-diagnostics-wait-pid={Environment.ProcessId}");
            using var process = Process.Start(startInfo);
        }

        // The entry point executes this after the host and legacy IPC have been disposed.
        // Launching the updater from game disposal leaves it waiting on the remaining shutdown work.
        protected override void ExecuteRestartOnExit()
        {
        }

        public void CompleteRestartOnExit() => base.ExecuteRestartOnExit();

        protected override void LoadComplete()
        {
            using var total = PerformancePhaseLogger.Begin("OsuGameDesktop.LoadComplete");
            using (PerformancePhaseLogger.Begin("OsuGameDesktop.BaseLoadComplete"))
                base.LoadComplete();


            // Portable/AppImage installs do not necessarily run an installer callback.
            // Register automatically for the primary client; development builds must not
            // replace the installed application's launcher with a bin/Debug path.
            if (Host.IsPrimaryInstance && !osu.Framework.Development.DebugUtils.IsDebugBuild)
                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var association = new FontFileAssociation();
                        if (association.IsSupported)
                            association.Register();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Could not register the .morafn file association.");
                    }
                });

            using var desktopSetup = PerformancePhaseLogger.Begin("OsuGameDesktop.DesktopSetup");
            Logger.Log($"GC runtime: {(GC.GetConfigurationVariables().ContainsKey("SatoriGC") ? "Satori" : "CoreCLR")}; "
                       + $"mode={System.Runtime.GCSettings.LatencyMode}; "
                       + $"runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}", LoggingTarget.Runtime);
            LoadComponentAsync(new DiscordRichPresence(), Add);

            if (OperatingSystem.IsWindows())
            {
                LoadComponentAsync(new WindowsPerformanceMode(), Add);
                LoadComponentAsync(new GameplayWinKeyBlocker(), Add);
            }
            else if (OperatingSystem.IsLinux())
            {
                LoadComponentAsync(linuxGamePriority, Add);
            }
            else if (RuntimeInfo.OS == RuntimeInfo.Platform.macOS && !IsPackageManaged && IsDeployedBuild)
            {
                LoadComponentAsync(new MacOSAppLocationChecker(), Add);
            }

            LoadComponentAsync(new MosuPerformanceLogger(), Add);
            if (Environment.GetEnvironmentVariable("MORA_VULKAN_MENU_BENCHMARK") == "1")
                LoadComponentAsync(new MoraVulkanMenuBenchmark(), Add);
            LoadComponentAsync(performanceDiagnosticsManager, manager =>
            {
                Add(manager);
                loadPerformanceRunnersWhenReady();
            });
            LoadComponentAsync(new ElevatedPrivilegesChecker(), Add);

            osuSchemeLinkIPCChannel = new OsuSchemeLinkIPCChannel(Host, this);
            archiveImportIPCChannel = new ArchiveImportIPCChannel(Host, this);

            if (EnableWebSocketServer)
                Add(new OsuWebSocketProvider());
        }

        private void loadPerformanceRunnersWhenReady()
        {
            bool benchmarkLaunch = Array.Exists(
                launchArgs,
                arg => arg.StartsWith("--mosu-replay-benchmark", StringComparison.Ordinal));

            if (!benchmarkLaunch)
            {
                loadPerformanceRunners();
                return;
            }

            // Benchmark configuration can change process/thread/window policies. Applying it while
            // osu!'s base UI components are still loading races the update and load threads, and has
            // intermittently stalled startup before FPSCounter completed. Wait for the main menu,
            // which is the lifecycle boundary the replay benchmark actually requires.
            ScreenStack.ScreenPushed += loadPerformanceRunnersOnMainMenu;

            if (ScreenStack.CurrentScreen is MainMenu)
                loadPerformanceRunners();
        }

        private void loadPerformanceRunnersOnMainMenu(IScreen? previous, IScreen next)
        {
            if (next is MainMenu)
                loadPerformanceRunners();
        }

        private void loadPerformanceRunners()
        {
            if (performanceRunnersLoaded)
                return;

            performanceRunnersLoaded = true;
            ScreenStack.ScreenPushed -= loadPerformanceRunnersOnMainMenu;

            Logger.Log("Loading performance diagnostic runners after main menu readiness.", LoggingTarget.Runtime, LogLevel.Verbose);

            LoadComponentAsync(new MosuPerformanceDiagnosticsRunner(launchArgs), diagnosticsRunner =>
            {
                Add(diagnosticsRunner);
                LoadComponentAsync(new MosuReplayBenchmarkRunner(launchArgs), Add);
            });
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            // MosuPerformanceLogger is loaded later. Enable tree attribution before the first
            // update frame when recording was already enabled in this profile's configuration.
            if (LocalConfig.Get<bool>(OsuSetting.ForkPerformanceLogging))
            {
                host.SetUpdateTreeDiagnostics(true);
                PerformancePhaseLogger.Enabled = true;
                Bindable<WorkingBeatmap>.TraceSlowValueChanges = true;
                DrawablePath.TraceSlowRendering = true;
                Logger.Log("MOSU_UPDATE_TREE_CAPTURE enabled=1 source=startup_performance_setting", LoggingTarget.Performance);
            }

            // Apple operating systems use a better icon provided via external assets.
            if (!RuntimeInfo.IsApple)
            {
                var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "lazer.ico");
                if (iconStream != null)
                    try
                    {
                        host.Window.SetIconFromStream(iconStream);
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Could not load icon: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                    }
            }

            host.Window.Title = Name;
        }

        public System.Threading.Tasks.Task<string?> StopPerformanceRecordingOnExitAsync()
            => performanceDiagnosticsManager.StopPerformanceRecordingOnExitAsync();

        protected override BatteryInfo CreateBatteryInfo() => FrameworkEnvironment.UseSDL3 ? new SDL3BatteryInfo() : new SDL2BatteryInfo();

        protected override void Dispose(bool isDisposing)
        {
            if (ScreenStack != null)
                ScreenStack.ScreenPushed -= loadPerformanceRunnersOnMainMenu;

            base.Dispose(isDisposing);
            osuSchemeLinkIPCChannel?.Dispose();
            archiveImportIPCChannel?.Dispose();
        }
    }
}
