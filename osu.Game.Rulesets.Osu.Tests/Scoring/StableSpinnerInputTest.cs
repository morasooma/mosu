using System;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.Scoring
{
    [TestFixture]
    public class StableSpinnerInputTest
    {
        [TestCase(60)]
        [TestCase(30)]
        public void ConstantInputHasExpectedVelocity(double hz)
        {
            var input = new StableSpinnerInput();
            double interval = 1000 / hz;
            for (int i = 0; i < 1000; i++)
                input.Sample(2 * Math.PI * 4 * interval / 1000, interval, 1, true);
            Assert.That(input.TargetVelocity, Is.EqualTo(2 * Math.PI * 4 / 1000).Within(1e-10));
        }

        [Test]
        public void UsesStableFixedDivisorForFastSamples()
        {
            var input = new StableSpinnerInput();
            input.Sample(0.2, 8, 1, true);
            Assert.That(input.TargetVelocity, Is.EqualTo(0.2 / (1000d / 60)).Within(1e-12));
        }

        [Test]
        public void StationaryInputDecaysInTwoSamples()
        {
            var input = new StableSpinnerInput();
            input.Sample(0.2, 1000d / 60, 1, true);
            double initial = input.TargetVelocity;
            input.Sample(0, 1000d / 60, 1, true);
            Assert.That(input.TargetVelocity, Is.EqualTo(initial / 3));
            input.Sample(0, 1000d / 60, 1, true);
            Assert.That(input.TargetVelocity, Is.Zero);
        }

        [TestCase(240, 240)]
        [TestCase(600, 477.464829275686)]
        public void PhysicsRpmMatchesInputAndStableCap(double inputRpm, double expectedRpm)
        {
            var state = new StableSpinnerState(10000, 5);
            for (int t = 1; t <= 9000; t++)
                state.Advance(t, 1, inputRpm * Math.PI * 2 / 60000);
            Assert.That(state.SpinsPerMinute, Is.EqualTo(expectedRpm).Within(0.001));
        }
    }
}
