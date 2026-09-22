// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Input;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Performance;
using osu.Game.Performance.Diagnostics;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using SixLabors.ImageSharp;

namespace osu.Desktop.Performance
{
    internal partial class MosuPerformanceLogger : Component
    {
        private const double sample_interval_ms = 100;
        private const double flush_interval_ms = 1000;

        private readonly BindableBool enabled = new BindableBool();

        private IBindable<LocalUserPlayingState> localUserPlaying = null!;
        private IBindable<WorkingBeatmap> beatmap = null!;
        private Bindable<bool> windowsUltraPerformanceMode = null!;
        private Bindable<bool> uncappedFrameRate = null!;
        private Bindable<FrameSync> frameSync = null!;
        private Bindable<ExecutionMode> executionMode = null!;
        private Bindable<RendererType> renderer = null!;
        private Bindable<WindowMode> windowMode = null!;
        private SkinManager skinManager = null!;

        private StreamWriter? writer;
        private string? currentPath;
        private readonly List<SampleSummary> samples = new List<SampleSummary>();
        private readonly HashSet<Guid> auditedSkinIds = new HashSet<Guid>();
        private readonly HashSet<string> auditedSceneKeys = new HashSet<string>();
        private bool trackPerfSources;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private TimeSpan lastCpuTime;
        private double lastCpuSampleMs;
        private double nextSampleMs;
        private double nextFlushMs;
        private double maxDrawMs;
        private double maxUpdateMs;
        private double maxInputMs;
        private PerformanceBreakdownMax maxDrawBreakdown;
        private PerformanceBreakdownMax maxUpdateBreakdown;
        private PerformanceBreakdownMax maxInputBreakdown;
        private long lastAllocatedBytes;
        private int lastGen0Collections;
        private int lastGen1Collections;
        private int lastGen2Collections;
        private GlobalStatistic<int>[] flushSourceStatistics = Array.Empty<GlobalStatistic<int>>();
        private int[] lastFlushSourceValues = Array.Empty<int>();

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, FrameworkConfigManager frameworkConfig, ILocalUserPlayInfo localUserInfo, IBindable<WorkingBeatmap> currentBeatmap, SkinManager skinManager)
        {
            localUserPlaying = localUserInfo.PlayingState.GetBoundCopy();
            beatmap = currentBeatmap.GetBoundCopy();
            this.skinManager = skinManager;
            windowsUltraPerformanceMode = config.GetBindable<bool>(OsuSetting.ForkWindowsUltraPerformanceMode);
            uncappedFrameRate = config.GetBindable<bool>(OsuSetting.ForkUncappedFrameRate);
            frameSync = frameworkConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync);
            executionMode = frameworkConfig.GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode);
            renderer = frameworkConfig.GetBindable<RendererType>(FrameworkSetting.Renderer);
            windowMode = frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode);

            config.BindWith(OsuSetting.ForkPerformanceLogging, enabled);
            enabled.BindValueChanged(e =>
            {
                Logger.Log($"mosu performance logging setting is {(e.NewValue ? "enabled" : "disabled")}; directory: {storage.GetFullPath("performance", true)}", LoggingTarget.Runtime, LogLevel.Verbose);
                setEnabled(e.NewValue);
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (writer == null)
                return;

            maxDrawMs = Math.Max(maxDrawMs, host.DrawThread.Clock.ElapsedFrameTime);
            maxUpdateMs = Math.Max(maxUpdateMs, host.UpdateThread.Clock.ElapsedFrameTime);
            maxInputMs = Math.Max(maxInputMs, host.InputThread.Clock.ElapsedFrameTime);
            // Standalone public build note: ThreadFramePerformanceSnapshot is an optional Mosu framework extension.

            double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            if (elapsedMs >= nextSampleMs)
            {
                writeSample(elapsedMs);

                while (nextSampleMs <= elapsedMs)
                    nextSampleMs += sample_interval_ms;
            }

            if (elapsedMs >= nextFlushMs)
            {
                writer.Flush();

                while (nextFlushMs <= elapsedMs)
                    nextFlushMs += flush_interval_ms;
            }
        }

        private void setEnabled(bool isEnabled)
        {
            if (isEnabled)
                startLogging();
            else
                stopLogging();
        }

        private void startLogging()
        {
            if (writer != null)
                return;

            string directory = storage.GetFullPath("performance", true);
            Directory.CreateDirectory(directory);

            currentPath = Path.Combine(directory, $"mosu-performance-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            writer = new StreamWriter(currentPath);
            samples.Clear();
            auditedSkinIds.Clear();
            auditedSceneKeys.Clear();
            gameplayStartElapsedMs = double.NaN;
            trackPerfSources = isPerfSourceLoggingEnabled();

            if (trackPerfSources)
            {
                // Standalone public build note: Source tracking is an optional Mosu framework extension.
            }

            stopwatch.Restart();
            nextSampleMs = 0;
            nextFlushMs = flush_interval_ms;
            maxDrawMs = maxUpdateMs = maxInputMs = 0;
            maxDrawBreakdown = maxUpdateBreakdown = maxInputBreakdown = default;

            using (var process = Process.GetCurrentProcess())
            {
                lastCpuTime = process.TotalProcessorTime;
            }

            lastCpuSampleMs = 0;
            lastAllocatedBytes = GC.GetTotalAllocatedBytes(false);
            lastGen0Collections = GC.CollectionCount(0);
            lastGen1Collections = GC.CollectionCount(1);
            lastGen2Collections = GC.CollectionCount(2);
            initialiseFlushSourceStatistics();
            // Standalone public build note: InputLatencyTelemetry is an optional Mosu framework extension.

            writer.WriteLine("local_time,elapsed_ms,playing_state,is_active,beatmap_id,beatmap_set_id,beatmap,skin_id,skin_name,score_combo,score_max_combo,score_miss_count,last_judgement,last_judgement_time_ms,health,draw_fps,draw_ms,draw_ms_max,draw_scheduler_ms_max,draw_work_ms_max,draw_sleep_ms_max,draw_gc_ms_max,draw_vbuf_binds_max,draw_vbuf_overflow_max,draw_texture_binds_max,draw_fbo_redraw_max,draw_calls_max,draw_shader_binds_max,draw_vertices_draw_max,draw_vertices_upl_max,draw_uniform_upl_max,draw_pixels_max,draw_batch_flushes_max,draw_pipeline_binds_max,draw_resource_set_binds_max,draw_resource_set_creates_max,draw_texture_upload_flushes_max,draw_texture_uploads_max,draw_deferred_event_process_us_max,draw_deferred_vertex_write_us_max,draw_deferred_vertex_commit_us_max,draw_deferred_vertex_draw_us_max,draw_deferred_vertex_map_count_max,draw_deferred_vertex_unmap_count_max,draw_deferred_vertex_bytes_max,draw_veldrid_swap_buffers_us_max,draw_veldrid_texture_upload_us_max,draw_pipeline_creates_max,draw_veldrid_vertex_set_us_max,draw_veldrid_vertex_update_us_max,draw_veldrid_vertex_draw_us_max,draw_veldrid_vertex_bytes_max,draw_flush_top1_source,draw_flush_top1_count,draw_flush_top2_source,draw_flush_top2_count,draw_flush_top3_source,draw_flush_top3_count,update_fps,update_ms,update_ms_max,update_scheduler_ms_max,update_work_ms_max,update_sleep_ms_max,update_gc_ms_max,update_invalidations_max,update_refreshes_max,update_draw_node_ctor_max,update_draw_node_appl_max,update_schedule_invk_max,update_input_queue_max,update_positional_iq_max,update_ccl_max,update_ccl_top1_source,update_ccl_top1_count,update_ccl_top2_source,update_ccl_top2_count,update_ccl_top3_source,update_ccl_top3_count,update_inval_top1_source,update_inval_top1_count,update_inval_top2_source,update_inval_top2_count,update_inval_top3_source,update_inval_top3_count,input_fps,input_ms,input_ms_max,input_scheduler_ms_max,input_work_ms_max,input_sleep_ms_max,input_gc_ms_max,input_events_collected,input_events_applied,input_frames_swapped,input_enqueue_to_collect_ms_max,input_collect_to_apply_ms_max,input_enqueue_to_apply_ms_max,input_apply_to_publish_ms_max,input_publish_to_swap_ms_max,input_enqueue_to_swap_ms_max,cpu_percent,working_set_mb,gc_heap_mb,gc_allocated_mb_delta,gc_total_allocated_mb,gc_gen0_delta,gc_gen1_delta,gc_gen2_delta,priority,windows_ultra_performance_mode,skin_performance_mode,uncapped_frame_rate,frame_sync,execution_mode,renderer_setting,resolved_renderer,window_mode,benchmark_id,benchmark_mode,benchmark_run,benchmark_warmup,benchmark_replay_hash,benchmark_gameplay_active");
            writeCurrentSkinAudit();
            Logger.Log($"mosu performance logging started: {currentPath}", LoggingTarget.Runtime, LogLevel.Verbose);

            // Record which side of an A/B this run is, so a result can never be attributed to the wrong variant.
            Logger.Log(
                $"mosu optimisation toggles: {MosuOptimisationToggles.Describe()}",
                LoggingTarget.Runtime,
                MosuOptimisationToggles.AllDefault ? LogLevel.Verbose : LogLevel.Important);
        }

        private void stopLogging()
        {
            if (writer == null)
                return;

            string? finishedPath = currentPath;

            try
            {
                writer.Flush();
                writer.Dispose();
                writeSummary(finishedPath);
            }
            catch (Exception e)
            {
                Logger.Error(e, "mosu performance logging could not finish cleanly.");
            }
            finally
            {
                // Standalone public build note: InputLatencyTelemetry and source tracking are optional Mosu framework extensions.
                if (trackPerfSources)
                {
                    trackPerfSources = false;
                }

                writer = null;
                currentPath = null;
                stopwatch.Reset();
            }

            if (finishedPath != null)
                Logger.Log($"mosu performance logging stopped: {finishedPath}", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        private void writeSample(double elapsedMs)
        {
            if (writer == null)
                return;

            using var process = Process.GetCurrentProcess();

            process.Refresh();

            double cpuSampleMs = stopwatch.Elapsed.TotalMilliseconds;
            double intervalSeconds = Math.Max(0.001, (cpuSampleMs - lastCpuSampleMs) / 1000);
            TimeSpan cpuTime = process.TotalProcessorTime;
            double cpuPercent = (cpuTime - lastCpuTime).TotalSeconds / intervalSeconds / Environment.ProcessorCount * 100;

            lastCpuTime = cpuTime;
            lastCpuSampleMs = cpuSampleMs;

            var currentBeatmap = beatmap.Value.BeatmapInfo;
            DateTimeOffset localTime = DateTimeOffset.Now;
            double drawFps = host.DrawThread.Clock.FramesPerSecond;
            double drawMs = host.DrawThread.Clock.ElapsedFrameTime;
            double updateFps = host.UpdateThread.Clock.FramesPerSecond;
            double updateMs = host.UpdateThread.Clock.ElapsedFrameTime;
            double inputFps = host.InputThread.Clock.FramesPerSecond;
            double inputMs = host.InputThread.Clock.ElapsedFrameTime;
            var drawBreakdown = maxDrawBreakdown;
            var updateBreakdown = maxUpdateBreakdown;
            var inputBreakdown = maxInputBreakdown;
            InputLatencySnapshot inputLatency = default;
            var drawFlushSources = sampleFlushSources();
            double workingSetMb = process.WorkingSet64 / 1024d / 1024d;
            double gcHeapMb = GC.GetTotalMemory(false) / 1024d / 1024d;
            long allocatedBytes = GC.GetTotalAllocatedBytes(false);
            long allocatedBytesDelta = Math.Max(0, allocatedBytes - lastAllocatedBytes);
            double allocatedMbDelta = allocatedBytesDelta / 1024d / 1024d;
            double totalAllocatedMb = allocatedBytes / 1024d / 1024d;
            int gen0Collections = GC.CollectionCount(0);
            int gen1Collections = GC.CollectionCount(1);
            int gen2Collections = GC.CollectionCount(2);
            int gen0Delta = gen0Collections - lastGen0Collections;
            int gen1Delta = gen1Collections - lastGen1Collections;
            int gen2Delta = gen2Collections - lastGen2Collections;
            Guid skinId = skinManager.CurrentSkinInfo.Value.ID;
            string skinName = skinManager.CurrentSkin.Value.Name;
            string beatmapName = currentBeatmap.ToString();

            lastAllocatedBytes = allocatedBytes;
            lastGen0Collections = gen0Collections;
            lastGen1Collections = gen1Collections;
            lastGen2Collections = gen2Collections;
            writeCurrentSkinAudit();
            writeCurrentSceneAudit(elapsedMs);
            writePixelSourceSample(elapsedMs);

            writer.Write(localTime.ToString("O", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(format(elapsedMs));
            writer.Write(',');
            writer.Write(localUserPlaying.Value);
            writer.Write(',');
            writer.Write(host.IsActive.Value);
            writer.Write(',');
            writer.Write(currentBeatmap.OnlineID);
            writer.Write(',');
            writer.Write(currentBeatmap.BeatmapSet?.OnlineID ?? 0);
            writer.Write(',');
            writer.Write(escape(beatmapName));
            writer.Write(',');
            writer.Write(skinId);
            writer.Write(',');
            writer.Write(escape(skinName));
            writer.Write(',');
            writer.Write(GameplayPerformanceSnapshot.Combo);
            writer.Write(',');
            writer.Write(GameplayPerformanceSnapshot.MaxCombo);
            writer.Write(',');
            writer.Write(GameplayPerformanceSnapshot.MissCount);
            writer.Write(',');
            writer.Write(escape(GameplayPerformanceSnapshot.LastJudgement));
            writer.Write(',');
            writer.Write(format(GameplayPerformanceSnapshot.LastJudgementTime));
            writer.Write(',');
            writer.Write(format(GameplayPerformanceSnapshot.Health));
            writer.Write(',');
            writer.Write(format(drawFps));
            writer.Write(',');
            writer.Write(format(drawMs));
            writer.Write(',');
            writer.Write(format(maxDrawMs));
            writer.Write(',');
            writeBreakdown(writer, drawBreakdown);
            writer.Write(',');
            writeDrawCounters(writer, drawBreakdown);
            writer.Write(',');
            writeFlushSources(writer, drawFlushSources);
            writer.Write(',');
            writer.Write(format(updateFps));
            writer.Write(',');
            writer.Write(format(updateMs));
            writer.Write(',');
            writer.Write(format(maxUpdateMs));
            writer.Write(',');
            writeBreakdown(writer, updateBreakdown);
            writer.Write(',');
            writeUpdateCounters(writer, updateBreakdown);
            writer.Write(',');
            writeCclSources(writer, updateBreakdown);
            writer.Write(',');
            writeInvalSources(writer, updateBreakdown);
            writer.Write(',');
            writer.Write(format(inputFps));
            writer.Write(',');
            writer.Write(format(inputMs));
            writer.Write(',');
            writer.Write(format(maxInputMs));
            writer.Write(',');
            writeBreakdown(writer, inputBreakdown);
            writer.Write(',');
            writer.Write(inputLatency.CollectedEventCount);
            writer.Write(',');
            writer.Write(inputLatency.AppliedEventCount);
            writer.Write(',');
            writer.Write(inputLatency.SwappedInputFrameCount);
            writer.Write(',');
            writer.Write(format(inputLatency.EnqueueToCollectMillisecondsMax));
            writer.Write(',');
            writer.Write(format(inputLatency.CollectToApplyMillisecondsMax));
            writer.Write(',');
            writer.Write(format(inputLatency.EnqueueToApplyMillisecondsMax));
            writer.Write(',');
            writer.Write(format(inputLatency.ApplyToPublishMillisecondsMax));
            writer.Write(',');
            writer.Write(format(inputLatency.PublishToSwapMillisecondsMax));
            writer.Write(',');
            writer.Write(format(inputLatency.EnqueueToSwapMillisecondsMax));
            writer.Write(',');
            writer.Write(format(cpuPercent));
            writer.Write(',');
            writer.Write(format(workingSetMb));
            writer.Write(',');
            writer.Write(format(gcHeapMb));
            writer.Write(',');
            writer.Write(format(allocatedMbDelta));
            writer.Write(',');
            writer.Write(format(totalAllocatedMb));
            writer.Write(',');
            writer.Write(gen0Delta);
            writer.Write(',');
            writer.Write(gen1Delta);
            writer.Write(',');
            writer.Write(gen2Delta);
            writer.Write(',');
            writer.Write(process.PriorityClass);
            writer.Write(',');
            writer.Write(windowsUltraPerformanceMode.Value);
            writer.Write(',');
            writer.Write(SkinPerformanceMode.Enabled);
            writer.Write(',');
            writer.Write(uncappedFrameRate.Value);
            writer.Write(',');
            writer.Write(frameSync.Value);
            writer.Write(',');
            writer.Write(executionMode.Value);
            writer.Write(',');
            writer.Write(renderer.Value);
            writer.Write(',');
            writer.Write(host.ResolvedRenderer);
            writer.Write(',');
            writer.Write(windowMode.Value);
            writer.Write(',');
            writer.Write(escape(GameplayPerformanceSnapshot.BenchmarkId));
            writer.Write(',');
            writer.Write(escape(GameplayPerformanceSnapshot.BenchmarkMode));
            writer.Write(',');
            writer.Write(GameplayPerformanceSnapshot.BenchmarkRun);
            writer.Write(',');
            writer.Write(GameplayPerformanceSnapshot.BenchmarkWarmup);
            writer.Write(',');
            writer.Write(escape(GameplayPerformanceSnapshot.BenchmarkReplayHash));
            writer.Write(',');
            writer.Write(GameplayPerformanceSnapshot.BenchmarkGameplayActive);
            writer.WriteLine();

            recordDiagnosticsStutter(localTime, elapsedMs, maxDrawMs, maxUpdateMs, maxDrawBreakdown, maxUpdateBreakdown);

            samples.Add(new SampleSummary(
                localTime,
                elapsedMs,
                localUserPlaying.Value,
                host.IsActive.Value,
                currentBeatmap.OnlineID,
                currentBeatmap.BeatmapSet?.OnlineID ?? 0,
                beatmapName,
                skinId,
                skinName,
                windowsUltraPerformanceMode.Value,
                SkinPerformanceMode.Enabled,
                uncappedFrameRate.Value,
                frameSync.Value,
                renderer.Value,
                host.ResolvedRenderer,
                windowMode.Value,
                GameplayPerformanceSnapshot.BenchmarkId,
                GameplayPerformanceSnapshot.BenchmarkMode,
                GameplayPerformanceSnapshot.BenchmarkRun,
                GameplayPerformanceSnapshot.BenchmarkWarmup,
                GameplayPerformanceSnapshot.BenchmarkReplayHash,
                GameplayPerformanceSnapshot.BenchmarkGameplayActive,
                GameplayPerformanceSnapshot.MissCount,
                GameplayPerformanceSnapshot.Health,
                drawFps,
                maxDrawMs,
                drawBreakdown.SchedulerMs,
                drawBreakdown.WorkMs,
                drawBreakdown.SleepMs,
                drawBreakdown.GcMs,
                drawBreakdown.VBufBinds,
                drawBreakdown.VBufOverflow,
                drawBreakdown.TextureBinds,
                drawBreakdown.FBORedraw,
                drawBreakdown.DrawCalls,
                drawBreakdown.ShaderBinds,
                drawBreakdown.VerticesDraw,
                drawBreakdown.VerticesUpl,
                drawBreakdown.UniformUpl,
                drawBreakdown.Pixels,
                drawBreakdown.BatchFlushes,
                drawBreakdown.PipelineBinds,
                drawBreakdown.ResourceSetBinds,
                drawBreakdown.ResourceSetCreates,
                drawBreakdown.TextureUploadFlushes,
                drawBreakdown.TextureUploads,
                drawBreakdown.DeferredEventProcessUs,
                drawBreakdown.DeferredVertexWriteUs,
                drawBreakdown.DeferredVertexCommitUs,
                drawBreakdown.DeferredVertexDrawUs,
                drawBreakdown.DeferredVertexMapCount,
                drawBreakdown.DeferredVertexUnmapCount,
                drawBreakdown.DeferredVertexBytes,
                drawBreakdown.VeldridSwapBuffersUs,
                drawBreakdown.VeldridTextureUploadUs,
                drawBreakdown.PipelineCreates,
                drawBreakdown.VeldridVertexSetUs,
                drawBreakdown.VeldridVertexUpdateUs,
                drawBreakdown.VeldridVertexDrawUs,
                drawBreakdown.VeldridVertexBytes,
                updateFps,
                maxUpdateMs,
                updateBreakdown.SchedulerMs,
                updateBreakdown.WorkMs,
                updateBreakdown.SleepMs,
                updateBreakdown.GcMs,
                updateBreakdown.Invalidations,
                updateBreakdown.Refreshes,
                updateBreakdown.DrawNodeCtor,
                updateBreakdown.DrawNodeAppl,
                updateBreakdown.ScheduleInvk,
                updateBreakdown.InputQueue,
                updateBreakdown.PositionalIQ,
                updateBreakdown.CCL,
                inputFps,
                maxInputMs,
                inputBreakdown.SchedulerMs,
                inputBreakdown.WorkMs,
                inputBreakdown.SleepMs,
                inputBreakdown.GcMs,
                inputLatency.CollectedEventCount,
                inputLatency.AppliedEventCount,
                inputLatency.SwappedInputFrameCount,
                inputLatency.EnqueueToCollectMillisecondsMax,
                inputLatency.CollectToApplyMillisecondsMax,
                inputLatency.EnqueueToApplyMillisecondsMax,
                inputLatency.ApplyToPublishMillisecondsMax,
                inputLatency.PublishToSwapMillisecondsMax,
                inputLatency.EnqueueToSwapMillisecondsMax,
                cpuPercent,
                workingSetMb,
                gcHeapMb,
                allocatedMbDelta,
                gen0Delta,
                gen1Delta,
                gen2Delta));

            maxDrawMs = maxUpdateMs = maxInputMs = 0;
            maxDrawBreakdown = maxUpdateBreakdown = maxInputBreakdown = default;
        }

        private void writeSummary(string? finishedPath)
        {
            if (finishedPath == null || samples.Count == 0)
                return;

            string summaryPath = getBenchmarkArtifactPath(finishedPath, ".summary.csv");

            using var summary = new StreamWriter(summaryPath);
            summary.WriteLine("scope,metric,value");

            writeSummaryScope(summary, "all", samples);
            var gameplaySamples = samples.Where(isGameplaySample).ToArray();

            writeSummaryScope(summary, "gameplay", gameplaySamples);

            foreach (var skinSamples in gameplaySamples.GroupBy(s => new { s.SkinId, s.SkinName }).OrderBy(g => g.Key.SkinName))
                writeSummaryScope(summary, $"gameplay_skin:{skinSamples.Key.SkinId}:{skinSamples.Key.SkinName}", skinSamples.ToArray());

            foreach (var skinModeSamples in gameplaySamples.GroupBy(s => new { s.SkinId, s.SkinName, s.SkinPerformanceMode, s.WindowsUltraPerformanceMode }).OrderBy(g => g.Key.SkinName))
                writeSummaryScope(summary, $"gameplay_skin_mode:{skinModeSamples.Key.SkinId}:{skinModeSamples.Key.SkinName}:skin_perf={skinModeSamples.Key.SkinPerformanceMode}:win_ultra={skinModeSamples.Key.WindowsUltraPerformanceMode}", skinModeSamples.ToArray());

            writeSegmentSummary(finishedPath);
        }

        private string getBenchmarkArtifactPath(string finishedPath, string extension)
        {
            string? benchmarkId = samples.Select(s => s.BenchmarkId).FirstOrDefault(id => !string.IsNullOrEmpty(id));

            if (string.IsNullOrEmpty(benchmarkId))
                return Path.ChangeExtension(finishedPath, extension);

            string directory = Path.GetDirectoryName(finishedPath) ?? storage.GetFullPath("performance", true);
            return Path.Combine(directory, $"mosu-performance-{benchmarkId}{extension}");
        }

        private void writeSegmentSummary(string finishedPath)
        {
            string segmentPath = getBenchmarkArtifactPath(finishedPath, ".segments.csv");

            using var segmentWriter = new StreamWriter(segmentPath);
            segmentWriter.WriteLine("segment,benchmark_id,benchmark_mode,benchmark_run,benchmark_warmup,benchmark_replay_hash,start_local,end_local,duration_s,beatmap_id,beatmap_set_id,beatmap,skin_id,skin_name,windows_ultra_performance_mode,skin_performance_mode,uncapped_frame_rate,frame_sync,renderer_setting,resolved_renderer,window_mode,samples,active_ratio,misses,miss_counter_end,health_min,health_end,draw_fps_avg,draw_fps_p1,draw_fps_p5,draw_fps_median,draw_fps_min,draw_ms_max_avg,draw_ms_max_p95,draw_ms_max_p99,draw_ms_max_median,draw_ms_max_worst,draw_scheduler_ms_max_p99,draw_work_ms_max_p99,draw_sleep_ms_max_p99,draw_gc_ms_max_p99,draw_vbuf_binds_max_p99,draw_vbuf_overflow_max_p99,draw_texture_binds_max_p99,draw_fbo_redraw_max_p99,draw_calls_max_p99,draw_shader_binds_max_p99,draw_vertices_draw_max_p99,draw_vertices_upl_max_p99,draw_uniform_upl_max_p99,draw_pixels_max_p99,draw_batch_flushes_max_p99,draw_pipeline_binds_max_p99,draw_resource_set_binds_max_p99,draw_resource_set_creates_max_p99,draw_texture_upload_flushes_max_p99,draw_texture_uploads_max_p99,update_ms_max_avg,update_ms_max_p95,update_ms_max_p99,update_ms_max_median,update_ms_max_worst,update_scheduler_ms_max_p99,update_work_ms_max_p99,update_sleep_ms_max_p99,update_gc_ms_max_p99,update_invalidations_max_p99,update_refreshes_max_p99,update_draw_node_ctor_max_p99,update_draw_node_appl_max_p99,update_schedule_invk_max_p99,update_input_queue_max_p99,update_positional_iq_max_p99,update_ccl_max_p99,input_ms_max_p99,input_thread_frame_ms_max_p99,input_events_collected_avg,input_events_applied_avg,input_frames_swapped_avg,input_enqueue_to_collect_ms_max_p99,input_collect_to_apply_ms_max_p99,input_enqueue_to_apply_ms_max_p99,input_apply_to_publish_ms_max_p99,input_publish_to_swap_ms_max_p99,input_enqueue_to_swap_ms_max_p99,cpu_percent_avg,gc_allocated_mb_delta_avg");

            var currentSegment = new List<SampleSummary>();
            int segmentIndex = 0;

            foreach (var sample in samples)
            {
                if (!isGameplaySample(sample))
                {
                    flushSegment();
                    continue;
                }

                if (currentSegment.Count > 0 && startsNewSegment(currentSegment[^1], sample))
                    flushSegment();

                currentSegment.Add(sample);
            }

            flushSegment();

            bool startsNewSegment(SampleSummary previous, SampleSummary next)
                => previous.BeatmapId != next.BeatmapId
                   || previous.SkinId != next.SkinId
                   || previous.SkinPerformanceMode != next.SkinPerformanceMode
                   || previous.WindowsUltraPerformanceMode != next.WindowsUltraPerformanceMode
                   || previous.BenchmarkId != next.BenchmarkId
                   || previous.BenchmarkMode != next.BenchmarkMode
                   || previous.BenchmarkRun != next.BenchmarkRun
                   || previous.BenchmarkWarmup != next.BenchmarkWarmup
                   || previous.BenchmarkReplayHash != next.BenchmarkReplayHash
                   || next.MissCount < previous.MissCount;

            void flushSegment()
            {
                if (currentSegment.Count == 0)
                    return;

                segmentIndex++;
                writeSegmentLine(segmentWriter, segmentIndex, currentSegment);
                currentSegment.Clear();
            }
        }

        private static void writeSegmentLine(StreamWriter writer, int segmentIndex, IReadOnlyList<SampleSummary> segment)
        {
            var first = segment[0];
            var last = segment[^1];
            int minMissCount = segment.Min(s => s.MissCount);
            int maxMissCount = segment.Max(s => s.MissCount);

            writer.Write(segmentIndex);
            writer.Write(',');
            writer.Write(escape(first.BenchmarkId));
            writer.Write(',');
            writer.Write(escape(first.BenchmarkMode));
            writer.Write(',');
            writer.Write(first.BenchmarkRun);
            writer.Write(',');
            writer.Write(first.BenchmarkWarmup);
            writer.Write(',');
            writer.Write(escape(first.BenchmarkReplayHash));
            writer.Write(',');
            writer.Write(first.LocalTime.ToString("O", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(last.LocalTime.ToString("O", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(format((last.ElapsedMs - first.ElapsedMs) / 1000));
            writer.Write(',');
            writer.Write(first.BeatmapId);
            writer.Write(',');
            writer.Write(first.BeatmapSetId);
            writer.Write(',');
            writer.Write(escape(first.Beatmap));
            writer.Write(',');
            writer.Write(first.SkinId);
            writer.Write(',');
            writer.Write(escape(first.SkinName));
            writer.Write(',');
            writer.Write(first.WindowsUltraPerformanceMode);
            writer.Write(',');
            writer.Write(first.SkinPerformanceMode);
            writer.Write(',');
            writer.Write(first.UncappedFrameRate);
            writer.Write(',');
            writer.Write(first.FrameSync);
            writer.Write(',');
            writer.Write(first.Renderer);
            writer.Write(',');
            writer.Write(first.ResolvedRenderer);
            writer.Write(',');
            writer.Write(first.WindowMode);
            writer.Write(',');
            writer.Write(segment.Count);
            writer.Write(',');
            writer.Write(format(segment.Count(s => s.IsActive) / (double)segment.Count));
            writer.Write(',');
            writer.Write(maxMissCount - minMissCount);
            writer.Write(',');
            writer.Write(maxMissCount);
            writer.Write(',');
            writer.Write(format(segment.Min(s => s.Health)));
            writer.Write(',');
            writer.Write(format(last.Health));
            writer.Write(',');
            writeStats(writer, segment.Select(s => s.DrawFps), lowerPercentiles: true);
            writer.Write(',');
            writeStats(writer, segment.Select(s => s.DrawMsMax), lowerPercentiles: false);
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.DrawSchedulerMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.DrawWorkMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.DrawSleepMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.DrawGcMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawVBufBindsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawVBufOverflowMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawTextureBindsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawFBORedrawMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawCallsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawShaderBindsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawVerticesDrawMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawVerticesUplMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawUniformUplMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawPixelsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawBatchFlushesMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawPipelineBindsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawResourceSetBindsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawResourceSetCreatesMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawTextureUploadFlushesMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.DrawTextureUploadsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writeStats(writer, segment.Select(s => s.UpdateMsMax), lowerPercentiles: false);
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.UpdateSchedulerMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.UpdateWorkMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.UpdateSleepMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.UpdateGcMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.UpdateInvalidationsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.UpdateRefreshesMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.UpdateDrawNodeCtorMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.UpdateDrawNodeApplMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.UpdateScheduleInvkMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.UpdateInputQueueMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.UpdatePositionalIQMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => (double)s.UpdateCCLMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.InputMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.InputMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(segment.Average(s => s.InputEventsCollected)));
            writer.Write(',');
            writer.Write(format(segment.Average(s => s.InputEventsApplied)));
            writer.Write(',');
            writer.Write(format(segment.Average(s => s.InputFramesSwapped)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.InputEnqueueToCollectMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.InputCollectToApplyMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.InputEnqueueToApplyMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.InputApplyToPublishMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.InputPublishToSwapMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(segment.Select(s => s.InputEnqueueToSwapMsMax).OrderBy(v => v).ToArray(), 0.99)));
            writer.Write(',');
            writer.Write(format(segment.Average(s => s.CpuPercent)));
            writer.Write(',');
            writer.Write(format(segment.Average(s => s.GcAllocatedMbDelta)));
            writer.WriteLine();
        }

        private static void writeStats(StreamWriter writer, IEnumerable<double> values, bool lowerPercentiles)
        {
            double[] sortedValues = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToArray();

            if (sortedValues.Length == 0)
            {
                writer.Write("0,0,0,0,0");
                return;
            }

            writer.Write(format(sortedValues.Average()));
            writer.Write(',');
            writer.Write(format(percentile(sortedValues, lowerPercentiles ? 0.01 : 0.95)));
            writer.Write(',');
            writer.Write(format(percentile(sortedValues, lowerPercentiles ? 0.05 : 0.99)));
            writer.Write(',');
            writer.Write(format(percentile(sortedValues, 0.5)));
            writer.Write(',');
            writer.Write(format(lowerPercentiles ? sortedValues[0] : sortedValues[^1]));
        }

        private static void recordDiagnosticsStutter(
            DateTimeOffset localTime,
            double elapsedMs,
            double maxDrawMs,
            double maxUpdateMs,
            PerformanceBreakdownMax drawBreakdown,
            PerformanceBreakdownMax updateBreakdown)
        {
            if (!MosuDiagnosticsStutterCollector.IsCollecting)
                return;

            if (!string.Equals(GameplayPerformanceSnapshot.BenchmarkMode, MosuDiagnosticsDefaults.BenchmarkMode, StringComparison.Ordinal))
                return;

            if (!GameplayPerformanceSnapshot.BenchmarkGameplayActive)
                return;

            double frameMs = Math.Max(maxDrawMs, maxUpdateMs);

            if (frameMs < MosuDiagnosticsDefaults.StutterThresholdMs)
                return;

            MosuDiagnosticsStutterCollector.TryRecord(new MosuDiagnosticsStutterEvent
            {
                LocalTime = localTime.ToString("O", CultureInfo.InvariantCulture),
                ElapsedMs = elapsedMs,
                Thread = maxDrawMs >= maxUpdateMs ? "draw" : "update",
                FrameMs = frameMs,
                GcMs = Math.Max(drawBreakdown.GcMs, updateBreakdown.GcMs),
                Ccl = updateBreakdown.CCL,
                Invalidations = updateBreakdown.Invalidations,
            });
        }

        private static bool isGameplaySample(SampleSummary sample)
            => sample.PlayingState == LocalUserPlayingState.Playing
               || (!string.IsNullOrEmpty(sample.BenchmarkId) && sample.BenchmarkGameplayActive);

        private static void writeSummaryScope(StreamWriter summary, string scope, IReadOnlyCollection<SampleSummary> scopeSamples)
        {
            if (scopeSamples.Count == 0)
                return;

            appendSummary(summary, scope, "draw_fps", scopeSamples.Select(s => s.DrawFps));
            appendSummary(summary, scope, "draw_ms_max", scopeSamples.Select(s => s.DrawMsMax));
            appendSummary(summary, scope, "draw_scheduler_ms_max", scopeSamples.Select(s => s.DrawSchedulerMsMax));
            appendSummary(summary, scope, "draw_work_ms_max", scopeSamples.Select(s => s.DrawWorkMsMax));
            appendSummary(summary, scope, "draw_sleep_ms_max", scopeSamples.Select(s => s.DrawSleepMsMax));
            appendSummary(summary, scope, "draw_gc_ms_max", scopeSamples.Select(s => s.DrawGcMsMax));
            appendSummary(summary, scope, "draw_vbuf_binds_max", scopeSamples.Select(s => (double)s.DrawVBufBindsMax));
            appendSummary(summary, scope, "draw_vbuf_overflow_max", scopeSamples.Select(s => (double)s.DrawVBufOverflowMax));
            appendSummary(summary, scope, "draw_texture_binds_max", scopeSamples.Select(s => (double)s.DrawTextureBindsMax));
            appendSummary(summary, scope, "draw_fbo_redraw_max", scopeSamples.Select(s => (double)s.DrawFBORedrawMax));
            appendSummary(summary, scope, "draw_calls_max", scopeSamples.Select(s => (double)s.DrawCallsMax));
            appendSummary(summary, scope, "draw_shader_binds_max", scopeSamples.Select(s => (double)s.DrawShaderBindsMax));
            appendSummary(summary, scope, "draw_vertices_draw_max", scopeSamples.Select(s => (double)s.DrawVerticesDrawMax));
            appendSummary(summary, scope, "draw_vertices_upl_max", scopeSamples.Select(s => (double)s.DrawVerticesUplMax));
            appendSummary(summary, scope, "draw_uniform_upl_max", scopeSamples.Select(s => (double)s.DrawUniformUplMax));
            appendSummary(summary, scope, "draw_pixels_max", scopeSamples.Select(s => (double)s.DrawPixelsMax));
            appendSummary(summary, scope, "draw_batch_flushes_max", scopeSamples.Select(s => (double)s.DrawBatchFlushesMax));
            appendSummary(summary, scope, "draw_pipeline_binds_max", scopeSamples.Select(s => (double)s.DrawPipelineBindsMax));
            appendSummary(summary, scope, "draw_resource_set_binds_max", scopeSamples.Select(s => (double)s.DrawResourceSetBindsMax));
            appendSummary(summary, scope, "draw_resource_set_creates_max", scopeSamples.Select(s => (double)s.DrawResourceSetCreatesMax));
            appendSummary(summary, scope, "draw_texture_upload_flushes_max", scopeSamples.Select(s => (double)s.DrawTextureUploadFlushesMax));
            appendSummary(summary, scope, "draw_texture_uploads_max", scopeSamples.Select(s => (double)s.DrawTextureUploadsMax));
            appendSummary(summary, scope, "draw_deferred_event_process_us_max", scopeSamples.Select(s => (double)s.DrawDeferredEventProcessUsMax));
            appendSummary(summary, scope, "draw_deferred_vertex_write_us_max", scopeSamples.Select(s => (double)s.DrawDeferredVertexWriteUsMax));
            appendSummary(summary, scope, "draw_deferred_vertex_commit_us_max", scopeSamples.Select(s => (double)s.DrawDeferredVertexCommitUsMax));
            appendSummary(summary, scope, "draw_deferred_vertex_draw_us_max", scopeSamples.Select(s => (double)s.DrawDeferredVertexDrawUsMax));
            appendSummary(summary, scope, "draw_deferred_vertex_map_count_max", scopeSamples.Select(s => (double)s.DrawDeferredVertexMapCountMax));
            appendSummary(summary, scope, "draw_deferred_vertex_unmap_count_max", scopeSamples.Select(s => (double)s.DrawDeferredVertexUnmapCountMax));
            appendSummary(summary, scope, "draw_deferred_vertex_bytes_max", scopeSamples.Select(s => (double)s.DrawDeferredVertexBytesMax));
            appendSummary(summary, scope, "draw_veldrid_swap_buffers_us_max", scopeSamples.Select(s => (double)s.DrawVeldridSwapBuffersUsMax));
            appendSummary(summary, scope, "draw_veldrid_texture_upload_us_max", scopeSamples.Select(s => (double)s.DrawVeldridTextureUploadUsMax));
            appendSummary(summary, scope, "draw_pipeline_creates_max", scopeSamples.Select(s => (double)s.DrawPipelineCreatesMax));
            appendSummary(summary, scope, "draw_veldrid_vertex_set_us_max", scopeSamples.Select(s => (double)s.DrawVeldridVertexSetUsMax));
            appendSummary(summary, scope, "draw_veldrid_vertex_update_us_max", scopeSamples.Select(s => (double)s.DrawVeldridVertexUpdateUsMax));
            appendSummary(summary, scope, "draw_veldrid_vertex_draw_us_max", scopeSamples.Select(s => (double)s.DrawVeldridVertexDrawUsMax));
            appendSummary(summary, scope, "draw_veldrid_vertex_bytes_max", scopeSamples.Select(s => (double)s.DrawVeldridVertexBytesMax));
            appendSummary(summary, scope, "update_fps", scopeSamples.Select(s => s.UpdateFps));
            appendSummary(summary, scope, "update_ms_max", scopeSamples.Select(s => s.UpdateMsMax));
            appendSummary(summary, scope, "update_scheduler_ms_max", scopeSamples.Select(s => s.UpdateSchedulerMsMax));
            appendSummary(summary, scope, "update_work_ms_max", scopeSamples.Select(s => s.UpdateWorkMsMax));
            appendSummary(summary, scope, "update_sleep_ms_max", scopeSamples.Select(s => s.UpdateSleepMsMax));
            appendSummary(summary, scope, "update_gc_ms_max", scopeSamples.Select(s => s.UpdateGcMsMax));
            appendSummary(summary, scope, "update_invalidations_max", scopeSamples.Select(s => (double)s.UpdateInvalidationsMax));
            appendSummary(summary, scope, "update_refreshes_max", scopeSamples.Select(s => (double)s.UpdateRefreshesMax));
            appendSummary(summary, scope, "update_draw_node_ctor_max", scopeSamples.Select(s => (double)s.UpdateDrawNodeCtorMax));
            appendSummary(summary, scope, "update_draw_node_appl_max", scopeSamples.Select(s => (double)s.UpdateDrawNodeApplMax));
            appendSummary(summary, scope, "update_schedule_invk_max", scopeSamples.Select(s => (double)s.UpdateScheduleInvkMax));
            appendSummary(summary, scope, "update_input_queue_max", scopeSamples.Select(s => (double)s.UpdateInputQueueMax));
            appendSummary(summary, scope, "update_positional_iq_max", scopeSamples.Select(s => (double)s.UpdatePositionalIQMax));
            appendSummary(summary, scope, "update_ccl_max", scopeSamples.Select(s => (double)s.UpdateCCLMax));
            appendSummary(summary, scope, "input_fps", scopeSamples.Select(s => s.InputFps));
            appendSummary(summary, scope, "input_ms_max", scopeSamples.Select(s => s.InputMsMax));
            appendSummary(summary, scope, "input_thread_frame_ms_max", scopeSamples.Select(s => s.InputMsMax));
            appendSummary(summary, scope, "input_scheduler_ms_max", scopeSamples.Select(s => s.InputSchedulerMsMax));
            appendSummary(summary, scope, "input_work_ms_max", scopeSamples.Select(s => s.InputWorkMsMax));
            appendSummary(summary, scope, "input_sleep_ms_max", scopeSamples.Select(s => s.InputSleepMsMax));
            appendSummary(summary, scope, "input_gc_ms_max", scopeSamples.Select(s => s.InputGcMsMax));
            appendSummary(summary, scope, "input_events_collected", scopeSamples.Select(s => (double)s.InputEventsCollected));
            appendSummary(summary, scope, "input_events_applied", scopeSamples.Select(s => (double)s.InputEventsApplied));
            appendSummary(summary, scope, "input_frames_swapped", scopeSamples.Select(s => (double)s.InputFramesSwapped));
            appendSummary(summary, scope, "input_enqueue_to_collect_ms_max", scopeSamples.Select(s => s.InputEnqueueToCollectMsMax));
            appendSummary(summary, scope, "input_collect_to_apply_ms_max", scopeSamples.Select(s => s.InputCollectToApplyMsMax));
            appendSummary(summary, scope, "input_enqueue_to_apply_ms_max", scopeSamples.Select(s => s.InputEnqueueToApplyMsMax));
            appendSummary(summary, scope, "input_apply_to_publish_ms_max", scopeSamples.Select(s => s.InputApplyToPublishMsMax));
            appendSummary(summary, scope, "input_publish_to_swap_ms_max", scopeSamples.Select(s => s.InputPublishToSwapMsMax));
            appendSummary(summary, scope, "input_enqueue_to_swap_ms_max", scopeSamples.Select(s => s.InputEnqueueToSwapMsMax));
            appendSummary(summary, scope, "cpu_percent", scopeSamples.Select(s => s.CpuPercent));
            appendSummary(summary, scope, "working_set_mb", scopeSamples.Select(s => s.WorkingSetMb));
            appendSummary(summary, scope, "gc_heap_mb", scopeSamples.Select(s => s.GcHeapMb));
            appendSummary(summary, scope, "gc_allocated_mb_delta", scopeSamples.Select(s => s.GcAllocatedMbDelta));
            appendSummary(summary, scope, "gc_gen0_delta", scopeSamples.Select(s => (double)s.GcGen0Delta));
            appendSummary(summary, scope, "gc_gen1_delta", scopeSamples.Select(s => (double)s.GcGen1Delta));
            appendSummary(summary, scope, "gc_gen2_delta", scopeSamples.Select(s => (double)s.GcGen2Delta));
        }

        private void writeCurrentSkinAudit()
        {
            if (currentPath == null)
                return;

            Guid skinId = skinManager.CurrentSkinInfo.Value.ID;

            if (!auditedSkinIds.Add(skinId))
                return;

            writeSkinAudit(currentPath, skinId, skinManager.CurrentSkin.Value.Name);
        }

        private void writeSkinAudit(string performancePath, Guid skinId, string skinName)
        {
            string auditPath = Path.ChangeExtension(performancePath, ".skin.csv");
            var fileStorage = storage.GetStorageForDirectory("files");
            bool writeHeader = !File.Exists(auditPath) || new System.IO.FileInfo(auditPath).Length == 0;

            using var audit = new StreamWriter(auditPath, append: true);

            if (writeHeader)
                audit.WriteLine("skin_id,skin_name,filename,extension,file_size_bytes,width,height,pixels,scale_hint,storage_path");

            bool wroteImage = false;

            foreach (var file in getCurrentSkinFiles())
            {
                string extension = Path.GetExtension(file.Filename).ToLowerInvariant();

                if (!isImageExtension(extension))
                    continue;

                wroteImage = true;

                int width = 0;
                int height = 0;
                long fileSize = 0;
                string storagePath = file.StoragePath;

                try
                {
                    string fullPath = fileStorage.GetFullPath(storagePath);
                    var info = new System.IO.FileInfo(fullPath);

                    if (info.Exists)
                    {
                        fileSize = info.Length;

                        using var stream = File.OpenRead(fullPath);
                        var imageInfo = Image.Identify(stream);

                        if (imageInfo != null)
                        {
                            width = imageInfo.Width;
                            height = imageInfo.Height;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"mosu performance logging could not audit skin image {file.Filename}.");
                }

                writeSkinAuditRow(audit, skinId, skinName, file.Filename, extension, fileSize, width, height, file.Filename.Contains("@2x", StringComparison.OrdinalIgnoreCase) ? "2x" : "1x", storagePath);
            }

            if (!wroteImage)
                writeSkinAuditRow(audit, skinId, skinName, "<built-in or no imported image files>", string.Empty, 0, 0, 0, string.Empty, string.Empty);
        }

        private List<SkinFileAuditEntry> getCurrentSkinFiles()
            => skinManager.CurrentSkinInfo.Value.PerformRead(s => s.Files.Select(f => new SkinFileAuditEntry(f.Filename, f.File.GetStoragePath())).ToList());

        private static bool isImageExtension(string extension)
            => extension is ".png" or ".jpg" or ".jpeg";

        private static void writeSkinAuditRow(StreamWriter audit, Guid skinId, string skinName, string filename, string extension, long fileSize, int width, int height, string scaleHint, string storagePath)
        {
            audit.Write(skinId);
            audit.Write(',');
            audit.Write(escape(skinName));
            audit.Write(',');
            audit.Write(escape(filename));
            audit.Write(',');
            audit.Write(extension);
            audit.Write(',');
            audit.Write(fileSize);
            audit.Write(',');
            audit.Write(width);
            audit.Write(',');
            audit.Write(height);
            audit.Write(',');
            audit.Write((long)width * height);
            audit.Write(',');
            audit.Write(scaleHint);
            audit.Write(',');
            audit.Write(escape(storagePath));
            audit.WriteLine();
        }

        private static void writeBreakdown(StreamWriter writer, PerformanceBreakdownMax breakdown)
        {
            writer.Write(format(breakdown.SchedulerMs));
            writer.Write(',');
            writer.Write(format(breakdown.WorkMs));
            writer.Write(',');
            writer.Write(format(breakdown.SleepMs));
            writer.Write(',');
            writer.Write(format(breakdown.GcMs));
        }

        private static void writeDrawCounters(StreamWriter writer, PerformanceBreakdownMax breakdown)
        {
            writer.Write(breakdown.VBufBinds);
            writer.Write(',');
            writer.Write(breakdown.VBufOverflow);
            writer.Write(',');
            writer.Write(breakdown.TextureBinds);
            writer.Write(',');
            writer.Write(breakdown.FBORedraw);
            writer.Write(',');
            writer.Write(breakdown.DrawCalls);
            writer.Write(',');
            writer.Write(breakdown.ShaderBinds);
            writer.Write(',');
            writer.Write(breakdown.VerticesDraw);
            writer.Write(',');
            writer.Write(breakdown.VerticesUpl);
            writer.Write(',');
            writer.Write(breakdown.UniformUpl);
            writer.Write(',');
            writer.Write(breakdown.Pixels);
            writer.Write(',');
            writer.Write(breakdown.BatchFlushes);
            writer.Write(',');
            writer.Write(breakdown.PipelineBinds);
            writer.Write(',');
            writer.Write(breakdown.ResourceSetBinds);
            writer.Write(',');
            writer.Write(breakdown.ResourceSetCreates);
            writer.Write(',');
            writer.Write(breakdown.TextureUploadFlushes);
            writer.Write(',');
            writer.Write(breakdown.TextureUploads);
            writer.Write(',');
            writer.Write(breakdown.DeferredEventProcessUs);
            writer.Write(',');
            writer.Write(breakdown.DeferredVertexWriteUs);
            writer.Write(',');
            writer.Write(breakdown.DeferredVertexCommitUs);
            writer.Write(',');
            writer.Write(breakdown.DeferredVertexDrawUs);
            writer.Write(',');
            writer.Write(breakdown.DeferredVertexMapCount);
            writer.Write(',');
            writer.Write(breakdown.DeferredVertexUnmapCount);
            writer.Write(',');
            writer.Write(breakdown.DeferredVertexBytes);
            writer.Write(',');
            writer.Write(breakdown.VeldridSwapBuffersUs);
            writer.Write(',');
            writer.Write(breakdown.VeldridTextureUploadUs);
            writer.Write(',');
            writer.Write(breakdown.PipelineCreates);
            writer.Write(',');
            writer.Write(breakdown.VeldridVertexSetUs);
            writer.Write(',');
            writer.Write(breakdown.VeldridVertexUpdateUs);
            writer.Write(',');
            writer.Write(breakdown.VeldridVertexDrawUs);
            writer.Write(',');
            writer.Write(breakdown.VeldridVertexBytes);
        }

        private static void writeFlushSources(StreamWriter writer, FlushSourceSample flushSources)
        {
            writer.Write(escape(flushSources.Top1Source));
            writer.Write(',');
            writer.Write(flushSources.Top1Count);
            writer.Write(',');
            writer.Write(escape(flushSources.Top2Source));
            writer.Write(',');
            writer.Write(flushSources.Top2Count);
            writer.Write(',');
            writer.Write(escape(flushSources.Top3Source));
            writer.Write(',');
            writer.Write(flushSources.Top3Count);
        }

        private void initialiseFlushSourceStatistics()
        {
            var sources = Enum.GetValues<FlushBatchSource>();

            flushSourceStatistics = new GlobalStatistic<int>[sources.Length];
            lastFlushSourceValues = new int[sources.Length];

            foreach (FlushBatchSource source in sources)
            {
                var statistic = GlobalStatistics.Get<int>(nameof(FlushBatchSource), source.ToString());
                int index = (int)source;

                flushSourceStatistics[index] = statistic;
                lastFlushSourceValues[index] = statistic.Value;
            }
        }

        private FlushSourceSample sampleFlushSources()
        {
            string top1Source = string.Empty;
            int top1Count = 0;
            string top2Source = string.Empty;
            int top2Count = 0;
            string top3Source = string.Empty;
            int top3Count = 0;

            for (int i = 0; i < flushSourceStatistics.Length; i++)
            {
                var statistic = flushSourceStatistics[i];

                if (statistic == null)
                    continue;

                int currentValue = statistic.Value;
                int delta = Math.Max(0, currentValue - lastFlushSourceValues[i]);
                lastFlushSourceValues[i] = currentValue;

                if (delta <= 0)
                    continue;

                string name = ((FlushBatchSource)i).ToString();

                if (delta > top1Count)
                {
                    top3Source = top2Source;
                    top3Count = top2Count;
                    top2Source = top1Source;
                    top2Count = top1Count;
                    top1Source = name;
                    top1Count = delta;
                }
                else if (delta > top2Count)
                {
                    top3Source = top2Source;
                    top3Count = top2Count;
                    top2Source = name;
                    top2Count = delta;
                }
                else if (delta > top3Count)
                {
                    top3Source = name;
                    top3Count = delta;
                }
            }

            return new FlushSourceSample(top1Source, top1Count, top2Source, top2Count, top3Source, top3Count);
        }

        private static void writeUpdateCounters(StreamWriter writer, PerformanceBreakdownMax breakdown)
        {
            writer.Write(breakdown.Invalidations);
            writer.Write(',');
            writer.Write(breakdown.Refreshes);
            writer.Write(',');
            writer.Write(breakdown.DrawNodeCtor);
            writer.Write(',');
            writer.Write(breakdown.DrawNodeAppl);
            writer.Write(',');
            writer.Write(breakdown.ScheduleInvk);
            writer.Write(',');
            writer.Write(breakdown.InputQueue);
            writer.Write(',');
            writer.Write(breakdown.PositionalIQ);
            writer.Write(',');
            writer.Write(breakdown.CCL);
        }

        private static void writeCclSources(StreamWriter writer, PerformanceBreakdownMax breakdown)
        {
            writer.Write(escape(breakdown.CclTop1Source ?? string.Empty));
            writer.Write(',');
            writer.Write(breakdown.CclTop1Count);
            writer.Write(',');
            writer.Write(escape(breakdown.CclTop2Source ?? string.Empty));
            writer.Write(',');
            writer.Write(breakdown.CclTop2Count);
            writer.Write(',');
            writer.Write(escape(breakdown.CclTop3Source ?? string.Empty));
            writer.Write(',');
            writer.Write(breakdown.CclTop3Count);
        }

        private static void writeInvalSources(StreamWriter writer, PerformanceBreakdownMax breakdown)
        {
            writer.Write(escape(breakdown.InvalTop1Source ?? string.Empty));
            writer.Write(',');
            writer.Write(breakdown.InvalTop1Count);
            writer.Write(',');
            writer.Write(escape(breakdown.InvalTop2Source ?? string.Empty));
            writer.Write(',');
            writer.Write(breakdown.InvalTop2Count);
            writer.Write(',');
            writer.Write(escape(breakdown.InvalTop3Source ?? string.Empty));
            writer.Write(',');
            writer.Write(breakdown.InvalTop3Count);
        }

        private static bool isPerfSourceLoggingEnabled()
            => string.Equals(Environment.GetEnvironmentVariable("MOSU_PERF_SOURCE_LOGGING"), "1", StringComparison.Ordinal)
               || string.Equals(Environment.GetEnvironmentVariable("MOSU_CCL_SOURCE_LOGGING"), "1", StringComparison.Ordinal);

        private void writeCurrentSceneAudit(double elapsedMs)
        {
            if (currentPath == null || !trackPerfSources)
                return;

            bool gameplayActive = localUserPlaying.Value == LocalUserPlayingState.Playing
                                  || (!string.IsNullOrEmpty(GameplayPerformanceSnapshot.BenchmarkId) && GameplayPerformanceSnapshot.BenchmarkGameplayActive);

            if (!gameplayActive)
            {
                gameplayStartElapsedMs = double.NaN;
                return;
            }

            if (double.IsNaN(gameplayStartElapsedMs))
                gameplayStartElapsedMs = elapsedMs;

            // The audit used to fire on the very first gameplay sample, which lands before any hit object has
            // spawned -- so the snapshot contained the HUD and overlays but none of the hit-object subtree, i.e.
            // exactly the part the audit exists to attribute. Wait until gameplay is properly under way.
            if (elapsedMs - gameplayStartElapsedMs < sceneAuditDelayMs)
                return;

            string sceneKey = $"{GameplayPerformanceSnapshot.BenchmarkId}:{GameplayPerformanceSnapshot.BenchmarkRun}:{GameplayPerformanceSnapshot.BenchmarkWarmup}";

            if (!auditedSceneKeys.Add(sceneKey))
                return;

            writeSceneAudit(currentPath, elapsedMs, sceneKey);
        }

        /// <summary>
        /// How far into gameplay the one-shot scene audit is taken, so it captures a loaded playfield rather than
        /// the moment gameplay starts. Override with <c>MOSU_SCENE_AUDIT_DELAY_MS</c>.
        /// </summary>
        private static readonly double sceneAuditDelayMs = readSceneAuditDelayMs();

        private double gameplayStartElapsedMs = double.NaN;

        private static double readSceneAuditDelayMs()
        {
            const double default_delay_ms = 20000;

            string? value = Environment.GetEnvironmentVariable("MOSU_SCENE_AUDIT_DELAY_MS");

            if (!string.IsNullOrEmpty(value)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                && parsed >= 0)
            {
                return parsed;
            }

            return default_delay_ms;
        }

        /// <summary>
        /// Appends the per-type pixel attribution accumulated since the previous sample to a
        /// <c>.pixels.csv</c> sidecar. Answers "which drawable types consume the fill-rate budget" — the binding
        /// constraint on bandwidth-bound iGPUs (worklog R11). Kept out of the main CSV to preserve its format.
        /// </summary>
        private void writePixelSourceSample(double elapsedMs)
        {
            // Standalone public build note: PixelSourceStatistics is an optional Mosu framework extension.
        }

        private void writeSceneAudit(string performancePath, double elapsedMs, string sceneKey)
        {
            string auditPath = Path.ChangeExtension(performancePath, ".scene.csv");
            bool writeHeader = !File.Exists(auditPath) || new System.IO.FileInfo(auditPath).Length == 0;

            using var audit = new StreamWriter(auditPath, append: true);

            if (writeHeader)
                audit.WriteLine($"benchmark_id,benchmark_mode,benchmark_run,benchmark_warmup,benchmark_replay_hash,elapsed_ms,beatmap_id,skin_id,skin_name,{GameplaySceneAudit.CSV_COLUMNS}");

            var currentBeatmap = beatmap.Value.BeatmapInfo;
            Guid skinId = skinManager.CurrentSkinInfo.Value.ID;
            string skinName = skinManager.CurrentSkin.Value.Name;

            try
            {
                foreach (var entry in GameplaySceneAudit.CountDrawableTypes(game))
                {
                    audit.Write(escape(GameplayPerformanceSnapshot.BenchmarkId));
                    audit.Write(',');
                    audit.Write(escape(GameplayPerformanceSnapshot.BenchmarkMode));
                    audit.Write(',');
                    audit.Write(GameplayPerformanceSnapshot.BenchmarkRun);
                    audit.Write(',');
                    audit.Write(GameplayPerformanceSnapshot.BenchmarkWarmup);
                    audit.Write(',');
                    audit.Write(escape(GameplayPerformanceSnapshot.BenchmarkReplayHash));
                    audit.Write(',');
                    audit.Write(format(elapsedMs));
                    audit.Write(',');
                    audit.Write(currentBeatmap.OnlineID);
                    audit.Write(',');
                    audit.Write(skinId);
                    audit.Write(',');
                    audit.Write(escape(skinName));
                    audit.Write(',');
                    audit.Write(escape(entry.TypeName));
                    audit.Write(',');
                    audit.Write(entry.TotalCount);
                    audit.Write(',');
                    audit.Write(entry.AliveCount);
                    audit.Write(',');
                    audit.Write(entry.PresentCount);
                    audit.Write(',');
                    audit.Write(entry.MaskingCount);
                    audit.Write(',');
                    audit.Write(entry.AlwaysPresentInvisibleCount);
                    audit.WriteLine();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"mosu performance logging could not audit scene graph for {sceneKey}.");
            }
        }

        private static void appendSummary(StreamWriter summary, string scope, string name, IEnumerable<double> values)
        {
            double[] sortedValues = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToArray();

            if (sortedValues.Length == 0)
                return;

            writeSummaryLine(summary, scope, $"{name}_avg", sortedValues.Average());
            writeSummaryLine(summary, scope, $"{name}_min", sortedValues[0]);
            writeSummaryLine(summary, scope, $"{name}_p1", percentile(sortedValues, 0.01));
            writeSummaryLine(summary, scope, $"{name}_p5", percentile(sortedValues, 0.05));
            writeSummaryLine(summary, scope, $"{name}_median", percentile(sortedValues, 0.5));
            writeSummaryLine(summary, scope, $"{name}_p95", percentile(sortedValues, 0.95));
            writeSummaryLine(summary, scope, $"{name}_p99", percentile(sortedValues, 0.99));
            writeSummaryLine(summary, scope, $"{name}_max", sortedValues[^1]);
        }

        private static void writeSummaryLine(StreamWriter summary, string scope, string metric, double value)
            => summary.WriteLine($"{escape(scope)},{escape(metric)},{format(value)}");

        private static double percentile(double[] sortedValues, double percentile)
        {
            if (sortedValues.Length == 1)
                return sortedValues[0];

            double scaledIndex = percentile * (sortedValues.Length - 1);
            int lowerIndex = (int)Math.Floor(scaledIndex);
            int upperIndex = (int)Math.Ceiling(scaledIndex);

            if (lowerIndex == upperIndex)
                return sortedValues[lowerIndex];

            double amount = scaledIndex - lowerIndex;
            return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * amount;
        }

        private static string format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string escape(string value)
        {
            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
                return value;

            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        protected override void Dispose(bool isDisposing)
        {
            stopLogging();
            enabled.UnbindAll();
            base.Dispose(isDisposing);
        }

#pragma warning disable CS0649
        private struct PerformanceBreakdownMax
        {
            public double SchedulerMs;
            public double WorkMs;
            public double SleepMs;
            public double GcMs;
            public long Invalidations;
            public long Refreshes;
            public long DrawNodeCtor;
            public long DrawNodeAppl;
            public long ScheduleInvk;
            public long InputQueue;
            public long PositionalIQ;
            public long CCL;
            public long VBufBinds;
            public long VBufOverflow;
            public long TextureBinds;
            public long FBORedraw;
            public long DrawCalls;
            public long ShaderBinds;
            public long VerticesDraw;
            public long VerticesUpl;
            public long UniformUpl;
            public long Pixels;
            public long BatchFlushes;
            public long PipelineBinds;
            public long ResourceSetBinds;
            public long ResourceSetCreates;
            public long TextureUploadFlushes;
            public long TextureUploads;
            public long DeferredEventProcessUs;
            public long DeferredVertexWriteUs;
            public long DeferredVertexCommitUs;
            public long DeferredVertexDrawUs;
            public long DeferredVertexMapCount;
            public long DeferredVertexUnmapCount;
            public long DeferredVertexBytes;
            public long VeldridSwapBuffersUs;
            public long VeldridTextureUploadUs;
            public long PipelineCreates;
            public long VeldridVertexSetUs;
            public long VeldridVertexUpdateUs;
            public long VeldridVertexDrawUs;
            public long VeldridVertexBytes;
            public string? CclTop1Source;
            public long CclTop1Count;
            public string? CclTop2Source;
            public long CclTop2Count;
            public string? CclTop3Source;
            public long CclTop3Count;
            public string? InvalTop1Source;
            public long InvalTop1Count;
            public string? InvalTop2Source;
            public long InvalTop2Count;
            public string? InvalTop3Source;
            public long InvalTop3Count;

            // Standalone public build note: ThreadFramePerformanceSnapshot is an optional Mosu framework extension.
        }
#pragma warning restore CS0649

        private readonly record struct InputLatencySnapshot(
            long CollectedEventCount = 0,
            long AppliedEventCount = 0,
            long SwappedInputFrameCount = 0,
            double EnqueueToCollectMillisecondsMax = 0,
            double CollectToApplyMillisecondsMax = 0,
            double EnqueueToApplyMillisecondsMax = 0,
            double ApplyToPublishMillisecondsMax = 0,
            double PublishToSwapMillisecondsMax = 0,
            double EnqueueToSwapMillisecondsMax = 0);

        private readonly record struct SampleSummary(
            DateTimeOffset LocalTime,
            double ElapsedMs,
            LocalUserPlayingState PlayingState,
            bool IsActive,
            int BeatmapId,
            int BeatmapSetId,
            string Beatmap,
            Guid SkinId,
            string SkinName,
            bool WindowsUltraPerformanceMode,
            bool SkinPerformanceMode,
            bool UncappedFrameRate,
            FrameSync FrameSync,
            RendererType Renderer,
            RendererType ResolvedRenderer,
            WindowMode WindowMode,
            string BenchmarkId,
            string BenchmarkMode,
            int BenchmarkRun,
            bool BenchmarkWarmup,
            string BenchmarkReplayHash,
            bool BenchmarkGameplayActive,
            int MissCount,
            double Health,
            double DrawFps,
            double DrawMsMax,
            double DrawSchedulerMsMax,
            double DrawWorkMsMax,
            double DrawSleepMsMax,
            double DrawGcMsMax,
            long DrawVBufBindsMax,
            long DrawVBufOverflowMax,
            long DrawTextureBindsMax,
            long DrawFBORedrawMax,
            long DrawCallsMax,
            long DrawShaderBindsMax,
            long DrawVerticesDrawMax,
            long DrawVerticesUplMax,
            long DrawUniformUplMax,
            long DrawPixelsMax,
            long DrawBatchFlushesMax,
            long DrawPipelineBindsMax,
            long DrawResourceSetBindsMax,
            long DrawResourceSetCreatesMax,
            long DrawTextureUploadFlushesMax,
            long DrawTextureUploadsMax,
            long DrawDeferredEventProcessUsMax,
            long DrawDeferredVertexWriteUsMax,
            long DrawDeferredVertexCommitUsMax,
            long DrawDeferredVertexDrawUsMax,
            long DrawDeferredVertexMapCountMax,
            long DrawDeferredVertexUnmapCountMax,
            long DrawDeferredVertexBytesMax,
            long DrawVeldridSwapBuffersUsMax,
            long DrawVeldridTextureUploadUsMax,
            long DrawPipelineCreatesMax,
            long DrawVeldridVertexSetUsMax,
            long DrawVeldridVertexUpdateUsMax,
            long DrawVeldridVertexDrawUsMax,
            long DrawVeldridVertexBytesMax,
            double UpdateFps,
            double UpdateMsMax,
            double UpdateSchedulerMsMax,
            double UpdateWorkMsMax,
            double UpdateSleepMsMax,
            double UpdateGcMsMax,
            long UpdateInvalidationsMax,
            long UpdateRefreshesMax,
            long UpdateDrawNodeCtorMax,
            long UpdateDrawNodeApplMax,
            long UpdateScheduleInvkMax,
            long UpdateInputQueueMax,
            long UpdatePositionalIQMax,
            long UpdateCCLMax,
            double InputFps,
            double InputMsMax,
            double InputSchedulerMsMax,
            double InputWorkMsMax,
            double InputSleepMsMax,
            double InputGcMsMax,
            long InputEventsCollected,
            long InputEventsApplied,
            long InputFramesSwapped,
            double InputEnqueueToCollectMsMax,
            double InputCollectToApplyMsMax,
            double InputEnqueueToApplyMsMax,
            double InputApplyToPublishMsMax,
            double InputPublishToSwapMsMax,
            double InputEnqueueToSwapMsMax,
            double CpuPercent,
            double WorkingSetMb,
            double GcHeapMb,
            double GcAllocatedMbDelta,
            int GcGen0Delta,
            int GcGen1Delta,
            int GcGen2Delta);

        private readonly record struct FlushSourceSample(
            string Top1Source,
            int Top1Count,
            string Top2Source,
            int Top2Count,
            string Top3Source,
            int Top3Count);

        private readonly record struct SkinFileAuditEntry(string Filename, string StoragePath);
    }
}
