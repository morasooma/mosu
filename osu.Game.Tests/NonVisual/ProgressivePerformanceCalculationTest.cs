using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ProgressivePerformanceCalculationTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestLatestPendingOnlyAndRewindSuppressesOldResult(bool explicitRewind)
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var completed = new ManualResetEventSlim();
            var cursor = new TestCursor(entered, release);
            var results = new List<double>();
            Exception? failure = null;
            using var worker = new ProgressivePerformanceCalculation(_ => cursor, new TestPerformance(), (result, _) =>
            {
                lock (results)
                    results.Add(result.Total);
                completed.Set();
            }, e => { failure = e; completed.Set(); });
            worker.Queue(new ScoreInfo(), 100, false);
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            worker.Queue(new ScoreInfo(), 200, false);
            worker.Queue(new ScoreInfo(), 300, false);
            worker.Queue(new ScoreInfo(), 50, explicitRewind);
            release.Set();
            Assert.That(completed.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(failure, Is.Null);
            lock (results)
                Assert.That(results, Is.EqualTo(new[] { 50.0 }));
            Assert.That(cursor.Queries, Is.EqualTo(new[] { 100.0, 50.0 }));
        }

        [Test]
        public void TestForwardResultIsPublishedRatherThanStarvingUnderContinuousRequests()
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var completed = new CountdownEvent(2);
            var cursor = new TestCursor(entered, release);
            var results = new List<double>();
            using var worker = new ProgressivePerformanceCalculation(_ => cursor, new TestPerformance(), (result, _) =>
            {
                lock (results)
                    results.Add(result.Total);
                completed.Signal();
            }, _ => completed.Signal());
            worker.Queue(new ScoreInfo(), 100, false);
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            worker.Queue(new ScoreInfo(), 200, false);
            worker.Queue(new ScoreInfo(), 300, false);
            release.Set();
            Assert.That(completed.Wait(TimeSpan.FromSeconds(5)), Is.True);
            lock (results)
                Assert.That(results, Is.EqualTo(new[] { 100.0, 300.0 }));
        }

        [Test]
        public void TestDisposalCancelsInFlightWithoutPublishing()
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var disposed = new ManualResetEventSlim();
            var cursor = new TestCursor(entered, release, disposed);
            int published = 0;
            var worker = new ProgressivePerformanceCalculation(_ => cursor, new TestPerformance(), (_, _) => Interlocked.Increment(ref published), _ => Interlocked.Increment(ref published));
            worker.Queue(new ScoreInfo(), 100, false);
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            worker.Dispose();
            Assert.That(disposed.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(published, Is.Zero);
        }

        private class TestCursor : IProgressiveDifficultyCalculator
        {
            private readonly ManualResetEventSlim entered;
            private readonly ManualResetEventSlim release;
            private readonly ManualResetEventSlim? disposed;
            public readonly List<double> Queries = new List<double>();

            public TestCursor(ManualResetEventSlim entered, ManualResetEventSlim release, ManualResetEventSlim? disposed = null)
            {
                this.entered = entered;
                this.release = release;
                this.disposed = disposed;
            }

            public DifficultyAttributes GetAttributesAtTime(double time, CancellationToken token = default)
            {
                Queries.Add(time);
                if (Queries.Count == 1)
                {
                    entered.Set();
                    release.Wait(token);
                }
                token.ThrowIfCancellationRequested();
                return new DifficultyAttributes { StarRating = time };
            }

            public void Dispose() => disposed?.Set();
        }

        private class TestPerformance : PerformanceCalculator
        {
            public TestPerformance() : base(null!) { }
            protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
                => new PerformanceAttributes { Total = attributes.StarRating };
        }
    }
}
