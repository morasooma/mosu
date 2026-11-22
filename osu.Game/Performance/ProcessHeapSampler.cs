using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Performance
{
    /// <summary>
    /// A capture-owned process/managed-heap poller. The update thread reads only published immutable snapshots.
    /// The heap figure is GC.GetTotalMemory(false), not an approximation from GC.GetGCMemoryInfo.
    /// </summary>
    public sealed class ProcessHeapSampler : IDisposable
    {
        public sealed record Snapshot(long Timestamp, double CpuPercent, double WorkingSetMb, double GcHeapMb, string Priority);

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Task completion;
        private Snapshot? latest;
        private Exception? failure;
        private int disposed;

        public Snapshot? Latest => Volatile.Read(ref latest);
        public Exception? Failure => Volatile.Read(ref failure);
        public Task Completion => completion;

        public ProcessHeapSampler(TimeSpan? interval = null)
        {
            TimeSpan period = interval ?? TimeSpan.FromMilliseconds(500);
            if (period <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval));

            completion = Task.Run(async () =>
            {
                try
                {
                    using var process = Process.GetCurrentProcess();
                    TimeSpan previousCpu = process.TotalProcessorTime;
                    long previousTimestamp = Stopwatch.GetTimestamp();

                    while (!cancellation.IsCancellationRequested)
                    {
                        await Task.Delay(period, cancellation.Token).ConfigureAwait(false);
                        process.Refresh();
                        long timestamp = Stopwatch.GetTimestamp();
                        TimeSpan cpu = process.TotalProcessorTime;
                        double seconds = Math.Max(0.001, Stopwatch.GetElapsedTime(previousTimestamp, timestamp).TotalSeconds);
                        var snapshot = new Snapshot(timestamp,
                            (cpu - previousCpu).TotalSeconds / seconds / Environment.ProcessorCount * 100,
                            process.WorkingSet64 / 1024d / 1024d,
                            GC.GetTotalMemory(false) / 1024d / 1024d,
                            process.PriorityClass.ToString());
                        Volatile.Write(ref latest, snapshot);
                        previousCpu = cpu;
                        previousTimestamp = timestamp;
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception e) { Volatile.Write(ref failure, e); }
            });
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                cancellation.Cancel();
                _ = completion.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
            }
        }
    }
}
