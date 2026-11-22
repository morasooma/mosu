using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Performance
{
    /// <summary>
    /// One file owner per capture. A full queue drops new rows rather than blocking the producer.
    /// Completion always observes writer and close failures; no task is spawned per row.
    /// </summary>
    public sealed class BoundedPerformanceFileSink
    {
        private readonly object gate = new object();
        private readonly Queue<(string File, string Text)> pending = new Queue<(string, string)>();
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);
        private readonly Func<string, TextWriter> open;
        private readonly Action? afterDrain;
        private readonly int capacity;
        private bool stopping;
        private long dropped;
        private Exception? failure;

        public Task Completion { get; }

        public long Dropped
        {
            get { lock (gate) return dropped; }
        }

        public Exception? Failure
        {
            get { lock (gate) return failure; }
        }

        public BoundedPerformanceFileSink(Func<string, TextWriter> open, int capacity = 256, Action? afterDrain = null)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            this.open = open;
            this.capacity = capacity;
            this.afterDrain = afterDrain;
            Completion = Task.Run(run);
        }

        public bool TryWrite(string file, string text)
        {
            lock (gate)
            {
                if (stopping || failure != null || pending.Count >= capacity)
                {
                    dropped++;
                    return false;
                }

                pending.Enqueue((file, text));
                signal.Release();
                return true;
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                if (stopping)
                    return;

                stopping = true;
                signal.Release();
            }
        }

        private void run()
        {
            var writers = new Dictionary<string, TextWriter>();

            bool processing = false;
            try
            {
                while (true)
                {
                    signal.Wait();
                    (string File, string Text) item;

                    lock (gate)
                    {
                        if (pending.Count == 0)
                        {
                            if (stopping)
                                break;

                            continue;
                        }

                        item = pending.Dequeue();
                    }

                    processing = true;
                    if (!writers.TryGetValue(item.File, out TextWriter? writer))
                        writers[item.File] = writer = open(item.File);

                    writer.Write(item.Text);
                    processing = false;
                }
            }
            catch (Exception e)
            {
                recordFailure(e);
                lock (gate)
                {
                    dropped += pending.Count + (processing ? 1 : 0);
                    pending.Clear();
                    stopping = true;
                }
            }
            finally
            {
                foreach (TextWriter writer in writers.Values)
                {
                    try { writer.Dispose(); }
                    catch (Exception e) { recordFailure(e); }
                }

                if (Failure == null)
                {
                    try { afterDrain?.Invoke(); }
                    catch (Exception e) { recordFailure(e); }
                }

                signal.Dispose();
            }
        }

        private void recordFailure(Exception e)
        {
            lock (gate) failure ??= e;
        }
    }
}
