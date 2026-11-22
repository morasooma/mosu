using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    [NonParallelizable]
    public class BackgroundCalculationLimiterTest
    {
        [Test]
        public void TestCheckpointYieldsWithinOneCalculation()
        {
            using var lease = BackgroundCalculationLimiter.Enter(4, 10);
            var stopwatch = Stopwatch.StartNew();
            Thread.Sleep(12);
            BackgroundCalculationLimiter.Checkpoint();
            Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(30));
        }

        [Test]
        public void TestActualLazyDecoderUsesCalculationBudget()
        {
            var working = new DecodingWorkingBeatmap();
            using var lease = BackgroundCalculationLimiter.Enter(4, 10);
            var timer = Stopwatch.StartNew();
            Assert.That(working.Beatmap.HitObjects.Count, Is.EqualTo(1000));
            Assert.That(timer.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(40), "The dedicated decoder thread must inherit the caller's budget.");
        }

        [Test]
        public async Task TestInteractiveUseDefersOnlyOptedInMaintenanceAndResumes()
        {
            BackgroundCalculationLimiter.DeferForInteractiveUse(250);
            using var started = new ManualResetEventSlim();
            using var acquired = new ManualResetEventSlim();
            Task maintenance = Task.Run(() =>
            {
                started.Set();
                using var slot = BackgroundCalculationLimiter.Enter(2, 100, deferWhileInteracting: true);
                acquired.Set();
            });

            Assert.That(started.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(acquired.Wait(TimeSpan.FromMilliseconds(50)), Is.False);
            using (BackgroundCalculationLimiter.Enter(2, 100))
                BackgroundCalculationLimiter.Checkpoint();
            await maintenance.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(acquired.IsSet, Is.True);
        }

        [Test]
        public async Task TestCancellationReleasesInteractiveWaitSlot()
        {
            using var started = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource();
            BackgroundCalculationLimiter.DeferForInteractiveUse(1000);
            Task maintenance = Task.Run(() =>
            {
                started.Set();
                using var slot = BackgroundCalculationLimiter.Enter(1, 100, cancellationToken: cancellation.Token, deferWhileInteracting: true);
            });
            Assert.That(started.Wait(TimeSpan.FromSeconds(5)), Is.True);
            cancellation.Cancel();
            Assert.ThrowsAsync<OperationCanceledException>(async () => await maintenance.WaitAsync(TimeSpan.FromSeconds(5)));
            // A cancelled acquisition must not keep the only concurrency slot checked out.
            await Task.Run(() =>
            {
                using var slot = BackgroundCalculationLimiter.Enter(1, 100);
                BackgroundCalculationLimiter.Checkpoint();
            }).WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task TestCancellationWhileWaitingForConcurrencyDoesNotAcquireSlot()
        {
            using var started = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource();
            using (BackgroundCalculationLimiter.Enter(1, 100))
            {
                Task waiter = Task.Run(() =>
                {
                    started.Set();
                    using var slot = BackgroundCalculationLimiter.Enter(1, 100, cancellationToken: cancellation.Token);
                });
                Assert.That(started.Wait(TimeSpan.FromSeconds(5)), Is.True);
                cancellation.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>(async () => await waiter.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            await Task.Run(() =>
            {
                using var slot = BackgroundCalculationLimiter.Enter(1, 100);
            }).WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task TestActiveCalculationFinishesDespiteInteractiveDeferral()
        {
            using var checkpoint = new ManualResetEventSlim();
            using var complete = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task maintenance = Task.Run(() =>
            {
                using var slot = BackgroundCalculationLimiter.Enter(4, 100, cancellationToken: cancellation.Token, deferWhileInteracting: true);
                BackgroundCalculationLimiter.DeferForInteractiveUse(1000);
                checkpoint.Set();
                BackgroundCalculationLimiter.Checkpoint();
                complete.Set();
            });
            try
            {
                Assert.That(checkpoint.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(complete.Wait(TimeSpan.FromMilliseconds(500)), Is.True, "Never suspend a shared lazy decoder mid-map.");
                await maintenance.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.That(complete.IsSet, Is.True);
            }
            finally
            {
                cancellation.Cancel();
                try { await maintenance; }
                catch (OperationCanceledException) { }
            }
        }

        [Test]
        public async Task TestShorterDeferralDoesNotShortenExistingDeadline()
        {
            BackgroundCalculationLimiter.DeferForInteractiveUse(200);
            BackgroundCalculationLimiter.DeferForInteractiveUse(0);
            using var acquired = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task maintenance = Task.Run(() =>
            {
                using var slot = BackgroundCalculationLimiter.Enter(4, 100, cancellationToken: cancellation.Token, deferWhileInteracting: true);
                acquired.Set();
            });
            try
            {
                Assert.That(acquired.Wait(TimeSpan.FromMilliseconds(50)), Is.False);
                await maintenance.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                cancellation.Cancel();
                try { await maintenance; }
                catch (OperationCanceledException) { }
            }
        }

        [Test]
        public async Task TestLazyDecoderContextDoesNotInheritAdmissionPause()
        {
            using var cancellation = new CancellationTokenSource();
            using var entered = new ManualResetEventSlim();
            using (BackgroundCalculationLimiter.Enter(4, 100, cancellationToken: cancellation.Token, deferWhileInteracting: true))
            {
                BackgroundCalculationLimiter.Context context = BackgroundCalculationLimiter.CaptureContext();
                BackgroundCalculationLimiter.DeferForInteractiveUse(1000);
                Task decoder = Task.Run(() => context.Run(() =>
                {
                    entered.Set();
                    BackgroundCalculationLimiter.Checkpoint();
                    return true;
                }));
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                await decoder.WaitAsync(TimeSpan.FromMilliseconds(500));
            }
            await Task.Run(() => BackgroundCalculationLimiter.Checkpoint()).WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task TestPausedMaintenanceDoesNotBlockImportConcurrencySlot()
        {
            using var ready = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task maintenance = Task.Run(() =>
            {
                BackgroundCalculationLimiter.DeferForInteractiveUse(1000);
                ready.Set();
                using var slot = BackgroundCalculationLimiter.Enter(1, 100, "osu", 1, cancellation.Token, deferWhileInteracting: true);
            });
            try
            {
                Assert.That(ready.Wait(TimeSpan.FromSeconds(5)), Is.True);
                await Task.Run(() =>
                {
                    using var slot = BackgroundCalculationLimiter.Enter(1, 100, "osu", 1, cancellation.Token);
                }).WaitAsync(TimeSpan.FromMilliseconds(500));
                Assert.That(maintenance.IsCompleted, Is.False, "Import ran while maintenance was still deferred.");
            }
            finally
            {
                cancellation.Cancel();
                try { await maintenance; }
                catch (OperationCanceledException) { }
            }
        }

        [Test]
        public async Task TestInteractionBeginsWhileMaintenanceWaitsForCapacity()
        {
            using var started = new ManualResetEventSlim();
            using var acquired = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task maintenance;
            using (BackgroundCalculationLimiter.Enter(1, 100))
            {
                maintenance = Task.Run(() =>
                {
                    started.Set();
                    using var slot = BackgroundCalculationLimiter.Enter(1, 100, cancellationToken: cancellation.Token, deferWhileInteracting: true);
                    acquired.Set();
                });
                Assert.That(started.Wait(TimeSpan.FromSeconds(5)), Is.True);
                BackgroundCalculationLimiter.DeferForInteractiveUse(200);
            }
            try
            {
                Assert.That(acquired.Wait(TimeSpan.FromMilliseconds(50)), Is.False);
                await maintenance.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                cancellation.Cancel();
                try { await maintenance; }
                catch (OperationCanceledException) { }
            }
        }

        private class DecodingWorkingBeatmap : TestWorkingBeatmap
        {
            public DecodingWorkingBeatmap() : base(new Beatmap()) { }

            protected override IBeatmap GetBeatmap()
            {
                var text = new StringBuilder("osu file format v14\n[General]\nMode:0\n[Difficulty]\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,500,4,2,0,100,1,0\n[HitObjects]\n");
                for (int i = 0; i < 1000; i++)
                    text.AppendLine($"128,192,{i * 250},1,0,0:0:0:0:");
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
                using var reader = new LineBufferedReader(stream);
                // Make the first real parser checkpoint yield deterministically.
                Thread.Sleep(15);
                return new LegacyBeatmapDecoder().Decode(reader);
            }
        }

        [Test]
        public void TestLazyDecodeInheritsBudgetAndRestoresWorkerContext()
        {
            using var lease = BackgroundCalculationLimiter.Enter(4, 10);
            BackgroundCalculationLimiter.Context context = BackgroundCalculationLimiter.CaptureContext();

            Task.Run(() =>
            {
                var timer = Stopwatch.StartNew();
                context.Run(() =>
                {
                    Thread.Sleep(15);
                    BackgroundCalculationLimiter.Checkpoint();
                    return true;
                });
                Assert.That(timer.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(40));

                // Leaving the scoped decoder must not throttle later unrelated worker work.
                Thread.Sleep(15);
                timer.Restart();
                BackgroundCalculationLimiter.Checkpoint();
                Assert.That(timer.ElapsedMilliseconds, Is.LessThan(40));
            }).GetAwaiter().GetResult();
        }

        [Test]
        public async Task TestModeLimitDoesNotBlockOtherModes()
        {
            using var first = BackgroundCalculationLimiter.Enter(4, 100, "osu", 1);
            using var started = new ManualResetEventSlim();
            using var acquired = new ManualResetEventSlim();
            Task waiter = Task.Run(() =>
            {
                started.Set();
                using var second = BackgroundCalculationLimiter.Enter(4, 100, "osu", 1);
                acquired.Set();
            });
            try
            {
                Assert.That(started.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(acquired.Wait(TimeSpan.FromMilliseconds(150)), Is.False);
                using var otherMode = BackgroundCalculationLimiter.Enter(4, 100, "taiko", 1);
                Assert.That(acquired.IsSet, Is.False);
            }
            finally
            {
                first.Dispose();
            }
            await waiter.WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task TestConcurrencyLimitAndRelease()
        {
            using var first = BackgroundCalculationLimiter.Enter(1, 50);
            using var started = new ManualResetEventSlim();
            using var acquired = new ManualResetEventSlim();

            Task waiter = Task.Run(() =>
            {
                started.Set();
                using var second = BackgroundCalculationLimiter.Enter(1, 50);
                acquired.Set();
            });

            try
            {
                Assert.That(started.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(acquired.Wait(TimeSpan.FromMilliseconds(150)), Is.False);
            }
            finally
            {
                first.Dispose();
            }

            await waiter.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(acquired.IsSet, Is.True);
        }
    }
}
