using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Beatmaps;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BackgroundCalculationFailuresTest
    {
        [Test]
        public void TestFailureSurvivesRestartAndDoesNotExpire()
        {
            using var storage = new TemporaryNativeStorage($"calculation-failures-{Guid.NewGuid():N}");
            var id = Guid.NewGuid();
            // Preserve old timeout records, including ones older than the former 24-hour backoff.
            File.WriteAllText(storage.GetFullPath("background-pp-timeouts.json", true), JsonConvert.SerializeObject(
                new Dictionary<string, DateTimeOffset> { [$"{id:N}:checksum"] = DateTimeOffset.UtcNow.AddYears(-5) }));
            var restarted = new BackgroundCalculationFailures(storage);
            Assert.That(restarted.ShouldSkip(id, "checksum"), Is.True);
            Assert.That(restarted.ShouldSkip(id, "changed"), Is.False);
            Assert.That(restarted.ShouldSkip(Guid.NewGuid(), "checksum"), Is.False);
            // Recording another failure must not prune an old one.
            restarted.Record(Guid.NewGuid(), "other", new InvalidDataException(), false);
            Assert.That(new BackgroundCalculationFailures(storage).ShouldSkip(id, "checksum"), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestErrorsAndTimeoutsPersist(bool timeout)
        {
            using var storage = new TemporaryNativeStorage($"calculation-record-{Guid.NewGuid():N}");
            var id = Guid.NewGuid();
            var failures = new BackgroundCalculationFailures(storage);
            Assert.That(failures.ShouldSkip(id, "checksum"), Is.False);
            failures.Record(id, "checksum", timeout ? new OperationCanceledException() : new InvalidDataException(), false);
            Assert.That(new BackgroundCalculationFailures(storage).ShouldSkip(id, "checksum"), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestUserCancellationAndShutdownDoNotMarkMapAsFailed(bool timeout)
        {
            using var storage = new TemporaryNativeStorage($"calculation-cancel-{Guid.NewGuid():N}");
            var id = Guid.NewGuid();
            var failures = new BackgroundCalculationFailures(storage);
            failures.Record(id, "checksum", timeout ? new OperationCanceledException() : new InvalidDataException(), true);
            Assert.That(new BackgroundCalculationFailures(storage).ShouldSkip(id, "checksum"), Is.False);
        }

        [Test]
        public void TestDisposedDependencyDoesNotMarkMapAsFailed()
        {
            using var storage = new TemporaryNativeStorage($"calculation-dispose-{Guid.NewGuid():N}");
            var id = Guid.NewGuid();
            new BackgroundCalculationFailures(storage).Record(id, "checksum", new ObjectDisposedException("dependency"), false);
            Assert.That(new BackgroundCalculationFailures(storage).ShouldSkip(id, "checksum"), Is.False);
        }

        [TestCase("none", true)]
        [TestCase("shutdown", false)]
        [TestCase("queue", false)]
        [TestCase("notification", false)]
        public void TestProcessorRecordsOnlyActualCalculationFailures(string cancellation, bool expected)
        {
            using var storage = new TemporaryNativeStorage($"calculation-processor-{Guid.NewGuid():N}");
            var failures = new BackgroundCalculationFailures(storage);
            using var processor = new BackgroundDataStoreProcessor();
            var type = typeof(BackgroundDataStoreProcessor);
            const BindingFlags instanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("calculationFailures", instanceFields)!.SetValue(processor, failures);
            var worker = type.GetField("libraryWorker", BindingFlags.Static | BindingFlags.NonPublic)!;
            bool previous = (bool)worker.GetValue(null)!;
            var notification = new ProgressNotification();
            var beatmap = new BeatmapInfo { MD5Hash = "checksum" };
            try
            {
                worker.SetValue(null, cancellation == "queue");
                if (cancellation is "shutdown" or "queue")
                {
                    string field = cancellation == "shutdown" ? "shutdownCancellation" : "processingCancellation";
                    ((CancellationTokenSource)type.GetField(field, instanceFields)!.GetValue(processor)!).Cancel();
                }
                if (cancellation == "notification")
                    notification.State = ProgressNotificationState.Cancelled;
                type.GetMethod("recordCalculationFailure", instanceFields)!.Invoke(processor,
                    new object[] { beatmap, new OperationCanceledException(), notification });
                Assert.That(new BackgroundCalculationFailures(storage).ShouldSkip(beatmap.ID, beatmap.MD5Hash), Is.EqualTo(expected));
            }
            finally
            {
                worker.SetValue(null, previous);
            }
        }

        [Test]
        public void TestConcurrentFailuresAreAllPersisted()
        {
            using var storage = new TemporaryNativeStorage($"calculation-parallel-{Guid.NewGuid():N}");
            var failures = new BackgroundCalculationFailures(storage);
            var ids = new Guid[16];
            Parallel.For(0, ids.Length, i =>
            {
                ids[i] = Guid.NewGuid();
                failures.Record(ids[i], "checksum", new InvalidDataException(), false);
            });
            var restarted = new BackgroundCalculationFailures(storage);
            foreach (var id in ids)
                Assert.That(restarted.ShouldSkip(id, "checksum"), Is.True);
        }

        [Test]
        public void TestCorruptOptionalCacheDoesNotBlockCalculations()
        {
            using var storage = new TemporaryNativeStorage($"calculation-corrupt-{Guid.NewGuid():N}");
            File.WriteAllText(storage.GetFullPath("background-pp-timeouts.json", true), "not json");
            var id = Guid.NewGuid();
            var failures = new BackgroundCalculationFailures(storage);
            Assert.That(failures.ShouldSkip(id, "checksum"), Is.False);
            failures.Record(id, "checksum", new InvalidDataException(), false);
            Assert.That(new BackgroundCalculationFailures(storage).ShouldSkip(id, "checksum"), Is.True);
        }
    }
}
