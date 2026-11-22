using System;
using System.Runtime;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Logging;
using osu.Game.Performance.Debug;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class PerformancePhaseLoggerTest
    {
        [TearDown]
        public void TearDown() => PerformancePhaseLogger.Enabled = false;

        [Test]
        public void TestSameThreadAllocationDelta()
        {
            PerformancePhaseLogger.Snapshot before = PerformancePhaseLogger.Snapshot.Capture();
            byte[] allocated = new byte[64 * 1024];
            GC.KeepAlive(allocated);
            PerformancePhaseLogger.Snapshot after = PerformancePhaseLogger.Snapshot.Capture();

            Assert.That(after.ThreadId, Is.EqualTo(before.ThreadId));
            Assert.That(after.ThreadAllocatedBytes - before.ThreadAllocatedBytes, Is.GreaterThanOrEqualTo(allocated.Length));
            Assert.Multiple(() =>
            {
                Assert.That(after.FormatAbsolute(), Does.Contain("alloc_scope=process alloc_mb="));
                Assert.That(after.FormatAbsolute(), Does.Contain("thread_alloc_scope=current_thread thread_alloc_mb="));
                Assert.That(after.FormatDelta(before), Does.Contain("thread_alloc_delta_mb="));
                Assert.That(after.FormatDelta(before), Does.Not.Contain("thread_alloc_delta_mb=unavailable"));
                Assert.That(after.FormatDelta(before), Does.Not.Contain("heap_delta_mb="));
                Assert.That(after.FormatDelta(before), Does.Not.Contain("ws_delta_mb="));
            });
        }

        [Test]
        public void TestCrossThreadAllocationDeltaUnavailable()
        {
            var before = new PerformancePhaseLogger.Snapshot(1024, 2048, 0, 0, 0, 0, GCLatencyMode.Interactive, 1, "First");
            var after = before with { AllocatedBytes = 4096, ThreadAllocatedBytes = 8192, ThreadId = 2, ThreadName = "Second" };

            string delta = after.FormatDelta(before);
            Assert.Multiple(() =>
            {
                Assert.That(delta, Does.Contain("alloc_scope=process alloc_delta_mb="));
                Assert.That(delta, Does.Contain("thread_alloc_delta_mb=unavailable"));
                Assert.That(delta, Does.Not.Contain("thread_alloc_delta_mb=0.006"));
            });
        }

        [Test]
        public void TestDisabledBeginDoesNotCaptureOrLog()
        {
            PerformancePhaseLogger.Enabled = false;
            int entries = 0;
            void capture(LogEntry entry)
            {
                if (entry.Message.Contains("[Phase::"))
                    Interlocked.Increment(ref entries);
            }

            Logger.NewEntry += capture;
            try
            {
                var first = PerformancePhaseLogger.Begin("disabled");
                var second = PerformancePhaseLogger.Begin("disabled again");
                Assert.That(second, Is.SameAs(first));
                first.SetEndDetail("ignored");
                first.End();
                second.Dispose();
            }
            finally
            {
                Logger.NewEntry -= capture;
            }

            Assert.That(entries, Is.Zero);
        }
    }
}
