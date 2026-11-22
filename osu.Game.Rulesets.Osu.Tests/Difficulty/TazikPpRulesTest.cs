using System;
using TazikPpRxCs;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class TazikPpRulesTest
    {
        [TestCase(1.5)]
        [TestCase(0.75)]
        [TestCase(2.0)]
        public void LockedDifficultyPreservesPlayedWindows(double rate)
        {
            float ar = RxLockedDifficulty.Ar(10, rate);
            double preempt = ar < 5 ? 1800 - 120 * ar : 1950 - 150 * ar;
            Assert.That(preempt / rate, Is.EqualTo(450).Within(0.001));
            Assert.That((80 - 6 * RxLockedDifficulty.Od(10, rate)) / rate, Is.EqualTo(20).Within(0.001));
        }

        [TestCase(1, 0.88)]
        [TestCase(2, 0.94)]
        [TestCase(3, 1)]
        public void SpikeFillerPenaltyIsLiftedForMultipleMoments(int moments, double expected)
            => Assert.That(RxSpikeFillerGuard.Apply(0.88, 1, moments), Is.EqualTo(expected).Within(1e-12));

        [TestCase(100, 1)]
        [TestCase(100, 1.5)]
        public void ImportedControlRewardsTwoPointPattern(double gap, double rate)
        {
            // Characterisation, not an anti-abuse guarantee: retain the upstream balance
            // and make its known two-point reward visible rather than silently fixing it.
            RxBeatmap beatmap = twoPointMap(gap);
            double control = RxAimControl.ControlPp(beatmap, rate);
            double guardedBaseSs = 10;
            double afterControl = guardedBaseSs * RxAimControl.Multiplier(control, guardedBaseSs);
            double minimumAfterRp = afterControl * RxRespectPoints.MULTIPLIER_MIN;
            TestContext.WriteLine($"Two-point: gap={gap}, rate={rate}, control={control:F2}, minimum SS after RP={minimumAfterRp:F2}");
            Assert.That(control, Is.GreaterThan(700 * rate * rate));
            Assert.That(minimumAfterRp, Is.GreaterThan(500));
        }

        [Test]
        public void ImportedControlUsesHardMapTimeThreshold()
        {
            Assert.That(RxAimControl.ControlPp(twoPointMap(110), 1), Is.GreaterThan(0));
            Assert.That(RxAimControl.ControlPp(twoPointMap(111), 1), Is.Zero);
            Assert.That(RxAimControl.ControlPp(twoPointMap(120), 1.5), Is.Zero);
        }

        [Test]
        public void RpCorrectionRemainsBounded()
        {
            Assert.That(RxRespectPoints.Multiplier(1, 1000), Is.EqualTo(0.8));
            Assert.That(RxRespectPoints.Multiplier(1000, 1), Is.EqualTo(1));
            Assert.That(RxRespectPoints.Multiplier(1000, 600), Is.EqualTo(1.2));
            Assert.That(RxRespectPoints.Multiplier(0, 1), Is.EqualTo(1));
        }

        private static RxBeatmap twoPointMap(double gap)
        {
            var beatmap = new RxBeatmap { CircleSize = 4, ApproachRate = 10, OverallDifficulty = 10 };
            float spacing = (float)(54.4 - 4.48 * beatmap.CircleSize);
            for (int i = 0; i < 1200; i++)
                beatmap.HitObjects.Add(new RxHitObject
                {
                    Kind = RxHitObjectKind.Circle,
                    StartTime = i * gap,
                    Position = new RxVec2(i % 2 * spacing, 100),
                });
            return beatmap;
        }
    }
}
