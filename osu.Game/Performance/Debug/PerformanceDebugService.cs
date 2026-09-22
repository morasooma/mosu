// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Process process = null!;
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
            process = Process.GetCurrentProcess();
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
                wasStuttering = false;
                return;
            }

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
            // Standalone public build note: GameThread.PerformanceSnapshot is an optional Mosu framework extension.
            PerformanceDebugSnapshot previous = Latest;

            var snapshot = new PerformanceDebugSnapshot
            {
                DrawFrameMs = host.DrawThread.Clock.ElapsedFrameTime,
                UpdateFrameMs = host.UpdateThread.Clock.ElapsedFrameTime,
                InputFrameMs = host.InputThread.Clock.ElapsedFrameTime,
                DrawGcMs = 0,
                UpdateGcMs = 0,
                InputGcMs = 0,
                WorkingSetMb = previous.WorkingSetMb,
                GcHeapMb = previous.GcHeapMb,
                AllocationRateMbPerSecond = previous.AllocationRateMbPerSecond,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                GcMode = GCSettings.LatencyMode,
                UpdateInvalidations = 0,
                UpdateCcl = 0,
                UpdateInputQueue = 0,
                DrawPipelineCreates = 0,
                DrawTextureUploadFlushes = 0,
                DrawTextureUploads = 0,
                DrawSwapBuffersUs = 0,
            };

            if (refreshMemory)
            {
                process.Refresh();
                long allocatedBytes = GC.GetTotalAllocatedBytes(false);
                double elapsedSeconds = Math.Max(0.001, (now - lastTelemetryTime) / 1000);
                snapshot.WorkingSetMb = process.WorkingSet64 / 1024d / 1024d;
                snapshot.GcHeapMb = GC.GetTotalMemory(false) / 1024d / 1024d;
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
            process?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}