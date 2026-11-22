// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Logging;

namespace osu.Framework.Statistics
{
    /// <summary>
    /// Lightweight freeze/stutter detector that logs slow frames with GC and memory diagnostics.
    /// Designed to be called once per frame from <see cref="PerformanceMonitor.NewFrame"/>.
    /// Unlike <see cref="BackgroundStackTraceCollector"/>, this does not attempt to capture stack traces
    /// and has near-zero overhead — just integer comparisons each frame.
    /// </summary>
    public class FreezeDetector
    {
        /// <summary>
        /// Globally enables or disables freeze detection logging.
        /// </summary>
        public static bool IsEnabled { get; set; } = false;

        /// <summary>
        /// Minimum frame time in milliseconds to be considered a freeze. Default: 30ms.
        /// </summary>
        public double ThresholdMs { get; set; } = 30;

        /// <summary>
        /// Minimum interval between logged freeze events (per thread) to avoid log spam
        /// during sustained stuttering. Default: 1 second.
        /// </summary>
        public double CooldownMs { get; set; } = 1000;

        private readonly string threadName;
        private readonly int[] lastGcCounts = new int[3];
        private double lastGcPauseTotalMs;
        private double lastLogTimeMs;
        private readonly Stopwatch wallClock = Stopwatch.StartNew();
        private long totalFreezeCount;
        private double totalFreezeTimeMs;

        public FreezeDetector(string threadName)
        {
            this.threadName = threadName;

            // Snapshot initial GC state so first frame doesn't show bogus deltas.
            for (int i = 0; i < 3; i++)
                lastGcCounts[i] = GC.CollectionCount(i);

            lastGcPauseTotalMs = GC.GetTotalPauseDuration().TotalMilliseconds;
        }

        /// <summary>
        /// Called once per frame. Checks if the frame exceeded the freeze threshold and logs diagnostics.
        /// </summary>
        /// <param name="elapsedFrameTimeMs">The elapsed time of the current frame in milliseconds.</param>
        public void NewFrame(double elapsedFrameTimeMs)
        {
            if (!IsEnabled)
                return;

            // Always update GC snapshot — we need accurate deltas even for non-freeze frames.
            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);

            double gcPauseTotalMs = GC.GetTotalPauseDuration().TotalMilliseconds;
            double gcPauseDeltaMs = gcPauseTotalMs - lastGcPauseTotalMs;

            int gc0Delta = gc0 - lastGcCounts[0];
            int gc1Delta = gc1 - lastGcCounts[1];
            int gc2Delta = gc2 - lastGcCounts[2];

            lastGcCounts[0] = gc0;
            lastGcCounts[1] = gc1;
            lastGcCounts[2] = gc2;
            lastGcPauseTotalMs = gcPauseTotalMs;

            if (elapsedFrameTimeMs < ThresholdMs)
                return;

            totalFreezeCount++;
            totalFreezeTimeMs += elapsedFrameTimeMs;

            double nowMs = wallClock.Elapsed.TotalMilliseconds;

            if (nowMs - lastLogTimeMs < CooldownMs)
                return;

            lastLogTimeMs = nowMs;

            long managedBytes = GC.GetTotalMemory(false);
            double managedMiB = managedBytes / 1024.0 / 1024.0;

            bool hasGc = gc0Delta > 0 || gc1Delta > 0 || gc2Delta > 0;
            string gcInfo = hasGc
                ? $"gc0=+{gc0Delta} gc1=+{gc1Delta} gc2=+{gc2Delta} gcPause={gcPauseDeltaMs:0.#}ms"
                : $"no GC (gcPause={gcPauseDeltaMs:0.#}ms)";

            Logger.Log(
                $"[FreezeDetector] Slow frame on \"{threadName}\": {elapsedFrameTimeMs:0.#}ms | {gcInfo} | managed={managedMiB:0.##}MiB | total freezes={totalFreezeCount} ({totalFreezeTimeMs:0.#}ms cumulative)",
                LoggingTarget.Performance,
                LogLevel.Verbose);
        }
    }
}
