// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Statistics;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.Overlays;
using osu.Game.Performance;
using osu.Game.Performance.Diagnostics;
using osu.Game.Screens.Menu;

namespace osu.Desktop.Performance
{
    public partial class PerformanceDiagnosticsManager : Component, IPerformanceDiagnosticsManager
    {
        private const uint asfw_any = uint.MaxValue;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private PerformanceDiagnosticsOverlay diagnosticsOverlay { get; set; } = null!;

        public bool IsRunning => CurrentSession != null
                                 && CurrentSession.Phase is MosuDiagnosticsPhase.PendingRestart
                                     or MosuDiagnosticsPhase.RunningBenchmark
                                     or MosuDiagnosticsPhase.PendingResultsRestart
                                     or MosuDiagnosticsPhase.Paused;

        public MosuDiagnosticsSession? CurrentSession { get; private set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            refreshSession();

            if (game.ScreenStack != null)
                game.ScreenStack.ScreenPushed += screenPushed;
        }

        public void ShowSetup()
        {
            refreshSession();

            if (CurrentSession?.Phase == MosuDiagnosticsPhase.Paused)
                diagnosticsOverlay.ShowPaused(CurrentSession);
            else if (IsRunning && CurrentSession != null)
                diagnosticsOverlay.ShowProgress(CurrentSession);
            else if (CurrentSession?.Phase == MosuDiagnosticsPhase.ShowResults)
                diagnosticsOverlay.ShowResults(CurrentSession);
            else
                diagnosticsOverlay.ShowSetup();
        }

        public void BeginSession(MosuDiagnosticsSetupOptions options)
        {
            if (IsRunning)
                return;

            var renderers = getAvailableRenderers(options.Mode).ToList();

            if (renderers.Count == 0)
                return;

            string sessionId = $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}";
            (List<MosuDiagnosticsProfile> profiles, List<MosuDiagnosticsSkippedProfile> skippedProfiles) =
                getProfilePlan(options, renderers[0]);

            var session = new MosuDiagnosticsSession
            {
                SchemaVersion = MosuDiagnosticsDefaults.SchemaVersion,
                SessionId = sessionId,
                Phase = MosuDiagnosticsPhase.PendingRestart,
                Options = options,
                ClientVersion = game.Version,
                ClientVersionHash = game.VersionHash,
                RuntimeDescription = RuntimeInformation.FrameworkDescription,
                Machine = MosuDiagnosticsMachineProfileCollector.Collect(storage, host.RendererInfo),
                CurrentConfiguration = captureCurrentConfiguration(),
                OriginalRenderer = frameworkConfig.Get<RendererType>(FrameworkSetting.Renderer),
                OriginalSkin = config.Get<string>(OsuSetting.Skin),
                Renderers = renderers,
                Profiles = profiles,
                SkippedProfiles = skippedProfiles,
                RendererIndex = 0,
                ProfileIndex = 0,
                ExpectedRenderer = renderers[0],
                ExpectedProfile = profiles[0],
                BenchmarkId = sessionId,
                StartedAt = DateTimeOffset.Now,
            };

            MosuDiagnosticsSessionStore.Save(storage, session);
            CurrentSession = session;
            diagnosticsOverlay.ShowProgress(session);
            frameworkConfig.SetValue(FrameworkSetting.Renderer, renderers[0]);
            restartWithArgs(MosuPerformanceDiagnosticsRunner.BuildLaunchArgs(session, includeBenchmark: true));
        }

        public void ShowResultsIfPending()
        {
            refreshSession();

            if (CurrentSession?.Phase == MosuDiagnosticsPhase.ShowResults)
                diagnosticsOverlay.ShowResults(CurrentSession);
        }

        public void ResumeSession()
        {
            refreshSession();

            if (CurrentSession?.Phase != MosuDiagnosticsPhase.Paused
                || CurrentSession.ExpectedRenderer == null
                || CurrentSession.ExpectedProfile == null)
                return;

            MosuDiagnosticsSession session = CurrentSession;
            session.Phase = MosuDiagnosticsPhase.PendingRestart;
            MosuDiagnosticsSessionStore.Save(storage, session);
            CurrentSession = session;
            frameworkConfig.SetValue(FrameworkSetting.Renderer, session.ExpectedRenderer.Value);
            diagnosticsOverlay.ShowProgress(session);
            restartWithArgs(MosuPerformanceDiagnosticsRunner.BuildLaunchArgs(session, includeBenchmark: true));
        }

        public void DismissResults()
        {
            refreshSession();

            if (CurrentSession == null)
                return;

            MosuDiagnosticsSessionStore.Delete(storage);
            CurrentSession = null;
            diagnosticsOverlay.Hide();
        }

        public void CancelSession()
        {
            refreshSession();

            if (CurrentSession == null)
                return;

            MosuDiagnosticsSession session = CurrentSession;
            frameworkConfig.SetValue(FrameworkSetting.Renderer, session.OriginalRenderer);
            restoreOriginalSkin(session);

            MosuDiagnosticsSessionStore.Delete(storage);
            CurrentSession = null;
            diagnosticsOverlay.Hide();
            restartWithArgs(MosuPerformanceDiagnosticsRunner.BuildRestoreLaunchArgs(session, continueDiagnostics: false));
        }

        public void ApplyRecommendedSettings(RendererType renderer)
        {
            MosuRecommendedPerformancePreset.Apply(config);
            frameworkConfig.SetValue(FrameworkSetting.Renderer, renderer);
        }

        public void OpenExportFolder()
        {
            refreshSession();

            if (CurrentSession != null)
                PresentExport(CurrentSession);
        }

        public async Task<string> CaptureMemoryDumpAsync(CancellationToken cancellationToken = default)
        {
            string captureId = $"memory-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}";
            Storage dumpRootStorage = storage.GetStorageForDirectory(Path.Combine("exports", "memory-dumps"));
            string dumpRootDirectory = dumpRootStorage.GetFullPath(string.Empty, true);
            restrictToCurrentUser(dumpRootDirectory, isDirectory: true);

            Storage dumpStorage = dumpRootStorage.GetStorageForDirectory(captureId);
            string dumpFilename = OperatingSystem.IsWindows() ? "Morasooma.dmp" : "Morasooma.core";
            string dumpPath = dumpStorage.GetFullPath(dumpFilename, true);
            string captureDirectory = dumpStorage.GetFullPath(string.Empty, true);

            restrictToCurrentUser(captureDirectory, isDirectory: true);

            try
            {
                using var process = Process.GetCurrentProcess();
                GCMemoryInfo gc = GC.GetGCMemoryInfo();

                var metadata = new
                {
                    schemaVersion = 1,
                    capturedAt = DateTimeOffset.Now,
                    processId = Environment.ProcessId,
                    game.Version,
                    game.VersionHash,
                    runtime = RuntimeInformation.FrameworkDescription,
                    operatingSystem = RuntimeInformation.OSDescription,
                    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    renderer = host.RendererInfo,
                    currentScreen = game.ScreenStack?.CurrentScreen?.GetType().FullName,
                    process = new
                    {
                        workingSetBytes = process.WorkingSet64,
                        privateBytes = process.PrivateMemorySize64,
                        virtualBytes = process.VirtualMemorySize64,
                        peakWorkingSetBytes = process.PeakWorkingSet64,
                        threadCount = process.Threads.Count,
                    },
                    gc = new
                    {
                        latencyMode = GCSettings.LatencyMode.ToString(),
                        serverGc = GCSettings.IsServerGC,
                        totalManagedBytes = GC.GetTotalMemory(false),
                        totalAllocatedBytes = GC.GetTotalAllocatedBytes(false),
                        heapSizeBytes = gc.HeapSizeBytes,
                        fragmentedBytes = gc.FragmentedBytes,
                        memoryLoadBytes = gc.MemoryLoadBytes,
                        highMemoryLoadThresholdBytes = gc.HighMemoryLoadThresholdBytes,
                        gen0Collections = GC.CollectionCount(0),
                        gen1Collections = GC.CollectionCount(1),
                        gen2Collections = GC.CollectionCount(2),
                    },
                    globalStatistics = GlobalStatistics.GetStatistics()
                                                       .OrderBy(s => s.Group)
                                                       .ThenBy(s => s.Name)
                                                       .Select(s => new { s.Group, s.Name, s.DisplayValue })
                                                       .ToArray(),
                };

                string metadataPath = dumpStorage.GetFullPath("metadata.json", true);
                await File.WriteAllTextAsync(
                    metadataPath,
                    JsonConvert.SerializeObject(metadata, Formatting.Indented),
                    cancellationToken).ConfigureAwait(false);
                restrictToCurrentUser(metadataPath, isDirectory: false);

                if (OperatingSystem.IsLinux())
                {
                    copyProcSnapshot("status", "process-status.txt");
                    copyProcSnapshot("smaps", "process-smaps.txt");
                    copyProcSnapshot("smaps_rollup", "process-smaps-rollup.txt");
                    copyProcSnapshot("maps", "process-maps.txt");
                }

                var client = new DiagnosticsClient(Environment.ProcessId);
                await client.WriteDumpAsync(DumpType.WithHeap, dumpPath, WriteDumpFlags.None, cancellationToken).ConfigureAwait(false);
                restrictToCurrentUser(dumpPath, isDirectory: false);
                Logger.Log($"Memory dump captured: {dumpPath}", LoggingTarget.Runtime, LogLevel.Important);
                return dumpPath;
            }
            catch
            {
                try
                {
                    if (Directory.Exists(captureDirectory))
                        Directory.Delete(captureDirectory, recursive: true);
                }
                catch (Exception cleanupException)
                {
                    // Preserve the original capture exception if the dump writer still has a file open.
                    Logger.Log($"Could not clean up failed memory dump directory: {cleanupException.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }

                throw;
            }

            void copyProcSnapshot(string sourceName, string destinationName)
            {
                try
                {
                    string destinationPath = dumpStorage.GetFullPath(destinationName, true);
                    File.Copy($"/proc/self/{sourceName}", destinationPath, true);
                    restrictToCurrentUser(destinationPath, isDirectory: false);
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not capture /proc/self/{sourceName}: {e.Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                }
            }

            static void restrictToCurrentUser(string path, bool isDirectory)
            {
                if (OperatingSystem.IsWindows())
                    return;

                try
                {
                    File.SetUnixFileMode(path, isDirectory
                        ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                        : UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not restrict memory dump permissions for {path}: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                }
            }
        }

        public void OpenMemoryDumpFolder()
            => storage.GetStorageForDirectory(Path.Combine("exports", "memory-dumps")).PresentExternally();

        internal void HandleSessionUpdated(MosuDiagnosticsSession session)
        {
            CurrentSession = session;
        }

        internal void RequestRestart(IEnumerable<string> args)
        {
            restartWithArgs(args);
        }

        internal void RequestShowResults(MosuDiagnosticsSession session)
        {
            CurrentSession = session;
            diagnosticsOverlay.ShowResults(session);
        }

        internal void RequestShowProgress(MosuDiagnosticsSession session)
        {
            CurrentSession = session;
            diagnosticsOverlay.ShowProgress(session);
        }

        internal bool PresentExport(MosuDiagnosticsSession session)
        {
            if (string.IsNullOrEmpty(session.ExportZipPath))
                return false;

            return storage.GetStorageForDirectory("exports").PresentFileExternally(Path.GetFileName(session.ExportZipPath));
        }

        private void refreshSession()
        {
            CurrentSession = MosuDiagnosticsSessionStore.TryLoad(storage);
        }

        private void screenPushed(IScreen? previous, IScreen next)
        {
            if (next is not MainMenu)
                return;

            Scheduler.AddDelayed(() =>
            {
                refreshSession();

                if (CurrentSession?.Phase == MosuDiagnosticsPhase.ShowResults)
                    diagnosticsOverlay.ShowResults(CurrentSession);
            }, 1000);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (game.ScreenStack != null)
                game.ScreenStack.ScreenPushed -= screenPushed;

            base.Dispose(isDisposing);
        }

        internal void RestoreOriginalSkin(MosuDiagnosticsSession session) => restoreOriginalSkin(session);

        private void restoreOriginalSkin(MosuDiagnosticsSession session)
        {
            if (!string.IsNullOrEmpty(session.OriginalSkin))
                config.SetValue(OsuSetting.Skin, session.OriginalSkin);
        }

        private IEnumerable<RendererType> getAvailableRenderers(MosuDiagnosticsMode mode)
        {
            if (mode != MosuDiagnosticsMode.RendererComparison)
                return new[] { host.ResolvedRenderer };

            IEnumerable<RendererType> availableRenderers = host.GetPreferredRenderersForCurrentPlatform().Order();

            availableRenderers = availableRenderers.Where(r => r != RendererType.Automatic);

            var currentRenderer = frameworkConfig.Get<RendererType>(FrameworkSetting.Renderer);

            if (currentRenderer != RendererType.Deferred_Vulkan)
                availableRenderers = availableRenderers.Where(t => t != RendererType.Deferred_Vulkan);

            if (currentRenderer != RendererType.Vulkan)
                availableRenderers = availableRenderers.Where(t => t != RendererType.Vulkan);

            RendererType originalResolvedRenderer = host.ResolvedRenderer;
            return availableRenderers
                   .OrderBy(r => r == originalResolvedRenderer ? 1 : 0)
                   .ThenBy(r => r);
        }

        private MosuDiagnosticsConfigurationSnapshot captureCurrentConfiguration() => new MosuDiagnosticsConfigurationSnapshot
        {
            WindowsUltraPerformanceMode = config.Get<bool>(OsuSetting.ForkWindowsUltraPerformanceMode),
            LargeTextureAtlas = config.Get<bool>(OsuSetting.ForkLargeTextureAtlas),
            SkinPerformanceMode = config.Get<bool>(OsuSetting.ForkSkinPerformanceMode),
            SkinPerformanceFreezeAnimations = config.Get<bool>(OsuSetting.ForkSkinPerformanceFreezeAnimations),
            SkinPerformanceSimplifyEffects = config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyEffects),
            SkinPerformanceOptimiseTextures = config.Get<bool>(OsuSetting.ForkSkinPerformanceOptimiseTextures),
            SkinPerformanceSimplifyHud = config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyHud),
            SkinPerformanceSimplifyCounters = config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyCounters),
            SkinPerformanceDisableKiaiFlashing = config.Get<bool>(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing),
            SkinPerformanceBlackBackground = config.Get<bool>(OsuSetting.ForkSkinPerformanceBlackBackground),
            ArgonFollowRing = config.Get<bool>(OsuSetting.ForkArgonFollowRing),
            DeferredVertexUploadBatching = config.Get<bool>(OsuSetting.ForkDeferredVertexUploadBatching),
            DeferredDirectVertexUpload = config.Get<bool>(OsuSetting.ForkDeferredDirectVertexUpload),
            DeferredDirectUniformUpload = config.Get<bool>(OsuSetting.ForkDeferredDirectUniformUpload),
            VeldridPipelineLookupCache = config.Get<bool>(OsuSetting.ForkVeldridPipelineLookupCache),
            StaticChildLifetimeCache = config.Get<bool>(OsuSetting.ForkStaticChildLifetimeCache),
            Use8kPollingRate = config.Get<bool>(OsuSetting.ForkUse8kPollingRate),
            AllowTearing = config.Get<bool>(OsuSetting.ForkAllowTearing),
            UpdateThreadSpinWait = config.Get<bool>(OsuSetting.ForkUpdateThreadSpinWait),
        };

        private (List<MosuDiagnosticsProfile> Profiles, List<MosuDiagnosticsSkippedProfile> SkippedProfiles) getProfilePlan(
            MosuDiagnosticsSetupOptions options,
            RendererType renderer)
        {
            IReadOnlyList<MosuDiagnosticsProfile> source = options.Mode switch
            {
                MosuDiagnosticsMode.Deep => MosuDiagnosticsProfiles.DeepProfiles,
                MosuDiagnosticsMode.Extended => MosuDiagnosticsProfiles.ExtendedProfiles,
                MosuDiagnosticsMode.RendererComparison => new[]
                {
                    MosuDiagnosticsProfile.Recommended,
                },
                _ => new[]
                {
                    MosuDiagnosticsProfile.Current,
                    MosuDiagnosticsProfile.Recommended,
                },
            };

            if (options.Mode is not (MosuDiagnosticsMode.Deep or MosuDiagnosticsMode.Extended))
                return (source.ToList(), new List<MosuDiagnosticsSkippedProfile>());

            return MosuDiagnosticsProfiles.CreateApplicablePlan(source, renderer, OperatingSystem.IsWindows());
        }

        private void restartWithArgs(IEnumerable<string> args)
        {
            string waitForCurrentProcessArg = $"--mosu-diagnostics-wait-pid={Environment.ProcessId}";
            string arguments = string.Join(' ', args
                                                 .Append(waitForCurrentProcessArg)
                                                 .Select(MosuPerformanceDiagnosticsRunner.QuoteArg));

            allowNextProcessToSetForegroundWindow();

            game.RestartOnExitAction = () =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                };

                if (OperatingSystem.IsWindows())
                    startInfo.Environment["SDL_FORCE_RAISEWINDOW"] = "1";

                Process.Start(startInfo);
            };

            Schedule(game.Exit);
        }

        private static void allowNextProcessToSetForegroundWindow()
        {
            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                if (!allowSetForegroundWindow(asfw_any))
                {
                    Logger.Log(
                        $"mosu diagnostics could not hand off foreground activation: win32_error={Marshal.GetLastWin32Error()}",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                Logger.Error(ex, "mosu diagnostics could not initialise foreground activation handoff.");
            }
        }

        [DllImport("user32.dll", EntryPoint = "AllowSetForegroundWindow", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool allowSetForegroundWindow(uint processId);
    }
}
