// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeProjectileMotionTest
    {
        [TestCase(DodgeMovementEasing.Linear, 25)]
        [TestCase(DodgeMovementEasing.EaseIn, 6.25f)]
        [TestCase(DodgeMovementEasing.EaseOut, 43.75f)]
        [TestCase(DodgeMovementEasing.EaseInOut, 12.5f)]
        public void TestMovementEasingChangesAuthoredFlight(DodgeMovementEasing easing, float expectedX)
        {
            var bullet = new DodgeBullet
            {
                StartTime = 0,
                Duration = 1000,
                Position = Vector2.Zero,
                EndPosition = new Vector2(100, 0),
                MovementEasing = easing,
            };

            Assert.That(bullet.PositionAt(250).X, Is.EqualTo(expectedX).Within(0.001f));
            Assert.That(bullet.PositionAt(1000), Is.EqualTo(bullet.EndPosition));
        }

        [Test]
        public void TestContinuedEasedBulletStillLeavesPlayfield()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 0,
                Duration = 1000,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(200, 100),
                ContinueUntilExit = true,
                MovementEasing = DodgeMovementEasing.EaseOut,
            };

            Assert.That(bullet.MovementEndTime, Is.GreaterThan(bullet.EndTime));
            Assert.That(bullet.PositionAt(bullet.MovementEndTime).X, Is.GreaterThan(512));
        }

        [Test]
        public void TestEmitterRotatesEachBurst()
        {
            var emitter = new DodgeEmitter
            {
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(356, 192),
                BulletCount = 2,
                SpreadAngle = 0,
                BurstCount = 4,
                BurstRotation = 90,
            };

            Assert.That(emitter.EndPositionAt(0, 0), Is.EqualTo(new Vector2(356, 192)));
            Assert.That(emitter.EndPositionAt(1, 0).X, Is.EqualTo(256).Within(0.001f));
            Assert.That(emitter.EndPositionAt(1, 0).Y, Is.EqualTo(292).Within(0.001f));
            Assert.That(emitter.EndPositionAt(2, 0).X, Is.EqualTo(156).Within(0.001f));
            Assert.That(emitter.EndPositionAt(2, 0).Y, Is.EqualTo(192).Within(0.001f));
        }
    }
}
