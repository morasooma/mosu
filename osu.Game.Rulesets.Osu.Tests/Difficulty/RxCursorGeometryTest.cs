// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using System.Linq;
using MosuPpRxCs;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty;

[TestFixture]
public class RxCursorGeometryTest
{
    private static RxBeatmap circles(params float[] xs)
    {
        var map = new RxBeatmap { CircleSize = 4, ApproachRate = 9, OverallDifficulty = 9 };
        map.TimingPoints.Add(new RxTimingPoint(0, 400));
        for (int i = 0; i < xs.Length; i++)
            map.HitObjects.Add(new RxHitObject { Position = new RxVec2(xs[i], 192), StartTime = 1000 + i * 50, Kind = RxHitObjectKind.Circle });
        return map;
    }

    [Test]
    public void TestProjectionKeepsInsideAndStopsAtBoundary()
    {
        var centre = new RxVec2(100, 100);
        var inside = new RxVec2(110, 100);
        Assert.That(RxCursorGeometry.EnterCircle(inside, centre, 30), Is.EqualTo(inside));
        Assert.That(RxCursorGeometry.EnterCircle(new RxVec2(160, 100), centre, 30), Is.EqualTo(new RxVec2(130, 100)));
    }

    [TestCase(0)]
    [TestCase(20)]
    [TestCase(40)]
    [TestCase(70)]
    public void TestOverlappingOscillationNeedsNoMovement(float spacing)
    {
        var map = circles(Enumerable.Range(0, 900).Select(i => i % 2 == 0 ? 256 - spacing / 2 : 256 + spacing / 2).ToArray());
        var path = RxDifficultyCalculator.CursorPath(map, 4);
        for (int i = 1; i < path.Count; i++)
            Assert.That((path[i].CursorPosition - path[i - 1].EndPosition).Length, Is.LessThan(0.001));
        Assert.That(RxAimControl.ControlPp(map, 1, true), Is.EqualTo(0));
        var result = MosuRxCalculator.Calculate(new RxCalculationRequest
        {
            Beatmap = map, Score = new RxScoreState { Count300 = 900, MaxCombo = 900 }, UseHitAreaDistances = true,
        });
        Assert.That(result.Pp, Is.EqualTo(0));
        Assert.That(result.Difficulty.MaxCombo, Is.EqualTo(900));
    }

    [Test]
    public void TestSmallPositiveMovementIsNotClassifiedAsZero()
    {
        var map = circles(Enumerable.Range(0, 900).Select(i => i % 2 == 0 ? 0f : 74f).ToArray());
        var path = RxDifficultyCalculator.CursorPath(map, 4);
        Assert.That((path[1].CursorPosition - path[0].EndPosition).Length, Is.GreaterThan(1));
        var result = MosuRxCalculator.Calculate(new RxCalculationRequest
        {
            Beatmap = map, Score = new RxScoreState { Count300 = 900, MaxCombo = 900 }, UseHitAreaDistances = true,
        });
        Assert.That(result.Pp, Is.GreaterThan(0));
    }

    [Test]
    public void TestOverlappingChainStillRequiresMovement()
    {
        var path = RxDifficultyCalculator.CursorPath(circles(0, 70, 140, 210), 4);
        Assert.That((path[2].CursorPosition - path[1].EndPosition).Length, Is.GreaterThan(60));
        Assert.That(path[2].Position, Is.EqualTo(new RxVec2(140, 192))); // Reading retains the actual centre.
    }

    [Test]
    public void TestDisjointCirclesUseEdgeDistance()
    {
        var path = RxDifficultyCalculator.CursorPath(circles(0, 200, 0), 4);
        float expected = 200 - 2 * RxCursorGeometry.Radius(4);
        Assert.That((path[1].CursorPosition - path[0].EndPosition).Length, Is.EqualTo(expected).Within(0.001));
        Assert.That((path[2].CursorPosition - path[1].EndPosition).Length, Is.EqualTo(expected).Within(0.001));
    }

    [Test]
    public void TestSmallerCirclesRequireMoreMovement()
    {
        var map = circles(0, 200, 0);
        float movement(float cs)
        {
            var path = RxDifficultyCalculator.CursorPath(map, cs);
            return (path[1].CursorPosition - path[0].EndPosition).Length;
        }
        Assert.That(movement(2), Is.LessThan(movement(4)));
        Assert.That(movement(4), Is.LessThan(movement(6)));
    }

    [Test]
    public void TestLegacyRemainsAvailable()
    {
        var map = circles(Enumerable.Range(0, 900).Select(i => i % 2 == 0 ? 236f : 276f).ToArray());
        var legacy = MosuRxCalculator.Calculate(new RxCalculationRequest { Beatmap = map, Score = new RxScoreState { Count300 = 900, MaxCombo = 900 } });
        Assert.That(legacy.Pp, Is.GreaterThan(0));
        Assert.That(RxAimControl.ControlPp(map, 1), Is.GreaterThan(1000));
        Assert.That(new MosuPpBalance().UseHitAreaDistances, Is.False);
    }

    [Test]
    public void TestSliderEntryAndFollowPathStayFeasible()
    {
        var map = OsuBeatmapParser.Parse("osu file format v14\n[General]\nMode:0\n[Difficulty]\nCircleSize:4\nOverallDifficulty:9\nApproachRate:9\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,400,4,2,0,100,1,0\n[HitObjects]\n0,192,1000,1,0,0:0:0:0:\n200,192,1100,2,0,L|500:192,1,300\n600,192,2400,1,0,0:0:0:0:\n");
        var path = RxDifficultyCalculator.CursorPath(map, 4);
        for (int i = 0; i < path.Count; i++)
            Assert.That((path[i].CursorPosition - map.HitObjects[i].Position).Length, Is.LessThanOrEqualTo(RxCursorGeometry.Radius(4) + 0.001));
        Assert.That(path[1].TravelDistance, Is.GreaterThan(0));
        Assert.That(path[1].EndPosition.X, Is.GreaterThan(path[1].CursorPosition.X));
        var score = new RxScoreState { Count300 = 3 };
        var old = MosuRxCalculator.Calculate(new RxCalculationRequest { Beatmap = map, Score = score });
        var current = MosuRxCalculator.Calculate(new RxCalculationRequest { Beatmap = map, Score = score, UseHitAreaDistances = true });
        Assert.That(current.Difficulty.MaxCombo, Is.EqualTo(old.Difficulty.MaxCombo));
        Assert.That(double.IsFinite(current.Pp), Is.True);
    }
}
