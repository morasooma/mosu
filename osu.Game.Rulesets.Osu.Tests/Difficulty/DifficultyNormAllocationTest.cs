using System;
using NUnit.Framework;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class DifficultyNormAllocationTest
    {
        private static double sink;
        [Test]
        public void TestFixedArityNormMatchesArrayAccumulation()
        {
            double[] values = { 0.0, -0.0, double.Epsilon, -double.Epsilon, 1e-200, 1e200, -1, 1, double.PositiveInfinity, double.NegativeInfinity, double.NaN };
            foreach (double exponent in new[] { 1.0, 1.1, 1.2, 1.5, 2.0, 3.0 })
            foreach (double first in values)
            foreach (double second in values)
            foreach (double third in values)
            {
                assertSame(DiffUtils.Norm(exponent, new[] { first, second }), DiffUtils.Norm(exponent, first, second));
                assertSame(DiffUtils.Norm(exponent, new[] { first, second, third }), DiffUtils.Norm(exponent, first, second, third));
            }
            for (int i = 0; i < 100; i++)
                GC.KeepAlive(DiffUtils.Norm(1.2, 2.0, 3.0, 4.0));
            long start = GC.GetAllocatedBytesForCurrentThread();
            double sum = 0;
            for (int i = 0; i < 1000; i++)
            {
                sum += DiffUtils.Norm(1.2, 2.0, 3.0);
                sum += DiffUtils.Norm(1.5, 2.0, 3.0, 4.0);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            sink = sum;
            Assert.That(allocated, Is.Zero);
        }

        private static void assertSame(double expected, double actual)
        {
            if (double.IsNaN(expected))
                Assert.That(actual, Is.NaN);
            else
                Assert.That(BitConverter.DoubleToInt64Bits(actual), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected)));
        }
    }
}
