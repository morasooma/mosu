// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using MosuPpRxCs;
using NUnit.Framework;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty;

[TestFixture]
public class RxPointSpamAccuracyTest
{
    private static RxDifficultyAttributes Attributes(int circles, int spam) => new()
    {
        CircleCount = circles, PointSpamCircleCount = spam, MaxCombo = circles,
        AimStrain = 2, SpeedStrain = 2, AimStrainForPp = 2, SpeedStrainForPp = 2,
        AimDifficultStrainCount = 100, SpeedDifficultStrainCount = 100,
        ReadingDifficultNoteCount = 100, Od = 10, Ar = 9, Cs = 2,
    };

    [TestCase(1440)]
    [TestCase(1441)]
    public void TestReportedStreamScoreCannotExceedPerfectAccuracy(int spam)
    {
        var attrs = Attributes(1442, spam);
        var ss = RxPerformanceCalculator.Calculate(attrs, new RxScoreState { Count300 = 1442 }, RxMods.Relax);
        var actual = RxPerformanceCalculator.Calculate(attrs, new RxScoreState
        {
            Count300 = 1413, Count100 = 27, Count50 = 1, Misses = 1, MaxCombo = 973,
        }, RxMods.Relax);
        Assert.That(double.IsFinite(actual.Pp), Is.True);
        Assert.That(actual.PpAccuracy, Is.Zero);
        Assert.That(actual.Pp, Is.LessThanOrEqualTo(ss.Pp));
    }

    [TestCase(0)]
    [TestCase(20)]
    public void TestNormalAccuracyKeepsExistingFormula(int spam)
    {
        var attrs = Attributes(100, spam);
        var result = RxPerformanceCalculator.Calculate(attrs,
            new RxScoreState { Count300 = 90, Count100 = 10 }, RxMods.Relax);
        double fraction = ((90 - spam) * 6d + 20) / ((100 - spam) * 6);
        double expected = Math.Pow(1.52163, 10) * Math.Pow(fraction, 24) * 2.83
                          * Math.Min(Math.Pow((100 - spam) / 1000d, 0.3), 1.15);
        Assert.That(result.PpAccuracy, Is.EqualTo(expected).Within(expected * 1e-5));
    }

    [Test]
    public void TestAllPointSpamStillGivesZero()
    {
        var result = RxPerformanceCalculator.Calculate(Attributes(100, 100),
            new RxScoreState { Count300 = 90, Count100 = 9, Misses = 1 }, RxMods.Relax);
        Assert.That(result.Pp, Is.Zero);
    }
}
