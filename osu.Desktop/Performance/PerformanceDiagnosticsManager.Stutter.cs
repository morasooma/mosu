using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Statistics;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Performance.Diagnostics;
using osu.Game.Screens.Play;

namespace osu.Desktop.Performance
{
    public partial class PerformanceDiagnosticsManager
    {
        [Resolved(canBeNull: true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        private Bindable<bool>? performanceLogging;
        private PerformanceRecordingController? recording;
        private IScreen? lastStutterScreen;
        private LocalUserPlayingState? lastStutterPlayingState;
        private bool? lastStutterActive;

        private void initialiseStutterRecording()
        {
            recording = new PerformanceRecordingController(captureStutterTraceAsync, (path, error, unexpectedStop) =>
            {
                if (unexpectedStop || error != null)
                    performanceLogging!.Value = false;
                if (error != null)
                {
                    Logger.Error(error, "Performance stack capture failed");
                    notificationOverlay?.Post(new SimpleErrorNotification
                    {
                        Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4068(error.Message),
                    });
                }
                else
                {
                    notificationOverlay?.Post(new SimpleNotification
                    {
                        Text = unexpectedStop
                            ? global::osu.Game.Localisation.ClientTranslationStrings.J4069
                            : global::osu.Game.Localisation.ClientTranslationStrings.J4070,
                    });
                }
            });
            performanceLogging = config.GetBindable<bool>(OsuSetting.ForkPerformanceLogging);
            // The automated benchmark needs CSV metrics, not the live EventPipe stack capture.
            // Running both would add profiling overhead to every A/B measurement.
            if (!game.IsDiagnosticsBenchmark)
                performanceLogging.BindValueChanged(e => recording.SetEnabled(e.NewValue), true);
        }

        // Called on Update. Export joins this exact capture; it cannot read a half-written nettrace.
        public Task<string?> FinishPerformanceRecordingAsync()
        {
            performanceLogging!.Value = false;
            recording!.SetEnabled(false);
            Task<string>? pending = recording.Pending;
            return pending != null ? finish(pending)
                : recording.LastError != null ? Task.FromException<string?>(recording.LastError)
                : Task.FromResult(recording.LastPath);

            static async Task<string?> finish(Task<string> pending) => await pending.ConfigureAwait(false);
        }

        // Only called by the entry point after host.Run returns and before host disposal.
        // No Update callbacks are needed for recorder finalisation.
        public Task<string?> StopPerformanceRecordingOnExitAsync() => recording?.StopAsync() ?? Task.FromResult<string?>(null);

        private Task<string> captureStutterTraceAsync(CancellationToken cancellationToken)
        {
            string root = storage.GetStorageForDirectory(Path.Combine("exports", "stutter-traces")).GetFullPath(string.Empty, true);
            string id = $"stutter-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            string directory = Path.Combine(root, id);
            string logs = storage.GetStorageForDirectory("logs").GetFullPath(string.Empty);
            string performance = storage.GetStorageForDirectory("performance").GetFullPath(string.Empty);
            var context = new
            {
                game.Version,
                game.VersionHash,
                renderer = host.RendererInfo,
                initialScreen = game.ScreenStack?.CurrentScreen?.GetType().FullName,
                frameworkAssembly = typeof(StutterTraceEventSource).Assembly.ManifestModule.ModuleVersionId,
                gameAssembly = typeof(osu.Game.OsuGame).Assembly.ManifestModule.ModuleVersionId,
                desktopAssembly = GetType().Assembly.ManifestModule.ModuleVersionId,
            };

            return Task.Run(async () =>
            {
                await StutterTraceRecorder.CaptureAsync(directory, context, cancellationToken, ready: () => Schedule(() =>
                {
                    if (performanceLogging?.Value == true)
                        notificationOverlay?.Post(new SimpleNotification
                        {
                            Text = global::osu.Game.Localisation.ClientTranslationStrings.J4071,
                        });
                })).ConfigureAwait(false);
                using (var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "metadata.json")).ConfigureAwait(false)))
                {
                    if (metadata.RootElement.TryGetProperty("eventsLost", out var lost) && lost.ValueKind == JsonValueKind.Number && lost.GetInt32() > 0)
                    {
                        int count = lost.GetInt32();
                        Logger.Log($"Performance recording finalized with {count} lost events DURING capture; coverage is incomplete (not time before logging).", LoggingTarget.Performance, LogLevel.Important);
                        Schedule(() => notificationOverlay?.Post(new SimpleNotification
                        {
                            Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4072(count),
                        }));
                    }
                }
                try
                {
                    copyRecentLogs(logs, Path.Combine(directory, "logs"));
                    copyRecentLogs(performance, Path.Combine(directory, "performance"));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    await File.WriteAllTextAsync(Path.Combine(directory, "log-export-error.txt"), e.Message).ConfigureAwait(false);
                }
                string zip = Path.Combine(root, id + ".zip");
                ZipFile.CreateFromDirectory(directory, zip, CompressionLevel.Fastest, false);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(zip, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                return zip;
            });
        }

        protected override void Update()
        {
            base.Update();
            recording?.Update();
            if (!StutterTraceEventSource.Log.IsEnabled())
            {
                lastStutterScreen = null;
                lastStutterPlayingState = null;
                lastStutterActive = null;
                return;
            }

            IScreen? screen = game.ScreenStack?.CurrentScreen;
            LocalUserPlayingState state = screen is Player player ? player.PlayingState.Value : LocalUserPlayingState.NotPlaying;
            bool active = host.IsActive.Value;
            if (!ReferenceEquals(screen, lastStutterScreen) || state != lastStutterPlayingState || active != lastStutterActive)
            {
                lastStutterScreen = screen;
                lastStutterPlayingState = state;
                lastStutterActive = active;
                StutterTraceEventSource.Log.Marker("ScreenState", $"{screen?.GetType().FullName}|{state}|active={active}", Stopwatch.GetTimestamp());
            }
        }

        private static void copyRecentLogs(string source, string destination)
        {
            if (!Directory.Exists(source))
                return;

            long remaining = 16 * 1024 * 1024;
            Directory.CreateDirectory(destination);
            foreach (var path in Directory.EnumerateFiles(source).Where(p => p.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                                          .OrderByDescending(File.GetLastWriteTimeUtc).Take(12))
            {
                if (remaining == 0)
                    break;
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                long length = Math.Min(input.Length, Math.Min(remaining, 4 * 1024 * 1024));
                input.Seek(-length, SeekOrigin.End);
                string target = Path.Combine(destination, Path.GetFileName(path) + ".tail.txt");
                using var output = File.Create(target);
                byte[] buffer = new byte[65536];
                while (length > 0)
                {
                    int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
                    if (read == 0)
                        break;
                    output.Write(buffer, 0, read);
                    remaining -= read;
                    length -= read;
                }
            }
        }
    }
}
