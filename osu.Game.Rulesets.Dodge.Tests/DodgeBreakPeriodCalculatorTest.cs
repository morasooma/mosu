// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeBreakPeriodCalculatorTest
    {
        [Test]
        public void TestBreakStartsAfterBulletStops()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
            };

            var result = DodgeBreakPeriodCalculator.ExcludeMovingProjectiles(
                new[] { new BreakPeriod(1500, 5000) },
                new DodgeHitObject[] { bullet },
                DodgeBullet.SIZE);

            Assert.That(result, Is.EqualTo(new[] { new BreakPeriod(2200, 5000) }));
        }

        [Test]
        public void TestMovingBulletSplitsBreak()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 2000,
                Duration = 1000,
            };

            var result = DodgeBreakPeriodCalculator.ExcludeMovingProjectiles(
                new[] { new BreakPeriod(1000, 5000) },
                new DodgeHitObject[] { bullet },
                DodgeBullet.SIZE);

            Assert.That(result, Is.EqualTo(new[]
            {
                new BreakPeriod(1000, 2000),
                new BreakPeriod(3200, 5000),
            }));
        }

        [Test]
        public void TestTooShortRemainderIsRemoved()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 3600,
            };

            var result = DodgeBreakPeriodCalculator.ExcludeMovingProjectiles(
                new[] { new BreakPeriod(1500, 5000) },
                new DodgeHitObject[] { bullet },
                DodgeBullet.SIZE);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void TestContinuedEmitterBlocksBreakUntilGameplayCap()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(266, 192),
                BulletCount = 3,
                ContinueUntilExit = true,
            };

            var result = DodgeBreakPeriodCalculator.ExcludeMovingProjectiles(
                new[] { new BreakPeriod(1500, 5000) },
                new DodgeHitObject[] { emitter },
                DodgeBullet.SIZE);

            Assert.That(result, Is.EqualTo(new[] { new BreakPeriod(4200, 5000) }));
        }

        [Test]
        public void TestContinuedBulletBlocksBreakAfterNominalEnd()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                EndPosition = new Vector2(266, 192),
                ContinueUntilExit = true,
            };

            var laterObject = new DodgeBullet
            {
                StartTime = 10_000,
                Duration = 1000,
            };

            var result = DodgeBreakPeriodCalculator.ExcludeMovingProjectiles(
                new[] { new BreakPeriod(2000, 5000) },
                new DodgeHitObject[] { bullet, laterObject },
                DodgeBullet.SIZE);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void TestContinuedEmitterBlocksBreakAfterNominalEnd()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(266, 192),
                BulletCount = 3,
                ContinueUntilExit = true,
            };

            var laterObject = new DodgeBullet
            {
                StartTime = 10_000,
                Duration = 1000,
            };

            var result = DodgeBreakPeriodCalculator.ExcludeMovingProjectiles(
                new[] { new BreakPeriod(2000, 5000) },
                new DodgeHitObject[] { emitter, laterObject },
                DodgeBullet.SIZE);

            Assert.That(result, Is.Empty);
        }
    }
}
