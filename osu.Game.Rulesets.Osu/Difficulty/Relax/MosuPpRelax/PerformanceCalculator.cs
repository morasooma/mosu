using System;

namespace MosuPpRxCs;

internal static class RxPerformanceCalculator
{
    private const float HIGH_STAR_AR9_BONUS_CAP = 0.10f;
    private const float STREAM_CS_BONUS_AT_5 = 0.23f;
    private const float STREAM_CS_BONUS_AT_6 = 0.26f;
    private const float STREAM_CS_BONUS_AT_7 = 0.29f;
    private const float STREAM_CS_BONUS_CAP = 0.32f;
    private const float LOW_AR_EXTRA_READING_CAP = 0.80f;
    private const float ORDINARY_STREAM_COMPONENT_BONUS = 0.10f;
    private const float DT_HIGH_CS_STREAM_BONUS_CAP = 0.20f;
    private const float MAX_COMBINED_STREAM_COMPONENT_BONUS = 0.65f;
    private const float SUSTAINED_WIDE_JUMP_AIM_BONUS = 0.22f;
    private const float HD_EZ_COMPONENT_BONUS = 0.15f;

    public static RxNativePerformanceResult Calculate(
        RxDifficultyAttributes attributes,
        RxScoreState score,
        RxMods mods)
    {
        uint objectCount = (uint)(attributes.CircleCount + attributes.SliderCount + attributes.SpinnerCount);
        if (score.PassedObjects.HasValue)
            objectCount = Math.Min(score.PassedObjects.Value, objectCount);

        uint n300 = score.Count300;
        uint n100 = score.Count100;
        uint n50 = score.Count50;
        uint misses = score.Misses;

        uint totalHits = Math.Min(n300 + n100 + n50 + misses, objectCount);
        float accuracy = objectCount == 0
            ? 0f
            : (6f * n300 + 2f * n100 + n50) / (6f * objectCount);

        float totalHitsF = totalHits;
        float effectiveMissCount = CalculateEffectiveMissCount(attributes, score, n100, n50, misses);
        if (attributes.CircleCount > 0 && attributes.CircleCount <= attributes.PointSpamCircleCount)
        {
            return new RxNativePerformanceResult
            {
                Difficulty = attributes,
                Pp = 0,
                PpAim = 0,
                PpSpeed = 0,
                PpAccuracy = 0,
                PpReading = 0,
                EffectiveMissCount = effectiveMissCount,
            };
        }
        float multiplier = 1.09f;

        if (mods.HasFlag(RxMods.SpunOut) && totalHitsF > 0f)
        {
            multiplier *= 1f - MathF.Pow(attributes.SpinnerCount / totalHitsF, 0.85f);
        }

        float aimValue = ComputeAimValue(attributes, mods, accuracy, totalHitsF, effectiveMissCount);
        float speedValue = ComputeSpeedValue(attributes, accuracy, totalHitsF, effectiveMissCount, n50);
        float accuracyValue = ComputeAccuracyValue(attributes, mods, totalHitsF, n300, n100, n50);
        float readingValue = ComputeReadingValue(attributes, mods, accuracy, effectiveMissCount);

        float accDepression = 1f;
        double aimStrainForStreamsNerf = attributes.AimStrainBeforeHighCsCompression > 0
            ? attributes.AimStrainBeforeHighCsCompression
            : attributes.AimStrain;
        double streamsNerf = Math.Round(aimStrainForStreamsNerf / attributes.SpeedStrain, 2, MidpointRounding.AwayFromZero);

        if (streamsNerf < 1.09)
        {
            float accuracyFactor = MathF.Abs(1f - accuracy);
            accDepression = MathF.Max(0.78f - accuracyFactor, 0.5f);

            if (accDepression > 0f)
            {
                aimValue *= accDepression;
                speedValue *= accDepression;
            }
        }

        float streamWeight = (float)attributes.CanonicalStreamWeight;
        float streamConfidence = RxMath.SmoothStep(streamWeight, 0.45f, 0.75f);
        float ordinaryStreamWeight = RxMath.SmoothStep(streamWeight, 0.55f, 0.90f);
        float streamCsBonus = StreamCsBonus((float)attributes.Cs) * streamConfidence;
        float dtHighCsBonus = DT_HIGH_CS_STREAM_BONUS_CAP
                              * streamConfidence
                              * RxMath.SmoothStep((float)attributes.Cs, 5f, 6f)
                              * RxMath.SmoothStep((float)attributes.ClockRate, 1f, 1.5f);

        float mixedFlowJumpWeight = RxMath.SmoothStep((float)attributes.CustomFlowAimBonusRatio, 0.08f, 0.15f)
                                    * RxMath.SmoothStep((float)attributes.JumpSpikeFillerWeight, 0.35f, 0.80f);
        float combinedStreamBonusCap = MAX_COMBINED_STREAM_COMPONENT_BONUS
                                       + (0.12f - MAX_COMBINED_STREAM_COMPONENT_BONUS) * mixedFlowJumpWeight;
        float combinedStreamBonus = MathF.Min(
            combinedStreamBonusCap,
            ORDINARY_STREAM_COMPONENT_BONUS * ordinaryStreamWeight + streamCsBonus + dtHighCsBonus);
        if (combinedStreamBonus > 0f)
        {
            aimValue *= 1f + combinedStreamBonus;
            speedValue *= 1f + combinedStreamBonus;
        }

        float sustainedWideJumpBonus = SUSTAINED_WIDE_JUMP_AIM_BONUS
                                       * RxMath.SmoothStep((float)attributes.SustainedWideJumpWeight, 0.25f, 0.75f)
                                       * (1f - streamConfidence);
        if (sustainedWideJumpBonus > 0f)
            aimValue *= 1f + sustainedWideJumpBonus;

        if (mods.HasFlag(RxMods.Hidden) && mods.HasFlag(RxMods.Easy))
        {
            aimValue *= 1f + HD_EZ_COMPONENT_BONUS;
            speedValue *= 1f + HD_EZ_COMPONENT_BONUS;
            readingValue *= 1f + HD_EZ_COMPONENT_BONUS;
        }

        float pp = MathF.Pow(
            MathF.Pow(aimValue, 1.185f)
            + MathF.Pow(speedValue, 0.83f * accDepression)
            + MathF.Pow(accuracyValue, 1.14f)
            + MathF.Pow(readingValue, 1.1f),
            1f / 1.1f)
            * multiplier;

        return new RxNativePerformanceResult
        {
            Difficulty = attributes,
            Pp = pp,
            PpAim = aimValue,
            PpSpeed = speedValue,
            PpAccuracy = accuracyValue,
            PpReading = readingValue,
            EffectiveMissCount = effectiveMissCount,
        };
    }

    private static float ComputeAimValue(
        RxDifficultyAttributes attributes,
        RxMods mods,
        float accuracy,
        float totalHits,
        float effectiveMissCount)
    {
        float rawAim = mods.HasFlag(RxMods.TouchDevice)
            ? MathF.Pow((float)(attributes.AimStrainForPp > 0 ? attributes.AimStrainForPp : attributes.AimStrain), 0.8f)
            : (float)(attributes.AimStrainForPp > 0 ? attributes.AimStrainForPp : attributes.AimStrain);

        float aimValue = MathF.Pow(5f * MathF.Max(rawAim / 0.0675f, 1f) - 4f, 3f) / 100000f;

        float lengthBonus = 0.79f
            + 0.59f * MathF.Min(totalHits / 2000f, 1f)
            + (totalHits > 2000f ? -0.1f * MathF.Log10(totalHits / 2000f) : 0f);

        if (lengthBonus > 1f)
            lengthBonus = MathF.Pow(lengthBonus, 0.88f);

        aimValue *= lengthBonus;

        if (effectiveMissCount > 0f)
            aimValue *= CalculateMissPenalty(effectiveMissCount, attributes.AimDifficultStrainCount);

        if (mods.HasFlag(RxMods.Flashlight))
        {
            aimValue *= 1f
                + 0.3f * MathF.Min(totalHits / 200f, 1f)
                + (totalHits > 200f ? 0.25f * MathF.Min((totalHits - 200f) / 300f, 1f) : 0f)
                + (totalHits > 500f ? (totalHits - 500f) / 1600f : 0f);
        }

        aimValue *= MathF.Pow(accuracy, 1.5f) * 0.85f;
        float od = MathF.Min((float)attributes.Od, 10f);
        aimValue *= 0.98f + od * od / 2500f;
        return aimValue;
    }

    private static float ComputeSpeedValue(
        RxDifficultyAttributes attributes,
        float accuracy,
        float totalHits,
        float effectiveMissCount,
        uint n50)
    {
        double speedStrainSource = attributes.SpeedStrainForPp > 0 ? attributes.SpeedStrainForPp : attributes.SpeedStrain;
        float speedValue = MathF.Pow(
            5f * MathF.Max((float)speedStrainSource / 0.0675f, 1f) - 4f,
            3f) / 100000f;

        float lengthBonus = 0.81f
            + 0.55f * MathF.Min(totalHits / 2000f, 1f)
            + (totalHits > 2000f ? -0.1f * MathF.Log10(totalHits / 2000f) : 0f);

        if (lengthBonus > 1f)
            lengthBonus = MathF.Pow(lengthBonus, 0.88f);

        speedValue *= lengthBonus;

        if (effectiveMissCount > 0f)
            speedValue *= CalculateMissPenalty(effectiveMissCount, attributes.SpeedDifficultStrainCount);

        float od = MathF.Min((float)attributes.Od, 10f);
        speedValue *= (0.93f + od * od / 750f)
            * MathF.Pow(accuracy, (14.5f - MathF.Max(od, 8f)) / 2f);

        float exponent = n50 < totalHits / 500f
            ? 0f
            : n50 - totalHits / 500f;

        speedValue *= MathF.Pow(0.98f, exponent);
        return speedValue;
    }

    private static float ComputeAccuracyValue(
        RxDifficultyAttributes attributes,
        RxMods mods,
        float totalHits,
        uint n300,
        uint n100,
        uint n50)
    {
        float circles = attributes.CircleCount;
        float spamCircles = attributes.PointSpamCircleCount;
        float effectiveCircles = MathF.Max(0f, circles - spamCircles);
        float betterAccuracyPercentage = 0f;

        if (effectiveCircles > 0f)
        {
            float effective300 = MathF.Max(0f, (float)n300 - spamCircles);
            float effectiveTotalHits = MathF.Max(0f, totalHits - spamCircles);

            betterAccuracyPercentage = MathF.Max(
                (((effective300 - (effectiveTotalHits - effectiveCircles)) * 6f + n100 * 2f + n50)
                / (effectiveCircles * 6f)),
                0f);
        }

        float od = MathF.Min((float)attributes.Od, 10f);
        float accuracyValue = MathF.Pow(1.52163f, od)
            * MathF.Pow(betterAccuracyPercentage, 24f)
            * 2.83f;

        accuracyValue *= MathF.Min(MathF.Pow(effectiveCircles / 1000f, 0.3f), 1.15f);

        if (mods.HasFlag(RxMods.Flashlight))
            accuracyValue *= 1.02f;

        return accuracyValue;
    }

    private static float ComputeReadingValue(
        RxDifficultyAttributes attributes,
        RxMods mods,
        float accuracy,
        float effectiveMissCount)
    {
        double readingStrain = attributes.ReadingStrainForPerformance > 0
            ? attributes.ReadingStrainForPerformance
            : attributes.ReadingStrain;
        float readingValue = ReadingValueFromStrain(readingStrain);

        if (effectiveMissCount > 0f)
            readingValue *= CalculateMissPenalty(effectiveMissCount, attributes.ReadingDifficultNoteCount);

        readingValue *= MathF.Pow(accuracy, 4f);

        if (attributes.Ar < 9d && attributes.ReadingStrainAtAr9ForPerformance > 0d)
        {
            float ar9Value = ReadingValueFromStrain(attributes.ReadingStrainAtAr9ForPerformance);

            if (effectiveMissCount > 0f)
                ar9Value *= CalculateMissPenalty(effectiveMissCount, attributes.ReadingDifficultNoteCount);

            ar9Value *= MathF.Pow(accuracy, 4f);
            float existingBonus = MathF.Max(readingValue - ar9Value, 0f);
            readingValue += LowArExtraReadingBonus(readingValue, existingBonus, (float)attributes.Ar);
        }

        readingValue *= 1f + HighStarAr9ReadingBonus((float)attributes.Ar, (float)attributes.Stars);

        return readingValue;
    }

    private static float ReadingValueFromStrain(double readingStrain)
        => MathF.Pow(
            5f * MathF.Max((float)readingStrain / 0.0675f, 1f) - 4f,
            3f) / 265000f;

    internal static float LowArBonusAmplification(float ar)
    {
        if (ar >= 9f)
            return 1f;

        float progress = RxMath.SmoothStep(RxMath.Clamp(ar, 0f, 9f), 0f, 9f);
        return 1f + 1.5f * (1f - MathF.Pow(progress, 1.5f));
    }

    internal static float LowArExtraReadingBonus(float readingValue, float existingBonus, float ar)
    {
        float extraStrength = LowArBonusAmplification(ar) - 1f;
        float uncappedExtra = existingBonus * extraStrength;
        float normalisedStrength = extraStrength / 1.5f;
        float cap = readingValue * LOW_AR_EXTRA_READING_CAP * normalisedStrength;
        return MathF.Min(uncappedExtra, cap);
    }

    internal static float HighStarAr9ReadingBonus(float ar, float stars)
    {
        float arWeight;

        if (ar <= 8.7f || ar >= 9.3f)
            arWeight = 0f;
        else if (ar < 9f)
            arWeight = RxMath.SmoothStep(ar, 8.7f, 9f);
        else
            arWeight = 1f - MathF.Pow(RxMath.SmoothStep(ar, 9f, 9.3f), 3f);

        float starWeight = RxMath.SmoothStep(stars, 8.5f, 10f);
        return HIGH_STAR_AR9_BONUS_CAP * arWeight * starWeight;
    }

    private static float CalculateMissPenalty(float effectiveMissCount, float difficultStrainCount)
        => 0.96f / (effectiveMissCount / (4f * MathF.Pow(MathF.Log(difficultStrainCount), 0.94f)) + 1f);

    private static float StreamCsBonus(float cs)
    {
        if (cs <= 2f)
            return 0f;
        if (cs < 3f)
            return 0.10f * RxMath.SmoothStep(cs, 2f, 3f);
        if (cs < 4f)
            return 0.10f + (0.20f - 0.10f) * RxMath.SmoothStep(cs, 3f, 4f);
        if (cs < 5f)
            return 0.20f
                   + (STREAM_CS_BONUS_AT_5 - 0.20f) * RxMath.SmoothStep(cs, 4f, 5f);
        if (cs < 6f)
            return STREAM_CS_BONUS_AT_5
                   + (STREAM_CS_BONUS_AT_6 - STREAM_CS_BONUS_AT_5) * RxMath.SmoothStep(cs, 5f, 6f);
        if (cs < 7f)
            return STREAM_CS_BONUS_AT_6
                   + (STREAM_CS_BONUS_AT_7 - STREAM_CS_BONUS_AT_6) * RxMath.SmoothStep(cs, 6f, 7f);
        if (cs < 8f)
            return STREAM_CS_BONUS_AT_7
                   + (STREAM_CS_BONUS_CAP - STREAM_CS_BONUS_AT_7) * RxMath.SmoothStep(cs, 7f, 8f);
        return STREAM_CS_BONUS_CAP;
    }

    private static float CalculateEffectiveMissCount(
        RxDifficultyAttributes attributes,
        RxScoreState score,
        uint n100,
        uint n50,
        uint misses)
    {
        float comboBasedMissCount = 0f;
        float combo = score.MaxCombo ?? (uint)attributes.MaxCombo;

        if (attributes.SliderCount > 0)
        {
            float fcThreshold = attributes.MaxCombo - 0.1f * attributes.SliderCount;
            if (combo < fcThreshold)
                comboBasedMissCount = fcThreshold / MathF.Max(combo, 1f);
        }

        float playedSliderCapacity = attributes.SliderCount;
        float lostComboCapacity = MathF.Max(0f, attributes.MaxCombo - combo) / 2f;
        comboBasedMissCount = MathF.Min(comboBasedMissCount, MathF.Min(playedSliderCapacity, lostComboCapacity));
        return MathF.Max(comboBasedMissCount, misses);
    }
}
