using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Scoring.Render;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ReplayRenderCatchUpTest
    {
        [TestCase(1500)]
        [TestCase(102000)]
        public async Task TestRepeatedClockValueDoesNotStartCapture(double target)
        {
            int updates = 0;
            await RenderReplayPlayer.WaitForInitialCatchUpAsync(target,
                _ => Task.FromResult(++updates <= 150 ? 0 : target), CancellationToken.None, TimeSpan.FromSeconds(5));

            Assert.That(updates, Is.EqualTo(151));
        }

        [Test]
        public async Task TestMustReachTargetNotJustWithinOneOutputFrame()
        {
            int updates = 0;
            await RenderReplayPlayer.WaitForInitialCatchUpAsync(102000,
                _ => Task.FromResult(++updates == 1 ? 101990.0 : 102000.0), CancellationToken.None, TimeSpan.FromSeconds(5));

            Assert.That(updates, Is.EqualTo(2));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public async Task TestNonFiniteClockCannotStartCapture(double invalidTime)
        {
            int updates = 0;
            await RenderReplayPlayer.WaitForInitialCatchUpAsync(102000,
                _ => Task.FromResult(++updates == 1 ? invalidTime : 102000), CancellationToken.None, TimeSpan.FromSeconds(5));

            Assert.That(updates, Is.EqualTo(2));
        }

        [Test]
        public void TestStalledSeekFailsInsteadOfStartingCapture()
        {
            var neverCompletes = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.ThrowsAsync<TimeoutException>(() => RenderReplayPlayer.WaitForInitialCatchUpAsync(102000,
                _ => neverCompletes.Task, CancellationToken.None, TimeSpan.FromMilliseconds(100)));
        }

        [Test]
        public void TestCancellationIsNotReportedAsTimeout()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.CatchAsync<OperationCanceledException>(() => RenderReplayPlayer.WaitForInitialCatchUpAsync(102000,
                _ => Task.FromCanceled<double>(cts.Token), cts.Token, TimeSpan.FromSeconds(5)));
        }
    }
}
