// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class RxCsGainTest
    {
        [TestCase(3f, 3f)]
        [TestCase(4.5f, 4.5f)]
        [TestCase(5f, 5f)]
        [TestCase(6.2f, 5f)] // a high-CS map is rated from CS 5
        public void TestReferenceCs(float nativeCs, float expected)
            => Assert.That(RxCsGain.ReferenceCs(nativeCs), Is.EqualTo(expected));

        [TestCase(4f, 5.2f)] // HR's own CS raise is not a gain
        [TestCase(6.2f, 6.5f)] // a high-CS map with HR: CS 5 × 1.3
        [TestCase(8f, 6.5f)]
        public void TestReferenceCsWithHardRock(float nativeCs, float expected)
            => Assert.That(RxCsGain.ReferenceCs(nativeCs, true), Is.EqualTo(expected).Within(1e-5));

        [Test]
        public void TestEasyMapKeepsItsReferencePp()
            // 300 PP at its own CS, 4.8× with DA CS 8: the gain is taken back completely.
            => Assert.That(4.8 * RxCsGain.Multiplier(4.8, 300), Is.EqualTo(1).Within(1e-12));

        [Test]
        public void TestHardMapKeepsTheWholeGain()
            => Assert.That(RxCsGain.Multiplier(3.2, RxCsGain.PP_FULL), Is.EqualTo(1));

        [Test]
        public void TestHarderMapsKeepMoreOfTheGain()
        {
            double easy = 3 * RxCsGain.Multiplier(3, 900);
            double middle = 3 * RxCsGain.Multiplier(3, 1400);

            Assert.Multiple(() =>
            {
                Assert.That(easy, Is.GreaterThan(1));
                Assert.That(middle, Is.GreaterThan(easy));
                Assert.That(middle, Is.LessThan(3));
            });
        }

        [TestCase(1.0)]
        [TestCase(0.7)] // bigger circles are CS Reduction's business
        public void TestNoGainNoChange(double gain)
            => Assert.That(RxCsGain.Multiplier(gain, 300), Is.EqualTo(1));
    }
}
