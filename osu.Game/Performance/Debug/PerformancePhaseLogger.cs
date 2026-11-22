using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Threading;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;

namespace osu.Game.Performance.Debug
{
    /// <summary>
    /// Lightweight, source-labelled phase markers for correlating expensive transitions with frame and GC telemetry.
    /// </summary>
    public static class PerformancePhaseLogger
    {
        private static long nextId;
        private static readonly Phase inactivePhase = new Phase();

        /// <summary>
        /// Phase snapshots and log formatting are only useful while a performance capture is active.
        /// </summary>
        public static volatile bool Enabled;

        public static Phase Begin(string name, GameHost? host = null, string? detail = null)
            => Enabled || StutterTraceEventSource.Log.IsEnabled() ? new Phase(Interlocked.Increment(ref nextId), name, host, detail) : inactivePhase;

        public sealed class Phase : IDisposable
        {
            private readonly long id;
            private readonly string name;
            private readonly GameHost? host;
            private readonly long startedTimestamp;
            private readonly Snapshot started;
            private readonly bool active;
            private readonly bool logSnapshots;
            private string? endDetail;
            private int ended;

            internal Phase()
            {
                name = string.Empty;
            }

            internal Phase(long id, string name, GameHost? host, string? detail)
            {
                this.id = id;
                this.name = name;
                this.host = host;
                active = true;
                startedTimestamp = Stopwatch.GetTimestamp();
                StutterTraceEventSource.Log.Phase(name, id, true, startedTimestamp);
                logSnapshots = Enabled;
                if (!logSnapshots)
                    return;

                started = Snapshot.Capture();

                Logger.Log($"[Phase::BEGIN] id={id} name={name} local_time={DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)} {started.FormatAbsolute()} detail={formatDetail(detail)}",
                    LoggingTarget.Performance, LogLevel.Verbose);
            }

            public void SetEndDetail(string? detail)
            {
                if (active)
                    endDetail = detail;
            }

            public void End(string? detail = null)
            {
                if (!active || Interlocked.Exchange(ref ended, 1) != 0)
                    return;

                StutterTraceEventSource.Log.Phase(name, id, false, Stopwatch.GetTimestamp());
                if (!logSnapshots)
                    return;

                Snapshot finished = Snapshot.Capture();
                double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
                string frameMetrics = string.Empty;

                if (host != null)
                {
                    var draw = host.DrawThread.PerformanceSnapshot;
                    var update = host.UpdateThread.PerformanceSnapshot;
                    frameMetrics = $" draw_ms={host.DrawThread.Clock.ElapsedFrameTime:0.###} update_ms={host.UpdateThread.Clock.ElapsedFrameTime:0.###}"
                                   + $" draw_gc_ms={draw.GcMs:0.###} update_gc_ms={update.GcMs:0.###}"
                                   + $" texture_uploads={draw.TextureUploads} texture_flushes={draw.TextureUploadFlushes} pipelines={draw.PipelineCreates}"
                                   + $" update_scheduler_ms={update.SchedulerMs:0.###} update_work_ms={update.WorkMs:0.###}";
                }

                Logger.Log($"[Phase::END] id={id} name={name} local_time={DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)} elapsed_ms={elapsedMilliseconds:0.###} {finished.FormatDelta(started)}{frameMetrics} detail={formatDetail(detail ?? endDetail)}",
                    LoggingTarget.Performance, LogLevel.Verbose);
            }

            public void Dispose() => End();
        }

        internal readonly record struct Snapshot(
            long AllocatedBytes,
            long ThreadAllocatedBytes,
            double PauseMilliseconds,
            int Gen0,
            int Gen1,
            int Gen2,
            GCLatencyMode LatencyMode,
            int ThreadId,
            string ThreadName)
        {
            public static Snapshot Capture()
            {
                return new Snapshot(
                    GC.GetTotalAllocatedBytes(false),
                    GC.GetAllocatedBytesForCurrentThread(),
                    GC.GetTotalPauseDuration().TotalMilliseconds,
                    GC.CollectionCount(0),
                    GC.CollectionCount(1),
                    GC.CollectionCount(2),
                    GCSettings.LatencyMode,
                    Environment.CurrentManagedThreadId,
                    getThreadName());
            }

            // alloc_* retains its existing process-wide meaning; thread_alloc_* is only comparable on the same managed thread.
            public string FormatAbsolute()
                => $"thread={ThreadName}({ThreadId}) alloc_scope=process alloc_mb={toMiB(AllocatedBytes):0.###}"
                   + $" thread_alloc_scope=current_thread thread_alloc_mb={toMiB(ThreadAllocatedBytes):0.###}"
                   + $" gc=[{Gen0},{Gen1},{Gen2}] gc_pause_ms={PauseMilliseconds:0.###} gc_mode={LatencyMode}";

            public string FormatDelta(Snapshot previous)
                => $"thread={ThreadName}({ThreadId}) alloc_scope=process alloc_delta_mb={toMiB(AllocatedBytes - previous.AllocatedBytes):0.###}"
                   + $" thread_alloc_scope=current_thread thread_alloc_delta_mb={(ThreadId == previous.ThreadId ? toMiB(ThreadAllocatedBytes - previous.ThreadAllocatedBytes).ToString("0.###", CultureInfo.InvariantCulture) : "unavailable")}"
                   + $" gc_delta=[{Gen0 - previous.Gen0},{Gen1 - previous.Gen1},{Gen2 - previous.Gen2}]"
                   + $" gc_pause_delta_ms={PauseMilliseconds - previous.PauseMilliseconds:0.###} gc_mode={LatencyMode}";

            private static string getThreadName()
            {
                if (ThreadSafety.IsUpdateThread)
                    return "Update";

                if (ThreadSafety.IsDrawThread)
                    return "Draw";

                return Thread.CurrentThread.Name ?? (Thread.CurrentThread.IsThreadPoolThread ? "ThreadPool" : "Managed");
            }

            private static double toMiB(long bytes) => bytes / 1024d / 1024d;
        }

        private static string formatDetail(string? detail)
            => string.IsNullOrWhiteSpace(detail) ? "-" : detail.Replace('\r', ' ').Replace('\n', ' ');
    }
}
