// Derived in part from osu! difficulty preprocessing. Copyright (c) ppy Pty Ltd.
// Licensed under the MIT Licence. See LICENCE in the repository root.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class OsuDifficultyPreparationTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestSliderTrackingMatchesOriginalCopyOrder(bool repeated)
        {
            var random = new Random(2971);
            int reorderedCases = 0;
            for (int n = 0; n < 160; n++)
            {
                var difficulty = new BeatmapDifficulty { CircleSize = n % 3 == 0 ? 7 : 4, SliderTickRate = n % 2 == 0 ? 8 : 1 };
                var slider = new Slider
                {
                    StartTime = 1000,
                    Position = new Vector2(100, 100),
                    RepeatCount = repeated ? 1 + n % 5 : 0,
                    SliderVelocityMultiplier = n % 2 == 0 ? 0.1 : 1,
                    Path = new SliderPath(n % 3 == 0 ? PathType.BEZIER : PathType.LINEAR, new[]
                    {
                        Vector2.Zero, new Vector2(random.Next(-200, 200), random.Next(-150, 150)), new Vector2(200, 100)
                    })
                };
                slider.ApplyDefaults(new ControlPointInfo(), difficulty);
                slider.StackHeight = n % 4 - 2;
                HitObject[] before = slider.NestedHitObjects.ToArray();
                var expected = originalTracking(slider);
                if (expected.reordered) reorderedCases++;
                var previous = new HitCircle { StartTime = 0, Position = new Vector2(256, 192) };
                previous.ApplyDefaults(new ControlPointInfo(), difficulty);
                var actual = new OsuDifficultyHitObject(slider, previous, 1, new List<DifficultyHitObject>(), 0);
                Assert.That(actual.LazyTravelTime, Is.EqualTo(expected.time), $"time {n}");
                Assert.That(actual.LazyTravelDistance, Is.EqualTo(expected.distance), $"distance {n}");
                Assert.That(actual.LazyEndPosition, Is.EqualTo(expected.end), $"position {n}");
                Assert.That(slider.NestedHitObjects, Is.EqualTo(before), $"source order {n}");
            }
            Assert.That(reorderedCases, Is.GreaterThan(0), "Must exercise the last-tick relocation branch.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReadingTransformationMatchesOriginalEveryPrefix(bool relaxProfile)
        {
            var reading = new ExposedReading(relaxProfile);
            var prepared = new List<DifficultyHitObject>();
            var times = new List<double>();
            var objects = new List<OsuHitObject>();
            double[] starts = { 0, 100, 60099.999, 60100, 60100.001, 70000, 200, 90000 };
            for (int i = 0; i < starts.Length; i++)
            {
                var circle = new HitCircle { StartTime = starts[i], Position = new Vector2(i * 50, 100) };
                circle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                objects.Add(circle);
                if (i > 0) prepared.Add(new OsuDifficultyHitObject(circle, objects[i - 1], 1, prepared, i - 1));
            }
            foreach (var obj in prepared)
            {
                reading.Process(obj);
                times.Add(obj.StartTime);
                var values = new List<double> { 0, -1, 2, 3, 0, 5, 8, 13, 21 };
                var expected = values.Where(v => v > 0).ToList();
                int reduced = relaxProfile ? times.Count : times.TakeWhile(t => !(t > times[0] + 60000)).Count();
                for (int i = 0; i < Math.Min(expected.Count, reduced); i++)
                {
                    double scale = Math.Log10(Interpolation.Lerp(1, 10, Math.Clamp((double)i / reduced, 0, 1)));
                    expected[i] *= Interpolation.Lerp(0.0, 1.0, scale);
                }
                Assert.That(reading.Transform(values), Is.EqualTo(expected), $"prefix {times.Count}");
                Assert.That(reading.Transform(values), Is.EqualTo(expected), "Repeated queries must not advance the prefix.");
            }
            Assert.That(new ExposedReading(relaxProfile).Transform(new List<double> { 1, 2 }), Is.EqualTo(new[] { 1d, 2d }));
        }

        private class ExposedReading : Reading
        {
            public ExposedReading(bool relaxProfile)
                : base(Array.Empty<Mod>(), relaxProfile)
            {
            }

            public List<double> Transform(List<double> values) => GetTransformedDifficulties(values);
        }

        // Preserve the previous allocation-heavy implementation as an independent
        // reference, including its intentional legacy tail and reordering rules.
        private static (double time, double distance, Vector2 end, bool reordered) originalTracking(Slider slider)
        {
            double trackingEndTime = Math.Max(slider.StartTime + slider.Duration + SliderEventGenerator.TAIL_LENIENCY,
                slider.StartTime + slider.Duration / 2);
            IList<HitObject> nested = slider.NestedHitObjects;
            SliderTick? lastTick = nested.OfType<SliderTick>().LastOrDefault();
            bool reordered = lastTick?.StartTime > trackingEndTime;
            if (reordered)
            {
                trackingEndTime = lastTick!.StartTime;
                var copy = nested.ToList();
                copy.Remove(lastTick);
                copy.Add(lastTick);
                nested = copy;
            }
            double time = trackingEndTime - slider.StartTime;
            double progress = time / slider.SpanDuration;
            if (progress % 2 >= 1) progress = 1 - progress % 1;
            else progress %= 1;
            Vector2 end = slider.StackedPosition + slider.Path.PositionAt(progress);
            Vector2 cursor = slider.StackedPosition;
            double signedRadius = OsuHitObject.OBJECT_RADIUS * LegacyRulesetExtensions.SanitizeSignedScale(slider.Scale);
            double scale = 50 / signedRadius;
            double distance = 0;
            for (int i = 1; i < nested.Count; i++)
            {
                var obj = (OsuHitObject)nested[i];
                Vector2 movement = obj.StackedPosition - cursor;
                double length = scale * movement.Length;
                double required = 50 * 1.8f;
                if (i == nested.Count - 1)
                {
                    Vector2 lazy = end - cursor;
                    if (lazy.Length < movement.Length) movement = lazy;
                    length = scale * movement.Length;
                }
                else if (obj is SliderRepeat) required = 50;
                if (length > required)
                {
                    cursor = Vector2.Add(cursor, Vector2.Multiply(movement, (float)((length - required) / length)));
                    length *= (length - required) / length;
                    distance += length;
                }
                if (i == nested.Count - 1) end = cursor;
            }
            return (time, distance, end, reordered);
        }
    }
}
