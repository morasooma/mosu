using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class HiddenFreezeFrameTest
    {
        [TestCase(0)]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(11)]
        public void TestApplicationOrder(float approachRate)
        {
            var hiddenFirst = createBeatmap(approachRate);
            var freezeFirst = createBeatmap(approachRate);
            var original = flatten(hiddenFirst).Select(o => (o.TimePreempt, o.TimeFadeIn)).ToArray();

            new OsuModHidden().ApplyToBeatmap(hiddenFirst);
            new OsuModFreezeFrame().ApplyToBeatmap(hiddenFirst);
            new OsuModFreezeFrame().ApplyToBeatmap(freezeFirst);
            new OsuModHidden().ApplyToBeatmap(freezeFirst);

            var expected = flatten(hiddenFirst).ToArray();
            var actual = flatten(freezeFirst).ToArray();
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            Assert.That(actual.OfType<SliderTick>(), Is.Not.Empty);
            Assert.That(actual.OfType<SliderRepeat>().Count(), Is.GreaterThan(3));

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(actual[i].TimePreempt, Is.EqualTo(expected[i].TimePreempt), $"Preempt at {i}");
                Assert.That(actual[i].TimeFadeIn, Is.EqualTo(expected[i].TimeFadeIn).Within(0.000001), $"Fade-in at {i}");
                double fadeIn = actual[i] is Slider ? original[i].TimeFadeIn : original[i].TimePreempt * OsuModHidden.FADE_IN_DURATION_MULTIPLIER;
                Assert.That(actual[i].TimeFadeIn, Is.EqualTo(fadeIn).Within(0.000001), $"Legacy fade-in at {i}");
            }

            Assert.That(actual[1].TimePreempt, Is.EqualTo(original[1].TimePreempt + 2000));
            Assert.That(actual.OfType<Spinner>().Single().TimePreempt, Is.EqualTo(expected.OfType<Spinner>().Single().TimePreempt));
            Assert.That(freezeFirst.HitObjects.OfType<HitCircle>().Last().TimePreempt,
                Is.EqualTo(hiddenFirst.HitObjects.OfType<HitCircle>().First().TimePreempt), "New combo resets the freeze offset");
        }

        [Test]
        public void TestReapplicationDoesNotAccumulatePreempt()
        {
            var beatmap = createBeatmap(5);
            var mod = new OsuModFreezeFrame();
            mod.ApplyToBeatmap(beatmap);
            var preempts = flatten(beatmap).Select(o => o.TimePreempt).ToArray();
            mod.ApplyToBeatmap(beatmap);
            Assert.That(flatten(beatmap).Select(o => o.TimePreempt), Is.EqualTo(preempts));
        }

        [Test]
        public void TestDefaultsClearFreezeAdjustment()
        {
            var beatmap = createBeatmap(5);
            new OsuModFreezeFrame().ApplyToBeatmap(beatmap);
            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { ApproachRate = 10 });
            var preempts = flatten(beatmap).Select(o => o.TimePreempt).ToArray();
            new OsuModHidden().ApplyToBeatmap(beatmap);
            var objects = flatten(beatmap).ToArray();
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] is not Slider)
                    Assert.That(objects[i].TimeFadeIn, Is.EqualTo(preempts[i] * OsuModHidden.FADE_IN_DURATION_MULTIPLIER));
            }
        }

        [Test]
        public void TestModsAreCompatible()
        {
            Assert.That(new OsuModHidden().IncompatibleMods.Any(t => t.IsAssignableFrom(typeof(OsuModFreezeFrame))), Is.False);
            Assert.That(new OsuModFreezeFrame().IncompatibleMods.Any(t => t.IsAssignableFrom(typeof(OsuModHidden))), Is.False);
        }

        private static OsuBeatmap createBeatmap(float approachRate)
        {
            var beatmap = new OsuBeatmap
            {
                HitObjects =
                {
                    new HitCircle { StartTime = 3000, NewCombo = true },
                    new HitCircle { StartTime = 5000 },
                    new Slider
                    {
                        StartTime = 7000,
                        RepeatCount = 5,
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(300, 0) }),
                    },
                    new Spinner { StartTime = 20000, EndTime = 22000 },
                    new HitCircle { StartTime = 24000, NewCombo = true },
                }
            };
            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { ApproachRate = approachRate });
            return beatmap;
        }

        private static IEnumerable<OsuHitObject> flatten(OsuBeatmap beatmap)
            => beatmap.HitObjects.SelectMany(flattenObject);

        private static IEnumerable<OsuHitObject> flattenObject(OsuHitObject obj)
        {
            yield return obj;
            foreach (var nested in obj.NestedHitObjects.OfType<OsuHitObject>().SelectMany(flattenObject))
                yield return nested;
        }
    }
}
