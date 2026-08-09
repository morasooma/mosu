// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class ManiaAntiAbuseTest
    {
        [Test]
        public void TestLowOverallDifficultyIsPenalised()
        {
            double od0 = ManiaDifficultyCalculator.CalculateOverallDifficultyPenalty(0);
            double od3 = ManiaDifficultyCalculator.CalculateOverallDifficultyPenalty(3);

            Assert.Multiple(() =>
            {
                Assert.That(od0, Is.LessThan(od3));
                Assert.That(od3, Is.LessThan(1));
                Assert.That(ManiaDifficultyCalculator.CalculateOverallDifficultyPenalty(5), Is.EqualTo(1));
                Assert.That(ManiaDifficultyCalculator.CalculateOverallDifficultyPenalty(10), Is.EqualTo(1));
            });
        }

        [Test]
        public void TestVibroMapPenaltyIgnoresSmallIsolatedSection()
        {
            Assert.That(ManiaDifficultyCalculator.CalculateVibroMapPenalty(0.40), Is.EqualTo(1));
        }

        [Test]
        public void TestVibroMapPenaltyCapsSustainedVibroMap()
        {
            Assert.That(ManiaDifficultyCalculator.CalculateVibroMapPenalty(0.45), Is.EqualTo(0.56).Within(0.0001));
            Assert.That(ManiaDifficultyCalculator.CalculateVibroMapPenalty(1), Is.EqualTo(0.56).Within(0.0001));
        }

        [Test]
        public void TestShortVibroBurstIsNotPenalised()
        {
            Assert.That(ManiaDifficultyHitObject.CalculateVibroPenalty(500), Is.EqualTo(1));
        }

        [Test]
        public void TestSustainedVibroIsProgressivelyPenalised()
        {
            double shortPack = ManiaDifficultyHitObject.CalculateVibroPenalty(700);
            double mediumPack = ManiaDifficultyHitObject.CalculateVibroPenalty(1100);
            double longPack = ManiaDifficultyHitObject.CalculateVibroPenalty(1500);

            Assert.Multiple(() =>
            {
                Assert.That(shortPack, Is.LessThan(1));
                Assert.That(mediumPack, Is.LessThan(shortPack));
                Assert.That(longPack, Is.LessThan(mediumPack));
                Assert.That(longPack, Is.EqualTo(0.1).Within(0.0001));
                Assert.That(ManiaDifficultyHitObject.CalculateVibroPenalty(10000), Is.EqualTo(longPack));
            });
        }

        [Test]
        public void TestRepeatedFourKeyChordRowsAreDetected()
        {
            double duration = ManiaDifficultyHitObject.CalculateNextVibroDuration(632, 79, 4, 0b1111, false, 0, 4, 0b1111, false, 0);

            Assert.That(duration, Is.EqualTo(711));
        }

        [Test]
        public void TestAlternatingDisjointChordRowsAreDetected()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(79, 2, 0b0011, false, 0, 2, 0b1100, false, 0), Is.True);
        }

        [Test]
        public void TestSparseRowResetsVibroStreak()
        {
            double duration = ManiaDifficultyHitObject.CalculateNextVibroDuration(1200, 200, 4, 0b1111, false, 0, 4, 0b1111, false, 0);

            Assert.That(duration, Is.Zero);
        }

        [Test]
        public void TestRepeatedFourKeyRowsAtOneHundredTwentyMillisecondsAreDetected()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(120, 4, 0b1111, false, 0, 4, 0b1111, false, 0), Is.True);
        }

        [Test]
        public void TestOrdinarySingleNoteStreamIsNotVibro()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(26, 1, 0b0001, false, 0, 1, 0b0010, false, 0), Is.False);
        }

        [Test]
        public void TestMicroHoldVibroIsDetected()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(26, 1, 0b0001, true, 52, 1, 0b0010, false, 0), Is.True);
        }

        [Test]
        public void TestSeparatelyMappedSlowMicroHoldVibroIsDetected()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(32.5, 1, 0b0001, true, 65, 1, 0b0010, false, 0), Is.True);
        }

        [Test]
        public void TestRateAdjustmentDoesNotReduceSourceVibroDuration()
        {
            double duration = ManiaDifficultyHitObject.CalculateNextVibroDuration(
                1000, 20, 26,
                1, 0b0001, true, 52,
                1, 0b0010, false, 0);

            Assert.That(duration, Is.EqualTo(1026));
        }

        [Test]
        public void TestNormalLengthHoldIsNotVibro()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(26, 1, 0b0001, true, 300, 1, 0b0010, false, 0), Is.False);
        }

        [Test]
        public void TestOrdinaryShortLnStreamIsNotVibro()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(75, 1, 0b0001, true, 75, 1, 0b0010, false, 0), Is.False);
        }

        [Test]
        public void TestOrdinaryChordLnStreamIsNotVibro()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(75, 2, 0b0011, true, 75, 2, 0b1100, false, 0), Is.False);
        }

        [Test]
        public void TestOverlappingDifferentChordsAreNotVibro()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(79, 2, 0b0011, false, 0, 2, 0b0110, false, 0), Is.False);
        }

        [Test]
        public void TestLongHoldChordRowsAreNotVibro()
        {
            Assert.That(ManiaDifficultyHitObject.IsVibroRowTransition(79, 4, 0b1111, true, 500, 4, 0b1111, false, 0), Is.False);
        }

        [Test]
        public void TestVibroPenaltyRequiresMapWideCoverage()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ManiaDifficultyCalculator.ShouldApplyVibroPenalty(0.40, 5000), Is.False);
                Assert.That(ManiaDifficultyCalculator.ShouldApplyVibroPenalty(0.60, 1499), Is.False);
                Assert.That(ManiaDifficultyCalculator.ShouldApplyVibroPenalty(0.60, 1500), Is.True);
            });
        }
    }
}
