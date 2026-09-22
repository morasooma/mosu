// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace MosuRxPureCs;

/// <summary>
/// Pure C# implementation of the native Realistik RX difficulty and performance calculation.
/// This layer intentionally contains no Mosu post-balance rules.
/// </summary>
internal static class MosuRxCalculator
{
    public static RxNativePerformanceResult Calculate(RxCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Beatmap);
        ArgumentNullException.ThrowIfNull(request.Score);

        if (request.Beatmap.HitObjects.Count == 0)
            throw new ArgumentException("Beatmap contains no hit objects.", nameof(request));

        resolveDifficulty(request, out float ar, out float od, out float cs, out float hp, out double clockRate);

        uint? passedObjects = request.Score.PassedObjects;

        if (!passedObjects.HasValue)
        {
            uint scoreObjects = request.Score.Count300
                                + request.Score.Count100
                                + request.Score.Count50
                                + request.Score.Misses;

            if (scoreObjects > 0)
                passedObjects = scoreObjects;
        }

        RxDifficultyAttributes difficulty = RxDifficultyCalculator.Calculate(
            request.Beatmap,
            request.Mods,
            ar,
            od,
            cs,
            hp,
            clockRate,
            passedObjects);

        return RxPerformanceCalculator.Calculate(
            difficulty,
            new RxScoreState
            {
                Count300 = request.Score.Count300,
                Count100 = request.Score.Count100,
                Count50 = request.Score.Count50,
                Misses = request.Score.Misses,
                MaxCombo = request.Score.MaxCombo,
                PassedObjects = passedObjects,
            },
            request.Mods);
    }

    public static RxNativePerformanceResult CalculateSs(
        RxBeatmap beatmap,
        RxMods mods = RxMods.Relax,
        float? effectiveAr = null,
        float? effectiveOd = null,
        float? effectiveCs = null,
        float? effectiveHp = null,
        double? clockRate = null)
    {
        uint objectCount = (uint)beatmap.HitObjects.Count;

        return Calculate(new RxCalculationRequest
        {
            Beatmap = beatmap,
            Mods = mods,
            Score = new RxScoreState
            {
                Count300 = objectCount,
                PassedObjects = objectCount,
            },
            EffectiveAr = effectiveAr,
            EffectiveOd = effectiveOd,
            EffectiveCs = effectiveCs,
            EffectiveHp = effectiveHp,
            ClockRate = clockRate,
        });
    }

    private static void resolveDifficulty(
        RxCalculationRequest request,
        out float ar,
        out float od,
        out float cs,
        out float hp,
        out double clockRate)
    {
        RxBeatmap map = request.Beatmap;
        ar = request.EffectiveAr ?? map.ApproachRate;
        od = request.EffectiveOd ?? map.OverallDifficulty;
        cs = request.EffectiveCs ?? map.CircleSize;
        hp = request.EffectiveHp ?? map.HpDrainRate;
        clockRate = request.ClockRate ?? defaultClockRate(request.Mods);

        if (clockRate <= 0 || double.IsNaN(clockRate) || double.IsInfinity(clockRate))
            throw new ArgumentOutOfRangeException(nameof(request.ClockRate), "Clock rate must be finite and greater than zero.");
    }

    private static double defaultClockRate(RxMods mods)
    {
        if (mods.HasFlag(RxMods.DoubleTime) || mods.HasFlag(RxMods.Nightcore))
            return 1.5;

        if (mods.HasFlag(RxMods.HalfTime))
            return 0.75;

        return 1;
    }
}
