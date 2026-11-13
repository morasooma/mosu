// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    /// <summary>The owner's examples of 2026-10-28 for the MosuPp 1.1 draft rules.</summary>
    [TestFixture]
    public class RxOwnerRulesTest
    {
        private static RxOwnerRules.Play play(float baseCs = 4, float cs = 4, float ar = 10, float od = 9, float baseOd = 9, double rate = 1,
                                              bool hardRock = false, bool difficultyAdjust = false, bool hidden = false)
            => new RxOwnerRules.Play(rate, baseCs, cs, ar, ar, baseOd, od, hardRock, difficultyAdjust, hidden, false);

        [TestCase(0.75, 1)]
        [TestCase(1, 1)]
        [TestCase(1.01, 1.01)]
        [TestCase(1.205, 1.055)]
        [TestCase(1.4, 1.1)]
        [TestCase(1.7, 1.12)]
        [TestCase(2, 1.14)]
        [TestCase(2.5, 1.17)]
        [TestCase(3, 1.2)]
        [TestCase(4, 1.2)]
        public void TestAdditionalRateBonus(double rate, double expected)
            => Assert.That(RxOwnerRules.RateBonus(rate), Is.EqualTo(expected).Within(1e-10));

        [TestCase(0.75, 1)]
        [TestCase(1, 1.22)]
        [TestCase(1.15, 1.22)]
        [TestCase(1.275, 1.11)]
        [TestCase(1.4, 1)]
        [TestCase(1.5, 1)]
        public void TestNomodFade(double rate, double expected)
            => Assert.That(RxOwnerRules.NomodMultiplier(rate, 1.22), Is.EqualTo(expected).Within(1e-10));

        [TestCase(1)]
        [TestCase(1.01)]
        [TestCase(1.15)]
        [TestCase(1.25)]
        [TestCase(1.4)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestRateBoundariesAreContinuous(double rate)
        {
            const double epsilon = 1e-8;
            Assert.That(Math.Abs(RxOwnerRules.RateBonus(rate + epsilon) - RxOwnerRules.RateBonus(rate - epsilon)), Is.LessThan(1e-6));
            if (rate > 1) // The existing HT policy is separate; only the new fade is continuous across both sides.
                Assert.That(Math.Abs(RxOwnerRules.NomodMultiplier(rate + epsilon, 1.22) - RxOwnerRules.NomodMultiplier(rate - epsilon, 1.22)), Is.LessThan(1e-6));
        }

        [Test]
        public void TestHardRockCs7()
            => Assert.That(RxOwnerRules.CsRaise(play(baseCs: 5.4f, cs: 7, hardRock: true)), Is.EqualTo(1.0828).Within(1e-9));

        [Test]
        public void TestDifficultyAdjustCsIsHardRock()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RxOwnerRules.CsRaise(play(baseCs: 5.4f, cs: 7, difficultyAdjust: true)), Is.EqualTo(1.0828).Within(1e-9));
                Assert.That(RxOwnerRules.CsRaise(play(baseCs: 4, cs: 3, difficultyAdjust: true)), Is.EqualTo(1));
                Assert.That(RxOwnerRules.CsRaise(play(cs: 7)), Is.EqualTo(1));
            });
        }

        [TestCase(8, 1.03)]
        [TestCase(10, 1.01)]
        [TestCase(11, 1.005)]
        [TestCase(12, 1.0)]
        [TestCase(-10, 1.19)]
        public void TestHidden(float ar, double expected)
            => Assert.That(RxOwnerRules.Hidden(play(ar: ar, hidden: true)), Is.EqualTo(expected).Within(1e-6));

        [Test]
        public void TestHiddenNeedsHidden() => Assert.That(RxOwnerRules.Hidden(play(ar: 8)), Is.EqualTo(1));

        [TestCase(10f, 11f, 1.07)]
        [TestCase(10f, 10.5f, 1.035)]
        [TestCase(8f, 10f, 1.0)]
        [TestCase(9f, 8f, 0.93)]
        [TestCase(10f, 7f, 0.79)]
        public void TestOd(float baseOd, float od, double expected)
            => Assert.That(RxOwnerRules.Od(play(od: od, baseOd: baseOd)), Is.EqualTo(expected).Within(1e-9));

        [Test]
        public void TestStreamExample()
        {
            // 330 BPM, spacing 1.5, three loaded parts (the longest 40 s), CS 4 → 8: 4.455% + 2.97% + 3.2%.
            var shape = new RxOwnerShape.Shape(180_000, 0, 1.5, 330, 330, 0, Array.Empty<double>(), Array.Empty<RxOwnerShape.Block>());
            var load = new RxLoadSections.Result(new double[3], new double[3], new double[] { 40, 20, 15 });

            Assert.That(RxOwnerRules.Streams(shape, load, play(cs: 8), 1), Is.EqualTo(1 + (4.455 + 2.97 + 3.2) / 100).Within(1e-9));
        }

        [Test]
        public void TestSliders()
        {
            var shape = new RxOwnerShape.Shape(60_000, 0.5, 0, 0, 180, 0, Array.Empty<double>(), Array.Empty<RxOwnerShape.Block>());
            Assert.That(RxOwnerRules.Sliders(shape), Is.EqualTo(1.14).Within(1e-9));
        }

        [TestCase(1.0, true)]
        [TestCase(1.15, true)]
        [TestCase(1.16, false)]
        [TestCase(0.75, false)]
        public void TestNomodRate(double rate, bool expected) => Assert.That(RxOwnerRules.IsNomodRate(rate), Is.EqualTo(expected));

        [TestCase(1.0, 0.41, 0.35)]
        [TestCase(1.0, 0.65, 1.0)]
        [TestCase(0.8, 0.41, 1.0)]
        [TestCase(0.875, 0.41, 0.675)]
        public void TestOverlappingStreamMap(double tightStreams, double spaced, double expected)
        {
            var shape = new RxOwnerShape.Shape(60_000, 0, 1, 300, 300, 0, Array.Empty<double>(), Array.Empty<RxOwnerShape.Block>(), spaced, tightStreams);
            Assert.That(RxOwnerRules.OverlappingStreamMap(shape), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void TestRepeatsInsideLoadedParts()
        {
            // 32 notes, four blocks of the same pattern; only the first 24 notes are loaded.
            double[] times = Enumerable.Range(0, 32).Select(i => 100.0 * i + 50).ToArray();
            var blocks = new[] { 0, 8, 16, 24 }.Select(b => new RxOwnerShape.Block(b, new[] { 0, 8, 16, 24 }.Where(o => o != b).ToArray())).ToArray();
            var shape = new RxOwnerShape.Shape(3200, 0, 0, 0, 180, 0, times, blocks);

            var all = new RxLoadSections.Result(new double[] { 0 }, new double[] { 3200 }, new double[] { 3.2 });
            var part = new RxLoadSections.Result(new double[] { 0 }, new double[] { 2400 }, new double[] { 2.4 });

            Assert.Multiple(() =>
            {
                Assert.That(RxOwnerRules.Repeats(shape, all), Is.EqualTo((4, 4)));
                Assert.That(RxOwnerRules.Repeats(shape, part), Is.EqualTo((3, 3)));
                Assert.That(RxOwnerRules.Repeats(shape, RxLoadSections.Result.EMPTY), Is.EqualTo((0, 0)));
            });
        }
    }
}
