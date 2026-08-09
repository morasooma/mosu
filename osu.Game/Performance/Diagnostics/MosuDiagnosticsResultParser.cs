// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace osu.Game.Performance.Diagnostics
{
    public static class MosuDiagnosticsResultParser
    {
        public sealed class ParsedBenchmarkMetrics
        {
            public double? AvgFps { get; init; }
            public double? P5Fps { get; init; }
            public double? P1Fps { get; init; }
            public int? MinFps { get; init; }
            public double? DrawP99Ms { get; init; }
            public double? UpdateP99Ms { get; init; }
            public double? DrawWorkP99Ms { get; init; }
            public double? UpdateWorkP99Ms { get; init; }
            public double? InputP99Ms { get; init; }
            public double? CclP99 { get; init; }
            public double? InvalP99 { get; init; }
            public int MeasuredRunCount { get; init; }
            public double? AvgFpsVariationPercent { get; init; }
            public double? P1FpsVariationPercent { get; init; }
            public MosuDiagnosticsQuality Quality { get; init; }
            public string? QualityReason { get; init; }
        }

        public static ParsedBenchmarkMetrics? TryParseSegments(string performanceDirectory, string benchmarkIdPrefix)
        {
            var segmentFiles = Directory.Exists(performanceDirectory)
                ? Directory.GetFiles(performanceDirectory, "mosu-performance-*.segments.csv", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            var matchingRows = new List<Dictionary<string, string>>();

            foreach (string file in segmentFiles.OrderByDescending(File.GetLastWriteTimeUtc))
            {
                foreach (var row in readCsvRows(file))
                {
                    if (!tryGet(row, "benchmark_id", out string? benchmarkId) || string.IsNullOrEmpty(benchmarkId))
                        continue;

                    if (!benchmarkId.StartsWith(benchmarkIdPrefix, StringComparison.Ordinal))
                        continue;

                    if (tryGet(row, "benchmark_mode", out string? mode) && !string.Equals(mode, MosuDiagnosticsDefaults.BenchmarkMode, StringComparison.Ordinal))
                        continue;

                    if (tryGet(row, "benchmark_warmup", out string? warmup) && string.Equals(warmup, bool.TrueString, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (tryGet(row, "active_ratio", out string? activeRatio)
                        && double.TryParse(activeRatio, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedActiveRatio)
                        && parsedActiveRatio < 0.999)
                        continue;

                    matchingRows.Add(row);
                }

                if (matchingRows.Count > 0)
                    break;
            }

            if (matchingRows.Count == 0)
                return null;

            double? avgVariation = variationPercent(matchingRows, "draw_fps_avg");
            double? p1Variation = variationPercent(matchingRows, "draw_fps_p1");
            double worstVariation = Math.Max(avgVariation ?? 0, p1Variation ?? 0);
            bool hasEnoughRuns = matchingRows.Count >= MosuDiagnosticsDefaults.MeasuredRuns;
            MosuDiagnosticsQuality quality = !hasEnoughRuns
                ? MosuDiagnosticsQuality.Failed
                : matchingRows.Count == 1
                    ? MosuDiagnosticsQuality.SingleRun
                    : worstVariation <= MosuDiagnosticsDefaults.ReliableVariationPercent
                    ? MosuDiagnosticsQuality.Reliable
                    : worstVariation <= MosuDiagnosticsDefaults.UnstableVariationPercent
                        ? MosuDiagnosticsQuality.Variable
                        : MosuDiagnosticsQuality.Unstable;

            return new ParsedBenchmarkMetrics
            {
                AvgFps = average(matchingRows, "draw_fps_avg"),
                P5Fps = average(matchingRows, "draw_fps_p5"),
                P1Fps = average(matchingRows, "draw_fps_p1"),
                MinFps = (int?)average(matchingRows, "draw_fps_min"),
                DrawP99Ms = average(matchingRows, "draw_ms_max_p99"),
                UpdateP99Ms = average(matchingRows, "update_ms_max_p99"),
                DrawWorkP99Ms = average(matchingRows, "draw_work_ms_max_p99"),
                UpdateWorkP99Ms = average(matchingRows, "update_work_ms_max_p99"),
                InputP99Ms = average(matchingRows, "input_ms_max_p99"),
                CclP99 = average(matchingRows, "update_ccl_max_p99"),
                InvalP99 = average(matchingRows, "update_invalidations_max_p99"),
                MeasuredRunCount = matchingRows.Count,
                AvgFpsVariationPercent = avgVariation,
                P1FpsVariationPercent = p1Variation,
                Quality = quality,
                QualityReason = !hasEnoughRuns
                    ? $"insufficient_measured_runs={matchingRows.Count}/{MosuDiagnosticsDefaults.MeasuredRuns}"
                    : quality == MosuDiagnosticsQuality.SingleRun
                        ? "single_measured_run"
                    : quality == MosuDiagnosticsQuality.Reliable
                        ? null
                        : $"measured_run_spread={worstVariation:0.##}%",
            };
        }

        private static double? average(IReadOnlyList<Dictionary<string, string>> rows, string column)
        {
            var values = rows.Select(r => tryParseDouble(r, column)).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            return values.Length == 0 ? null : values.Average();
        }

        private static double? variationPercent(IReadOnlyList<Dictionary<string, string>> rows, string column)
        {
            var values = rows.Select(r => tryParseDouble(r, column)).Where(v => v.HasValue).Select(v => v!.Value).ToArray();

            if (values.Length < 2)
                return null;

            double mean = values.Average();
            return mean == 0 ? null : (values.Max() - values.Min()) / mean * 100;
        }

        private static double? tryParseDouble(Dictionary<string, string> row, string column)
        {
            if (!tryGet(row, column, out string? value))
                return null;

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : null;
        }

        private static IEnumerable<Dictionary<string, string>> readCsvRows(string path)
        {
            using var reader = new StreamReader(path);
            string? headerLine = reader.ReadLine();

            if (headerLine == null)
                yield break;

            string[] headers = headerLine.Split(',');

            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] values = line.Split(',');
                var row = new Dictionary<string, string>(StringComparer.Ordinal);

                for (int i = 0; i < headers.Length && i < values.Length; i++)
                    row[headers[i]] = values[i];

                yield return row;
            }
        }

        private static bool tryGet(Dictionary<string, string> row, string key, out string? value)
        {
            if (row.TryGetValue(key, out string? found))
            {
                value = found;
                return true;
            }

            value = null;
            return false;
        }
    }
}
