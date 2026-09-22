// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeEmitterTest
    {
        [Test]
        public void TestTrajectoryGuideDefaultsToPath()
        {
            Assert.That(new DodgeEmitter().TrajectoryGuideStyle, Is.EqualTo(DodgeTrajectoryGuideStyle.Path));
        }

        [Test]
        public void TestFanTrajectories()
        {
            var emitter = new DodgeEmitter
            {
                Position = Vector2.Zero,
                AimPosition = new Vector2(100, 0),
                BulletCount = 3,
                SpreadAngle = 90,
            };

            Assert.Multiple(() =>
            {
                Assert.That(Precision.AlmostEquals(emitter.EndPositionAt(0), new Vector2(70.71068f, -70.71068f), 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(emitter.EndPositionAt(1), new Vector2(100, 0), 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(emitter.EndPositionAt(2), new Vector2(70.71068f, 70.71068f), 0.001f), Is.True);
            });
        }

        [Test]
        public void TestFullCircleDoesNotDuplicateDirection()
        {
            var emitter = new DodgeEmitter
            {
                Position = Vector2.Zero,
                AimPosition = new Vector2(100, 0),
                BulletCount = 4,
                SpreadAngle = 360,
            };

            Assert.Multiple(() =>
            {
                Assert.That(Precision.AlmostEquals(emitter.EndPositionAt(0), new Vector2(100, 0), 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(emitter.EndPositionAt(1), new Vector2(0, 100), 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(emitter.EndPositionAt(2), new Vector2(-100, 0), 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(emitter.EndPositionAt(3), new Vector2(0, -100), 0.001f), Is.True);
            });
        }

        [Test]
        public void TestMovementUsesSharedDuration()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(100, 100),
                AimPosition = new Vector2(300, 100),
                BulletCount = 3,
                SpreadAngle = 0,
            };

            Assert.That(emitter.PositionAt(1, 1250), Is.EqualTo(new Vector2(200, 100)));
        }

        [Test]
        public void TestTimelineEndIncludesEmissionDuration()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 500,
                BurstCount = 3,
                BurstInterval = 250,
                BurstBeatDivisor = 0,
                Position = new Vector2(100, 100),
                AimPosition = new Vector2(200, 100),
            };

            emitter.SetEditorTimelineEndTime(2500);

            Assert.Multiple(() =>
            {
                Assert.That(emitter.EmissionDuration, Is.EqualTo(500));
                Assert.That(emitter.Duration, Is.EqualTo(1000));
                Assert.That(emitter.EndTime, Is.EqualTo(2500));
                Assert.That(emitter.AimPosition, Is.EqualTo(new Vector2(200, 100)));
            });
        }

        [Test]
        public void TestEmitterBulletsUseConfiguredWave()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(100, 100),
                AimPosition = new Vector2(300, 100),
                BulletCount = 3,
                SpreadAngle = 0,
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = 40,
                WaveCycles = 1,
            };

            Assert.That(Precision.AlmostEquals(emitter.PositionAt(1, 1250), new Vector2(150, 140), 0.001f), Is.True);
        }

        [Test]
        public void TestMovingEmitterEmitsBurstsAlongSourcePath()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(100, 100),
                AimPosition = new Vector2(200, 100),
                MovementEndPosition = new Vector2(300, 100),
                BulletCount = 3,
                SpreadAngle = 0,
                BurstCount = 3,
                BurstInterval = 250,
                BurstBeatDivisor = 0,
                MoveSource = true,
            };

            Assert.Multiple(() =>
            {
                Assert.That(emitter.EmissionTimeAt(0), Is.EqualTo(1000));
                Assert.That(emitter.EmissionTimeAt(1), Is.EqualTo(1250));
                Assert.That(emitter.EmissionTimeAt(2), Is.EqualTo(1500));
                Assert.That(emitter.SourcePositionAt(1), Is.EqualTo(new Vector2(200, 100)));
                Assert.That(emitter.EndPositionAt(1, 1), Is.EqualTo(new Vector2(300, 100)));
                Assert.That(emitter.PositionAt(1, 1, 1500), Is.EqualTo(new Vector2(250, 100)));
                Assert.That(emitter.EndTime, Is.EqualTo(2000));
                Assert.That(emitter.MovementEndTime, Is.EqualTo(2000));
            });
        }

        [Test]
        public void TestStationaryEmitterCanRepeatWithoutMovingSource()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Position = new Vector2(100, 100),
                AimPosition = new Vector2(200, 100),
                MovementEndPosition = new Vector2(300, 100),
                BurstCount = 3,
                BurstInterval = 250,
                BurstBeatDivisor = 0,
                MoveSource = false,
            };

            Assert.Multiple(() =>
            {
                Assert.That(emitter.EmissionTimeAt(2), Is.EqualTo(1500));
                Assert.That(emitter.SourcePositionAt(0), Is.EqualTo(new Vector2(100, 100)));
                Assert.That(emitter.SourcePositionAt(1), Is.EqualTo(new Vector2(100, 100)));
                Assert.That(emitter.SourcePositionAt(2), Is.EqualTo(new Vector2(100, 100)));
            });
        }

        [Test]
        public void TestBurstIntervalUsesBeatDivisorAtEmitterStart()
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 600 });
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                BurstCount = 3,
                BurstBeatDivisor = (int)DodgeEmitterBeatDivisor.Quarter,
            };

            emitter.ApplyDefaults(controlPoints, new BeatmapDifficulty());

            Assert.Multiple(() =>
            {
                Assert.That(emitter.BurstInterval, Is.EqualTo(150));
                Assert.That(emitter.EmissionTimeAt(1), Is.EqualTo(1150));
                Assert.That(emitter.EmissionTimeAt(2), Is.EqualTo(1300));
            });
        }

        [Test]
        public void TestRepeatedBurstsCreateSeparateJudgements()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                BurstCount = 3,
                BurstInterval = 250,
                BurstBeatDivisor = 0,
            };

            emitter.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            Assert.Multiple(() =>
            {
                Assert.That(emitter.NestedHitObjects, Has.Count.EqualTo(2));
                Assert.That(emitter.NestedHitObjects, Has.All.TypeOf<DodgeEmitterBurst>());
                Assert.That(emitter.NestedHitObjects[0].StartTime, Is.EqualTo(1000));
                Assert.That(emitter.NestedHitObjects[1].StartTime, Is.EqualTo(1250));
            });
        }
    }
}
