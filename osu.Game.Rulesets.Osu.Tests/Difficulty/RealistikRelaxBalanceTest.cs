// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MosuRxPureCs;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax.Realistik;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty;

[TestFixture]
public class RealistikRelaxBalanceTest
{
    [Test]
    public void TestVerticalNeutralModsCannotBypass()
    {
        Mod relax = new OsuModRelax();

        Assert.That(RealistikRelaxBalance.IsVerticalBalanceEligible(new[] { relax }), Is.True);
        Assert.That(RealistikRelaxBalance.IsVerticalBalanceEligible(new Mod[] { relax, new OsuModNoFail() }), Is.True);
        Assert.That(RealistikRelaxBalance.IsVerticalBalanceEligible(new Mod[] { relax, new OsuModSuddenDeath() }), Is.True);
        Assert.That(RealistikRelaxBalance.IsVerticalBalanceEligible(new Mod[] { relax, new OsuModPerfect() }), Is.True);
        Assert.That(RealistikRelaxBalance.IsVerticalBalanceEligible(new Mod[] { relax, new OsuModClassic() }), Is.True);
        Assert.That(RealistikRelaxBalance.IsVerticalBalanceEligible(new Mod[] { relax, new OsuModDoubleTime() }), Is.False);
        Assert.That(RealistikRelaxBalance.IsVerticalBalanceEligible(new Mod[] { relax, new OsuModNightcore() }), Is.False);
    }

    [Test]
    public void TestAntiFarm5738687ComponentGuard()
    {
        RxBeatmap beatmap = readMap("5738687");
        RxScoreState score = ss(beatmap);
        RxNativePerformanceResult result = MosuRxCalculator.Calculate(new RxCalculationRequest
        {
            Beatmap = beatmap,
            Mods = RxMods.Relax,
            Score = score,
            EffectiveCs = 10,
            ClockRate = 1.5,
        });
        double oldAim = result.PpAim;
        double oldSpeed = result.PpSpeed;
        double oldAccuracy = result.PpAccuracy;
        double oldReading = result.PpReading;

        RealistikRelaxBalance.ApplyComponentGuards(result, new Mod[] { new OsuModRelax(), new OsuModDoubleTime() }, score, beatmap, 10, 1.5);

        Assert.That(result.PpAim, Is.LessThan(oldAim * 0.95));
        Assert.That(result.PpSpeed, Is.EqualTo(oldSpeed));
        Assert.That(result.PpAccuracy, Is.EqualTo(oldAccuracy));
        Assert.That(result.PpReading, Is.EqualTo(oldReading));
    }

    [Test]
    public void TestAntiAbuseOnlyUsesPassedObjects()
    {
        RxBeatmap full = readMap("5738687");
        RxBeatmap prefix = readMap("5738687");
        prefix.HitObjects.RemoveRange(100, prefix.HitObjects.Count - 100);
        RxScoreState score = new RxScoreState
        {
            Count300 = 100,
            MaxCombo = 100,
            PassedObjects = 100,
        };
        RxNativePerformanceResult fullResult = calculate(full, score);
        RxNativePerformanceResult prefixResult = calculate(prefix, score);

        RealistikRelaxBalance.ApplyComponentGuards(fullResult, new Mod[] { new OsuModRelax(), new OsuModDoubleTime() }, score, full, 10, 1.5);
        RealistikRelaxBalance.ApplyComponentGuards(prefixResult, new Mod[] { new OsuModRelax(), new OsuModDoubleTime() }, score, prefix, 10, 1.5);

        Assert.That(fullResult.PpAim, Is.EqualTo(prefixResult.PpAim).Within(0.0002));
        Assert.That(fullResult.PpSpeed, Is.EqualTo(prefixResult.PpSpeed).Within(0.0002));
        Assert.That(RealistikRelaxBalance.VerticalPressure(full, 10, 100, 1.5),
            Is.EqualTo(RealistikRelaxBalance.VerticalPressure(prefix, 10, 100, 1.5)).Within(0.000001));
    }

    [Test]
    public void TestStrongLongPointStreamIsZeroPp()
    {
        RxBeatmap beatmap = createPointStreamBeatmap(64);
        RxScoreState score = ss(beatmap);
        var result = new RxNativePerformanceResult
        {
            Difficulty = new RxDifficultyAttributes
            {
                AimStrain = 0,
                SpeedStrain = 5,
                Cs = 5,
            },
            PpAim = 0,
            PpSpeed = 47,
            PpAccuracy = 114,
            PpReading = 12,
        };
        result.Pp = RealistikRelaxBalance.RebuildTotal(result, score, 1);
        RxDifficultyAttributes difficulty = result.Difficulty;

        RealistikRelaxBalance.ApplyComponentGuards(result, new Mod[] { new OsuModRelax() }, score, beatmap, 5, 1);

        assertZeroPp(result);
        Assert.That(result.Difficulty, Is.SameAs(difficulty));
    }

    [Test]
    public void TestPointStreamGuardIgnoresWeakDetection()
    {
        RxBeatmap beatmap = createNormalStreamBeatmap(64);
        RxScoreState score = ss(beatmap);
        RxNativePerformanceResult result = createLowAimResult();
        result.Pp = RealistikRelaxBalance.RebuildTotal(result, score, 1);
        double oldTotal = result.Pp;
        double oldSpeed = result.PpSpeed;
        double oldAccuracy = result.PpAccuracy;

        RealistikRelaxBalance.ApplyComponentGuards(result, new Mod[] { new OsuModRelax() }, score, beatmap, 5, 1);

        Assert.That(result.PpSpeed, Is.EqualTo(oldSpeed));
        Assert.That(result.PpAccuracy, Is.EqualTo(oldAccuracy));
        Assert.That(result.Pp, Is.EqualTo(oldTotal));
        Assert.That(result.Pp, Is.GreaterThan(0));
    }

    [Test]
    public void TestRepeatedShortPointBurstsAreZeroPp()
    {
        RxBeatmap beatmap = createRepeatedShortPointBurstBeatmap(25);
        RxScoreState score = ss(beatmap);
        RxNativePerformanceResult result = createLowAimResult();
        result.Pp = RealistikRelaxBalance.RebuildTotal(result, score, 1);

        RealistikRelaxBalance.ApplyComponentGuards(result, new Mod[] { new OsuModRelax() }, score, beatmap, 5, 1);

        assertZeroPp(result);
    }

    [Test]
    public void TestMilkCrown1199955DoesNotHardZeroAcrossCircleSizes()
    {
        RxBeatmap beatmap = readMap("1199955");
        RxScoreState score = ss(beatmap);
        double previousTotal = 0;

        foreach (float circleSize in new[] { 3f, 4f, 4.3f, 5f, 6f, 7f, 8f, 10f })
        {
            RxNativePerformanceResult result = calculate(beatmap, score, circleSize, 1);
            double stars = result.Difficulty.Stars;
            double total = applyRxBalance(result, new Mod[] { new OsuModRelax() }, score, beatmap, circleSize, 1);

            Assert.That(total, Is.GreaterThan(0), $"CS{circleSize} must not hard-zero");
            Assert.That(total, Is.GreaterThan(previousTotal), $"CS{circleSize} must remain on the smooth increasing CS curve");
            Assert.That(result.Difficulty.Stars, Is.EqualTo(stars), $"CS{circleSize} stars must be unchanged by balance");
            previousTotal = total;
        }
    }

    [TestCase(0.25)]
    [TestCase(0.50)]
    [TestCase(0.75)]
    [TestCase(1.00)]
    public void TestMilkCrown1199955PlayedPrefixesDoNotHardZero(double completion)
    {
        RxBeatmap beatmap = readMap("1199955");
        uint passedObjects = (uint)Math.Round(beatmap.HitObjects.Count * completion);
        var score = new RxScoreState
        {
            Count300 = passedObjects,
            PassedObjects = passedObjects,
        };
        RxNativePerformanceResult result = calculate(beatmap, score, 5, 1);

        applyRxBalance(result, new Mod[] { new OsuModRelax() }, score, beatmap, 5, 1);

        Assert.That(result.Pp, Is.GreaterThan(0), $"{completion:P0} played prefix must not hard-zero");
    }

    [Test]
    public void TestKnownPointAbuse5332616RemainsZeroAcrossModsAndCircleSizes()
    {
        RxBeatmap beatmap = readMap("5332616");
        RxScoreState score = ss(beatmap);
        (Mod[] Mods, double ClockRate)[] modSets =
        {
            (new Mod[] { new OsuModRelax() }, 1),
            (new Mod[] { new OsuModMosuRelax(), new OsuModHidden() }, 1),
            (new Mod[] { new OsuModRelax(), new OsuModHardRock() }, 1),
            (new Mod[] { new OsuModMosuRelax(), new OsuModDoubleTime() }, 1.5),
            (new Mod[] { new OsuModRelax(), new OsuModNightcore() }, 1.5),
            (new Mod[] { new OsuModRelax(), new OsuModHalfTime() }, 0.75),
        };

        foreach ((Mod[] mods, double clockRate) in modSets)
        {
            foreach (float circleSize in new[] { 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f })
            {
                RxNativePerformanceResult result = calculate(beatmap, score, circleSize, clockRate);
                double stars = result.Difficulty.Stars;

                applyRxBalance(result, mods, score, beatmap, circleSize, clockRate);

                assertZeroPp(result);
                Assert.That(result.Difficulty.Stars, Is.EqualTo(stars));
            }
        }
    }

    [TestCase(0.50)]
    [TestCase(0.75)]
    [TestCase(1.00)]
    [TestCase(1.25)]
    [TestCase(1.50)]
    [TestCase(2.00)]
    public void TestKnownPointAbuse5332616CannotBeBypassedByCustomRate(double clockRate)
    {
        RxBeatmap beatmap = readMap("5332616");
        RxScoreState score = ss(beatmap);
        RxNativePerformanceResult result = calculate(beatmap, score, 5, clockRate);

        applyRxBalance(result, new Mod[] { new OsuModRelax() }, score, beatmap, 5, clockRate);

        assertZeroPp(result);
    }

    [Test]
    public void TestModeratePointSuspicionRetainsPercentagePenalty()
    {
        RxBeatmap beatmap = createRepeatedShortPointBurstBeatmap(15);
        RxScoreState score = ss(beatmap);
        RxNativePerformanceResult result = createLowAimResult();
        double oldSpeed = result.PpSpeed;
        double oldAccuracy = result.PpAccuracy;
        double oldTotal = RealistikRelaxBalance.RebuildTotal(result, score, 1);
        result.Pp = oldTotal;

        RealistikRelaxBalance.ApplyComponentGuards(result, new Mod[] { new OsuModRelax() }, score, beatmap, 5, 1);

        Assert.That(result.Pp, Is.GreaterThan(0));
        Assert.That(result.PpSpeed, Is.GreaterThan(0).And.LessThan(oldSpeed));
        Assert.That(result.PpAccuracy, Is.GreaterThan(0).And.LessThan(oldAccuracy));
        Assert.That(RealistikRelaxBalance.RebuildTotal(result, score, 1), Is.LessThan(oldTotal));
    }

    [Test]
    public void TestPointStreamHardZeroCannotBeBypassedByMods()
    {
        RxBeatmap beatmap = createRepeatedShortPointBurstBeatmap(25);
        RxScoreState score = ss(beatmap);
        Mod[][] modSets =
        {
            new Mod[] { new OsuModRelax() },
            new Mod[] { new OsuModMosuRelax(), new OsuModHidden() },
            new Mod[] { new OsuModRelax(), new OsuModHardRock() },
            new Mod[] { new OsuModMosuRelax(), new OsuModDoubleTime() },
            new Mod[] { new OsuModRelax(), new OsuModNightcore() },
            new Mod[] { new OsuModMosuRelax(), new OsuModNoFail(), new OsuModSuddenDeath(), new OsuModPerfect(), new OsuModClassic() },
        };

        foreach (Mod[] mods in modSets)
        {
            RxNativePerformanceResult result = createLowAimResult();
            result.Pp = RealistikRelaxBalance.RebuildTotal(result, score, 1);
            double clockRate = mods.Any(m => m is OsuModDoubleTime or OsuModNightcore) ? 1.5 : 1;

            RealistikRelaxBalance.ApplyComponentGuards(result, mods, score, beatmap, 5, clockRate);

            assertZeroPp(result);
        }
    }

    [Test]
    public void TestPointStreamGuardOnlyUsesPassedObjects()
    {
        RxBeatmap full = createNormalStreamBeatmap(20);
        RxBeatmap prefix = createNormalStreamBeatmap(20);

        for (int i = 0; i < 64; i++)
        {
            full.HitObjects.Add(new RxHitObject
            {
                Kind = RxHitObjectKind.Circle,
                Position = new RxVec2(256, 192),
                StartTime = (20 + i) * 50,
            });
        }

        var score = new RxScoreState
        {
            Count300 = 20,
            PassedObjects = 20,
        };
        RxNativePerformanceResult fullResult = createLowAimResult();
        RxNativePerformanceResult prefixResult = createLowAimResult();
        fullResult.Pp = RealistikRelaxBalance.RebuildTotal(fullResult, score, 1);
        prefixResult.Pp = RealistikRelaxBalance.RebuildTotal(prefixResult, score, 1);

        RealistikRelaxBalance.ApplyComponentGuards(fullResult, new Mod[] { new OsuModRelax() }, score, full, 5, 1);
        RealistikRelaxBalance.ApplyComponentGuards(prefixResult, new Mod[] { new OsuModRelax() }, score, prefix, 5, 1);

        Assert.That(fullResult.PpSpeed, Is.EqualTo(prefixResult.PpSpeed));
        Assert.That(fullResult.PpAccuracy, Is.EqualTo(prefixResult.PpAccuracy));
        Assert.That(fullResult.Pp, Is.EqualTo(prefixResult.Pp));
        Assert.That(fullResult.Pp, Is.GreaterThan(0));
    }

    [Test]
    public void TestPlayedPrefixContainingPointAbuseIsZeroPp()
    {
        RxBeatmap beatmap = createNormalStreamBeatmap(20);
        for (int i = 0; i < 128; i++)
        {
            beatmap.HitObjects.Add(new RxHitObject
            {
                Kind = RxHitObjectKind.Circle,
                Position = new RxVec2(256, 192),
                StartTime = (20 + i) * 50,
            });
        }

        RxScoreState score = ss(beatmap);
        RxNativePerformanceResult result = createLowAimResult();
        result.Pp = RealistikRelaxBalance.RebuildTotal(result, score, 1);

        RealistikRelaxBalance.ApplyComponentGuards(result, new Mod[] { new OsuModMosuRelax() }, score, beatmap, 5, 1);

        assertZeroPp(result);
    }

    [Test]
    public void TestVanillaDoesNotEnterPointStreamHardZero()
    {
        RxBeatmap beatmap = createPointStreamBeatmap(64);
        RxScoreState score = ss(beatmap);
        RxNativePerformanceResult result = createLowAimResult();
        result.Pp = RealistikRelaxBalance.RebuildTotal(result, score, 1);
        double oldTotal = result.Pp;

        RealistikRelaxBalance.ApplyComponentGuards(result, Array.Empty<Mod>(), score, beatmap, 5, 1);

        Assert.That(result.Pp, Is.EqualTo(oldTotal));
        Assert.That(result.PpSpeed, Is.EqualTo(47));
        Assert.That(result.PpAccuracy, Is.EqualTo(114));
        Assert.That(result.PpReading, Is.EqualTo(12));
    }

    [Test]
    public void TestLengthBonusChangesOnlyAimBeforeNormAssembly()
    {
        RxBeatmap beatmap = readMap("801165");
        RxScoreState score = ss(beatmap);
        RxNativePerformanceResult result = MosuRxCalculator.Calculate(new RxCalculationRequest
        {
            Beatmap = beatmap,
            Mods = RxMods.Relax,
            Score = score,
        });
        double oldTotal = result.Pp;
        double oldAim = result.PpAim;
        double oldSpeed = result.PpSpeed;
        double oldAccuracy = result.PpAccuracy;
        double oldReading = result.PpReading;

        double rebuilt = RealistikRelaxBalance.ApplyLengthAimBonusForTesting(result, 1.08, score);

        Assert.That(result.PpAim, Is.EqualTo(oldAim * 1.08).Within(0.0002));
        Assert.That(result.PpSpeed, Is.EqualTo(oldSpeed));
        Assert.That(result.PpAccuracy, Is.EqualTo(oldAccuracy));
        Assert.That(result.PpReading, Is.EqualTo(oldReading));
        Assert.That(rebuilt, Is.GreaterThan(oldTotal));
        Assert.That(rebuilt, Is.LessThan(oldTotal * 1.08));
    }

    [TestCase(8d, 0.80d)]
    [TestCase(9d, 0.90d)]
    [TestCase(10d, 1.00d)]
    [TestCase(11d, 1.05d)]
    public void TestCustomOdCurve(double od, double expected)
        => Assert.That(RealistikRelaxBalance.OdMultiplier(od), Is.EqualTo(expected).Within(0.000001));

    [TestCase("2496318")]
    [TestCase("4766797")]
    [TestCase("801165")]
    public void TestOd11HasOnlyCustomFivePercentReward(string mapName)
    {
        RxBeatmap beatmap = readMap(mapName);
        RxScoreState score = ss(beatmap);
        RxNativePerformanceResult od10 = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, effectiveOd: 10f);
        RxNativePerformanceResult od11 = MosuRxCalculator.CalculateSs(beatmap, RxMods.Relax, effectiveOd: 11f);
        Mod[] mods = { new OsuModRelax() };
        double finalOd10 = applyRxBalance(od10, mods, score, beatmap, beatmap.CircleSize, 1);
        double finalOd11 = applyRxBalance(od11, mods, score, beatmap, beatmap.CircleSize, 1);

        Assert.Multiple(() =>
        {
            Assert.That(od11.PpAim, Is.EqualTo(od10.PpAim).Within(0.0002));
            Assert.That(od11.PpSpeed, Is.EqualTo(od10.PpSpeed).Within(0.0002));
            Assert.That(od11.PpAccuracy, Is.EqualTo(od10.PpAccuracy).Within(0.0002));
            Assert.That(od11.PpReading, Is.EqualTo(od10.PpReading).Within(0.0002));
            Assert.That(finalOd11 / finalOd10, Is.EqualTo(1.05).Within(0.000001));
        });
    }

    [Test]
    public void TestCustomOdCurveIsContinuousAtOd9AndOd10()
    {
        foreach (double knot in new[] { 9d, 10d, 11d })
        {
            double left = RealistikRelaxBalance.OdMultiplier(knot - 0.001);
            double centre = RealistikRelaxBalance.OdMultiplier(knot);
            double right = RealistikRelaxBalance.OdMultiplier(knot + 0.001);

            Assert.That(left, Is.LessThanOrEqualTo(centre));
            Assert.That(centre, Is.LessThanOrEqualTo(right));
            Assert.That(right - left, Is.LessThan(0.001));
        }
    }

    private static RxNativePerformanceResult calculate(RxBeatmap beatmap, RxScoreState score, float effectiveCs = 10, double clockRate = 1.5)
        => MosuRxCalculator.Calculate(new RxCalculationRequest
        {
            Beatmap = beatmap,
            Mods = RxMods.Relax,
            Score = score,
            EffectiveCs = effectiveCs,
            ClockRate = clockRate,
        });

    private static double applyRxBalance(
        RxNativePerformanceResult result,
        Mod[] mods,
        RxScoreState score,
        RxBeatmap beatmap,
        float circleSize,
        double clockRate)
    {
        double nativeMultiplier = result.Pp / Math.Max(RealistikRelaxBalance.RebuildTotal(result, score, 1), double.Epsilon);
        RealistikRelaxBalance.ApplyComponentGuards(result, mods, score, beatmap, circleSize, clockRate);
        if (result.Pp == 0)
            return 0;

        double patternMultiplier = RealistikRelaxBalance.PatternMultiplier(mods, result);
        result.PpAim *= RealistikRelaxBalance.LengthAimMultiplier(mods, result);
        result.Pp = RealistikRelaxBalance.RebuildTotal(result, score, nativeMultiplier);

        if (RealistikRelaxBalance.IsVerticalBalanceEligible(mods))
        {
            double verticalPressure = RealistikRelaxBalance.VerticalPressure(beatmap, circleSize, score.PassedObjects, clockRate);
            result.PpAim *= 1 - 0.06 * verticalPressure;
            result.Pp = RealistikRelaxBalance.RebuildTotal(result, score, nativeMultiplier);
        }

        return result.Pp * RealistikRelaxBalance.OdMultiplier(result.Difficulty.Od) * patternMultiplier;
    }

    private static RxScoreState ss(RxBeatmap beatmap)
        => new RxScoreState
        {
            Count300 = (uint)beatmap.HitObjects.Count,
            PassedObjects = (uint)beatmap.HitObjects.Count,
        };

    private static RxNativePerformanceResult createLowAimResult()
        => new RxNativePerformanceResult
        {
            Difficulty = new RxDifficultyAttributes
            {
                AimStrain = 0,
                SpeedStrain = 5,
                Cs = 5,
            },
            PpAim = 0,
            PpSpeed = 47,
            PpAccuracy = 114,
            PpReading = 12,
        };

    private static void assertZeroPp(RxNativePerformanceResult result)
    {
        Assert.That(result.Pp, Is.Zero);
        Assert.That(result.PpAim, Is.Zero);
        Assert.That(result.PpSpeed, Is.Zero);
        Assert.That(result.PpAccuracy, Is.Zero);
        Assert.That(result.PpReading, Is.Zero);
    }

    private static RxBeatmap createPointStreamBeatmap(int count)
    {
        var beatmap = new RxBeatmap { CircleSize = 5 };

        for (int i = 0; i < count; i++)
        {
            beatmap.HitObjects.Add(new RxHitObject
            {
                Kind = RxHitObjectKind.Circle,
                Position = new RxVec2(256, 192),
                StartTime = i * 50,
            });
        }

        return beatmap;
    }

    private static RxBeatmap createNormalStreamBeatmap(int count)
    {
        var beatmap = new RxBeatmap { CircleSize = 5 };

        for (int i = 0; i < count; i++)
        {
            beatmap.HitObjects.Add(new RxHitObject
            {
                Kind = RxHitObjectKind.Circle,
                Position = new RxVec2(i % 2 == 0 ? 128 : 384, 192),
                StartTime = i * 50,
            });
        }

        return beatmap;
    }

    private static RxBeatmap createRepeatedShortPointBurstBeatmap(int burstCount)
    {
        var beatmap = new RxBeatmap { CircleSize = 5 };
        double time = 0;

        for (int burst = 0; burst < burstCount; burst++)
        {
            for (int i = 0; i < 3; i++)
            {
                beatmap.HitObjects.Add(new RxHitObject
                {
                    Kind = RxHitObjectKind.Circle,
                    Position = new RxVec2(256, 192),
                    StartTime = time,
                });
                time += 50;
            }

            time += 150;
        }

        return beatmap;
    }

    private static RxBeatmap readMap(string name)
    {
        Assembly assembly = typeof(RealistikRelaxBalanceTest).Assembly;
        string resourceName = $"osu.Game.Rulesets.Osu.Tests.Resources.Testing.Beatmaps.{name}.osu";
        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return OsuBeatmapParser.Parse(reader.ReadToEnd());
    }
}
