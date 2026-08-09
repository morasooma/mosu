// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Dodge.Edit;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeEditorTrackBoundsTest
    {
        [Test]
        public void TestDurationIsClampedAtTrackEnd()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 9_500,
                Duration = 2_000,
            };

            Assert.That(DodgeEditorTrackBounds.Constrain(bullet, 10_000, true), Is.True);
            Assert.That(bullet.Duration, Is.EqualTo(500));
            Assert.That(bullet.GetEndTime(), Is.EqualTo(10_000));
        }

        [Test]
        public void TestManualPlacementStartIsClampedAtTrackEnd()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 10_100,
                Duration = 2_000,
            };

            Assert.That(DodgeEditorTrackBounds.Constrain(emitter, 10_000, true), Is.True);
            Assert.That(emitter.StartTime, Is.EqualTo(10_000));
            Assert.That(emitter.Duration, Is.Zero);
        }

        [Test]
        public void TestGeneratedObjectAfterTrackEndIsRejected()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 10_100,
                Duration = 2_000,
            };

            Assert.That(DodgeEditorTrackBounds.Constrain(bullet, 10_000, false), Is.False);
        }

        [Test]
        public void TestGeneratedObjectAtTrackEndIsRejected()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 10_000,
                Duration = 2_000,
            };

            Assert.That(DodgeEditorTrackBounds.Constrain(bullet, 10_000, false), Is.False);
        }

        [Test]
        public void TestMovingEmitterBurstsAndFlightAreClampedAtTrackEnd()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 9_000,
                Duration = 800,
                BurstCount = 5,
                BurstInterval = 250,
            };

            Assert.That(DodgeEditorTrackBounds.Constrain(emitter, 10_000, true), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(emitter.BurstCount, Is.EqualTo(5));
                Assert.That(emitter.EmissionDuration, Is.EqualTo(1000));
                Assert.That(emitter.Duration, Is.Zero);
                Assert.That(emitter.EndTime, Is.EqualTo(10_000));
            });
        }
    }
}
