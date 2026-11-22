// Adapted from taziksfear’s TazikPp 1.0.0 (PR #5, 60690a2, cache 40).

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class TazikPpAccuracyGateTest
    {
        [TestCase(0)]
        [TestCase(150)]
        [TestCase(RxAccuracyGate.SKILL_START)]
        public void TestEasyMapKeepsTheFloor(double skillPp)
            => Assert.That(RxAccuracyGate.Multiplier(skillPp), Is.EqualTo(RxAccuracyGate.FLOOR).Within(1e-12));

        [TestCase(RxAccuracyGate.SKILL_FULL)]
        [TestCase(3000)]
        public void TestHardMapKeepsAllAccuracy(double skillPp)
            => Assert.That(RxAccuracyGate.Multiplier(skillPp), Is.EqualTo(1).Within(1e-12));

        [Test]
        public void TestMonotone()
        {
            double previous = 0;

            for (double skill = 0; skill <= 500; skill += 10)
            {
                double multiplier = RxAccuracyGate.Multiplier(skill);
                Assert.That(multiplier, Is.GreaterThanOrEqualTo(previous));
                previous = multiplier;
            }
        }
    }
}
