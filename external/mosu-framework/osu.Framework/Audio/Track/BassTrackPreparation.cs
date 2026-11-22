using System;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Framework.Audio.Track
{
    /// <summary>
    /// Serialises cold track preparation off the audio thread. Device init/free must not race
    /// native stream creation before the decoding channels have been detached from the device.
    /// </summary>
    internal static class BassTrackPreparation
    {
        private static readonly ConcurrentExclusiveSchedulerPair scheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, 1);
        private static readonly ReaderWriterLockSlim deviceLifetime = new ReaderWriterLockSlim();

        public static Task Run(Action action) => Task.Factory.StartNew(() =>
        {
            deviceLifetime.EnterReadLock();
            try
            {
                action();
            }
            finally
            {
                deviceLifetime.ExitReadLock();
            }
        }, CancellationToken.None, TaskCreationOptions.HideScheduler, scheduler.ExclusiveScheduler);

        public static bool ChangeDevice(Func<bool> action)
        {
            deviceLifetime.EnterWriteLock();
            try
            {
                return action();
            }
            finally
            {
                deviceLifetime.ExitWriteLock();
            }
        }
    }
}
