// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class RxLongMapPenaltyTest
    {
        [TestCase(0, 1)]
        [TestCase(60, 1)]
        [TestCase(90, 1)]
        [TestCase(105, 0.975)]
        [TestCase(120, 0.95)]
        [TestCase(360, 0.90)]
        [TestCase(600, 0.85)]
        [TestCase(1800, 0.85)]
        public void TestAnchors(double seconds, double expected)
            => Assert.That(RxLongMapPenalty.Multiplier(seconds, 0.15), Is.EqualTo(expected).Within(1e-12));

        [Test]
        public void TestOffByDefault()
        {
            Assert.That(new MosuPpBalance().LongMapNerf, Is.Zero);
            Assert.That(RxLongMapPenalty.Multiplier(1800, 0), Is.EqualTo(1));
        }

        [Test]
        public void TestLongerNeverPaysMoreAndBoundariesAreContinuous()
        {
            double previous = 1;
            for (double t = 0; t <= 1000; t += 0.1)
            {
                double current = RxLongMapPenalty.Multiplier(t, 0.15);
                Assert.That(current, Is.InRange(0.85, previous + 1e-12));
                previous = current;
            }
            foreach (double boundary in new[] { 90.0, 120.0, 600.0 })
                Assert.That(Math.Abs(RxLongMapPenalty.Multiplier(boundary + 1e-5, 0.15)
                    - RxLongMapPenalty.Multiplier(boundary - 1e-5, 0.15)), Is.LessThan(1e-9));
        }

        [Test]
        public void TestPlayedTimeMakesDtLessAffected()
            => Assert.That(RxLongMapPenalty.Multiplier(360 / 1.5, 0.15), Is.GreaterThan(RxLongMapPenalty.Multiplier(360, 0.15)));

        [Test]
        public void TestRejectsInvalidParameters()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RxLongMapPenalty.Multiplier(double.NaN, 0.15));
            Assert.Throws<ArgumentOutOfRangeException>(() => RxLongMapPenalty.Multiplier(300, 0.6));
        }
    }
}
