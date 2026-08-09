// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Dodge.Beatmaps
{
    public class DodgeBeatmapConverter : BeatmapConverter<DodgeHitObject>
    {
        public DodgeBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
        }

        public override bool CanConvert() => Beatmap.HitObjects.All(hitObject => hitObject is DodgeHitObject);

        protected override Beatmap<DodgeHitObject> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
        {
            Beatmap<DodgeHitObject> converted = base.ConvertBeatmap(original, cancellationToken);

            // BeatmapConverter performs a shallow beatmap clone and passes through objects which
            // already match the target type. A fresh Dodge object is required for every gameplay
            // load, otherwise Retry applies defaults to an object still observed by the previous
            // player's drawable from a background loading thread.
            converted.HitObjects = converted.HitObjects.Select(CloneHitObject).ToList();
            DodgeBeatmapSettings.SetPlayerSize(converted.Difficulty, DodgeBeatmapSettings.GetPlayerSize(Beatmap.Difficulty));
            DodgeBeatmapSettings.SetForceStoryboard(converted.Difficulty, DodgeBeatmapSettings.GetForceStoryboard(Beatmap.Difficulty));
            DodgeBeatmapSettings.SetForceBeatmapSkin(converted.Difficulty, DodgeBeatmapSettings.GetForceBeatmapSkin(Beatmap.Difficulty));

            var playableBreaks = DodgeBreakPeriodCalculator.ExcludeMovingProjectiles(
                converted.Breaks,
                converted.HitObjects,
                DodgeBeatmapSettings.GetBulletSize(converted.Difficulty));

            converted.Breaks.Clear();
            converted.Breaks.AddRange(playableBreaks);
            return converted;
        }

        internal static DodgeHitObject CloneHitObject(DodgeHitObject hitObject)
        {
            DodgeHitObject clone = hitObject switch
            {
                DodgeBullet bullet => new DodgeBullet
                {
                    StartTime = bullet.StartTime,
                    Duration = bullet.Duration,
                    Position = bullet.Position,
                    EndPosition = bullet.EndPosition,
                    Shape = bullet.Shape,
                    ContinueUntilExit = bullet.ContinueUntilExit,
                    Colour = bullet.Colour,
                    OutlineColour = bullet.OutlineColour,
                    Opacity = bullet.Opacity,
                    OutlineThickness = bullet.OutlineThickness,
                    MovementType = bullet.MovementType,
                    WaveAmplitude = bullet.WaveAmplitude,
                    WaveCycles = bullet.WaveCycles,
                    WavePhase = bullet.WavePhase,
                    TrajectoryGuideStyle = bullet.TrajectoryGuideStyle,
                },
                DodgeEmitter emitter => new DodgeEmitter
                {
                    StartTime = emitter.StartTime,
                    Duration = emitter.Duration,
                    Position = emitter.Position,
                    AimPosition = emitter.AimPosition,
                    MovementEndPosition = emitter.MoveSource ? emitter.MovementEndPosition : emitter.Position,
                    MoveSource = emitter.MoveSource,
                    BulletCount = emitter.BulletCount,
                    SpreadAngle = emitter.SpreadAngle,
                    BurstCount = emitter.BurstCount,
                    BurstInterval = emitter.BurstInterval,
                    BurstBeatDivisor = emitter.BurstBeatDivisor,
                    Shape = emitter.Shape,
                    ContinueUntilExit = emitter.ContinueUntilExit,
                    Colour = emitter.Colour,
                    OutlineColour = emitter.OutlineColour,
                    Opacity = emitter.Opacity,
                    OutlineThickness = emitter.OutlineThickness,
                    MovementType = emitter.MovementType,
                    WaveAmplitude = emitter.WaveAmplitude,
                    WaveCycles = emitter.WaveCycles,
                    WavePhase = emitter.WavePhase,
                    TrajectoryGuideStyle = emitter.TrajectoryGuideStyle,
                },
                DodgeCameraChange cameraChange => new DodgeCameraChange
                {
                    StartTime = cameraChange.StartTime,
                    Duration = cameraChange.Duration,
                    Position = cameraChange.Position,
                    EndPosition = cameraChange.EndPosition,
                    Continuous = cameraChange.Continuous,
                },
                DodgeArenaChange change => new DodgeArenaChange
                {
                    StartTime = change.StartTime,
                    Duration = change.Duration,
                    TargetPosition = change.TargetPosition,
                    TargetSize = change.TargetSize,
                    TargetRotation = change.TargetRotation,
                    Colour = change.Colour,
                    Opacity = change.Opacity,
                    OutlineColour = change.OutlineColour,
                    BorderOpacity = change.BorderOpacity,
                },
                DodgeBeam beam => new DodgeBeam
                {
                    StartTime = beam.StartTime,
                    Duration = beam.Duration,
                    Position = beam.Position,
                    EndPosition = beam.EndPosition,
                    BeamWidth = beam.BeamWidth,
                    Colour = beam.Colour,
                    OutlineColour = beam.OutlineColour,
                    Opacity = beam.Opacity,
                    OutlineThickness = beam.OutlineThickness,
                },
                _ => throw new InvalidOperationException($"Unsupported Dodge object type: {hitObject.GetType().Name}"),
            };

            clone.Samples = hitObject.Samples.ToList();
            return clone;
        }
    }
}
