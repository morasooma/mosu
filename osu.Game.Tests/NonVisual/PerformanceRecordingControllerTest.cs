using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Performance.Diagnostics;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class PerformanceRecordingControllerTest
    {
        [Test]
        public void TestSingleSwitchStartsStopsAndJoinsExactTask()
        {
            int starts = 0;
            CancellationToken token = default;
            var result = new TaskCompletionSource<string>();
            using var controller = new PerformanceRecordingController(t => { starts++; token = t; return result.Task; }, (_, _, _) => { });
            controller.SetEnabled(true);
            controller.SetEnabled(true);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(controller.Pending, Is.SameAs(result.Task));
            controller.SetEnabled(false);
            Assert.That(token.IsCancellationRequested, Is.True);
            Assert.That(controller.Pending, Is.SameAs(result.Task), "export must await finalisation, not read a partial trace");
            result.SetResult("capture.zip");
            controller.Update();
            Assert.That(controller.LastPath, Is.EqualTo("capture.zip"));
            Assert.That(controller.Pending, Is.Null);
        }

        [Test]
        public void TestRapidOffOnWaitsForOldCaptureToDrain()
        {
            var jobs = new List<TaskCompletionSource<string>>();
            using var controller = new PerformanceRecordingController(_ =>
            {
                var job = new TaskCompletionSource<string>();
                jobs.Add(job);
                return job.Task;
            }, (_, _, _) => { });
            controller.SetEnabled(true);
            controller.SetEnabled(false);
            controller.SetEnabled(true);
            Assert.That(jobs, Has.Count.EqualTo(1));
            jobs[0].SetResult("old.zip");
            controller.Update();
            Assert.That(jobs, Has.Count.EqualTo(2));
            Assert.That(controller.Pending, Is.SameAs(jobs[1].Task));
        }

        [Test]
        public void TestLimitCompletionDoesNotSilentlyRestart()
        {
            int starts = 0;
            bool unexpected = false;
            using var controller = new PerformanceRecordingController(_ => { starts++; return Task.FromResult("capped.zip"); }, (_, _, cap) => unexpected = cap);
            controller.SetEnabled(true);
            controller.Update();
            controller.Update();
            Assert.That(unexpected, Is.True);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(controller.LastPath, Is.EqualTo("capped.zip"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCaptureErrorsAreReportedWithoutRetryLoop(bool synchronous)
        {
            int starts = 0;
            Exception? reported = null;
            using var controller = new PerformanceRecordingController(_ =>
            {
                starts++;
                if (synchronous) throw new InvalidOperationException("test");
                return Task.FromException<string>(new InvalidOperationException("test"));
            }, (_, error, _) => reported = error);
            controller.SetEnabled(true);
            controller.Update();
            controller.Update();
            Assert.That(reported, Is.TypeOf<InvalidOperationException>());
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(controller.LastPath, Is.Null);
        }

        [Test]
        public void TestFailedNewRecordingDoesNotExportPreviousTrace()
        {
            int starts = 0;
            using var controller = new PerformanceRecordingController(_ => ++starts == 1
                ? Task.FromResult("old.zip") : Task.FromException<string>(new InvalidOperationException("new capture failed")), (_, _, _) => { });
            controller.SetEnabled(true);
            controller.Update();
            Assert.That(controller.LastPath, Is.EqualTo("old.zip"));
            controller.SetEnabled(true);
            Assert.That(controller.LastPath, Is.Null);
            controller.Update();
            Assert.That(controller.LastPath, Is.Null);
            Assert.That(controller.LastError, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public async Task TestExitJoinsExactCaptureWithoutAnotherUpdateOrRestart()
        {
            var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken token = default;
            int starts = 0;
            int callbacks = 0;
            using var controller = new PerformanceRecordingController(t => { token = t; starts++; return result.Task; }, (_, _, _) => callbacks++);
            controller.SetEnabled(true);
            controller.SetEnabled(false);
            controller.SetEnabled(true); // queued desired restart must be suppressed on exit
            Task<string?> stopping = controller.StopAsync();
            Assert.That(token.IsCancellationRequested, Is.True);
            Assert.That(stopping.IsCompleted, Is.False);
            Assert.That(controller.Pending, Is.SameAs(result.Task));
            result.SetResult("exit.zip");
            Assert.That(await stopping, Is.EqualTo("exit.zip"));
            controller.Update();
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [Test]
        public void TestExitSurfacesCaptureFailure()
        {
            using var controller = new PerformanceRecordingController(_ => Task.FromException<string>(new InvalidOperationException("drain failed")), (_, _, _) => { });
            controller.SetEnabled(true);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await controller.StopAsync());
        }

        [Test]
        public void TestDisposeCancelsWithoutStartingAgain()
        {
            CancellationToken token = default;
            int starts = 0;
            using var controller = new PerformanceRecordingController(t => { token = t; starts++; return new TaskCompletionSource<string>().Task; }, (_, _, _) => { });
            controller.SetEnabled(true);
            controller.Dispose();
            Assert.That(token.IsCancellationRequested, Is.True);
            controller.SetEnabled(true);
            controller.Update();
            Assert.That(starts, Is.EqualTo(1));
        }
    }
}
