// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
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
    }
}
