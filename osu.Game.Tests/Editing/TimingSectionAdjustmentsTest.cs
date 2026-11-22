// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.Timing;

namespace osu.Game.Tests.Editing
{
    [TestFixture]
    public class TimingSectionAdjustmentsTest
    {
        [Test]
        public void TestOffsetAdjustment()
        {
            var controlPoints = new ControlPointInfo();

            controlPoints.Add(100, new TimingControlPoint { BeatLength = 100 });
            controlPoints.Add(50_000, new TimingControlPoint { BeatLength = 200 });
            controlPoints.Add(100_000, new TimingControlPoint { BeatLength = 50 });

            var beatmap = new Beatmap
            {
                ControlPointInfo = controlPoints,
                HitObjects = new List<HitObject>
                {
                    new HitCircle { StartTime = 0 },
                    new HitCircle { StartTime = 200 },
                    new HitCircle { StartTime = 49_900 },
                    new HitCircle { StartTime = 50_000 },
                    new HitCircle { StartTime = 50_200 },
                    new HitCircle { StartTime = 99_800 },
                    new HitCircle { StartTime = 100_000 },
                    new HitCircle { StartTime = 100_050 },
                    new HitCircle { StartTime = 100_550 },
                }
            };

            moveTimingPoint(beatmap, 100, -50);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.HitObjects[0].StartTime, Is.EqualTo(-50));
                Assert.That(beatmap.HitObjects[1].StartTime, Is.EqualTo(150));
                Assert.That(beatmap.HitObjects[2].StartTime, Is.EqualTo(49_850));
                Assert.That(beatmap.HitObjects[3].StartTime, Is.EqualTo(50_000));
            });

            moveTimingPoint(beatmap, 50_000, 1_000);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.HitObjects[2].StartTime, Is.EqualTo(49_850));
                Assert.That(beatmap.HitObjects[3].StartTime, Is.EqualTo(51_000));
                Assert.That(beatmap.HitObjects[4].StartTime, Is.EqualTo(51_200));
                Assert.That(beatmap.HitObjects[5].StartTime, Is.EqualTo(100_800));
                Assert.That(beatmap.HitObjects[6].StartTime, Is.EqualTo(100_000));
            });

            moveTimingPoint(beatmap, 100_000, 10_000);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.HitObjects[4].StartTime, Is.EqualTo(51_200));
                Assert.That(beatmap.HitObjects[5].StartTime, Is.EqualTo(110_800));
                Assert.That(beatmap.HitObjects[6].StartTime, Is.EqualTo(110_000));
                Assert.That(beatmap.HitObjects[7].StartTime, Is.EqualTo(110_050));
                Assert.That(beatmap.HitObjects[8].StartTime, Is.EqualTo(110_550));
            });
        }

        [Test]
        public void TestBPMAdjustment()
        {
            var controlPoints = new ControlPointInfo();

            controlPoints.Add(100, new TimingControlPoint { BeatLength = 100 });
            controlPoints.Add(50_000, new TimingControlPoint { BeatLength = 200 });
            controlPoints.Add(100_000, new TimingControlPoint { BeatLength = 50 });

            var beatmap = new Beatmap
            {
                ControlPointInfo = controlPoints,
                HitObjects = new List<HitObject>
                {
                    new HitCircle { StartTime = 0 },
                    new HitCircle { StartTime = 200 },
                    new Spinner { StartTime = 500, EndTime = 1000 },
                    new HitCircle { StartTime = 49_900 },
                    new HitCircle { StartTime = 50_000 },
                    new HitCircle { StartTime = 50_200 },
                    new HitCircle { StartTime = 99_800 },
                    new HitCircle { StartTime = 100_000 },
                    new HitCircle { StartTime = 100_050 },
                    new HitCircle { StartTime = 100_550 },
                }
            };

            adjustBeatLength(beatmap, 100, 50);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.HitObjects[0].StartTime, Is.EqualTo(50));
                Assert.That(beatmap.HitObjects[1].StartTime, Is.EqualTo(150));
                Assert.That(beatmap.HitObjects[2].StartTime, Is.EqualTo(300));
                Assert.That(beatmap.HitObjects[2].GetEndTime(), Is.EqualTo(550));
                Assert.That(beatmap.HitObjects[3].StartTime, Is.EqualTo(25_000));
                Assert.That(beatmap.HitObjects[4].StartTime, Is.EqualTo(50_000));
            });

            adjustBeatLength(beatmap, 50_000, 400);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.HitObjects[2].StartTime, Is.EqualTo(300));
                Assert.That(beatmap.HitObjects[2].GetEndTime(), Is.EqualTo(550));
                Assert.That(beatmap.HitObjects[3].StartTime, Is.EqualTo(25_000));
                Assert.That(beatmap.HitObjects[4].StartTime, Is.EqualTo(50_000));
                Assert.That(beatmap.HitObjects[5].StartTime, Is.EqualTo(50_400));
                Assert.That(beatmap.HitObjects[6].StartTime, Is.EqualTo(149_600));
                Assert.That(beatmap.HitObjects[7].StartTime, Is.EqualTo(100_000));
            });

            adjustBeatLength(beatmap, 100_000, 100);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.HitObjects[5].StartTime, Is.EqualTo(50_400));
                Assert.That(beatmap.HitObjects[6].StartTime, Is.EqualTo(199_200));
                Assert.That(beatmap.HitObjects[7].StartTime, Is.EqualTo(100_000));
                Assert.That(beatmap.HitObjects[8].StartTime, Is.EqualTo(100_100));
                Assert.That(beatmap.HitObjects[9].StartTime, Is.EqualTo(101_100));
            });
        }

        [TestCase(50)]
        [TestCase(-50)]
        public void TestMovingTimingCarriesEffectsUntilNextTiming(double adjustment)
        {
            var beatmap = new Beatmap();
            var info = beatmap.ControlPointInfo;
            info.GroupAt(50, true).Add(new EffectControlPoint());
            info.GroupAt(100, true).Add(new TimingControlPoint());
            info.GroupAt(100, true).Add(new EffectControlPoint());
            info.GroupAt(200, true).Add(new EffectControlPoint());
            info.GroupAt(300, true).Add(new TimingControlPoint());
            info.GroupAt(300, true).Add(new EffectControlPoint());
            info.GroupAt(400, true).Add(new EffectControlPoint());
            var effects = info.EffectPoints.ToArray();

            TimingSectionAdjustments.MoveControlPointGroups(beatmap, new[] { info.GroupAt(100) }, adjustment, false);

            Assert.That(effects.Select(e => e.Time), Is.EqualTo(new[] { 50, 100 + adjustment, 200 + adjustment, 300, 400 }));
            Assert.That(info.TimingPoints.Select(t => t.Time), Is.EqualTo(new[] { 100 + adjustment, 300 }));
        }

        [TestCase(100)]
        [TestCase(-100)]
        public void TestOverlappingSelectedRangesMoveOnlyOnce(double adjustment)
        {
            var beatmap = new Beatmap();
            var info = beatmap.ControlPointInfo;
            foreach (double time in new[] { 100, 300, 500 })
                info.GroupAt(time, true).Add(new TimingControlPoint());
            foreach (double time in new[] { 200, 400, 600 })
                info.GroupAt(time, true).Add(new EffectControlPoint());
            var timing = info.TimingPoints.ToArray();
            var effects = info.EffectPoints.ToArray();
            beatmap.HitObjects.AddRange(new HitObject[]
            {
                new HitCircle { StartTime = 200 }, new HitCircle { StartTime = 300 }, new HitCircle { StartTime = 500 }
            });

            var moved = TimingSectionAdjustments.MoveControlPointGroups(beatmap,
                new[] { info.GroupAt(100), info.GroupAt(200), info.GroupAt(300), info.GroupAt(400) }, adjustment, true);

            Assert.That(timing.Select(t => t.Time), Is.EqualTo(new[] { 100 + adjustment, 300 + adjustment, 500 }));
            Assert.That(effects.Select(e => e.Time), Is.EqualTo(new[] { 200 + adjustment, 400 + adjustment, 600 }));
            Assert.That(beatmap.HitObjects.Select(h => h.StartTime), Is.EqualTo(new[] { 200 + adjustment, 300 + adjustment, 500 }));
            Assert.That(moved.Select(g => g.Time), Is.EqualTo(new[] { 100 + adjustment, 200 + adjustment, 300 + adjustment, 400 + adjustment }));
        }

        [Test]
        public void TestMovingEffectRowsDoesNotMoveTimingOrOtherEffects()
        {
            var beatmap = new Beatmap();
            var info = beatmap.ControlPointInfo;
            info.GroupAt(100, true).Add(new TimingControlPoint());
            foreach (double time in new[] { 200, 300, 400 })
                info.GroupAt(time, true).Add(new EffectControlPoint());
            var effects = info.EffectPoints.ToArray();

            TimingSectionAdjustments.MoveControlPointGroups(beatmap, new[] { info.GroupAt(200), info.GroupAt(400) }, 10, true);

            Assert.That(effects.Select(e => e.Time), Is.EqualTo(new[] { 210, 300, 410 }));
            Assert.That(info.TimingPoints.Single().Time, Is.EqualTo(100));
        }

        [Test]
        public void TestMovingTimingDoesNotMoveObjectsWhenDisabled()
        {
            var beatmap = new Beatmap();
            beatmap.ControlPointInfo.GroupAt(100, true).Add(new TimingControlPoint());
            beatmap.ControlPointInfo.GroupAt(200, true).Add(new EffectControlPoint());
            beatmap.HitObjects.Add(new HitCircle { StartTime = 200 });

            TimingSectionAdjustments.MoveControlPointGroups(beatmap, new[] { beatmap.ControlPointInfo.GroupAt(100) }, 20, false);

            Assert.That(beatmap.HitObjects.Single().StartTime, Is.EqualTo(200));
            Assert.That(beatmap.ControlPointInfo.EffectPoints.Single().Time, Is.EqualTo(220));
        }

        [Test]
        public void TestMovingOntoSelectedRowsPreservesIdenticalEffects()
        {
            var beatmap = new Beatmap();
            var info = beatmap.ControlPointInfo;
            var effects = new[] { new EffectControlPoint(), new EffectControlPoint(), new EffectControlPoint() };
            for (int i = 0; i < effects.Length; i++)
                info.GroupAt(i * 100, true).Add(effects[i]);

            TimingSectionAdjustments.MoveControlPointGroups(beatmap, info.Groups.ToArray(), 100, false);

            Assert.That(info.EffectPoints, Is.EqualTo(effects));
            Assert.That(info.Groups.Select(g => g.Time), Is.EqualTo(new[] { 100, 200, 300 }));
        }

        [Test]
        public void TestMovingEmptyRow()
        {
            var beatmap = new Beatmap();
            var group = beatmap.ControlPointInfo.GroupAt(100, true);
            var moved = TimingSectionAdjustments.MoveControlPointGroups(beatmap, new[] { group }, -150, false);
            Assert.That(moved.Single().Time, Is.EqualTo(-50));
            Assert.That(beatmap.ControlPointInfo.Groups.Single(), Is.SameAs(moved.Single()));
        }

        private static void moveTimingPoint(IBeatmap beatmap, double originalTime, double adjustment)
        {
            var controlPoints = beatmap.ControlPointInfo;
            var controlPointGroup = controlPoints.GroupAt(originalTime);
            var timingPoint = controlPointGroup.ControlPoints.OfType<TimingControlPoint>().Single();
            controlPoints.RemoveGroup(controlPointGroup);
            TimingSectionAdjustments.AdjustHitObjectOffset(beatmap, timingPoint, adjustment);
            controlPoints.Add(originalTime - adjustment, timingPoint);
        }

        private static void adjustBeatLength(IBeatmap beatmap, double groupTime, double newBeatLength)
        {
            var controlPoints = beatmap.ControlPointInfo;
            var controlPointGroup = controlPoints.GroupAt(groupTime);
            var timingPoint = controlPointGroup.ControlPoints.OfType<TimingControlPoint>().Single();
            double oldBeatLength = timingPoint.BeatLength;
            timingPoint.BeatLength = newBeatLength;
            TimingSectionAdjustments.SetHitObjectBPM(beatmap, timingPoint, oldBeatLength);
        }
    }
}
