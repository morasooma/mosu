using System;
using System.Collections.Generic;

namespace MosuRxPureCs;

[Flags]
public enum RxMods : uint
{
    None = 0,
    NoFail = 1 << 0,
    Easy = 1 << 1,
    TouchDevice = 1 << 2,
    Hidden = 1 << 3,
    HardRock = 1 << 4,
    SuddenDeath = 1 << 5,
    DoubleTime = 1 << 6,
    Relax = 1 << 7,
    HalfTime = 1 << 8,
    Nightcore = 1 << 9,
    Flashlight = 1 << 10,
    SpunOut = 1 << 12,
}

public enum RxHitObjectKind
{
    Circle,
    Slider,
    Spinner,
}

public enum RxPathType
{
    Linear,
    Bezier,
    Catmull,
    PerfectCurve,
}

public sealed class RxPathControlPoint
{
    public RxVec2 Position { get; set; }
    public RxPathType? Kind { get; set; }

    public RxPathControlPoint(RxVec2 position, RxPathType? kind = null)
    {
        Position = position;
        Kind = kind;
    }
}

public sealed class RxSliderData
{
    public double? ExpectedDistance { get; set; }
    public int Repeats { get; set; }
    public List<RxPathControlPoint> ControlPoints { get; } = new();
}

public sealed class RxHitObject
{
    public double StartTime { get; set; }
    public RxVec2 Position { get; set; }
    public RxHitObjectKind Kind { get; set; }
    public RxSliderData? Slider { get; set; }
}

public readonly record struct RxTimingPoint(double Time, double BeatLength);
public readonly record struct RxDifficultyPoint(double Time, double SliderVelocity, bool GenerateTicks);

public sealed class RxBeatmap
{
    public int Version { get; set; } = 14;
    public float ApproachRate { get; set; } = 5f;
    public float CircleSize { get; set; } = 5f;
    public float HpDrainRate { get; set; } = 5f;
    public float OverallDifficulty { get; set; } = 5f;
    public double SliderMultiplier { get; set; } = 1.4;
    public double SliderTickRate { get; set; } = 1.0;

    public List<RxTimingPoint> TimingPoints { get; } = new();
    public List<RxDifficultyPoint> DifficultyPoints { get; } = new();
    public List<RxHitObject> HitObjects { get; } = new();
}

public sealed class RxScoreState
{
    public uint Count300 { get; set; }
    public uint Count100 { get; set; }
    public uint Count50 { get; set; }
    public uint Misses { get; set; }
    public uint? MaxCombo { get; set; }
    public uint? PassedObjects { get; set; }
}

public sealed class RxCalculationRequest
{
    public required RxBeatmap Beatmap { get; init; }
    public required RxScoreState Score { get; init; }
    public RxMods Mods { get; init; } = RxMods.Relax;

    // The Mosu bridge fed Rust a playable beatmap whose difficulty settings
    // were already modified. These overrides reproduce that cleanly in C#.
    // If null, standard EZ/HR rules are applied to the .osu base values.
    public float? EffectiveAr { get; init; }
    public float? EffectiveOd { get; init; }
    public float? EffectiveCs { get; init; }
    public float? EffectiveHp { get; init; }

    // Explicit rate wins. Otherwise DT/NC=1.5, HT=0.75, else 1.0.
    public double? ClockRate { get; init; }
}

public sealed class RxDifficultyAttributes
{
    public double AimStrain { get; set; }
    public double AimStrainBeforeHighCsCompression { get; set; }
    public double SpeedStrain { get; set; }
    public double ReadingStrain { get; set; }
    public double ReadingStrainForPerformance { get; set; }
    public double ReadingStrainAtAr9ForPerformance { get; set; }
    public double CanonicalStreamWeight { get; set; }
    public double Ar { get; set; }
    public double Od { get; set; }
    public double Hp { get; set; }
    public double Cs { get; set; }
    public int CircleCount { get; set; }
    public int SliderCount { get; set; }
    public int SpinnerCount { get; set; }
    public double Stars { get; set; }
    public int MaxCombo { get; set; }
    public float AimDifficultStrainCount { get; set; }
    public float SpeedDifficultStrainCount { get; set; }
    public float ReadingDifficultNoteCount { get; set; }
    public double JumpSpikeFillerWeight { get; set; }
    public double SpeedSpikeFillerWeight { get; set; }
    public double CustomFlowAimBonusRatio { get; set; }
    public double WideFlowPatternWeight { get; set; }
    public uint FlowSectionCount { get; set; }
}

public sealed class RxNativePerformanceResult
{
    public required RxDifficultyAttributes Difficulty { get; init; }
    public double Pp { get; set; }
    public double PpAim { get; set; }
    public double PpSpeed { get; set; }
    public double PpAccuracy { get; set; }
    public double PpReading { get; set; }
    public double EffectiveMissCount { get; init; }
}
