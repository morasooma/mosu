// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;

namespace osu.Game.Scoring.Render
{
    internal static class FFmpegPostProcessor
    {
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
            // amix default normalization divides each input by the number of inputs (1/2 each),
            // giving ~50% volume per source — matching what the player heard in-game.
            // normalize=0 was causing clipping (full+full) which the alimiter had to squash → distortion.
            string filterAndMapArgs = videoAlreadyHasAudio
                ? $"-filter_complex \"{gainFilter};[0:a:0]{musicFilterArg}[m];[m][hs]amix=inputs=2:duration=first[a]\" -map 0:v:0 -map \"[a]\""
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

            if (Math.Abs(hitsoundGain - 1) > 0.0001)
            {
                filter.Append($"{current}volume={formatInvariant(hitsoundGain)}[hs2];");
                current = "[hs2]";
            }

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
