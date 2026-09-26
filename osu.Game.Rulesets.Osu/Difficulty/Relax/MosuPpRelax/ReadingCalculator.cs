using System;
using System.Collections.Generic;
using System.Linq;

namespace MosuPpRxCs;

internal readonly record struct ReadingObject(
    float Time,
    float JumpDistance,
    float StrainTime,
    float Delta,
    float? Angle,
    float? NormalisedVectorAngle);

internal readonly record struct ReadingParameters(
    float TimePreemptRaw,
    float Preempt,
    float TimeFadeInRaw,
    float HdFadeInRaw,
    float ClockRate,
    bool Hidden);

internal static class ReadingEvaluator
{
    private const float READING_WINDOW_SIZE = 3000f;
    private const float NORMALISED_RADIUS = 52f;
    private const float NORMALISED_DIAMETER = NORMALISED_RADIUS * 2f;
    private const float DISTANCE_INFLUENCE_THRESHOLD = NORMALISED_DIAMETER * 1.5f;
    private const float HIDDEN_MULTIPLIER = 0.28f;
    private const float DENSITY_MULTIPLIER = 2.4f;
    private const float DENSITY_DIFFICULTY_BASE = 2.5f;
    private const float PREEMPT_BALANCING_FACTOR = 140000f;
    private const float PREEMPT_STARTING_POINT = 500f;
    private const float MINIMUM_ANGLE_RELEVANCY_TIME = 2000f;
    private const float MAXIMUM_ANGLE_RELEVANCY_TIME = 200f;
    private const float HD_FADE_OUT_DURATION_MULTIPLIER = 0.3f;

    public static float EvaluateDifficultyOf(
        IReadOnlyList<ReadingObject> objects,
        int index,
        ReadingParameters parameters)
    {
        if (index == 0)
            return 0f;

        ReadingObject current = objects[index];
        float velocity = MathF.Max(1f, current.JumpDistance / current.StrainTime);

        float currentVisibleDensity = RetrieveCurrentVisibleObjectDensity(objects, index, parameters);
        float pastInfluence = GetPastObjectDifficultyInfluence(objects, index, parameters);
        float constantAngleNerf = GetConstantAngleNerfFactor(objects, index);

        ReadingObject? next = index + 1 < objects.Count ? objects[index + 1] : null;

        float densityDifficulty = CalculateDensityDifficulty(
            next,
            velocity,
            constantAngleNerf,
            pastInfluence,
            currentVisibleDensity);

        float hiddenDifficulty = parameters.Hidden
            ? CalculateHiddenDifficulty(
                objects,
                index,
                parameters,
                pastInfluence,
                currentVisibleDensity,
                velocity,
                constantAngleNerf)
            : 0f;

        float preemptDifficulty = CalculatePreemptDifficulty(
            velocity,
            constantAngleNerf,
            parameters.Preempt);

        return RxMath.Norm(1.5f, preemptDifficulty, hiddenDifficulty, densityDifficulty);
    }

    private static float CalculateDensityDifficulty(
        ReadingObject? next,
        float velocity,
        float constantAngleNerf,
        float pastInfluence,
        float currentVisibleDensity)
    {
        float futureInfluence = MathF.Sqrt(currentVisibleDensity);

        if (next is { } nextObject)
        {
            futureInfluence *= RxMath.SmootherStep(
                nextObject.JumpDistance,
                15f,
                DISTANCE_INFLUENCE_THRESHOLD);
        }

        float difficulty = MathF.Pow(pastInfluence + futureInfluence, 1.7f)
            * 0.4f
            * constantAngleNerf
            * velocity;

        difficulty = MathF.Max(difficulty - DENSITY_DIFFICULTY_BASE, 0f);
        difficulty = MathF.Pow(difficulty, 0.45f) * DENSITY_MULTIPLIER;
        return difficulty;
    }

    private static float CalculatePreemptDifficulty(
        float velocity,
        float constantAngleNerf,
        float preempt)
    {
        float difference = PREEMPT_STARTING_POINT - preempt;
        float halfRect = (difference + MathF.Abs(difference)) / 2f;
        float preemptDifficulty = MathF.Pow(halfRect, 2.5f) / PREEMPT_BALANCING_FACTOR;
        return preemptDifficulty * constantAngleNerf * velocity;
    }

    private static float CalculateHiddenDifficulty(
        IReadOnlyList<ReadingObject> objects,
        int index,
        ReadingParameters parameters,
        float pastInfluence,
        float currentVisibleDensity,
        float velocity,
        float constantAngleNerf)
    {
        float timeSpentInvisible = (
            parameters.HdFadeInRaw
            + parameters.TimePreemptRaw * HD_FADE_OUT_DURATION_MULTIPLIER)
            / parameters.ClockRate;

        float timeInvisibleFactor = MathF.Pow(timeSpentInvisible, 2.2f) * 0.022f;
        float densityFactor = MathF.Pow(currentVisibleDensity + pastInfluence, 3.3f) * 3f;

        float hiddenDifficulty = (timeInvisibleFactor + densityFactor)
            * constantAngleNerf
            * velocity
            * 0.01f;

        hiddenDifficulty = MathF.Pow(hiddenDifficulty, 0.4f) * HIDDEN_MULTIPLIER;

        if (index > 0)
        {
            ReadingObject current = objects[index];
            ReadingObject previous = objects[index - 1];

            if (current.JumpDistance == 0f)
            {
                float previousClickWithPreempt = previous.Time + parameters.TimePreemptRaw;
                float currentOpacity = OpacityAt(
                    current.Time,
                    previousClickWithPreempt,
                    parameters,
                    true);

                if (currentOpacity == 0f && previousClickWithPreempt > current.Time)
                {
                    hiddenDifficulty += HIDDEN_MULTIPLIER
                        * 7500f
                        / MathF.Pow(current.StrainTime, 1.5f);
                }
            }
        }

        return hiddenDifficulty;
    }

    private static float GetPastObjectDifficultyInfluence(
        IReadOnlyList<ReadingObject> objects,
        int index,
        ReadingParameters parameters)
    {
        ReadingObject current = objects[index];
        float influence = 0f;

        for (int i = index - 1; i >= 0; i--)
        {
            ReadingObject loopObject = objects[i];
            float timeGap = current.Time - loopObject.Time;

            if (timeGap > READING_WINDOW_SIZE)
                break;
            if (loopObject.Time + parameters.TimePreemptRaw < current.Time)
                break;

            float loopDifficulty = OpacityAt(
                current.Time,
                loopObject.Time,
                parameters,
                false);

            float distanceFactor = RxMath.SmootherStep(
                loopObject.JumpDistance,
                15f,
                DISTANCE_INFLUENCE_THRESHOLD);

            float timeNerf = GetTimeNerfFactor(timeGap);
            influence += loopDifficulty * distanceFactor * timeNerf;
        }

        return influence;
    }

    private static float RetrieveCurrentVisibleObjectDensity(
        IReadOnlyList<ReadingObject> objects,
        int index,
        ReadingParameters parameters)
    {
        ReadingObject current = objects[index];
        float density = 0f;

        for (int i = index + 1; i < objects.Count; i++)
        {
            ReadingObject hitObject = objects[i];
            float timeGap = hitObject.Time - current.Time;

            if (timeGap > READING_WINDOW_SIZE)
                break;
            if (current.Time + parameters.TimePreemptRaw < hitObject.Time)
                break;

            float opacity = OpacityAt(
                hitObject.Time,
                current.Time,
                parameters,
                false);

            float timeNerf = GetTimeNerfFactor(timeGap);
            density += opacity * timeNerf;
        }

        return density;
    }

    private static float GetConstantAngleNerfFactor(
        IReadOnlyList<ReadingObject> objects,
        int index)
    {
        ReadingObject current = objects[index];
        float constantAngleCount = 0f;
        float currentTimeGap = 0f;
        int i = 0;

        while (currentTimeGap < MINIMUM_ANGLE_RELEVANCY_TIME)
        {
            if (i >= index)
                break;

            ReadingObject loopObject = objects[index - 1 - i];
            float longIntervalFactor = 1f - ReverseLerp(
                loopObject.StrainTime,
                MAXIMUM_ANGLE_RELEVANCY_TIME,
                MINIMUM_ANGLE_RELEVANCY_TIME);

            if (current.Angle is { } currentAngle && loopObject.Angle is { } loopAngle)
            {
                float angleDifference = MathF.Abs(currentAngle - loopAngle);
                float stackFactor = RxMath.SmootherStep(
                    loopObject.JumpDistance,
                    0f,
                    NORMALISED_RADIUS);

                float clampedAngle = angleDifference * stackFactor;
                clampedAngle = MathF.Min(clampedAngle, 30f * MathF.PI / 180f);
                constantAngleCount += MathF.Cos(3f * clampedAngle) * longIntervalFactor;
            }

            currentTimeGap = current.Time - loopObject.Time;
            i++;
        }

        return RxMath.Clamp(2f / constantAngleCount, 0.2f, 1f);
    }

    private static float OpacityAt(
        float objectTime,
        float queryTime,
        ReadingParameters parameters,
        bool hidden)
    {
        float fadeInStart = objectTime - parameters.TimePreemptRaw;
        float fadeInDuration = parameters.TimeFadeInRaw;

        if (hidden)
        {
            float fadeOutStart = objectTime - parameters.TimePreemptRaw + fadeInDuration;
            float fadeOutDuration = parameters.TimePreemptRaw * HD_FADE_OUT_DURATION_MULTIPLIER;
            float fadeIn = RxMath.Clamp((queryTime - fadeInStart) / fadeInDuration, 0f, 1f);
            float fadeOut = RxMath.Clamp((queryTime - fadeOutStart) / fadeOutDuration, 0f, 1f);
            return fadeIn * (1f - fadeOut);
        }

        return RxMath.Clamp((queryTime - fadeInStart) / fadeInDuration, 0f, 1f);
    }

    private static float GetTimeNerfFactor(float deltaTime)
        => RxMath.Clamp(2f - deltaTime / (READING_WINDOW_SIZE / 2f), 0f, 1f);

    private static float ReverseLerp(float value, float start, float end)
        => RxMath.Clamp((value - start) / (end - start), 0f, 1f);
}

internal sealed class ReadingSkill
{
    private const float SKILL_MULTIPLIER = 2.5f;
    private const float STRAIN_DECAY_BASE = 0.8f;
    private const float HARMONIC_SCALE = 1f;
    private const float DECAY_EXPONENT = 0.9f;
    private const float REDUCED_DIFFICULTY_BASE_LINE = 0f;
    private const float REDUCED_DIFFICULTY_DURATION = 60_000f;

    private readonly List<float> objectDifficulties = new();
    private readonly List<float> objectTimes = new();
    private float currentDifficulty;
    private float noteWeightSum;

    public void ProcessAll(
        IReadOnlyList<ReadingObject> objects,
        ReadingParameters parameters)
    {
        objectDifficulties.Clear();
        objectTimes.Clear();
        currentDifficulty = 0f;

        for (int i = 0; i < objects.Count; i++)
        {
            if (i > 0)
                currentDifficulty *= StrainDecay(objects[i].Delta);

            float readingValue = ReadingEvaluator.EvaluateDifficultyOf(objects, i, parameters);
            currentDifficulty += readingValue * SKILL_MULTIPLIER;
            objectDifficulties.Add(currentDifficulty);
            objectTimes.Add(objects[i].Time);
        }
    }

    public float DifficultyValue(float firstObjectTime, float clockRate)
        => CalculateDifficulty(false, firstObjectTime, clockRate);

    public float PrefixStableDifficultyValue(float firstObjectTime, float clockRate)
        => CalculateDifficulty(true, firstObjectTime, clockRate);

    private float CalculateDifficulty(bool prefixStable, float firstObjectTime, float clockRate)
    {
        var difficulties = new List<float>(objectDifficulties.Count);
        var difficultyTimes = new List<float>(objectDifficulties.Count);

        for (int i = 0; i < objectDifficulties.Count; i++)
        {
            if (objectDifficulties[i] <= 0f)
                continue;

            difficulties.Add(objectDifficulties[i]);
            difficultyTimes.Add(objectTimes[i]);
        }

        if (difficulties.Count == 0)
            return 0f;

        if (prefixStable)
            ApplyPrefixStableDifficultyTransformation(difficulties, difficultyTimes, firstObjectTime, clockRate);
        else
            ApplyLegacyDifficultyTransformation(difficulties);

        difficulties.Sort((a, b) => b.CompareTo(a));

        float difficulty = 0f;
        noteWeightSum = 0f;

        for (int index = 0; index < difficulties.Count; index++)
        {
            float idx = index;
            float harmonicTerm = HARMONIC_SCALE / (1f + idx);
            float weight = (1f + harmonicTerm)
                / (MathF.Pow(idx, DECAY_EXPONENT) + 1f + harmonicTerm);

            noteWeightSum += weight;
            difficulty += difficulties[index] * weight;
        }

        return difficulty;
    }

    public float CountTopWeightedObjectDifficulties(float difficultyValue)
    {
        if (objectDifficulties.Count == 0 || noteWeightSum == 0f)
            return 0f;

        float consistentTop = difficultyValue / noteWeightSum;
        if (consistentTop == 0f)
            return 0f;

        float sum = 0f;
        foreach (float difficulty in objectDifficulties)
            sum += Logistic(difficulty / consistentTop, 1.15f, 5f, 1.1f);

        return sum;
    }

    private void ApplyLegacyDifficultyTransformation(List<float> difficulties)
    {
        int reducedNoteCount = objectDifficulties.Count;
        int count = Math.Min(difficulties.Count, reducedNoteCount);
        for (int i = 0; i < count; i++)
            ApplyDifficultyScale(difficulties, i, (float)i / reducedNoteCount);
    }

    private static void ApplyPrefixStableDifficultyTransformation(
        List<float> difficulties,
        IReadOnlyList<float> difficultyTimes,
        float firstObjectTime,
        float clockRate)
    {
        for (int i = 0; i < difficulties.Count; i++)
            ApplyDifficultyScale(difficulties, i, PrefixStableOpeningProgress(difficultyTimes[i], firstObjectTime, clockRate));
    }

    internal static float PrefixStableOpeningProgress(float objectTime, float firstObjectTime, float clockRate)
        => RxMath.Clamp((objectTime - firstObjectTime) / (REDUCED_DIFFICULTY_DURATION * clockRate), 0f, 1f);

    private static void ApplyDifficultyScale(List<float> difficulties, int index, float t)
    {
        float lerped = 1f + 9f * t;
        float scale = MathF.Log10(lerped);
        difficulties[index] *= REDUCED_DIFFICULTY_BASE_LINE
                               + (1f - REDUCED_DIFFICULTY_BASE_LINE) * scale;
    }

    private static float Logistic(float x, float midpointOffset, float multiplier, float maxValue)
        => maxValue / (1f + MathF.Exp(multiplier * (midpointOffset - x)));

    private static float StrainDecay(float milliseconds)
        => MathF.Pow(STRAIN_DECAY_BASE, milliseconds / 1000f);
}
