// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class RxRespectPointsMultiplierTest
    {
        [Test]
        public void TestEasyMapGetsNoRpBonus()
            // 200 PP with RP 500 (2.5×): no bonus at all.
            => Assert.That(RxRespectPoints.Multiplier(500, 200), Is.EqualTo(1));

        [Test]
        public void TestHardMapKeepsTheFullBonus()
            => Assert.That(RxRespectPoints.Multiplier(2000, 1000), Is.EqualTo(RxRespectPoints.MULTIPLIER_MAX));

        [Test]
        public void TestPenaltyIsNotGated()
            => Assert.That(RxRespectPoints.Multiplier(100, 200), Is.EqualTo(RxRespectPoints.MULTIPLIER_MIN));

        [Test]
        public void TestBonusGrowsWithTheMapsPp()
        {
            double low = RxRespectPoints.Multiplier(2 * 350, 350);
            double high = RxRespectPoints.Multiplier(2 * 550, 550);

            Assert.Multiple(() =>
            {
                Assert.That(low, Is.GreaterThan(1));
                Assert.That(high, Is.GreaterThan(low));
                Assert.That(high, Is.LessThan(RxRespectPoints.MULTIPLIER_MAX));
            });
        }
    }
}
