// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class RxCsReductionTest
    {
        [TestCase(4, 4)] // NM
        [TestCase(4, 5.2)] // HR / DA raised CS: the CS PP Buff handles it
        [TestCase(2.6, 2.6)] // a map whose own CS is low is not touched
        public void TestNoPenaltyWithoutBiggerCircles(double nativeCs, double playedCs)
            => Assert.That(RxCsReduction.Multiplier(nativeCs, playedCs, RxCsReduction.DEFAULT_STRENGTH), Is.EqualTo(1));

        [Test]
        public void TestLowerCsCostsMore()
        {
            double cs3 = RxCsReduction.Multiplier(4, 3, RxCsReduction.DEFAULT_STRENGTH);
            double cs2 = RxCsReduction.Multiplier(4, 2, RxCsReduction.DEFAULT_STRENGTH);

            Assert.Multiple(() =>
            {
                Assert.That(cs3, Is.LessThan(1));
                Assert.That(cs2, Is.LessThan(cs3));
                // radius 36.48 → 45.44 px: 0.803^1.6
                Assert.That(cs2, Is.EqualTo(System.Math.Pow((54.4 - 4.48 * 4) / (54.4 - 4.48 * 2), 1.6)).Within(1e-12));
            });
        }

        [Test]
        public void TestZeroStrengthDisablesRule()
            => Assert.That(RxCsReduction.Multiplier(4, 1, 0), Is.EqualTo(1));
    }
}
