using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;

namespace osu.Game.Tests.Rulesets.Scoring
{
    [TestFixture]
    public class HitEventExtensionsTest
    {
        [Test]
        public void TestExactNullableHitEventShapeDoesNotCrash()
        {
            var syntheticEvent = new HitEvent(15.0, 1.0, HitResult.Great, null, null, null);

            Assert.DoesNotThrow(() => HitEventExtensions.AffectsUnstableRate(syntheticEvent));
            Assert.That(HitEventExtensions.AffectsUnstableRate(syntheticEvent), Is.True);

            var events = new List<HitEvent> { syntheticEvent };

            Assert.That(events.CalculateMedianHitError(), Is.EqualTo(15.0));
            Assert.That(events.CalculateAverageHitError(), Is.EqualTo(15.0));
            Assert.That(events.CalculateUnstableRate()?.Result, Is.EqualTo(0.0));
        }

        [Test]
        public void TestWithoutHitObjectReferencesPreservesGameplayRateAndPosition()
        {
            var original = new HitEvent(12.5, 1.5, HitResult.Great, new HitObject(), null, osuTK.Vector2.One);
            var detached = original.WithoutHitObjectReferences();

            Assert.That(detached.HitObject, Is.Null);
            Assert.That(detached.LastHitObject, Is.Null);
            Assert.That(detached.TimeOffset, Is.EqualTo(12.5));
            Assert.That(detached.GameplayRate, Is.EqualTo(1.5));
            Assert.That(detached.Result, Is.EqualTo(HitResult.Great));
            Assert.That(detached.Position, Is.EqualTo(osuTK.Vector2.One));

            Assert.That(HitEventExtensions.AffectsUnstableRate(detached), Is.True);
        }

        [TestCase(HitResult.Perfect)]
        [TestCase(HitResult.Great)]
        [TestCase(HitResult.Good)]
        [TestCase(HitResult.Ok)]
        [TestCase(HitResult.Meh)]
        public void TestLegitimateHitEventsAffectUnstableRateWhenDetached(HitResult result)
        {
            var ev = new HitEvent(0, 1.0, result, null, null, null);
            Assert.That(HitEventExtensions.AffectsUnstableRate(ev), Is.True);
        }

        [TestCase(HitResult.Perfect)]
        [TestCase(HitResult.Great)]
        [TestCase(HitResult.Good)]
        [TestCase(HitResult.Ok)]
        [TestCase(HitResult.Meh)]
        public void TestLegitimateHitEventsAffectUnstableRateWithHitWindows(HitResult result)
        {
            var ev = new HitEvent(0, 1.0, result, new HitObject(), null, null);
            Assert.That(HitEventExtensions.AffectsUnstableRate(ev), Is.True);
        }

        [TestCase(HitResult.Miss)]
        [TestCase(HitResult.None)]
        [TestCase(HitResult.IgnoreMiss)]
        [TestCase(HitResult.IgnoreHit)]
        [TestCase(HitResult.ComboBreak)]
        [TestCase(HitResult.SmallBonus)]
        [TestCase(HitResult.LargeBonus)]
        [TestCase(HitResult.SmallTickHit)]
        [TestCase(HitResult.LargeTickHit)]
        [TestCase(HitResult.SliderTailHit)]
        public void TestExcludedEventsRemainExcludedWhenDetached(HitResult result)
        {
            var ev = new HitEvent(10.0, 1.0, result, null, null, null);
            Assert.That(HitEventExtensions.AffectsUnstableRate(ev), Is.False);
        }

        [Test]
        public void TestEmptyHitWindowsRemainExcludedEvenWithBasicResult()
        {
            var emptyWindowsObject = new HitObject { HitWindows = HitWindows.Empty };

            Assert.Multiple(() =>
            {
                Assert.That(HitEventExtensions.AffectsUnstableRate(new HitEvent(0, 1.0, HitResult.Meh, emptyWindowsObject, null, null)), Is.False);
                Assert.That(HitEventExtensions.AffectsUnstableRate(new HitEvent(0, 1.0, HitResult.Great, emptyWindowsObject, null, null)), Is.False);
                Assert.That(HitEventExtensions.AffectsUnstableRate(new HitEvent(0, 1.0, HitResult.SmallBonus, emptyWindowsObject, null, null)), Is.False);
                Assert.That(HitEventExtensions.AffectsUnstableRate(new HitEvent(0, 1.0, HitResult.Miss, emptyWindowsObject, null, null)), Is.False);
            });
        }

        [Test]
        public void TestCalculateMedianAndAverageHitErrorWithDetachedEvents()
        {
            // 50 legitimate timed hit events with offsets -24..25
            var events = Enumerable.Range(-24, 50)
                                   .Select(i => new HitEvent(i * 2.0, 1.0, HitResult.Great, null, null, null))
                                   .ToList();

            // Insert excluded events that should not affect median or average
            events.Insert(5, new HitEvent(500.0, 1.0, HitResult.Miss, null, null, null));
            events.Insert(15, new HitEvent(999.0, 1.0, HitResult.SmallBonus, null, null, null));
            events.Insert(25, new HitEvent(-999.0, 1.0, HitResult.LargeTickHit, null, null, null));

            double? median = events.CalculateMedianHitError();
            double? average = events.CalculateAverageHitError();

            Assert.That(median, Is.Not.Null);
            Assert.That(average, Is.Not.Null);

            // Time offsets: -48, -46, ..., 50 (50 items).
            // Middle items are index 24 (offset 0) and 25 (offset 2), median average is 1.0
            Assert.That(median!.Value, Is.EqualTo(1.0).Within(0.001));
            Assert.That(average!.Value, Is.EqualTo(1.0).Within(0.001));
        }

        [Test]
        public void TestDetachedHitEventsMatchOriginalScoreCalculationsAcrossDeepCloneForSessionState()
        {
            var hitObject = new HitObject();
            var emptyWindowsHitObject = new HitObject { HitWindows = HitWindows.Empty };

            var originalEvents = new List<HitEvent>
            {
                new HitEvent(-20.0, 1.0, HitResult.Great, hitObject, null, null),
                new HitEvent(-10.0, 1.0, HitResult.Good, hitObject, null, null),
                new HitEvent(0.0, 1.0, HitResult.Perfect, hitObject, null, null),
                new HitEvent(10.0, 1.0, HitResult.Ok, hitObject, null, null),
                new HitEvent(20.0, 1.0, HitResult.Meh, hitObject, null, null),
                // Excluded events from gameplay (misses, ticks, bonuses)
                new HitEvent(-50.0, 1.0, HitResult.Miss, hitObject, null, null),
                new HitEvent(100.0, 1.0, HitResult.LargeTickHit, emptyWindowsHitObject, null, null),
                new HitEvent(200.0, 1.0, HitResult.SmallBonus, emptyWindowsHitObject, null, null),
            };

            var originalScore = new ScoreInfo
            {
                HitEvents = originalEvents,
            };

            double? originalMedian = originalScore.HitEvents.CalculateMedianHitError();
            double? originalAverage = originalScore.HitEvents.CalculateAverageHitError();
            var originalUr = originalScore.HitEvents.CalculateUnstableRate();

            Assert.That(originalMedian, Is.Not.Null);
            Assert.That(originalAverage, Is.Not.Null);
            Assert.That(originalUr, Is.Not.Null);

            var sessionScore = originalScore.DeepCloneForSessionState();

            Assert.That(sessionScore.HitEvents.All(e => e.HitObject == null), Is.True);
            Assert.That(sessionScore.HitEvents.All(e => e.LastHitObject == null), Is.True);

            double? sessionMedian = sessionScore.HitEvents.CalculateMedianHitError();
            double? sessionAverage = sessionScore.HitEvents.CalculateAverageHitError();
            var sessionUr = sessionScore.HitEvents.CalculateUnstableRate();

            Assert.That(sessionMedian, Is.EqualTo(originalMedian));
            Assert.That(sessionAverage, Is.EqualTo(originalAverage));
            Assert.That(sessionUr!.Result, Is.EqualTo(originalUr!.Result).Within(0.0001));
            Assert.That(sessionUr.EventCount, Is.EqualTo(originalUr.EventCount));
        }

        [Test]
        public void TestHitEventTimingDistributionGraphWithDetachedEvents()
        {
            var detachedEvents = new List<HitEvent>
            {
                new HitEvent(-10.0, 1.0, HitResult.Great, null, null, null),
                new HitEvent(0.0, 1.0, HitResult.Perfect, null, null, null),
                new HitEvent(10.0, 1.0, HitResult.Great, null, null, null),
                new HitEvent(50.0, 1.0, HitResult.Miss, null, null, null),
                new HitEvent(0.0, 1.0, HitResult.SmallBonus, null, null, null),
            };

            Assert.DoesNotThrow(() =>
            {
                _ = new HitEventTimingDistributionGraph(detachedEvents);
            });
        }
    }
}
