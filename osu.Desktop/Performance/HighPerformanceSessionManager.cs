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

        private GCLatencyMode? sessionGCMode;

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
            sessionGCMode = null;
            try
            {
                // LowLatency shrinks the workstation GC's Gen0 budget drastically. Gameplay startup allocates
                // hundreds of megabytes while constructing the player and skin, which can otherwise trigger
                // thousands of foreground collections and stall the update thread for several seconds.
                // SustainedLowLatency still favours short pauses, but permits background collection and keeps a
                // practical allocation budget for the whole gameplay session.
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                // Satori maps both low-latency requests to LowLatency when read back. Remember the
                // actual mode so that leaving gameplay can restore Interactive on either runtime.
                sessionGCMode = GCSettings.LatencyMode;
                Logger.Log($"GC LatencyMode set to: {GCSettings.LatencyMode} (requested: {GCLatencyMode.SustainedLowLatency}, ServerGC: {GCSettings.IsServerGC})");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set GC LatencyMode to {GCLatencyMode.SustainedLowLatency}");
            }

            // Do not force a collection here. Player construction is already allocation-heavy, and forcing the
            // mode transition to take effect synchronously makes the gameplay-start path more expensive.
        }

        private void exitSession()
        {
            if (Interlocked.Decrement(ref activeSessions) > 0)
            {
                Logger.Log($"High performance session finished ({activeSessions} others remain)");
                return;
            }

            Logger.Log("Ending high performance session");

            if (sessionGCMode.HasValue && GCSettings.LatencyMode == sessionGCMode.Value)
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
