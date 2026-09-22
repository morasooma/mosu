// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class AimAssistMotionLogicTest
    {
        [Test]
        public void TestFastLongJumpExpandsAcquisitionReach()
        {
            float radius = AimAssistController.GetMotionAwareAssistRadius(185, 1800, 95, 1);
            Assert.That(radius, Is.GreaterThanOrEqualTo(285));
        }

        [Test]
        public void TestSlowMovementDoesNotExpandAcquisitionReach()
        {
            float radius = AimAssistController.GetMotionAwareAssistRadius(185, 250, 95, 1);
            Assert.That(radius, Is.EqualTo(185).Within(0.01f));
        }

        [Test]
        public void TestDistantFutureTargetDoesNotExpandAcquisitionReach()
        {
            float radius = AimAssistController.GetMotionAwareAssistRadius(185, 1800, 400, 1);
            Assert.That(radius, Is.EqualTo(185).Within(0.01f));
        }

        [Test]
        public void TestPointAcquisitionUsesPredictedMissInsteadOfRawJumpDistance()
        {
            float nearJumpMiss = AimAssistController.GetPredictedPointMissDistance(
                new Vector2(0, 0), new Vector2(300, 40), new Vector2(1875, 0), 0.16f);
            float farJumpMiss = AimAssistController.GetPredictedPointMissDistance(
                new Vector2(0, 0), new Vector2(500, 40), new Vector2(3125, 0), 0.16f);

            Assert.Multiple(() =>
            {
                Assert.That(nearJumpMiss, Is.EqualTo(40).Within(0.01f));
                Assert.That(farJumpMiss, Is.EqualTo(40).Within(0.01f));
            });
        }

        [Test]
        public void TestLongJumpIsNotRejectedAfterTrajectoryWasAccepted()
        {
            bool shouldCorrect = AimAssistController.ShouldAttemptPointCorrection(
                rawDistance: 500,
                predictedMissDistance: 50,
                hitboxRadius: 24,
                assistanceRadius: 185,
                movingTowardTarget: true);

            Assert.That(shouldCorrect, Is.True);
        }

        [Test]
        public void TestPointCorrectionRejectsAnUnrelatedTrajectory()
        {
            bool shouldCorrect = AimAssistController.ShouldAttemptPointCorrection(
                rawDistance: 500,
                predictedMissDistance: 220,
                hitboxRadius: 24,
                assistanceRadius: 185,
                movingTowardTarget: true);

            Assert.That(shouldCorrect, Is.False);
        }

        [Test]
        public void TestPointCorrectionDoesNotFightAPlayerLeavingTheTarget()
        {
            bool shouldCorrect = AimAssistController.ShouldAttemptPointCorrection(
                rawDistance: 80,
                predictedMissDistance: 50,
                hitboxRadius: 24,
                assistanceRadius: 185,
                movingTowardTarget: false);

            Assert.That(shouldCorrect, Is.False);
        }

        [Test]
        public void TestPointIntentConfidencePrefersAnIsolatedAlignedTarget()
        {
            float confidence = AimAssistController.GetPointIntentConfidence(35, 24, 185, 0.98f, 0.9f, 0.1f);
            Assert.That(confidence, Is.GreaterThan(0.75f));
        }

        [Test]
        public void TestPointIntentConfidenceRejectsAmbiguousNeighbour()
        {
            float confidence = AimAssistController.GetPointIntentConfidence(35, 24, 185, 0.98f, 0.9f, 0.82f);
            Assert.That(confidence, Is.LessThan(0.2f));
        }

        [Test]
        public void TestBallisticPhaseDoesNotAddForwardAcceleration()
        {
            Assert.That(AimAssistController.ShouldAllowForwardCorrectionForMotion(false, 1800, 2200, 120), Is.False);
        }

        [Test]
        public void TestHomingPhaseMayRepairAnUrgentUndershoot()
        {
            Assert.That(AimAssistController.ShouldAllowForwardCorrectionForMotion(false, 900, 1500, 70), Is.True);
        }

        [Test]
        public void TestCapturedTargetNeverReceivesForwardAcceleration()
        {
            Assert.That(AimAssistController.ShouldAllowForwardCorrectionForMotion(true, 600, 1200, 50), Is.False);
        }

        [Test]
        public void TestHitboxRadiusChangesRequiredCorrectionNotAcquisitionDistance()
        {
            float predictedMiss = AimAssistController.GetPredictedPointMissDistance(
                new Vector2(0, 0), new Vector2(500, 50), new Vector2(3125, 0), 0.16f);
            Vector2 largeCircleCorrection = AimAssistController.GetTrajectoryMissCorrection(
                new Vector2(0, 0), new Vector2(500, 50), 40, new Vector2(3125, 0), 0.16f);
            Vector2 smallCircleCorrection = AimAssistController.GetTrajectoryMissCorrection(
                new Vector2(0, 0), new Vector2(500, 50), 20, new Vector2(3125, 0), 0.16f);

            Assert.Multiple(() =>
            {
                Assert.That(predictedMiss, Is.EqualTo(50).Within(0.01f));
                Assert.That(largeCircleCorrection.Length, Is.EqualTo(10).Within(0.01f));
                Assert.That(smallCircleCorrection.Length, Is.EqualTo(30).Within(0.01f));
            });
        }

        [Test]
        public void TestMissCorrectionFollowsPlayerTrajectoryInsteadOfPullingForward()
        {
            Vector2 correction = AimAssistController.GetTrajectoryMissCorrection(
                new Vector2(0, 0), new Vector2(100, 30), 20, new Vector2(1000, 0), 0.12f);

            Assert.Multiple(() =>
            {
                Assert.That(correction.X, Is.EqualTo(0).Within(0.01f));
                Assert.That(correction.Y, Is.EqualTo(10).Within(0.01f));
            });
        }

        [Test]
        public void TestTrajectoryThroughHitboxNeedsNoCorrection()
        {
            Vector2 correction = AimAssistController.GetTrajectoryMissCorrection(
                new Vector2(0, 0), new Vector2(100, 15), 20, new Vector2(1000, 0), 0.12f);

            Assert.That(correction, Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void TestTimedTrajectoryDetectsStraightLineUndershoot()
        {
            Vector2 correction = AimAssistController.GetTrajectoryMissCorrection(
                new Vector2(0, 0), new Vector2(100, 0), 20, new Vector2(500, 0), 0.1f);

            Assert.Multiple(() =>
            {
                Assert.That(correction.X, Is.EqualTo(30).Within(0.01f));
                Assert.That(correction.Y, Is.EqualTo(0).Within(0.01f));
            });
        }

        [Test]
        public void TestPointAssistStepCannotTurnPlayerMovementIntoACorner()
        {
            Vector2 rawStep = new Vector2(12, 0);
            Vector2 assistStep = AimAssistController.GetPlayerLedAssistStep(new Vector2(0, 20), rawStep, 0.8f, Vector2.Zero);
            Vector2 outputStep = rawStep + assistStep;

            Assert.Multiple(() =>
            {
                Assert.That(Vector2.Dot(assistStep, rawStep), Is.EqualTo(0).Within(0.01f));
                Assert.That(assistStep.Length, Is.LessThanOrEqualTo(rawStep.Length * 0.6f + 0.01f));
                Assert.That(Vector2.Dot(outputStep, rawStep), Is.GreaterThan(0));
            });
        }

        [Test]
        public void TestUrgentTimedUndershootCanReceiveBoundedForwardHelp()
        {
            Vector2 rawStep = new Vector2(12, 0);
            Vector2 assistStep = AimAssistController.GetPlayerLedAssistStep(new Vector2(20, 0), rawStep, 1, Vector2.Zero, true);

            Assert.That(assistStep.X, Is.GreaterThan(0));
            Assert.That(assistStep.X, Is.LessThan(rawStep.X * 0.4f));
        }

        [Test]
        public void TestForwardHelpIsDisabledAfterCapture()
        {
            Assert.That(AimAssistController.ShouldAllowForwardCorrection(new Vector2(20, 0), new Vector2(12, 0), true, false), Is.False);
        }

        [Test]
        public void TestForwardHelpNeverBrakesPlayer()
        {
            Assert.That(AimAssistController.ShouldAllowForwardCorrection(new Vector2(-20, 0), new Vector2(12, 0), false, false), Is.False);
        }

        [Test]
        public void TestSeventyPercentAssistBuildsUsefulCorrectionSmoothly()
        {
            Vector2 rawStep = new Vector2(12, 0);
            Vector2 previousStep = Vector2.Zero;
            float totalCorrection = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector2 nextStep = AimAssistController.GetPlayerLedAssistStep(new Vector2(0, 20), rawStep, 0.7f, previousStep);
                Assert.That((nextStep - previousStep).Length, Is.LessThanOrEqualTo(rawStep.Length * 0.25f));
                totalCorrection += nextStep.Y;
                previousStep = nextStep;
            }

            Assert.That(totalCorrection, Is.GreaterThan(28));
        }

        [Test]
        public void TestPointAuthorityIsNotAttenuatedByProximityTwice()
        {
            float weight = AimAssistController.GetPointCorrectionWeight(0.82f, 0.5f, 1, 1);
            Assert.That(weight, Is.EqualTo(0.41f).Within(0.001f));
        }

        [Test]
        public void TestStickyProfileAcceptsARecoverableSlightlyOffAxisTrajectory()
        {
            MosuAimAssistProfile sticky = MosuAimAssistProfile.FromStrength(0.7f);

            bool shouldActivate = AimAssistController.ShouldActivatePointTrajectory(
                predictedMissDistance: 55,
                assistanceRadius: sticky.FovRadius,
                directionAlignment: -0.2f,
                configuredIntentThreshold: (float)sticky.IntentThreshold,
                confidence: 0);

            Assert.That(shouldActivate, Is.True);
        }

        [Test]
        public void TestStickyProfileStillRejectsATrajectoryOutsideItsField()
        {
            MosuAimAssistProfile sticky = MosuAimAssistProfile.FromStrength(0.7f);

            bool shouldActivate = AimAssistController.ShouldActivatePointTrajectory(
                predictedMissDistance: sticky.FovRadius + 1,
                assistanceRadius: sticky.FovRadius,
                directionAlignment: 1,
                configuredIntentThreshold: (float)sticky.IntentThreshold,
                confidence: 1);

            Assert.That(shouldActivate, Is.False);
        }

        [Test]
        public void TestLowProfileRejectsTheSameOffAxisTrajectory()
        {
            MosuAimAssistProfile low = MosuAimAssistProfile.FromStrength(0);

            bool shouldActivate = AimAssistController.ShouldActivatePointTrajectory(
                predictedMissDistance: 55,
                assistanceRadius: low.FovRadius,
                directionAlignment: -0.2f,
                configuredIntentThreshold: (float)low.IntentThreshold,
                confidence: 0);

            Assert.That(shouldActivate, Is.False);
        }

        [Test]
        public void TestSeventyPercentModStrengthCorrectsARecoverableMiss()
        {
            const float hitboxRadius = 32;
            const float missDistance = 55;
            float configuredStrength = (float)MosuAimAssistProfile.FromStrength(0.7f).Strength;
            float strengthFactor = AimAssistController.GetConfiguredStrengthFactor(configuredStrength);
            float authority = AimAssistController.GetPointCorrectionWeight(strengthFactor, 1, 1, 1);
            float requiredCorrection = missDistance - hitboxRadius;
            float remainingMiss = missDistance - requiredCorrection * authority;

            Assert.That(remainingMiss, Is.LessThanOrEqualTo(hitboxRadius));
        }

        [Test]
        public void TestMaximumConfiguredStrengthCanRepairARecoverableMiss()
        {
            const float hitboxRadius = 32;
            const float missDistance = 55;
            float strengthFactor = AimAssistController.GetConfiguredStrengthFactor(0.92f);
            float authority = AimAssistController.GetPointCorrectionWeight(strengthFactor, 1, 1, 1);
            float requiredCorrection = missDistance - hitboxRadius;
            float remainingMiss = missDistance - requiredCorrection * authority;

            Assert.That(remainingMiss, Is.LessThanOrEqualTo(hitboxRadius));
        }

        [Test]
        public void TestMaximumConfiguredStrengthKeepsFullAuthorityOnAlignedApproach()
        {
            float strengthFactor = AimAssistController.GetConfiguredStrengthFactor(0.92f);
            float authority = AimAssistController.GetPointCorrectionWeight(strengthFactor, 1, 1, 1);

            Assert.That(authority, Is.EqualTo(1).Within(0.001f));
        }

        [Test]
        public void TestApproachTimingStartsDuringInitialMovement()
        {
            float timing = AimAssistController.GetApproachTimingWeight(300, 32, 450);

            Assert.That(timing, Is.GreaterThan(0));
        }

        [Test]
        public void TestApproachWithinHitWindowHasFullTimingAuthority()
        {
            float timing = AimAssistController.GetApproachTimingWeight(150, 50);

            Assert.That(timing, Is.EqualTo(1).Within(0.001f));
        }

        [Test]
        public void TestApproachTimingAuthorityBuildsBeforeHitWindow()
        {
            float timing = AimAssistController.GetApproachTimingWeight(250, 50);

            Assert.That(timing, Is.GreaterThan(0));
            Assert.That(timing, Is.LessThan(1));
        }

        [Test]
        public void TestPointAssistCannotReverseItsBendInOneFrame()
        {
            Vector2 rawStep = new Vector2(12, 0);
            Vector2 previousStep = new Vector2(0, 2);
            Vector2 assistStep = AimAssistController.GetPlayerLedAssistStep(new Vector2(0, -20), rawStep, 1, previousStep);

            Assert.That(assistStep.Y, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void TestPointAssistDoesNotMoveWithoutPlayerMovement()
        {
            Vector2 assistStep = AimAssistController.GetPlayerLedAssistStep(new Vector2(0, 20), Vector2.Zero, 1);
            Assert.That(assistStep, Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void TestSliderTrackingDoesNotMoveWithoutPlayerMovement()
        {
            Vector2 assistStep = AimAssistController.GetPlayerLedTrackingStep(new Vector2(30, 20), Vector2.Zero, 1);
            Assert.That(assistStep, Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void TestSliderTrackingCannotPullAlongOrAgainstPlayerMovement()
        {
            Vector2 rawStep = new Vector2(12, 0);
            Vector2 assistStep = AimAssistController.GetPlayerLedTrackingStep(new Vector2(-30, 20), rawStep, 1, Vector2.Zero);

            Assert.Multiple(() =>
            {
                Assert.That(Vector2.Dot(assistStep, rawStep), Is.EqualTo(0).Within(0.01f));
                Assert.That(assistStep.Length, Is.LessThan(rawStep.Length));
            });
        }

        [Test]
        public void TestReleaseConvergenceDoesNotAddSidewaysMovement()
        {
            Vector2 rawStep = new Vector2(10, 2);
            Vector2 nextOffset = AimAssistController.GetPlayerLedReleaseOffset(new Vector2(8, 8), rawStep);
            Vector2 outputStep = rawStep + nextOffset - new Vector2(8, 8);

            Assert.That(Math.Abs(cross(outputStep, rawStep)), Is.LessThan(0.01f));
        }

        private static float cross(Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;
    }
}