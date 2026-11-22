using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LivePerformanceMissSnapshotTest
    {
        [Test]
        public void CopiesOnlyMissesAndDoesNotMutateQueuedSnapshots()
        {
            var events = new List<HitEvent> { hit(1, HitResult.Great), hit(2, HitResult.Miss), hit(3, HitResult.Great) };
            var tracker = new LivePerformanceMissSnapshot();
            var first = tracker.Capture(events);
            events.Add(hit(4, HitResult.Miss));
            var second = tracker.Capture(events);
            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(second.Count, Is.EqualTo(2));
            Assert.That(second.All(e => e.Result == HitResult.Miss), Is.True);
            Assert.That(second[0].HitObject, Is.SameAs(events[1].HitObject));
            Assert.That(second[0].Position, Is.EqualTo(events[1].Position));
            Assert.That(first, Is.Not.SameAs(second));
        }

        [Test]
        public void RewindAndReplacementCannotReuseOldMisses()
        {
            var events = Enumerable.Range(0, 10).Select(i => hit(i, i % 2 == 0 ? HitResult.Miss : HitResult.Great)).ToList();
            var tracker = new LivePerformanceMissSnapshot();
            var before = tracker.Capture(events);
            events.RemoveRange(4, 6);
            Assert.That(tracker.Capture(events).Count, Is.EqualTo(2));
            events.Add(hit(20, HitResult.Miss));
            Assert.That(tracker.Capture(events).Count, Is.EqualTo(3));
            // Explicit invalidation is called on every reverted judgement, even if playback catches
            // up to the old event count before the next throttled snapshot.
            events[1] = hit(21, HitResult.Miss);
            tracker.Invalidate();
            Assert.That(tracker.Capture(events).Count, Is.EqualTo(4));
            Assert.That(before.Count, Is.EqualTo(5));
            events.Clear();
            Assert.That(tracker.Capture(events), Is.Empty);
        }

        [Test]
        public void ReplacedCursorIsDetectedWithoutExplicitInvalidation()
        {
            var events = new List<HitEvent> { hit(1, HitResult.Miss), hit(2, HitResult.Great) };
            var tracker = new LivePerformanceMissSnapshot();
            tracker.Capture(events);
            events.Clear();
            events.Add(hit(3, HitResult.Great));
            events.Add(hit(4, HitResult.Miss));
            events.Add(hit(5, HitResult.Great));
            var result = tracker.Capture(events);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].HitObject, Is.SameAs(events[1].HitObject));
        }

        [TestCase(1000)]
        [TestCase(100000)]
        public void SnapshotAllocationDependsOnMissesNotSuccessfulHits(int hitCount)
        {
            var events = Enumerable.Repeat(hit(1, HitResult.Great), hitCount).ToList();
            events[5] = hit(5, HitResult.Miss);
            events[^1] = hit(hitCount, HitResult.Miss);
            var tracker = new LivePerformanceMissSnapshot();
            tracker.Capture(events);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var result = tracker.Capture(events);
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            TestContext.WriteLine($"{hitCount} hits, 2 misses: snapshot allocated {allocated} bytes");
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(allocated, Is.LessThan(2048), "Queued snapshots must not copy the entire hit-event history.");
        }

        private static HitEvent hit(int time, HitResult result)
            => new HitEvent(0, 1, result, new HitCircle { StartTime = time, Position = new Vector2(time, 100) }, null, new Vector2(time + 10, 100));
    }
}
