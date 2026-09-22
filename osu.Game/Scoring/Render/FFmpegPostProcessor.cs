// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;

namespace osu.Game.Scoring.Render
{
    internal static class FFmpegPostProcessor
    {
        /// <summary>
        /// Each source is pre-scaled by this factor and summed by amix with normalize=0.
        /// This keeps a constant per-source level for the whole video. amix's default
        /// normalization renormalizes the remaining input (doubling it) once the hitsound
        /// stream ends mid-video (last trigger + tail), making the music gradually louder
        /// toward the end of the video.
        /// </summary>
        private const double amix_input_scale = 0.5;

        public static void MixHitsoundsIntoVideo(string inputVideoPath, string hitsoundPath, string outputPath, bool videoAlreadyHasAudio, double hitsoundGain = 1,
                                                 double trimLeadingMs = 0, double placeAtMs = 0, double? playDurationMs = null,
                                                 double musicVolume = 0.85)
        {
            if (!File.Exists(inputVideoPath))
                throw new FileNotFoundException("Rendered video for post-processing was not found.", inputVideoPath);

            if (!File.Exists(hitsoundPath))
                throw new FileNotFoundException("Captured hitsound audio was not found.", hitsoundPath);

            string tempOutputPath = $"{outputPath}.muxing.mp4";
            string gainFilter = buildHitsoundFilter(hitsoundGain, trimLeadingMs, placeAtMs, playDurationMs);
            // Music volume is scaled by the player's client music volume setting (VolumeTrack).
            // Hitsound gain incorporates the player's effect volume setting (VolumeSample).
            string musicFilterArg = Math.Abs(musicVolume - 1) > 0.001 ? $"volume={formatInvariant(musicVolume)}" : "anull";
            // Both inputs are explicitly halved above/in the hitsound chain (matching the previous
            // amix behaviour while both sources are active), then summed without renormalisation.
            // A naive normalize=0 without the pre-scaling used to clip (full + full); with it, the
            // headroom is identical to the old normalized mix.
            string filterAndMapArgs = videoAlreadyHasAudio
                ? $"-filter_complex \"{gainFilter};[0:a:0]{musicFilterArg},volume={formatInvariant(amix_input_scale)}[m];[m][hs]amix=inputs=2:duration=first:normalize=0[a]\" -map 0:v:0 -map \"[a]\""
                : $"-filter_complex \"{gainFilter};[hs]anull[a]\" -map 0:v:0 -map \"[a]\"";

            string arguments = $"-i \"{inputVideoPath}\" -i \"{hitsoundPath}\" {filterAndMapArgs} -c:v copy -c:a aac -b:a 192k -movflags +faststart -y \"{tempOutputPath}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = FFmpegEncoder.ExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Framework.Logging.Logger.Log($"[FFmpeg] Post-processing hitsounds into final video: {inputVideoPath} + {hitsoundPath} -> {outputPath} (gain={formatInvariant(hitsoundGain)}, musicVolume={formatInvariant(musicVolume)}, trimLeading={formatInvariant(trimLeadingMs)} ms, placeAt={formatInvariant(placeAtMs)} ms, duration={(playDurationMs.HasValue ? formatInvariant(playDurationMs.Value) : "auto")} ms)",
                Framework.Logging.LoggingTarget.Runtime,
                Framework.Logging.LogLevel.Verbose);

            string? lastErrorLine = null;

            using var process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException("Failed to start FFmpeg post-processing step.");

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    lastErrorLine = e.Data;
            };
            process.BeginErrorReadLine();
            if (!process.WaitForExit(120000))
            {
                process.Kill(true);
                throw new InvalidOperationException("FFmpeg post-processing timed out after 120 seconds.");
            }

            if (process.ExitCode != 0)
                throw new InvalidOperationException(lastErrorLine ?? $"FFmpeg post-processing exited with code {process.ExitCode}.");

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            File.Move(tempOutputPath, outputPath);
        }

        /// <summary>
        /// Re-containers the intermediate render (Matroska with PCM audio) into a real MP4 with AAC audio.
        /// A bare file rename would leave a mislabelled MKV that most players refuse to play audio from.
        /// </summary>
        /// <param name="inputVideoPath">Path to intermediate input video file.</param>
        /// <param name="outputPath">Destination path for the remuxed MP4.</param>
        /// <param name="hasAudio">Whether the intermediate is expected to contain an audio stream.</param>
        /// <param name="musicVolume">Constant gain applied to the audio during the remux.</param>
        public static void RemuxToMp4(string inputVideoPath, string outputPath, bool hasAudio, double musicVolume = 1)
        {
            if (!File.Exists(inputVideoPath))
                throw new FileNotFoundException("Rendered video for remuxing was not found.", inputVideoPath);

            string tempOutputPath = $"{outputPath}.remux.mp4";
            string audioArgs = hasAudio
                ? $"-map 0:a:0 -c:a aac -b:a 192k{(Math.Abs(musicVolume - 1) > 0.001 ? $" -af \"volume={formatInvariant(musicVolume)}\"" : string.Empty)}"
                : "-an";
            string arguments = $"-i \"{inputVideoPath}\" -map 0:v:0 {audioArgs} -c:v copy -movflags +faststart -y \"{tempOutputPath}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = FFmpegEncoder.ExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Framework.Logging.Logger.Log($"[FFmpeg] Remuxing intermediate into final MP4: {inputVideoPath} -> {outputPath} (hasAudio={hasAudio}, musicVolume={formatInvariant(musicVolume)})",
                Framework.Logging.LoggingTarget.Runtime,
                Framework.Logging.LogLevel.Verbose);

            string? lastErrorLine = null;

            using var process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException("Failed to start FFmpeg remux step.");

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    lastErrorLine = e.Data;
            };
            process.BeginErrorReadLine();
            if (!process.WaitForExit(120000))
            {
                process.Kill(true);
                throw new InvalidOperationException("FFmpeg remux timed out after 120 seconds.");
            }

            if (process.ExitCode != 0)
                throw new InvalidOperationException(lastErrorLine ?? $"FFmpeg remux exited with code {process.ExitCode}.");

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            File.Move(tempOutputPath, outputPath);

            if (File.Exists(inputVideoPath) && inputVideoPath != outputPath)
                File.Delete(inputVideoPath);
        }

        private static string buildHitsoundFilter(double hitsoundGain, double trimLeadingMs, double placeAtMs, double? playDurationMs)
        {
            string current = "[1:a:0]";
            var filter = new System.Text.StringBuilder();

            if (trimLeadingMs > 0.5)
            {
                filter.Append($"{current}atrim=start={formatInvariant(trimLeadingMs / 1000.0)},asetpts=PTS-STARTPTS[hs0];");
                current = "[hs0]";
            }

            if (playDurationMs.HasValue && playDurationMs.Value > 0)
            {
                filter.Append($"{current}atrim=end={formatInvariant(playDurationMs.Value / 1000.0)},asetpts=PTS-STARTPTS[hs1];");
                current = "[hs1]";
            }

            // Always apply the mix scale so amix (normalize=0) receives a constant-level input.
            filter.Append($"{current}volume={formatInvariant(amix_input_scale * hitsoundGain)}[hs2];");
            current = "[hs2]";

            if (placeAtMs > 0.5)
            {
                int delayMs = Math.Max(0, (int)Math.Round(placeAtMs));
                filter.Append($"{current}adelay={delayMs}|{delayMs}[hs]");
            }
            else
            {
                filter.Append($"{current}anull[hs]");
            }

            return filter.ToString();
        }

        public static void ApplyMusicVolume(string inputVideoPath, string outputPath, double musicVolume)
        {
            if (Math.Abs(musicVolume - 1.0) < 0.001)
            {
                if (inputVideoPath != outputPath)
                {
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                    File.Move(inputVideoPath, outputPath);
                }
                return;
            }

            if (!File.Exists(inputVideoPath))
                throw new FileNotFoundException("Rendered video for volume adjustment was not found.", inputVideoPath);

            string tempOutputPath = $"{outputPath}.vol.mp4";
            string arguments = $"-i \"{inputVideoPath}\" -filter:a \"volume={formatInvariant(musicVolume)}\" -c:v copy -c:a aac -b:a 192k -movflags +faststart -y \"{tempOutputPath}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = FFmpegEncoder.ExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Framework.Logging.Logger.Log($"[FFmpeg] Applying music volume {formatInvariant(musicVolume)} to {inputVideoPath} -> {outputPath}",
                Framework.Logging.LoggingTarget.Runtime,
                Framework.Logging.LogLevel.Verbose);

            string? lastErrorLine = null;

            using var process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException("Failed to start FFmpeg volume adjustment step.");

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    lastErrorLine = e.Data;
            };
            process.BeginErrorReadLine();
            if (!process.WaitForExit(120000))
            {
                process.Kill(true);
                throw new InvalidOperationException("FFmpeg volume adjustment timed out after 120 seconds.");
            }

            if (process.ExitCode != 0)
                throw new InvalidOperationException(lastErrorLine ?? $"FFmpeg volume adjustment exited with code {process.ExitCode}.");

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            File.Move(tempOutputPath, outputPath);

            if (File.Exists(inputVideoPath) && inputVideoPath != outputPath)
                File.Delete(inputVideoPath);
        }

        private static string formatInvariant(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
