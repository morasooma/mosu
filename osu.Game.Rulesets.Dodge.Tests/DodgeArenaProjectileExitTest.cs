using NUnit.Framework;
using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeArenaProjectileExitTest
    {
        private static DodgeBullet bulletAt(double time = 0) => new DodgeBullet
        {
            StartTime = time,
            Duration = 1000,
            Position = new Vector2(256, 192),
            EndPosition = new Vector2(512, 192),
            ContinueUntilExit = true,
        };

        private static DodgeArenaChange resizeAt(double time, float width, double duration = 0) => new DodgeArenaChange
        {
            StartTime = time,
            Duration = duration,
            WorldSpace = true,
            TargetPosition = Vector2.Zero,
            TargetSize = new Vector2(width, 384),
        };

        [Test]
        public void TestFutureExpansionDoesNotExtendEarlierBullet()
        {
            var bullet = bulletAt();
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, resizeAt(5000, 2048) });
            Assert.That(bullet.MovementEndTime, Is.EqualTo(1000 + bullet.BulletSize / 2 / 0.256).Within(0.01));
        }

        [Test]
        public void TestExpansionBeforeExitExtendsFlight()
        {
            var bullet = bulletAt();
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, resizeAt(500, 1024) });
            Assert.That(bullet.MovementEndTime, Is.EqualTo(3000 + bullet.BulletSize / 2 / 0.256).Within(0.02));
        }

        [Test]
        public void TestResizeTransitionUsesInterpolatedBoundary()
        {
            var bullet = bulletAt();
            // The trailing edge clears the interpolated right boundary.
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, resizeAt(500, 1024, 10000) });
            Assert.That(bullet.MovementEndTime, Is.EqualTo((230.4 + bullet.BulletSize / 2) / 0.2048).Within(0.02));
        }

        [Test]
        public void TestShrinkAfterLargeArenaUsesSmallBoundary()
        {
            var bullet = bulletAt(2000);
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, resizeAt(0, 2048), resizeAt(1500, 512) });
            Assert.That(bullet.MovementEndTime, Is.EqualTo(3000 + bullet.BulletSize / 2 / 0.256).Within(0.01));
        }

        [Test]
        public void TestBriefShrinkPermanentlyEndsFlight()
        {
            var bullet = bulletAt();
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[]
            {
                bullet, resizeAt(0, 2048), resizeAt(500, 300), resizeAt(500.1, 2048),
            });
            Assert.That(bullet.MovementEndTime, Is.EqualTo(500).Within(0.001));
        }

        [Test]
        public void TestRotatedArenaUsesOrientedBoundaryNotUnionBox()
        {
            var bullet = bulletAt();
            var arena = resizeAt(0, 512);
            arena.TargetRotation = 90;
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, arena });
            // Rotating the 512x384 rectangle around (256,192) puts its right edge at 448.
            Assert.That(bullet.MovementEndTime, Is.EqualTo(750 + bullet.BulletSize / 2 / 0.256).Within(0.01));
        }

        [Test]
        public void TestEmitterBurstsUseTheirOwnEmissionTimes()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 0,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 2,
                SpreadAngle = 0,
                BurstCount = 2,
                BurstInterval = 2000,
                ContinueUntilExit = true,
            };
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { emitter, resizeAt(1500, 1024) });
            Assert.Multiple(() =>
            {
                Assert.That(emitter.ExitTimeAt(0, 0), Is.EqualTo(1000 + emitter.BulletSize / 2 / 0.256).Within(0.01));
                Assert.That(emitter.ExitTimeAt(1, 0), Is.EqualTo(5000 + emitter.BulletSize / 2 / 0.256).Within(0.01));
            });
        }

        [Test]
        public void TestCameraDriftUsesCurrentArena()
        {
            var bullet = bulletAt();
            var arena = DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, resizeAt(5000, 2048) });
            double exit = DodgeWorldGeometry.ExitTime(new RectangleF(0, 0, 2048, 384), arena, 0, 1000,
                bullet.Position, bullet.EndPosition, 0, DodgeMovementType.Linear, 0, 1, 0, DodgeMovementEasing.Linear,
                t => new Vector2((float)(0.256 * t), 0));
            Assert.That(exit, Is.EqualTo(500).Within(0.02));
        }

        [Test]
        public void TestLegacyResizeTriggerAlsoRespectsItsTime()
        {
            var bullet = bulletAt();
            var trigger = new DodgeTrigger
            {
                StartTime = 5000,
                Duration = 0,
                Action = DodgeTriggerAction.ResizeArena,
                TargetPosition = Vector2.Zero,
                TargetSize = new Vector2(2048, 384),
            };
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, trigger });
            Assert.That(bullet.MovementEndTime, Is.EqualTo(1000 + bullet.BulletSize / 2 / 0.256).Within(0.01));
        }

        [Test]
        public void TestChangingEventTimeRecalculatesExitWithoutChangingWorldBounds()
        {
            var bullet = bulletAt();
            var resize = resizeAt(5000, 1024);
            DodgeHitObject[] objects = { bullet, resize };
            DodgeWorldGeometry.ApplyArenaTimeline(objects);
            var bounds = bullet.WorldBounds;
            Assert.That(bullet.MovementEndTime, Is.EqualTo(1000 + bullet.BulletSize / 2 / 0.256).Within(0.01));
            resize.StartTime = 500;
            DodgeWorldGeometry.ApplyArenaTimeline(objects);
            Assert.Multiple(() =>
            {
                Assert.That(bullet.WorldBounds, Is.EqualTo(bounds));
                Assert.That(bullet.MovementEndTime, Is.EqualTo(3000 + bullet.BulletSize / 2 / 0.256).Within(0.02));
            });
        }

        [Test]
        public void TestOffscreenRayEntersAfterEarlierArenaExpansion()
        {
            var bullet = bulletAt();
            bullet.Position = new Vector2(800, 192);
            bullet.EndPosition = new Vector2(900, 192);
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, resizeAt(500, 1024) });
            Assert.That(bullet.MovementEndTime, Is.EqualTo((224 + bullet.BulletSize / 2) / 0.1).Within(0.02));
        }

        [Test]
        public void TestStationaryBulletExitsAtLaterShrinkWithoutWaitingForFutureExpansion()
        {
            var bullet = bulletAt();
            bullet.Position = bullet.EndPosition = new Vector2(450, 192);
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[]
            {
                bullet, resizeAt(500, 300), resizeAt(900, 2048),
            });
            Assert.That(bullet.MovementEndTime, Is.EqualTo(500).Within(0.001));
        }

        [Test]
        public void TestExitAfterTransitionFinishesUsesFinalArena()
        {
            var bullet = bulletAt();
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, resizeAt(100, 1024, 300) });
            Assert.That(bullet.MovementEndTime, Is.EqualTo(3000 + bullet.BulletSize / 2 / 0.256).Within(0.02));
        }

        [Test]
        public void TestNoArenaEventsPreservesOriginalTrajectorySolver()
        {
            var bullet = bulletAt();
            bullet.MovementType = DodgeMovementType.Sine;
            double original = bullet.MovementEndTime;
            DodgeWorldGeometry.ApplyArenaTimeline(new[] { bullet });
            Assert.That(bullet.MovementEndTime, Is.EqualTo(original));
        }
    }
}
