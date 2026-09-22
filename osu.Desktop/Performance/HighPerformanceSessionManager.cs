// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Game.Performance;

namespace osu.Desktop.Performance
{
    public class HighPerformanceSessionManager : IHighPerformanceSessionManager
    {
        public bool IsSessionActive => activeSessions > 0;

        private int activeSessions;

        private GCLatencyMode originalGCMode;

        public IDisposable BeginSession()
        {
            enterSession();
            return new InvokeOnDisposal<HighPerformanceSessionManager>(this, static m => m.exitSession());
        }

        private void enterSession()
        {
            if (Interlocked.Increment(ref activeSessions) > 1)
            {
                Logger.Log($"High performance session requested ({activeSessions} running in total)");
                return;
            }

            Logger.Log("Starting high performance session");

            originalGCMode = GCSettings.LatencyMode;
            try
            {
                GCSettings.LatencyMode = GCLatencyMode.LowLatency;
                Logger.Log($"GC LatencyMode set to: {GCSettings.LatencyMode} (requested: {GCLatencyMode.LowLatency}, ServerGC: {GCSettings.IsServerGC})");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set GC LatencyMode to {GCLatencyMode.LowLatency}");
            }

            // Without doing this, the new GC mode won't kick in until the next GC, which could be at a more noticeable point in time.
            GC.Collect(0);
        }

        private void exitSession()
        {
            if (Interlocked.Decrement(ref activeSessions) > 0)
            {
                Logger.Log($"High performance session finished ({activeSessions} others remain)");
                return;
            }

            Logger.Log("Ending high performance session");

            if (GCSettings.LatencyMode == GCLatencyMode.LowLatency)
            {
                try
                {
                    GCSettings.LatencyMode = originalGCMode;
                    Logger.Log($"GC LatencyMode restored to: {GCSettings.LatencyMode}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to restore GC LatencyMode to {originalGCMode}");
                }
            }

            // Do not force a full collection here. Gameplay-related caches must bound their own
            // lifetimes; a blocking compacting GC causes a visible results-screen stall.
        }
    }
}
