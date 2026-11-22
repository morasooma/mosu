using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class HiddenSegmentRateTest
    {
        [Test]
        public void TestHiddenTimingSurvivesTimelineConversion(
            [Values(false, true)] bool flatline,
            [Values(false, true)] bool baked,
            [Values(false, true)] bool hiddenFirst,
            [Values(5, 10)] float approachRate)
        {
            var raw = new Beatmap<OsuHitObject>();
            raw.Difficulty.ApproachRate = approachRate;
            raw.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            for (int i = 0; i < 32; i++)
                raw.HitObjects.Add(new HitCircle { StartTime = 2000 + i * 250, Position = new Vector2(100 + i % 2 * 30, 100) });
            raw.HitObjects.Add(new Slider
            {
                StartTime = 10000,
                Position = new Vector2(100),
                RepeatCount = 3,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(200, 0)) }, 200),
            });
            raw.HitObjects.Add(new Spinner { StartTime = 13000, Duration = 2000 });
            for (int i = 0; i < 40; i++)
                raw.HitObjects.Add(new HitCircle { StartTime = 16000 + i * 80, Position = new Vector2(70 + i % 2 * 350, 100 + i % 3 * 80) });
            raw.Breaks.Add(new BreakPeriod(11500, 12500));

            ModSegmentRate rate = flatline ? new OsuModFlatline() : new OsuModGlitch { Amount = { Value = 100 } };
            rate.BakedTiming.Value = baked;
            var hidden = new OsuModHidden();
            var working = new FlatWorkingBeatmap(raw);
            var playable = working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo,
                hiddenFirst ? new Mod[] { hidden, rate } : new Mod[] { rate, hidden });

            Assert.That(rate.RatePlan.Segments, Is.Not.Empty);
            var objects = playable.HitObjects.Cast<OsuHitObject>().SelectMany(flatten).ToArray();
            Assert.That(objects.OfType<SliderTick>(), Is.Not.Empty);
            Assert.That(objects.OfType<SliderRepeat>(), Is.Not.Empty);
            foreach (var obj in objects.Where(o => o is not Slider))
                Assert.That(obj.TimeFadeIn, Is.EqualTo(obj.TimePreempt * OsuModHidden.FADE_IN_DURATION_MULTIPLIER).Within(1e-6), obj.GetType().Name);

            var originalBreak = raw.Breaks.Single();
            var mappedBreak = playable.Breaks.Single();
            Assert.That(mappedBreak.StartTime, Is.EqualTo(baked ? rate.RatePlan.Map(originalBreak.StartTime) : originalBreak.StartTime).Within(1e-6));
            Assert.That(mappedBreak.EndTime, Is.EqualTo(baked ? rate.RatePlan.Map(originalBreak.EndTime) : originalBreak.EndTime).Within(1e-6));
            Assert.That(raw.Breaks.Single().StartTime, Is.EqualTo(11500));
        }

        private static IEnumerable<OsuHitObject> flatten(OsuHitObject obj)
        {
            yield return obj;
            foreach (var nested in obj.NestedHitObjects.OfType<OsuHitObject>().SelectMany(flatten))
                yield return nested;
        }
    }
}
