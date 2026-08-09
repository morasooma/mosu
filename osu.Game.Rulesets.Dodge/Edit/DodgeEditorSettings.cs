// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Dodge.Objects;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public class DodgeEditorSettings
    {
        public Bindable<DodgeBulletShape> BulletShape { get; } = new Bindable<DodgeBulletShape>();

        public BindableBool BulletContinueUntilExit { get; } = new BindableBool();

        public BindableBool BulletLockFlight { get; } = new BindableBool();

        public Bindable<DodgeMovementType> BulletMovementType { get; } = new Bindable<DodgeMovementType>();

        public BindableFloat BulletWaveAmplitude { get; } = createWaveAmplitude();

        public BindableInt BulletWaveCycles { get; } = createWaveCycles();

        public BindableFloat BulletWavePhase { get; } = createWavePhase();

        public Bindable<DodgeTrajectoryGuideStyle> BulletTrajectoryGuideStyle { get; }
            = new Bindable<DodgeTrajectoryGuideStyle>(DodgeTrajectoryGuideStyle.Arrow);

        public Bindable<Colour4> BulletColour { get; } = new Bindable<Colour4>(Colour4.White);

        public Bindable<Colour4> BulletOutlineColour { get; } = new Bindable<Colour4>(Colour4.White);

        public BindableFloat BulletOpacity { get; } = new BindableFloat(1)
        {
            MinValue = 0.1f,
            MaxValue = 1,
            Precision = 0.01f,
        };

        public BindableFloat BulletOutlineThickness { get; } = new BindableFloat
        {
            MinValue = 0,
            MaxValue = 8,
            Precision = 0.25f,
        };

        public Bindable<DodgeBulletShape> EmitterShape { get; } = new Bindable<DodgeBulletShape>();

        public BindableInt EmitterBulletCount { get; } = new BindableInt(DodgeEmitter.DEFAULT_BULLET_COUNT)
        {
            MinValue = DodgeEmitter.MIN_BULLET_COUNT,
            MaxValue = DodgeEmitter.MAX_BULLET_COUNT,
        };

        public BindableFloat EmitterSpreadAngle { get; } = new BindableFloat(DodgeEmitter.DEFAULT_SPREAD_ANGLE)
        {
            MinValue = DodgeEmitter.MIN_SPREAD_ANGLE,
            MaxValue = DodgeEmitter.MAX_SPREAD_ANGLE,
            Precision = 1,
        };

        public BindableBool EmitterContinueUntilExit { get; } = new BindableBool();

        public BindableBool EmitterMoving { get; } = new BindableBool();

        public BindableBool EmitterRepeating { get; } = new BindableBool();

        public Bindable<DodgeMovementType> EmitterMovementType { get; } = new Bindable<DodgeMovementType>();

        public BindableFloat EmitterWaveAmplitude { get; } = createWaveAmplitude();

        public BindableInt EmitterWaveCycles { get; } = createWaveCycles();

        public BindableFloat EmitterWavePhase { get; } = createWavePhase();

        public Bindable<DodgeTrajectoryGuideStyle> EmitterTrajectoryGuideStyle { get; }
            = new Bindable<DodgeTrajectoryGuideStyle>(DodgeTrajectoryGuideStyle.Path);

        public BindableInt EmitterBurstCount { get; } = new BindableInt(DodgeEmitter.DEFAULT_BURST_COUNT)
        {
            MinValue = 2,
            MaxValue = DodgeEmitter.MAX_BURST_COUNT,
        };

        public Bindable<DodgeEmitterBeatDivisor> EmitterBurstBeatDivisor { get; }
            = new Bindable<DodgeEmitterBeatDivisor>(DodgeEmitterBeatDivisor.Quarter);

        public Bindable<Colour4> EmitterColour { get; } = new Bindable<Colour4>(Colour4.White);

        public Bindable<Colour4> EmitterOutlineColour { get; } = new Bindable<Colour4>(Colour4.White);

        public BindableFloat EmitterOpacity { get; } = new BindableFloat(1)
        {
            MinValue = 0.1f,
            MaxValue = 1,
            Precision = 0.01f,
        };

        public BindableFloat EmitterOutlineThickness { get; } = new BindableFloat
        {
            MinValue = 0,
            MaxValue = 8,
            Precision = 0.25f,
        };

        public Bindable<Colour4> ArenaBackgroundColour { get; } = new Bindable<Colour4>(Colour4.Black);

        public BindableFloat ArenaBackgroundOpacity { get; } = createOpacity();

        public Bindable<Colour4> ArenaBorderColour { get; } = new Bindable<Colour4>(Colour4.White);

        public BindableFloat ArenaBorderOpacity { get; } = createOpacity();

        public BindableFloat ArenaRotation { get; } = new BindableFloat(0)
        {
            MinValue = -360,
            MaxValue = 360,
            Precision = 1,
        };

        public BindableFloat ArenaKiaiShakeAngle { get; } = new BindableFloat(0)
        {
            MinValue = 0,
            MaxValue = 30,
            Precision = 0.5f,
        };

        public BindableFloat BeamWidth { get; } = new BindableFloat(DodgeBeam.DEFAULT_WIDTH)
        {
            MinValue = DodgeBeam.MIN_WIDTH,
            MaxValue = DodgeBeam.MAX_WIDTH,
            Precision = 1,
        };

        public Bindable<Colour4> BeamColour { get; } = new Bindable<Colour4>(Colour4.White);

        public Bindable<Colour4> BeamOutlineColour { get; } = new Bindable<Colour4>(Colour4.White);

        public BindableFloat BeamOpacity { get; } = new BindableFloat(1)
        {
            MinValue = 0.1f,
            MaxValue = 1,
            Precision = 0.01f,
        };

        public BindableFloat BeamOutlineThickness { get; } = new BindableFloat
        {
            MinValue = 0,
            MaxValue = 8,
            Precision = 0.25f,
        };

        public BindableBool CompactPlayfield { get; } = new BindableBool();

        public BindableBool CameraContinuousScroll { get; } = new BindableBool();

        public BindableBool ShowBulletCoverage { get; } = new BindableBool();

        public BindableBool ShowAutoplayRoute { get; } = new BindableBool();

        public BindableBool GridSnapEnabled { get; } = new BindableBool();

        private static BindableFloat createWaveAmplitude() => new BindableFloat(DodgeHitObject.DEFAULT_WAVE_AMPLITUDE)
        {
            MinValue = -512,
            MaxValue = 512,
            Precision = 1,
        };

        private static BindableInt createWaveCycles() => new BindableInt(DodgeHitObject.DEFAULT_WAVE_CYCLES)
        {
            MinValue = 1,
            MaxValue = 32,
        };

        private static BindableFloat createWavePhase() => new BindableFloat
        {
            MinValue = -180,
            MaxValue = 180,
            Precision = 1,
        };

        private static BindableFloat createOpacity() => new BindableFloat(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };
    }
}
