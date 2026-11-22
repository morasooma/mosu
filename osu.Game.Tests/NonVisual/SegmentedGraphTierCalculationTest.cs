using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SegmentedGraphTierCalculationTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(6)]
        public void TestIntegerTiersMatchOriginal(int tierCount)
        {
            check(Array.Empty<int>(), tierCount);
            check(new[] { 0, 0, 0 }, tierCount);
            check(new[] { -20, -10, -1 }, tierCount);
            check(new[] { int.MinValue, -1, 0, 1, int.MaxValue }, tierCount);
            var random = new Random(583);
            check(Enumerable.Range(0, 30000).Select(_ => random.Next(-10000, 10000)).ToArray(), tierCount);
        }

        [Test]
        public void TestFloatingPointTiersMatchOriginal()
        {
            check(new[] { -1.5f, 0f, 0.25f, 1f, 8.25f }, 6);
            check(new[] { float.NaN, float.NegativeInfinity, 0f, float.PositiveInfinity }, 6);
            check(new[] { -1.5, 0.0, 0.25, 1.0, double.MaxValue }, 6);
        }

        [Test]
        public void TestConversionDoesNotBoxEachValue()
        {
            int[] values = Enumerable.Range(0, 30000).ToArray();
            using var graph = new SegmentedGraph<int>(6);
            var calculate = typeof(SegmentedGraph<int>).GetMethod("recalculateTiers", BindingFlags.Instance | BindingFlags.NonPublic)!
                                                       .CreateDelegate<Action<int[]>>(graph);
            calculate(values);
            long start = GC.GetAllocatedBytesForCurrentThread();
            calculate(values);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            // Two arrays of four-byte elements, with room for array headers.
            Assert.That(allocated, Is.LessThan(values.Length * 8L + 1024));
        }

        [TestCase(0f)]
        [TestCase(4f)]
        [TestCase(100f)]
        public void TestVisibleSegmentsMatchOriginal(float width) => SegmentCopyProbe.Check(width);

        private partial class SegmentCopyProbe : SegmentedGraph<int>
        {
            public static void Check(float width)
            {
                var manager = new SegmentManager(1);
                manager.Add(0, 0, 0);
                manager.Add(0, 0, 0.25f);
                manager.Add(0, 0.25f, 0.5f);
                manager.Add(0, 0.5f, 1f);
                var result = new List<SegmentInfo>();
                manager.CopyVisibleTo(result, width);
                Assert.That(result, Is.EqualTo(manager.Where(s => s.Length * width > 1).ToArray()));
            }
        }

        private static void check<T>(T[] values, int tierCount)
            where T : struct, IComparable<T>, IConvertible, IEquatable<T>
        {
            float[] converted = values.Select(v => Convert.ToSingle(v)).ToArray();
            if (converted.Length > 0)
            {
                float min = converted.Min();
                if (min < 0)
                {
                    for (int i = 0; i < converted.Length; i++)
                        converted[i] += Math.Abs(min);
                }
                float max = converted.Max();
                for (int i = 0; i < converted.Length; i++)
                    converted[i] /= max;
            }
            int[] expected = converted.Select(v => (int)Math.Floor(v * tierCount)).ToArray();
            using var graph = new SegmentedGraph<T>(tierCount);
            typeof(SegmentedGraph<T>).GetMethod("recalculateTiers", BindingFlags.Instance | BindingFlags.NonPublic)!
                                     .Invoke(graph, new object[] { values });
            var actual = (int[])typeof(SegmentedGraph<T>).GetField("tiers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(graph)!;
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
