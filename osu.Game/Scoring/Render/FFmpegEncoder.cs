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
    /// callers can pipe raw RGBA frames into it.
    /// </summary>
    public class FFmpegEncoder : IDisposable
    {
        private static readonly string local_ffmpeg_path = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");

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
            if (string.IsNullOrEmpty(encoder) || !hardware_encoders.Contains(encoder))
                return true;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    Arguments = $"-hide_banner -f lavfi -i nullsrc=s=2x2:d=0.1 -c:v {encoder} -f null -",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return false;

                string stderr = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(10_000))
                {
                    try { process.Kill(); } catch { }
                    return false;
                }

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

        /// <summary>
        /// Ensures FFmpeg is available at the local path. Downloads it if missing.
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
            await downloadFFmpeg(local_ffmpeg_path, onProgress, token).ConfigureAwait(false);
        }

        private readonly Process ffmpegProcess;
        public string? LastErrorLine { get; private set; }

        public Stream InputStream => ffmpegProcess.StandardInput.BaseStream;

        public FFmpegEncoder(int inputWidth, int inputHeight, int outputWidth, int outputHeight, int fps, int bitrateMbps, string encoder, ReplayRenderQualityPreset qualityPreset, string outputPath, bool flipVertical = false,
                             string? audioPath = null, double audioOffsetMs = 0, double audioDelayMs = 0, double audioFrequencyRate = 1, double audioTempoRate = 1,
                             double? durationSeconds = null, double? outputDurationSeconds = null, int? videoFrameCount = null, double audioVolume = 1)
        {
            if (hardware_encoders.Contains(encoder) && !tryValidateEncoder(encoder))
            {
                Framework.Logging.Logger.Log(
                    $"[FFmpeg] Hardware encoder \"{encoder}\" is unavailable on this system. Falling back to libx264 (CPU).",
                    Framework.Logging.LoggingTarget.Runtime,
                    Framework.Logging.LogLevel.Important);
                encoder = "libx264";
            }

            bool isVaapi = encoder.Contains("vaapi", StringComparison.OrdinalIgnoreCase);

            string hwAccelArgs = isVaapi
                ? "-vaapi_device /dev/dri/renderD128"
                : string.Empty;

            string inputArgs = $"-probesize 32 -analyzeduration 0 -f rawvideo -pix_fmt rgba -s {inputWidth}x{inputHeight} -r {fps} -i pipe:0";
            string audioArgs = buildAudioInputArgs(audioPath);
            int inputSampleRate = getAudioSampleRate(audioPath);
            string audioFilterArgs = buildAudioFilterArgs(audioArgs, audioOffsetMs, audioDelayMs, audioFrequencyRate, audioTempoRate, audioVolume, inputSampleRate, durationSeconds);
            string vfArgs = buildFilterArgs(isVaapi, flipVertical, inputWidth, inputHeight, outputWidth, outputHeight);
            string encoderTuningArgs = buildEncoderTuningArgs(encoder, qualityPreset, fps);
            string mapArgs = string.IsNullOrWhiteSpace(audioArgs) ? string.Empty : "-map 0:v:0 -map 1:a:0";
            string audioCodecArgs = string.IsNullOrWhiteSpace(audioArgs) ? string.Empty : "-c:a pcm_s16le";
            string outputLimitArgs = buildOutputLimitArgs(outputDurationSeconds, videoFrameCount);
            string pixFmtArg = isVaapi ? string.Empty : "-pix_fmt yuv420p";
            string containerArgs = outputPath.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ? string.Empty : "-movflags +faststart+frag_keyframe+empty_moov";
            string outputArgs = $"-c:v {encoder} {encoderTuningArgs} -bf 0 -g {fps} -b:v {bitrateMbps}M {pixFmtArg} {audioFilterArgs} {audioCodecArgs} {containerArgs} {mapArgs} {outputLimitArgs} -y \"{outputPath}\"";

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

            ffmpegProcess.BeginErrorReadLine();
            ffmpegProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    LastErrorLine = e.Data;
                    Framework.Logging.Logger.Log($"[FFmpeg] {e.Data}", Framework.Logging.LoggingTarget.Runtime, Framework.Logging.LogLevel.Verbose);
                }
            };
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

        private static string buildEncoderTuningArgs(string encoder, ReplayRenderQualityPreset qualityPreset, int fps)
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
                "h264_nvenc" => qualityPreset switch
                {
                    ReplayRenderQualityPreset.Fast => "-preset p1 -tune ll -delay 0 -rc cbr",
                    ReplayRenderQualityPreset.Balanced => "-preset p4 -tune ll -delay 0 -rc cbr",
                    ReplayRenderQualityPreset.Quality => "-preset p6 -tune ll -delay 0 -rc cbr",
                    _ => "-preset p4 -tune ll -delay 0 -rc cbr",
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

            if (File.Exists(local_ffmpeg_path))
            {
                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        var mode = File.GetUnixFileMode(local_ffmpeg_path);
                        if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
                            return null;
                    }
                    catch
                    {
                        return null;
                    }
                }

                return local_ffmpeg_path;
            }

            return null;
        }

        private static async Task downloadFFmpeg(string targetPath, Action<int, long, long>? onProgress, CancellationToken token)
        {
            bool isWindows = OperatingSystem.IsWindows();
            string url;
            if (isWindows)
            {
                url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
            }
            else
            {
                var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
                string archStr = arch == System.Runtime.InteropServices.Architecture.Arm64 ? "linuxarm64" : "linux64";
                url = $"https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-{archStr}-gpl.tar.xz";
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

                // Set executable permissions
                File.SetUnixFileMode(targetPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
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
