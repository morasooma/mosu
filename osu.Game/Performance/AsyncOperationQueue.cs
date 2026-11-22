using System;
using System.Threading.Tasks;

namespace osu.Game.Performance
{
    /// <summary>
    /// Ordered worker dispatch for already-snapshotted outgoing data. A failed item does not
    /// poison the queue; its own returned task still carries the failure to its caller.
    /// </summary>
    internal sealed class AsyncOperationQueue
    {
        private readonly object sync = new object();
        private Task tail = Task.CompletedTask;

        public Task Enqueue(Func<Task> operation) => Enqueue(async () =>
        {
            await operation().ConfigureAwait(false);
            return true;
        });

        /// <summary>
        /// Order synchronous invocation/serialization, but do not block subsequent invocation
        /// behind the prior network response. The returned task still awaits that response.
        /// </summary>
        public Task EnqueueInvocation(Func<Task> operation) => Enqueue(() => Task.FromResult(operation())).Unwrap();

        public Task<T> EnqueueInvocation<T>(Func<Task<T>> operation) => Enqueue(() => Task.FromResult(operation())).Unwrap();

        public Task<T> Enqueue<T>(Func<Task<T>> operation)
        {
            lock (sync)
            {
                Task previous = tail;
                Task<T> current = Task.Run(async () =>
                {
                    try
                    {
                        await previous.ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // The prior caller owns its failure; preserve subsequent order.
                    }
                    return await operation().ConfigureAwait(false);
                });
                // Do not retain an indefinitely growing chain of completed payloads/tasks.
                tail = current;
                _ = current.ContinueWith(completed =>
                {
                    _ = completed.Exception;
                    lock (sync)
                    {
                        if (ReferenceEquals(tail, completed))
                            tail = Task.CompletedTask;
                    }
                }, TaskScheduler.Default);
                return current;
            }
        }
    }
}
