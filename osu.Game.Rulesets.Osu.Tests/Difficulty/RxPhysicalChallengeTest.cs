// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using NUnit.Framework;
using MosuPpRxCs;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty;
[TestFixture]
public class RxPhysicalChallengeTest
{
    private static RxBeatmap triangle(double gap = 100, int count = 600, bool overlap = false, bool reflect = false)
    {
        var map = new RxBeatmap { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 };
        map.TimingPoints.Add(new RxTimingPoint(0, 300));
        var positions = new[] { new RxVec2(100,100), new RxVec2(300,100), new RxVec2(200,273.20508f) };
        for (int i = 0; i < count; i++)
        {
            var v = overlap ? positions[0] : positions[i % 3];
            if (reflect) v = new RxVec2(v.X, 384-v.Y);
            map.HitObjects.Add(new RxHitObject { StartTime = 3000 + i * gap, Position = v, Kind = RxHitObjectKind.Circle });
        }
        return map;
    }

    [Test]
    public void OverlapsDoNotEarnSpeedOrRotation()
    {
        var f = RxPhysicalChallenge.Analyze(triangle(overlap:true));
        Assert.That(f.SpeedPressure, Is.Zero); Assert.That(f.RotationShare, Is.Zero); Assert.That(f.VisibleNotes, Is.Zero);
    }
    [Test]
    public void SustainedFastMovementPaysMoreThanSlowOrIsolatedMovement()
    {
        var fast = RxPhysicalChallenge.Analyze(triangle());
        var slow = RxPhysicalChallenge.Analyze(triangle(250));
        var shortMap = RxPhysicalChallenge.Analyze(triangle(count:6));
        Assert.That(fast.SpeedPressure, Is.GreaterThan(slow.SpeedPressure));
        Assert.That(fast.SpeedPressure, Is.GreaterThan(shortMap.SpeedPressure * 10));
        Assert.That(fast.RotationShare, Is.GreaterThan(0.85));
    }
    [Test]
    public void MetadataBpmDoesNotChangeTheCorrection()
    {
        var map = triangle(); var a = RxPhysicalChallenge.Analyze(map);
        map.TimingPoints.Clear();map.TimingPoints.Add(new RxTimingPoint(0, 50));
        Assert.That(RxPhysicalChallenge.Analyze(map), Is.EqualTo(a));
    }
    [Test]
    public void RotationOrientationDoesNotChangeTheCorrection()
    {
        var a = RxPhysicalChallenge.Analyze(triangle());var b = RxPhysicalChallenge.Analyze(triangle(reflect:true));
        Assert.That(a.RotationShare, Is.EqualTo(b.RotationShare).Within(1e-6));
        Assert.That(a.SpeedPressure, Is.EqualTo(b.SpeedPressure).Within(1e-6));
    }
    [Test]
    public void BackAndForthIsNotCircularFlow()
    {
        var map = triangle(); for (int i=0;i<map.HitObjects.Count;i++)map.HitObjects[i].Position = new RxVec2(i%2==0 ? 100:300,100);
        Assert.That(RxPhysicalChallenge.Analyze(map).RotationShare, Is.Zero);
    }
    [Test]
    public void RhythmBreaksPreventFullRotationDiscount()
    {
        var map=triangle();for(int i=0;i<map.HitObjects.Count;i++)map.HitObjects[i].StartTime=3000+i/2*400+i%2*100;
        Assert.That(RxPhysicalChallenge.Analyze(map).RotationShare, Is.Zero);
    }
    [Test]
    public void ExistingReadingBonusIsNotPaidTwice()
    {
        var f = new RxPhysicalChallenge.Features(1,10,1);
        Assert.That(RxPhysicalChallenge.Reading(f,1.2,1),Is.EqualTo(1));
        Assert.That(RxPhysicalChallenge.Reading(f,1,1),Is.EqualTo(1.12).Within(1e-10));
        Assert.That(RxPhysicalChallenge.Speed(f,1),Is.EqualTo(1.16).Within(1e-10));
        Assert.That(RxPhysicalChallenge.Rotation(f,1),Is.EqualTo(0.94).Within(1e-10));
        Assert.That(RxPhysicalChallenge.Circle(4,4,1),Is.EqualTo(1));
        Assert.That(RxPhysicalChallenge.Circle(4,2,1),Is.EqualTo(1));
        Assert.That(RxPhysicalChallenge.Circle(4,10,1),Is.InRange(1,1.08));
    }
}
