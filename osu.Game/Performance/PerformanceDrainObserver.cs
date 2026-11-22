using System;
using System.Threading.Tasks;

namespace osu.Game.Performance
{
    public static class PerformanceDrainObserver
    {
        public static void Observe(Task sinkCompletion, Action reportSink, Task auditCompletion, Action<Exception> reportAudit)
        {
            _ = sinkCompletion.ContinueWith(_ => reportSink(), TaskScheduler.Default);
            _ = auditCompletion.ContinueWith(completed =>
            {
                if (completed.IsFaulted)
                    reportAudit(completed.Exception!);
            }, TaskScheduler.Default);
        }
    }
}
