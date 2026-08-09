// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Scoring.Render
{
    /// <summary>
    /// Captures frames from the current game window at a fixed rate and streams them
    /// to an FFmpeg child process for encoding. Works on both Windows (DirectX) and
    /// Linux (OpenGL) because it relies on <see cref="GameHost.TakeScreenshotAsync"/>,
    /// which uses the correct platform-specific readback internally.
    /// </summary>
    public partial class ReplayRenderer : Component
    {
        private readonly ScoreInfo score;
        private readonly int fps;
        private readonly int bitrate;
        private readonly string resolution;
        private readonly string encoder;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        /// <summary>
        /// Fires when a frame is captured, providing (currentFrame, totalFrames).
        /// </summary>
        public Action<int, int>? OnProgress;

        /// <summary>
        /// Fires when rendering finishes, providing the output file path.
        /// </summary>
        public Action<string>? OnComplete;

        /// <summary>
        /// Fires when an error occurs.
        /// </summary>
        public Action<Exception>? OnError;

        private CancellationTokenSource? cancellation;

        public ReplayRenderer(ScoreInfo score, string resolution, int fps, int bitrate, string encoder)
        {
            this.score = score;
            this.resolution = resolution;
            this.fps = fps;
            this.bitrate = bitrate;
            this.encoder = encoder;
        }

        /// <summary>
        /// Begin capturing the current game window at <see cref="fps"/> and encoding
        /// to an MP4 file with FFmpeg. The call returns immediately; encoding runs on
        /// a background thread. Hook <see cref="OnProgress"/>/<see cref="OnComplete"/>
        /// to track state.
        /// </summary>
        /// <param name="totalFrames">How many frames to capture. Defaults to 0 meaning no limit (stop via <see cref="Stop"/>).</param>
        public Task StartRenderAsync(int totalFrames = 0)
        {
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;

            return Task.Run(async () =>
            {
                int width = int.Parse(resolution.Split('x')[0]);
                int height = int.Parse(resolution.Split('x')[1]);

                string renderDir = storage.GetStorageForDirectory("renders").GetFullPath(".");
                Directory.CreateDirectory(renderDir);
                string rawTitle = score.BeatmapInfo?.Metadata?.Title ?? "unknown";
                string sanitizedTitle = string.Join("_", rawTitle.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                string outputPath = Path.Combine(renderDir, $"render_{sanitizedTitle}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4");

                // On Linux, OpenGL returns pixels upside-down (bottom-up row order).
                // On Windows, DirectX returns them top-down.
                // FFmpegEncoder adds -vf vflip automatically for Linux.
                bool isLinux = OperatingSystem.IsLinux();
                var ffmpeg = new FFmpegEncoder(width, height, width, height, fps, bitrate, encoder, ReplayRenderQualityPreset.Balanced, outputPath, flipVertical: isLinux);

                Logger.Log($"[ReplayRenderer] Starting render → {outputPath}", LoggingTarget.Runtime);

                int frame = 0;
                double msPerFrame = 1000.0 / fps;

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (totalFrames > 0 && frame >= totalFrames)
                            break;

                        // Wait for the draw thread to produce a new frame, then read it back.
                        using var image = await captureFrameAsync(token).ConfigureAwait(false);

                        if (image == null)
                            break;

                        // Write raw RGBA pixels directly to FFmpeg stdin.
                        await writeImageToStreamAsync(image, ffmpeg.InputStream, token).ConfigureAwait(false);

                        frame++;
                        OnProgress?.Invoke(frame, totalFrames);

                        Logger.Log($"[ReplayRenderer] Frame {frame} encoded.", LoggingTarget.Runtime, LogLevel.Debug);
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.Log("[ReplayRenderer] Render cancelled.", LoggingTarget.Runtime);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[ReplayRenderer] Render error.");
                    OnError?.Invoke(ex);
                }
                finally
                {
                    ffmpeg.Stop();
                    Logger.Log($"[ReplayRenderer] Render complete. {frame} frames written to {outputPath}", LoggingTarget.Runtime);
                    OnComplete?.Invoke(outputPath);
                }
            }, token);
        }

        /// <summary>
        /// Stop encoding after the current frame is written.
        /// </summary>
        public void Stop() => cancellation?.Cancel();

        // ─── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Schedule a screenshot readback on the draw thread, then await it.
        /// Works with both OpenGL (Linux) and DirectX (Windows) via the framework's
        /// platform-agnostic <see cref="GameHost.TakeScreenshotAsync"/>.
        /// </summary>
        private async Task<Image<Rgba32>?> captureFrameAsync(CancellationToken token)
        {
            // We need to sync to the draw thread so TakeScreenshotAsync captures the
            // frame that corresponds to the current game-clock position.
            var tcs = new TaskCompletionSource<Image<Rgba32>?>(TaskCreationOptions.RunContinuationsAsynchronously);

            host.DrawThread.Scheduler.Add(() =>
            {
                host.TakeScreenshotAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        tcs.TrySetException(t.Exception!);
                    else
                        tcs.TrySetResult(t.GetResultSafely());
                }, TaskScheduler.Default);
            });

            using (token.Register(() => tcs.TrySetCanceled()))
                return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Copies all pixel data from an <see cref="Image{Rgba32}"/> into <paramref name="stream"/>
        /// as a contiguous array of raw RGBA bytes, suitable for piping into FFmpeg.
        /// </summary>
        private static async Task writeImageToStreamAsync(Image<Rgba32> image, Stream stream, CancellationToken token)
        {
            // ImageSharp stores rows contiguously; we copy them row-by-row into a
            // single buffer so we can do one big write rather than N small ones.
            int width = image.Width;
            int height = image.Height;
            byte[] buffer = new byte[width * height * 4];

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var dst = MemoryMarshal.AsBytes(row);
                    dst.CopyTo(buffer.AsSpan(y * width * 4));
                }
            });

            await stream.WriteAsync(buffer, token).ConfigureAwait(false);
        }
    }
}
