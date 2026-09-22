using System;
using System.Collections.Generic;
using System.Linq;

namespace MosuRxPureCs;

internal sealed class PreparedOsuObject
{
    public float Time { get; init; }
    public RxVec2 Position { get; init; }
    public RxVec2 EndPosition { get; init; }
    public float? TravelDistance { get; init; }
    public bool IsSpinner => !TravelDistance.HasValue;
}

internal sealed class DifficultyObject
{
    public required PreparedOsuObject Base { get; init; }
    public (float JumpDistance, float StrainTime)? PreviousValues { get; init; }
    public float JumpDistance { get; init; }
    public float TravelDistance { get; init; }
    public float? Angle { get; init; }
    public float? NormalisedVectorAngle { get; init; }
    public float Delta { get; init; }
    public float StrainTime { get; init; }

    public static DifficultyObject Create(
        PreparedOsuObject current,
        PreparedOsuObject previous,
        (float JumpDistance, float StrainTime)? previousValues,
        PreparedOsuObject? previousPrevious,
        float clockRate,
        float scalingFactor)
    {
        float delta = (current.Time - previous.Time) / clockRate;
        float strainTime = MathF.Max(delta, 50f);
        float travelDistance = previous.TravelDistance ?? 0f;
        RxVec2 previousCursorPosition = previous.EndPosition;

        float jumpDistance = current.IsSpinner
            ? 0f
            : ((current.Position - previousCursorPosition) * scalingFactor).Length;

        float? normalisedVectorAngle = null;
        if (!current.IsSpinner && previousPrevious is not null)
        {
            RxVec2 v = current.Position - previousCursorPosition;
            normalisedVectorAngle = MathF.Atan2(MathF.Abs(v.Y), MathF.Abs(v.X));
        }

        float? angle = null;
        if (previousPrevious is not null)
        {
            RxVec2 previousPreviousCursorPosition = previousPrevious.EndPosition;
            RxVec2 v1 = previousPreviousCursorPosition - previous.Position;
            RxVec2 v2 = current.Position - previousCursorPosition;
            float dot = v1.Dot(v2);
            float det = v1.X * v2.Y - v1.Y * v2.X;
            angle = MathF.Abs(MathF.Atan2(det, dot));
        }

        return new DifficultyObject
        {
            Base = current,
            PreviousValues = previousValues,
            JumpDistance = jumpDistance,
            TravelDistance = travelDistance,
            Angle = angle,
            NormalisedVectorAngle = normalisedVectorAngle,
            Delta = delta,
            StrainTime = strainTime,
        };
    }
}

internal enum SkillKind
{
    Aim,
    Speed,
}

internal readonly record struct RecentObject(
    float? NormalisedVectorAngle,
    float StrainTime,
    float JumpDistance,
    float? Angle);

internal sealed class Skill
{
    private const float SPEED_SKILL_MULTIPLIER = 1400f;
    private const float SPEED_STRAIN_DECAY_BASE = 0.3f;
    private const float AIM_SKILL_MULTIPLIER = 26.25f;
    private const float AIM_STRAIN_DECAY_BASE = 0.15f;
    private const float DECAY_WEIGHT = 0.9f;
    private const int MAX_RECENT_OBJECTS = 8;

    private float currentStrain = 1f;
    private float currentSectionPeak = 1f;
    private float? previousTime;
    private float? cachedDifficultyValue;
    private readonly SkillKind kind;
    private readonly List<RecentObject> recentObjects = new(MAX_RECENT_OBJECTS);

    public List<float> StrainPeaks { get; } = new(128);
    public List<float> ObjectStrains { get; } = new();

    public Skill(SkillKind kind) => this.kind = kind;

    public void SaveCurrentPeak() => StrainPeaks.Add(currentSectionPeak);

    public void StartNewSectionFrom(float time)
    {
        if (!previousTime.HasValue)
            return;
        currentSectionPeak = PeakStrain(time - previousTime.Value);
    }

    public void Process(DifficultyObject current)
    {
        currentStrain *= StrainDecay(current.Delta);
        currentStrain += StrainValueOf(current, recentObjects) * SkillMultiplier();

        ObjectStrains.Add(currentStrain);
        currentSectionPeak = MathF.Max(currentSectionPeak, currentStrain);
        previousTime = current.Base.Time;

        var recent = new RecentObject(
            current.NormalisedVectorAngle,
            current.StrainTime,
            current.JumpDistance,
            current.Angle);

        if (recentObjects.Count >= MAX_RECENT_OBJECTS)
            recentObjects.RemoveAt(recentObjects.Count - 1);
        recentObjects.Insert(0, recent);
    }

    public float DifficultyValue()
    {
        if (StrainPeaks.Count == 0)
            return 0f;

        int sectionCount = StrainPeaks.Count;
        int earlySectionCount = (int)MathF.Floor(sectionCount * 0.85f);

        if (sectionCount > 5 && earlySectionCount > 0)
        {
            float earlyMax = 0f;
            for (int i = 0; i < earlySectionCount; i++)
                earlyMax = MathF.Max(earlyMax, StrainPeaks[i]);

            float spikeThreshold = MathF.Max(earlyMax * 1.35f, 1f);

            for (int i = earlySectionCount; i < sectionCount; i++)
            {
                float peak = StrainPeaks[i];
                if (peak > spikeThreshold)
                    StrainPeaks[i] = spikeThreshold + (peak - spikeThreshold) * 0.62f;
            }
        }

        StrainPeaks.Sort((a, b) => b.CompareTo(a));

        float difficulty = 0f;
        float weight = 1f;
        foreach (float strain in StrainPeaks)
        {
            difficulty += strain * weight;
            weight *= DECAY_WEIGHT;
        }

        cachedDifficultyValue = difficulty;
        return difficulty;
    }

    public float CountDifficultStrains()
    {
        float difficultyValue = cachedDifficultyValue ?? DifficultyValue();
        float singleStrain = difficultyValue / 10f;
        float sum = 0f;

        foreach (float strain in ObjectStrains)
            sum += 1.1f / (1f + MathF.Exp(-10f * (strain / singleStrain - 0.88f)));

        return sum;
    }

    private float SkillMultiplier() => kind == SkillKind.Aim ? AIM_SKILL_MULTIPLIER : SPEED_SKILL_MULTIPLIER;
    private float StrainDecayBase() => kind == SkillKind.Aim ? AIM_STRAIN_DECAY_BASE : SPEED_STRAIN_DECAY_BASE;
    private float PeakStrain(float deltaTime) => currentStrain * StrainDecay(deltaTime);
    private float StrainDecay(float milliseconds) => MathF.Pow(StrainDecayBase(), milliseconds / 1000f);

    private float StrainValueOf(DifficultyObject current, IReadOnlyList<RecentObject> recent)
        => kind == SkillKind.Aim ? AimStrainValue(current, recent) : SpeedStrainValue(current);

    private static float AimStrainValue(DifficultyObject current, IReadOnlyList<RecentObject> recent)
    {
        const float AIM_ANGLE_BONUS_BEGIN = MathF.PI / 3f;
        const float TIMING_THRESHOLD = 107f;

        if (current.Base.IsSpinner)
            return 0f;

        float result = 0f;

        if (current.PreviousValues is { } previous
            && current.Angle is { } angle
            && angle > AIM_ANGLE_BONUS_BEGIN)
        {
            const float scale = 90f;
            float angleBonus = MathF.Sqrt(
                MathF.Pow(MathF.Sin(angle - AIM_ANGLE_BONUS_BEGIN), 2f)
                * MathF.Max(previous.JumpDistance - scale, 0f)
                * MathF.Max(current.JumpDistance - scale, 0f));

            result = 1.5f * ApplyDiminishingExp(MathF.Max(angleBonus, 0f))
                / MathF.Max(TIMING_THRESHOLD, previous.StrainTime);
        }

        float jumpDistanceExp = ApplyDiminishingExp(current.JumpDistance);
        float travelDistanceExp = ApplyDiminishingExp(current.TravelDistance);
        float distanceExp = jumpDistanceExp + travelDistanceExp + MathF.Sqrt(travelDistanceExp * jumpDistanceExp);

        float aimStrain = MathF.Max(
            result + distanceExp / MathF.Max(current.StrainTime, TIMING_THRESHOLD),
            distanceExp / current.StrainTime);

        aimStrain *= VectorAngleRepetition(current, recent);
        return aimStrain;
    }

    private static float SpeedStrainValue(DifficultyObject current)
    {
        const float SINGLE_SPACING_THRESHOLD = 125f;
        const float SPEED_ANGLE_BONUS_BEGIN = 5f * MathF.PI / 6f;
        const float PI_OVER_4 = MathF.PI / 4f;
        const float PI_OVER_2 = MathF.PI / 2f;
        const float MIN_SPEED_BONUS = 75f;
        const float MAX_SPEED_BONUS = 45f;
        const float SPEED_BALANCING_FACTOR = 40f;

        if (current.Base.IsSpinner)
            return 0f;

        float distance = MathF.Min(SINGLE_SPACING_THRESHOLD, current.TravelDistance + current.JumpDistance);
        float deltaTime = MathF.Max(MAX_SPEED_BONUS, current.Delta);
        float speedBonus = 1f;

        if (deltaTime < MIN_SPEED_BONUS)
        {
            float expBase = (MIN_SPEED_BONUS - deltaTime) / SPEED_BALANCING_FACTOR;
            speedBonus += expBase * expBase;
        }

        float angleBonus = 1f;
        if (current.Angle is { } angle && angle < SPEED_ANGLE_BONUS_BEGIN)
        {
            float expBase = MathF.Sin(1.5f * (SPEED_ANGLE_BONUS_BEGIN - angle));
            angleBonus = 1f + expBase * expBase / 3.57f;

            if (angle < PI_OVER_2)
            {
                angleBonus = 1.28f;

                if (distance < 90f && angle < PI_OVER_4)
                {
                    angleBonus += (1f - angleBonus) * MathF.Min((90f - distance) / 10f, 1f);
                }
                else if (distance < 90f)
                {
                    angleBonus += (1f - angleBonus)
                        * MathF.Min((90f - distance) / 10f, 1f)
                        * MathF.Sin((PI_OVER_2 - angle) / PI_OVER_4);
                }
            }
        }

        return (1f + (speedBonus - 1f) * 0.75f)
            * angleBonus
            * (0.95f + speedBonus * MathF.Pow(distance / SINGLE_SPACING_THRESHOLD, 3.5f))
            / current.StrainTime;
    }

    private static float VectorAngleRepetition(DifficultyObject current, IReadOnlyList<RecentObject> recent)
    {
        const float MAXIMUM_REPETITION_NERF = 0.08f;
        const float REPETITION_THRESHOLD = 1.3f;
        const float MAXIMUM_VECTOR_INFLUENCE = 1f;
        const float NORMALISED_DIAMETER = 104f;
        const int NOTE_LIMIT = 6;

        if (current.Angle is not { } currentAngle || current.NormalisedVectorAngle is not { } currentVectorAngle)
            return 1f;
        if (recent.Count == 0 || recent[0].Angle is null)
            return 1f;

        float lastAngle = recent[0].Angle!.Value;
        float constantAngleCount = 0f;
        int limit = Math.Min(NOTE_LIMIT, recent.Count);

        for (int i = 0; i < limit; i++)
        {
            RecentObject obj = recent[i];
            float maxDt = MathF.Max(current.StrainTime, obj.StrainTime);
            float minDt = MathF.Min(current.StrainTime, obj.StrainTime);
            if (maxDt > 1.1f * minDt)
                break;

            if (obj.NormalisedVectorAngle is { } loopVectorAngle)
            {
                float angleDifference = MathF.Abs(currentVectorAngle - loopVectorAngle);
                float maxAngle = 11.25f * MathF.PI / 180f;
                constantAngleCount += MathF.Cos(8f * MathF.Min(angleDifference, maxAngle));
            }
        }

        float vectorRepetition = MathF.Pow(
            MathF.Min(REPETITION_THRESHOLD / MathF.Max(constantAngleCount, 0f), 1f),
            2f);

        float stackFactor = RxMath.SmootherStep(current.JumpDistance, 0f, NORMALISED_DIAMETER);
        float angleDifferenceAdjusted = MathF.Cos(
            2f * MathF.Min(MathF.Abs(currentAngle - lastAngle) * stackFactor, 45f * MathF.PI / 180f));

        float acuteBonus = SmoothStepReverseAngle(lastAngle);
        float baseNerf = 1f - MAXIMUM_REPETITION_NERF * acuteBonus * angleDifferenceAdjusted;

        return MathF.Pow(
            baseNerf + (1f - baseNerf) * vectorRepetition * MAXIMUM_VECTOR_INFLUENCE * stackFactor,
            2f);
    }

    // Rust's smoothstep helper is used with start=140deg, end=40deg (reversed).
    // Preserve its exact clamp behaviour rather than "fixing" it.
    private static float SmoothStepReverseAngle(float angle)
    {
        float start = 140f * MathF.PI / 180f;
        float end = 40f * MathF.PI / 180f;
        float x = RxMath.Clamp((angle - start) / (end - start), 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private static float ApplyDiminishingExp(float value) => MathF.Pow(value, 0.99f);
}

internal static class RxDifficultyCalculator
{
    private const float OBJECT_RADIUS = 64f;
    private const float SECTION_LENGTH = 400f;
    private const float DIFFICULTY_MULTIPLIER = 0.0675f;
    private const float NORMALIZED_RADIUS = 52f;

    private const float FLOW_MAX_AIM_STRAIN_BONUS = 0.17f;
    private const float FLOW_MAX_STRAIN_TIME = 125f;
    private const float FLOW_MIN_SPACING = 42f;
    private const float FLOW_FULL_SPACING = 88f;
    private const float FLOW_JUMP_FADE_BEGIN = 150f;
    private const float FLOW_JUMP_FADE_END = 215f;
    private static readonly float FLOW_MIN_ANGLE = 80f * MathF.PI / 180f;
    private static readonly float FLOW_FULL_ANGLE = 132f * MathF.PI / 180f;
    private const float FLOW_TIMING_TOLERANCE = 0.22f;
    private const float FLOW_SPACING_RATIO_FULL = 1.10f;
    private const float FLOW_SPACING_RATIO_ZERO = 1.45f;

    private const float HYBRID_JUMP_SPACING_START = 175f;
    private const float HYBRID_JUMP_SPACING_FULL = 250f;
    private const float HYBRID_JUMP_FAST_TIME = 90f;
    private const float HYBRID_JUMP_SLOW_TIME = 150f;
    private const float HYBRID_JUMP_DENSITY_START = 0.02f;
    private const float HYBRID_JUMP_DENSITY_FULL = 0.10f;
    private const float HYBRID_JUMP_SHARE_START = 0.20f;
    private const float HYBRID_JUMP_SHARE_FULL = 0.60f;
    private const float HYBRID_MAX_FLOW_REDUCTION = 0.68f;

    private const float WIDE_FLOW_SPACING_START = 105f;
    private const float WIDE_FLOW_SPACING_FULL = 150f;
    private const float WIDE_FLOW_SHARE_START = 0.25f;
    private const float WIDE_FLOW_SHARE_FULL = 0.75f;
    private const float WIDE_FLOW_MAX_REDUCTION = 0.80f;
    private const int FLOW_MIN_RUN_NOTES = 6;
    private const int FLOW_FULL_RUN_NOTES = 8;
    private const float FLOW_FULL_MAP_DENSITY = 0.035f;

    private const float HIGH_CS_STREAM_COUNT_RATIO_START = 1.75f;
    private const float HIGH_CS_STREAM_COUNT_RATIO_FULL = 2.50f;
    private const float HIGH_CS_STREAM_STRAIN_RATIO_START = 1.45f;
    private const float HIGH_CS_STREAM_STRAIN_RATIO_END = 1.75f;
    private const float HIGH_CS_STREAM_DELTA_RETENTION = 0.40f;

    private const float PREEMPT_MIN = 450f;

    public static RxDifficultyAttributes Calculate(
        RxBeatmap map,
        RxMods mods,
        float ar,
        float od,
        float cs,
        float hp,
        double clockRate,
        uint? passedObjects)
        => calculate(map, mods, ar, od, cs, hp, clockRate, passedObjects, true);

    private static RxDifficultyAttributes calculate(
        RxBeatmap map,
        RxMods mods,
        float ar,
        float od,
        float cs,
        float hp,
        double clockRate,
        uint? passedObjects,
        bool applyHighCsStreamCompression)
    {
        var attributes = new RxDifficultyAttributes
        {
            Ar = ar,
            Od = od,
            Cs = cs,
            Hp = hp,
        };

        int take = (int)Math.Min(passedObjects ?? (uint)map.HitObjects.Count, (uint)map.HitObjects.Count);
        if (take < 2)
            return attributes;

        float clockRateF = (float)clockRate;
        float sectionLength = SECTION_LENGTH * clockRateF;
        float radius = OBJECT_RADIUS * (1f - 0.7f * (cs - 5f) / 5f) / 2f;
        float scalingFactor = NORMALIZED_RADIUS / radius;

        if (radius < 30f)
        {
            float smallCircleBonus = MathF.Min(30f - radius, 5f) / 50f;
            scalingFactor *= 1f + smallCircleBonus;
        }

        var prepared = new List<PreparedOsuObject>(take);
        for (int i = 0; i < take; i++)
            prepared.Add(PrepareObject(map.HitObjects[i], map, radius, scalingFactor, attributes));

        var aim = new Skill(SkillKind.Aim);
        var speed = new Skill(SkillKind.Speed);

        int flowRunNotes = 0;
        float flowWeightSum = 0f;
        float extremeJumpWeightSum = 0f;
        float wideFlowWeightSum = 0f;
        var readingObjects = new List<ReadingObject>(take);

        float currentSectionEnd = MathF.Ceiling((float)map.HitObjects[0].StartTime / sectionLength) * sectionLength;

        PreparedOsuObject? previousPrevious = null;
        PreparedOsuObject previous = prepared[0];
        (float JumpDistance, float StrainTime)? previousValues = null;

       readingObjects.Add(new ReadingObject(previous.Time, 0f, 0f, 0f, null, null));

       PreparedOsuObject current = prepared[1];
        DifficultyObject second = DifficultyObject.Create(
            current,
            previous,
            previousValues,
            previousPrevious,
            clockRateF,
            scalingFactor);

        while (second.Base.Time > currentSectionEnd)
            currentSectionEnd += sectionLength;

        aim.Process(second);
        speed.Process(second);
        readingObjects.Add(ToReadingObject(current, second));

        previousPrevious = previous;
        previousValues = (second.JumpDistance, second.StrainTime);
        previous = current;

        for (int index = 2; index < prepared.Count; index++)
        {
            current = prepared[index];
            DifficultyObject h = DifficultyObject.Create(
                current,
                previous,
                previousValues,
                previousPrevious,
                clockRateF,
                scalingFactor);

            while (h.Base.Time > currentSectionEnd)
            {
                aim.SaveCurrentPeak();
                aim.StartNewSectionFrom(currentSectionEnd);
                speed.SaveCurrentPeak();
                speed.StartNewSectionFrom(currentSectionEnd);
                currentSectionEnd += sectionLength;
            }

            aim.Process(h);
            speed.Process(h);

            float timingWeight = 0f;
            float spacingConsistencyWeight = 0f;

            if (previousValues is { } prevValues)
            {
                float maxTime = MathF.Max(h.StrainTime, prevValues.StrainTime);
                float minTime = MathF.Max(MathF.Min(h.StrainTime, prevValues.StrainTime), 1f);
                float timingRatio = maxTime / minTime;
                timingWeight = 1f - FlowSmoothStep(timingRatio, 1.05f, 1f + FLOW_TIMING_TOLERANCE);

                float maxSpacing = MathF.Max(h.JumpDistance, prevValues.JumpDistance);
                float minSpacing = MathF.Max(MathF.Min(h.JumpDistance, prevValues.JumpDistance), 1f);
                float spacingRatio = maxSpacing / minSpacing;
                spacingConsistencyWeight = 1f - FlowSmoothStep(
                    spacingRatio,
                    FLOW_SPACING_RATIO_FULL,
                    FLOW_SPACING_RATIO_ZERO);
            }

            float speedWeight = 1f - FlowSmoothStep(h.StrainTime, 92f, FLOW_MAX_STRAIN_TIME);
            float spacingWeight = FlowSmoothStep(h.JumpDistance, FLOW_MIN_SPACING, FLOW_FULL_SPACING);
            float jumpGuard = 1f - FlowSmoothStep(h.JumpDistance, FLOW_JUMP_FADE_BEGIN, FLOW_JUMP_FADE_END);
            float angleWeight = h.Angle.HasValue
                ? FlowSmoothStep(h.Angle.Value, FLOW_MIN_ANGLE, FLOW_FULL_ANGLE)
                : 0f;

            float extremeJumpSpacingWeight = FlowSmoothStep(
                h.JumpDistance,
                HYBRID_JUMP_SPACING_START,
                HYBRID_JUMP_SPACING_FULL);

            float extremeJumpSpeedWeight = 1f - FlowSmoothStep(
                h.StrainTime,
                HYBRID_JUMP_FAST_TIME,
                HYBRID_JUMP_SLOW_TIME);

            extremeJumpWeightSum += extremeJumpSpacingWeight * extremeJumpSpeedWeight;

            float noteFlowWeight = timingWeight
                * spacingConsistencyWeight
                * MathF.Max(speedWeight, 0.25f)
                * spacingWeight
                * jumpGuard
                * angleWeight;

            if (h.StrainTime <= FLOW_MAX_STRAIN_TIME
                && h.JumpDistance >= FLOW_MIN_SPACING
                && noteFlowWeight >= 0.14f)
            {
                flowRunNotes++;

                if (flowRunNotes >= FLOW_MIN_RUN_NOTES)
                {
                    float runWeight = FlowSmoothStep(
                        flowRunNotes,
                        FLOW_MIN_RUN_NOTES,
                        FLOW_FULL_RUN_NOTES);

                    float contribution = noteFlowWeight * runWeight;
                    flowWeightSum += contribution;

                    float wideSpacingWeight = FlowSmoothStep(
                        h.JumpDistance,
                        WIDE_FLOW_SPACING_START,
                        WIDE_FLOW_SPACING_FULL);

                    wideFlowWeightSum += contribution * wideSpacingWeight;
                }
            }
            else
            {
                flowRunNotes = 0;
            }

            readingObjects.Add(ToReadingObject(current, h));
            previousPrevious = previous;
            previousValues = (h.JumpDistance, h.StrainTime);
            previous = current;
        }

        aim.SaveCurrentPeak();
        speed.SaveCurrentPeak();

        float jumpSpikeFillerWeight = SectionSpikeFillerWeight(aim.StrainPeaks);
        float speedSpikeFillerWeight = SectionSpikeFillerWeight(speed.StrainPeaks);
        float baseAimStrain = MathF.Sqrt(aim.DifficultyValue()) * DIFFICULTY_MULTIPLIER;

        float flowDensity = flowWeightSum / Math.Max(take, 1);
        float baseMapFlowWeight = FlowSmoothStep(flowDensity, 0f, FLOW_FULL_MAP_DENSITY);

        float extremeJumpDensity = extremeJumpWeightSum / Math.Max(take, 1);
        float jumpPresence = FlowSmoothStep(
            extremeJumpDensity,
            HYBRID_JUMP_DENSITY_START,
            HYBRID_JUMP_DENSITY_FULL);

        float jumpShare = extremeJumpWeightSum / (extremeJumpWeightSum + flowWeightSum + 0.0001f);
        float jumpCompetition = FlowSmoothStep(
            jumpShare,
            HYBRID_JUMP_SHARE_START,
            HYBRID_JUMP_SHARE_FULL);

        float hybridContamination = jumpPresence * jumpCompetition;
        float hybridGuard = 1f - HYBRID_MAX_FLOW_REDUCTION * hybridContamination;

        float wideFlowShare = wideFlowWeightSum / (flowWeightSum + 0.0001f);
        float wideFlowPressure = FlowSmoothStep(
            wideFlowShare,
            WIDE_FLOW_SHARE_START,
            WIDE_FLOW_SHARE_FULL);

        float wideFlowGuard = 1f - WIDE_FLOW_MAX_REDUCTION * wideFlowPressure;
        float wideFlowPatternWeight = RxMath.Clamp(baseMapFlowWeight * wideFlowPressure, 0f, 1f);
        uint flowSectionCount = (uint)aim.StrainPeaks.Count;

        float effectiveMapFlowWeight = baseMapFlowWeight * hybridGuard * wideFlowGuard;
        float aimStrain = baseAimStrain * (1f + FLOW_MAX_AIM_STRAIN_BONUS * effectiveMapFlowWeight);
        float customFlowAimBonusRatio = baseAimStrain > 0.0001f
            ? MathF.Max(aimStrain / baseAimStrain - 1f, 0f)
            : 0f;

        float speedStrain = MathF.Sqrt(speed.DifficultyValue()) * DIFFICULTY_MULTIPLIER;
        float aimDifficultStrainCount = aim.CountDifficultStrains();
        float speedDifficultStrainCount = speed.CountDifficultStrains();

        float timePreemptRaw = ArToPreempt(ar);
        float preempt = timePreemptRaw / clockRateF;
        float timeFadeInRaw = 400f * MathF.Min(timePreemptRaw / PREEMPT_MIN, 1f);
        float hdFadeInRaw = timePreemptRaw * 0.4f;

        var readingParameters = new ReadingParameters(
            timePreemptRaw,
            preempt,
            timeFadeInRaw,
            hdFadeInRaw,
            clockRateF,
            mods.HasFlag(RxMods.Hidden));

        var readingSkill = new ReadingSkill();
        readingSkill.ProcessAll(readingObjects, readingParameters);
        float firstObjectTime = readingObjects.Count > 0 ? readingObjects[0].Time : 0f;
        float prefixStableReadingDifficultyValue = readingSkill.PrefixStableDifficultyValue(firstObjectTime, clockRateF);
        float readingDifficultyValue = readingSkill.DifficultyValue(firstObjectTime, clockRateF);
        float readingStrain = MathF.Sqrt(prefixStableReadingDifficultyValue) * DIFFICULTY_MULTIPLIER;
        float readingStrainForPerformance = MathF.Sqrt(readingDifficultyValue) * DIFFICULTY_MULTIPLIER;
        float readingStrainAtAr9ForPerformance = readingStrainForPerformance;

        if (ar < 9f)
        {
            float ar9TimePreemptRaw = ArToPreempt(9f);
            var ar9ReadingParameters = new ReadingParameters(
                ar9TimePreemptRaw,
                ar9TimePreemptRaw / clockRateF,
                400f * MathF.Min(ar9TimePreemptRaw / PREEMPT_MIN, 1f),
                ar9TimePreemptRaw * 0.4f,
                clockRateF,
                mods.HasFlag(RxMods.Hidden));
            var ar9ReadingSkill = new ReadingSkill();
            ar9ReadingSkill.ProcessAll(readingObjects, ar9ReadingParameters);
            readingStrainAtAr9ForPerformance = MathF.Sqrt(ar9ReadingSkill.DifficultyValue(firstObjectTime, clockRateF))
                                               * DIFFICULTY_MULTIPLIER;
        }
        float readingDifficultNoteCount = readingSkill.CountTopWeightedObjectDifficulties(readingDifficultyValue);

        float starAimStrain = baseAimStrain;
        float aimStrainBeforeHighCsCompression = aimStrain;
        RxDifficultyAttributes? canonical = null;
        float streamWeight = 0f;

        if (applyHighCsStreamCompression && cs > 3f)
        {
            canonical = calculate(map, mods, ar, od, 5f, hp, clockRate, passedObjects, false);
            streamWeight = HighCsStreamWeight(canonical);
        }

        if (canonical is not null && cs > 5f)
        {
            if (streamWeight > 0f)
            {
                float deltaRetention = 1f + (HIGH_CS_STREAM_DELTA_RETENTION - 1f) * streamWeight;
                float canonicalBaseAimStrain = (float)(canonical.AimStrain / (1d + canonical.CustomFlowAimBonusRatio));

                aimStrain = CompressPositiveStrainDelta((float)canonical.AimStrain, aimStrain, deltaRetention);
                starAimStrain = CompressPositiveStrainDelta(canonicalBaseAimStrain, starAimStrain, deltaRetention);
                readingStrain = CompressPositiveStrainDelta((float)canonical.ReadingStrain, readingStrain, deltaRetention);
                readingStrainForPerformance = CompressPositiveStrainDelta(
                    (float)canonical.ReadingStrainForPerformance,
                    readingStrainForPerformance,
                    deltaRetention);
                readingStrainAtAr9ForPerformance = CompressPositiveStrainDelta(
                    (float)canonical.ReadingStrainAtAr9ForPerformance,
                    readingStrainAtAr9ForPerformance,
                    deltaRetention);
            }
        }

        float sum = starAimStrain + speedStrain + readingStrain;
        float maxComponent = MathF.Max(starAimStrain, MathF.Max(speedStrain, readingStrain));
        float stars = sum + MathF.Abs(maxComponent - sum / 3f) / 2f;

        attributes.Stars = stars;
        attributes.AimStrain = aimStrain;
        attributes.AimStrainBeforeHighCsCompression = aimStrainBeforeHighCsCompression;
        attributes.SpeedStrain = speedStrain;
        attributes.ReadingStrain = readingStrain;
        attributes.ReadingStrainForPerformance = readingStrainForPerformance;
        attributes.ReadingStrainAtAr9ForPerformance = readingStrainAtAr9ForPerformance;
        attributes.AimDifficultStrainCount = aimDifficultStrainCount;
        attributes.SpeedDifficultStrainCount = speedDifficultStrainCount;
        attributes.ReadingDifficultNoteCount = readingDifficultNoteCount;
        attributes.JumpSpikeFillerWeight = jumpSpikeFillerWeight;
        attributes.SpeedSpikeFillerWeight = speedSpikeFillerWeight;
        attributes.CustomFlowAimBonusRatio = customFlowAimBonusRatio;
        attributes.WideFlowPatternWeight = wideFlowPatternWeight;
        attributes.FlowSectionCount = flowSectionCount;
        attributes.CanonicalStreamWeight = streamWeight;

        return attributes;
    }

    private static float HighCsStreamWeight(RxDifficultyAttributes canonical)
    {
        float difficultCountRatio = canonical.SpeedDifficultStrainCount
                                    / MathF.Max(canonical.AimDifficultStrainCount, 0.0001f);
        float countDominance = FlowSmoothStep(
            difficultCountRatio,
            HIGH_CS_STREAM_COUNT_RATIO_START,
            HIGH_CS_STREAM_COUNT_RATIO_FULL);

        float strainRatio = (float)(canonical.AimStrain / Math.Max(canonical.SpeedStrain, 0.0001));
        float strainBalance = 1f - FlowSmoothStep(
            strainRatio,
            HIGH_CS_STREAM_STRAIN_RATIO_START,
            HIGH_CS_STREAM_STRAIN_RATIO_END);

        return RxMath.Clamp(countDominance * strainBalance, 0f, 1f);
    }

    private static float CompressPositiveStrainDelta(float canonical, float effective, float deltaRetention)
        => canonical + MathF.Max(0f, effective - canonical) * deltaRetention;

    private static ReadingObject ToReadingObject(PreparedOsuObject current, DifficultyObject h)
        => new(
            current.Time,
            h.JumpDistance,
            h.StrainTime,
            h.Delta,
            h.Angle,
            h.NormalisedVectorAngle);

    private static PreparedOsuObject PrepareObject(
       RxHitObject h,
       RxBeatmap map,
       float radius,
        float scalingFactor,
        RxDifficultyAttributes attributes)
    {
        attributes.MaxCombo += 1;

        if (h.Kind == RxHitObjectKind.Circle)
        {
            attributes.CircleCount += 1;
            return new PreparedOsuObject
            {
                Time = (float)h.StartTime,
                Position = h.Position,
                EndPosition = h.Position,
                TravelDistance = 0f,
            };
        }

        if (h.Kind == RxHitObjectKind.Spinner || h.Slider is null)
        {
            attributes.SpinnerCount += 1;
            return new PreparedOsuObject
            {
                Time = (float)h.StartTime,
                Position = h.Position,
                EndPosition = h.Position,
                TravelDistance = null,
            };
        }

        attributes.SliderCount += 1;
        RxSliderData slider = h.Slider;

        double beatLength = TimingPointAt(map.TimingPoints, h.StartTime)?.BeatLength ?? 1000d;
        RxDifficultyPoint? point = DifficultyPointAt(map.DifficultyPoints, h.StartTime);
        double sliderVelocity = point?.SliderVelocity ?? 1d;
        bool generateTicks = point?.GenerateTicks ?? true;

        double scoringDistance = 100d * map.SliderMultiplier * sliderVelocity;
        double velocity = scoringDistance / beatLength;
        RxVec2 endPosition = h.Position;
        float travelDistance = 0f;
        float approximateFollowCircleRadius = radius * 3f;

        double tickDistanceMultiplier = map.Version < 8 ? 1d / sliderVelocity : 1d;
        double tickDistance = generateTicks
            ? scoringDistance / map.SliderTickRate * tickDistanceMultiplier
            : double.PositiveInfinity;

        double spanCount = slider.Repeats + 1d;
        var curve = new SliderCurve(slider.ControlPoints, slider.ExpectedDistance);
        double curveDistance = curve.Distance;

        if (velocity == 0d || spanCount <= 0d)
        {
            return new PreparedOsuObject
            {
                Time = (float)h.StartTime,
                Position = h.Position,
                EndPosition = h.Position,
                TravelDistance = 0f,
            };
        }

        double endTime = h.StartTime + spanCount * curveDistance / velocity;
        double totalDuration = endTime - h.StartTime;
        double spanDuration = totalDuration / spanCount;

        void ComputeVertex(double time)
        {
            attributes.MaxCombo += 1;
            double progress = spanDuration == 0d ? 0d : (time - h.StartTime) / spanDuration;

            if (progress % 2d >= 1d)
                progress = 1d - progress % 1d;
            else
                progress %= 1d;

            RxVec2 currentPosition = h.Position + curve.PositionAt(progress);
            RxVec2 difference = currentPosition - endPosition;
            float distance = difference.Length;

            if (distance > approximateFollowCircleRadius)
            {
                distance -= approximateFollowCircleRadius;
                endPosition += difference.Normalize() * distance;
                travelDistance += distance;
            }
        }

        double length = Math.Min(curveDistance, 100000d);
        tickDistance = Math.Clamp(tickDistance, 0d, length);
        double minDistanceFromEnd = velocity * 10d;
        double currentDistance = tickDistance;
        var ticks = new List<double>();

        if (tickDistance != 0d && !double.IsInfinity(tickDistance) && length > 0d)
        {
            while (currentDistance < length - minDistanceFromEnd)
            {
                double progress = currentDistance / length;
                double currentTime = h.StartTime + progress * spanDuration;
                ComputeVertex(currentTime);
                ticks.Add(currentTime);
                currentDistance += tickDistance;
            }

            for (int spanIndex = 1; spanIndex <= slider.Repeats; spanIndex++)
            {
                double spanIndexDouble = spanIndex;
                double currentTime = h.StartTime + spanDuration * spanIndexDouble;
                ComputeVertex(currentTime);
                double spanOffset = spanIndexDouble * spanDuration;

                if ((spanIndex & 1) == 1)
                {
                    double basis = h.StartTime + h.StartTime + spanDuration;
                    for (int i = ticks.Count - 1; i >= 0; i--)
                        ComputeVertex(spanOffset + basis - ticks[i]);
                }
                else
                {
                    foreach (double tick in ticks)
                        ComputeVertex(spanOffset + tick);
                }
            }
        }

        double finalSpanStartTime = h.StartTime + slider.Repeats * spanDuration;
        double finalSpanEndTime = Math.Max(
            h.StartTime + totalDuration / 2d,
            finalSpanStartTime + spanDuration - 36d);
        ComputeVertex(finalSpanEndTime);

        travelDistance *= scalingFactor;

        return new PreparedOsuObject
        {
            Time = (float)h.StartTime,
            Position = h.Position,
            EndPosition = endPosition,
            TravelDistance = travelDistance,
        };
    }

    private static RxTimingPoint? TimingPointAt(IReadOnlyList<RxTimingPoint> points, double time)
    {
        int index = UpperBound(points, time, static p => p.Time) - 1;
        return index >= 0 ? points[index] : null;
    }

    private static RxDifficultyPoint? DifficultyPointAt(IReadOnlyList<RxDifficultyPoint> points, double time)
    {
        int index = UpperBound(points, time, static p => p.Time) - 1;
        return index >= 0 ? points[index] : null;
    }

    private static int UpperBound<T>(IReadOnlyList<T> values, double time, Func<T, double> selector)
    {
        int lo = 0;
        int hi = values.Count;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (selector(values[mid]) <= time)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private static float FlowSmoothStep(float value, float start, float end)
    {
        if (end <= start)
            return value >= end ? 1f : 0f;
        float x = RxMath.Clamp((value - start) / (end - start), 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private static float SectionSpikeFillerWeight(IReadOnlyList<float> peaks)
    {
        int n = peaks.Count;
        if (n < 150)
            return 0f;

        float[] sorted = peaks.ToArray();
        Array.Sort(sorted);

        int topCount = Math.Min(n, 3);
        float peakReference = 0f;
        for (int i = n - topCount; i < n; i++)
            peakReference += sorted[i];
        peakReference /= topCount;

        if (peakReference <= 0.0001f)
            return 0f;

        int baselineIndex = (int)MathF.Round((n - 1) * 0.60f);
        float baseline = MathF.Max(sorted[Math.Min(baselineIndex, n - 1)], 0.0001f);
        float spikeRatio = peakReference / baseline;
        float spikeStrength = AntiFarmSmoothStep(spikeRatio, 1.35f, 1.90f);

        if (spikeStrength <= 0f)
            return 0f;

        float hardThreshold = peakReference * 0.72f;
        int hardSections = 0;
        foreach (float peak in peaks)
            if (peak >= hardThreshold)
                hardSections++;

        float hardCoverage = (float)hardSections / n;
        float sustained = AntiFarmSmoothStep(hardCoverage, 0.08f, 0.24f);
        float sparseWeight = 1f - sustained;
        float longWeight = AntiFarmSmoothStep(n, 150f, 300f);

        return RxMath.Clamp(spikeStrength * sparseWeight * longWeight, 0f, 1f);
   }

   private static float AntiFarmSmoothStep(float value, float start, float end)
    {
        if (end <= start)
            return value >= end ? 1f : 0f;
        float x = RxMath.Clamp((value - start) / (end - start), 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private static float ArToPreempt(float ar)
    {
        const float arPreemptMin = 1800f;
        const float arPreemptMid = 1200f;
        const float arPreemptMax = 450f;

        if (ar > 5f)
            return arPreemptMid + (arPreemptMax - arPreemptMid) * (ar - 5f) / 5f;
        if (ar < 5f)
            return arPreemptMid - (arPreemptMid - arPreemptMin) * (5f - ar) / 5f;
        return arPreemptMid;
    }
}
