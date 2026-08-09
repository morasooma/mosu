// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Dodge.Edit;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeCoverageCalculatorTest
    {
        [Test]
        public void TestEmptyMapIsEntirelySafe()
        {
            int[] counts = DodgeCoverageCalculator.CalculateHitCounts(Enumerable.Empty<DodgeHitObject>());

            Assert.That(counts, Has.Length.EqualTo(DodgeCoverageCalculator.DEFAULT_COLUMNS * DodgeCoverageCalculator.DEFAULT_ROWS));
            Assert.That(counts, Has.All.Zero);
        }

        [Test]
        public void TestBulletMarksPlayerCollisionWidthAlongTrajectory()
        {
            var bullet = new DodgeBullet
            {
                Position = new Vector2(100, 192),
                EndPosition = new Vector2(400, 192),
                Duration = 1000,
            };

            int[] counts = DodgeCoverageCalculator.CalculateHitCounts(new DodgeHitObject[] { bullet });

            Assert.That(countAt(counts, new Vector2(252, 188)), Is.GreaterThan(0));
            Assert.That(countAt(counts, new Vector2(252, 164)), Is.Zero);
        }

        [Test]
        public void TestPlayerSizeChangesCoverageWidth()
        {
            var bullet = new DodgeBullet
            {
                Position = new Vector2(100, 192),
                EndPosition = new Vector2(400, 192),
                Duration = 1000,
            };

            int[] defaultCounts = DodgeCoverageCalculator.CalculateHitCounts(new DodgeHitObject[] { bullet });
            int[] largeCounts = DodgeCoverageCalculator.CalculateHitCounts(new DodgeHitObject[] { bullet }, playerSize: 32);

            Assert.That(countAt(defaultCounts, new Vector2(252, 172)), Is.Zero);
            Assert.That(countAt(largeCounts, new Vector2(252, 172)), Is.GreaterThan(0));
        }

        [Test]
        public void TestWaveCoverageFollowsCurveInsteadOfChord()
        {
            var bullet = new DodgeBullet
            {
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(300, 100),
                Duration = 1000,
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = 64,
                WaveCycles = 1,
            };

            int[] counts = DodgeCoverageCalculator.CalculateHitCounts(new DodgeHitObject[] { bullet });

            Assert.Multiple(() =>
            {
                Assert.That(countAt(counts, new Vector2(148, 164)), Is.GreaterThan(0));
                Assert.That(countAt(counts, new Vector2(148, 100)), Is.Zero);
            });
        }

        [Test]
        public void TestAllEmitterRaysContributeCoverage()
        {
            var emitter = new DodgeEmitter
            {
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(400, 192),
                BulletCount = 3,
                SpreadAngle = 90,
                Duration = 1000,
            };

            int[] counts = DodgeCoverageCalculator.CalculateHitCounts(new DodgeHitObject[] { emitter });

            Assert.That(countAt(counts, new Vector2(396, 188)), Is.GreaterThan(0), "centre ray");
            Assert.That(countAt(counts, new Vector2(356, 92)), Is.GreaterThan(0), "upper ray");
            Assert.That(countAt(counts, new Vector2(356, 292)), Is.GreaterThan(0), "lower ray");
        }

        [Test]
        public void TestContinuedTrajectoryIncludesGracePeriodAndIsThenCapped()
        {
            var bullet = new DodgeBullet
            {
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(150, 100),
                Duration = 1000,
                ContinueUntilExit = true,
            };
            int[] counts = DodgeCoverageCalculator.CalculateHitCounts(new DodgeHitObject[] { bullet });

            Assert.That(countAt(counts, new Vector2(244, 100)), Is.GreaterThan(0));
            Assert.That(countAt(counts, new Vector2(276, 100)), Is.Zero);
        }

        [Test]
        public void TestArenaPositionDoesNotInvalidateCoverage()
        {
            var arena = new DodgeArenaChange
            {
                StartTime = 2000,
                TargetPosition = new Vector2(10, 20),
                TargetSize = new Vector2(400, 300),
            };

            int initialHash = DodgeCoverageCalculator.CalculateStateHash(new DodgeHitObject[] { arena });

            arena.TargetPosition = new Vector2(50, 60);
            arena.TargetSize = new Vector2(300, 200);

            Assert.That(DodgeCoverageCalculator.CalculateStateHash(new DodgeHitObject[] { arena }), Is.EqualTo(initialHash));
        }

        [Test]
        public void TestBulletPositionInvalidatesCoverage()
        {
            var bullet = new DodgeBullet
            {
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(200, 100),
            };

            int initialHash = DodgeCoverageCalculator.CalculateStateHash(new DodgeHitObject[] { bullet });
            bullet.Position = new Vector2(100, 200);

            Assert.That(DodgeCoverageCalculator.CalculateStateHash(new DodgeHitObject[] { bullet }), Is.Not.EqualTo(initialHash));
        }

        [Test]
        public void TestCalculationUsesCapturedObjectState()
        {
            var bullet = new DodgeBullet
            {
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(400, 100),
            };

            DodgeCoverageCalculator.CalculationInput input = DodgeCoverageCalculator.CreateCalculationInput(new DodgeHitObject[] { bullet });
            bullet.Position = bullet.EndPosition = new Vector2(100, 200);

            int[] counts = DodgeCoverageCalculator.CalculateHitCounts(input);

            Assert.Multiple(() =>
            {
                Assert.That(countAt(counts, new Vector2(252, 100)), Is.GreaterThan(0));
                Assert.That(countAt(counts, new Vector2(252, 200)), Is.Zero);
            });
        }

        private static int countAt(int[] counts, Vector2 position)
        {
            int column = (int)(position.X / (512 / DodgeCoverageCalculator.DEFAULT_COLUMNS));
            int row = (int)(position.Y / (384 / DodgeCoverageCalculator.DEFAULT_ROWS));
            return counts[row * DodgeCoverageCalculator.DEFAULT_COLUMNS + column];
        }
    }
}
