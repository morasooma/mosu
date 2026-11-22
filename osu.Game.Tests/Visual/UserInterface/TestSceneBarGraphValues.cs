using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneBarGraphValues : OsuTestScene
    {
        private BarGraph graph = null!;

        [SetUp]
        public void SetUp() => Schedule(() => Child = graph = new BarGraph
        {
            RelativeSizeAxes = Axes.Both,
        });

        [TestCase(false)]
        [TestCase(true)]
        public void TestValuesAreEnumeratedOnce(bool explicitMaximum)
        {
            int enumerations = 0;
            float[] values = { -2, 0, 5, 10 };
            IEnumerable<float> enumerate()
            {
                enumerations++;
                foreach (float value in values)
                    yield return value;
            }

            AddStep("set deferred values", () =>
            {
                graph.MaxValue = explicitMaximum ? 20 : null;
                graph.Values = enumerate();
            });
            AddAssert("enumerated once", () => enumerations, () => Is.EqualTo(1));
            AddAssert("normalization preserved", () => snapshot("finalLengths"), () => Is.EqualTo(normalize(values, explicitMaximum ? 20 : null)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestNormalizationMatchesOriginalAndDoesNotMutateInput(bool deferred)
        {
            float[][] cases =
            {
                Array.Empty<float>(),
                new[] { 0f, -0f, 0f },
                new[] { -10f, -1f, -5f },
                new[] { -10f, 0f, 2.5f, 10f },
                new[] { float.NaN, 1f, float.NaN, 2f },
                new[] { float.NaN, float.NaN },
                new[] { BitConverter.Int32BitsToSingle(unchecked((int)0x7fc00001)), BitConverter.Int32BitsToSingle(unchecked((int)0xffc00002)) },
                new[] { -0f, 0f },
                new[] { float.NegativeInfinity, 0f, float.PositiveInfinity },
                new[] { float.Epsilon, float.MaxValue },
            };
            float?[] maxima = { null, 0, -1, 4, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            foreach (float[] values in cases)
            {
                foreach (float? maximum in maxima)
                {
                    float[] source = values.ToArray();
                    int[] original = bits(source);
                    IEnumerable<float> input = deferred ? source.Select(value => value) : source;
                    float[] expected = normalize(input, maximum);
                    AddStep($"set {source.Length} values, max {maximum}", () =>
                    {
                        graph.MaxValue = maximum;
                        graph.Values = input;
                    });
                    AddAssert("exact normalized float bits", () => bits(snapshot("finalLengths")), () => Is.EqualTo(bits(expected)));
                    AddAssert("source unchanged", () => bits(source), () => Is.EqualTo(original));
                }
            }
        }

        [Test]
        public void TestReplacementKeepsPreviousAnimationTargetsAndClearsExcess()
        {
            AddStep("set initial values", () => graph.Values = new[] { 1f, 2f, 4f });
            AddAssert("initial targets", () => snapshot("finalLengths"), () => Is.EqualTo(new[] { 0.25f, 0.5f, 1f }));
            AddStep("replace with fewer values", () => graph.Values = new[] { 3f, 1f });
            AddAssert("previous targets become animation start", () => snapshot("initialLengths"), () => Is.EqualTo(new[] { 0.25f, 0.5f }));
            AddAssert("new animation target", () => snapshot("finalLengths"), () => Is.EqualTo(new[] { 1f, 1f / 3 }));
            AddAssert("excess instantaneous bar removed", () => snapshot("InstantaneousLengths").Length, () => Is.EqualTo(2));
            AddStep("clear values", () => graph.Values = Array.Empty<float>());
            AddAssert("all animation arrays cleared", () => snapshot("initialLengths").Length + snapshot("finalLengths").Length + snapshot("InstantaneousLengths").Length,
                () => Is.Zero);
            AddStep("repopulate", () => graph.Values = new[] { 7f });
            AddAssert("starts from zero after clear", () => snapshot("initialLengths"), () => Is.EqualTo(new[] { 0f }));
            AddAssert("full height target", () => snapshot("finalLengths"), () => Is.EqualTo(new[] { 1f }));
        }

        private static float[] normalize(IEnumerable<float> values, float? maximum)
        {
            if (!values.Any())
                return Array.Empty<float>();

            float max = maximum ?? values.Max();
            return values.Select(value => max == 0 ? 0 : Math.Max(0f, value / max)).ToArray();
        }

        private static int[] bits(float[] values) => values.Select(BitConverter.SingleToInt32Bits).ToArray();

        private float[] snapshot(string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object bars = typeof(BarGraph).GetField("bars", flags)!.GetValue(graph)!;
            return ((IEnumerable<float>)(bars.GetType().GetField(name, flags)?.GetValue(bars)
                                        ?? bars.GetType().GetProperty(name)!.GetValue(bars))!).ToArray();
        }
    }
}
