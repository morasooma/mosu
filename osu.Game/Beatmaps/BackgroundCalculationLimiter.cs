using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Bounds concurrent library maintenance calculations. Interactive difficulty lookups do not use this gate.
    /// This limits parallelism, not the CPU usage of an individual calculation.
    /// </summary>
    internal static class BackgroundCalculationLimiter
    {
        private static readonly object sync = new object();
        private static int running;
        private static long interactiveDeadline;
        private static readonly Dictionary<string, int> modeRunning = new Dictionary<string, int>();

        [ThreadStatic]
        private static Releaser? current;

        /// <summary>
        /// Let carousel navigation settle before resuming speculative library maintenance.
        /// Interactive difficulty requests and import transactions do not use this pause.
        /// </summary>
        internal static void DeferForInteractiveUse(int milliseconds)
        {
            long deadline = Stopwatch.GetTimestamp() + (long)(milliseconds * (double)Stopwatch.Frequency / 1000);
            long previous;
            do
            {
                previous = Interlocked.Read(ref interactiveDeadline);
                if (previous >= deadline)
                    return;
            } while (Interlocked.CompareExchange(ref interactiveDeadline, deadline, previous) != previous);
        }

        private static void waitForInteractiveUse(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double remaining = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), Interlocked.Read(ref interactiveDeadline)).TotalMilliseconds;
                if (remaining <= 0)
                    return;
                wait((int)Math.Clamp(Math.Ceiling(remaining), 1, 50), cancellationToken);
            }
        }

        private static void wait(int milliseconds, CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                if (cancellationToken.WaitHandle.WaitOne(milliseconds))
                    cancellationToken.ThrowIfCancellationRequested();
            }
            else
                Thread.Sleep(milliseconds);
        }

        /// <summary>
        /// Checkpoints within a single map limit sustained work, rather than sleeping only between maps.
        /// This is cooperative and does not cap GC, unmanaged work or other application threads.
        /// </summary>
        public static void Checkpoint() => current?.Checkpoint();

        // Lazy decoding runs on a dedicated thread while the calculation caller waits.
        // Carry the existing lease there, without acquiring a second concurrency slot.
        public static Context CaptureContext() => new Context(current);

        internal readonly struct Context
        {
            private readonly Releaser? budget;

            internal Context(Releaser? budget) => this.budget = budget;

            public T Run<T>(Func<T> action)
            {
                var previous = current;
                current = budget;
                try
                {
                    return action();
                }
                finally
                {
                    current = previous;
                }
            }
        }

        public static IDisposable Enter(int maximumConcurrency, int dutyPercent, string? mode = null, int modeConcurrency = 4,
                                        CancellationToken cancellationToken = default, bool deferWhileInteracting = false)
        {
            maximumConcurrency = Math.Clamp(maximumConcurrency, 1, 4);
            // Pause only before acquiring a slot. In-flight work must be allowed to
            // finish: another consumer may be awaiting the same lazy decoder or own
            // a Realm transaction while waiting for this concurrency gate.
            while (true)
            {
                if (deferWhileInteracting)
                    waitForInteractiveUse(cancellationToken);

                lock (sync)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (running < maximumConcurrency
                        && (mode == null || modeRunning.GetValueOrDefault(mode) < Math.Clamp(modeConcurrency, 1, 4)))
                    {
                        // Interaction may have begun while this caller was waiting for capacity.
                        if (deferWhileInteracting && Stopwatch.GetTimestamp() < Interlocked.Read(ref interactiveDeadline))
                            continue;

                        running++;
                        if (mode != null)
                            modeRunning[mode] = modeRunning.GetValueOrDefault(mode) + 1;
                        break;
                    }
                    Monitor.Wait(sync, 50);
                }
            }

            var lease = new Releaser(Math.Clamp(dutyPercent, 10, 100), current, mode);
            current = lease;
            return lease;
        }

        internal sealed class Releaser : IDisposable
        {
            private readonly int dutyPercent;
            private readonly string? mode;
            private readonly Releaser? previous;
            private long windowStart = Stopwatch.GetTimestamp();
            private bool disposed;

            public Releaser(int dutyPercent, Releaser? previous, string? mode)
            {
                this.dutyPercent = dutyPercent;
                this.mode = mode;
                this.previous = previous;
            }

            public void Checkpoint()
            {
                if (dutyPercent == 100)
                    return;

                double elapsedMs = Stopwatch.GetElapsedTime(windowStart).TotalMilliseconds;
                if (elapsedMs < 10)
                    return;

                // Limit continuous work to roughly 10 ms. The OS may oversleep, so this
                // controls average duty cycle, not an OS-enforced CPU quota.
                int restMs = (int)Math.Ceiling(elapsedMs * (100 - dutyPercent) / dutyPercent);
                Thread.Sleep(Math.Min(restMs, 100));
                windowStart = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                current = previous;
                lock (sync)
                {
                    if (disposed)
                        return;

                    disposed = true;
                    running--;
                    if (mode != null)
                    {
                        if (--modeRunning[mode] == 0)
                            modeRunning.Remove(mode);
                    }
                    Monitor.PulseAll(sync);
                }
            }
        }
    }
}
