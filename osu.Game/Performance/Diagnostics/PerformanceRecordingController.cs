using System;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Performance.Diagnostics
{
    /// <summary>
    /// Owns a single recording while an existing settings switch is enabled.
    /// SetEnabled/Update/Dispose are called on Update; capture I/O runs independently.
    /// </summary>
    public sealed class PerformanceRecordingController : IDisposable
    {
        private readonly Func<CancellationToken, Task<string>> start;
        private readonly Action<string?, Exception?, bool> completed;
        private CancellationTokenSource? cancellation;
        private Task<string>? task;
        private bool desired;
        private bool stopping;
        private bool disposed;

        public Task<string>? Pending => task;
        public string? LastPath { get; private set; }
        public Exception? LastError { get; private set; }

        public PerformanceRecordingController(Func<CancellationToken, Task<string>> start, Action<string?, Exception?, bool> completed)
        {
            this.start = start;
            this.completed = completed;
        }

        public void SetEnabled(bool enabled)
        {
            if (disposed)
                return;
            desired = enabled;
            if (!enabled && task != null)
            {
                stopping = true;
                cancellation!.Cancel();
            }
            if (enabled && task == null)
                begin();
        }

        private void begin()
        {
            stopping = false;
            LastPath = null;
            LastError = null;
            cancellation = new CancellationTokenSource();
            try
            {
                task = start(cancellation.Token);
            }
            catch (Exception e)
            {
                task = Task.FromException<string>(e);
            }
            // Observe faults even if the owning component is disposed before its next Update.
            _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void Update()
        {
            if (disposed || task?.IsCompleted != true)
                return;

            bool unexpectedStop = !stopping;
            string? path = null;
            Exception? error = null;
            try { path = task.GetAwaiter().GetResult(); }
            catch (Exception e) { error = e; }
            LastPath = path;
            LastError = error;
            task = null;
            cancellation!.Dispose();
            cancellation = null;
            // Cap/error disables logging rather than silently leaving CSV-only capture enabled.
            if (unexpectedStop || error != null)
                desired = false;
            completed(path, error, unexpectedStop);
            if (!disposed && desired && task == null)
                begin();
        }

        /// <summary>
        /// Called after the game threads have stopped. Joins the exact capture, including its
        /// drain/export, without requiring another Update or restarting a desired recording.
        /// </summary>
        public Task<string?> StopAsync()
        {
            SetEnabled(false);
            return task != null ? join(task)
                : LastError != null ? Task.FromException<string?>(LastError)
                : Task.FromResult(LastPath);

            static async Task<string?> join(Task<string> pending) => await pending.ConfigureAwait(false);
        }

        public void Dispose()
        {
            disposed = true;
            desired = false;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
        }
    }
}
