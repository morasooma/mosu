// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.Performance;
using osu.Game.Performance.Diagnostics;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;

namespace osu.Desktop.Performance
{
    internal partial class MosuPerformanceDiagnosticsRunner : Component
    {
        public const string LAUNCH_ARG = "--mosu-diagnostics";
        public const string ATLAS_SIZE_ARG = "--mosu-diagnostics-atlas-size";

        private const int segment_parse_retry_count = 40;
        private const double segment_parse_retry_delay = 250;
        private const int results_ready_retry_count = 120;
        private const double results_ready_retry_delay = 500;
        private const double results_menu_settle_delay = 1000;

        private readonly string[] launchArgs;
        private bool started;
        private string? displayedResultsSessionId;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private PerformanceDiagnosticsManager diagnosticsManager { get; set; } = null!;

        public MosuPerformanceDiagnosticsRunner(string[]? launchArgs)
        {
            this.launchArgs = launchArgs ?? Array.Empty<string>();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            MosuReplayBenchmarkRunner.BenchmarkCompleted += onBenchmarkCompleted;

            bool diagnosticsLaunch = isDiagnosticsLaunch();
            var persistedSession = MosuDiagnosticsSessionStore.TryLoad(storage);
            bool hasPendingResults = persistedSession?.Phase is MosuDiagnosticsPhase.PendingResultsRestart or MosuDiagnosticsPhase.ShowResults;

            if (!diagnosticsLaunch
                && persistedSession?.Phase is MosuDiagnosticsPhase.PendingRestart or MosuDiagnosticsPhase.RunningBenchmark)
            {
                MosuDiagnosticsStutterCollector.Reset();
                persistedSession.Phase = MosuDiagnosticsPhase.Paused;
                MosuDiagnosticsSessionStore.Save(storage, persistedSession);
                diagnosticsManager.HandleSessionUpdated(persistedSession);
                Logger.Log($"mosu diagnostics paused persisted session {persistedSession.SessionId} on a normal launch.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            if (!diagnosticsLaunch && !hasPendingResults)
                return;

            Scheduler.AddDelayed(tryContinueSession, 250);
        }

        protected override void Dispose(bool isDisposing)
        {
            MosuReplayBenchmarkRunner.BenchmarkCompleted -= onBenchmarkCompleted;
            base.Dispose(isDisposing);
        }

        private bool isDiagnosticsLaunch()
            => launchArgs.Any(arg => string.Equals(arg, LAUNCH_ARG, StringComparison.Ordinal));

        private void tryContinueSession()
        {
            if (started)
                return;

            var session = MosuDiagnosticsSessionStore.TryLoad(storage);

            if (session == null || !MosuDiagnosticsSessionStore.IsResumable(session))
                return;

            started = true;
            diagnosticsManager.HandleSessionUpdated(session);

            switch (session.Phase)
            {
                case MosuDiagnosticsPhase.PendingRestart:
                    diagnosticsManager.RequestShowProgress(session);
                    handlePendingRestart(session);
                    break;

                case MosuDiagnosticsPhase.RunningBenchmark:
                    diagnosticsManager.RequestShowProgress(session);

                    if (launchArgs.Any(arg => arg.StartsWith("--mosu-replay-benchmark", StringComparison.Ordinal)))
                        beginBenchmarkCollection(session);
                    break;

                case MosuDiagnosticsPhase.PendingResultsRestart:
                    showCompletedSession(session);
                    break;

                case MosuDiagnosticsPhase.ShowResults:
                    requestShowResultsWhenReady(session.SessionId, 0);
                    break;
            }
        }

        private void handlePendingRestart(MosuDiagnosticsSession session)
        {
            if (session.ExpectedRenderer == null
                || session.ExpectedProfile == null
                || session.RendererIndex >= session.Renderers.Count
                || session.ProfileIndex >= session.Profiles.Count)
            {
                completeSession(session);
                return;
            }

            var expected = session.ExpectedRenderer.Value;
            var resolved = host.ResolvedRenderer;
            var setting = frameworkConfig.Get<RendererType>(FrameworkSetting.Renderer);

            Logger.Log($"mosu diagnostics renderer check: expected={expected}, setting={setting}, resolved={resolved}", LoggingTarget.Runtime, LogLevel.Important);

            if (resolved != expected)
            {
                recordRendererFailure(session, expected, resolved, $"resolved_renderer_mismatch:{resolved}");
                advanceStep(session);
                return;
            }

            session.Phase = MosuDiagnosticsPhase.RunningBenchmark;
            MosuDiagnosticsSessionStore.Save(storage, session);
            diagnosticsManager.HandleSessionUpdated(session);
            diagnosticsManager.RequestShowProgress(session);

            if (launchArgs.Any(arg => arg.StartsWith("--mosu-replay-benchmark", StringComparison.Ordinal)))
                beginBenchmarkCollection(session);
            else
                diagnosticsManager.RequestRestart(BuildLaunchArgs(session, includeBenchmark: true));
        }

        private void beginBenchmarkCollection(MosuDiagnosticsSession session)
        {
            if (session.ExpectedRenderer == null || session.ExpectedProfile == null)
                return;

            MosuDiagnosticsStutterCollector.BeginCollection(
                frameworkConfig.Get<RendererType>(FrameworkSetting.Renderer),
                host.ResolvedRenderer);

            Logger.Log(
                $"mosu diagnostics collecting stutters for renderer {session.ExpectedRenderer.Value}, profile {session.ExpectedProfile.Value}",
                LoggingTarget.Runtime,
                LogLevel.Important);
        }

        private void onBenchmarkCompleted(string benchmarkId, string? failureReason)
        {
            var session = MosuDiagnosticsSessionStore.TryLoad(storage);

            if (session == null || session.Phase != MosuDiagnosticsPhase.RunningBenchmark)
                return;

            if (!benchmarkId.StartsWith(session.SessionId, StringComparison.Ordinal))
                return;

            var stutters = MosuDiagnosticsStutterCollector.TakeEvents().ToList();
            MosuDiagnosticsStutterCollector.EndCollection();

            if (!string.IsNullOrEmpty(failureReason)
                && session.ExpectedRenderer != null)
            {
                recordRendererFailure(session, session.ExpectedRenderer.Value, host.ResolvedRenderer, failureReason);

                if (isSessionBlockingFailure(failureReason))
                {
                    completeSession(session);
                    return;
                }

                advanceStep(session);
                return;
            }

            tryRecordRendererSuccess(session.SessionId, benchmarkId, stutters, 0);
        }

        private void tryRecordRendererSuccess(string sessionId, string benchmarkId, List<MosuDiagnosticsStutterEvent> stutters, int attempt)
        {
            var session = MosuDiagnosticsSessionStore.TryLoad(storage);

            if (session == null
                || session.SessionId != sessionId
                || session.Phase != MosuDiagnosticsPhase.RunningBenchmark)
                return;

            if (session.ExpectedRenderer == null || session.ExpectedProfile == null)
                return;

            string performanceDirectory = storage.GetFullPath("performance", true);
            var metrics = MosuDiagnosticsResultParser.TryParseSegments(performanceDirectory, benchmarkId);

            bool hasAllMeasuredRuns = metrics?.MeasuredRunCount >= MosuDiagnosticsDefaults.MeasuredRuns;

            if (!hasAllMeasuredRuns && attempt < segment_parse_retry_count)
            {
                Scheduler.AddDelayed(
                    () => tryRecordRendererSuccess(sessionId, benchmarkId, stutters, attempt + 1),
                    segment_parse_retry_delay);
                return;
            }

            session.Results.Add(new MosuDiagnosticsRendererResult
            {
                Renderer = session.ExpectedRenderer.Value,
                Profile = session.ExpectedProfile.Value,
                ResolvedRenderer = host.ResolvedRenderer.ToString(),
                Started = true,
                Completed = true,
                AvgFps = metrics?.AvgFps,
                P5Fps = metrics?.P5Fps,
                P1Fps = metrics?.P1Fps,
                MinFps = metrics?.MinFps,
                DrawP99Ms = metrics?.DrawP99Ms,
                UpdateP99Ms = metrics?.UpdateP99Ms,
                DrawWorkP99Ms = metrics?.DrawWorkP99Ms,
                UpdateWorkP99Ms = metrics?.UpdateWorkP99Ms,
                InputP99Ms = metrics?.InputP99Ms,
                CclP99 = metrics?.CclP99,
                InvalP99 = metrics?.InvalP99,
                MeasuredRunCount = metrics?.MeasuredRunCount ?? 0,
                AvgFpsVariationPercent = metrics?.AvgFpsVariationPercent,
                P1FpsVariationPercent = metrics?.P1FpsVariationPercent,
                Quality = metrics?.Quality ?? MosuDiagnosticsQuality.Failed,
                QualityReason = metrics == null ? "metrics_missing" : metrics.QualityReason,
                StutterCount = stutters.Count,
                Stutters = stutters,
            });

            session.PendingStutters.AddRange(stutters);
            MosuDiagnosticsSessionStore.Save(storage, session);
            diagnosticsManager.HandleSessionUpdated(session);
            advanceStep(session);
        }

        private void recordRendererFailure(MosuDiagnosticsSession session, RendererType expected, RendererType resolved, string reason)
        {
            MosuDiagnosticsStutterCollector.Reset();

            session.Results.Add(new MosuDiagnosticsRendererResult
            {
                Renderer = expected,
                Profile = session.ExpectedProfile ?? MosuDiagnosticsProfile.Current,
                ResolvedRenderer = resolved.ToString(),
                Started = false,
                Completed = false,
                FailureReason = reason,
                Quality = MosuDiagnosticsQuality.Failed,
                QualityReason = reason,
            });

            MosuDiagnosticsSessionStore.Save(storage, session);
            diagnosticsManager.HandleSessionUpdated(session);
        }

        private static bool isSessionBlockingFailure(string reason)
            => reason.StartsWith("benchmark_replay_file_missing:", StringComparison.Ordinal)
               || reason.StartsWith("benchmark_replay_import_failed:", StringComparison.Ordinal)
               || reason.StartsWith("benchmark_replay_import_cancelled", StringComparison.Ordinal)
               || reason.StartsWith("benchmark_replay_beatmap_mismatch:", StringComparison.Ordinal)
               || reason.StartsWith("benchmark_replay_preparation_timeout:", StringComparison.Ordinal);

        private void advanceStep(MosuDiagnosticsSession session)
        {
            session.ProfileIndex++;

            if (session.ProfileIndex >= session.Profiles.Count)
            {
                session.ProfileIndex = 0;
                session.RendererIndex++;

                if (session.RendererIndex >= session.Renderers.Count)
                {
                    completeSession(session);
                    return;
                }
            }

            session.ExpectedRenderer = session.Renderers[session.RendererIndex];
            session.ExpectedProfile = session.Profiles[session.ProfileIndex];
            session.Phase = MosuDiagnosticsPhase.PendingRestart;
            MosuDiagnosticsSessionStore.Save(storage, session);
            frameworkConfig.SetValue(FrameworkSetting.Renderer, session.ExpectedRenderer.Value);
            diagnosticsManager.RequestShowProgress(session);
            diagnosticsManager.RequestRestart(BuildLaunchArgs(session, includeBenchmark: true));
        }

        private void completeSession(MosuDiagnosticsSession session)
        {
            MosuDiagnosticsStutterCollector.Reset();
            session.Phase = MosuDiagnosticsPhase.PendingResultsRestart;
            session.CompletedAt = DateTimeOffset.Now;
            session.ExpectedRenderer = null;
            session.ExpectedProfile = null;
            frameworkConfig.SetValue(FrameworkSetting.Renderer, session.OriginalRenderer);
            diagnosticsManager.RestoreOriginalSkin(session);
            MosuDiagnosticsSessionStore.Save(storage, session);
            diagnosticsManager.HandleSessionUpdated(session);
            diagnosticsManager.RequestShowProgress(session);
            diagnosticsManager.RequestRestart(BuildRestoreLaunchArgs(session, continueDiagnostics: true));
        }

        private void showCompletedSession(MosuDiagnosticsSession session)
        {
            session.Phase = MosuDiagnosticsPhase.ShowResults;
            MosuDiagnosticsSessionStore.Save(storage, session);
            diagnosticsManager.HandleSessionUpdated(session);
            requestShowResultsWhenReady(session.SessionId, 0);

            if (!session.Options.ExportZip)
                return;

            Task.Run(() =>
                {
                    try
                    {
                        return (Path: MosuDiagnosticsExporter.ExportZip(storage, session), Error: (Exception?)null);
                    }
                    catch (Exception ex)
                    {
                        return (Path: (string?)null, Error: ex);
                    }
                })
                .ContinueWith(exportTask => Schedule(() =>
                {
                    var exportResult = exportTask.GetResultSafely();
                    var current = MosuDiagnosticsSessionStore.TryLoad(storage);

                    if (current == null
                        || current.SessionId != session.SessionId
                        || current.Phase != MosuDiagnosticsPhase.ShowResults)
                        return;

                    if (exportResult.Error != null)
                    {
                        Logger.Error(exportResult.Error, "mosu diagnostics failed to export the results ZIP.");
                    }
                    else
                    {
                        current.ExportZipPath = exportResult.Path;
                        MosuDiagnosticsSessionStore.Save(storage, current);
                        diagnosticsManager.HandleSessionUpdated(current);

                        if (displayedResultsSessionId == current.SessionId)
                        {
                            diagnosticsManager.RequestShowResults(current);
                            presentExportOnce(current);
                        }
                    }
                }));
        }

        private void requestShowResultsWhenReady(string sessionId, int attempt)
        {
            var session = MosuDiagnosticsSessionStore.TryLoad(storage);

            if (session == null
                || session.SessionId != sessionId
                || session.Phase != MosuDiagnosticsPhase.ShowResults)
                return;

            if (game.ScreenStack?.CurrentScreen is MainMenu)
            {
                Scheduler.AddDelayed(() =>
                {
                    var current = MosuDiagnosticsSessionStore.TryLoad(storage);

                    if (current == null
                        || current.SessionId != sessionId
                        || current.Phase != MosuDiagnosticsPhase.ShowResults)
                        return;

                    Logger.Log($"mosu diagnostics displaying completed session {sessionId}.", LoggingTarget.Runtime, LogLevel.Important);
                    displayedResultsSessionId = sessionId;
                    diagnosticsManager.HandleSessionUpdated(current);
                    diagnosticsManager.RequestShowResults(current);
                    presentExportOnce(current);
                }, results_menu_settle_delay);
                return;
            }

            if (attempt < results_ready_retry_count)
            {
                Scheduler.AddDelayed(
                    () => requestShowResultsWhenReady(sessionId, attempt + 1),
                    results_ready_retry_delay);
                return;
            }

            Logger.Log(
                $"mosu diagnostics main menu was not ready; displaying completed session {sessionId} using the fallback.",
                LoggingTarget.Runtime,
                LogLevel.Important);
            displayedResultsSessionId = sessionId;
            diagnosticsManager.RequestShowResults(session);
            presentExportOnce(session);
        }

        private void presentExportOnce(MosuDiagnosticsSession session)
        {
            if (session.ExportFolderPresented || string.IsNullOrEmpty(session.ExportZipPath))
                return;

            if (!diagnosticsManager.PresentExport(session))
                return;

            session.ExportFolderPresented = true;
            MosuDiagnosticsSessionStore.Save(storage, session);
            diagnosticsManager.HandleSessionUpdated(session);
        }

        public static string GetRendererBenchmarkId(MosuDiagnosticsSession session)
            => $"{session.SessionId}-{session.ExpectedRenderer}-{session.ExpectedProfile}";

        public static IEnumerable<string> BuildLaunchArgs(MosuDiagnosticsSession session, bool includeBenchmark)
        {
            yield return LAUNCH_ARG;

            MosuDiagnosticsConfigurationSnapshot profile = resolveProfile(session);
            yield return $"{ATLAS_SIZE_ARG}={(profile.LargeTextureAtlas ? 4096 : 1024)}";

            if (!includeBenchmark)
                yield break;

            string benchmarkId = GetRendererBenchmarkId(session);

            yield return "--mosu-replay-benchmark";
            yield return $"--mosu-replay-benchmark-id={benchmarkId}";
            yield return $"--mosu-replay-benchmark-beatmap={MosuDiagnosticsDefaults.BeatmapOnlineId}";
            yield return $"--mosu-replay-benchmark-score={MosuDiagnosticsDefaults.ScoreHash}";
            yield return $"--mosu-replay-benchmark-replay-file={MosuDiagnosticsDefaults.ReplayFilename}";
            yield return $"--mosu-replay-benchmark-warmups={MosuDiagnosticsDefaults.WarmupRuns}";
            yield return $"--mosu-replay-benchmark-runs={MosuDiagnosticsDefaults.MeasuredRuns}";
            yield return $"--mosu-replay-benchmark-mode={MosuDiagnosticsDefaults.BenchmarkMode}";
            yield return "--mosu-replay-benchmark-start-delay-ms=1000";
            yield return "--mosu-replay-benchmark-between-runs-ms=750";
            yield return "--mosu-replay-benchmark-no-exit";
            yield return "--mosu-replay-benchmark-logging=true";
            yield return "--mosu-replay-benchmark-uncapped=true";
            yield return "--mosu-replay-benchmark-frame-sync=Unlimited";

            if (session.Options.UseDefaultSkin)
                yield return "--mosu-replay-benchmark-skin=argon";
            else
                yield return "--mosu-replay-benchmark-skin=inherit";

            yield return boolArg("--mosu-replay-benchmark-windows-ultra", profile.WindowsUltraPerformanceMode);
            yield return boolArg("--mosu-replay-benchmark-skin-performance", profile.SkinPerformanceMode);
            yield return boolArg("--mosu-replay-benchmark-skin-freeze-animations", profile.SkinPerformanceFreezeAnimations);
            yield return boolArg("--mosu-replay-benchmark-skin-simplify-effects", profile.SkinPerformanceSimplifyEffects);
            yield return boolArg("--mosu-replay-benchmark-skin-optimise-textures", profile.SkinPerformanceOptimiseTextures);
            yield return boolArg("--mosu-replay-benchmark-skin-simplify-hud", profile.SkinPerformanceSimplifyHud);
            yield return boolArg("--mosu-replay-benchmark-skin-simplify-counters", profile.SkinPerformanceSimplifyCounters);
            yield return boolArg("--mosu-replay-benchmark-skin-disable-kiai", profile.SkinPerformanceDisableKiaiFlashing);
            yield return boolArg("--mosu-replay-benchmark-skin-black-background", profile.SkinPerformanceBlackBackground);
            yield return boolArg("--mosu-replay-benchmark-argon-follow-ring", profile.ArgonFollowRing);
            yield return boolArg("--mosu-replay-benchmark-deferred-vertex-batching", profile.DeferredVertexUploadBatching);
            yield return boolArg("--mosu-replay-benchmark-deferred-direct-vertex-upload", profile.DeferredDirectVertexUpload);
            yield return boolArg("--mosu-replay-benchmark-deferred-direct-uniform-upload", profile.DeferredDirectUniformUpload);
            yield return boolArg("--mosu-replay-benchmark-veldrid-pipeline-cache", profile.VeldridPipelineLookupCache);
            yield return boolArg("--mosu-replay-benchmark-static-child-lifetime-cache", profile.StaticChildLifetimeCache);
            yield return boolArg("--mosu-replay-benchmark-use-8k", profile.Use8kPollingRate);
            yield return boolArg("--mosu-replay-benchmark-allow-tearing", profile.AllowTearing);
            yield return boolArg("--mosu-replay-benchmark-spin-wait", profile.UpdateThreadSpinWait);
        }

        public static IEnumerable<string> BuildRestoreLaunchArgs(MosuDiagnosticsSession session, bool continueDiagnostics)
        {
            if (continueDiagnostics)
                yield return LAUNCH_ARG;

            yield return $"{ATLAS_SIZE_ARG}={(session.CurrentConfiguration.LargeTextureAtlas ? 4096 : 1024)}";
        }

        private static MosuDiagnosticsConfigurationSnapshot resolveProfile(MosuDiagnosticsSession session)
            => MosuDiagnosticsProfiles.Resolve(
                session.ExpectedProfile ?? MosuDiagnosticsProfile.CleanBaseline,
                session.CurrentConfiguration);

        private static string boolArg(string name, bool value) => $"{name}={value.ToString().ToLowerInvariant()}";

        public static string QuoteArg(string arg)
        {
            if (!arg.Contains(' ') && !arg.Contains('"'))
                return arg;

            return '"' + arg.Replace("\"", "\\\"") + '"';
        }
    }
}
