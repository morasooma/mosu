using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace osu.Game.Performance
{
    /// <summary>
    /// Publishes shared benchmark artifacts in session stop order, after each independent sink drain.
    /// Only one processor is active; completed sessions are removed rather than retained in a task chain.
    /// </summary>
    public sealed class OrderedPerformanceArtifactPublisher
    {
        private readonly object gate = new object();
        private readonly Queue<(Task Drain, Action Publish)> pending = new Queue<(Task, Action)>();
        private bool processing;
        private const int maximum_pending_publications = 8;

        /// <returns>False if the bounded pending queue is full. The caller must report the omitted artifacts.</returns>
        public bool Enqueue(Task drain, Action publish)
        {
            lock (gate)
            {
                if (pending.Count >= maximum_pending_publications)
                    return false;

                pending.Enqueue((drain, publish));
                if (!processing)
                {
                    processing = true;
                    _ = Task.Run(process);
                }

                return true;
            }
        }

        private async Task process()
        {
            while (true)
            {
                (Task Drain, Action Publish) item;
                lock (gate)
                {
                    if (pending.Count == 0)
                    {
                        processing = false;
                        return;
                    }

                    item = pending.Dequeue();
                }

                try
                {
                    await item.Drain.ConfigureAwait(false);
                    item.Publish();
                }
                catch (Exception e)
                {
                    // A failed publication must not prevent later sessions from publishing.
                    osu.Framework.Logging.Logger.Error(e, "mosu performance artifact publication failed.");
                }
            }
        }
    }
}
