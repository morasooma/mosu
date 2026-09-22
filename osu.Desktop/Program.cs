// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using osu.Desktop.LegacyIpc;
using osu.Desktop.Performance;
using osu.Desktop.Windows;
using osu.Framework;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.IPC;
using osu.Game.Tournament;
using SDL;
using Velopack;

namespace osu.Desktop
{
    public static class Program
    {
#if DEBUG
        private const string base_game_name = @"mosu-development";
#else
        private const string base_game_name = @"osu";
        private const string legacy_game_name = @"mosu";
#endif

        private const string diagnostics_wait_pid_arg = "--mosu-diagnostics-wait-pid=";
        private const int diagnostics_parent_exit_timeout_ms = 30000;

        private static LegacyTcpIpcProvider? legacyIpc;

        private static bool isFirstRun;

        [STAThread]
        public static void Main(string[] args)
        {
#if DEBUG
            Environment.SetEnvironmentVariable("OSU_INSECURE_REQUESTS", "1");
#endif
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), getCompatibleGameName(), "logs", "crash_startup.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[CRITICAL ERROR] {DateTime.UtcNow}\n{e.ExceptionObject}\n\n");
            };

            // IMPORTANT DON'T IGNORE: For general sanity, velopack's setup needs to run before anything else.
            // This has bitten us in the rear before (bricked updater), and although the underlying issue from
            // last time has been fixed, let's not tempt fate.
            setupVelopack(args);

            if (!waitForPreviousDiagnosticsProcess(args))
                return;

            // Skin performance mode changes the drawable tree and texture loading. Applying benchmark
            // arguments from a component after the main menu has loaded leaves both sides of an A/B run
            // contaminated by the persisted startup configuration.
            MosuBenchmarkSkinOverride.ApplyFromArguments(args);

            if (OperatingSystem.IsWindows())
            {
                var windowsVersion = Environment.OSVersion.Version;

                // While .NET 8 only supports Windows 10 and above, running on Windows 7/8.1 may still work. We are limited by realm currently, as they choose to only support 8.1 and higher.
                // See https://www.mongodb.com/docs/realm/sdk/dotnet/compatibility/
                if (windowsVersion.Major < 6 || (windowsVersion.Major == 6 && windowsVersion.Minor <= 2))
                {
                    unsafe
                    {
                        // If users running in compatibility mode becomes more of a common thing, we may want to provide better guidance or even consider
                        // disabling it ourselves.
                        // We could also better detect compatibility mode if required:
                        // https://stackoverflow.com/questions/10744651/how-i-can-detect-if-my-application-is-running-under-compatibility-mode#comment58183249_10744730
                        SDL3.SDL_ShowSimpleMessageBox(SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR,
                            "Your operating system is too old to run Morasooma"u8,
                            "This version of Morasooma requires at least Windows 8.1 to run.\n"u8
                            + "Please upgrade your operating system or consider using an older version of Morasooma.\n\n"u8
                            + "If you are running a newer version of Windows, please check you don't have \"Compatibility mode\" turned on for Morasooma."u8, null);
                        return;
                    }
                }
            }

            // NVIDIA profiles are based on the executable name of a process.
            // Lazer and stable share the same executable name.
            // Stable sets this setting to "Off", which may not be what we want, so let's force it back to the default "Auto" on startup.
            if (OperatingSystem.IsWindows())
                NVAPI.ThreadedOptimisations = NvThreadControlSetting.OGL_THREAD_CONTROL_DEFAULT;

            // This is a safe default. Localised usages should specify lower values as required.
            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromMilliseconds(1000));

            // Back up the cwd before DesktopGameHost changes it
            string cwd = Environment.CurrentDirectory;

            string gameName = getCompatibleGameName();
            bool tournamentClient = false;
            int? debugClientId = null;

            foreach (string arg in args)
            {
                string[] split = arg.Split('=');

                string key = split[0];
                string val = split.Length > 1 ? split[1] : string.Empty;

                switch (key)
                {
                    case "--mosu-diagnostics-atlas-size":
                        if (val is "1024" or "4096")
                            Environment.SetEnvironmentVariable("MOSU_TEXTURE_ATLAS_SIZE", val);

                        break;

                    case "--tournament":
                        tournamentClient = true;
                        break;

                    case "--tag-coop-bot":
                        if (!DebugUtils.IsDebugBuild)
                            throw new InvalidOperationException("Cannot use the Tag Co-op bot in a non-debug build.");

                        Environment.SetEnvironmentVariable("MOSU_TAG_COOP_BOT", "1");
                        break;

                    case "--tag-coop-latency":
                        setTagCoopNetworkOption("MOSU_TAG_COOP_LATENCY", val, 0, 5000);
                        break;

                    case "--tag-coop-jitter":
                        setTagCoopNetworkOption("MOSU_TAG_COOP_JITTER", val, 0, 5000);
                        break;

                    case "--tag-coop-loss":
                        setTagCoopNetworkOption("MOSU_TAG_COOP_LOSS", val, 0, 100);
                        break;

                    case "--dodge-latency":
                        setDebugNetworkOption("MOSU_DODGE_LATENCY", val, 0, 5000, "Dodge latency");
                        break;

                    case "--debug-client-id":
                        if (!DebugUtils.IsDebugBuild)
                            throw new InvalidOperationException("Cannot use this argument in a non-debug build.");

                        if (!int.TryParse(val, out int clientID))
                            throw new ArgumentException("Provided client ID must be an integer.");

                        debugClientId = clientID;
                        gameName = $"{base_game_name}-{clientID}";
                        break;
                }
            }

            // Secondary local clients exist to make multiplayer development possible on one machine.
            // Let them play their Tag Co-op turns automatically while the primary client stays human-controlled.
            if (debugClientId > 0)
                Environment.SetEnvironmentVariable("MOSU_TAG_COOP_BOT", "1");

            static void setTagCoopNetworkOption(string environmentVariable, string value, int minimum, int maximum)
                => setDebugNetworkOption(environmentVariable, value, minimum, maximum, "Tag Co-op network option");

            static void setDebugNetworkOption(string environmentVariable, string value, int minimum, int maximum, string optionName)
            {
                if (!DebugUtils.IsDebugBuild)
                    throw new InvalidOperationException("Cannot simulate multiplayer networking in a non-debug build.");

                if (!int.TryParse(value, out int parsed) || parsed < minimum || parsed > maximum)
                    throw new ArgumentException($"{optionName} must be between {minimum} and {maximum}.");

                Environment.SetEnvironmentVariable(environmentVariable, parsed.ToString());
            }

            bool renderReplay = args.Any(a => a.StartsWith("--render-replay", StringComparison.Ordinal));

            var hostOptions = new HostOptions
            {
                IPCPipeName = !tournamentClient && !renderReplay ? OsuGame.IPC_PIPE_NAME : null,
                FriendlyGameName = OsuGameBase.GAME_NAME,
            };

            GameHost host;
            host = Host.GetSuitableDesktopHost(gameName, hostOptions);

            using (host)
            {
                if (!host.IsPrimaryInstance && !renderReplay)
                {
                    if (host is IIpcHost ipcHost && trySendIPCMessage(ipcHost, cwd, args))
                        return;

                    // we want to allow multiple instances to be started when in debug.
                    if (!DebugUtils.IsDebugBuild)
                    {
                        Logger.Log(@"Morasooma does not support multiple running instances.", LoggingTarget.Runtime, LogLevel.Error);
                        return;
                    }
                }

                if (host.IsPrimaryInstance && !renderReplay)
                {
                    try
                    {
                        Logger.Log("Starting legacy IPC provider...");
                        legacyIpc = new LegacyTcpIpcProvider();
                        legacyIpc.Bind();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to start legacy IPC provider");
                    }
                }

                if (renderReplay)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        // Force standard Direct3D11 renderer during host startup to ensure background rendering
                        // works even when the window is hidden. Changing the config later is too late.
                        Environment.SetEnvironmentVariable("OSU_GRAPHICS_RENDERER", "veldrid");
                        Environment.SetEnvironmentVariable("OSU_GRAPHICS_SURFACE", "direct3d11");
                    }

                    host.Run(new osu.Game.Scoring.Render.ReplayRenderGame(args));
                }
                else if (tournamentClient)
                    host.Run(new TournamentGame());
                else
                {
                    host.Run(new OsuGameDesktop(args)
                    {
                        IsFirstRun = isFirstRun,
                        EnableWebSocketServer = Environment.GetEnvironmentVariable("OSU_WEBSOCKET_SERVER") == "1",
                        ApplyPendingConfigurationRestore = host.IsPrimaryInstance,
                    });
                }
            }

            // The legacy IPC provider is owned by the desktop entry point rather
            // than the game host, so it must be shut down explicitly after the
            // host has completed. Leaving it alive also makes clean-exit issues
            // difficult to distinguish from a host shutdown problem.
            legacyIpc?.Dispose();
            legacyIpc = null;

            Logger.Log("Desktop entry point completed after host disposal.", LoggingTarget.Runtime, LogLevel.Verbose);
            Logger.Flush();
        }

        private static string getCompatibleGameName()
        {
#if DEBUG
            return base_game_name;
#else
            foreach (string storagePath in getUserStoragePaths())
            {
                if (containsExistingInstallation(Path.Combine(storagePath, legacy_game_name)))
                    return legacy_game_name;
            }

            return base_game_name;
#endif
        }

        private static bool containsExistingInstallation(string path)
        {
            // storage.ini is sufficient on its own because the installation may keep all
            // user data in the custom location specified by its FullPath setting.
            return File.Exists(Path.Combine(path, "storage.ini"))
                   || File.Exists(Path.Combine(path, OsuGameBase.CLIENT_DATABASE_FILENAME))
                   || File.Exists(Path.Combine(path, "game.ini"))
                   || File.Exists(Path.Combine(path, "framework.ini"));
        }

#if !DEBUG
        private static System.Collections.Generic.IEnumerable<string> getUserStoragePaths()
        {
            if (OperatingSystem.IsWindows())
            {
                yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                yield break;
            }

            yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Older osu!framework builds could use ~/.local/share on macOS.
            if (OperatingSystem.IsMacOS())
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
#endif

        private static bool waitForPreviousDiagnosticsProcess(string[] args)
        {
            string? waitArgument = args.FirstOrDefault(arg => arg.StartsWith(diagnostics_wait_pid_arg, StringComparison.Ordinal));

            if (waitArgument == null)
                return true;

            if (!int.TryParse(waitArgument.AsSpan(diagnostics_wait_pid_arg.Length), out int processId)
                || processId <= 0
                || processId == Environment.ProcessId)
            {
                Logger.Log($"Invalid diagnostics parent process argument: {waitArgument}", LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }

            try
            {
                using var previousProcess = Process.GetProcessById(processId);

                Logger.Log($"Waiting for diagnostics parent process {processId} to exit before host startup.", LoggingTarget.Runtime, LogLevel.Verbose);

                if (!previousProcess.WaitForExit(diagnostics_parent_exit_timeout_ms))
                {
                    Logger.Log(
                        $"Diagnostics parent process {processId} did not exit within {diagnostics_parent_exit_timeout_ms} ms; aborting chained launch.",
                        LoggingTarget.Runtime,
                        LogLevel.Error);
                    return false;
                }
            }
            catch (ArgumentException)
            {
                // The parent completed before this process had a chance to inspect it.
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Could not wait for diagnostics parent process {processId}; aborting chained launch.");
                return false;
            }

            return true;
        }

        private static bool trySendIPCMessage(IIpcHost host, string cwd, string[] args)
        {
            if (args.Length == 1 && args[0].StartsWith(OsuGameBase.OSU_PROTOCOL, StringComparison.Ordinal))
            {
                var osuSchemeLinkHandler = new OsuSchemeLinkIPCChannel(host);
                if (!osuSchemeLinkHandler.HandleLinkAsync(args[0]).Wait(3000))
                    throw new IPCTimeoutException(osuSchemeLinkHandler.GetType());

                return true;
            }

            if (args.Length > 0 && args[0].Contains('.')) // easy way to check for a file import in args
            {
                var importer = new ArchiveImportIPCChannel(host);

                foreach (string file in args)
                {
                    Console.WriteLine(@"Importing {0}", file);
                    if (!importer.ImportAsync(Path.GetFullPath(file, cwd)).Wait(3000))
                        throw new IPCTimeoutException(importer.GetType());
                }

                return true;
            }

            return false;
        }

        private static void setupVelopack(string[] args)
        {
            // Arguments being present indicate the user is either starting the game in a special (aka tournament) mode,
            // or is running with pending imports via file association or otherwise.
            //
            // In both these scenarios, we'd hope the game does not attempt to update.
            //
            // Special consideration for velopack startup arguments, which must be handled during update.
            // See https://docs.velopack.io/integrating/hooks#command-line-hooks.
            if (args.Length > 0 && !args[0].StartsWith("--velo", StringComparison.Ordinal))
            {
                Logger.Log("Handling arguments, skipping velopack setup.");
                return;
            }

            if (OsuGameDesktop.IsPackageManaged)
            {
                Logger.Log("Updates are being managed by an external provider. Skipping Velopack setup.");
                return;
            }

            var app = VelopackApp.Build();

            app.OnFirstRun(_ => isFirstRun = true);

            if (OperatingSystem.IsWindows())
                configureWindows(app);

            app.Run();
        }

        [SupportedOSPlatform("windows")]
        private static void configureWindows(VelopackApp app)
        {
            app.OnFirstRun(_ => WindowsAssociationManager.InstallAssociations());
            app.OnAfterUpdateFastCallback(_ => WindowsAssociationManager.UpdateAssociations());
            app.OnBeforeUninstallFastCallback(_ => WindowsAssociationManager.UninstallAssociations());
        }
    }
}
