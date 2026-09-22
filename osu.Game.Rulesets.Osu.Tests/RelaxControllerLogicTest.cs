// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using System;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.UI;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class RelaxControllerLogicTest
    {
        [Test]
        public void TestAlternateThresholdForcesFastBurstLikePatterns()
        {
            double thresholdTapWindow = RelaxController.ConvertStableBpmToGameplayTapWindow(170, 1);

            Assert.That(RelaxController.ShouldForceAlternationByThreshold(
                currentSupportsThresholdAlternation: true,
                nextSupportsThresholdAlternation: false,
                singletapFriendlyJump: false,
                effectiveGap: 83,
                nextEffectiveGap: double.PositiveInfinity,
                alternationThresholdTapWindow: thresholdTapWindow), Is.True);
        }

        [Test]
        public void TestAlternateThresholdForcesBurstEntryWhenNextNoteContinuesFastCadence()
        {
            double thresholdTapWindow = RelaxController.ConvertStableBpmToGameplayTapWindow(170, 1);

            Assert.That(RelaxController.ShouldForceAlternationByThreshold(
                currentSupportsThresholdAlternation: false,
                nextSupportsThresholdAlternation: true,
                singletapFriendlyJump: false,
                effectiveGap: 83,
                nextEffectiveGap: 83,
                alternationThresholdTapWindow: thresholdTapWindow), Is.True);
        }

        [Test]
        public void TestAlternateThresholdDoesNotForceBurstEntryWithoutFastContinuation()
        {
            double thresholdTapWindow = RelaxController.ConvertStableBpmToGameplayTapWindow(170, 1);

            Assert.That(RelaxController.ShouldForceAlternationByThreshold(
                currentSupportsThresholdAlternation: false,
                nextSupportsThresholdAlternation: true,
                singletapFriendlyJump: false,
                effectiveGap: 83,
                nextEffectiveGap: 118,
                alternationThresholdTapWindow: thresholdTapWindow), Is.False);
        }

        [Test]
        public void TestAlternateThresholdDoesNotOverrideSingletapFriendlyJumps()
        {
            double thresholdTapWindow = RelaxController.ConvertStableBpmToGameplayTapWindow(170, 1);

            Assert.That(RelaxController.ShouldForceAlternationByThreshold(
                currentSupportsThresholdAlternation: true,
                nextSupportsThresholdAlternation: false,
                singletapFriendlyJump: true,
                effectiveGap: 83,
                nextEffectiveGap: double.PositiveInfinity,
                alternationThresholdTapWindow: thresholdTapWindow), Is.False);
        }

        [Test]
        public void TestAlternateThresholdDoesNotApplyOutsideBurstLikePatterns()
        {
            double thresholdTapWindow = RelaxController.ConvertStableBpmToGameplayTapWindow(170, 1);

            Assert.That(RelaxController.ShouldForceAlternationByThreshold(
                currentSupportsThresholdAlternation: false,
                nextSupportsThresholdAlternation: false,
                singletapFriendlyJump: false,
                effectiveGap: 83,
                nextEffectiveGap: double.PositiveInfinity,
                alternationThresholdTapWindow: thresholdTapWindow), Is.False);
        }

        [Test]
        public void TestSameFingerCadenceCapForcesBurstEntryRepeat()
        {
            Assert.That(RelaxController.ShouldForceAlternationBySameFingerCadenceCap(
                defaultWouldRepeatSameAction: true,
                currentSupportsThresholdAlternation: false,
                nextSupportsThresholdAlternation: true,
                singletapFriendlyJump: false,
                elapsedSinceLastPlannedAction: 83,
                effectiveGap: 83,
                nextEffectiveGap: 83,
                cadenceWindow: 88), Is.True);
        }

        [Test]
        public void TestSameFingerCadenceCapDoesNotForceAfterWindowExpires()
        {
            Assert.That(RelaxController.ShouldForceAlternationBySameFingerCadenceCap(
                defaultWouldRepeatSameAction: true,
                currentSupportsThresholdAlternation: false,
                nextSupportsThresholdAlternation: true,
                singletapFriendlyJump: false,
                elapsedSinceLastPlannedAction: 124,
                effectiveGap: 83,
                nextEffectiveGap: 83,
                cadenceWindow: 88), Is.False);
        }

        [TestCase(2, 100, 7, 80, 16)]
        [TestCase(2, -100, 7, 80, -12)]
        [TestCase(120, 0, 7, 80, 80)]
        [TestCase(-120, 0, 7, 80, -80)]
        public void TestTimingOffsetIsBounded(double baseOffset, double sampledNoise, double variance, double hitWindow, double expected)
        {
            Assert.That(RelaxController.GetBoundedTimingOffset(baseOffset, sampledNoise, variance, hitWindow), Is.EqualTo(expected));
        }

        [Test]
        public void TestReliableProfileDoesNotRetryAnUnjudgedPress()
        {
            Assert.That(RelaxController.ShouldRetryUnjudgedPress(
                usesReliableProfile: true,
                targetWasHovered: false,
                usedSweptCrossing: false,
                usedNearTap: true,
                usedBlindTap: false), Is.False);
        }

        [Test]
        public void TestReliableProfileMayRetryARealInsidePressRejectedByNotelock()
        {
            Assert.That(RelaxController.ShouldRetryUnjudgedPress(
                usesReliableProfile: true,
                targetWasHovered: true,
                usedSweptCrossing: false,
                usedNearTap: false,
                usedBlindTap: false), Is.True);
        }

        [Test]
        public void TestReliableTimingDistributionContainsEarlyAndLateHits()
        {
            var mod = new OsuModMosuRelax { Preset = { Value = MosuRelaxPreset.Reliable } };
            double earliest = double.PositiveInfinity;
            double latest = double.NegativeInfinity;

            for (int sample = -20; sample <= 20; sample++)
            {
                double offset = RelaxController.GetBoundedTimingOffset(
                    mod.BaseOffset.Value,
                    sample / 10.0 * mod.TimingVariance.Value,
                    mod.TimingVariance.Value,
                    150);
                earliest = Math.Min(earliest, offset);
                latest = Math.Max(latest, offset);
            }

            Assert.Multiple(() =>
            {
                Assert.That(earliest, Is.LessThan(-8));
                Assert.That(latest, Is.GreaterThan(8));
                Assert.That((earliest + latest) * 0.5, Is.InRange(-3, 1));
            });
        }

        [Test]
        public void TestAimTimingFollowsArrivalOnBothSidesOfTheBeat()
        {
            double early = linkedPressTime(970);
            double late = linkedPressTime(1030);

            Assert.Multiple(() =>
            {
                Assert.That(early, Is.InRange(973, 999));
                Assert.That(late, Is.GreaterThan(1030));
                Assert.That(linkedPressTime(null), Is.EqualTo(1000));
                Assert.That(linkedPressTime(880), Is.EqualTo(1000));
                Assert.That(linkedPressTime(940), Is.GreaterThan(linkedPressTime(960)));
            });
        }

        [TestCase(0.75)]
        [TestCase(1)]
        [TestCase(1.5)]
        public void TestAimTimingKeepsRealTimeBehaviourAtDifferentRates(double rate)
        {
            foreach (double arrival in new[] { 880.0, 940, 970, 1030 })
            {
                double result = RelaxController.GetAimLinkedPressTime(1000 * rate, 1000 * rate, arrival * rate,
                    3 * rate, 0.65, 24 * rate, 80 * rate, 150 * rate);
                Assert.That(result / rate, Is.EqualTo(linkedPressTime(arrival)).Within(1e-6));
            }
        }

        [Test]
        public void TestAimTimingRespectsHitWindowAndFingerRecovery()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RelaxController.GetAimLinkedPressTime(1000, 940, 970, 3, 0.65, 24, 80, 10), Is.EqualTo(990));
                Assert.That(RelaxController.GetAimLinkedPressTime(1000, 1000, 1009, 3, 0.65, 24, 80, 10), Is.EqualTo(1010));
                Assert.That(RelaxController.GetAimLinkedPressTime(1000, 1000, 970, 3, 0.65, 24, 80, 150, 1020), Is.EqualTo(1020));
            });
        }

        private static double linkedPressTime(double? arrival)
            => RelaxController.GetAimLinkedPressTime(1000, 1000, arrival, 3, 0.65, 24, 80, 150);

        [TestCase(MosuRelaxPreset.Natural)]
        [TestCase(MosuRelaxPreset.Balanced)]
        [TestCase(MosuRelaxPreset.Reliable)]
        public void TestPresetAimInfluenceIsSmallAndPreservesTimingVariation(MosuRelaxPreset preset)
        {
            var mod = new OsuModMosuRelax { Preset = { Value = preset } };
            double coupling = RelaxController.GetAimTimingCoupling(mod.TimingVariance.Value);
            double cap = RelaxController.GetMaximumAimShift(mod.TimingVariance.Value);
            double earliest = double.PositiveInfinity;
            double latest = double.NegativeInfinity;

            // Проверяем весь диапазон разрешённого шума и раннего доведения,
            // включая стековый множитель, без случайных исходов теста.
            foreach (double scale in new[] { 1.0, 1.35 })
            foreach (double drift in new[] { -mod.DynamicDrift.Value, 0, mod.DynamicDrift.Value })
            for (int sample = -20; sample <= 20; sample++)
            for (int arrival = -120; arrival <= -25; arrival += 5)
            {
                double variance = mod.TimingVariance.Value * scale;
                double offset = RelaxController.GetBoundedTimingOffset(mod.BaseOffset.Value + drift, sample / 10.0 * variance, variance, 150);
                double planned = 1000 + offset;
                double linked = RelaxController.GetAimLinkedPressTime(1000, planned, 1000 + arrival, 3, coupling, cap, 80, 150);
                Assert.That(linked, Is.InRange(planned - cap, planned + 6));
                earliest = Math.Min(earliest, linked - 1000);
                latest = Math.Max(latest, linked - 1000);
            }

            Assert.Multiple(() =>
            {
                Assert.That(cap, Is.LessThanOrEqualTo(4.5));
                Assert.That(earliest, Is.LessThan(0));
                Assert.That(latest, Is.GreaterThan(0));
                if (preset == MosuRelaxPreset.Reliable)
                {
                    Assert.That(earliest, Is.GreaterThan(-32));
                    Assert.That(latest, Is.LessThan(30));
                }
            });
        }

        [TestCase(0)]
        [TestCase(0.5)]
        [TestCase(1)]
        public void TestDisabledMisaltStaysDisabledWhenTired(double stamina)
        {
            Assert.That(RelaxController.GetMisaltProbability(0, stamina), Is.Zero);
            Assert.That(RelaxController.GetMisaltProbability(1, stamina), Is.EqualTo(1));
        }

        [TestCase(-100)]
        [TestCase(0)]
        [TestCase(100)]
        public void TestAutomaticSliderReleaseKeepsTailAndLeavesRoomForNextNote(double noise)
        {
            double release = RelaxController.GetSliderReleaseTime(1400, 0, 30, noise, 40);
            Assert.That(release, Is.InRange(1400, 1420));
            Assert.That(RelaxController.GetSliderReleaseTime(1400, -40, 30, noise, 40), Is.EqualTo(1360));
            Assert.That(RelaxController.GetSliderReleaseTime(1400, 40, 30, noise, 40), Is.EqualTo(1440));
        }
    }
}
