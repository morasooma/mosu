// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
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
using osu.Desktop.Windows;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.IPC;
using osu.Game.Performance;
using osu.Game.Performance.Diagnostics;
using osu.Game.Screens.Menu;
using osu.Game.Utils;

namespace osu.Desktop
{
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

        protected override UpdateManager CreateUpdateManager()
        {
            // If this is the first time we've run the game, ie it is being installed,
            // reset the user's release stream specified by the installation target.
            //
            // This ensures that if a user is trying to recover from a failed startup, it will keep them
            // on the stream which is imminently being reinstalled.
            if (IsFirstRun)
                LocalConfig.SetValue(OsuSetting.ReleaseStream, ReleaseStream.Lazer);

            if (IsPackageManaged)
                return new NoActionUpdateManager();

            return new VelopackUpdateManager();
        }

        public override bool RestartAppWhenExited()
        {
            RestartOnExitAction = () => Velopack.UpdateExe.Start(waitPid: (uint)Environment.ProcessId);
            return true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            LoadComponentAsync(new DiscordRichPresence(), Add);

            if (OperatingSystem.IsWindows())
            {
                LoadComponentAsync(new WindowsPerformanceMode(), Add);
            }
            else if (RuntimeInfo.OS == RuntimeInfo.Platform.macOS && !IsPackageManaged && IsDeployedBuild)
            {
                LoadComponentAsync(new MacOSAppLocationChecker(), Add);
            }

            LoadComponentAsync(new MosuPerformanceLogger(), Add);
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
