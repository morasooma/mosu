// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Utils;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeTrajectoryTest
    {
        [Test]
        public void TestTimelineResizeSlowsWaveTrajectory()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(300, 100),
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = 40,
                WaveCycles = 2,
            };

            bullet.SetEditorTimelineEndTime(2000);

            Assert.Multiple(() =>
            {
                Assert.That(bullet.Duration, Is.EqualTo(1000));
                Assert.That(bullet.EndPosition, Is.EqualTo(new Vector2(300, 100)));
                Assert.That(Precision.AlmostEquals(bullet.PositionAt(1500), new Vector2(200, 100), 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(bullet.PositionAt(2000), bullet.EndPosition, 0.001f), Is.True);
            });
        }

        [Test]
        public void TestBulletStopsAtControlEndByDefault()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(200, 100),
            };

            Assert.Multiple(() =>
            {
                Assert.That(bullet.MovementEndTime, Is.EqualTo(1500));
                Assert.That(bullet.PositionAt(3000), Is.EqualTo(new Vector2(200, 100)));
                Assert.That(bullet.TrajectoryEndPosition, Is.EqualTo(new Vector2(200, 100)));
            });
        }

        [Test]
        public void TestBulletContinuesUntilFullyOutsidePlayfield()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(200, 100),
                ContinueUntilExit = true,
            };

            Assert.Multiple(() =>
            {
                Assert.That(bullet.PositionAt(1500), Is.EqualTo(new Vector2(200, 100)));
                Assert.That(bullet.PositionAt(2000), Is.EqualTo(new Vector2(300, 100)));
                Assert.That(Precision.AlmostEquals(bullet.TrajectoryEndPosition, new Vector2(518, 100), 0.001f), Is.True);
                Assert.That(bullet.MovementEndTime, Is.EqualTo(3090).Within(0.001));
                Assert.That(Precision.AlmostEquals(bullet.PositionAt(bullet.MovementEndTime), bullet.TrajectoryEndPosition, 0.001f), Is.True);
            });
        }

        [Test]
        public void TestBulletOutsidePlayfieldCanStillEnterAndLeave()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 0,
                Duration = 500,
                Position = new Vector2(-100, 192),
                EndPosition = new Vector2(-50, 192),
                ContinueUntilExit = true,
            };

            Assert.That(Precision.AlmostEquals(bullet.TrajectoryEndPosition, new Vector2(518, 192), 0.001f), Is.True);
        }

        [Test]
        public void TestCameraAwareContinuationWaitsForOutsideBulletToEnter()
        {
            double exitTime = DodgeTrajectory.CalculateExitTimeWithCamera(
                0,
                500,
                new Vector2(-100, 192),
                new Vector2(-50, 192),
                12,
                DodgeMovementType.Linear,
                DodgeHitObject.DEFAULT_WAVE_AMPLITUDE,
                DodgeHitObject.DEFAULT_WAVE_CYCLES,
                0,
                _ => Vector2.Zero,
                10000);

            Vector2 exitPosition = DodgeTrajectory.PositionAtProgress(
                new Vector2(-100, 192),
                new Vector2(-50, 192),
                (float)(exitTime / 500),
                DodgeMovementType.Linear,
                DodgeHitObject.DEFAULT_WAVE_AMPLITUDE,
                DodgeHitObject.DEFAULT_WAVE_CYCLES,
                0);

            Assert.Multiple(() =>
            {
                Assert.That(exitTime, Is.GreaterThan(1000), "The projectile must survive long enough to enter the playfield.");
                Assert.That(exitPosition.X, Is.EqualTo(DodgePlayfield.WIDTH + 6).Within(0.01));
            });
        }

        [Test]
        public void TestSineBulletOutsideAtAuthoredEndCanStillEnterAndLeave()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 0,
                Duration = 500,
                Position = new Vector2(-100, 192),
                EndPosition = new Vector2(-50, 192),
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = 20,
                WaveCycles = 1,
                ContinueUntilExit = true,
            };

            Assert.Multiple(() =>
            {
                Assert.That(bullet.MovementEndTime, Is.GreaterThan(1000), "The projectile must not expire before entering the playfield.");
                Assert.That(bullet.TrajectoryEndPosition.X, Is.GreaterThanOrEqualTo(DodgePlayfield.WIDTH + bullet.BulletSize / 2 - 0.01f));
            });
        }

        [Test]
        public void TestBulletOutsideFlyingAwayExpiresImmediately()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(-100, 192),
                EndPosition = new Vector2(-150, 192),
                ContinueUntilExit = true,
            };

            Assert.That(bullet.MovementEndTime, Is.EqualTo(1000), "A projectile starting outside and flying away should expire immediately.");
        }

        [Test]
        public void TestCameraAwareBulletOutsideFlyingAwayDoesNotLoopOrSurvive()
        {
            double exitTime = DodgeTrajectory.CalculateExitTimeWithCamera(
                1000,
                500,
                new Vector2(-100, 192),
                new Vector2(-150, 192),
                12,
                DodgeMovementType.Linear,
                0,
                0,
                0,
                _ => Vector2.Zero,
                180000);

            Assert.That(exitTime, Is.EqualTo(1000), "Camera-aware search should immediately discard an outside projectile moving away.");
        }

        [Test]
        public void TestEmitterKeepsEachBulletUntilItsOwnExit()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 0,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(356, 192),
                BulletCount = 3,
                SpreadAngle = 180,
                ContinueUntilExit = true,
            };

            Assert.Multiple(() =>
            {
                Assert.That(emitter.ExitTimeAt(0), Is.GreaterThan(emitter.EndTime));
                Assert.That(emitter.ExitTimeAt(1), Is.GreaterThan(emitter.EndTime));
                Assert.That(emitter.ExitTimeAt(2), Is.GreaterThan(emitter.EndTime));
                Assert.That(emitter.MovementEndTime, Is.EqualTo(2620).Within(0.001));
                Assert.That(Precision.AlmostEquals(emitter.TrajectoryEndPositionAt(1), new Vector2(518, 192), 0.001f), Is.True);
            });
        }

        [Test]
        public void TestLastContinuedObjectIsNotClippedByGracePeriod()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(110, 100),
                ContinueUntilExit = true,
            };
            var playfield = new DodgePlayfield(new DodgeHitObject[] { bullet });

            Assert.Multiple(() =>
            {
                Assert.That(bullet.MovementEndTime, Is.GreaterThan(bullet.EndTime));
                Assert.That(playfield.GameplayEndTime, Is.EqualTo(bullet.EndTime));
                Assert.That(bullet.MovementEndTime, Is.GreaterThan(bullet.EndTime + DodgePlayfield.CONTINUED_BULLET_GRACE_PERIOD));
                Assert.That(playfield.ContinuedBulletEndTime, Is.EqualTo(bullet.MovementEndTime));
                Assert.That(playfield.GetEffectiveMovementEndTime(bullet), Is.EqualTo(bullet.MovementEndTime));
            });
        }

        [Test]
        public void TestContinuedObjectMayExitDuringGracePeriod()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(200, 100),
                ContinueUntilExit = true,
            };
            var laterObject = new DodgeBullet
            {
                StartTime = 2000,
                Duration = 500,
            };
            var playfield = new DodgePlayfield(new DodgeHitObject[] { bullet, laterObject });

            Assert.That(playfield.GetEffectiveMovementEndTime(bullet), Is.EqualTo(bullet.MovementEndTime));
        }

        [Test]
        public void TestSineBulletFollowsWaveAndStillHitsControlEnd()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(300, 100),
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = 40,
                WaveCycles = 1,
            };

            Assert.Multiple(() =>
            {
                Assert.That(Precision.AlmostEquals(bullet.PositionAt(1250), new Vector2(150, 140), 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(bullet.PositionAt(1500), new Vector2(200, 100), 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(bullet.PositionAt(1750), new Vector2(250, 60), 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(bullet.PositionAt(2000), bullet.EndPosition, 0.001f), Is.True);
            });
        }

        [Test]
        public void TestSineBulletContinuesWaveUntilOutside()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 0,
                Duration = 1000,
                Position = new Vector2(100, 192),
                EndPosition = new Vector2(300, 192),
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = 64,
                WaveCycles = 2,
                WavePhase = 35,
                ContinueUntilExit = true,
            };

            Assert.Multiple(() =>
            {
                Assert.That(bullet.MovementEndTime, Is.GreaterThan(bullet.EndTime));
                Assert.That(Precision.AlmostEquals(bullet.PositionAt(bullet.EndTime), bullet.EndPosition, 0.001f), Is.True);
                Assert.That(Precision.AlmostEquals(bullet.PositionAt(bullet.MovementEndTime), bullet.TrajectoryEndPosition, 0.001f), Is.True);
                Assert.That(bullet.TrajectoryEndPosition.X, Is.GreaterThanOrEqualTo(DodgePlayfield.WIDTH + bullet.BulletSize / 2 - 0.01f));
            });
        }

        [Test]
        public void TestArenaChangeDoesNotExtendContinuedBulletGracePeriod()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(110, 100),
                ContinueUntilExit = true,
            };
            var arenaChange = new DodgeArenaChange
            {
                StartTime = 1200,
                Duration = 10000,
            };
            var playfield = new DodgePlayfield(new DodgeHitObject[] { bullet, arenaChange });

            Assert.Multiple(() =>
            {
                Assert.That(playfield.GameplayEndTime, Is.EqualTo(bullet.EndTime));
                Assert.That(playfield.GetEffectiveMovementEndTime(bullet), Is.EqualTo(bullet.MovementEndTime));
            });
        }
    }
}
