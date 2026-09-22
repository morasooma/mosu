// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Desktop.Windows;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Performance;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Performance.Diagnostics;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Desktop.Performance
{
    internal partial class MosuReplayBenchmarkRunner : Component
    {
        public static event Action<string, string?>? BenchmarkCompleted;

        private const int default_beatmap_online_id = 4999492;
        private const double benchmark_replay_preparation_timeout_ms = 180000;
        private static readonly Guid classic_skin_id = new("81F02CD3-EEC6-4865-AC23-FAE26A386187");
        private static readonly Guid argon_skin_id = new("CFFA69DE-B3E3-4DEE-8563-3C4F425C05D0");

        private readonly string[] launchArgs;

        private BenchmarkOptions? options;
        private ScoreInfo? selectedScore;
        private string benchmarkId = string.Empty;
        private string? benchmarkFailureReason;
        private string replayHash = string.Empty;
        private int currentRun;
        private bool currentRunCompleted;
        private ScheduledDelegate? watchdogDelegate;
        private StreamWriter? manifestWriter;
        private ConfigSnapshot? previousConfig;
        private ThreadRateSnapshot? previousThreadRates;
        private bool waitingForWindowFocus;
        private bool benchmarkReplayPreparationStarted;
        private bool benchmarkReplayImportCompleted;
        private string? benchmarkReplayPreparationFailure;
        private DateTime benchmarkReplayPreparationDeadline;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        public MosuReplayBenchmarkRunner(string[]? launchArgs)
        {
            this.launchArgs = launchArgs ?? Array.Empty<string>();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            options = BenchmarkOptions.Parse(launchArgs);

            if (options == null)
                return;

            Logger.Log(
                $"mosu replay benchmark early skin state: {SkinPerformanceMode.DescribeEffectiveState()}",
                LoggingTarget.Runtime,
                LogLevel.Important);

            benchmarkId = string.IsNullOrWhiteSpace(options.BenchmarkId)
                ? $"{(isCarouselBenchmark ? "carousel" : "replay")}-{DateTime.Now:yyyyMMdd-HHmmss}"
                : options.BenchmarkId;

            GameplayPerformanceSnapshot.BenchmarkRunnerActive = true;
            captureBenchmarkExecutionMode();
            applyBenchmarkConfig();
            enableBenchmarkExecutionMode();
            applyBenchmarkSkin();
            Scheduler.AddDelayed(tryStart, options.StartDelayMs);
        }

        private void tryStart()
        {
            if (options == null)
                return;

            if (OperatingSystem.IsWindows()
                && options.WindowsUltraPerformanceMode == true
                && WindowsPerformanceMode.UltraRealtimeFallbackActive)
            {
                openManifest();
                failBenchmark("windows_ultra_realtime_watchdog_fallback");
                return;
            }

            if (game.ScreenStack?.CurrentScreen == null || game.ScreenStack.CurrentScreen is StartupScreen)
            {
                Scheduler.AddDelayed(tryStart, 500);
                return;
            }

            if (!ensureBenchmarkWindowActive(tryStart))
                return;

            synchroniseBenchmarkInactiveThreadRates();
            Logger.Log(
                $"mosu replay benchmark execution mode: window_active={host.IsActive.Value};"
                + $" draw_hz={host.DrawThread.ActiveHz:0.###}/{host.DrawThread.InactiveHz:0.###};"
                + $" update_hz={host.UpdateThread.ActiveHz:0.###}/{host.UpdateThread.InactiveHz:0.###};"
                + $" input_hz={host.InputThread.ActiveHz:0.###}/{host.InputThread.InactiveHz:0.###}",
                LoggingTarget.Runtime,
                LogLevel.Verbose);

            openManifest();

            if (isCarouselBenchmark)
            {
                tryStartCarouselBenchmark();
                return;
            }

            selectedScore = findBenchmarkScore();

            if (selectedScore == null)
            {
                waitForBundledBenchmarkReplay();
                return;
            }

            replayHash = string.IsNullOrEmpty(selectedScore.Hash) ? selectedScore.ID.ToString() : selectedScore.Hash;
            double replayDuration = getReplayDuration(selectedScore);

            Logger.Log(
                $"mosu replay benchmark starting: id={benchmarkId}, score={selectedScore}, replay={replayHash}, duration={replayDuration:0.###}ms, runs={options.MeasuredRuns}, warmups={options.WarmupRuns}, config={describeConfig()}",
                LoggingTarget.Runtime,
                LogLevel.Verbose);

            writeManifest("start", 0, false, $"score_id={selectedScore.ID};score_hash={selectedScore.Hash};score={selectedScore};beatmap={selectedScore.BeatmapInfo};hash={replayHash};duration_ms={replayDuration:0.###};config={describeConfig()};renderer={describeRenderer()}");
            logRendererPath();

            game.ScreenStack.ScreenPushed += screenPushed;
            startNextRun();
        }

        private ScoreInfo? findBenchmarkScore()
        {
            if (options == null)
                return null;

            List<ScoreInfo> candidates;

            if (!string.IsNullOrWhiteSpace(options.ScoreLookup))
            {
                if (Guid.TryParse(options.ScoreLookup, out Guid scoreId))
                    candidates = queryBenchmarkScores(s => !s.DeletePending && s.ID == scoreId, 1);
                else
                    candidates = queryBenchmarkScores(s => !s.DeletePending && s.Hash == options.ScoreLookup, 1);

                ScoreInfo? replayScore = firstScoreWithLocalReplay(candidates);
                if (replayScore == null)
                    return null;

                if (options.BeatmapOnlineId > 0 && replayScore.BeatmapInfo?.OnlineID != options.BeatmapOnlineId)
                {
                    benchmarkReplayPreparationFailure =
                        $"benchmark_replay_beatmap_mismatch:expected={options.BeatmapOnlineId};actual={replayScore.BeatmapInfo?.OnlineID ?? 0}";
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(options.BeatmapHash)
                    && !string.Equals(replayScore.BeatmapHash, options.BeatmapHash, StringComparison.OrdinalIgnoreCase))
                {
                    benchmarkReplayPreparationFailure =
                        $"benchmark_replay_beatmap_hash_mismatch:expected={options.BeatmapHash};actual={replayScore.BeatmapHash}";
                    return null;
                }

                return replayScore;
            }

            if (!string.IsNullOrWhiteSpace(options.BeatmapHash))
            {
                string beatmapHash = options.BeatmapHash;

                candidates = queryBenchmarkScores(
                    s => !s.DeletePending && s.BeatmapHash == beatmapHash,
                    200);

                ScoreInfo? replayScore = firstScoreWithLocalReplay(candidates);
                if (replayScore != null)
                    return replayScore;
            }

            if (options.BeatmapOnlineId > 0)
            {
                int failedRank = (int)ScoreRank.F;
                int beatmapOnlineId = options.BeatmapOnlineId;

                candidates = queryBenchmarkScores(
                    s => !s.DeletePending
                         && s.Ruleset.OnlineID == 0
                         && s.RankInt != failedRank
                         && s.BeatmapInfo != null
                         && s.BeatmapInfo.OnlineID == beatmapOnlineId,
                    200);

                ScoreInfo? replayScore = firstScoreWithLocalReplay(candidates);
                if (replayScore != null)
                    return replayScore;
            }

            return null;
        }

        private void waitForBundledBenchmarkReplay()
        {
            if (options == null)
                return;

            if (!string.IsNullOrEmpty(benchmarkReplayPreparationFailure))
            {
                failBenchmark(benchmarkReplayPreparationFailure);
                return;
            }

            if (!benchmarkReplayPreparationStarted)
            {
                string replayPath = resolveBenchmarkReplayPath(options.ReplayFilePath);

                if (!File.Exists(replayPath))
                {
                    failBenchmark($"benchmark_replay_file_missing:{replayPath}");
                    return;
                }

                benchmarkReplayPreparationStarted = true;
                benchmarkReplayPreparationDeadline = DateTime.UtcNow.AddMilliseconds(benchmark_replay_preparation_timeout_ms);

                Logger.Log(
                    $"mosu replay benchmark importing bundled replay '{replayPath}' and waiting for beatmap {options.BeatmapOnlineId}.",
                    LoggingTarget.Runtime,
                    LogLevel.Important);
                writeManifest("replay_prepare", 0, false, $"path={replayPath};beatmap_id={options.BeatmapOnlineId};score={options.ScoreLookup}");

                Task importTask = scoreManager.Import(replayPath);
                _ = importTask.ContinueWith(
                    task => Schedule(() =>
                    {
                        benchmarkReplayImportCompleted = true;

                        if (task.IsFaulted)
                            benchmarkReplayPreparationFailure = $"benchmark_replay_import_failed:{task.Exception?.GetBaseException().Message}";
                        else if (task.IsCanceled)
                            benchmarkReplayPreparationFailure = "benchmark_replay_import_cancelled";
                        else
                        {
                            Logger.Log(
                                "mosu replay benchmark bundled replay import pass completed; waiting for the exact replay and its beatmap to become locally available.",
                                LoggingTarget.Runtime,
                                LogLevel.Verbose);
                        }
                    }),
                    TaskScheduler.Default);
            }

            if (DateTime.UtcNow >= benchmarkReplayPreparationDeadline)
            {
                string stage = benchmarkReplayImportCompleted ? "beatmap_download_or_reimport" : "initial_replay_import";
                failBenchmark($"benchmark_replay_preparation_timeout:{stage}");
                return;
            }

            Scheduler.AddDelayed(tryStart, 500);
        }

        private static string resolveBenchmarkReplayPath(string? configuredPath)
        {
            string path = string.IsNullOrWhiteSpace(configuredPath)
                ? MosuDiagnosticsDefaults.ReplayFilename
                : configuredPath;

            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppContext.BaseDirectory, path));
        }

        private List<ScoreInfo> queryBenchmarkScores(Func<ScoreInfo, bool> predicate, int limit)
        {
            return realm.Run(r => r.All<ScoreInfo>()
                                   .AsEnumerable()
                                   .Where(predicate)
                                   .OrderByDescending(s => s.Date)
                                   .Take(limit)
                                   .Detach()
                                   .ToList());
        }

        private ScoreInfo? firstScoreWithLocalReplay(IEnumerable<ScoreInfo> candidates)
        {
            foreach (var candidate in candidates)
            {
                try
                {
                    Score? score = scoreManager.GetScore(candidate);

                    if (score?.Replay?.Frames.Count > 0 && isReplayDurationAllowed(score))
                        return candidate;
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"mosu replay benchmark skipped unreadable score {candidate}.");
                }
            }

            return null;
        }

        private bool isReplayDurationAllowed(Score score)
        {
            double duration = getReplayDuration(score);

            if (options?.MinReplayDurationMs != null && duration < options.MinReplayDurationMs.Value)
                return false;

            if (options?.MaxReplayDurationMs != null && duration > options.MaxReplayDurationMs.Value)
                return false;

            return true;
        }

        private double getReplayDuration(ScoreInfo scoreInfo)
        {
            try
            {
                return scoreManager.GetScore(scoreInfo) is { } score ? getReplayDuration(score) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static double getReplayDuration(Score score)
        {
            if (score.Replay?.Frames.Count > 0)
                return score.Replay.Frames[^1].Time;

            return score.ScoreInfo.BeatmapInfo?.Length ?? 0;
        }

        private void startNextRun()
        {
            if (options == null || selectedScore == null)
                return;

            if (!ensureBenchmarkWindowActive(startNextRun))
                return;

            currentRun++;

            if (currentRun > options.TotalRuns)
            {
                finishBenchmark();
                return;
            }

            synchroniseBenchmarkInactiveThreadRates();
            currentRunCompleted = false;
            bool warmup = currentRun <= options.WarmupRuns;

            GameplayPerformanceSnapshot.SetBenchmark(benchmarkId, options.BenchmarkMode ?? "replay", currentRun, warmup, replayHash);

            Logger.Log($"mosu replay benchmark run {currentRun}/{options.TotalRuns} starting (warmup={warmup}).", LoggingTarget.Runtime, LogLevel.Verbose);
            writeManifest("run_start", currentRun, warmup, string.Empty);

            game.PresentScore(selectedScore, ScorePresentType.Gameplay);

            watchdogDelegate?.Cancel();
            watchdogDelegate = Scheduler.AddDelayed(() => failBenchmark($"run_timeout:{currentRun}"), options.RunTimeoutMs);
        }

        private void screenPushed(IScreen? previous, IScreen next)
        {
            if (next is not Player player)
                return;

            int run = currentRun;

            player.OnGameplayStarted += () =>
            {
                if (run == currentRun)
                {
                    bool measuredGameplay = run > (options?.WarmupRuns ?? 0);
                    MosuDiagnosticsStutterCollector.SetMeasuredGameplayActive(measuredGameplay);
                    GameplayPerformanceSnapshot.SetBenchmarkGameplayActive(true);
                    writeManifest(
                        "gameplay_start",
                        run,
                        run <= (options?.WarmupRuns ?? 0),
                        $"effective_skin={SkinPerformanceMode.DescribeEffectiveState()}");
                }
            };

            player.OnShowingResults += () => Schedule(() => completeRun(run, "results"));
        }

        private void completeRun(int run, string reason)
        {
            if (options == null)
                return;

            if (run != currentRun || currentRunCompleted)
                return;

            currentRunCompleted = true;
            MosuDiagnosticsStutterCollector.SetMeasuredGameplayActive(false);
            GameplayPerformanceSnapshot.SetBenchmarkGameplayActive(false);
            watchdogDelegate?.Cancel();
            watchdogDelegate = null;

            bool warmup = run <= options.WarmupRuns;

            Logger.Log($"mosu replay benchmark run {run}/{options.TotalRuns} completed ({reason}, warmup={warmup}).", LoggingTarget.Runtime, LogLevel.Verbose);
            writeManifest("run_complete", run, warmup, reason);

            Scheduler.AddDelayed(startNextRun, options.BetweenRunsDelayMs);
        }

        private void failBenchmark(string reason)
        {
            if (options == null)
                return;

            benchmarkFailureReason = reason;
            MosuDiagnosticsStutterCollector.SetMeasuredGameplayActive(false);
            GameplayPerformanceSnapshot.SetBenchmarkGameplayActive(false);
            Logger.Log($"mosu replay benchmark failed: id={benchmarkId}, reason={reason}.", LoggingTarget.Runtime, LogLevel.Verbose);
            writeManifest("run_failed", currentRun, currentRun <= options.WarmupRuns, reason);
            finishBenchmark();
        }

        private void finishBenchmark()
        {
            if (options == null)
                return;

            GameplayPerformanceSnapshot.SetBenchmarkGameplayActive(false);
            MosuDiagnosticsStutterCollector.SetMeasuredGameplayActive(false);
            Logger.Log($"mosu replay benchmark completed: id={benchmarkId}.", LoggingTarget.Runtime, LogLevel.Verbose);
            writeManifest("complete", 0, false, string.Empty);

            game.ScreenStack.ScreenPushed -= screenPushed;
            cleanupCarouselBenchmark();
            GameplayPerformanceSnapshot.ClearBenchmark();

            manifestWriter?.Flush();
            manifestWriter?.Dispose();
            manifestWriter = null;

            restoreBenchmarkExecutionMode();
            restoreBenchmarkConfig();

            BenchmarkCompleted?.Invoke(benchmarkId, benchmarkFailureReason);

            if (options.ExitAfterCompletion)
                Scheduler.AddDelayed(game.Exit, options.ExitDelayMs);
        }

        private bool ensureBenchmarkWindowActive(Action retry)
        {
            if (host.IsActive.Value)
            {
                if (waitingForWindowFocus)
                    Logger.Log("mosu replay benchmark window focus acquired.", LoggingTarget.Runtime, LogLevel.Verbose);

                waitingForWindowFocus = false;
                return true;
            }

            if (!waitingForWindowFocus)
            {
                waitingForWindowFocus = true;
                Logger.Log("mosu replay benchmark waiting for window focus.", LoggingTarget.Runtime, LogLevel.Important);
            }

            host.Window?.Show();
            host.Window?.Raise();
            Scheduler.AddDelayed(retry, 250);
            return false;
        }

        private void captureBenchmarkExecutionMode()
        {
            previousThreadRates ??= new ThreadRateSnapshot(
                host.DrawThread.InactiveHz,
                host.UpdateThread.InactiveHz,
                host.InputThread.InactiveHz,
                false);
        }

        private void enableBenchmarkExecutionMode()
        {
            captureBenchmarkExecutionMode();
            synchroniseBenchmarkInactiveThreadRates();
        }

        private void synchroniseBenchmarkInactiveThreadRates()
        {
            if (previousThreadRates == null)
                return;

            host.DrawThread.InactiveHz = host.DrawThread.ActiveHz;
            host.UpdateThread.InactiveHz = host.UpdateThread.ActiveHz;
            host.InputThread.InactiveHz = host.InputThread.ActiveHz;
        }

        private void restoreBenchmarkExecutionMode()
        {
            if (previousThreadRates == null)
                return;

            var previous = previousThreadRates.Value;
            previousThreadRates = null;

            host.DrawThread.InactiveHz = previous.DrawInactiveHz;
            host.UpdateThread.InactiveHz = previous.UpdateInactiveHz;
            host.InputThread.InactiveHz = previous.InputInactiveHz;
            GameplayPerformanceSnapshot.BenchmarkRunnerActive = false;
        }

        private void applyBenchmarkConfig()
        {
            if (options == null)
                return;

            void applyStep(string name, Action apply)
            {
                // These markers are deliberately flushed. If a platform/config callback blocks,
                // the exported runtime log must retain the last entered step rather than losing it
                // in the logger buffer when the user has to terminate the stuck process.
                Logger.Log($"mosu replay benchmark config step begin: {name}.", LoggingTarget.Runtime, LogLevel.Verbose);
                Logger.Flush();
                apply();
                Logger.Log($"mosu replay benchmark config step end: {name}.", LoggingTarget.Runtime, LogLevel.Verbose);
                Logger.Flush();
            }

            bool diagnosticsMode = string.Equals(options.BenchmarkMode, MosuDiagnosticsDefaults.BenchmarkMode, StringComparison.Ordinal);

            previousConfig ??= new ConfigSnapshot(
                config.Get<bool>(OsuSetting.AutomaticallyDownloadMissingBeatmaps),
                config.Get<bool>(OsuSetting.ForkPerformanceLogging),
                config.Get<bool>(OsuSetting.ForkUncappedFrameRate),
                config.Get<bool>(OsuSetting.ForkWindowsUltraPerformanceMode),
                config.Get<bool>(OsuSetting.ForkLargeTextureAtlas),
                config.Get<bool>(OsuSetting.ForkSkinPerformanceMode),
                config.Get<bool>(OsuSetting.ForkSkinPerformanceFreezeAnimations),
                config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyEffects),
                config.Get<bool>(OsuSetting.ForkSkinPerformanceOptimiseTextures),
                config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyHud),
                config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyCounters),
                config.Get<bool>(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing),
                config.Get<bool>(OsuSetting.ForkSkinPerformanceBlackBackground),
                config.Get<bool>(OsuSetting.ForkArgonFollowRing),
                config.Get<bool>(OsuSetting.ForkDeferredVertexUploadBatching),
                config.Get<bool>(OsuSetting.ForkDeferredDirectVertexUpload),
                config.Get<bool>(OsuSetting.ForkDeferredDirectUniformUpload),
                config.Get<bool>(OsuSetting.ForkVeldridPipelineLookupCache),
                config.Get<bool>(OsuSetting.ForkStaticChildLifetimeCache),
                config.Get<bool>(OsuSetting.ForkUse8kPollingRate),
                config.Get<bool>(OsuSetting.ForkSongSelectCarouselPerformanceMode),
                config.Get<string>(OsuSetting.Skin),
                frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync),
                false,
                false);

            if (diagnosticsMode)
                applyStep("auto_download_missing_beatmaps", () => config.SetValue(OsuSetting.AutomaticallyDownloadMissingBeatmaps, true));

            if (options.EnablePerformanceLogging || diagnosticsMode)
                applyStep("performance_logging", () => config.SetValue(OsuSetting.ForkPerformanceLogging, true));

            if (diagnosticsMode)
            {
                applyStep("uncapped_frame_rate", () => config.SetValue(OsuSetting.ForkUncappedFrameRate, true));
                applyStep("frame_sync", () => frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Unlimited));
            }
            else if (options.UncappedFrameRate != null)
            {
                applyStep("uncapped_frame_rate", () => config.SetValue(OsuSetting.ForkUncappedFrameRate, options.UncappedFrameRate.Value));
            }

            if (options.WindowsUltraPerformanceMode != null)
                applyStep("windows_ultra_performance_mode", () => config.SetValue(OsuSetting.ForkWindowsUltraPerformanceMode, options.WindowsUltraPerformanceMode.Value));

            if (options.LargeTextureAtlas != null)
                applyStep("large_texture_atlas", () => config.SetValue(OsuSetting.ForkLargeTextureAtlas, options.LargeTextureAtlas.Value));

            if (options.SkinPerformanceMode != null)
                applyStep("skin_performance_mode", () => config.SetValue(OsuSetting.ForkSkinPerformanceMode, options.SkinPerformanceMode.Value));

            if (options.SkinPerformanceFreezeAnimations != null)
                applyStep("skin_freeze_animations", () => config.SetValue(OsuSetting.ForkSkinPerformanceFreezeAnimations, options.SkinPerformanceFreezeAnimations.Value));

            if (options.SkinPerformanceSimplifyEffects != null)
                applyStep("skin_simplify_effects", () => config.SetValue(OsuSetting.ForkSkinPerformanceSimplifyEffects, options.SkinPerformanceSimplifyEffects.Value));

            if (options.SkinPerformanceOptimiseTextures != null)
                applyStep("skin_optimise_textures", () => config.SetValue(OsuSetting.ForkSkinPerformanceOptimiseTextures, options.SkinPerformanceOptimiseTextures.Value));

            if (options.SkinPerformanceSimplifyHud != null)
                applyStep("skin_simplify_hud", () => config.SetValue(OsuSetting.ForkSkinPerformanceSimplifyHud, options.SkinPerformanceSimplifyHud.Value));

            if (options.SkinPerformanceSimplifyCounters != null)
                applyStep("skin_simplify_counters", () => config.SetValue(OsuSetting.ForkSkinPerformanceSimplifyCounters, options.SkinPerformanceSimplifyCounters.Value));

            if (options.SkinPerformanceDisableKiaiFlashing != null)
                applyStep("skin_disable_kiai_flashing", () => config.SetValue(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing, options.SkinPerformanceDisableKiaiFlashing.Value));

            if (options.SkinPerformanceBlackBackground != null)
                applyStep("skin_black_background", () => config.SetValue(OsuSetting.ForkSkinPerformanceBlackBackground, options.SkinPerformanceBlackBackground.Value));

            if (options.ArgonFollowRing != null)
                applyStep("argon_follow_ring", () => config.SetValue(OsuSetting.ForkArgonFollowRing, options.ArgonFollowRing.Value));

            if (options.DeferredVertexUploadBatching != null)
                applyStep("deferred_vertex_upload_batching", () => config.SetValue(OsuSetting.ForkDeferredVertexUploadBatching, options.DeferredVertexUploadBatching.Value));

            if (options.DeferredDirectVertexUpload != null)
                applyStep("deferred_direct_vertex_upload", () => config.SetValue(OsuSetting.ForkDeferredDirectVertexUpload, options.DeferredDirectVertexUpload.Value));

            if (options.DeferredDirectUniformUpload != null)
                applyStep("deferred_direct_uniform_upload", () => config.SetValue(OsuSetting.ForkDeferredDirectUniformUpload, options.DeferredDirectUniformUpload.Value));

            if (options.VeldridPipelineLookupCache != null)
                applyStep("veldrid_pipeline_lookup_cache", () => config.SetValue(OsuSetting.ForkVeldridPipelineLookupCache, options.VeldridPipelineLookupCache.Value));

            if (options.StaticChildLifetimeCache != null)
            {
                applyStep("static_child_lifetime_cache", () =>
                {
                    config.SetValue(OsuSetting.ForkStaticChildLifetimeCache, options.StaticChildLifetimeCache.Value);
                });
            }

            if (options.Use8kPollingRate != null)
            {
                applyStep("input_8k", () =>
                {
                    config.SetValue(OsuSetting.ForkUse8kPollingRate, options.Use8kPollingRate.Value);
                });
            }

            if (options.CarouselPerformanceMode != null)
                applyStep("carousel_performance_mode", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPerformanceMode, options.CarouselPerformanceMode.Value));

            if (!diagnosticsMode && options.FrameSync != null)
                applyStep("frame_sync", () => frameworkConfig.SetValue(FrameworkSetting.FrameSync, options.FrameSync.Value));
        }

        private void applyBenchmarkSkin()
        {
            if (options?.ForcedSkinId == null)
                return;

            Guid skinId = options.ForcedSkinId.Value;
            config.SetValue(OsuSetting.Skin, skinId.ToString());

            var liveSkin = realm.Run(r => r.Find<SkinInfo>(skinId)?.ToLive(realm));
            if (liveSkin != null)
                skinManager.CurrentSkinInfo.Value = liveSkin;
        }

        private void restoreBenchmarkConfig()
        {
            if (previousConfig == null)
            {
                bool hadBenchmarkOverride = SkinPerformanceMode.HasBenchmarkOverride;
                SkinPerformanceMode.ClearBenchmarkOverride();

                if (hadBenchmarkOverride)
                    skinManager.ReloadCurrentSkin();

                return;
            }

            var previous = previousConfig.Value;
            previousConfig = null;

            // Always stop the active logger first so its summary/segments files are complete before
            // BenchmarkCompleted is raised. Merely restoring a previous `true` value would otherwise
            // leave the writer open until process exit and diagnostics would observe missing metrics.
            config.SetValue(OsuSetting.ForkPerformanceLogging, false);
            config.SetValue(OsuSetting.AutomaticallyDownloadMissingBeatmaps, previous.AutomaticallyDownloadMissingBeatmaps);
            config.SetValue(OsuSetting.ForkUncappedFrameRate, previous.UncappedFrameRate);
            config.SetValue(OsuSetting.ForkWindowsUltraPerformanceMode, previous.WindowsUltraPerformanceMode);
            config.SetValue(OsuSetting.ForkLargeTextureAtlas, previous.LargeTextureAtlas);
            config.SetValue(OsuSetting.ForkSkinPerformanceMode, previous.SkinPerformanceMode);
            config.SetValue(OsuSetting.ForkSkinPerformanceFreezeAnimations, previous.SkinPerformanceFreezeAnimations);
            config.SetValue(OsuSetting.ForkSkinPerformanceSimplifyEffects, previous.SkinPerformanceSimplifyEffects);
            config.SetValue(OsuSetting.ForkSkinPerformanceOptimiseTextures, previous.SkinPerformanceOptimiseTextures);
            config.SetValue(OsuSetting.ForkSkinPerformanceSimplifyHud, previous.SkinPerformanceSimplifyHud);
            config.SetValue(OsuSetting.ForkSkinPerformanceSimplifyCounters, previous.SkinPerformanceSimplifyCounters);
            config.SetValue(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing, previous.SkinPerformanceDisableKiaiFlashing);
            config.SetValue(OsuSetting.ForkSkinPerformanceBlackBackground, previous.SkinPerformanceBlackBackground);
            config.SetValue(OsuSetting.ForkArgonFollowRing, previous.ArgonFollowRing);
            config.SetValue(OsuSetting.ForkDeferredVertexUploadBatching, previous.DeferredVertexUploadBatching);
            config.SetValue(OsuSetting.ForkDeferredDirectVertexUpload, previous.DeferredDirectVertexUpload);
            config.SetValue(OsuSetting.ForkDeferredDirectUniformUpload, previous.DeferredDirectUniformUpload);
            config.SetValue(OsuSetting.ForkVeldridPipelineLookupCache, previous.VeldridPipelineLookupCache);
            config.SetValue(OsuSetting.ForkStaticChildLifetimeCache, previous.StaticChildLifetimeCache);
            config.SetValue(OsuSetting.ForkUse8kPollingRate, previous.Use8kPollingRate);
            config.SetValue(OsuSetting.ForkSongSelectCarouselPerformanceMode, previous.CarouselPerformanceMode);
            config.SetValue(OsuSetting.Skin, previous.Skin);
            frameworkConfig.SetValue(FrameworkSetting.FrameSync, previous.FrameSync);

            if (Guid.TryParse(previous.Skin, out Guid previousSkinId))
            {
                var liveSkin = realm.Run(r => r.Find<SkinInfo>(previousSkinId)?.ToLive(realm));
                if (liveSkin != null)
                    skinManager.CurrentSkinInfo.Value = liveSkin;
            }

            SkinPerformanceMode.ClearBenchmarkOverride();
            skinManager.ReloadCurrentSkin();
            config.SetValue(OsuSetting.ForkPerformanceLogging, previous.PerformanceLogging);
        }

        private string describeConfig()
            => $"logging={config.Get<bool>(OsuSetting.ForkPerformanceLogging)}"
               + $";uncapped={config.Get<bool>(OsuSetting.ForkUncappedFrameRate)}"
               + $";win_ultra={config.Get<bool>(OsuSetting.ForkWindowsUltraPerformanceMode)}"
               + $";large_atlas={config.Get<bool>(OsuSetting.ForkLargeTextureAtlas)}"
               + $";skin_perf={config.Get<bool>(OsuSetting.ForkSkinPerformanceMode)}"
               + $";skin_freeze_animations={config.Get<bool>(OsuSetting.ForkSkinPerformanceFreezeAnimations)}"
               + $";skin_simplify_effects={config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyEffects)}"
               + $";skin_optimise_textures={config.Get<bool>(OsuSetting.ForkSkinPerformanceOptimiseTextures)}"
               + $";skin_simplify_hud={config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyHud)}"
               + $";skin_simplify_counters={config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyCounters)}"
               + $";skin_disable_kiai={config.Get<bool>(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing)}"
               + $";skin_black_background={config.Get<bool>(OsuSetting.ForkSkinPerformanceBlackBackground)}"
               + $";argon_follow_ring={config.Get<bool>(OsuSetting.ForkArgonFollowRing)}"
               + $";deferred_vertex_batching={config.Get<bool>(OsuSetting.ForkDeferredVertexUploadBatching)}"
               + $";deferred_direct_vertex_upload={config.Get<bool>(OsuSetting.ForkDeferredDirectVertexUpload)}"
               + $";deferred_direct_uniform_upload={config.Get<bool>(OsuSetting.ForkDeferredDirectUniformUpload)}"
               + $";veldrid_pipeline_lookup_cache={config.Get<bool>(OsuSetting.ForkVeldridPipelineLookupCache)}"
               + $";static_child_lifetime_cache={config.Get<bool>(OsuSetting.ForkStaticChildLifetimeCache)}"
               + $";input_8k={config.Get<bool>(OsuSetting.ForkUse8kPollingRate)}"
               + $";carousel_perf={config.Get<bool>(OsuSetting.ForkSongSelectCarouselPerformanceMode)}"
               + $";carousel_previews={config.Get<bool>(OsuSetting.ForkSongSelectCarouselPreviews)}"
               + $";carousel_preview_resolution={config.Get<int>(OsuSetting.ForkSongSelectCarouselPreviewResolution)}"
               + $";allow_tearing=false"
               + $";spin_wait=false"
               + $";skin={config.Get<string>(OsuSetting.Skin)}"
               + $";frame_sync={frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync)}"
               + $";effective_skin={SkinPerformanceMode.DescribeEffectiveState()}";

        private string describeRenderer()
            => $"setting={frameworkConfig.Get<RendererType>(FrameworkSetting.Renderer)}"
               + $";resolved={host.ResolvedRenderer}"
               + $";backend={host.Renderer.GetType().Name}";

        private void logRendererPath()
        {
            var resolved = host.ResolvedRenderer;
            var setting = frameworkConfig.Get<RendererType>(FrameworkSetting.Renderer);

            Logger.Log($"mosu replay benchmark renderer path: setting={setting}, resolved={resolved}, backend={host.Renderer.GetType().Name}", LoggingTarget.Runtime, LogLevel.Important);

            if (OperatingSystem.IsWindows() && resolved != RendererType.Deferred_Direct3D11)
            {
                Logger.Log(
                    "mosu replay benchmark warning: expected Deferred_Direct3D11 on Windows for comparable FPS numbers. Check framework.ini Renderer=Automatic and osu-framework mosu renderer fallback.",
                    LoggingTarget.Runtime,
                    LogLevel.Important);
            }
        }

        private void openManifest()
        {
            if (manifestWriter != null)
                return;

            string directory = storage.GetFullPath("performance", true);
            Directory.CreateDirectory(directory);

            manifestWriter = new StreamWriter(Path.Combine(directory, $"mosu-replay-benchmark-{benchmarkId}.manifest.csv"));
            manifestWriter.WriteLine("local_time,benchmark_id,event,run,warmup,details");
        }

        private void writeManifest(string eventName, int run, bool warmup, string details)
        {
            if (manifestWriter == null)
                return;

            manifestWriter.Write(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
            manifestWriter.Write(',');
            manifestWriter.Write(benchmarkId);
            manifestWriter.Write(',');
            manifestWriter.Write(eventName);
            manifestWriter.Write(',');
            manifestWriter.Write(run);
            manifestWriter.Write(',');
            manifestWriter.Write(warmup);
            manifestWriter.Write(',');
            manifestWriter.Write(escape(details));
            manifestWriter.WriteLine();
            manifestWriter.Flush();
        }

        private static string escape(string value)
        {
            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
                return value;

            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        protected override void Dispose(bool isDisposing)
        {
            watchdogDelegate?.Cancel();

            if (game.ScreenStack != null)
                game.ScreenStack.ScreenPushed -= screenPushed;

            GameplayPerformanceSnapshot.ClearBenchmark();
            manifestWriter?.Dispose();
            cleanupCarouselBenchmark();
            restoreBenchmarkExecutionMode();
            restoreBenchmarkConfig();

            base.Dispose(isDisposing);
        }

        private readonly record struct ThreadRateSnapshot(
            double DrawInactiveHz,
            double UpdateInactiveHz,
            double InputInactiveHz,
            bool AllowBenchmarkInactiveExecution);

        private sealed record BenchmarkOptions(
            string? BenchmarkId,
            string? BenchmarkMode,
            int BeatmapOnlineId,
            string? BeatmapHash,
            string? ScoreLookup,
            string? ReplayFilePath,
            int WarmupRuns,
            int MeasuredRuns,
            double? MinReplayDurationMs,
            double? MaxReplayDurationMs,
            double StartDelayMs,
            double BetweenRunsDelayMs,
            double RunTimeoutMs,
            double ExitDelayMs,
            bool ExitAfterCompletion,
            bool EnablePerformanceLogging,
            bool? UncappedFrameRate,
            bool? WindowsUltraPerformanceMode,
            bool? LargeTextureAtlas,
            bool? SkinPerformanceMode,
            bool? SkinPerformanceFreezeAnimations,
            bool? SkinPerformanceSimplifyEffects,
            bool? SkinPerformanceOptimiseTextures,
            bool? SkinPerformanceSimplifyHud,
            bool? SkinPerformanceSimplifyCounters,
            bool? SkinPerformanceDisableKiaiFlashing,
            bool? SkinPerformanceBlackBackground,
            bool? ArgonFollowRing,
            bool? DeferredVertexUploadBatching,
            bool? DeferredDirectVertexUpload,
            bool? DeferredDirectUniformUpload,
            bool? VeldridPipelineLookupCache,
            bool? StaticChildLifetimeCache,
            bool? Use8kPollingRate,
            bool? CarouselPerformanceMode,
            bool? AllowTearing,
            bool? UpdateThreadSpinWait,
            Guid? ForcedSkinId,
            FrameSync? FrameSync,
            int CarouselBeatmapOnlineId,
            double CarouselDurationMs,
            double CarouselSettleMs,
            double CarouselKeyRepeatMs)
        {
            public int TotalRuns => WarmupRuns + MeasuredRuns;

            public static BenchmarkOptions? Parse(IReadOnlyList<string> args)
            {
                bool enabled = string.Equals(Environment.GetEnvironmentVariable("MOSU_REPLAY_BENCHMARK"), "1", StringComparison.Ordinal);

                string? benchmarkId = Environment.GetEnvironmentVariable("MOSU_REPLAY_BENCHMARK_ID");
                string? benchmarkMode = null;
                int beatmapOnlineId = default_beatmap_online_id;
                string? beatmapHash = null;
                string? scoreLookup = null;
                string? replayFilePath = null;
                int warmups = 1;
                int runs = 1;
                double? minReplayDurationMs = null;
                double? maxReplayDurationMs = null;
                double startDelay = 3000;
                double betweenRunsDelay = 2500;
                double runTimeout = 90000;
                double exitDelay = 1500;
                bool exitAfterCompletion = true;
                bool skipProfile = false;
                bool enablePerformanceLogging = true;
                bool? uncappedFrameRate = true;
                bool? windowsUltraPerformanceMode = null;
                bool? largeTextureAtlas = null;
                bool? skinPerformanceMode = null;
                bool? skinPerformanceFreezeAnimations = null;
                bool? skinPerformanceSimplifyEffects = null;
                bool? skinPerformanceOptimiseTextures = null;
                bool? skinPerformanceSimplifyHud = null;
                bool? skinPerformanceSimplifyCounters = null;
                bool? skinPerformanceDisableKiaiFlashing = null;
                bool? skinPerformanceBlackBackground = null;
                bool? argonFollowRing = null;
                bool? deferredVertexUploadBatching = null;
                bool? deferredDirectVertexUpload = null;
                bool? deferredDirectUniformUpload = null;
                bool? veldridPipelineLookupCache = null;
                bool? staticChildLifetimeCache = null;
                bool? use8kPollingRate = null;
                bool? carouselPerformanceMode = null;
                bool? allowTearing = null;
                bool? updateThreadSpinWait = null;
                Guid? forcedSkinId = null;
                FrameSync? frameSync = osu.Framework.Configuration.FrameSync.Unlimited;
                int carouselBeatmapOnlineId = 0;
                double carouselDurationMs = 10000;
                double carouselSettleMs = 1500;
                double carouselKeyRepeatMs = 80;

                foreach (string arg in args)
                {
                    string[] split = arg.Split('=', 2);
                    string key = split[0];
                    string value = split.Length > 1 ? split[1] : string.Empty;

                    switch (key)
                    {
                        case "--mosu-replay-benchmark":
                            enabled = true;
                            break;

                        case "--mosu-carousel-benchmark":
                            benchmarkMode = "carousel";
                            enabled = true;
                            break;

                        case "--mosu-carousel-benchmark-id":
                            benchmarkId = value;
                            benchmarkMode = "carousel";
                            enabled = true;
                            break;

                        case "--mosu-carousel-benchmark-beatmap":
                            if (int.TryParse(value, out int parsedCarouselBeatmapOnlineId))
                                carouselBeatmapOnlineId = Math.Max(0, parsedCarouselBeatmapOnlineId);

                            benchmarkMode = "carousel";
                            enabled = true;
                            break;

                        case "--mosu-carousel-benchmark-duration-ms":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedCarouselDuration))
                                carouselDurationMs = Math.Max(1000, parsedCarouselDuration);

                            benchmarkMode = "carousel";
                            enabled = true;
                            break;

                        case "--mosu-carousel-benchmark-settle-ms":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedCarouselSettle))
                                carouselSettleMs = Math.Max(0, parsedCarouselSettle);

                            benchmarkMode = "carousel";
                            enabled = true;
                            break;

                        case "--mosu-carousel-benchmark-key-repeat-ms":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedCarouselKeyRepeat))
                                carouselKeyRepeatMs = Math.Max(16, parsedCarouselKeyRepeat);

                            benchmarkMode = "carousel";
                            enabled = true;
                            break;

                        case "--mosu-carousel-benchmark-performance-mode":
                            if (tryParseBool(value, out bool parsedCarouselPerformanceMode))
                                carouselPerformanceMode = parsedCarouselPerformanceMode;

                            benchmarkMode = "carousel";
                            enabled = true;
                            break;

                        case "--mosu-carousel-benchmark-warmups":
                            if (int.TryParse(value, out int parsedCarouselWarmups))
                                warmups = Math.Max(0, parsedCarouselWarmups);

                            benchmarkMode = "carousel";
                            enabled = true;
                            break;

                        case "--mosu-carousel-benchmark-runs":
                            if (int.TryParse(value, out int parsedCarouselRuns))
                                runs = Math.Max(1, parsedCarouselRuns);

                            benchmarkMode = "carousel";
                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-id":
                            benchmarkId = value;
                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-mode":
                            benchmarkMode = value;
                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-beatmap":
                            if (int.TryParse(value, out int parsedBeatmapOnlineId))
                            {
                                beatmapOnlineId = parsedBeatmapOnlineId;
                                enabled = true;
                            }

                            break;

                        case "--mosu-replay-benchmark-score":
                            scoreLookup = value;
                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-beatmap-hash":
                            beatmapHash = value.Trim();
                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-replay-file":
                            replayFilePath = value;
                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-warmups":
                            if (int.TryParse(value, out int parsedWarmups))
                                warmups = Math.Max(0, parsedWarmups);

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-runs":
                            if (int.TryParse(value, out int parsedRuns))
                                runs = Math.Max(1, parsedRuns);

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-min-duration-ms":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedMinDuration))
                                minReplayDurationMs = Math.Max(0, parsedMinDuration);

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-max-duration-ms":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedMaxDuration))
                                maxReplayDurationMs = Math.Max(0, parsedMaxDuration);

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-start-delay-ms":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedStartDelay))
                                startDelay = Math.Max(0, parsedStartDelay);

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-between-runs-ms":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedBetweenRunsDelay))
                                betweenRunsDelay = Math.Max(0, parsedBetweenRunsDelay);

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-timeout-ms":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedTimeout))
                                runTimeout = Math.Max(10000, parsedTimeout);

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-exit-delay-ms":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedExitDelay))
                                exitDelay = Math.Max(0, parsedExitDelay);

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-no-exit":
                            exitAfterCompletion = false;
                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-no-config":
                            skipProfile = true;
                            enablePerformanceLogging = false;
                            uncappedFrameRate = null;
                            windowsUltraPerformanceMode = null;
                            largeTextureAtlas = null;
                            skinPerformanceMode = null;
                            skinPerformanceFreezeAnimations = null;
                            skinPerformanceSimplifyEffects = null;
                            skinPerformanceOptimiseTextures = null;
                            skinPerformanceSimplifyHud = null;
                            skinPerformanceSimplifyCounters = null;
                            skinPerformanceDisableKiaiFlashing = null;
                            skinPerformanceBlackBackground = null;
                            argonFollowRing = null;
                            deferredVertexUploadBatching = null;
                            deferredDirectVertexUpload = null;
                            deferredDirectUniformUpload = null;
                            veldridPipelineLookupCache = null;
                            staticChildLifetimeCache = null;
                            use8kPollingRate = null;
                            carouselPerformanceMode = null;
                            allowTearing = null;
                            updateThreadSpinWait = null;
                            forcedSkinId = null;
                            frameSync = null;
                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-use-8k":
                            if (tryParseBool(value, out bool parsedUse8k))
                                use8kPollingRate = parsedUse8k;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-allow-tearing":
                            if (tryParseBool(value, out bool parsedAllowTearing))
                                allowTearing = parsedAllowTearing;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-spin-wait":
                            if (tryParseBool(value, out bool parsedSpinWait))
                                updateThreadSpinWait = parsedSpinWait;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-skin":
                            forcedSkinId = parseBenchmarkSkinId(value);
                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-logging":
                            if (tryParseBool(value, out bool parsedLogging))
                                enablePerformanceLogging = parsedLogging;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-uncapped":
                            if (tryParseBool(value, out bool parsedUncapped))
                                uncappedFrameRate = parsedUncapped;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-windows-ultra":
                            if (tryParseBool(value, out bool parsedWindowsUltra))
                                windowsUltraPerformanceMode = parsedWindowsUltra;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-skin-performance":
                            if (tryParseBool(value, out bool parsedSkinPerformance))
                                skinPerformanceMode = parsedSkinPerformance;

                            enabled = true;
                            break;

                        case "--mosu-diagnostics-atlas-size":
                            if (int.TryParse(value, out int parsedAtlasSize) && parsedAtlasSize is 1024 or 4096)
                                largeTextureAtlas = parsedAtlasSize == 4096;

                            break;

                        case "--mosu-replay-benchmark-skin-freeze-animations":
                            if (tryParseBool(value, out bool parsedSkinFreezeAnimations))
                                skinPerformanceFreezeAnimations = parsedSkinFreezeAnimations;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-skin-simplify-effects":
                            if (tryParseBool(value, out bool parsedSkinSimplifyEffects))
                                skinPerformanceSimplifyEffects = parsedSkinSimplifyEffects;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-skin-optimise-textures":
                            if (tryParseBool(value, out bool parsedSkinOptimiseTextures))
                                skinPerformanceOptimiseTextures = parsedSkinOptimiseTextures;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-skin-simplify-hud":
                            if (tryParseBool(value, out bool parsedSkinSimplifyHud))
                                skinPerformanceSimplifyHud = parsedSkinSimplifyHud;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-skin-simplify-counters":
                            if (tryParseBool(value, out bool parsedSkinSimplifyCounters))
                                skinPerformanceSimplifyCounters = parsedSkinSimplifyCounters;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-skin-disable-kiai":
                            if (tryParseBool(value, out bool parsedSkinDisableKiai))
                                skinPerformanceDisableKiaiFlashing = parsedSkinDisableKiai;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-skin-black-background":
                            if (tryParseBool(value, out bool parsedSkinBlackBackground))
                                skinPerformanceBlackBackground = parsedSkinBlackBackground;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-argon-follow-ring":
                            if (tryParseBool(value, out bool parsedArgonFollowRing))
                                argonFollowRing = parsedArgonFollowRing;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-deferred-vertex-batching":
                            if (tryParseBool(value, out bool parsedDeferredVertexBatching))
                                deferredVertexUploadBatching = parsedDeferredVertexBatching;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-deferred-direct-vertex-upload":
                            if (tryParseBool(value, out bool parsedDeferredDirectVertexUpload))
                                deferredDirectVertexUpload = parsedDeferredDirectVertexUpload;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-deferred-direct-uniform-upload":
                            if (tryParseBool(value, out bool parsedDeferredDirectUniformUpload))
                                deferredDirectUniformUpload = parsedDeferredDirectUniformUpload;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-veldrid-pipeline-cache":
                            if (tryParseBool(value, out bool parsedVeldridPipelineLookupCache))
                                veldridPipelineLookupCache = parsedVeldridPipelineLookupCache;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-static-child-lifetime-cache":
                            if (tryParseBool(value, out bool parsedStaticChildLifetimeCache))
                                staticChildLifetimeCache = parsedStaticChildLifetimeCache;

                            enabled = true;
                            break;

                        case "--mosu-replay-benchmark-frame-sync":
                            if (Enum.TryParse(value, ignoreCase: true, out FrameSync parsedFrameSync))
                                frameSync = parsedFrameSync;

                            enabled = true;
                            break;
                    }
                }

                if (!enabled)
                    return null;

                if (!skipProfile && forcedSkinId == null && !string.Equals(benchmarkMode, MosuDiagnosticsDefaults.BenchmarkMode, StringComparison.Ordinal))
                    forcedSkinId = argon_skin_id;

                if (string.Equals(benchmarkMode, MosuDiagnosticsDefaults.BenchmarkMode, StringComparison.Ordinal))
                {
                    enablePerformanceLogging = true;
                    uncappedFrameRate = true;
                    frameSync = osu.Framework.Configuration.FrameSync.Unlimited;
                }

                return new BenchmarkOptions(
                    benchmarkId,
                    benchmarkMode,
                    beatmapOnlineId,
                    beatmapHash,
                    scoreLookup,
                    replayFilePath,
                    warmups,
                    runs,
                    minReplayDurationMs,
                    maxReplayDurationMs,
                    startDelay,
                    betweenRunsDelay,
                    runTimeout,
                    exitDelay,
                    exitAfterCompletion,
                    enablePerformanceLogging,
                    uncappedFrameRate,
                    windowsUltraPerformanceMode,
                    largeTextureAtlas,
                    skinPerformanceMode,
                    skinPerformanceFreezeAnimations,
                    skinPerformanceSimplifyEffects,
                    skinPerformanceOptimiseTextures,
                    skinPerformanceSimplifyHud,
                    skinPerformanceSimplifyCounters,
                    skinPerformanceDisableKiaiFlashing,
                    skinPerformanceBlackBackground,
                    argonFollowRing,
                    deferredVertexUploadBatching,
                    deferredDirectVertexUpload,
                    deferredDirectUniformUpload,
                    veldridPipelineLookupCache,
                    staticChildLifetimeCache,
                    use8kPollingRate,
                    carouselPerformanceMode,
                    allowTearing,
                    updateThreadSpinWait,
                    forcedSkinId,
                    frameSync,
                    carouselBeatmapOnlineId,
                    carouselDurationMs,
                    carouselSettleMs,
                    carouselKeyRepeatMs);
            }

            private static Guid? parseBenchmarkSkinId(string value)
            {
                if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "inherit", StringComparison.OrdinalIgnoreCase))
                    return null;

                if (Guid.TryParse(value, out Guid parsedGuid))
                    return parsedGuid;

                switch (value.Trim().ToLowerInvariant())
                {
                    case "classic":
                    case "legacy":
                        return classic_skin_id;

                    case "argon":
                    case "default":
                        return argon_skin_id;

                    default:
                        Logger.Log($"mosu replay benchmark ignored unknown skin '{value}', using argon.", LoggingTarget.Runtime, LogLevel.Verbose);
                        return argon_skin_id;
                }
            }

            private static bool tryParseBool(string value, out bool parsed)
            {
                if (bool.TryParse(value, out parsed))
                    return true;

                if (int.TryParse(value, out int numeric))
                {
                    parsed = numeric != 0;
                    return true;
                }

                return false;
            }
        }

        private readonly record struct ConfigSnapshot(
            bool AutomaticallyDownloadMissingBeatmaps,
            bool PerformanceLogging,
            bool UncappedFrameRate,
            bool WindowsUltraPerformanceMode,
            bool LargeTextureAtlas,
            bool SkinPerformanceMode,
            bool SkinPerformanceFreezeAnimations,
            bool SkinPerformanceSimplifyEffects,
            bool SkinPerformanceOptimiseTextures,
            bool SkinPerformanceSimplifyHud,
            bool SkinPerformanceSimplifyCounters,
            bool SkinPerformanceDisableKiaiFlashing,
            bool SkinPerformanceBlackBackground,
            bool ArgonFollowRing,
            bool DeferredVertexUploadBatching,
            bool DeferredDirectVertexUpload,
            bool DeferredDirectUniformUpload,
            bool VeldridPipelineLookupCache,
            bool StaticChildLifetimeCache,
            bool Use8kPollingRate,
            bool CarouselPerformanceMode,
            string Skin,
            FrameSync FrameSync,
            bool AllowTearing,
            bool UpdateThreadSpinWait);
    }
}
