// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Net.Http;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;
using osu.Game.Configuration;

namespace osu.Game.Scoring.Render
{
    /// <summary>
    /// Wraps an FFmpeg child process and exposes its stdin as a <see cref="Stream"/> so
    /// callers can pipe raw RGBA, BGRA or NV12 frames into it.
    /// </summary>
    public class FFmpegEncoder : IDisposable
    {
        private static readonly string local_ffmpeg_path = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        // AppImage mounts are read-only. Keep downloaded binaries in the user's writable data directory.
        private static readonly string cached_ffmpeg_path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Morasooma", "ffmpeg");

        internal static string ExecutablePath => resolveExecutablePath();

        private static readonly HashSet<string> hardware_encoders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "h264_nvenc", "h264_amf", "h264_qsv", "h264_vaapi", "hevc_vaapi",
        };

        /// <summary>
        /// Runs a minimal FFmpeg test encode to verify that the given encoder can be initialised
        /// with the current driver / hardware environment. Returns false if the encoder fails to
        /// open (e.g. outdated NVIDIA driver for nvenc, missing VAAPI device, etc.).
        /// </summary>
        private static bool tryValidateEncoder(string encoder)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    Arguments = BuildEncoderValidationArguments(encoder),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return false;

                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(10_000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return false;
                }

                string stderr = stderrTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                {
                    Framework.Logging.Logger.Log(
                        $"[FFmpeg] Encoder \"{encoder}\" failed validation (exit {process.ExitCode}). " +
                        $"FFmpeg stderr: {stderr.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault()}",
                        Framework.Logging.LoggingTarget.Runtime,
                        Framework.Logging.LogLevel.Important);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Framework.Logging.Logger.Log(
                    $"[FFmpeg] Encoder \"{encoder}\" validation threw: {ex.Message}",
                    Framework.Logging.LoggingTarget.Runtime,
                    Framework.Logging.LogLevel.Important);
                return false;
            }
        }

        internal static string BuildEncoderValidationArguments(string encoder)
        {
            // A 2x2 frame is below the supported dimensions of several Windows hardware encoders.
            // VAAPI additionally requires hardware surfaces, just like the real export pipeline.
            bool isVaapi = encoder.Contains("vaapi", StringComparison.OrdinalIgnoreCase);
            string deviceArgs = isVaapi ? "-vaapi_device /dev/dri/renderD128 " : string.Empty;
            string formatArgs = isVaapi ? "-vf format=nv12,hwupload" : "-pix_fmt yuv420p";
            return $"-hide_banner -nostdin {deviceArgs}-f lavfi -i color=s=256x256:r=30 -frames:v 1 {formatArgs} -c:v {encoder} -f null -";
        }

        /// <summary>
        /// Ensures FFmpeg is available. Downloads it to a writable cache if missing.
        /// Call this before constructing <see cref="FFmpegEncoder"/> to avoid blocking.
        /// </summary>
        /// <param name="onProgress">Callback receiving (percent, downloadedMB, totalMB). Called periodically during download.</param>
        /// <param name="token">Cancellation token.</param>
        public static async Task EnsureDownloadedAsync(Action<int, long, long>? onProgress = null, CancellationToken token = default)
        {
            string? resolved = tryResolveExecutablePath();
            if (resolved != null)
                return;

            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
                throw new FileNotFoundException("FFmpeg was not found. Install it manually or set FFMPEG_PATH.");

            Framework.Logging.Logger.Log("[FFmpeg] FFmpeg was not found. Downloading stable release...", Framework.Logging.LoggingTarget.Runtime, Framework.Logging.LogLevel.Important);
            string targetPath = OperatingSystem.IsLinux() ? cached_ffmpeg_path : local_ffmpeg_path;
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await downloadFFmpeg(targetPath, onProgress, token).ConfigureAwait(false);
        }

        private readonly Process ffmpegProcess;
        public string? LastErrorLine { get; private set; }

        public Stream InputStream => ffmpegProcess.StandardInput.BaseStream;
        internal string SelectedEncoder { get; }

        internal static string BuildRawVideoInputArguments(int width, int height, int fps, bool inputIsBgra, bool inputIsNv12)
        {
            if (inputIsNv12 && inputIsBgra)
                throw new ArgumentException("NV12 and BGRA input formats are mutually exclusive.");
            if (inputIsNv12 && ((width & 1) != 0 || (height & 1) != 0))
                throw new ArgumentException("NV12 input requires even dimensions.");
            string format = inputIsNv12 ? "nv12" : inputIsBgra ? "bgra" : "rgba";
            string colourArgs = inputIsNv12 ? "-colorspace bt709 -color_range tv -color_primaries bt709 -color_trc bt709" : string.Empty;
            return $"-probesize 32 -analyzeduration 0 -f rawvideo -pix_fmt {format} {colourArgs} -s {width}x{height} -r {fps} -i pipe:0";
        }

        public FFmpegEncoder(int inputWidth, int inputHeight, int outputWidth, int outputHeight, int fps, int bitrateMbps, string encoder, ReplayRenderQualityPreset qualityPreset, string outputPath, bool flipVertical = false,
                             string? audioPath = null, double audioOffsetMs = 0, double audioDelayMs = 0, double audioFrequencyRate = 1, double audioTempoRate = 1,
                             double? durationSeconds = null, double? outputDurationSeconds = null, int? videoFrameCount = null, double audioVolume = 1, bool inputIsBgra = false, bool inputIsNv12 = false)
        {
            if (encoder == RenderEncodingSelector.CompactEncoder && !tryValidateEncoder(encoder))
                throw new InvalidOperationException("HEVC export requires an FFmpeg build with libx265 support. Install one or set FFMPEG_PATH to it.");

            if (hardware_encoders.Contains(encoder) && !tryValidateEncoder(encoder))
            {
                Framework.Logging.Logger.Log(
                    $"[FFmpeg] Hardware encoder \"{encoder}\" is unavailable on this system. Falling back to libx264 (CPU).",
                    Framework.Logging.LoggingTarget.Runtime,
                    Framework.Logging.LogLevel.Important);
                encoder = "libx264";
            }

            SelectedEncoder = encoder;
            bool isVaapi = encoder.Contains("vaapi", StringComparison.OrdinalIgnoreCase);

            string hwAccelArgs = isVaapi
                ? "-vaapi_device /dev/dri/renderD128"
                : string.Empty;

            string inputFormat = inputIsNv12 ? "nv12" : inputIsBgra ? "bgra" : "rgba";
            string colourArgs = inputIsNv12 ? "-colorspace bt709 -color_range tv -color_primaries bt709 -color_trc bt709" : string.Empty;
            string inputArgs = BuildRawVideoInputArguments(inputWidth, inputHeight, fps, inputIsBgra, inputIsNv12);
            string audioArgs = buildAudioInputArgs(audioPath);
            int inputSampleRate = getAudioSampleRate(audioPath);
            string audioFilterArgs = buildAudioFilterArgs(audioArgs, audioOffsetMs, audioDelayMs, audioFrequencyRate, audioTempoRate, audioVolume, inputSampleRate, durationSeconds);
            string vfArgs = buildFilterArgs(isVaapi, flipVertical, inputWidth, inputHeight, outputWidth, outputHeight);
            string encoderTuningArgs = BuildEncoderTuningArgs(encoder, qualityPreset);
            string mapArgs = string.IsNullOrWhiteSpace(audioArgs) ? string.Empty : "-map 0:v:0 -map 1:a:0";
            string audioCodecArgs = string.IsNullOrWhiteSpace(audioArgs) ? string.Empty : "-c:a pcm_s16le";
            string outputLimitArgs = buildOutputLimitArgs(outputDurationSeconds, videoFrameCount);
            string pixFmtArg = isVaapi ? string.Empty : "-pix_fmt yuv420p";
            string containerArgs = outputPath.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ? string.Empty : "-movflags +faststart+frag_keyframe+empty_moov";
            string rateControlArgs = BuildRateControlArgs(encoder, qualityPreset, bitrateMbps);
            string codecTagArgs = !outputPath.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) && encoder == RenderEncodingSelector.CompactEncoder ? "-tag:v hvc1" : string.Empty;
            string outputArgs = $"-c:v {encoder} {encoderTuningArgs} {rateControlArgs} -g {fps} {pixFmtArg} {colourArgs} {audioFilterArgs} {audioCodecArgs} {codecTagArgs} {containerArgs} {mapArgs} {outputLimitArgs} -y \"{outputPath}\"";

            string arguments = string.Join(" ", new[]
            {
                hwAccelArgs,
                inputArgs,
                audioArgs,
                vfArgs,
                outputArgs,
            }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Framework.Logging.Logger.Log($"[FFmpeg] Using executable: {startInfo.FileName}", Framework.Logging.LoggingTarget.Runtime);
            Framework.Logging.Logger.Log($"[FFmpeg] Audio pipeline: offset={formatInvariant(audioOffsetMs)} ms, delay={formatInvariant(audioDelayMs)} ms, frequency={formatInvariant(audioFrequencyRate)}, tempo={formatInvariant(audioTempoRate)}, volume={formatInvariant(audioVolume)}, duration={(durationSeconds.HasValue ? formatInvariant(durationSeconds.Value) : "auto")} s, outputDuration={(outputDurationSeconds.HasValue ? formatInvariant(outputDurationSeconds.Value) : "auto")} s, videoFrames={(videoFrameCount.HasValue ? videoFrameCount.Value.ToString() : "auto")}",
                Framework.Logging.LoggingTarget.Runtime,
                Framework.Logging.LogLevel.Verbose);

            ffmpegProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start FFmpeg.");

            try
            {
                if (OperatingSystem.IsWindows())
                    ffmpegProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
            }
            catch
            {
            }

            Framework.Logging.Logger.Log($"[FFmpeg] Encoding with {encoder}, input={inputFormat}, preset={qualityPreset}", Framework.Logging.LoggingTarget.Runtime);
            ffmpegProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    LastErrorLine = e.Data;
                    Framework.Logging.Logger.Log($"[FFmpeg] {e.Data}", Framework.Logging.LoggingTarget.Runtime, Framework.Logging.LogLevel.Verbose);
                }
            };
            ffmpegProcess.BeginErrorReadLine();
        }

        // The compact encoder reorders/buffers frames. A successful capture is not a
        // successful export until FFmpeg has flushed its queue and closed the container.
        internal async Task FinishAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await Task.Run(() => ffmpegProcess.StandardInput.Close()).WaitAsync(timeout.Token).ConfigureAwait(false);
                await ffmpegProcess.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException("FFmpeg did not finish encoding within 30 seconds of the final frame.");
            }

            if (ffmpegProcess.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg failed to finish encoding (exit {ffmpegProcess.ExitCode}). {LastErrorLine}");
        }

        public void Stop()
        {
            try
            {
                if (!ffmpegProcess.HasExited)
                {
                    // Closing StandardInput flushes the pipe buffer, which can hang forever if FFmpeg stopped reading.
                    var closeTask = System.Threading.Tasks.Task.Run(() => ffmpegProcess.StandardInput.Close());
                    closeTask.Wait(2000);
                }
            }
            catch
            {
            }

            try
            {
                if (!ffmpegProcess.WaitForExit(5000) && !ffmpegProcess.HasExited)
                    ffmpegProcess.Kill();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            try
            {
                if (!ffmpegProcess.HasExited)
                    ffmpegProcess.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            ffmpegProcess.Dispose();
        }

        private static string buildFilterArgs(bool isVaapi, bool flipVertical, int inputWidth, int inputHeight, int outputWidth, int outputHeight)
        {
            var filters = new List<string>();

            if (flipVertical)
                filters.Add("vflip");

            if (inputWidth != outputWidth || inputHeight != outputHeight)
                filters.Add($"scale={outputWidth}:{outputHeight}");

            if (isVaapi)
            {
                filters.Add("format=nv12");
                filters.Add("hwupload");
            }

            return filters.Count == 0
                ? string.Empty
                : $"-vf \"{string.Join(",", filters)}\"";
        }

        private static string buildAudioInputArgs(string? audioPath)
        {
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
                return string.Empty;

            return $"-i \"{audioPath}\"";
        }

        private static string buildAudioFilterArgs(string audioArgs, double audioOffsetMs, double audioDelayMs, double audioFrequencyRate, double audioTempoRate, double audioVolume,
                                                   int inputSampleRate, double? durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(audioArgs))
                return string.Empty;

            var filters = new List<string>();

            // Use sample-accurate atrim instead of demuxer-level -ss/-t, which can be off by tens of
            // milliseconds on compressed audio (mp3/ogg) and manifests as noticeable A/V desync.
            if (audioOffsetMs > 0.5 || durationSeconds > 0)
            {
                string start = formatInvariant(audioOffsetMs / 1000.0);

                if (durationSeconds > 0)
                    filters.Add($"atrim=start={start}:duration={formatInvariant(durationSeconds.Value)}");
                else
                    filters.Add($"atrim=start={start}");

                filters.Add("asetpts=PTS-STARTPTS");
            }

            if (audioVolume > 0 && Math.Abs(audioVolume - 1) >= 0.0001)
                filters.Add($"volume={formatInvariant(audioVolume)}");

            if (audioFrequencyRate > 0 && Math.Abs(audioFrequencyRate - 1) >= 0.0001)
            {
                int targetSampleRate = (int)Math.Round(inputSampleRate * audioFrequencyRate);
                filters.Add($"asetrate={targetSampleRate}");
                filters.Add($"aresample={inputSampleRate}");
            }

            if (audioTempoRate > 0 && Math.Abs(audioTempoRate - 1) >= 0.0001)
                filters.Add(buildAtempoFilterChain(audioTempoRate));

            // Using input timestamp offsets (`-itsoffset`) for the music track does not survive the
            // second ffmpeg pass that mixes hitsounds back into the rendered video. During that pass
            // the audio is decoded from samples and the container timestamp gap is effectively lost,
            // which makes the music begin at t=0 in the final MP4. Insert real silent samples instead.
            if (audioDelayMs > 0)
            {
                int delayMs = Math.Max(0, (int)Math.Round(audioDelayMs));
                filters.Add($"adelay={delayMs}|{delayMs}");
            }

            // NOTE: Do NOT add "apad" here. apad produces infinite audio and never sends EOF,
            // which prevents FFmpeg from finalising the output file. When FFmpeg reaches -frames:v
            // it stops reading video from stdin, but keeps processing the infinite audio stream.
            // This causes the frame writer's pipe write to block, which cascades to the capture
            // loop and hangs the entire render. The first atrim=start=X:duration=Y already limits
            // the audio to the correct duration and sends EOF when done. With -frames:v limiting
            // video and no -shortest flag, FFmpeg reads exactly the right number of video frames
            // regardless of when the audio stream ends.

            return $"-filter:a \"{string.Join(",", filters)}\"";
        }

        private static string buildOutputLimitArgs(double? outputDurationSeconds, int? videoFrameCount)
        {
            var args = new List<string>();

            if (videoFrameCount.HasValue && videoFrameCount.Value > 0)
                args.Add($"-frames:v {videoFrameCount.Value}");

            if (outputDurationSeconds.HasValue && outputDurationSeconds.Value > 0)
                args.Add($"-t {formatInvariant(outputDurationSeconds.Value)}");

            return string.Join(" ", args);
        }

        internal static string BuildRateControlArgs(string encoder, ReplayRenderQualityPreset qualityPreset, int bitrateMbps)
        {
            if (encoder != RenderEncodingSelector.CompactEncoder)
                return $"-bf 0 -b:v {bitrateMbps}M";

            // No fixed bitrate or low-latency/B-frame override in the compact offline mode.
            int crf = qualityPreset switch
            {
                ReplayRenderQualityPreset.Fast => 26,
                ReplayRenderQualityPreset.Quality => 22,
                _ => 24,
            };
            return $"-crf {crf}";
        }

        internal static string BuildEncoderTuningArgs(string encoder, ReplayRenderQualityPreset qualityPreset)
        {
            return encoder switch
            {
                "libx264" => qualityPreset switch
                {
                    ReplayRenderQualityPreset.Fast => $"-preset ultrafast -tune zerolatency -threads 0",
                    ReplayRenderQualityPreset.Balanced => $"-preset veryfast -tune zerolatency -threads 0",
                    ReplayRenderQualityPreset.Quality => $"-preset fast -tune zerolatency -threads 0",
                    _ => $"-preset veryfast -tune zerolatency -threads 0",
                },
                RenderEncodingSelector.CompactEncoder => qualityPreset switch
                {
                    ReplayRenderQualityPreset.Fast => "-preset veryfast -x265-params pools=4:frame-threads=2",
                    _ => "-preset fast -x265-params pools=4:frame-threads=2",
                },
                // Offline export needs throughput, not immediate packet delivery. Let NVENC
                // keep its normal surface queue instead of forcing a synchronous -delay 0.
                // Preserve the selected preset, rate control and visual-quality policy.
                "h264_nvenc" => qualityPreset switch
                {
                    ReplayRenderQualityPreset.Fast => "-preset p1 -tune ll -rc cbr",
                    ReplayRenderQualityPreset.Balanced => "-preset p4 -tune ll -rc cbr",
                    ReplayRenderQualityPreset.Quality => "-preset p6 -tune ll -rc cbr",
                    _ => "-preset p4 -tune ll -rc cbr",
                },
                "h264_amf" => qualityPreset switch
                {
                    ReplayRenderQualityPreset.Fast => "-usage transcoding -rc cbr -quality speed",
                    ReplayRenderQualityPreset.Balanced => "-usage transcoding -rc cbr -quality balanced",
                    ReplayRenderQualityPreset.Quality => "-usage transcoding -rc cbr -quality quality",
                    _ => "-usage transcoding -rc cbr -quality balanced",
                },
                "h264_qsv" => qualityPreset switch
                {
                    ReplayRenderQualityPreset.Fast => "-preset faster -look_ahead 0",
                    ReplayRenderQualityPreset.Balanced => "-preset medium -look_ahead 0",
                    ReplayRenderQualityPreset.Quality => "-preset slower -look_ahead 0",
                    _ => "-preset medium -look_ahead 0",
                },
                _ when encoder.Contains("vaapi", StringComparison.OrdinalIgnoreCase) => qualityPreset switch
                {
                    ReplayRenderQualityPreset.Fast => "-compression_level 4",
                    ReplayRenderQualityPreset.Balanced => "-compression_level 2",
                    ReplayRenderQualityPreset.Quality => "-compression_level 1",
                    _ => "-compression_level 2",
                },
                _ => string.Empty,
            };
        }

        private static string buildAtempoFilterChain(double playbackRate)
        {
            var filters = new List<string>();
            double remainingRate = playbackRate;

            while (remainingRate > 2.0)
            {
                filters.Add("atempo=2.0");
                remainingRate /= 2.0;
            }

            while (remainingRate < 0.5)
            {
                filters.Add("atempo=0.5");
                remainingRate /= 0.5;
            }

            filters.Add($"atempo={formatInvariant(remainingRate)}");
            return string.Join(",", filters);
        }

        private static string formatInvariant(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static string resolveExecutablePath()
        {
            string? resolved = tryResolveExecutablePath();

            if (resolved != null)
                return resolved;

            throw new FileNotFoundException(
                OperatingSystem.IsWindows()
                    ? "FFmpeg was not found. Call EnsureDownloadedAsync() before constructing FFmpegEncoder."
                    : "FFmpeg was not found. Install it manually or set FFMPEG_PATH.");
        }

        private static string? tryResolveExecutablePath()
        {
            string? envPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");

            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
                return envPath;

            if (isExecutable(local_ffmpeg_path))
                return local_ffmpeg_path;

            if (OperatingSystem.IsLinux())
            {
                if (isExecutable(cached_ffmpeg_path))
                    return cached_ffmpeg_path;

                // Prefer the distro's FFmpeg over downloading a second copy (also works in AppImages).
                foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(directory))
                        continue;

                    string candidate = Path.Combine(directory, "ffmpeg");
                    if (isExecutable(candidate))
                        return candidate;
                }
            }

            return null;
        }

        private static bool isExecutable(string path)
        {
            if (!File.Exists(path))
                return false;

            if (OperatingSystem.IsWindows())
                return true;

            try
            {
                var mode = File.GetUnixFileMode(path);
                return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task downloadFFmpeg(string targetPath, Action<int, long, long>? onProgress, CancellationToken token)
        {
            bool isWindows = OperatingSystem.IsWindows();
            string url;
            if (isWindows)
            {
                url = "https://morasooma.net/client/updates/ffmpeg/ffmpeg-release-essentials.zip";
            }
            else
            {
                var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
                string archStr = arch == System.Runtime.InteropServices.Architecture.Arm64 ? "linuxarm64" : "linux64";
                url = $"https://morasooma.net/client/updates/ffmpeg/ffmpeg-master-latest-{archStr}-gpl.tar.xz";
            }

            string extension = isWindows ? "zip" : "tar.xz";
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"ffmpeg_{Guid.NewGuid():N}.{extension}");

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; mosu/1.0)");

                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;

                    using (var contentStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                    using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalReadBytes = 0;
                        int readBytes;
                        int lastReportedPercent = -1;
                        long lastReportedMegabytes = -1;

                        while ((readBytes = await contentStream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, readBytes), token).ConfigureAwait(false);
                            totalReadBytes += readBytes;

                            if (totalBytes.HasValue && totalBytes.Value > 0)
                            {
                                int percent = (int)((double)totalReadBytes / totalBytes.Value * 100);
                                if (percent != lastReportedPercent)
                                {
                                    long downloadedMB = totalReadBytes / 1024 / 1024;
                                    long totalMB = totalBytes.Value / 1024 / 1024;
                                    Framework.Logging.Logger.Log($"[FFmpeg] Downloading: {percent}% ({downloadedMB} MB / {totalMB} MB)", Framework.Logging.LoggingTarget.Runtime, Framework.Logging.LogLevel.Important);
                                    onProgress?.Invoke(percent, downloadedMB, totalMB);
                                    lastReportedPercent = percent;
                                }
                            }
                            else
                            {
                                long downloadedMB = totalReadBytes / 1024 / 1024;

                                if (downloadedMB != lastReportedMegabytes)
                                {
                                    Framework.Logging.Logger.Log($"[FFmpeg] Downloading: {downloadedMB} MB", Framework.Logging.LoggingTarget.Runtime, Framework.Logging.LogLevel.Important);
                                    onProgress?.Invoke(0, downloadedMB, 0);
                                    lastReportedMegabytes = downloadedMB;
                                }
                            }
                        }
                    }
                }

                Framework.Logging.Logger.Log("[FFmpeg] Download complete. Extracting executable...", Framework.Logging.LoggingTarget.Runtime, Framework.Logging.LogLevel.Important);
                onProgress?.Invoke(-1, 0, 0);

                if (isWindows)
                {
                    using (var archive = ZipFile.OpenRead(tempFilePath))
                    {
                        var ffmpegEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                        if (ffmpegEntry == null)
                            throw new FileNotFoundException("Could not find ffmpeg.exe inside the downloaded archive.");

                        ffmpegEntry.ExtractToFile(targetPath, overwrite: true);
                    }
                }
                else
                {
                    await extractTarXzAsync(tempFilePath, targetPath, token).ConfigureAwait(false);
                }

                Framework.Logging.Logger.Log($"[FFmpeg] Successfully downloaded and extracted FFmpeg to {targetPath}", Framework.Logging.LoggingTarget.Runtime, Framework.Logging.LogLevel.Important);
            }
            catch (OperationCanceledException)
            {
                Framework.Logging.Logger.Log("[FFmpeg] Download cancelled.", Framework.Logging.LoggingTarget.Runtime);
                throw;
            }
            catch (Exception ex)
            {
                Framework.Logging.Logger.Error(ex, "[FFmpeg] Failed to download or extract FFmpeg.");
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFilePath))
                        File.Delete(tempFilePath);
                }
                catch
                {
                }
            }
        }

        private static async Task extractTarXzAsync(string archivePath, string targetPath, CancellationToken token)
        {
            string tempExtractDir = Path.Combine(Path.GetTempPath(), $"ffmpeg_extract_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempExtractDir);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "tar",
                    Arguments = $"-xf \"{archivePath}\" -C \"{tempExtractDir}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        throw new InvalidOperationException("Failed to start tar process.");

                    await process.WaitForExitAsync(token).ConfigureAwait(false);

                    if (process.ExitCode != 0)
                        throw new InvalidOperationException($"tar process exited with code {process.ExitCode}");
                }

                // Recursively find the ffmpeg executable in the extracted folder
                string? ffmpegSource = Directory.EnumerateFiles(tempExtractDir, "ffmpeg", SearchOption.AllDirectories)
                                                .FirstOrDefault();

                if (ffmpegSource == null)
                    throw new FileNotFoundException("Could not find ffmpeg binary inside the extracted archive.");

                if (File.Exists(targetPath))
                    File.Delete(targetPath);

                File.Copy(ffmpegSource, targetPath, overwrite: true);

                // Set executable permissions on Unix platforms.
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(targetPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempExtractDir))
                        Directory.Delete(tempExtractDir, recursive: true);
                }
                catch
                {
                }
            }
        }

        private static int getAudioSampleRate(string? audioPath)
        {
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
                return 44100;

            try
            {
                int decodeStream = Bass.CreateStream(audioPath, 0, 0, BassFlags.Decode);
                if (decodeStream != 0)
                {
                    var info = Bass.ChannelGetInfo(decodeStream);
                    Bass.StreamFree(decodeStream);
                    if (info.Frequency > 0)
                        return info.Frequency;
                }
            }
            catch (Exception ex)
            {
                Framework.Logging.Logger.Log($"[FFmpeg] Failed to query audio sample rate: {ex.Message}. Falling back to 44100.", Framework.Logging.LoggingTarget.Runtime, Framework.Logging.LogLevel.Important);
            }

            return 44100;
        }
    }
}
