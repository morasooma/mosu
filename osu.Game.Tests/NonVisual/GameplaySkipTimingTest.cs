using NUnit.Framework;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class GameplaySkipTimingTest
    {
        [TestCase(1800, 18400)]
        [TestCase(600, 19000)]
        [TestCase(3000, 17200)]
        public void TestResumeBeforeNextNoteAppears(double preempt, double expected)
        {
            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = 9000 });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 20450, TimePreempt = preempt });
            var period = new BreakPeriod(10000, 20000);
            Assert.That(GameplaySkipTiming.GetBreakTarget(beatmap, period), Is.EqualTo(expected));
            Assert.That(expected, Is.LessThan(20450 - preempt));
        }

        [Test]
        public void TestNoSkipWhenReadWindowAlreadyStarted()
        {
            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = 12450, TimePreempt = 3000 });
            var period = new BreakPeriod(10000, 12000);
            Assert.That(GameplaySkipTiming.GetBreakTarget(beatmap, period), Is.LessThanOrEqualTo(period.StartTime));
        }

        [Test]
        public void TestNoSkippingAcrossAnActiveSlider()
        {
            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new Spinner { StartTime = 9000, Duration = 3000 });
            Assert.That(GameplaySkipTiming.GetBreakTarget(beatmap, new BreakPeriod(10000, 20000)), Is.EqualTo(10000));
        }

        [Test]
        public void TestDurationLimitMatchesServer()
        {
            var beatmap = new OsuBeatmap();
            Assert.That(GameplaySkipTiming.GetBreakTarget(beatmap, new BreakPeriod(10000, 620001)), Is.EqualTo(10000));
        }
    }
}
