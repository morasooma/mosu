using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Performance;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class PerformanceLoggerReviewFixesTest
    {
        [Test]
        public async Task TestSkinDirectoryAcquisitionRunsAfterProducerReturns()
        {
            string path = Path.Combine(Path.GetTempPath(), $"skin-audit-{Guid.NewGuid()}");
            var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                var storage = new NativeStorage(path);
                Task audit = PerformanceSkinAuditWorker.Schedule(waiting.Task, storage, files =>
                {
                    Assert.That(files.GetFullPath("image.png"), Does.Contain("files"));
                });
                Assert.That(Directory.Exists(Path.Combine(path, "files")), Is.False);
                waiting.SetResult();
                await audit.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.That(Directory.Exists(Path.Combine(path, "files")), Is.True);
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
        }

        [Test]
        public async Task TestSameBenchmarkArtifactsPublishInStopOrderDespiteSlowFirstDrain()
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            string path = Path.Combine(Path.GetTempPath(), $"benchmark-{Guid.NewGuid()}.summary.csv");
            string segmentsPath = Path.ChangeExtension(path, ".segments.csv");
            var publisher = new OrderedPerformanceArtifactPublisher();
            try
            {
                var first = new BoundedPerformanceFileSink(_ => new BlockingWriter(entered, release));
                first.TryWrite("first", "row");
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                publisher.Enqueue(first.Completion, () =>
                {
                    File.WriteAllText(path, "first");
                    File.WriteAllText(segmentsPath, "first");
                });
                first.Stop();
                var second = new BoundedPerformanceFileSink(_ => new StringWriter());
                second.TryWrite("second", "row");
                var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                publisher.Enqueue(second.Completion, () =>
                {
                    File.WriteAllText(path, "second");
                    File.WriteAllText(segmentsPath, "second");
                    published.SetResult();
                });
                second.Stop();
                await second.Completion.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.That(File.Exists(path), Is.False);
                Assert.That(File.Exists(segmentsPath), Is.False);
                release.Set();
                await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.That(File.ReadAllText(path), Is.EqualTo("second"));
                Assert.That(File.ReadAllText(segmentsPath), Is.EqualTo("second"));
            }
            finally
            {
                release.Set();
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(segmentsPath)) File.Delete(segmentsPath);
            }
        }

        [Test]
        public async Task TestSinkFailureReportedBeforeDelayedAudit()
        {
            var audit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sink = new BoundedPerformanceFileSink(_ => new FailingWriter());
            sink.TryWrite("row", "data");
            PerformanceDrainObserver.Observe(sink.Completion, () =>
            {
                Assert.That(sink.Failure, Is.TypeOf<IOException>());
                reported.SetResult();
            }, audit.Task, _ => Assert.Fail("unexpected audit failure"));
            sink.Stop();
            await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(audit.Task.IsCompleted, Is.False);
            audit.SetResult();
        }

        [Test]
        public async Task TestPublicationQueueIsBoundedWhileDrainIsBlocked()
        {
            var drain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var publisher = new OrderedPerformanceArtifactPublisher();
            int published = 0;
            try
            {
                Assert.That(publisher.Enqueue(drain.Task, () => { }), Is.True);
                int accepted = 0;
                for (int i = 0; i < 20; i++)
                {
                    if (publisher.Enqueue(drain.Task, () => Interlocked.Increment(ref published)))
                        accepted++;
                }

                Assert.That(accepted, Is.InRange(7, 8));
                drain.SetResult();
                // Wait for accepted publications without assuming when the worker dequeued the first item.
                for (int i = 0; i < 500 && Volatile.Read(ref published) != accepted; i++)
                    await Task.Delay(10);
                Assert.That(Volatile.Read(ref published), Is.EqualTo(accepted));
                Assert.That(publisher.Enqueue(Task.CompletedTask, () => completed.SetResult()), Is.True);
                await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                drain.TrySetResult();
            }
        }

        private sealed class FailingWriter : StringWriter
        {
            public override void Write(string? value) => throw new IOException("controlled failure");
        }

        private sealed class BlockingWriter(ManualResetEventSlim entered, ManualResetEventSlim release) : StringWriter
        {
            public override void Write(string? value)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                base.Write(value);
            }
        }
    }
}
