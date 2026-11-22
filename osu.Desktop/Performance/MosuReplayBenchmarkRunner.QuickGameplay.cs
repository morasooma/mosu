using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Screens;
using osu.Framework.Statistics;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using SDL;
using SixLabors.ImageSharp;
using Size = System.Drawing.Size;

namespace osu.Desktop.Performance
{
    internal partial class MosuReplayBenchmarkRunner
    {
        private const int quick_gameplay_seconds = 10;
        private CancellationTokenSource? quickCaptureCancellation;

        private void startQuickGameplay(ReplayPlayer player, int run)
        {
            if (!player.LoadedBeatmapSuccessfully || host.Window?.ClientSize is not { Width: 1920, Height: 1080 })
            {
                failBenchmark("quick_gameplay_requires_loaded_replay_and_1920x1080");
                return;
            }

            string directory = storage.GetFullPath(Path.Combine("performance", $"quick-gameplay-{benchmarkId}"), true);
            Directory.CreateDirectory(directory);
            var skin = skinManager.CurrentSkin.Value;
            var skinInfo = skin.SkinInfo.PerformRead(s => new { s.ID, s.Name });
            if (Environment.GetEnvironmentVariable("MOSU_QUICK_EXPECT_SKIN") is { } expected && !skinInfo.ID.ToString().Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                failBenchmark($"quick_gameplay_wrong_skin:expected={expected};actual={skinInfo.ID}");
                return;
            }

            var context = new
            {
                ProcessId = Environment.ProcessId,
                Width = 1920,
                Height = 1080,
                Renderer = describeRenderer(),
                Skin = skinInfo,
                SkinType = skin.GetType().Name,
                SkinPerformance = SkinPerformanceMode.DescribeEffectiveState(),
                FrameSync = frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync).ToString(),
                DrawHz = host.DrawThread.ActiveHz,
                UpdateHz = host.UpdateThread.ActiveHz,
                Beatmap = selectedScore!.BeatmapInfo!.ToString(),
                BeatmapId = selectedScore.BeatmapInfo.OnlineID,
                BeatmapHash = selectedScore.BeatmapInfo.MD5Hash,
                ReplayHash = replayHash,
                Mods = player.GameplayState.Mods.Select(m => m.Acronym).ToArray(),
                ObjectCount = player.GameplayState.Beatmap.HitObjects.Count,
                LastObjectMs = player.GameplayState.Beatmap.GetLastObjectTime(),
            };
            File.WriteAllText(Path.Combine(directory, "session.json"), JsonSerializer.Serialize(context));
            bool sampleStacks = Environment.GetEnvironmentVariable("MOSU_QUICK_STACKS") == "1";
            Task<(Size Surface, RectangleI Viewport)> initialGeometry = queryQuickDrawGeometry();
            var cancellation = quickCaptureCancellation = new CancellationTokenSource();
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<string> trace = Task.Run(() => StutterTraceRecorder.CaptureGameplayAsync(directory, context, cancellation.Token, () => ready.TrySetResult(), sampleStacks));
            _ = trace.ContinueWith(t =>
            {
                _ = t.Exception;
                cancellation.Dispose();
            }, TaskScheduler.Default);

            long start = 0;
            double firstJudgement = 0;
            int startJudgements = 0;
            // Reuse a single scheduled delegate to avoid adding allocation pressure to the capture.
            ScheduledDelegate? tickTask = null;
            void tick()
            {
                if (benchmarkFailureReason != null || currentRunCompleted)
                {
                    tickTask?.Cancel();
                    return;
                }
                if (trace.IsCompleted)
                {
                    tickTask?.Cancel();
                    failBenchmark("quick_gameplay_trace_stopped_early");
                    return;
                }
                if (player.GameplayState.HasCompleted || !player.IsCurrentScreen())
                {
                    tickTask?.Cancel();
                    failBenchmark("replay_ended_before_quick_gameplay_window");
                    return;
                }

                double judgement = GameplayPerformanceSnapshot.LastJudgementTime;
                if (start == 0 && initialGeometry.IsCompleted
                    && (!initialGeometry.IsCompletedSuccessfully || !isQuickGeometryValid(initialGeometry.GetResultSafely())))
                {
                    tickTask?.Cancel();
                    failBenchmark($"quick_gameplay_invalid_initial_geometry:{describeQuickGeometry(initialGeometry)}");
                    return;
                }
                if (start == 0 && initialGeometry.IsCompletedSuccessfully && ready.Task.IsCompleted && player.GameplayState.LastJudgementResult.Value != null)
                {
                    firstJudgement = judgement;
                    startJudgements = player.GameplayState.ScoreProcessor.Statistics.Values.Sum();
                    start = Stopwatch.GetTimestamp();
                    StutterTraceEventSource.Log.Marker("QuickGameplayStart", string.Empty, start);
                }

                long end = Stopwatch.GetTimestamp();
                if (start == 0 || Stopwatch.GetElapsedTime(start, end).TotalSeconds < quick_gameplay_seconds)
                    return;

                tickTask?.Cancel();
                StutterTraceEventSource.Log.Marker("QuickGameplayEnd", string.Empty, end);
                cancelQuickGameplay();
                int judgements = player.GameplayState.ScoreProcessor.Statistics.Values.Sum() - startJudgements;
                if (judgement - firstJudgement < 5000 || judgements == 0 || skinManager.CurrentSkinInfo.Value.ID != skinInfo.ID)
                {
                    failBenchmark("quick_gameplay_did_not_advance_or_skin_changed");
                    return;
                }

                _ = saveEvidence(end, judgement, judgements);
            }

            tickTask = Scheduler.AddDelayed(tick, 0, repeat: true);

            async Task saveEvidence(long end, double judgement, int judgements)
            {
                try
                {
                    // Geometry and readback are checked outside the measured interval.
                    var initial = await initialGeometry.ConfigureAwait(false);
                    var final = await queryQuickDrawGeometry().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    if (!isQuickGeometryValid(final))
                        throw new InvalidOperationException($"Wrong final draw geometry: {final}");
                    using var screenshot = await host.TakeScreenshotAsync().ConfigureAwait(false);
                    if (screenshot.Width != 1920 || screenshot.Height != 1080)
                        throw new InvalidOperationException($"Wrong framebuffer size: {screenshot.Width}x{screenshot.Height}");
                    await screenshot.SaveAsPngAsync(Path.Combine(directory, "frame.png")).ConfigureAwait(false);
                    await trace.ConfigureAwait(false);
                    File.WriteAllText(Path.Combine(directory, "window.json"), JsonSerializer.Serialize(new
                    {
                        StartTicks = start,
                        EndTicks = end,
                        Frequency = Stopwatch.Frequency,
                        ElapsedMs = Stopwatch.GetElapsedTime(start, end).TotalMilliseconds,
                        FirstJudgementMs = firstJudgement,
                        LastJudgementMs = judgement,
                        Judgements = judgements,
                        screenshot.Width,
                        screenshot.Height,
                        SurfaceAtStart = new { initial.Surface.Width, initial.Surface.Height },
                        SurfaceAtEnd = new { final.Surface.Width, final.Surface.Height },
                        ViewportAtStart = new { initial.Viewport.X, initial.Viewport.Y, initial.Viewport.Width, initial.Viewport.Height },
                        ViewportAtEnd = new { final.Viewport.X, final.Viewport.Y, final.Viewport.Width, final.Viewport.Height },
                    }));
                    Schedule(() => completeRun(run, "quick_gameplay_window"));
                }
                catch (Exception e)
                {
                    Schedule(() => failBenchmark($"quick_gameplay_evidence_failed:{e.Message}"));
                }
            }
        }

        private Task<(Size Surface, RectangleI Viewport)> queryQuickDrawGeometry()
        {
            var result = new TaskCompletionSource<(Size, RectangleI)>(TaskCreationOptions.RunContinuationsAsynchronously);
            host.DrawThread.Scheduler.Add(() =>
            {
                try
                {
                    result.SetResult((getCurrentEglDrawSize(), host.Renderer.Viewport));
                }
                catch (Exception e)
                {
                    result.SetException(e);
                }
            }, true);
            return result.Task;
        }

        private static bool isQuickGeometryValid((Size Surface, RectangleI Viewport) geometry) =>
            geometry.Surface == new Size(1920, 1080) && geometry.Viewport == new RectangleI(0, 0, 1920, 1080);

        private static string describeQuickGeometry(Task<(Size Surface, RectangleI Viewport)> geometry) => geometry.IsCompletedSuccessfully
            ? geometry.GetResultSafely().ToString()
            : geometry.Exception?.GetBaseException().Message ?? geometry.Status.ToString();

        private static unsafe Size getCurrentEglDrawSize()
        {
            const int egl_draw = 0x3059;
            const int egl_width = 0x3057;
            const int egl_height = 0x3056;
            var getCurrentSurface = (delegate* unmanaged[Cdecl]<int, IntPtr>)SDL3.SDL_EGL_GetProcAddress("eglGetCurrentSurface");
            var querySurface = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int*, uint>)SDL3.SDL_EGL_GetProcAddress("eglQuerySurface");
            IntPtr display = SDL3.SDL_EGL_GetCurrentDisplay();
            if (getCurrentSurface == null || querySurface == null || display == IntPtr.Zero)
                throw new InvalidOperationException("No current SDL EGL display or surface query API");

            IntPtr surface = getCurrentSurface(egl_draw);
            int width, height;
            if (surface == IntPtr.Zero || querySurface(display, surface, egl_width, &width) == 0 || querySurface(display, surface, egl_height, &height) == 0)
                throw new InvalidOperationException("Unable to query actual EGL draw surface size");
            return new Size(width, height);
        }

        private void cancelQuickGameplay()
        {
            var cancellation = quickCaptureCancellation;
            quickCaptureCancellation = null;
            if (cancellation == null)
                return;
            try { cancellation.Cancel(); }
            catch (ObjectDisposedException) { } // The trace can fail or hit its limit before the game window ends.
        }
    }
}
