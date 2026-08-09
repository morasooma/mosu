// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class OsuLargeCircleAimPenaltyTest
    {
        [Test]
        public void TestNoPenaltyAtOrAboveCircleSizeFour()
        {
            Assert.Multiple(() =>
            {
                Assert.That(calculatePenalty(4), Is.EqualTo(1));
                Assert.That(calculatePenalty(5), Is.EqualTo(1));
                Assert.That(calculatePenalty(10), Is.EqualTo(1));
            });
        }

        [Test]
        public void TestPenaltyIncreasesWithCircleRadius()
        {
            double cs3 = calculatePenalty(3);
            double cs2 = calculatePenalty(2);
            double cs0 = calculatePenalty(0);

            Assert.Multiple(() =>
            {
                Assert.That(cs3, Is.LessThan(1));
                Assert.That(cs2, Is.LessThan(cs3));
                Assert.That(cs0, Is.LessThan(cs2));
            });
        }

        [Test]
        public void TestCircleSizeTwoUsesSoftRadiusPenalty()
        {
            Assert.That(calculatePenalty(2), Is.EqualTo(0.947).Within(0.001));
        }

        private static double calculatePenalty(float circleSize)
        {
            double radius = OsuHitObject.OBJECT_RADIUS * LegacyRulesetExtensions.CalculateScaleFromCircleSize(circleSize, true);
            return Aim.CalculateLargeCirclePenalty(radius);
        }
    }
}
