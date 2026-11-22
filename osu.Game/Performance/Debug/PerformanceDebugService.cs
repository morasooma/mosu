// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.Configuration;

namespace osu.Game.Performance.Debug
{
    public partial class PerformanceDebugService : Component
    {
        private const double telemetry_interval_ms = 250;
        private const int max_freeze_events = 32;

        private readonly Queue<PerformanceFreezeEvent> freezeEvents = new Queue<PerformanceFreezeEvent>(max_freeze_events);
        private readonly Bindable<DebugHudMode> mode = new Bindable<DebugHudMode>();
        private readonly BindableBool showMemoryInToolbar = new BindableBool();
        private readonly BindableBool showFreezeAlerts = new BindableBool();
        private GameHost host = null!;
        private ProcessHeapSampler? processSampler;
        private double lastTelemetryTime;
        private long lastAllocatedBytes;
        private bool wasStuttering;
        private long freezeSequence;

        public PerformanceDebugSnapshot Latest { get; private set; }
        public PerformanceFreezeEvent? LastFreeze { get; private set; }
        public IReadOnlyCollection<PerformanceFreezeEvent> FreezeEvents => freezeEvents;
        public bool Enabled => mode.Value != DebugHudMode.Disabled || showMemoryInToolbar.Value || showFreezeAlerts.Value;

        public string CreateCsvReport() => PerformanceDebugReportFormatter.FormatCsv(freezeEvents);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, GameHost gameHost)
        {
            host = gameHost;
            config.BindWith(OsuSetting.ForkDebugHudMode, mode);
            config.BindWith(OsuSetting.ForkShowMemoryInToolbar, showMemoryInToolbar);
            config.BindWith(OsuSetting.ForkDebugFreezeAlerts, showFreezeAlerts);
            lastAllocatedBytes = GC.GetTotalAllocatedBytes(false);
            lastTelemetryTime = Time.Current - telemetry_interval_ms;
        }

        protected override void Update()
        {
            base.Update();

            if (!Enabled)
            {
                processSampler?.Dispose();
                processSampler = null;
                wasStuttering = false;
                return;
            }

            // Process.Refresh/WorkingSet64 can read procfs or query the Windows
            // process API. Keep that work (and heap polling) out of the frame whose
            // timing this HUD is supposed to report. Reuse the existing poller.
            processSampler ??= new ProcessHeapSampler(TimeSpan.FromMilliseconds(telemetry_interval_ms));
            double now = Time.Current;
            bool refreshMemory = now - lastTelemetryTime >= telemetry_interval_ms;
            PerformanceDebugSnapshot snapshot = capture(refreshMemory, now);
            Latest = snapshot;

            bool stuttering = snapshot.WorstFrameMs >= PerformanceFreezeClassifier.STUTTER_THRESHOLD_MS;

            if (stuttering && !wasStuttering)
            {
                var freeze = new PerformanceFreezeEvent(++freezeSequence, DateTimeOffset.Now, snapshot, PerformanceFreezeClassifier.Classify(snapshot));
                LastFreeze = freeze;

                if (freezeEvents.Count == max_freeze_events)
                    freezeEvents.Dequeue();

                freezeEvents.Enqueue(freeze);
            }

            wasStuttering = stuttering;
        }

        private PerformanceDebugSnapshot capture(bool refreshMemory, double now)
        {
            var draw = host.DrawThread.PerformanceSnapshot;
            var update = host.UpdateThread.PerformanceSnapshot;
            var input = host.InputThread.PerformanceSnapshot;
            PerformanceDebugSnapshot previous = Latest;

            var snapshot = new PerformanceDebugSnapshot
            {
                DrawFrameMs = host.DrawThread.Clock.ElapsedFrameTime,
                UpdateFrameMs = host.UpdateThread.Clock.ElapsedFrameTime,
                InputFrameMs = host.InputThread.Clock.ElapsedFrameTime,
                DrawGcMs = draw.GcMs,
                UpdateGcMs = update.GcMs,
                InputGcMs = input.GcMs,
                WorkingSetMb = previous.WorkingSetMb,
                GcHeapMb = previous.GcHeapMb,
                AllocationRateMbPerSecond = previous.AllocationRateMbPerSecond,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                GcMode = GCSettings.LatencyMode,
                UpdateInvalidations = update.Invalidations,
                UpdateCcl = update.CCL,
                UpdateInputQueue = update.InputQueue,
                DrawPipelineCreates = draw.PipelineCreates,
                DrawTextureUploadFlushes = draw.TextureUploadFlushes,
                DrawTextureUploads = draw.TextureUploads,
                DrawSwapBuffersUs = draw.VeldridSwapBuffersUs,
            };

            if (refreshMemory)
            {
                long allocatedBytes = GC.GetTotalAllocatedBytes(false);
                double elapsedSeconds = Math.Max(0.001, (now - lastTelemetryTime) / 1000);
                if (processSampler?.Latest is { } memory)
                {
                    snapshot.WorkingSetMb = memory.WorkingSetMb;
                    snapshot.GcHeapMb = memory.GcHeapMb;
                }
                snapshot.AllocationRateMbPerSecond = Math.Max(0, allocatedBytes - lastAllocatedBytes) / 1024d / 1024d / elapsedSeconds;
                lastAllocatedBytes = allocatedBytes;
                lastTelemetryTime = now;
            }

            return snapshot;
        }

        protected override void Dispose(bool isDisposing)
        {
            mode.UnbindAll();
            showMemoryInToolbar.UnbindAll();
            showFreezeAlerts.UnbindAll();
            processSampler?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}