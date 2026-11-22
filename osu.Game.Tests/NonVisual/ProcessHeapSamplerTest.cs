using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Performance;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ProcessHeapSamplerTest
    {
        [Test]
        public async Task TestDirectHeapPollVersusCachedReadTiming()
        {
            using var sampler = new ProcessHeapSampler(TimeSpan.FromMilliseconds(500));
            var deadline = Stopwatch.StartNew();
            while (sampler.Latest == null && deadline.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(10);
            Assert.That(sampler.Latest, Is.Not.Null);

            var direct = new double[100];
            var cached = new double[100];
            for (int i = 0; i < direct.Length; i++)
            {
                long start = Stopwatch.GetTimestamp();
                GC.KeepAlive(GC.GetTotalMemory(false));
                direct[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                start = Stopwatch.GetTimestamp();
                GC.KeepAlive(sampler.Latest);
                cached[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            Array.Sort(direct);
            Array.Sort(cached);
            TestContext.Progress.WriteLine($"100 direct GetTotalMemory(false): median={direct[50]:F4}ms max={direct[^1]:F4}ms; 100 cached reads: median={cached[50]:F4}ms max={cached[^1]:F4}ms; sampler period=500ms (versus prior 100ms update poll)");
        }

        [Test]
        public async Task TestSnapshotsArePublishedThenStopOnDispose()
        {
            var sampler = new ProcessHeapSampler(TimeSpan.FromMilliseconds(25));
            try
            {
                Assert.That(sampler.Latest, Is.Null);
                var deadline = Stopwatch.StartNew();
                while (sampler.Latest == null && deadline.Elapsed < TimeSpan.FromSeconds(5))
                    await Task.Delay(10);
                var snapshot = sampler.Latest;
                Assert.That(snapshot, Is.Not.Null);
                Assert.That(snapshot!.Timestamp, Is.GreaterThan(0));
                Assert.That(snapshot.GcHeapMb, Is.GreaterThan(0));
                Assert.That(snapshot.WorkingSetMb, Is.GreaterThan(0));
                Assert.That(sampler.Failure, Is.Null);
            }
            finally { sampler.Dispose(); }
            await sampler.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            var stopped = sampler.Latest;
            await Task.Delay(70);
            Assert.That(sampler.Latest, Is.SameAs(stopped));
            sampler.Dispose();
        }
    }
}
