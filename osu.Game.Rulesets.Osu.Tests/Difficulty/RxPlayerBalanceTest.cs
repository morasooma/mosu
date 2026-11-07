// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class RxPlayerBalanceTest
    {
        private static readonly RxPlayerBalance.Shape shape = new RxPlayerBalance.Shape(180_000, 0.4);

        private static readonly MosuPpBalance balance = new MosuPpBalance
        {
            LengthExponent = 0.1, SliderShareExponent = -0.5, RateExponent = -0.7, RateLevelExponent = 0.45,
        };

        [TestCase(1.0)]
        [TestCase(1.5)]
        [TestCase(0.75)]
        public void TestOffByDefault(double rate)
            => Assert.That(RxPlayerBalance.Multiplier(shape, rate, 1000, new MosuPpBalance()), Is.EqualTo(1));

        [Test]
        public void TestLengthAndSliders()
            => Assert.That(RxPlayerBalance.Multiplier(shape, 1, 1000, balance),
                Is.EqualTo(Math.Pow(180.0 / 90, 0.1) * Math.Exp(-0.5 * (0.4 - 0.2))).Within(1e-12));

        [Test]
        public void TestRateCostsMoreOnEasyMaps()
        {
            var plain = new MosuPpBalance { RateExponent = -0.7, RateLevelExponent = 0.45 };
            double easy = RxPlayerBalance.Multiplier(shape, 1.5, 300, plain), hard = RxPlayerBalance.Multiplier(shape, 1.5, 3000, plain);

            Assert.Multiple(() =>
            {
                Assert.That(easy, Is.LessThan(hard));
                Assert.That(hard, Is.LessThan(1));
                Assert.That(hard, Is.EqualTo(Math.Exp(Math.Log(1.5) * (-0.7 + 0.45 * Math.Log(2)))).Within(1e-12));
            });
        }

        [Test]
        public void TestHalfTimeAndHugeLevelNeverGain()
        {
            var plain = new MosuPpBalance { RateExponent = -0.1, RateLevelExponent = 1 };

            Assert.Multiple(() =>
            {
                Assert.That(RxPlayerBalance.Multiplier(shape, 0.75, 300, plain), Is.EqualTo(1));
                Assert.That(RxPlayerBalance.Multiplier(shape, 1.5, 100_000, plain), Is.EqualTo(1));
            });
        }

        [Test]
        public void TestLengthIsPlayedLength()
        {
            var lengthOnly = new MosuPpBalance { LengthExponent = 0.2 };

            // 180 s at rate 1.5 is played in 120 s.
            Assert.That(RxPlayerBalance.Multiplier(new RxPlayerBalance.Shape(180_000, 0.2), 1.5, 1000, lengthOnly),
                Is.EqualTo(Math.Pow(120.0 / 90, 0.2)).Within(1e-12));
        }
    }
}
