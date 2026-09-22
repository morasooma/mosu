// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// Creates independent copies of Dodge objects for gameplay conversion, editor repeat and prefabs.
    /// Keep all author-controlled properties here so new object features cannot silently disappear
    /// when a pattern is duplicated or a gameplay beatmap is created.
    /// </summary>
    public static class DodgeHitObjectCloner
    {
        public static DodgeHitObject Clone(DodgeHitObject source, double timeOffset = 0, Vector2 positionOffset = default)
        {
            DodgeHitObject clone = source switch
            {
                DodgeBullet bullet => new DodgeBullet
                {
                    Duration = bullet.Duration,
                    Position = bullet.Position + positionOffset,
                    EndPosition = bullet.EndPosition + positionOffset,
                    Shape = bullet.Shape,
                    ContinueUntilExit = bullet.ContinueUntilExit,
                },
                DodgeEmitter emitter => new DodgeEmitter
                {
                    Duration = emitter.Duration,
                    Position = emitter.Position + positionOffset,
                    AimPosition = emitter.AimPosition + positionOffset,
                    MovementEndPosition = emitter.MovementEndPosition + positionOffset,
                    MoveSource = emitter.MoveSource,
                    BulletCount = emitter.BulletCount,
                    SpreadAngle = emitter.SpreadAngle,
                    BurstCount = emitter.BurstCount,
                    BurstInterval = emitter.BurstInterval,
                    BurstBeatDivisor = emitter.BurstBeatDivisor,
                    BurstRotation = emitter.BurstRotation,
                    Shape = emitter.Shape,
                    ContinueUntilExit = emitter.ContinueUntilExit,
                },
                DodgeBeam beam => new DodgeBeam
                {
                    Duration = beam.Duration,
                    Position = beam.Position + positionOffset,
                    EndPosition = beam.EndPosition + positionOffset,
                    BeamWidth = beam.BeamWidth,
                },
                DodgeArenaChange arena => new DodgeArenaChange
                {
                    Duration = arena.Duration,
                    TargetPosition = arena.TargetPosition + positionOffset,
                    TargetSize = arena.TargetSize,
                    TargetRotation = arena.TargetRotation,
                    KiaiShakeAngle = arena.KiaiShakeAngle,
                    BorderOpacity = arena.BorderOpacity,
                    Easing = arena.Easing,
                },
                DodgeCameraChange camera => new DodgeCameraChange
                {
                    Duration = camera.Duration,
                    Position = camera.Position + positionOffset,
                    EndPosition = camera.EndPosition + positionOffset,
                    Continuous = camera.Continuous,
                    Easing = camera.Easing,
                },
                DodgeTrigger trigger => new DodgeTrigger
                {
                    Duration = trigger.Duration,
                    Action = trigger.Action,
                    Strength = trigger.Strength,
                    Position = trigger.Position + positionOffset,
                },
                _ => throw new ArgumentException($"Unsupported Dodge object {source.GetType().Name}.", nameof(source)),
            };

            clone.StartTime = source.StartTime + timeOffset;
            clone.Colour = source.Colour;
            clone.OutlineColour = source.OutlineColour;
            clone.Opacity = source.Opacity;
            clone.OutlineThickness = source.OutlineThickness;
            clone.MovementType = source.MovementType;
            clone.MovementEasing = source.MovementEasing;
            clone.WaveAmplitude = source.WaveAmplitude;
            clone.WaveCycles = source.WaveCycles;
            clone.WavePhase = source.WavePhase;
            clone.TrajectoryGuideStyle = source.TrajectoryGuideStyle;
            clone.Samples = source.Samples.ToList();
            return clone;
        }
    }
}
