using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// Serial, latest-pending-only worker. All calculator/cursor access (including disposal)
    /// belongs to the worker. A rewind invalidates already running forward results.
    /// </summary>
    internal sealed class ProgressivePerformanceCalculation : IDisposable
    {
        private readonly object sync = new object();
        private readonly Func<CancellationToken, IProgressiveDifficultyCalculator> createCursor;
        private readonly PerformanceCalculator performance;
        private readonly Action<PerformanceAttributes, long> completed;
        private readonly Action<Exception> failed;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private IProgressiveDifficultyCalculator? cursor;
        private Request? pending;
        private bool running;
        private bool disposed;
        private long epoch;
        private double lastTime = double.NegativeInfinity;

        public ProgressivePerformanceCalculation(Func<CancellationToken, IProgressiveDifficultyCalculator> createCursor,
                                                PerformanceCalculator performance, Action<PerformanceAttributes, long> completed, Action<Exception> failed)
        {
            this.createCursor = createCursor;
            this.performance = performance;
            this.completed = completed;
            this.failed = failed;
        }

        public long Queue(ScoreInfo score, double time, bool rewind)
        {
            lock (sync)
            {
                if (disposed)
                    return epoch;
                if (rewind || time < lastTime)
                    epoch++;
                lastTime = time;
                pending = new Request(score, time, epoch);
                if (!running)
                {
                    running = true;
                    _ = Task.Run(drain);
                }
                return epoch;
            }
        }

        private void drain()
        {
            try
            {
                while (true)
                {
                    Request request;
                    lock (sync)
                    {
                        if (disposed || pending == null)
                            return;
                        request = pending;
                        pending = null;
                    }
                    cancellation.Token.ThrowIfCancellationRequested();
                    cursor ??= createCursor(cancellation.Token);
                    var attributes = cursor.GetAttributesAtTime(request.Time, cancellation.Token);
                    if (attributes == null)
                        continue;
                    var result = performance.Calculate(request.Score, attributes);
                    lock (sync)
                    {
                        if (!disposed && request.Epoch == epoch)
                            completed(result, request.Epoch);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                lock (sync)
                {
                    pending = null;
                    cursor?.Dispose();
                    cursor = null;
                    if (!disposed)
                        failed(e);
                }
            }
            finally
            {
                lock (sync)
                {
                    running = false;
                    if (disposed)
                    {
                        cursor?.Dispose();
                        cursor = null;
                    }
                    else if (pending != null)
                    {
                        // A request may arrive after the empty-queue check and before finally.
                        running = true;
                        _ = Task.Run(drain);
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                pending = null;
                cancellation.Cancel();
                if (!running)
                {
                    cursor?.Dispose();
                    cursor = null;
                }
            }
        }

        private sealed record Request(ScoreInfo Score, double Time, long Epoch);
    }
}
