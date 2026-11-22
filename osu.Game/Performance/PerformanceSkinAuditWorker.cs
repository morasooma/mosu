using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Platform;

namespace osu.Game.Performance
{
    public static class PerformanceSkinAuditWorker
    {
        public static Task Schedule(Task previous, Storage rootStorage, Action<Storage> audit)
            => previous.ContinueWith(completed =>
            {
                completed.GetAwaiter().GetResult();
                audit(rootStorage.GetStorageForDirectory("files"));
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }
}
