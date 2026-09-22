// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Mixing;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Graphics.Shaders;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Beatmaps;
using osu.Game.Utils;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Standalone public build note: High-performance asynchronous Veldrid texture readback
// via GameHost.BufferedScreenshotRequest is an optional Mosu framework extension. Standalone public builds
// fall back to upstream host.TakeScreenshotAsync() for frame capture.

namespace osu.Game.Scoring.Render
{
    /// <summary>
    /// A special <see cref="ReplayPlayer"/> subclass used for offline replay rendering.
    /// It drives the gameplay clock at a fixed output frame rate, captures each
    /// rendered frame via <see cref="GameHost.TakeScreenshotAsync"/>, and pipes the raw
    /// RGBA pixels to an <see cref="FFmpegEncoder"/>.
    /// </summary>
    public partial class RenderReplayPlayer : ReplayPlayer
    {
        private const double results_animation_display_duration = Player.RESULTS_DISPLAY_DELAY + 2500;
        private const double forced_results_display_duration = 2500;
        private const double simulation_update_rate = 240;
        private const double hitsound_capture_tail_duration = 1000;
        private const double render_music_volume = 1.0;
        private const int capture_pipeline_depth = 3;
        private const int frame_writer_queue_capacity = capture_pipeline_depth;

        private readonly int outputWidth;
        private readonly int outputHeight;
        private readonly int fps;
        private readonly int bitrateMbps;
        private readonly string encoderName;
        private readonly ReplayRenderQualityPreset qualityPreset;
        private readonly string outputPath;
        private readonly bool flipVertical;
        private readonly Score score;
        private readonly string? audioPath;
        private readonly Func<bool>? isCancellationRequested;
        private readonly double? customStartTime;
        private readonly double? customEndTime;
        private readonly bool showResultsAfterCustomPeriod;
        private readonly double userMusicVolume;
        private readonly double userEffectVolume;
        private RenderGameplayHitsoundRenderer gameplayHitsoundRenderer = null!;

        public Action<string>? OnRenderComplete;
        public Action? OnRenderCancelled;
        public Action<Exception>? OnRenderError;
        public Action<int, int>? OnProgress;

        public int TotalFrames { get; private set; }
        public int RenderedFrames { get; private set; }

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private OsuGameBase gameBase { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private ShaderManager shaders { get; set; } = null!;

        private FFmpegEncoder? ffmpeg;
        private CancellationTokenSource? cts;
        private bool renderStarted;
        private double renderStartTime;
        private double renderEndTime;
        private double renderAppliedAudioOffset;
        private double renderAudioOffset;
        private double renderAudioDelay;
        private double renderAudioSourceDuration;
        private double renderAudioFrequencyRate = 1;
        private double renderAudioTempoRate = 1;
        private double renderGameplayRate = 1;
        private double renderFrameStep = 1000.0 / 60;
        private double renderOutputDuration;
        private double hitsoundCaptureEndTime;
        private int forcedResultsStartFrame = -1;
        private bool forcedResultsShown;
        private double renderSimulationUpdateRate = simulation_update_rate;
        private string activeOutputPath = string.Empty;
        private string? debugTracePath;
        private StreamWriter? debugTraceWriter;
        private RenderHitsoundCapture? hitsoundCapture;
        private string? hitsoundCapturePath;
        private Channel<FrameWriteRequest>? frameWriteChannel;
        private Task? frameWriterTask;
        // Saved host Hz limits restored on dispose so we don't leave the host capped.
        private double savedMaxUpdateHz;
        private double savedMaxDrawHz;
        private readonly Stopwatch renderWallClock = new Stopwatch();
        private double totalFrameCaptureWallTimeMs;
        private double totalFrameQueueWriteWallTimeMs;
        private long peakManagedBytes;
        private long peakWorkingSetBytes;
        private long peakPrivateBytes;

        public override bool CursorVisible => false;

        protected override bool SuppressSamplePlayback => true;
        protected override bool AllowSamplePlaybackDuringCatchUp => false;

        public RenderReplayPlayer(Score score, int outputWidth, int outputHeight, int fps, int bitrateMbps, string encoderName, ReplayRenderQualityPreset qualityPreset, string outputPath, bool flipVertical, string? audioPath = null,
                                  double userMusicVolume = 1.0, double userEffectVolume = 1.0,
                                  Func<bool>? isCancellationRequested = null,
                                  double? customStartTime = null, double? customEndTime = null, bool showResultsAfterCustomPeriod = false)
            : base(score, new PlayerConfiguration
            {
                ShowLeaderboard = false,
                AllowUserInteraction = false,
                ShowResults = true,
            })
        {
            this.score = score;
            this.outputWidth = outputWidth;
            this.outputHeight = outputHeight;
            this.fps = fps;
            this.bitrateMbps = bitrateMbps;
            this.encoderName = encoderName;
            this.qualityPreset = qualityPreset;
            this.outputPath = outputPath;
            this.flipVertical = flipVertical;
            this.audioPath = audioPath;
            this.userMusicVolume = Math.Clamp(userMusicVolume, 0, 1);
            this.userEffectVolume = Math.Clamp(userEffectVolume, 0, 1);
            this.isCancellationRequested = isCancellationRequested;
            this.customStartTime = customStartTime;
            this.customEndTime = customEndTime;
            this.showResultsAfterCustomPeriod = showResultsAfterCustomPeriod;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = (DependencyContainer)base.CreateChildDependencies(parent);

            Mods.Value = score.ScoreInfo.Mods;
            Ruleset.Value = score.ScoreInfo.Ruleset;

            var audioManager = parent.Get<AudioManager>();
            gameplayHitsoundRenderer = new RenderGameplayHitsoundRenderer(audioManager);
            dependencies.CacheAs<IRenderGameplayHitsoundRenderer>(gameplayHitsoundRenderer);
            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            gameplayHitsoundRenderer.SkinSourceProvider = () => GameplaySkinSource;
            Logger.Log("[RenderReplayPlayer] LoadComplete reached.", LoggingTarget.Runtime, LogLevel.Verbose);
            hideReplayInterface();
            Scheduler.Add(startCapture);
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            Logger.Log("[RenderReplayPlayer] OnEntering reached.", LoggingTarget.Runtime, LogLevel.Verbose);

            FinishTransforms(true);
            Alpha = 1;
            Scale = Vector2.One;

            GameplayClockContainer.FinishTransforms(true);
            GameplayClockContainer.Alpha = 1;
        }

        private void hideReplayInterface()
        {
            config.SetValue(OsuSetting.ReplaySettingsOverlay, false);
            ReplayOverlay.Settings.Expanded.Value = false;
        }

        private void prepareDeterministicRenderState()
        {
            DrawableRuleset.FrameStablePlayback = true;
            DrawableRuleset?.Cursor?.PrepareForDeterministicRender(DrawableRuleset.FrameStableClock);
            resetCursorTrail();
            renderGame?.SetRenderSceneTime(0);
        }

        private ReplayRenderGame? renderGame => gameBase as ReplayRenderGame;

        private void startCapture()
        {
            if (renderStarted)
            {
                Logger.Log("[RenderReplayPlayer] startCapture skipped because render already started.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            if (!LoadedBeatmapSuccessfully)
            {
                Logger.Log("[RenderReplayPlayer] startCapture skipped because beatmap was not loaded successfully.", LoggingTarget.Runtime, LogLevel.Error);
                OnRenderError?.Invoke(new InvalidOperationException("Beatmap failed to load for replay render."));
                return;
            }

            renderStarted = true;
            cts = new CancellationTokenSource();

            try
            {
                double outputMsPerFrame = 1000.0 / fps;
                double? endTime = Score?.Replay?.Frames?.LastOrDefault()?.Time;

                if (!endTime.HasValue)
                    throw new InvalidOperationException("Replay render could not determine the final replay frame time.");

                renderAudioFrequencyRate = Math.Abs(GameplayClockContainer.AdjustmentsFromMods.AggregateFrequency.Value);
                renderAudioTempoRate = Math.Abs(GameplayClockContainer.AdjustmentsFromMods.AggregateTempo.Value);
                renderGameplayRate = renderAudioFrequencyRate * renderAudioTempoRate;

                if (renderGameplayRate <= 0)
                    renderGameplayRate = Math.Abs(GameplayClockContainer.GetTrueGameplayRate());

                if (renderGameplayRate <= 0)
                    renderGameplayRate = Math.Abs(ModUtils.CalculateRateWithMods(GameplayState.Mods));

                if (renderGameplayRate <= 0)
                    throw new InvalidOperationException("Replay render does not support non-positive gameplay rates.");

                // Skip the beatmap intro by starting at GameplayStartTime.
                // We add a 1000ms lead-in (pre-roll) so the video doesn't start abruptly on the first note.
                double startTime = GameplayClockContainer.GameplayStartTime;
                double leadInTime = Math.Max(GameplayClockContainer.StartTime, startTime - 1000);

                if (endTime.HasValue && leadInTime >= endTime.Value)
                    leadInTime = GameplayClockContainer.StartTime;

                double gameplayCompletionTime = Math.Max(endTime.Value, GameplayState.Beatmap.GetLastObjectTime());
                double? firstObjectTime = GameplayState.Beatmap.HitObjects.FirstOrDefault()?.StartTime;

                if (GameplayState.Storyboard.LatestEventTime.HasValue)
                    gameplayCompletionTime = Math.Max(gameplayCompletionTime, GameplayState.Storyboard.LatestEventTime.Value);

                // The capture always starts from leadInTime conceptually (so the game processes ALL
                // hit objects from the beginning, keeping combo/score/accuracy counters correct),
                // but we only CAPTURE frames within the trim period. Frames before customStartTime
                // are processed via fast catch-up (Update ticks without screenshot capture).
                renderStartTime = customStartTime ?? leadInTime;
                double captureStartTime = renderStartTime;
                renderEndTime = customEndTime ?? (gameplayCompletionTime + results_animation_display_duration * renderGameplayRate);
                hitsoundCaptureEndTime = renderEndTime;
                bool appendForcedResults = showResultsAfterCustomPeriod && customEndTime.HasValue;
                renderAppliedAudioOffset = (GameplayClockContainer as RenderGameplayClockContainer)?.MusicFileOffset ?? 0;

                double rawAudioStartTime = renderStartTime - renderAppliedAudioOffset;
                double audioTimelineEndTime = renderEndTime + (appendForcedResults ? forced_results_display_duration * renderGameplayRate : 0);
                double rawAudioEndTime = audioTimelineEndTime - renderAppliedAudioOffset;

                renderAudioOffset = Math.Max(0, rawAudioStartTime);
                renderAudioDelay = Math.Max(0, -rawAudioStartTime) / renderGameplayRate;
                renderFrameStep = outputMsPerFrame * renderGameplayRate;
                renderAudioSourceDuration = Math.Max(0, rawAudioEndTime - renderAudioOffset) / 1000.0;

                // Seek to leadInTime for the catch-up phase (game processes all objects from start).
                GameplayClockContainer.Seek(leadInTime);

                gameplayHitsoundRenderer.PlaybackFrequency = 1;
                gameplayHitsoundRenderer.PlaybackTempo = 1;
                prepareDeterministicRenderState();
                renderSimulationUpdateRate = determineSimulationUpdateRate();

                int gameplayFrames = Math.Max(1, (int)Math.Ceiling((renderEndTime - captureStartTime) / renderFrameStep));
                int forcedResultsFrames = appendForcedResults
                    ? Math.Max(1, (int)Math.Ceiling(forced_results_display_duration * fps / 1000.0))
                    : 0;

                forcedResultsStartFrame = appendForcedResults ? gameplayFrames : -1;
                forcedResultsShown = false;
                TotalFrames = gameplayFrames + forcedResultsFrames;
                // Add one frame of headroom to the output duration so that -frames:v (not -t)
                // is the binding constraint for FFmpeg. Without this, -t can truncate slightly
                // short of the last frame due to decimal formatting, causing FFmpeg to stop
                // reading stdin before all frames are written.
                renderOutputDuration = (TotalFrames + 1) / (double)fps;
                initialiseAudioOutputs();

                Logger.Log($"[RenderReplayPlayer] Starting replay render (captureStart={renderStartTime:F0} ms, end={renderEndTime:F0} ms, gameplayEnd={gameplayCompletionTime:F0} ms, hitsoundCaptureEnd={hitsoundCaptureEndTime:F0} ms, gameplayRate={renderGameplayRate:0.###}, frequencyRate={renderAudioFrequencyRate:0.###}, tempoRate={renderAudioTempoRate:0.###}, frameStep={renderFrameStep:0.###} ms, appliedAudioOffset={renderAppliedAudioOffset:0.###} ms, audioOffset={renderAudioOffset:0.###} ms, audioDelay={renderAudioDelay:0.###} ms, audioSourceDuration={renderAudioSourceDuration:0.###} s, outputDuration={renderOutputDuration:0.###} s, totalFrames={TotalFrames}, simulationUpdateRate={renderSimulationUpdateRate:0.###}, trailType={getTrailDrawableType()}, trailPlacementAlgorithm={getTrailPlacementAlgorithm()}, legacyDisjointTrail={isLegacyDisjointTrail()}) -> {outputWidth}x{outputHeight} @ {fps} fps -> {activeOutputPath}",
                    LoggingTarget.Runtime,
                    LogLevel.Verbose);

                if (firstObjectTime.HasValue)
                {
                    double firstObjectVideoOffset = Math.Max(0, firstObjectTime.Value - (customStartTime ?? renderStartTime)) / renderGameplayRate;
                    Logger.Log($"[RenderReplayPlayer] First hit object starts at {firstObjectTime.Value:0.###} ms gameplay time and is expected at {firstObjectVideoOffset:0.###} ms in the rendered video timeline.",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);
                }

                initialiseDebugTrace();
                peakManagedBytes = 0;
                peakWorkingSetBytes = 0;
                peakPrivateBytes = 0;
                OnProgress?.Invoke(0, TotalFrames);
                renderWallClock.Restart();
                Task.Run(captureLoop, cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[RenderReplayPlayer] Failed to initialise replay render.");
                OnRenderError?.Invoke(ex);
            }
        }

        private async Task captureLoop()
        {
            if (cts == null)
                return;

            CancellationToken token = cts.Token;
            bool completedSuccessfully = false;
            bool cancelled = false;
            int frame = 0;
            Exception? postProcessingException = null;

            try
            {
                if (TotalFrames <= 0)
                    throw new InvalidOperationException("Replay render could not determine a valid frame count.");

                await stabiliseInitialFrameAsync(token).ConfigureAwait(false);

                // Throttle the host to the output fps for the duration of the render.
                savedMaxUpdateHz = host.MaximumUpdateHz;
                savedMaxDrawHz = host.MaximumDrawHz;
                host.MaximumUpdateHz = double.MaxValue;
                host.MaximumDrawHz = double.MaxValue;

                while (!token.IsCancellationRequested && frame < TotalFrames)
                {
                    if (isCancellationRequested?.Invoke() == true)
                    {
                        cancelled = true;
                        break;
                    }

                    await showForcedResultsIfRequiredAsync(frame, token).ConfigureAwait(false);

                    await writeDebugSnapshotAsync(frame, getFrameTime(frame), token).ConfigureAwait(false);

                    double previousFrameTime = frame == 0 ? renderStartTime : getFrameTime(frame - 1);
                    double previousSceneTime = frame == 0 ? getSceneTime(0) : getSceneTime(frame - 1);

                    using Image<Rgba32> currentCapture = await advanceAndCaptureAsync(
                        previousFrameTime,
                        getFrameTime(frame),
                        previousSceneTime,
                        getSceneTime(frame),
                        token).ConfigureAwait(false);

                    ffmpeg ??= createEncoder(currentCapture.Width, currentCapture.Height);
                    frameWriteChannel ??= Channel.CreateBounded<FrameWriteRequest>(new BoundedChannelOptions(frame_writer_queue_capacity)
                    {
                        SingleReader = true,
                        SingleWriter = true,
                        FullMode = BoundedChannelFullMode.Wait,
                    });
                    frameWriterTask ??= Task.Run(() => frameWriterLoopAsync(frameWriteChannel.Reader, ffmpeg.InputStream, token), token);

                    if (frameWriterTask.IsCompleted)
                        await frameWriterTask.ConfigureAwait(false);

                    FrameWriteRequest writeRequest = createFrameWriteRequest(currentCapture);
                    if (!await writeFrameToChannelWithTimeoutAsync(frameWriteChannel, writeRequest, token).ConfigureAwait(false))
                    {
                        cancelled = true;
                        break;
                    }

                    await captureHitsoundAudioAsync(frame + 1, token).ConfigureAwait(false);

                    frame++;

                    RenderedFrames = frame;
                    OnProgress?.Invoke(frame, TotalFrames);

                    if (frame == 1 || frame % Math.Max(1, fps) == 0)
                        logMemoryUsage(frame, "capture-loop");
                }

                if (!token.IsCancellationRequested)
                {
                    RenderedFrames = frame;
                    OnProgress?.Invoke(frame, TotalFrames);
                }

                cancelled |= token.IsCancellationRequested;
                completedSuccessfully = !cancelled && frame >= TotalFrames;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                if (ffmpeg?.LastErrorLine != null && ex is IOException)
                    ex = new InvalidOperationException(ffmpeg.LastErrorLine, ex);

                Logger.Error(ex, "[RenderReplayPlayer] Capture error.");
                OnRenderError?.Invoke(ex);
            }
            finally
            {
                Logger.Log("[RenderReplayPlayer] Capture loop finished, entering finalisation.", LoggingTarget.Runtime, LogLevel.Verbose);

                if (frameWriteChannel != null)
                    frameWriteChannel.Writer.TryComplete();

                if (frameWriterTask != null)
                {
                    try
                    {
                        Logger.Log("[RenderReplayPlayer] Waiting for frame writer to complete...", LoggingTarget.Runtime, LogLevel.Verbose);
                        await frameWriterTask.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
                        Logger.Log("[RenderReplayPlayer] Frame writer completed.", LoggingTarget.Runtime, LogLevel.Verbose);
                    }
                    catch (TimeoutException)
                    {
                        Logger.Log("[RenderReplayPlayer] Frame writer timed out (15s), force-closing FFmpeg stdin to unblock.",
                            LoggingTarget.Runtime,
                            LogLevel.Important);

                        try { ffmpeg?.InputStream.Close(); } catch { }
                        try { await frameWriterTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); } catch { }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during cancellation.
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[RenderReplayPlayer] Frame writer completed with error during finalisation: {ex.Message}",
                            LoggingTarget.Runtime,
                            LogLevel.Verbose);
                    }
                }

                // Restore host rate limits now that the capture loop has finished.
                if (savedMaxUpdateHz > 0)
                {
                    host.MaximumUpdateHz = savedMaxUpdateHz;
                    host.MaximumDrawHz = savedMaxDrawHz;
                }

                Logger.Log("[RenderReplayPlayer] Stopping beatmap track...", LoggingTarget.Runtime, LogLevel.Verbose);
                try
                {
                    // Track.Stop() may need the Update thread to process. In SingleThread mode
                    // the Update thread is shared, and calling Stop() from a Task.Run thread can
                    // deadlock if it synchronously waits on the audio system. Dispatch to the
                    // Update thread with a hard timeout so we never block the finalisation.
                    var trackStopTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    host.UpdateThread.Scheduler.Add(() =>
                    {
                        try { Beatmap.Value.Track.Stop(); } catch { }
                        trackStopTcs.TrySetResult();
                    });

                    if (!trackStopTcs.Task.Wait(TimeSpan.FromSeconds(5)))
                        Logger.Log("[RenderReplayPlayer] Track.Stop() timed out after 5s, continuing.", LoggingTarget.Runtime, LogLevel.Important);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[RenderReplayPlayer] Track.Stop() failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                }

                Logger.Log("[RenderReplayPlayer] Stopping FFmpeg...", LoggingTarget.Runtime, LogLevel.Verbose);
                ffmpeg?.Stop();

                Logger.Log("[RenderReplayPlayer] Disposing hitsound capture...", LoggingTarget.Runtime, LogLevel.Verbose);
                hitsoundCapture?.Dispose();

                Logger.Log("[RenderReplayPlayer] Logging final memory usage...", LoggingTarget.Runtime, LogLevel.Verbose);
                logMemoryUsage(RenderedFrames, "capture-final");

                writeDebugSummary(completedSuccessfully, cancelled, postProcessingException);
                debugTraceWriter?.Dispose();

                if (completedSuccessfully)
                {
                    Logger.Log("[RenderReplayPlayer] Starting audio post-processing...", LoggingTarget.Runtime, LogLevel.Verbose);
                    try
                    {
                        finaliseAudioPostProcessing();
                    }
                    catch (Exception ex)
                    {
                        postProcessingException = ex;
                    }
                }

                Logger.Log("[RenderReplayPlayer] Finalisation complete.", LoggingTarget.Runtime, LogLevel.Verbose);
            }

            if (postProcessingException != null)
            {
                OnRenderError?.Invoke(postProcessingException);
                return;
            }

            if (completedSuccessfully)
            {
                Logger.Log($"[RenderReplayPlayer] Render complete -> {outputPath}");
                OnRenderComplete?.Invoke(outputPath);
            }
            else if (cancelled)
            {
                Logger.Log($"[RenderReplayPlayer] Render cancelled -> {outputPath}");
                OnRenderCancelled?.Invoke();
            }
        }

        private static FrameWriteRequest createFrameWriteRequest(Image<Rgba32> image)
        {
            int byteCount = checked(image.Width * image.Height * 4);
            var buffer = new byte[byteCount];

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < image.Height; y++)
                    MemoryMarshal.AsBytes(accessor.GetRowSpan(y)).CopyTo(buffer.AsSpan(y * image.Width * 4, image.Width * 4));
            });

            return new FrameWriteRequest(buffer, byteCount);
        }

        private async Task<Image<Rgba32>> advanceAndCaptureAsync(double currentTime, double targetTime, double currentSceneTime, double targetSceneTime, CancellationToken token)
        {
            long startedAt = Stopwatch.GetTimestamp();
            double gameplayDelta = targetTime - currentTime;
            double sceneDelta = targetSceneTime - currentSceneTime;

            double simulationStep = renderGameplayRate * 1000.0 / Math.Max(fps, renderSimulationUpdateRate);
            int stepCount = Math.Abs(gameplayDelta) <= 0.0001 ? 1 : Math.Max(1, (int)Math.Ceiling(Math.Abs(gameplayDelta) / simulationStep));

            var tcs = new TaskCompletionSource<Image<Rgba32>?>(TaskCreationOptions.RunContinuationsAsynchronously);

            host.UpdateThread.Scheduler.Add(() =>
            {
                // Batch all simulation steps in a single Update tick
                for (int step = 1; step <= stepCount; step++)
                {
                    double progress = step / (double)stepCount;
                    double stepTime = currentTime + gameplayDelta * progress;
                    double stepSceneTime = currentSceneTime + sceneDelta * progress;

                    renderGame?.SetRenderSceneTime(stepSceneTime);
                    GameplayClockContainer.Seek(stepTime);
                }

                host.DrawThread.Scheduler.Add(() =>
                {
                    host.TakeScreenshotAsync().ContinueWith(t =>
                    {
                        if (t.IsFaulted) tcs.TrySetException(t.Exception!);
                        else if (t.IsCanceled) tcs.TrySetCanceled();
                        else tcs.TrySetResult(t.GetResultSafely());
                    });
                });
            });

            Image<Rgba32>? result;
            using (token.Register(() => tcs.TrySetCanceled()))
                result = await tcs.Task.ConfigureAwait(false);

            totalFrameCaptureWallTimeMs += Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            return result ?? throw new InvalidOperationException("Screenshot capture returned no image.");
        }

        // presentCurrentFrameAsync — single UpdateThread→DrawThread round-trip, used for stabilisation only.
        private Task presentCurrentFrameAsync(double targetTime, double sceneTime, CancellationToken token)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            host.UpdateThread.Scheduler.Add(() =>
            {
                renderGame?.SetRenderSceneTime(sceneTime);
                GameplayClockContainer.Seek(targetTime);

                host.DrawThread.Scheduler.Add(() => tcs.TrySetResult());
            });

            using (token.Register(() => tcs.TrySetCanceled()))
                return tcs.Task;
        }

        private async Task stabiliseInitialFrameAsync(CancellationToken token)
        {
            // When a custom trim start is set (e.g. 102s into the beatmap), the game needs to
            // process ALL hit objects from the beginning so combo/score/accuracy counters are
            // correct at the trim point. We do this by pumping Update ticks (without screenshot
            // capture) from leadInTime to renderStartTime. This is fast because each tick is
            // just a CPU-side game state update — no GPU readback or encoding.
            double seekTarget = renderStartTime;

            if (Math.Abs(seekTarget - GameplayClockContainer.StartTime) > 2000)
            {
                Logger.Log($"[RenderReplayPlayer] Fast catch-up from {GameplayClockContainer.StartTime:F0} ms to {seekTarget:F0} ms (no screenshot capture).",
                    LoggingTarget.Runtime,
                    LogLevel.Verbose);

                // Seek once to the target. The FrameStableClock will process time in ~16ms steps
                // per Update tick. We pump empty Update ticks (no screenshot capture) and wait
                // for the clock to converge.
                var seekTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                host.UpdateThread.Scheduler.Add(() =>
                {
                    GameplayClockContainer.Seek(seekTarget);
                    seekTcs.TrySetResult();
                });
                using (token.Register(() => seekTcs.TrySetCanceled()))
                    await seekTcs.Task.ConfigureAwait(false);

                int catchupTicks = 0;
                int maxTicks = 100000;
                double lastFrameStableTime = double.MinValue;

                while (catchupTicks < maxTicks && !token.IsCancellationRequested)
                {
                    var tickTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                    host.UpdateThread.Scheduler.Add(() =>
                    {
                        GameplayClockContainer.Seek(seekTarget);
                        tickTcs.TrySetResult();
                    });

                    using (token.Register(() => tickTcs.TrySetCanceled()))
                        await tickTcs.Task.ConfigureAwait(false);

                    catchupTicks++;

                    // Check the FrameStableClock (not GameplayClockContainer) — it lags behind
                    // the seek target while processing hit objects in ~16ms steps per Update tick.
                    double frameStableTime = DrawableRuleset?.FrameStableClock?.CurrentTime ?? 0;
                    if (Math.Abs(frameStableTime - seekTarget) < renderFrameStep)
                    {
                        Logger.Log($"[RenderReplayPlayer] Catch-up converged after {catchupTicks} ticks (frameStable={frameStableTime:F0} ms).",
                            LoggingTarget.Runtime,
                            LogLevel.Verbose);
                        break;
                    }

                    // Safety: if the frame-stable clock stopped advancing, abort to avoid infinite loop.
                    if (Math.Abs(frameStableTime - lastFrameStableTime) < 0.01 && catchupTicks > 100)
                    {
                        Logger.Log($"[RenderReplayPlayer] Catch-up clock stalled at {frameStableTime:F0} ms after {catchupTicks} ticks.",
                            LoggingTarget.Runtime,
                            LogLevel.Important);
                        break;
                    }

                    lastFrameStableTime = frameStableTime;
                }

                if (catchupTicks >= maxTicks)
                    Logger.Log($"[RenderReplayPlayer] Catch-up did not converge after {maxTicks} ticks.",
                        LoggingTarget.Runtime,
                        LogLevel.Important);
            }

            // Pump a few ordinary screenshots to let the scene settle visually before capture.
            for (int i = 0; i < 10; i++)
            {
                using Image<Rgba32> _ = await advanceAndCaptureAsync(renderStartTime, renderStartTime, 0, 0, token).ConfigureAwait(false);
            }

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            host.UpdateThread.Scheduler.Add(() =>
            {
                prepareDeterministicRenderState();
                tcs.TrySetResult();
            });

            using (token.Register(() => tcs.TrySetCanceled()))
                await tcs.Task.ConfigureAwait(false);

            await presentCurrentFrameAsync(renderStartTime, getSceneTime(0), token).ConfigureAwait(false);

            // Drain ALL accumulated hitsound data from the mixer AFTER catch-up, stabilisation and
            // deterministic state preparation — right before the first real capture frame.
            // FlushMixerAsync only resets the read position to 0 but does NOT clear the buffer data,
            // so hitsounds from the catch-up phase would still be readable via ChannelGetData.
            // We must read and discard everything that's in the buffer.
        }

        /// <summary>
        /// Reads and discards all data from the mixer buffer so that the next
        /// <see cref="RenderHitsoundCapture.CaptureFrameAsync"/> starts from a clean state.
        /// </summary>
        // captureFrameAsync removed and integrated into advanceAndCaptureAsync.

        protected override void Dispose(bool isDisposing)
        {
            cts?.Cancel();
            gameplayHitsoundRenderer?.Dispose();
            base.Dispose(isDisposing);
        }

        // Fallback capture moved inline to advanceAndCaptureAsync.

        protected override GameplayClockContainer CreateGameplayClockContainer(WorkingBeatmap beatmap, double gameplayStart)
        {
            if (!beatmap.TrackLoaded)
            {
                Logger.Log("[RenderReplayPlayer] Loading beatmap track for render clock.", LoggingTarget.Runtime, LogLevel.Verbose);
                beatmap.LoadTrack();
            }

            return new RenderGameplayClockContainer(beatmap, gameplayStart);
        }

        protected override ResultsScreen CreateResults(ScoreInfo score)
            => new RenderResultsScreen(score)
            {
                AllowWatchingReplay = false,
                AllowRetry = false,
                IsLocalPlay = true,
            };

        private async Task showForcedResultsIfRequiredAsync(int frame, CancellationToken token)
        {
            if (forcedResultsShown || forcedResultsStartFrame < 0 || frame < forcedResultsStartFrame)
                return;

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ResultsScreen? resultsScreen = null;

            void completeWhenResultsAreReady()
            {
                if (tcs.Task.IsCompleted)
                    return;

                if (token.IsCancellationRequested)
                {
                    tcs.TrySetCanceled();
                    return;
                }

                if (resultsScreen == null || (ReferenceEquals(this.GetChildScreen(), resultsScreen) && resultsScreen.LoadState >= LoadState.Ready))
                {
                    forcedResultsShown = true;
                    tcs.TrySetResult();
                    return;
                }

                host.UpdateThread.Scheduler.Add(completeWhenResultsAreReady);
            }

            host.UpdateThread.Scheduler.Add(() =>
            {
                try
                {
                    if (this.GetChildScreen() is ResultsScreen)
                    {
                        completeWhenResultsAreReady();
                        return;
                    }

                    Logger.Log($"[RenderReplayPlayer] Showing results after custom render period at frame {frame}.",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);

                    // Player.OnSuspending() asserts that gameplay cannot be resumed once a
                    // results screen is pushed. A forced result can happen before the natural
                    // gameplay completion path which normally sets this flag.
                    ValidForResume = false;
                    resultsScreen = CreateResults(score.ScoreInfo);
                    this.Push(resultsScreen);
                    completeWhenResultsAreReady();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            using (token.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
                }
                catch (TimeoutException ex)
                {
                    tcs.TrySetCanceled();
                    throw new TimeoutException("Results screen did not become ready within 30 seconds.", ex);
                }
            }
        }

        private FFmpegEncoder createEncoder(int captureWidth, int captureHeight)
        {
            if (captureWidth <= 0 || captureHeight <= 0)
                throw new InvalidOperationException("Rendered frame size is invalid.");

            Logger.Log($"[RenderReplayPlayer] Initialising encoder {captureWidth}x{captureHeight} -> {outputWidth}x{outputHeight} @ {fps} fps -> {activeOutputPath}");
            return new FFmpegEncoder(captureWidth, captureHeight, outputWidth, outputHeight, fps, bitrateMbps, encoderName, qualityPreset, activeOutputPath,
                flipVertical: flipVertical,
                audioPath: audioPath,
                audioOffsetMs: renderAudioOffset,
                audioDelayMs: renderAudioDelay,
                audioFrequencyRate: renderAudioFrequencyRate,
                audioTempoRate: renderAudioTempoRate,
                durationSeconds: renderAudioSourceDuration,
                outputDurationSeconds: renderOutputDuration,
                videoFrameCount: TotalFrames,
                audioVolume: render_music_volume);
        }

        private void resetCursorTrail() => DrawableRuleset?.Cursor?.GetType().GetMethod("ResetTrail")?.Invoke(DrawableRuleset.Cursor, null);

        private double getFrameTime(int frame)
            => Math.Min(renderEndTime, renderStartTime + frame * renderFrameStep);

        private double getSceneTime(int frame) => frame * 1000.0 / fps;

        private double determineSimulationUpdateRate()
        {
            var cursor = DrawableRuleset?.Cursor;
            var cursorType = cursor?.GetType();
            if (cursorType?.FullName == "osu.Game.Rulesets.Osu.UI.Cursor.OsuCursorContainer")
            {
                var prop = cursorType.GetProperty("RequiresHighFrequencyRenderSimulation");
                if (prop?.GetValue(cursor) is true)
                    return simulation_update_rate;

                return fps;
            }

            return simulation_update_rate;
        }

        private string getTrailDrawableType()
        {
            var cursor = DrawableRuleset?.Cursor;
            var cursorType = cursor?.GetType();
            if (cursorType?.FullName == "osu.Game.Rulesets.Osu.UI.Cursor.OsuCursorContainer")
            {
                var prop = cursorType.GetProperty("ActiveTrailDrawableType");
                return prop?.GetValue(cursor) as string ?? "unknown";
            }

            return "unknown";
        }

        private string getTrailPlacementAlgorithm()
        {
            var cursor = DrawableRuleset?.Cursor;
            var cursorType = cursor?.GetType();
            if (cursorType?.FullName == "osu.Game.Rulesets.Osu.UI.Cursor.OsuCursorContainer")
            {
                var prop = cursorType.GetProperty("ActiveTrailPlacementAlgorithm");
                return prop?.GetValue(cursor) as string ?? "unknown";
            }

            return "unknown";
        }

        private bool isLegacyDisjointTrail()
        {
            var cursor = DrawableRuleset?.Cursor;
            var cursorType = cursor?.GetType();
            if (cursorType?.FullName == "osu.Game.Rulesets.Osu.UI.Cursor.OsuCursorContainer")
            {
                var prop = cursorType.GetProperty("IsLegacyDisjointTrail");
                if (prop?.GetValue(cursor) is bool value)
                    return value;
            }

            return false;
        }

        private void initialiseAudioOutputs()
        {
            hitsoundCapture?.Dispose();
            hitsoundCapture = null;
            hitsoundCapturePath = null;
            activeOutputPath = outputPath;
            Logger.Log("[RenderReplayPlayer] Gameplay hitsound export is unavailable with the stock framework; rendering video and music only.",
                LoggingTarget.Runtime,
                LogLevel.Verbose);
        }

        private async Task captureHitsoundAudioAsync(int frameIndexExclusive, CancellationToken token)
        {
            if (hitsoundCapture == null)
                return;

            int cappedFrameIndexExclusive = Math.Min(frameIndexExclusive, getHitsoundCaptureFrameLimit());

            if (cappedFrameIndexExclusive <= 0)
                return;

            await hitsoundCapture.CaptureFrameAsync(cappedFrameIndexExclusive, token).ConfigureAwait(false);
        }

        private int getHitsoundCaptureFrameLimit()
        {
            double captureVideoDuration = Math.Max(0, hitsoundCaptureEndTime - renderStartTime) / renderGameplayRate;
            return Math.Max(0, (int)Math.Ceiling(captureVideoDuration * fps / 1000.0));
        }

        private void finaliseAudioPostProcessing()
        {
            if (hitsoundCapturePath == null || !File.Exists(hitsoundCapturePath))
            {
                if (!string.IsNullOrWhiteSpace(audioPath) && Math.Abs(userMusicVolume - 1.0) > 0.001 && File.Exists(outputPath))
                {
                    Logger.Log($"[RenderReplayPlayer] No hitsound capture; applying user music volume ({userMusicVolume:0.###}) to output.",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);
                    FFmpegPostProcessor.ApplyMusicVolume(outputPath, outputPath, userMusicVolume);
                }
                return;
            }

            try
            {
                var sampleTriggerRecorder = renderGame?.GameplaySampleTriggerRecorder;
                double? firstTriggerVideoTime = null;
                double? lastTriggerVideoTime = null;
                double? hitsoundMixDurationMs = null;
                double hitsoundTrimLeadingMs = 0;
                double hitsoundPlacementMs = 0;
                double? hitsoundPlayDurationMs = null;

                if (sampleTriggerRecorder?.FirstTriggerTime is double firstTriggerTime)
                    firstTriggerVideoTime = Math.Max(0, firstTriggerTime - renderStartTime) / renderGameplayRate;

                if (sampleTriggerRecorder?.LastTriggerTime is double lastTriggerTime)
                {
                    lastTriggerVideoTime = Math.Max(0, lastTriggerTime - renderStartTime) / renderGameplayRate;
                    hitsoundMixDurationMs = Math.Min(
                        Math.Max(0, (hitsoundCaptureEndTime - renderStartTime) / renderGameplayRate),
                        lastTriggerVideoTime.Value + hitsound_capture_tail_duration);
                }

                // Use the same value for both trim and placement to avoid compounding.
                // If they differ (e.g. FirstAudibleTimeMs detects noise at ~0 while firstTriggerVideoTime
                // is 10000ms at 0.1x speed), trim + delay stack and double the offset.
                // firstTriggerVideoTime is the reliable source — it comes from the gameplay clock, not
                // amplitude analysis which can be fooled by mixer noise.
                if (firstTriggerVideoTime.HasValue)
                {
                    hitsoundTrimLeadingMs = Math.Max(0, firstTriggerVideoTime.Value);
                    hitsoundPlacementMs = hitsoundTrimLeadingMs;
                }
                else if (hitsoundCapture?.FirstAudibleTimeMs is double firstAudibleTime)
                {
                    hitsoundTrimLeadingMs = Math.Max(0, firstAudibleTime);
                    hitsoundPlacementMs = hitsoundTrimLeadingMs;
                }

                if (hitsoundMixDurationMs.HasValue)
                    hitsoundPlayDurationMs = Math.Max(0, hitsoundMixDurationMs.Value - hitsoundPlacementMs);

                if (hitsoundCapture != null)
                {
                    Logger.Log($"[RenderReplayPlayer] Hitsound capture stats: requestedFrames={hitsoundCapture.TotalRequestedSampleFrames}, readFrames={hitsoundCapture.TotalReadSampleFrames}, silentFrames={hitsoundCapture.TotalSilentSampleFrames}, readBytes={hitsoundCapture.TotalReadBytes}, peakAmplitude={hitsoundCapture.PeakAmplitude:0.#####}, audible={hitsoundCapture.HasAudibleData}, recommendedGain={hitsoundCapture.RecommendedMixGain:0.###}, firstAudibleMs={(hitsoundCapture.FirstAudibleTimeMs?.ToString("0.###") ?? "n/a")}, lastAudibleMs={(hitsoundCapture.LastAudibleTimeMs?.ToString("0.###") ?? "n/a")}, firstTriggerVideoMs={(firstTriggerVideoTime?.ToString("0.###") ?? "n/a")}, lastTriggerVideoMs={(lastTriggerVideoTime?.ToString("0.###") ?? "n/a")}, triggerCount={(sampleTriggerRecorder?.TriggerCount.ToString() ?? "0")}, trimLeadingMs={hitsoundTrimLeadingMs:0.###}, placeAtMs={hitsoundPlacementMs:0.###}, playDurationMs={(hitsoundPlayDurationMs?.ToString("0.###") ?? "auto")}, mixDurationMs={(hitsoundMixDurationMs?.ToString("0.###") ?? "auto")}",
                        LoggingTarget.Runtime,
                        hitsoundCapture.HasAudibleData ? LogLevel.Verbose : LogLevel.Important);
                }

                if (hitsoundCapture?.HasAudibleData == true)
                {
                    Logger.Log($"[RenderReplayPlayer] Mixing captured hitsounds from {hitsoundCapturePath} into {outputPath}",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);
                    double effectiveHitsoundGain = (hitsoundCapture?.RecommendedMixGain ?? 1) * userEffectVolume;
                    Logger.Log($"[RenderReplayPlayer] Using client volume ratio for mix (music={userMusicVolume:0.###}, effect={userEffectVolume:0.###}, hitsoundGain={effectiveHitsoundGain:0.###}).",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);
                    FFmpegPostProcessor.MixHitsoundsIntoVideo(
                        activeOutputPath,
                        hitsoundCapturePath,
                        outputPath,
                        !string.IsNullOrWhiteSpace(audioPath),
                        effectiveHitsoundGain,
                        hitsoundTrimLeadingMs,
                        hitsoundPlacementMs,
                        hitsoundPlayDurationMs,
                        userMusicVolume);

                    if (activeOutputPath != outputPath && File.Exists(activeOutputPath))
                        File.Delete(activeOutputPath);

                    if (File.Exists(hitsoundCapturePath))
                        File.Delete(hitsoundCapturePath);
                }
                else if (activeOutputPath != outputPath && File.Exists(activeOutputPath))
                {
                    // No audible hitsounds captured. Apply the user's music volume to the intermediate video.
                    if (!string.IsNullOrWhiteSpace(audioPath) && Math.Abs(userMusicVolume - 1.0) > 0.001)
                    {
                        Logger.Log($"[RenderReplayPlayer] No audible hitsounds; applying user music volume ({userMusicVolume:0.###}) to final output.",
                            LoggingTarget.Runtime,
                            LogLevel.Verbose);
                        FFmpegPostProcessor.ApplyMusicVolume(activeOutputPath, outputPath, userMusicVolume);
                    }
                    else
                    {
                        if (File.Exists(outputPath))
                            File.Delete(outputPath);
                        File.Move(activeOutputPath, outputPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[RenderReplayPlayer] Failed to mux captured hitsounds into the final video.");
                throw;
            }
        }

        private void initialiseDebugTrace()
        {
            ReplayRenderDebugTraceMode debugTraceMode = config.Get<ReplayRenderDebugTraceMode>(OsuSetting.ForkReplayRenderDebugTraceMode);

            if (debugTraceMode == ReplayRenderDebugTraceMode.Disabled)
            {
                debugTracePath = null;
                debugTraceWriter = null;
                return;
            }

            debugTracePath = $"{outputPath}.debug.jsonl";
            debugTraceWriter = new StreamWriter(File.Open(debugTracePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true,
            };

            Logger.Log($"[RenderReplayPlayer] Writing debug trace to {debugTracePath}", LoggingTarget.Runtime, LogLevel.Verbose);

            debugTraceWriter.WriteLine(JsonConvert.SerializeObject(new
            {
                type = "session",
                outputPath,
                debugTracePath,
                outputWidth,
                outputHeight,
                fps,
                bitrateMbps,
                encoderName,
                renderStartTime,
                renderEndTime,
                renderGameplayRate,
                renderAudioFrequencyRate,
                renderAudioTempoRate,
                renderAppliedAudioOffset,
                renderAudioOffset,
                renderAudioDelay,
                renderAudioSourceDuration,
                renderOutputDuration,
                activeOutputPath,
                hitsoundCapturePath,
                totalFrames = TotalFrames,
                debugTraceMode = debugTraceMode.ToString(),
            }));
        }

        private async Task writeDebugSnapshotAsync(int frame, double targetTime, CancellationToken token)
        {
            if (debugTraceWriter == null)
                return;

            if (config.Get<ReplayRenderDebugTraceMode>(OsuSetting.ForkReplayRenderDebugTraceMode) != ReplayRenderDebugTraceMode.Full)
                return;

            var snapshot = await createDebugSnapshotAsync(frame, targetTime, token).ConfigureAwait(false);
            debugTraceWriter.WriteLine(JsonConvert.SerializeObject(snapshot));
        }

        private async Task frameWriterLoopAsync(ChannelReader<FrameWriteRequest> reader, Stream stream, CancellationToken token)
        {
            await foreach (FrameWriteRequest request in reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                try
                {
                    using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    writeCts.CancelAfter(5000);
                    await stream.WriteAsync(request.Buffer.AsMemory(0, request.ByteCount), writeCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                        throw;
                    
                    var ex = new TimeoutException("Frame write timed out! FFmpeg likely stopped reading.");
                    osu.Framework.Logging.Logger.Log($"[RenderReplayPlayer] {ex.Message}", osu.Framework.Logging.LoggingTarget.Runtime, osu.Framework.Logging.LogLevel.Important);
                    throw ex;
                }
                catch (IOException ex)
                {
                    if (token.IsCancellationRequested)
                        break;

                    throw new IOException("FFmpeg process ended unexpectedly.", ex);
                }
                catch (ObjectDisposedException ex)
                {
                    if (token.IsCancellationRequested)
                        break;

                    throw new ObjectDisposedException("FFmpeg pipe was disposed unexpectedly.", ex);
                }
            }
        }

        private readonly record struct FrameWriteRequest(byte[] Buffer, int ByteCount);

        /// <summary>
        /// Writes a frame to the channel with a hard timeout. If the frame writer is stuck
        /// (e.g. FFmpeg stopped reading stdin), the channel write would block indefinitely.
        /// This method detects the stall, force-closes FFmpeg's stdin to unblock the writer,
        /// and returns false so the caller can abort the render gracefully.
        /// </summary>
        private async Task<bool> writeFrameToChannelWithTimeoutAsync(Channel<FrameWriteRequest> channel, FrameWriteRequest capture, CancellationToken token)
        {
            long startedAt = Stopwatch.GetTimestamp();
            ValueTask writeTask = channel.Writer.WriteAsync(capture, token);
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), token);

            if (await Task.WhenAny(writeTask.AsTask(), timeoutTask).ConfigureAwait(false) == timeoutTask)
            {
                Logger.Log("[RenderReplayPlayer] Channel write timed out (frame writer stalled), force-closing FFmpeg stdin.",
                    LoggingTarget.Runtime,
                    LogLevel.Important);

                try { ffmpeg?.InputStream.Close(); } catch { }

                try { await frameWriterTask!.ConfigureAwait(false); } catch { }

                return false;
            }

            totalFrameQueueWriteWallTimeMs += Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            return true;
        }

        private void writeDebugSummary(bool completedSuccessfully, bool cancelled, Exception? postProcessingException)
        {
            if (debugTraceWriter == null)
                return;

            renderWallClock.Stop();

            double wallTimeMs = renderWallClock.Elapsed.TotalMilliseconds;
            double outputVideoDurationMs = renderOutputDuration * 1000;
            double averageFrameWallTimeMs = RenderedFrames > 0 ? wallTimeMs / RenderedFrames : 0;
            double effectiveRenderFps = wallTimeMs > 0 ? RenderedFrames / (wallTimeMs / 1000.0) : 0;
            double realtimeFactor = wallTimeMs > 0 ? outputVideoDurationMs / wallTimeMs : 0;

            debugTraceWriter.WriteLine(JsonConvert.SerializeObject(new
            {
                type = "summary",
                completedSuccessfully,
                cancelled,
                renderedFrames = RenderedFrames,
                totalFrames = TotalFrames,
                outputPath,
                activeOutputPath,
                hitsoundCapturePath,
                postProcessingError = postProcessingException?.Message,
                performance = new
                {
                    wallTimeMs,
                    outputVideoDurationMs,
                    averageFrameWallTimeMs,
                    effectiveRenderFps,
                    realtimeFactor,
                    averageAdvanceAndCaptureWallTimeMs = RenderedFrames > 0 ? totalFrameCaptureWallTimeMs / RenderedFrames : 0,
                    averageQueueWriteWallTimeMs = RenderedFrames > 0 ? totalFrameQueueWriteWallTimeMs / RenderedFrames : 0,
                    frameWriterQueueCapacity = frame_writer_queue_capacity,
                },
                memory = new
                {
                    peakManagedBytes,
                    peakWorkingSetBytes,
                    peakPrivateBytes,
                },
                hitsoundCapture = hitsoundCapture == null ? null : new
                {
                    hitsoundCapture.TotalRequestedSampleFrames,
                    hitsoundCapture.TotalReadSampleFrames,
                    hitsoundCapture.TotalSilentSampleFrames,
                    hitsoundCapture.TotalReadBytes,
                    hitsoundCapture.PeakAmplitude,
                    hitsoundCapture.HasAudibleData,
                    hitsoundCapture.RecommendedMixGain,
                    hitsoundCapture.FirstAudibleTimeMs,
                    hitsoundCapture.LastAudibleTimeMs,
                }
            }));
        }

        private async Task<Image<Rgba32>> measureCaptureAsync(Task<Image<Rgba32>> captureTask, long startedAt)
        {
            Image<Rgba32> image = await captureTask.ConfigureAwait(false);
            totalFrameCaptureWallTimeMs += Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            return image;
        }

        private Task<object> createDebugSnapshotAsync(int frame, double targetTime, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            host.UpdateThread.Scheduler.Add(() =>
            {
                try
                {
                    object? inputManager = getPropertyValue(DrawableRuleset, "KeyBindingInputManager");
                    object? replayHandler = getPropertyValue(inputManager, "ReplayInputHandler");
                    object? replayStartFrame = getPropertyValue(replayHandler, "StartFrame");
                    object? replayCurrentFrame = getPropertyValue(replayHandler, "CurrentFrame");
                    object? replayEndFrame = getPropertyValue(replayHandler, "EndFrame");
                    object? cursor = DrawableRuleset.Cursor;
                    object? activeCursor = getPropertyValue(cursor, "ActiveCursor");

                    tcs.TrySetResult(new
                    {
                        type = "frame",
                        frame,
                        targetTime,
                        sceneTime = renderGame?.RenderCaptureTarget.Clock.CurrentTime,
                        gameplayClockTime = GameplayClockContainer.CurrentTime,
                        gameplayClockRate = GameplayClockContainer.Rate,
                        gameplayClockElapsed = GameplayClockContainer.ElapsedFrameTime,
                        frameStableTime = DrawableRuleset.FrameStableClock.CurrentTime,
                        frameStableRate = DrawableRuleset.FrameStableClock.Rate,
                        frameStableElapsed = DrawableRuleset.FrameStableClock.ElapsedFrameTime,
                        frameStableIsCatchingUp = DrawableRuleset.FrameStableClock.IsCatchingUp.Value,
                        frameStableWaitingOnFrames = DrawableRuleset.FrameStableClock.WaitingOnFrames.Value,
                        frameStablePlaybackEnabled = DrawableRuleset.FrameStablePlayback,
                        replayStartFrameTime = getPropertyValue(replayStartFrame, "Time"),
                        replayCurrentFrameTime = getPropertyValue(replayCurrentFrame, "Time"),
                        replayEndFrameTime = getPropertyValue(replayEndFrame, "Time"),
                        inputManagerType = inputManager?.GetType().FullName,
                        replayHandlerType = replayHandler?.GetType().FullName,
                        osuInputMousePosition = getNestedPropertyValue(inputManager, "CurrentState", "Mouse", "Position"),
                        originalUserCursorPosition = getPropertyValue(inputManager, "OriginalUserCursorPosition"),
                        virtualCursorPosition = getPropertyValue(inputManager, "VirtualCursorPosition"),
                        replayCursorPosition = getPropertyValue(inputManager, "ReplayCursorPosition"),
                        hasReplayCursorPosition = getPropertyValue(inputManager, "HasReplayCursorPosition"),
                        replayBotActive = getPropertyValue(inputManager, "ReplayBotActive"),
                        allowUserCursorMovement = getPropertyValue(inputManager, "AllowUserCursorMovement"),
                        useParentInput = getPropertyValue(inputManager, "UseParentInput"),
                        gameplayCursorType = cursor?.GetType().FullName,
                        gameplayActiveCursorType = activeCursor?.GetType().FullName,
                        gameplayActiveCursorPosition = getPropertyValue(activeCursor, "Position"),
                        gameplayActiveCursorScreenSpaceCentre = getNestedPropertyValue(activeCursor, "ScreenSpaceDrawQuad", "Centre"),
                        gameplayCursorScreenSpaceCentre = getNestedPropertyValue(cursor, "ScreenSpaceDrawQuad", "Centre"),
                        gameplayCursorAlpha = getPropertyValue(cursor, "Alpha"),
                        gameplayCursorVisibleState = getPropertyValue(cursor, "LastFrameState")?.ToString(),
                        replayOverlayAlpha = ReplayOverlay.Alpha,
                        replaySettingsExpanded = ReplayOverlay.Settings.Expanded.Value,
                        hudVisible = HUDOverlay.ShowHud.Value,
                        currentChildScreenType = this.GetChildScreen()?.GetType().FullName,
                        playerAlpha = Alpha,
                        gameplayClockContainerAlpha = GameplayClockContainer.Alpha,
                        drawableRulesetType = DrawableRuleset.GetType().FullName,
                    });
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(new
                    {
                        type = "frame-error",
                        frame,
                        targetTime,
                        error = ex.ToString(),
                    });
                }
            });

            using (token.Register(() => tcs.TrySetCanceled()))
                return tcs.Task;
        }

        private void logMemoryUsage(int frame, string phase)
        {
            long managedBytes = GC.GetTotalMemory(false);
            peakManagedBytes = Math.Max(peakManagedBytes, managedBytes);

            try
            {
                using Process process = Process.GetCurrentProcess();
                peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, process.WorkingSet64);
                peakPrivateBytes = Math.Max(peakPrivateBytes, process.PrivateMemorySize64);

                Logger.Log($"[RenderReplayPlayer] Memory ({phase}, frame={frame}/{TotalFrames}): managed={formatBytes(managedBytes)}, workingSet={formatBytes(process.WorkingSet64)}, private={formatBytes(process.PrivateMemorySize64)}, gc0={GC.CollectionCount(0)}, gc1={GC.CollectionCount(1)}, gc2={GC.CollectionCount(2)}",
                    LoggingTarget.Runtime,
                    LogLevel.Verbose);
            }
            catch (Exception ex)
            {
                Logger.Log($"[RenderReplayPlayer] Failed to query process memory usage during {phase}: {ex.Message}",
                    LoggingTarget.Runtime,
                    LogLevel.Verbose);
            }
        }

        private static string formatBytes(long bytes)
            => $"{bytes / 1024d / 1024d:0.##} MiB";

        private static object? getPropertyValue(object? instance, string propertyName)
        {
            if (instance == null)
                return null;

            for (Type? type = instance.GetType(); type != null; type = type.BaseType)
            {
                PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (property != null)
                    return property.GetValue(instance);
            }

            return null;
        }

        private static object? getNestedPropertyValue(object? instance, params string[] propertyNames)
        {
            object? current = instance;

            foreach (string propertyName in propertyNames)
            {
                current = getPropertyValue(current, propertyName);

                if (current == null)
                    return null;
            }

            return current;
        }
    }
}
