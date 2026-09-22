// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using osu.Framework.Platform;
using osu.Game.IO;
using osu.Game.Performance.Diagnostics;

namespace osu.Desktop.Performance
{
    internal static class MosuDiagnosticsExporter
    {
        private static readonly JsonSerializerSettings serializer_settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
        };

        public static string ExportZip(Storage storage, MosuDiagnosticsSession session)
        {
            var exportStorage = storage.GetStorageForDirectory("exports");
            string zipFileName = $"diagnostics_{session.SessionId}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            string performanceDirectory = storage.GetFullPath("performance", true);

            using (var zipStream = exportStorage.GetStream(zipFileName, FileAccess.Write, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                addTextEntry(archive, "README.txt", createReadme(session));
                addTextEntry(archive, "report.txt", createReport(session));
                addTextEntry(archive, "stutters.csv", createStuttersCsv(session));
                addTextEntry(archive, "build.json", JsonConvert.SerializeObject(new
                {
                    session.ClientVersion,
                    session.ClientVersionHash,
                    session.RuntimeDescription,
                    session.Machine.OsDescription,
                    session.Machine.CpuDescription,
                    session.Machine.GpuDescription,
                    session.OriginalRenderer,
                    session.OriginalSkin,
                }, serializer_settings));
                addTextEntry(archive, "session.json", JsonConvert.SerializeObject(session, serializer_settings));

                if (File.Exists(MosuDiagnosticsSessionStore.GetSessionPath(storage)))
                    addFileEntry(archive, "performance/diagnostics/session.json", MosuDiagnosticsSessionStore.GetSessionPath(storage));

                foreach (string file in collectPerformanceFiles(performanceDirectory, session))
                    addFileEntry(archive, Path.Combine("performance", Path.GetFileName(file)), file);

                foreach (string file in collectRuntimeLogs(storage, session))
                    addFileEntry(archive, Path.Combine("logs", Path.GetFileName(file)), file);
            }

            return exportStorage.GetFullPath(zipFileName);
        }

        private static string createReadme(MosuDiagnosticsSession session) =>
            $"""
             Morasooma performance diagnostics

             Send this ZIP file as-is. Do not unpack or rename individual files.

             Session: {session.SessionId}
             Started: {session.StartedAt:O}
             Completed: {session.CompletedAt:O}
             Client: {session.ClientVersion}
             """;

        private static string createReport(MosuDiagnosticsSession session)
        {
            var report = new StringBuilder();
            report.AppendLine("Morasooma performance diagnostics report");
            report.AppendLine($"Session: {session.SessionId}");
            report.AppendLine($"Mode: {session.Options.Mode}");
            report.AppendLine($"Started: {session.StartedAt:O}");
            report.AppendLine($"Completed: {session.CompletedAt:O}");
            report.AppendLine($"Client: {session.ClientVersion}");
            report.AppendLine($"Version hash: {session.ClientVersionHash}");
            report.AppendLine($"Runtime: {session.RuntimeDescription}");
            report.AppendLine($"OS: {session.Machine.OsDescription}");
            report.AppendLine($"CPU: {session.Machine.CpuDescription}");
            report.AppendLine($"Renderer: {session.Machine.GpuDescription}");
            report.AppendLine($"Completed stages: {session.CompletedSteps}/{session.TotalSteps}");
            report.AppendLine($"Settings not applicable to this system: {session.SkippedProfiles.Count}");
            report.AppendLine();
            appendReadableComparisonSummary(report, session);
            report.AppendLine();
            report.AppendLine("RAW RESULTS (CSV)");
            report.AppendLine("renderer,profile,comparison_baseline,baseline_avg_fps,baseline_p1_fps,quality,avg_run_spread_percent,p1_run_spread_percent,avg_fps,p5_fps,p1_fps,min_fps,avg_delta_percent,p1_delta_percent,draw_work_improvement_percent,update_work_improvement_percent,input_improvement_percent,draw_p99_ms,update_p99_ms,draw_work_p99_ms,update_work_p99_ms,input_p99_ms,stutters,failure");

            foreach (MosuDiagnosticsRendererResult result in session.Results)
            {
                MosuDiagnosticsRendererResult? comparisonBaseline = MosuDiagnosticsAnalysis.CreateComparisonBaseline(session, result);

                report.AppendLine(string.Join(',',
                    csv(result.Renderer.ToString()),
                    csv(result.Profile.ToString()),
                    csv(comparisonBaseline?.Profile.ToString() ?? string.Empty),
                    invariant(comparisonBaseline?.AvgFps),
                    invariant(comparisonBaseline?.P1Fps),
                    csv(result.Quality.ToString()),
                    invariant(result.AvgFpsVariationPercent),
                    invariant(result.P1FpsVariationPercent),
                    invariant(result.AvgFps),
                    invariant(result.P5Fps),
                    invariant(result.P1Fps),
                    result.MinFps?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    invariant(increasePercent(comparisonBaseline?.AvgFps, result.AvgFps)),
                    invariant(increasePercent(comparisonBaseline?.P1Fps, result.P1Fps)),
                    invariant(decreasePercent(comparisonBaseline?.DrawWorkP99Ms, result.DrawWorkP99Ms)),
                    invariant(decreasePercent(comparisonBaseline?.UpdateWorkP99Ms, result.UpdateWorkP99Ms)),
                    invariant(decreasePercent(comparisonBaseline?.InputP99Ms, result.InputP99Ms)),
                    invariant(result.DrawP99Ms),
                    invariant(result.UpdateP99Ms),
                    invariant(result.DrawWorkP99Ms),
                    invariant(result.UpdateWorkP99Ms),
                    invariant(result.InputP99Ms),
                    result.StutterCount.ToString(CultureInfo.InvariantCulture),
                    csv(result.FailureReason ?? string.Empty)));
            }

            return report.ToString();
        }

        private static void appendReadableComparisonSummary(StringBuilder report, MosuDiagnosticsSession session)
        {
            report.AppendLine("COMPARISON SUMMARY");
            report.AppendLine("Every candidate below is compared with the time-adjusted clean baseline where all tested optimisations are disabled.");
            report.AppendLine();

            var comparisons = session.Results
                                     .Where(result =>
                                         result.Completed
                                         && result.Quality != MosuDiagnosticsQuality.Failed
                                         && !MosuDiagnosticsProfiles.IsReferenceProfile(result.Profile))
                                     .Select(result => new
                                     {
                                         Result = result,
                                         Baseline = MosuDiagnosticsAnalysis.CreateComparisonBaseline(session, result),
                                     })
                                     .Where(comparison => comparison.Baseline != null)
                                     .OrderBy(comparison => comparison.Result.Profile == MosuDiagnosticsProfile.Recommended ? 0 : 1)
                                     .ThenBy(comparison => session.Profiles.IndexOf(comparison.Result.Profile))
                                     .ToList();

            if (comparisons.Count == 0)
            {
                report.AppendLine("No completed profile-to-baseline comparisons were available.");
            }
            else
            {
                foreach (var comparison in comparisons)
                {
                    MosuDiagnosticsRendererResult result = comparison.Result;
                    MosuDiagnosticsRendererResult baseline = comparison.Baseline!;

                    report.AppendLine(getReadableProfileName(result.Profile));
                    report.AppendLine($"  Renderer: {result.Renderer}");
                    report.AppendLine($"  Average FPS: {readable(result.AvgFps)} vs {readable(baseline.AvgFps)} ({signed(increasePercent(baseline.AvgFps, result.AvgFps))})");
                    report.AppendLine($"  Slowest 1% FPS: {readable(result.P1Fps)} vs {readable(baseline.P1Fps)} ({signed(increasePercent(baseline.P1Fps, result.P1Fps))})");
                    report.AppendLine($"  Draw work p99: {signed(decreasePercent(baseline.DrawWorkP99Ms, result.DrawWorkP99Ms))}");
                    report.AppendLine($"  Update work p99: {signed(decreasePercent(baseline.UpdateWorkP99Ms, result.UpdateWorkP99Ms))}");
                    report.AppendLine($"  Input p99: {signed(decreasePercent(baseline.InputP99Ms, result.InputP99Ms))}");
                    report.AppendLine($"  Candidate measurement: {result.Quality}; run spread average {percentage(result.AvgFpsVariationPercent)}, slowest 1% {percentage(result.P1FpsVariationPercent)}");

                    if (result.Profile == MosuDiagnosticsProfile.Use8kPollingRate)
                        report.AppendLine("  Scope: CPU and input-thread cost of 8000 Hz scheduling during replay playback; mouse sensor and USB latency are not measured.");
                    else if (result.Profile == MosuDiagnosticsProfile.AllowTearing)
                        report.AppendLine("  Scope: current Windows display mode; the effect depends on window mode, graphics driver and VRR support.");

                    report.AppendLine();
                }
            }

            if (session.SkippedProfiles.Count == 0)
                return;

            report.AppendLine("NOT APPLICABLE ON THIS SYSTEM");

            foreach (MosuDiagnosticsSkippedProfile skipped in session.SkippedProfiles)
                report.AppendLine($"  {getReadableProfileName(skipped.Profile)} on {skipped.Renderer}: {getReadableSkipReason(skipped.Reason)}");
        }

        private static string getReadableProfileName(MosuDiagnosticsProfile profile) => profile switch
        {
            MosuDiagnosticsProfile.Recommended => "COMPLETE RECOMMENDED PROFILE VS ALL OPTIMISATIONS DISABLED",
            MosuDiagnosticsProfile.SkinPerformanceMode => "Complete skin performance package",
            _ => profile.ToString(),
        };

        private static string getReadableSkipReason(MosuDiagnosticsSkipReason reason) => reason switch
        {
            MosuDiagnosticsSkipReason.RequiresWindows => "requires Windows",
            MosuDiagnosticsSkipReason.RequiresDeferredRenderer => "only affects the Deferred renderer",
            MosuDiagnosticsSkipReason.RequiresNonDeferredRenderer => "only affects the non-Deferred Veldrid renderer",
            _ => reason.ToString(),
        };

        private static string readable(double? value) =>
            value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "n/a";

        private static string percentage(double? value) =>
            value != null ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) + "%" : "n/a";

        private static string signed(double? value) =>
            value != null ? value.Value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture) + "%" : "n/a";

        private static double? increasePercent(double? baseline, double? candidate) =>
            baseline is > 0 && candidate != null ? (candidate.Value - baseline.Value) / baseline.Value * 100 : null;

        private static double? decreasePercent(double? baseline, double? candidate) =>
            baseline is > 0 && candidate != null ? (baseline.Value - candidate.Value) / baseline.Value * 100 : null;

        private static string createStuttersCsv(MosuDiagnosticsSession session)
        {
            var output = new StringBuilder();
            output.AppendLine("renderer,profile,local_time,elapsed_ms,thread,frame_ms,gc_ms,ccl,invalidations,note");

            foreach (MosuDiagnosticsRendererResult result in session.Results)
            {
                foreach (MosuDiagnosticsStutterEvent stutter in result.Stutters)
                {
                    output.AppendLine(string.Join(',',
                        csv(result.Renderer.ToString()),
                        csv(result.Profile.ToString()),
                        csv(stutter.LocalTime),
                        stutter.ElapsedMs.ToString("0.###", CultureInfo.InvariantCulture),
                        csv(stutter.Thread),
                        stutter.FrameMs.ToString("0.###", CultureInfo.InvariantCulture),
                        stutter.GcMs.ToString("0.###", CultureInfo.InvariantCulture),
                        stutter.Ccl.ToString(CultureInfo.InvariantCulture),
                        stutter.Invalidations.ToString(CultureInfo.InvariantCulture),
                        csv(stutter.Note ?? string.Empty)));
                }
            }

            return output.ToString();
        }

        private static IEnumerable<string> collectRuntimeLogs(Storage storage, MosuDiagnosticsSession session)
        {
            string logsDirectory = storage.GetFullPath("logs", true);

            if (!Directory.Exists(logsDirectory))
                yield break;

            DateTime startUtc = session.StartedAt.UtcDateTime.AddMinutes(-2);
            DateTime endUtc = (session.CompletedAt ?? DateTimeOffset.Now).UtcDateTime.AddMinutes(5);

            foreach (string file in Directory.GetFiles(logsDirectory, "*.runtime.log", SearchOption.TopDirectoryOnly))
            {
                DateTime modified = File.GetLastWriteTimeUtc(file);

                if (modified >= startUtc && modified <= endUtc)
                    yield return file;
            }
        }

        private static IEnumerable<string> collectPerformanceFiles(string performanceDirectory, MosuDiagnosticsSession session)
        {
            if (!Directory.Exists(performanceDirectory))
                yield break;

            var prefixes = new HashSet<string>(StringComparer.Ordinal)
            {
                session.SessionId,
            };

            foreach (var result in session.Results)
                prefixes.Add($"{session.SessionId}-{result.Renderer}");

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string prefix in prefixes)
            {
                foreach (string pattern in new[]
                         {
                             $"mosu-replay-benchmark-{prefix}*",
                             $"mosu-performance-{prefix}*",
                         })
                {
                    foreach (string file in Directory.GetFiles(performanceDirectory, pattern, SearchOption.TopDirectoryOnly))
                    {
                        if (yielded.Add(file))
                            yield return file;
                    }
                }
            }

            foreach (string file in Directory.GetFiles(performanceDirectory, "mosu-performance-*.segments.csv", SearchOption.TopDirectoryOnly))
            {
                if (yielded.Add(file) && fileContainsBenchmarkPrefix(file, prefixes))
                    yield return file;
            }

            foreach (string file in Directory.GetFiles(performanceDirectory, "mosu-performance-*.summary.csv", SearchOption.TopDirectoryOnly))
            {
                if (yielded.Add(file) && fileContainsBenchmarkPrefix(file, prefixes))
                    yield return file;
            }

            foreach (string file in Directory.GetFiles(performanceDirectory, "mosu-performance-*.csv", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(file);

                if (name.EndsWith(".segments.csv", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".summary.csv", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (yielded.Add(file) && fileContainsBenchmarkPrefix(file, prefixes))
                    yield return file;
            }
        }

        private static bool fileContainsBenchmarkPrefix(string filePath, IEnumerable<string> prefixes)
        {
            string name = Path.GetFileName(filePath);

            if (prefixes.Any(prefix => name.Contains(prefix, StringComparison.Ordinal)))
                return true;

            try
            {
                using var reader = new StreamReader(filePath);

                for (int i = 0; i < 5 && !reader.EndOfStream; i++)
                {
                    string? line = reader.ReadLine();

                    if (line != null && prefixes.Any(prefix => line.Contains(prefix, StringComparison.Ordinal)))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void addTextEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write(content);
        }

        private static void addFileEntry(ZipArchive archive, string entryName, string filePath)
        {
            try
            {
                var entry = archive.CreateEntry(entryName.Replace('\\', '/'));
                using var entryStream = entry.Open();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fileStream.CopyTo(entryStream);
            }
            catch
            {
            }
        }

        private static string invariant(double? value) =>
            value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;

        private static string csv(string value)
        {
            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
                return value;

            return '"' + value.Replace("\"", "\"\"") + '"';
        }
    }
}
