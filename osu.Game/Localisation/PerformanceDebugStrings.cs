using System.Runtime;
using osu.Framework.Localisation;
using osu.Game.Performance.Debug;

namespace osu.Game.Localisation
{
    public static class PerformanceDebugStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.PerformanceDebug";

        public static LocalisableString CompactMetrics(PerformanceDebugSnapshot s) => text("compact_metrics", s.DrawFrameMs, s.UpdateFrameMs, s.InputFrameMs, s.WorkingSetMb, s.GcHeapMb, s.AllocationRateMbPerSecond);

        public static LocalisableString DetailedMetrics(PerformanceDebugSnapshot s) => text("detailed_metrics", s.DrawFrameMs, s.UpdateFrameMs, s.InputFrameMs, s.WorkingSetMb, s.GcHeapMb, s.AllocationRateMbPerSecond, GcMode(s.GcMode), s.Gen0Collections, s.Gen1Collections, s.Gen2Collections);

        public static LocalisableString FreezeAlert(PerformanceFreezeEvent freeze) => text("freeze_alert", freeze.Snapshot.WorstFrameMs, FreezeCause(freeze.Cause));

        public static LocalisableString MemoryTooltip(PerformanceDebugSnapshot s) => text("memory_tooltip", s.WorkingSetMb, s.GcHeapMb, s.AllocationRateMbPerSecond, GcMode(s.GcMode));

        public static LocalisableString MemoryToolbar(PerformanceDebugSnapshot s) => text("memory_toolbar", s.WorkingSetMb, s.GcHeapMb);

        public static LocalisableString FreezeCause(PerformanceFreezeCause cause) => text(cause switch
        {
            PerformanceFreezeCause.GarbageCollection => "cause_gc",
            PerformanceFreezeCause.GpuPipeline => "cause_gpu",
            PerformanceFreezeCause.TextureUpload => "cause_texture",
            PerformanceFreezeCause.Presentation => "cause_presentation",
            PerformanceFreezeCause.UiChurn => "cause_ui",
            PerformanceFreezeCause.Input => "cause_input",
            PerformanceFreezeCause.DrawWork => "cause_draw",
            PerformanceFreezeCause.UpdateWork => "cause_update",
            _ => "cause_unknown",
        });

        public static LocalisableString GcMode(GCLatencyMode mode) => text(mode switch
        {
            GCLatencyMode.Batch => "gc_batch",
            GCLatencyMode.Interactive => "gc_interactive",
            GCLatencyMode.LowLatency => "gc_low_latency",
            GCLatencyMode.SustainedLowLatency => "gc_sustained_low_latency",
            GCLatencyMode.NoGCRegion => "gc_no_gc_region",
            _ => "gc_unknown",
        });

        private static LocalisableString text(string key, params object[] args) => new TranslatableString($"{prefix}:{key}", key switch
        {
                @"compact_metrics" => @"Draw {0:0.0}  Update {1:0.0}  Input {2:0.0} ms
RAM {3:0} MB  GC {4:0} MB / {5:0.0} MB/s",
                @"detailed_metrics" => @"Draw {0:0.0} ms   Update {1:0.0} ms   Input {2:0.0} ms
RAM {3:0} MB   GC {4:0} MB   allocations {5:0.0} MB/s
GC mode {6}   Gen 0: {7}  Gen 1: {8}  Gen 2: {9}",
                @"freeze_alert" => @"STUTTER {0:0.0} ms — likely: {1}",
                @"memory_tooltip" => @"Process RAM: {0:0} MB
GC heap: {1:0} MB
Allocations: {2:0.0} MB/s
GC mode: {3}",
                @"memory_toolbar" => @"RAM {0:0} MB | GC {1:0} MB",
                @"cause_gc" => @"GC pause",
                @"cause_gpu" => @"GPU pipeline",
                @"cause_texture" => @"texture upload",
                @"cause_presentation" => @"GPU / VSync",
                @"cause_ui" => @"UI rebuilding",
                @"cause_input" => @"input thread",
                @"cause_draw" => @"draw thread",
                @"cause_update" => @"update thread",
                @"cause_unknown" => @"unknown",
                @"gc_batch" => @"batch",
                @"gc_interactive" => @"interactive",
                @"gc_low_latency" => @"low latency",
                @"gc_sustained_low_latency" => @"sustained low latency",
                @"gc_no_gc_region" => @"GC disabled (NoGCRegion)",
                @"gc_unknown" => @"unknown",
            _ => key,
        }, args);
    }
}
