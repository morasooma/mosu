// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Configuration;

namespace osu.Game.Scoring.Render
{
    public static class RenderEncodingSelector
    {
        public const string AutoEncoderLabel = "Auto (Recommended)";

        private static readonly Lazy<string[]> detectedGpuNames = new Lazy<string[]>(detectGpuNames);

        public static IReadOnlyList<string> GetEncoderOptions()
            => new[]
            {
                AutoEncoderLabel,
                "libx264 (CPU)",
                "h264_nvenc (NVIDIA)",
                "h264_amf (AMD)",
                "h264_qsv (Intel)",
                "h264_vaapi (Linux)",
                "hevc_vaapi (Linux)",
            };

        public static string GetRecommendedEncoderLabel(GameHost host)
        {
            string gpuSummary = string.Join(" | ", detectedGpuNames.Value).ToLowerInvariant();

            if (OperatingSystem.IsWindows())
            {
                if (gpuSummary.Contains("nvidia"))
                    return "h264_nvenc (NVIDIA)";

                if (gpuSummary.Contains("amd") || gpuSummary.Contains("radeon"))
                    return "h264_amf (AMD)";

                if (gpuSummary.Contains("intel"))
                    return "h264_qsv (Intel)";
            }

            if (OperatingSystem.IsLinux())
            {
                if (gpuSummary.Contains("nvidia"))
                    return "h264_nvenc (NVIDIA)";

                if (gpuSummary.Contains("intel") || gpuSummary.Contains("amd") || gpuSummary.Contains("radeon"))
                    return "h264_vaapi (Linux)";
            }

            return "libx264 (CPU)";
        }

        public static string ResolveEncoderArgument(string selectedLabel, GameHost host)
            => resolveEncoderArgument(selectedLabel, host, allowHardwareDetection: true);

        public static int GetRecommendedBitrateMbps(string resolution, int fps, string selectedLabel, ReplayRenderQualityPreset qualityPreset, GameHost host)
        {
            (int width, int height) = parseResolution(resolution);
            double megapixels = width * height / 1_000_000.0;

            string encoderArgument = resolveEncoderArgument(selectedLabel, host, allowHardwareDetection: false);
            double efficiencyMultiplier = encoderArgument switch
            {
                "libx264" => 1.0,
                "h264_nvenc" => 1.1,
                "h264_amf" => 1.15,
                "h264_qsv" => 1.1,
                "h264_vaapi" => 1.2,
                "hevc_vaapi" => 0.85,
                _ => 1.0,
            };

            double presetMultiplier = qualityPreset switch
            {
                ReplayRenderQualityPreset.Fast => 3.5,
                ReplayRenderQualityPreset.Balanced => 5.0,
                ReplayRenderQualityPreset.Quality => 6.5,
                _ => 5.0,
            };

            // Deliberately derive the bitrate from resolution only.
            // In replay renders, increasing output fps inflates file size on its own, while using
            // fps in the bitrate heuristic tended to overshoot and produce unnecessarily heavy files.
            int recommended = (int)Math.Ceiling(megapixels * presetMultiplier * efficiencyMultiplier);
            return Math.Clamp(recommended, 4, 28);
        }

        private static string resolveEncoderArgument(string selectedLabel, GameHost host, bool allowHardwareDetection)
        {
            string resolved = selectedLabel;

            if (selectedLabel == AutoEncoderLabel)
                resolved = allowHardwareDetection ? GetRecommendedEncoderLabel(host) : getFastAutoFallback(host);

            return resolved.Split(' ')[0];
        }

        private static string getFastAutoFallback(GameHost host)
        {
            if (OperatingSystem.IsLinux())
                return "h264_vaapi (Linux)";

            if (OperatingSystem.IsWindows())
            {
                string gpuSummary = string.Join(" | ", detectedGpuNames.Value).ToLowerInvariant();

                if (gpuSummary.Contains("nvidia"))
                    return "h264_nvenc (NVIDIA)";

                if (gpuSummary.Contains("amd") || gpuSummary.Contains("radeon"))
                    return "h264_amf (AMD)";

                if (gpuSummary.Contains("intel"))
                    return "h264_qsv (Intel)";
            }

            return "libx264 (CPU)";
        }

        public static string DescribeRecommendation(GameHost host)
        {
            string encoder = GetRecommendedEncoderLabel(host);
            string gpu = detectedGpuNames.Value.Length > 0
                ? string.Join(", ", detectedGpuNames.Value)
                : "unknown GPU";

            return $"{encoder} via {gpu}";
        }

        private static (int width, int height) parseResolution(string resolution)
        {
            string[] parts = resolution.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height)
                && width > 0
                && height > 0)
            {
                return (width, height);
            }

            return (1920, 1080);
        }

        private static string[] detectGpuNames()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    return readCommandLines("powershell", "-NoProfile -ExecutionPolicy Bypass -Command \"(Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name) -join [Environment]::NewLine\"");

                if (OperatingSystem.IsLinux())
                    return readCommandLines("/bin/sh", "-lc \"lspci | grep -Ei 'vga|3d|display'\"");
            }
            catch
            {
            }

            return Array.Empty<string>();
        }

        private static string[] readCommandLines(string fileName, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });

            if (process == null)
                return Array.Empty<string>();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1500);

            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .ToArray();
        }
    }
}
