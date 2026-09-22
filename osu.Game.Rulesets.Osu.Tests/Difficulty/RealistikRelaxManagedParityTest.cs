// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Reflection;
using MosuRxPureCs;
using NUnit.Framework;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty;

[TestFixture]
public class RealistikRelaxManagedParityTest
{
    private const double parity_precision = 0.0002;

    [TestCase("801165", RxMods.Relax, 333.28631591796875, 137.43943786621094, 111.52545166015625, 79.78475189208984, 2.6034746170043945)]
    [TestCase("1124896", RxMods.Relax | RxMods.DoubleTime, 318.27740478515625, 144.08921813964844, 88.13529205322266, 59.11530303955078, 6.568825721740723)]
    [TestCase("1124896", RxMods.Relax | RxMods.Nightcore, 318.27740478515625, 144.08921813964844, 88.13529205322266, 59.11530303955078, 6.568825721740723)]
    [TestCase("2496318", RxMods.Relax | RxMods.Hidden | RxMods.DoubleTime, 3865.886474609375, 1757.235595703125, 516.807373046875, 145.15443420410156, 315.6304626464844)]
    public void TestSsNativeParity(
        string mapName,
        RxMods mods,
        double pp,
        double aim,
        double speed,
        double accuracy,
        double reading)
    {
        RxNativePerformanceResult actual = MosuRxCalculator.CalculateSs(readMap(mapName), mods);
        assertNative(actual, pp, aim, speed, accuracy, reading);
    }

    [Test]
    public void TestHardRockNativeParityOnPlayableGeometry()
    {
        RxBeatmap beatmap = readMap("801165");

        foreach (RxHitObject hitObject in beatmap.HitObjects)
        {
            hitObject.Position = new RxVec2(hitObject.Position.X, 384 - hitObject.Position.Y);

            if (hitObject.Slider is not null)
            {
                foreach (RxPathControlPoint controlPoint in hitObject.Slider.ControlPoints)
                    controlPoint.Position = new RxVec2(controlPoint.Position.X, -controlPoint.Position.Y);
            }
        }

        RxNativePerformanceResult actual = MosuRxCalculator.CalculateSs(
            beatmap,
            RxMods.Relax,
            effectiveAr: 10,
            effectiveOd: 10,
            effectiveCs: 5.2f,
            effectiveHp: 8.4f);

        assertNative(actual, 574.35845947265625, 205.0375518798828, 118.4924545288086, 184.73025512695312, 0.9904443621635437);
    }

    [Test]
    public void TestDifficultyAdjustAndCustomRateNativeParity()
    {
        RxNativePerformanceResult actual = MosuRxCalculator.CalculateSs(
            readMap("1341554"),
            RxMods.Relax,
            effectiveAr: 9.8f,
            effectiveOd: 9.4f,
            effectiveCs: 7.3f,
            effectiveHp: 6,
            clockRate: 1.25);

        assertNative(actual, 774.39312744140625, 365.10894775390625, 138.04132080078125, 95.8757095336914, 29.220630645751953);
    }

    [Test]
    public void TestAntiFarm5738687NativeParity()
    {
        RxNativePerformanceResult actual = MosuRxCalculator.CalculateSs(
            readMap("5738687"),
            RxMods.Relax,
            effectiveCs: 10,
            clockRate: 1.5);

        assertNative(actual, 5338.359375, 2302.642578125, 1042.002685546875, 120.85305786132812, 655.9622802734375);
        Assert.That(actual.Difficulty.AimStrain, Is.EqualTo(9.17938232421875).Within(parity_precision));
        Assert.That(actual.Difficulty.SpeedStrain, Is.EqualTo(6.560817241668701).Within(parity_precision));
        Assert.That(actual.Difficulty.JumpSpikeFillerWeight, Is.Zero);
        Assert.That(actual.Difficulty.SpeedSpikeFillerWeight, Is.Zero);
    }

    [Test]
    public void TestMissAndLowComboNativeParity()
    {
        RxNativePerformanceResult actual = calculate(
            "801165",
            RxMods.Relax,
            new RxScoreState
            {
                Count300 = 1559,
                Count100 = 30,
                Count50 = 8,
                Misses = 15,
                MaxCombo = 180,
            });

        assertNative(actual, 142.9047393798828, 66.82955932617188, 46.805973052978516, 26.811471939086914, 1.227562665939331);
    }

    [TestCase(100u, 0u, 0u, 0u, 100u, 2834.433349609375, 1290.037109375, 593.39453125, 93.2306137084961, 273.2618103027344)]
    [TestCase(105u, 10u, 2u, 3u, 50u, 2348.12841796875, 1128.78125, 479.8934020996094, 8.368054389953613, 188.65869140625)]
    public void TestPartialScoreRegression(
        uint count300,
        uint count100,
        uint count50,
        uint misses,
        uint combo,
        double pp,
        double aim,
        double speed,
        double accuracy,
        double reading)
    {
        uint passedObjects = count300 + count100 + count50 + misses;
        RxNativePerformanceResult actual = calculate(
            "5738687",
            RxMods.Relax,
            new RxScoreState
            {
                Count300 = count300,
                Count100 = count100,
                Count50 = count50,
                Misses = misses,
                MaxCombo = combo,
                PassedObjects = passedObjects,
            },
            effectiveCs: 10,
            clockRate: 1.5);

        assertNative(actual, pp, aim, speed, accuracy, reading);
        Assert.That(actual.Difficulty.CircleCount + actual.Difficulty.SliderCount + actual.Difficulty.SpinnerCount, Is.EqualTo(passedObjects));
    }

    [Test]
    public void TestPassedObjectsExcludesFutureDifficultyObjects()
    {
        RxBeatmap prefixOnly = readMap("801165");
        prefixOnly.HitObjects.RemoveRange(100, prefixOnly.HitObjects.Count - 100);

        RxScoreState score = new RxScoreState
        {
            Count300 = 100,
            MaxCombo = 100,
            PassedObjects = 100,
        };

        RxNativePerformanceResult partial = calculate("801165", RxMods.Relax, score);
        RxNativePerformanceResult explicitPrefix = MosuRxCalculator.Calculate(new RxCalculationRequest
        {
            Beatmap = prefixOnly,
            Mods = RxMods.Relax,
            Score = score,
        });

        Assert.That(partial.Pp, Is.EqualTo(57.321529388427734).Within(parity_precision));
        Assert.That(partial.Pp, Is.EqualTo(explicitPrefix.Pp).Within(parity_precision));
        Assert.That(partial.Difficulty.Stars, Is.EqualTo(explicitPrefix.Difficulty.Stars).Within(parity_precision));
        Assert.That(partial.Difficulty.FlowSectionCount, Is.EqualTo(explicitPrefix.Difficulty.FlowSectionCount));
    }

    [TestCase("1199955", new[]
    {
        2d, 9.478196, 4.295699, 3.415838, 294.597015, 176.308533, 108.375336, 1.398286, 648.423889,
        3d, 9.892584, 4.524089, 3.420835, 344.792053, 177.095825, 108.375336, 1.985502, 738.740295,
        4d, 10.413728, 4.793652, 3.429101, 420.869537, 182.684799, 108.375336, 3.123382, 879.023926,
        5d, 11.081923, 5.117734, 3.443629, 534.891724, 192.860672, 108.375336, 5.432867, 1094.395142,
        6d, 11.549820, 5.344214, 3.484165, 631.864868, 207.054001, 108.375336, 7.205756, 1281.699097,
        7d, 12.162138, 5.628189, 3.591719, 764.851196, 235.036850, 108.375336, 9.541998, 1543.825439,
        8d, 12.883765, 5.935308, 3.814608, 928.519958, 291.795105, 108.375336, 11.984812, 1874.575439,
        10d, 15.347092, 7.093690, 4.433842, 1592.319214, 460.976807, 108.375336, 21.938923, 3247.370117,
    })]
    [TestCase("4766797", new[]
    {
        2d, 9.460249, 3.890154, 3.972118, 201.058578, 258.406616, 199.119247, 1.324603, 577.452515,
        3d, 9.848537, 4.483694, 4.047992, 396.881592, 350.911865, 199.119247, 1.939657, 967.535156,
        4d, 10.328819, 4.859533, 4.174139, 519.383972, 394.853607, 199.119247, 2.769236, 1198.698486,
        5d, 11.036972, 5.186249, 4.383477, 660.460510, 478.262878, 199.119247, 4.039024, 1476.204590,
        6d, 12.043308, 5.401827, 4.910243, 775.166443, 700.165527, 199.119247, 5.526063, 1726.180664,
        7d, 13.256362, 5.672829, 5.404611, 931.483398, 970.251770, 199.119247, 10.090370, 2066.012451,
        8d, 14.017333, 5.875950, 5.553658, 1072.240845, 1090.261475, 199.119247, 16.613861, 2361.545410,
        10d, 15.801111, 6.716681, 5.610416, 1607.066406, 1124.366577, 199.119247, 37.885967, 3460.465332,
    })]
    public void TestHighCsFlowCompressionMatrix(string mapName, double[] expected)
    {
        double previousPp = 0;

        for (int i = 0; i < expected.Length; i += 9)
        {
            float circleSize = (float)expected[i];
            RxNativePerformanceResult actual = MosuRxCalculator.CalculateSs(readMap(mapName), RxMods.Relax, effectiveCs: circleSize);

            Assert.Multiple(() =>
            {
                Assert.That(actual.Difficulty.Stars, Is.EqualTo(expected[i + 1]).Within(parity_precision));
                Assert.That(actual.Difficulty.AimStrain, Is.EqualTo(expected[i + 2]).Within(parity_precision));
                Assert.That(actual.Difficulty.SpeedStrain, Is.EqualTo(expected[i + 3]).Within(parity_precision));
                Assert.That(actual.PpAim, Is.EqualTo(expected[i + 4]).Within(parity_precision));
                Assert.That(actual.PpSpeed, Is.EqualTo(expected[i + 5]).Within(parity_precision));
                Assert.That(actual.PpAccuracy, Is.EqualTo(expected[i + 6]).Within(parity_precision));
                Assert.That(actual.PpReading, Is.EqualTo(expected[i + 7]).Within(parity_precision));
                Assert.That(actual.Pp, Is.EqualTo(expected[i + 8]).Within(parity_precision));
                Assert.That(actual.Pp, Is.GreaterThan(previousPp));
            });

            previousPp = actual.Pp;
        }
    }

    [TestCase("1199955")]
    [TestCase("4766797")]
    public void TestStreamCsRewardOnlyChangesAimAndSpeed(string mapName)
    {
        RxBeatmap beatmap = readMap(mapName);
        var score = new RxScoreState
        {
            Count300 = (uint)beatmap.HitObjects.Count,
            PassedObjects = (uint)beatmap.HitObjects.Count,
        };

        foreach (float circleSize in new[] { 2f, 3f, 4f, 5f, 6f, 7f, 8f })
        {
            RxNativePerformanceResult rewarded = calculate(mapName, RxMods.Relax, score, effectiveCs: circleSize);
            double streamWeight = rewarded.Difficulty.CanonicalStreamWeight;
            rewarded.Difficulty.CanonicalStreamWeight = 0;
            RxNativePerformanceResult baseline = RxPerformanceCalculator.Calculate(rewarded.Difficulty, score, RxMods.Relax);
            double expectedMultiplier = 1 + streamWeight * streamCsBonus(circleSize);

            Assert.Multiple(() =>
            {
                Assert.That(rewarded.PpAim, Is.EqualTo(baseline.PpAim * expectedMultiplier).Within(parity_precision));
                Assert.That(rewarded.PpSpeed, Is.EqualTo(baseline.PpSpeed * expectedMultiplier).Within(parity_precision));
                Assert.That(rewarded.PpAccuracy, Is.EqualTo(baseline.PpAccuracy).Within(parity_precision));
                Assert.That(rewarded.PpReading, Is.EqualTo(baseline.PpReading).Within(parity_precision));
                if (circleSize <= 3)
                    Assert.That(expectedMultiplier, Is.EqualTo(1));
            });
        }
    }

    [TestCase("1199955", 4f)]
    [TestCase("1199955", 6f)]
    [TestCase("4766797", 5f)]
    [TestCase("4766797", 8f)]
    public void TestStreamCsRewardUsesEffectiveCircleSize(string mapName, float circleSize)
    {
        RxBeatmap adjustedMap = readMap(mapName);
        RxNativePerformanceResult adjusted = MosuRxCalculator.CalculateSs(adjustedMap, RxMods.Relax, effectiveCs: circleSize);
        RxBeatmap nativeMap = readMap(mapName);
        nativeMap.CircleSize = circleSize;
        RxNativePerformanceResult native = MosuRxCalculator.CalculateSs(nativeMap, RxMods.Relax);

        Assert.Multiple(() =>
        {
            Assert.That(adjusted.Difficulty.Stars, Is.EqualTo(native.Difficulty.Stars).Within(parity_precision));
            Assert.That(adjusted.PpAim, Is.EqualTo(native.PpAim).Within(parity_precision));
            Assert.That(adjusted.PpSpeed, Is.EqualTo(native.PpSpeed).Within(parity_precision));
            Assert.That(adjusted.PpAccuracy, Is.EqualTo(native.PpAccuracy).Within(parity_precision));
            Assert.That(adjusted.PpReading, Is.EqualTo(native.PpReading).Within(parity_precision));
            Assert.That(adjusted.Pp, Is.EqualTo(native.Pp).Within(parity_precision));
        });
    }

    [Test]
    public void TestStreamCsRewardIsContinuousAtCurveKnots()
    {
        foreach (float knot in new[] { 3f, 4f, 5f })
        {
            double left = rewardAt(knot - 0.001f);
            double centre = rewardAt(knot);
            double right = rewardAt(knot + 0.001f);

            Assert.That(left, Is.LessThanOrEqualTo(centre));
            Assert.That(centre, Is.LessThanOrEqualTo(right));
            Assert.That(right - left, Is.LessThan(0.001));
        }

        double rewardAt(float circleSize)
        {
            RxBeatmap beatmap = readMap("1199955");
            var score = new RxScoreState
            {
                Count300 = (uint)beatmap.HitObjects.Count,
                PassedObjects = (uint)beatmap.HitObjects.Count,
            };
            RxNativePerformanceResult rewarded = calculate("1199955", RxMods.Relax, score, effectiveCs: circleSize);
            rewarded.Difficulty.CanonicalStreamWeight = 0;
            RxNativePerformanceResult baseline = RxPerformanceCalculator.Calculate(rewarded.Difficulty, score, RxMods.Relax);
            return rewarded.PpAim / baseline.PpAim - 1;
        }
    }

    [TestCase("2496318", 5f, 12.409039, 5.833277, 3.328931, 770.298950, 171.010071, 145.154434, 12.968418, 1585.911987)]
    [TestCase("2496318", 6f, 13.544444, 6.437897, 3.328931, 1038.236572, 171.010071, 145.154434, 21.833086, 2118.722656)]
    [TestCase("2496318", 8f, 16.226891, 8.007730, 3.328931, 2007.943359, 171.009995, 145.154434, 46.093330, 4126.560059)]
    [TestCase("2496318", 10f, 21.145411, 11.104417, 3.328932, 5384.864746, 171.010117, 145.154434, 99.226250, 11604.363281)]
    [TestCase("1341554", 5f, 7.367327, 3.155982, 2.611959, 98.468895, 66.222321, 53.269176, 1.818774, 228.347305)]
    [TestCase("1341554", 6f, 8.246421, 3.594115, 2.667254, 146.363770, 70.610405, 53.269176, 3.714436, 309.527802)]
    [TestCase("1341554", 8f, 10.493410, 4.715111, 2.762560, 334.079468, 78.621147, 53.269176, 14.796458, 649.376953)]
    [TestCase("1341554", 10f, 14.191135, 6.878387, 2.790887, 1048.509399, 81.113808, 53.269176, 42.855415, 2046.722656)]
    [TestCase("1124896", 5f, 6.050526, 2.598427, 2.027236, 61.943398, 34.050102, 59.115303, 1.336396, 169.518478)]
    [TestCase("1124896", 6f, 6.828789, 2.870521, 2.154472, 84.016365, 41.070686, 59.115303, 3.362296, 207.042343)]
    [TestCase("1124896", 8f, 8.693149, 3.616622, 2.440888, 170.033432, 60.266720, 59.115303, 12.432981, 360.286835)]
    [TestCase("1124896", 10f, 11.644922, 5.254623, 2.480975, 528.921448, 63.354481, 59.115303, 35.214386, 1029.420044)]
    public void TestHighCsJumpBenchmarksRemainUnchanged(
        string mapName,
        float circleSize,
        double stars,
        double aimStrain,
        double speedStrain,
        double aimPp,
        double speedPp,
        double accuracyPp,
        double readingPp,
        double totalPp)
    {
        RxNativePerformanceResult actual = MosuRxCalculator.CalculateSs(readMap(mapName), RxMods.Relax, effectiveCs: circleSize);

        Assert.Multiple(() =>
        {
            Assert.That(actual.Difficulty.Stars, Is.EqualTo(stars).Within(parity_precision));
            Assert.That(actual.Difficulty.AimStrain, Is.EqualTo(aimStrain).Within(parity_precision));
            Assert.That(actual.Difficulty.SpeedStrain, Is.EqualTo(speedStrain).Within(parity_precision));
            Assert.That(actual.PpAim, Is.EqualTo(aimPp).Within(parity_precision));
            Assert.That(actual.PpSpeed, Is.EqualTo(speedPp).Within(parity_precision));
            Assert.That(actual.PpAccuracy, Is.EqualTo(accuracyPp).Within(parity_precision));
            Assert.That(actual.PpReading, Is.EqualTo(readingPp).Within(parity_precision));
            Assert.That(actual.Pp, Is.EqualTo(totalPp).Within(parity_precision));
        });
    }

    [Test]
    public void TestHighCsCompressionOnlyUsesPassedObjects()
    {
        RxBeatmap prefixOnly = readMap("1199955");
        prefixOnly.HitObjects.RemoveRange(300, prefixOnly.HitObjects.Count - 300);

        RxScoreState score = new RxScoreState
        {
            Count300 = 300,
            MaxCombo = 300,
            PassedObjects = 300,
        };

        RxNativePerformanceResult partial = calculate("1199955", RxMods.Relax, score, effectiveCs: 10);
        RxNativePerformanceResult explicitPrefix = MosuRxCalculator.Calculate(new RxCalculationRequest
        {
            Beatmap = prefixOnly,
            Mods = RxMods.Relax,
            Score = score,
            EffectiveCs = 10,
        });

        Assert.That(partial.Pp, Is.EqualTo(explicitPrefix.Pp).Within(parity_precision));
        Assert.That(partial.Difficulty.Stars, Is.EqualTo(explicitPrefix.Difficulty.Stars).Within(parity_precision));
        Assert.That(partial.Difficulty.AimStrain, Is.EqualTo(explicitPrefix.Difficulty.AimStrain).Within(parity_precision));
        Assert.That(partial.Difficulty.ReadingStrain, Is.EqualTo(explicitPrefix.Difficulty.ReadingStrain).Within(parity_precision));
    }

    [TestCase("4766797", new[] { 9.180628, 10.349401, 10.351427, 10.386457 })]
    [TestCase("3104339", new[] { 8.571223, 8.594542, 11.198914, 11.214419 })]
    public void TestReadingStarsDoNotDecreaseAsPlayedPrefixGrows(string mapName, double[] expectedStars)
    {
        RxBeatmap beatmap = readMap(mapName);
        double previousStars = 0;

        for (int i = 0; i < expectedStars.Length; i++)
        {
            double completion = (i + 1) * 0.25;
            uint passedObjects = (uint)Math.Floor(beatmap.HitObjects.Count * completion);
            RxNativePerformanceResult result = calculate(mapName, RxMods.Relax, new RxScoreState
            {
                Count300 = passedObjects,
                PassedObjects = passedObjects,
            });

            Assert.That(result.Difficulty.Stars, Is.EqualTo(expectedStars[i]).Within(parity_precision));
            Assert.That(result.Difficulty.Stars, Is.GreaterThanOrEqualTo(previousStars));
            previousStars = result.Difficulty.Stars;
        }
    }

    [TestCase("801165", 7.515325)]
    [TestCase("2496318", 11.507415)]
    [TestCase("1124896", 5.456490)]
    public void TestStableFullMapStarsRemainCloseToPreviousBaseline(string mapName, double previousStars)
    {
        RxNativePerformanceResult result = MosuRxCalculator.CalculateSs(readMap(mapName), RxMods.Relax);

        Assert.That(Math.Abs(result.Difficulty.Stars / previousStars - 1), Is.LessThan(0.02));
    }

    [TestCase(0.75f)]
    [TestCase(1f)]
    [TestCase(1.25f)]
    [TestCase(1.5f)]
    public void TestReadingOpeningHorizonUsesRealTime(float clockRate)
    {
        float firstObjectTime = 1000;
        foreach ((float realTime, float expected) in new[] { (0f, 0f), (15_000f, 0.25f), (30_000f, 0.5f), (60_000f, 1f), (90_000f, 1f) })
        {
            float objectTime = firstObjectTime + realTime * clockRate;
            Assert.That(ReadingSkill.PrefixStableOpeningProgress(objectTime, firstObjectTime, clockRate),
                Is.EqualTo(expected).Within(parity_precision));
        }
    }

    [TestCase("801165")]
    [TestCase("1124896")]
    [TestCase("1341554")]
    [TestCase("2593923")]
    public void TestLowerClockRateDoesNotIncreaseDifficultyOrPerfectPp(string mapName)
    {
        RxBeatmap beatmap = readMap(mapName);
        RxNativePerformanceResult halfRate = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, clockRate: 0.5);
        RxNativePerformanceResult threeQuarterRate = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, clockRate: 0.75);
        RxNativePerformanceResult ninetyFivePercentRate = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, clockRate: 0.95);
        RxNativePerformanceResult normalRate = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, clockRate: 1);

        Assert.Multiple(() =>
        {
            Assert.That(halfRate.Difficulty.Stars, Is.LessThanOrEqualTo(threeQuarterRate.Difficulty.Stars));
            Assert.That(threeQuarterRate.Difficulty.Stars, Is.LessThanOrEqualTo(ninetyFivePercentRate.Difficulty.Stars));
            Assert.That(ninetyFivePercentRate.Difficulty.Stars, Is.LessThanOrEqualTo(normalRate.Difficulty.Stars));
            Assert.That(halfRate.Pp, Is.LessThanOrEqualTo(threeQuarterRate.Pp));
            Assert.That(threeQuarterRate.Pp, Is.LessThanOrEqualTo(ninetyFivePercentRate.Pp));
            Assert.That(ninetyFivePercentRate.Pp, Is.LessThanOrEqualTo(normalRate.Pp));
        });
    }

    [TestCase("801165")]
    [TestCase("4766797")]
    [TestCase("2496318")]
    public void TestPerfectAccuracyLosesPpMonotonicallyWithComboBreaks(string mapName)
    {
        RxBeatmap beatmap = readMap(mapName);
        uint objectCount = (uint)beatmap.HitObjects.Count;
        RxNativePerformanceResult fullCombo = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax);
        uint maximumCombo = (uint)fullCombo.Difficulty.MaxCombo;
        RxNativePerformanceResult seventyFivePercentCombo = calculatePerfectAccuracy(maximumCombo * 3 / 4);
        RxNativePerformanceResult fiftyPercentCombo = calculatePerfectAccuracy(maximumCombo / 2);
        RxNativePerformanceResult twentyFivePercentCombo = calculatePerfectAccuracy(maximumCombo / 4);

        Assert.Multiple(() =>
        {
            Assert.That(fullCombo.Pp, Is.GreaterThan(seventyFivePercentCombo.Pp));
            Assert.That(seventyFivePercentCombo.Pp, Is.GreaterThan(fiftyPercentCombo.Pp));
            Assert.That(fiftyPercentCombo.Pp, Is.GreaterThan(twentyFivePercentCombo.Pp));
            Assert.That(fullCombo.PpAccuracy, Is.EqualTo(seventyFivePercentCombo.PpAccuracy));
            Assert.That(fullCombo.Difficulty.Stars, Is.EqualTo(twentyFivePercentCombo.Difficulty.Stars));
        });

        RxNativePerformanceResult calculatePerfectAccuracy(uint combo) => calculate(
            mapName,
            RxMods.Relax,
            new RxScoreState
            {
                Count300 = objectCount,
                MaxCombo = combo,
                PassedObjects = objectCount,
            });
    }

    [TestCase("801165")]
    [TestCase("4766797")]
    [TestCase("2496318")]
    public void TestAccuracyAndMissDegradationRemainMonotonic(string mapName)
    {
        RxBeatmap beatmap = readMap(mapName);
        uint objectCount = (uint)beatmap.HitObjects.Count;
        uint imperfectCount = Math.Max(1, objectCount / 20);

        RxNativePerformanceResult ss = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax);
        uint maximumCombo = (uint)ss.Difficulty.MaxCombo;
        RxNativePerformanceResult lowerAccuracy = calculate(mapName, RxMods.Relax, new RxScoreState
        {
            Count300 = objectCount - imperfectCount,
            Count100 = imperfectCount,
            MaxCombo = maximumCombo,
            PassedObjects = objectCount,
        });
        RxNativePerformanceResult misses = calculate(mapName, RxMods.Relax, new RxScoreState
        {
            Count300 = objectCount - imperfectCount,
            Misses = imperfectCount,
            MaxCombo = maximumCombo / 2,
            PassedObjects = objectCount,
        });

        Assert.That(ss.Pp, Is.GreaterThan(lowerAccuracy.Pp));
        Assert.That(lowerAccuracy.Pp, Is.GreaterThan(misses.Pp));
    }

    [TestCase(0f, 2.5f)]
    [TestCase(2f, 2.432752f)]
    [TestCase(4f, 2.096066f)]
    [TestCase(5f, 1.832297f)]
    [TestCase(6f, 1.543708f)]
    [TestCase(7f, 1.274794f)]
    [TestCase(8f, 1.076495f)]
    [TestCase(9f, 1f)]
    [TestCase(10f, 1f)]
    public void TestLowArBonusAmplificationCurve(float ar, float expected)
        => Assert.That(RxPerformanceCalculator.LowArBonusAmplification(ar), Is.EqualTo(expected).Within(0.000001));

    [TestCase("1199955")]
    [TestCase("4766797")]
    [TestCase("801165")]
    [TestCase("2496318")]
    public void TestLowArAmplifiesOnlyExistingReadingBonus(string mapName)
    {
        RxBeatmap beatmap = readMap(mapName);
        RxNativePerformanceResult ar9 = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, effectiveAr: 9f);
        double ar9ReadingBeforeHighStarBonus = readingValueFromStrain(ar9.Difficulty.ReadingStrainForPerformance);

        foreach (float ar in new[] { 0f, 2f, 4f, 5f, 6f, 7f, 8f, 8.9f })
        {
            RxNativePerformanceResult actual = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, effectiveAr: ar);
            double previousReading = readingValueFromStrain(actual.Difficulty.ReadingStrainForPerformance);
            double highStarMultiplier = 1 + RxPerformanceCalculator.HighStarAr9ReadingBonus(ar, (float)actual.Difficulty.Stars);
            double readingBeforeHighStarBonus = actual.PpReading / highStarMultiplier;
            double previousBonus = previousReading - ar9ReadingBeforeHighStarBonus;
            double amplifiedBonus = readingBeforeHighStarBonus - ar9ReadingBeforeHighStarBonus;

            Assert.Multiple(() =>
            {
                Assert.That(previousBonus, Is.GreaterThan(0));
                Assert.That(amplifiedBonus / previousBonus,
                    Is.EqualTo(RxPerformanceCalculator.LowArBonusAmplification(ar)).Within(parity_precision));
                Assert.That(actual.PpAim, Is.EqualTo(ar9.PpAim).Within(parity_precision));
                Assert.That(actual.PpSpeed, Is.EqualTo(ar9.PpSpeed).Within(parity_precision));
                Assert.That(actual.PpAccuracy, Is.EqualTo(ar9.PpAccuracy).Within(parity_precision));
            });
        }
    }

    [TestCase("1199955", 10f, 932.781677246, 3.758989811)]
    [TestCase("2496318", 10f, 1296.620117188, 7.962969780)]
    public void TestAr10AndAboveRemainExact(string mapName, float ar, double pp, double reading)
    {
        RxNativePerformanceResult actual = MosuRxCalculator.CalculateSs(readMap(mapName), RxMods.Relax, effectiveAr: ar);
        Assert.That(actual.Pp, Is.EqualTo(pp).Within(parity_precision));
        Assert.That(actual.PpReading, Is.EqualTo(reading).Within(parity_precision));
    }

    [TestCase(8.5f, 0f)]
    [TestCase(9f, 0.0259259f)]
    [TestCase(9.5f, 0.0740741f)]
    [TestCase(10f, 0.10f)]
    [TestCase(11f, 0.10f)]
    public void TestHighStarAr9ReadingReward(float stars, float expectedBonus)
    {
        RxDifficultyAttributes attributes = createSyntheticDifficulty(stars, 9f);
        RxScoreState score = new RxScoreState { Count300 = 100 };
        RxNativePerformanceResult rewarded = RxPerformanceCalculator.Calculate(attributes, score, RxMods.Relax);
        attributes.Ar = 10f;
        RxNativePerformanceResult baseline = RxPerformanceCalculator.Calculate(attributes, score, RxMods.Relax);

        Assert.Multiple(() =>
        {
            Assert.That(rewarded.PpReading / baseline.PpReading - 1, Is.EqualTo(expectedBonus).Within(parity_precision));
            Assert.That(rewarded.PpAim, Is.EqualTo(baseline.PpAim));
            Assert.That(rewarded.PpSpeed, Is.EqualTo(baseline.PpSpeed));
            Assert.That(rewarded.PpAccuracy, Is.EqualTo(baseline.PpAccuracy));
            Assert.That(rewarded.Difficulty.Stars, Is.EqualTo(stars));
        });
    }

    [Test]
    public void TestHighStarAr9RewardIsSmoothAndLeavesAr10Unchanged()
    {
        Assert.That(RxPerformanceCalculator.HighStarAr9ReadingBonus(8.7f, 11f), Is.Zero);
        Assert.That(RxPerformanceCalculator.HighStarAr9ReadingBonus(9.3f, 11f), Is.Zero);
        Assert.That(RxPerformanceCalculator.HighStarAr9ReadingBonus(10f, 11f), Is.Zero);
        Assert.That(RxPerformanceCalculator.HighStarAr9ReadingBonus(11f, 11f), Is.Zero);
    }

    [TestCase("1199955")]
    [TestCase("4766797")]
    [TestCase("2496318")]
    public void TestLowArRewardIsSmoothAtAr9(string mapName)
    {
        RxBeatmap beatmap = readMap(mapName);
        RxNativePerformanceResult ar89 = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, effectiveAr: 8.9f);
        RxNativePerformanceResult ar90 = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, effectiveAr: 9f);
        RxNativePerformanceResult ar91 = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, effectiveAr: 9.1f);

        Assert.That(ar89.Pp, Is.GreaterThan(ar90.Pp));
        Assert.That(ar90.Pp, Is.GreaterThan(ar91.Pp));
        Assert.That(Math.Abs((ar89.Pp - ar90.Pp) - (ar90.Pp - ar91.Pp)), Is.LessThan(1.0));
    }

    private static double readingValueFromStrain(double strain)
        => Math.Pow(5 * Math.Max(strain / 0.0675, 1) - 4, 3) / 265000;

    private static RxDifficultyAttributes createSyntheticDifficulty(float stars, float ar)
        => new RxDifficultyAttributes
        {
            Stars = stars,
            Ar = ar,
            Od = 8,
            AimStrain = 3,
            AimStrainBeforeHighCsCompression = 3,
            SpeedStrain = 3,
            ReadingStrain = 3,
            ReadingStrainForPerformance = 3,
            CircleCount = 100,
            MaxCombo = 100,
            AimDifficultStrainCount = 10,
            SpeedDifficultStrainCount = 10,
            ReadingDifficultNoteCount = 10,
        };

    private static double streamCsBonus(float cs)
    {
        if (cs <= 3) return 0;
        if (cs < 4) return 0.025 * RxMath.SmoothStep(cs, 3, 4);
        if (cs < 5) return 0.025 + 0.045 * RxMath.SmoothStep(cs, 4, 5);
        if (cs < 6) return 0.07 + 0.04 * RxMath.SmoothStep(cs, 5, 6);
        if (cs < 7) return 0.11 + 0.04 * RxMath.SmoothStep(cs, 6, 7);
        if (cs < 8) return 0.15 + 0.04 * RxMath.SmoothStep(cs, 7, 8);
        return 0.19;
    }

    private static RxNativePerformanceResult calculate(
        string mapName,
        RxMods mods,
        RxScoreState score,
        float? effectiveCs = null,
        double? clockRate = null)
        => MosuRxCalculator.Calculate(new RxCalculationRequest
        {
            Beatmap = readMap(mapName),
            Mods = mods,
            Score = score,
            EffectiveCs = effectiveCs,
            ClockRate = clockRate,
        });

    private static RxBeatmap readMap(string name)
    {
        Assembly assembly = typeof(RealistikRelaxManagedParityTest).Assembly;
        string resourceName = $"osu.Game.Rulesets.Osu.Tests.Resources.Testing.Beatmaps.{name}.osu";

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return OsuBeatmapParser.Parse(reader.ReadToEnd());
    }

    private static void assertNative(
        RxNativePerformanceResult actual,
        double pp,
        double aim,
        double speed,
        double accuracy,
        double reading)
    {
        Assert.That(actual.Pp, Is.EqualTo(pp).Within(parity_precision));
        Assert.That(actual.PpAim, Is.EqualTo(aim).Within(parity_precision));
        Assert.That(actual.PpSpeed, Is.EqualTo(speed).Within(parity_precision));
        Assert.That(actual.PpAccuracy, Is.EqualTo(accuracy).Within(parity_precision));
        Assert.That(actual.PpReading, Is.EqualTo(reading).Within(parity_precision));
    }
}
